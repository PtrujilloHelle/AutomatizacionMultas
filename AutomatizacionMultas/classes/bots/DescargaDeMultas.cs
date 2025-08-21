//DescargaDeMultas.cs
using AutomatizacionMultas.classes.configs;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// 👇 para usar TextUtils.SanitizeFileName si lo deseas aquí también
using AutomatizacionMultas.classes.utils;

namespace AutomatizacionMultas.classes.bots
{
    internal partial class DescargaDeMultas : SeleniumBot<DescargaDeMultasConfig>
    {
        // Tags de log
        private const string L_DL = "DL";
        private const string L_ZIP = "ZIP";
        private const string L_OCR = "OCR";
        private const string L_OK = "OK";
        private const string L_ERR = "ERR";

        public DescargaDeMultas(DescargaDeMultasConfig cfg) : base(cfg)
        {
            EnsureDirectoryExists();
            EnsureCookieDirAndMigrate();
        }

        public void EnsureDirectoryExists()
        {
            Directory.CreateDirectory(Config.SaveOptions.PdfRoot);
            Directory.CreateDirectory(Config.SaveOptions.DownloadDir);
        }

        protected override Task ExecuteAsync()
        {
            EnsureLoggedIn();
            HumanPause(1000, 1500);

            try
            {
                SetUpFilters();
                SelectMostrarTodos();
                EnsureAllRowsLoadedByScrolling();

                Log(L_DL, "Descargando, descomprimiendo y renombrando…");
                DownloadProcess();
                Log(L_OK, "Todo terminado.");
            }
            catch (Exception ex)
            {
                Log(L_ERR, $"Error general: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /* ===================== Flujo principal ===================== */

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
                if (i >= rows.Count) break;

                var row = rows[i];

                try
                {
                    ProcessRow(rows, i, secuencias);
                    i++;
                }
                catch (StaleElementReferenceException)
                {
                    Log(L_DL, $"DOM cambió, reintentando fila {i + 1}…");
                    HumanPause(400, 800);
                }
                catch (Exception ex)
                {
                    Log(L_ERR, $"Fila {i + 1}: {ex.Message}");
                    i++;
                }
            }
        }

        /* ===================== Pasos por fila ===================== */

        private void ProcessRow(IReadOnlyList<IWebElement> rows, int index, Dictionary<string, int> secuencias)
        {
            var row = rows[index];
            ScrollIntoView(row);

            var cells = row.FindElements(By.TagName("td"));
            if (cells.Count <= Math.Max(Config.ScrapMapping.ColMatricula, Config.ScrapMapping.ColOrganismo))
            {
                Log(L_DL, $"Fila {index + 1}: columnas insuficientes, se omite.");
                return;
            }

            string organismo = SafeText(cells[Config.ScrapMapping.ColOrganismo].Text);
            if (string.IsNullOrWhiteSpace(organismo)) organismo = "SIN_ORGANISMO";

            string matricula = SafeText(cells[Config.ScrapMapping.ColMatricula].Text);

            var boton = cells[^1].FindElements(By.TagName("a")).FirstOrDefault();
            if (boton == null)
            {
                Log(L_DL, $"Fila {index + 1}: sin botón de descarga.");
                return;
            }

            // 1) Descarga ZIP
            if (!TryDownloadZip(boton, index, out string tempZip))
            {
                Log(L_ZIP, $"Fila {index + 1}: descarga falló.");
                return;
            }

            // 2) Nombrado de ZIP y move
            string finalZipPath = BuildFinalZipPath(organismo, matricula, secuencias);
            MoveWithRetry(tempZip, finalZipPath, retries: 10, delayMs: 300);
            Log(L_ZIP, $"Fila {index + 1}: ZIP movido como {Path.GetFileName(finalZipPath)}");

            // 3) Extraer PDF
            string? extractedPdf = ExtractZip(finalZipPath);
            if (extractedPdf is null)
            {
                Log(L_ZIP, $"Fila {index + 1}: ZIP sin PDF.");
                return;
            }

            // 4) Renombrar por OCR (Benalmádena o general)
            RenamePdfByOrganism(extractedPdf, organismo);

            Log(L_OK, $"PDF listo: {Path.GetFileName(extractedPdf)}");
            HumanPause(600, 1200);
        }

        /* ===================== Helpers por paso ===================== */

        private bool TryDownloadZip(IWebElement boton, int rowIndex, out string nuevoZip)
        {
            var prevZips = Directory.GetFiles(Config.SaveOptions.DownloadDir, "*.zip")
                                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            nuevoZip = string.Empty;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0) Log(L_ZIP, $"Fila {rowIndex + 1}: reintentando descarga (#{attempt + 1})…");
                SimulateHumanClick(boton);
                var path = WaitNewZip(Config.SaveOptions.DownloadDir, prevZips, TimeSpan.FromSeconds(60));
                if (!string.IsNullOrEmpty(path)) { nuevoZip = path; return true; }
            }
            return false;
        }

        private string BuildFinalZipPath(string organismo, string matricula, Dictionary<string, int> secuencias)
        {
            string orgClean = SanitizeFileName(organismo);
            string name = !string.IsNullOrWhiteSpace(matricula)
                ? $"{orgClean}_{SanitizeFileName(matricula)}.zip"
                : $"{orgClean}_{secuencias.GetValueOrDefault(organismo, 1)}.zip";

            secuencias[organismo] = secuencias.GetValueOrDefault(organismo, 1) + 1;
            return Path.Combine(Config.SaveOptions.DownloadDir, name);
        }

        private void RenamePdfByOrganism(string extractedPdf, string organismo)
        {
            try
            {
                if (!IsExcludedOrganism(organismo))
                {
                    if (IsBenalmadena(organismo))
                    {
                        Log(L_OCR, $"Benalmádena detectado → OCR especial.");
                        TryRenamePdfByContentBenalmadena(extractedPdf);
                    }
                    else
                    {
                        TryRenamePdfByContent(extractedPdf);
                    }
                }
                else
                {
                    Log(L_OCR, $"[{Path.GetFileName(extractedPdf)}] OCR omitido (organismo excluido).");
                }
            }
            catch (Exception ex)
            {
                Log(L_ERR, $"OCR/rename: {ex.Message}");
            }
        }

        private static void Log(string tag, string message) =>
            Console.WriteLine($"[{tag}] {message}");
    }
}
