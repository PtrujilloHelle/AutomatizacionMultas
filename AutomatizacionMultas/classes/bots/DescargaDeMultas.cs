//DescargaDeMultas.cs

using AutomatizacionMultas.classes.configs;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UglyToad.PdfPig;

namespace AutomatizacionMultas.classes.bots
{
    internal class DescargaDeMultas : SeleniumBot<DescargaDeMultasConfig>
    {
        // ↓ Solo utilidades internas (no config):
        private static readonly Random Rnd = new();

        public DescargaDeMultas(DescargaDeMultasConfig cfg) : base(cfg)
        {
            EnsureDirectoryExists();
        }

        public void EnsureDirectoryExists()
        {
            // Directorios garantizados
            Directory.CreateDirectory(Config.SaveOptions.PdfRoot);
            Directory.CreateDirectory(Config.SaveOptions.DownloadDir);
        }

        // -----------------------------------------------------------------
        // --------------------  MÉTODOS AUXILIARES  ------------------------
        // -----------------------------------------------------------------

        string? ExtractZip(string zipPath)
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

                destPdf = EnsureUniquePath(destPdf);               // ⬅️ evita sobrescribir
                Directory.CreateDirectory(Path.GetDirectoryName(destPdf)!);
                pdfEntry.ExtractToFile(destPdf, overwrite: false);
                return destPdf;
            }
            else
            {
                string subDir = Path.Combine(
                    Config.SaveOptions.PdfRoot,
                    Path.GetFileNameWithoutExtension(zipPath));

                subDir = EnsureUniquePath(subDir);                 // ⬅️ idem para carpeta
                Directory.CreateDirectory(subDir);
                ZipFile.ExtractToDirectory(zipPath, subDir, true);
                return null;
            }
        }


        void TryRenamePdfByContent(string pdfPath)
        {
            string firstText = ReadPdfText(pdfPath, true);
            string normalizedStart = NormalizeAggressively(firstText);

            if (Config.SaveOptions.ExcludedPdfStarts.Any(pref => normalizedStart.Contains(NormalizeAggressively(pref))))
                return; // Excluido

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

        // ---- PDF helpers ----
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

        bool IsExcludedOrganism(string org) =>
            Config.SaveOptions.ExcludedPdfStarts.Any(pref =>
                NormalizeAggressively(org).Contains(NormalizeAggressively(pref)));

        private IReadOnlyList<IWebElement> GetRows() =>
            Driver.FindElements(By.CssSelector("table tbody tr")).ToList();

        private int CountRows() => GetRows().Count;

        private void WaitProcessingGone(WebDriverWait wait)
        {
            try { wait.Until(d => d.FindElements(By.CssSelector("div.dataTables_processing")) is var p && (p.Count == 0 || !p[0].Displayed)); }
            catch { }
        }

        private bool TryLoadMoreRows(ref int lastCount, int attempts = 3)
        {
            for (int j = 0; j < attempts; j++)
            {
                ((IJavaScriptExecutor)Driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                HumanPause(600, 900);
                WaitProcessingGone(Wait);

                int now = CountRows();
                if (now > lastCount) { lastCount = now; return true; }
            }
            return false;
        }

        private void EnsureAllRowsLoadedByScrolling()
        {
            int last = -1, calmRounds = 0, current = CountRows();

            while (current > last || calmRounds < 2)
            {
                if (current > last) { last = current; calmRounds = 0; }
                else calmRounds++;

                ((IJavaScriptExecutor)Driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                HumanPause(500, 800);
                WaitProcessingGone(Wait);
                current = CountRows();
            }

            ((IJavaScriptExecutor)Driver).ExecuteScript("window.scrollTo(0, 0);");
            HumanPause(300, 500);
        }

        private void SelectMostrarTodos()
        {
            WaitProcessingGone(Wait);
            var selectEl = Wait.Until(d =>
                d.FindElement(By.CssSelector("#cli_notificaciones_length select[name='cli_notificaciones_length']")));

            var sel = new SelectElement(selectEl);
            if (sel.Options.Any(o => o.Text.Trim().Equals("Todos", StringComparison.OrdinalIgnoreCase)))
                sel.SelectByText("Todos");
            else if (sel.Options.Any(o => o.GetAttribute("value") == "-1"))
                sel.SelectByValue("-1");
            else
                sel.SelectByIndex(sel.Options.Count - 1);

            HumanPause(800, 1500);
            try { Wait.Until(d => d.FindElements(By.CssSelector("table tbody tr")).Count > 15); }
            catch { }
        }

        protected override Task ExecuteAsync()
        {

            // ---------- LOGIN ----------
            LoginStep();
            HumanPause(1200, 2200);

            try
            {
                // ---------- MENÚ “NOTIFICACIONES” ----------
                SetUpFilters();

                // ---------- MOSTRAR -> TODOS ----------
                SelectMostrarTodos();

                // ---------- Scroll inicial ----------
                EnsureAllRowsLoadedByScrolling();

                Console.WriteLine("Descargando, descomprimiendo y renombrando…");

                // ---------- Download all ----------
                DownloadProcess();

                Console.WriteLine("🏁 Todo terminado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error general: {ex.Message}");
            }

            return Task.CompletedTask;

        }

        private void DownloadProcess()
        {
            var secuencias = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            int lastKnownCount = CountRows();

            while (true)
            {
                var rows = GetRows();
                if (i >= rows.Count && !TryLoadMoreRows(ref lastKnownCount)) break;

                rows = GetRows();
                if (i >= rows.Count) break; // seguridad

                var row = rows[i];

                try
                {
                    ScrollIntoView(row);

                    var cells = row.FindElements(By.TagName("td"));
                    if (cells.Count <= Math.Max(Config.ScrapMapping.ColMatricula, Config.ScrapMapping.ColOrganismo)) { i++; continue; }

                    string organismo = SafeText(cells[Config.ScrapMapping.ColOrganismo].Text);
                    if (string.IsNullOrWhiteSpace(organismo)) organismo = "SIN_ORGANISMO";
                    string matricula = SafeText(cells[Config.ScrapMapping.ColMatricula].Text);

                    var boton = cells.Last().FindElements(By.TagName("a")).FirstOrDefault();
                    if (boton == null) { Console.WriteLine($"⚠️  Fila {i + 1}: sin botón."); i++; continue; }

                    var prevZips = Directory.GetFiles(Config.SaveOptions.DownloadDir, "*.zip")
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    SimulateHumanClick(boton);

                    string nuevoZip = WaitNewZip(Config.SaveOptions.DownloadDir, prevZips, TimeSpan.FromSeconds(60));
                    if (nuevoZip == null) { Console.WriteLine($"⚠️  Fila {i + 1}: descarga falló."); i++; continue; }

                    // ---------- Nombre final ----------
                    string finalName = !string.IsNullOrWhiteSpace(matricula)
                        ? $"{SanitizeFileName(organismo)}_{SanitizeFileName(matricula)}.zip"
                        : $"{SanitizeFileName(organismo)}_{secuencias.GetValueOrDefault(organismo, 1)}.zip";

                    secuencias[organismo] = secuencias.GetValueOrDefault(organismo, 1) + 1;

                    string finalZipPath = Path.Combine(Config.SaveOptions.DownloadDir, finalName);
                    MoveWithRetry(nuevoZip, finalZipPath, 10, 300);

                    // ---------- Descomprimir ----------
                    string? extractedPdf = ExtractZip(finalZipPath);
                    if (extractedPdf is null) { i++; continue; }

                    // ---------- Renombrar ----------
                    if (!IsExcludedOrganism(organismo))
                        TryRenamePdfByContent(extractedPdf);
                    else
                        Console.WriteLine($"📄 [{Path.GetFileName(extractedPdf)}] OCR omitido (organismo excluido).");

                    Console.WriteLine($"✅ PDF listo: {Path.GetFileName(extractedPdf)}");
                    HumanPause(600, 1200);
                    i++;
                }
                catch (StaleElementReferenceException)
                {
                    Console.WriteLine($"↻ DOM cambió, reintentando fila {i + 1}…");
                    HumanPause(400, 800);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error en fila {i + 1}: {ex.Message}");
                    i++;
                }
            }
        }

        private void SetUpFilters()
        {
            // --- NAVEGACIÓN AL MENÚ ---
            SimulateHumanClick(Wait.Until(d => d.FindElement(By.XPath("//a[@title='Sedes electronicas']"))));
            HumanPause(600, 1200);

            SimulateHumanClick(Wait.Until(d => d.FindElement(By.XPath("//span[normalize-space()='Notificaciones']"))));
            HumanPause(1500, 2500);

            // --- FILTRO DE FECHA ---
            /* Si FixedDate está vacío ⇒ hoy.
               Admite tanto "dd/MM/yyyy" como "yyyy-MM-dd". */
            string fechaFija = ParseFixedDate(Config.SaveOptions.FixedDate);

            var desdeInput = Wait.Until(d => d.FindElement(By.Id("beginDateCreated")));
            var hastaInput = Wait.Until(d => d.FindElement(By.Id("endDateCreated")));

            desdeInput.Clear(); SimulateHumanTyping(desdeInput, fechaFija);
            hastaInput.Clear(); SimulateHumanTyping(hastaInput, fechaFija);
            hastaInput.SendKeys(Keys.Enter);

            HumanPause(1400, 2000);
        }

        /* Helper privado */
        private static string ParseFixedDate(string? raw)
        {
            raw = raw?.Trim();                          // ← recorte extra
            if (string.IsNullOrEmpty(raw))
                return DateTime.Today.ToString("dd/MM/yyyy");

            // dd/MM/yyyy
            if (DateTime.TryParseExact(raw, "dd/MM/yyyy", null,
                                       DateTimeStyles.None, out var dt))
                return dt.ToString("dd/MM/yyyy");

            // yyyy-MM-dd o cualquier formato reconocible
            if (DateTime.TryParse(raw, out dt))
                return dt.ToString("dd/MM/yyyy");

            // Fallback: hoy
            return DateTime.Today.ToString("dd/MM/yyyy");
        }

        private void LoginStep()
        {
            Driver.Navigate().GoToUrl(Config.PyramidConnection.Url);
            SimulateHumanTyping(Wait.Until(d => d.FindElement(By.Name("username"))), Config.PyramidConnection.Username);
            var passInput = Driver.FindElement(By.Name("password"));
            SimulateHumanTyping(passInput, Config.PyramidConnection.Password);
            passInput.SendKeys(Keys.Enter);
        }
    }
}