//DescargaDeMultas.Pdf.cs
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using UglyToad.PdfPig;
using static AutomatizacionMultas.classes.bots.DescargaDeMultas_ParseFixedDateHelper;
using AutomatizacionMultas.classes.utils;                   // PlateExtractor
using static AutomatizacionMultas.classes.utils.TextUtils;  // normalizaciones

namespace AutomatizacionMultas.classes.bots
{
    internal partial class DescargaDeMultas
    {
        /* ============== ZIP → PDF ============== */

        private string? ExtractZip(string zipPath)
        {
            // Subcarpeta por fecha con guiones
            string fecha = ParseFixedDate(Config.SaveOptions.FixedDate); // "07/08/2025"
            string fechaFolder = fecha.Replace('/', '-');                // "07-08-2025"
            string dateRoot = Path.Combine(Config.SaveOptions.PdfRoot, fechaFolder);
            Directory.CreateDirectory(dateRoot);

            if (Config.SaveOptions.OnePdfPerZip)
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var pdfEntry = archive.Entries
                                      .FirstOrDefault(e => e.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
                if (pdfEntry == null)
                {
                    Console.WriteLine($"⚠️  {Path.GetFileName(zipPath)} sin PDF.");
                    return null;
                }

                string destPdf = Path.Combine(dateRoot, Path.GetFileNameWithoutExtension(zipPath) + ".pdf");
                destPdf = EnsureUniquePath(destPdf);
                Directory.CreateDirectory(Path.GetDirectoryName(destPdf)!);
                pdfEntry.ExtractToFile(destPdf, overwrite: false);
                return destPdf;
            }
            else
            {
                string subDir = Path.Combine(dateRoot, Path.GetFileNameWithoutExtension(zipPath));
                subDir = EnsureUniquePath(subDir);
                Directory.CreateDirectory(subDir);
                ZipFile.ExtractToDirectory(zipPath, subDir, true);
                return null;
            }
        }

        /* ============== RENOMBRADO POR MATRÍCULA ============== */

        private void TryRenamePdfByContent(string pdfPath)
        {
            // Si el organismo está en lista de exclusión, no intentamos OCR
            string firstText = ReadPdfText(pdfPath, firstPageOnly: true);
            if (Config.SaveOptions.ExcludedPdfStarts.Any(pref => ContainsNormalized(firstText, pref)))
                return;

            string fullText = ReadPdfText(pdfPath, firstPageOnly: false);
            string? plate = PlateExtractor.FindGeneral(fullText);
            if (plate is null) return;

            RenameWithPlate(pdfPath, plate);
        }

        private void TryRenamePdfByContentBenalmadena(string pdfPath)
        {
            string fullText = ReadPdfText(pdfPath, firstPageOnly: false);
            string? plate = PlateExtractor.FindBenalmadena(fullText);
            if (plate is null) return;

            RenameWithPlate(pdfPath, plate);
        }

        private static void RenameWithPlate(string pdfPath, string plate)
        {
            string dir = Path.GetDirectoryName(pdfPath)!;
            string baseName = Regex.Replace(Path.GetFileNameWithoutExtension(pdfPath), @"_(\d+)$", "");
            string newName = $"{baseName}_{plate}.pdf";
            string newPath = Path.Combine(dir, newName);

            if (string.Equals(pdfPath, newPath, StringComparison.OrdinalIgnoreCase)) return;

            string finalPath = newPath;
            int dup = 2;
            while (File.Exists(finalPath))
                finalPath = Path.Combine(dir, $"{baseName}_{plate}_{dup++}.pdf");

            File.Move(pdfPath, finalPath);
        }

        /* ============== LECTURA PDF ============== */

        private string ReadPdfText(string path, bool firstPageOnly)
        {
            var sb = new StringBuilder();
            using var doc = PdfDocument.Open(path);
            if (firstPageOnly) sb.Append(doc.GetPage(1).Text);
            else foreach (var p in doc.GetPages()) sb.AppendLine(p.Text);
            return sb.ToString();
        }

        /* ============== POLÍTICAS POR ORGANISMO ============== */

        private bool IsExcludedOrganism(string org) =>
            Config.SaveOptions.ExcludedPdfStarts.Any(pref => ContainsNormalized(org, pref));

        private static bool IsBenalmadena(string org) =>
            ContainsNormalized(org, "Benalmádena");

        /* ============== ESTRATEGIA CENTRALIZADA ============== */
        private void RenameDependingOnOrganism(string organismo, string pdfPath)
        {
            try
            {
                if (IsExcludedOrganism(organismo))
                {
                    Log("OCR", $"[{Path.GetFileName(pdfPath)}] OCR omitido (organismo excluido).");
                    return;
                }

                if (IsBenalmadena(organismo))
                {
                    Log("OCR", "Benalmádena detectado → OCR especial.");
                    TryRenamePdfByContentBenalmadena(pdfPath);
                }
                else
                {
                    TryRenamePdfByContent(pdfPath);
                }
            }
            catch (Exception ex)
            {
                Log("ERR", $"OCR/rename: {ex.Message}");
            }
        }
    }
}
