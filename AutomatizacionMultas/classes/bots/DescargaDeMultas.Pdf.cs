//DescargaDeMultas.Pdf.cs
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using OpenQA.Selenium;
using UglyToad.PdfPig;

namespace AutomatizacionMultas.classes.bots
{
    internal partial class DescargaDeMultas
    {
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

                string destPdf = Path.Combine(
                    dateRoot, // <- aquí va la carpeta con guiones
                    Path.GetFileNameWithoutExtension(zipPath) + ".pdf");

                destPdf = EnsureUniquePath(destPdf);
                Directory.CreateDirectory(Path.GetDirectoryName(destPdf)!);
                pdfEntry.ExtractToFile(destPdf, overwrite: false);
                return destPdf;
            }
            else
            {
                string subDir = Path.Combine(
                    dateRoot, // <- también cuelga del dateRoot
                    Path.GetFileNameWithoutExtension(zipPath));

                subDir = EnsureUniquePath(subDir);
                Directory.CreateDirectory(subDir);
                ZipFile.ExtractToDirectory(zipPath, subDir, true);
                return null;
            }
        }

        /* ===================== RENOMBRADOS ===================== */

        private void TryRenamePdfByContent(string pdfPath)
        {
            string firstText = ReadPdfText(pdfPath, true);
            string normalizedStart = NormalizeAggressively(firstText);

            if (Config.SaveOptions.ExcludedPdfStarts.Any(pref => normalizedStart.Contains(NormalizeAggressively(pref))))
                return;

            string fullText = ReadPdfText(pdfPath, false);
            string? plate = FindPlate(fullText);
            if (plate is null) return;

            RenameWithPlate(pdfPath, plate);
        }

        // *** ESPECIAL BENALMÁDENA ***
        private void TryRenamePdfByContentBenalmadena(string pdfPath)
        {
            string fullText = ReadPdfText(pdfPath, false);
            string? plate = FindPlateBenalmadena(fullText);
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

        /* ===================== LECTURA/EXTRACCIÓN ===================== */

        private string ReadPdfText(string path, bool firstPageOnly)
        {
            var sb = new StringBuilder();
            using var doc = PdfDocument.Open(path);
            if (firstPageOnly) sb.Append(doc.GetPage(1).Text);
            else foreach (var p in doc.GetPages()) sb.AppendLine(p.Text);
            return sb.ToString();
        }

        private string? FindPlate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string norm = Regex.Replace(text, @"\s+", " ").ToUpperInvariant();
            var plateCore = new Regex(@"\b\d{4}\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]{3}\b",
                                      RegexOptions.Compiled | RegexOptions.IgnoreCase);

            foreach (var key in new[] { "MATRICULA", "MATRÍCULA" })
            {
                int idx = norm.IndexOf(key, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    var mCtx = plateCore.Match(norm.Substring(idx, Math.Min(120, norm.Length - idx)));
                    if (mCtx.Success) return Regex.Replace(mCtx.Value, @"[^A-Z0-9]", "");
                    idx = norm.IndexOf(key, idx + key.Length, StringComparison.Ordinal);
                }
            }
            var m = plateCore.Match(norm);
            if (m.Success) return Regex.Replace(m.Value, @"[^A-Z0-9]", "");
            string compact = Regex.Replace(text, @"[^A-Z0-9]", "", RegexOptions.IgnoreCase).ToUpperInvariant();
            var m2 = Regex.Match(compact, @"\d{4}[BCDFGHJKLMNPRSTVWXYZ]{3}");
            return m2.Success ? m2.Value : null;
        }

        // *** EXTRACTOR ESPECÍFICO BENALMÁDENA ***
        private string? FindPlateBenalmadena(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // 1) Normaliza: sin tildes, mayúsculas, conserva espacios
            string norm = RemoveDiacriticsKeepSpaces(text).ToUpperInvariant();

            // 2) Busca por líneas la etiqueta “MATRICULA” y extrae en la misma línea
            foreach (var raw in Regex.Split(norm, @"\r?\n"))
            {
                var line = Regex.Replace(raw, @"\s+", " ");
                if (line.Contains("MATRICULA"))
                {
                    var m = Regex.Match(line, @"\b\d{4}\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]{3}\b");
                    if (m.Success) return Regex.Replace(m.Value, @"[^A-Z0-9]", "");
                }
            }

            // 3) Ventana alrededor de la primera “MATRICULA”, tolerante a separadores raros
            int idx = norm.IndexOf("MATRICULA", StringComparison.Ordinal);
            if (idx >= 0)
            {
                int start = Math.Max(0, idx - 120);
                string window = norm.Substring(start, Math.Min(800, norm.Length - start));
                var soft = Regex.Match(window,
                    @"\d\s*[-]?\s*\d\s*[-]?\s*\d\s*[-]?\s*\d\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]");
                if (soft.Success)
                {
                    string compact = Regex.Replace(soft.Value, @"[^A-Z0-9]", "");
                    if (Regex.IsMatch(compact, @"^\d{4}[BCDFGHJKLMNPRSTVWXYZ]{3}$"))
                        return compact;
                }
            }

            // 4) Fallback global estricto (texto compactado)
            string compactAll = Regex.Replace(norm, @"[^A-Z0-9]", "");
            var m3 = Regex.Match(compactAll, @"\d{4}[BCDFGHJKLMNPRSTVWXYZ]{3}");
            return m3.Success ? m3.Value : null;
        }

        /* ===================== HELPERS ===================== */

        // Quita diacríticos pero conserva espacios
        private static string RemoveDiacriticsKeepSpaces(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private string NormalizeAggressively(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string s = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in s)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            s = sb.ToString().Normalize(NormalizationForm.FormC);
            return Regex.Replace(s, @"\s+", "").ToUpperInvariant().Trim();
        }

        private bool IsExcludedOrganism(string org) =>
            Config.SaveOptions.ExcludedPdfStarts.Any(pref =>
                NormalizeAggressively(org).Contains(NormalizeAggressively(pref)));

        // Detector robusto de “Benalmádena”
        private bool IsBenalmadena(string org)
        {
            var s = RemoveDiacriticsKeepSpaces(org ?? "").ToUpperInvariant();
            s = Regex.Replace(s, @"\s+", "");
            return s.StartsWith("BENALMADENA");
        }
    }
}
