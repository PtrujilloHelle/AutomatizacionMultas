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
                    Config.SaveOptions.PdfRoot,
                    Path.GetFileNameWithoutExtension(zipPath) + ".pdf");

                destPdf = EnsureUniquePath(destPdf);
                Directory.CreateDirectory(Path.GetDirectoryName(destPdf)!);
                pdfEntry.ExtractToFile(destPdf, overwrite: false);
                return destPdf;
            }
            else
            {
                string subDir = Path.Combine(
                    Config.SaveOptions.PdfRoot,
                    Path.GetFileNameWithoutExtension(zipPath));

                subDir = EnsureUniquePath(subDir);
                Directory.CreateDirectory(subDir);
                ZipFile.ExtractToDirectory(zipPath, subDir, true);
                return null;
            }
        }

        private void TryRenamePdfByContent(string pdfPath)
        {
            string firstText = ReadPdfText(pdfPath, true);
            string normalizedStart = NormalizeAggressively(firstText);

            if (Config.SaveOptions.ExcludedPdfStarts.Any(pref => normalizedStart.Contains(NormalizeAggressively(pref))))
                return;

            string fullText = ReadPdfText(pdfPath, false);
            string? plate = FindPlate(fullText);
            if (plate is null) return;

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
    }
}
