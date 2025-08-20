//DescargaDeMultas.cs
using AutomatizacionMultas.classes.configs;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;

namespace AutomatizacionMultas.classes.bots
{
    internal partial class DescargaDeMultas : SeleniumBot<DescargaDeMultasConfig>
    {
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

                Console.WriteLine("Descargando, descomprimiendo y renombrando…");
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
                if (i >= rows.Count) break;

                var row = rows[i];

                try
                {
                    ScrollIntoView(row);

                    var cells = row.FindElements(By.TagName("td"));
                    if (cells.Count <= Math.Max(Config.ScrapMapping.ColMatricula, Config.ScrapMapping.ColOrganismo)) { i++; continue; }

                    string organismo = SafeText(cells[Config.ScrapMapping.ColOrganismo].Text);
                    if (string.IsNullOrWhiteSpace(organismo)) organismo = "SIN_ORGANISMO";
                    string matricula = SafeText(cells[Config.ScrapMapping.ColMatricula].Text);

                    var boton = cells[^1].FindElements(By.TagName("a")).FirstOrDefault();
                    if (boton == null) { Console.WriteLine($"⚠️  Fila {i + 1}: sin botón."); i++; continue; }

                    var prevZips = Directory.GetFiles(Config.SaveOptions.DownloadDir, "*.zip")
                                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    string? nuevoZip = null;
                    for (int attempt = 0; attempt < 2 && nuevoZip == null; attempt++)
                    {
                        if (attempt > 0) Console.WriteLine($"↻ Reintentando descarga en fila {i + 1} (intento {attempt + 1})…");
                        SimulateHumanClick(boton);
                        nuevoZip = WaitNewZip(Config.SaveOptions.DownloadDir, prevZips, TimeSpan.FromSeconds(60));
                    }

                    if (nuevoZip == null) { Console.WriteLine($"⚠️  Fila {i + 1}: descarga falló."); i++; continue; }

                    string finalName = !string.IsNullOrWhiteSpace(matricula)
                        ? $"{SanitizeFileName(organismo)}_{SanitizeFileName(matricula)}.zip"
                        : $"{SanitizeFileName(organismo)}_{secuencias.GetValueOrDefault(organismo, 1)}.zip";

                    secuencias[organismo] = secuencias.GetValueOrDefault(organismo, 1) + 1;

                    string finalZipPath = Path.Combine(Config.SaveOptions.DownloadDir, finalName);
                    MoveWithRetry(nuevoZip, finalZipPath, 10, 300);

                    string? extractedPdf = ExtractZip(finalZipPath);
                    if (extractedPdf is null) { i++; continue; }

                    // ---------- Renombrar ----------
                    if (!IsExcludedOrganism(organismo))
                    {
                        if (IsBenalmadena(organismo))
                            TryRenamePdfByContentBenalmadena(extractedPdf);
                        else
                            TryRenamePdfByContent(extractedPdf);
                    }
                    else
                    {
                        Console.WriteLine($"📄 [{Path.GetFileName(extractedPdf)}] OCR omitido (organismo excluido).");
                    }


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
    }
}
