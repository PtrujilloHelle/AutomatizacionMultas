using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Obtenerarchivosdelamulta.Data;
using Obtenerarchivosdelamulta.Extraction;
using Microsoft.Extensions.Logging;

namespace Obtenerarchivosdelamulta.Services;

public sealed class MultaPipeline
{
    private readonly Obtenerarchivosdelamulta.Domain.PipelineOptions _opt; // ← totalmente calificado
    private readonly PdfTextReader _reader;
    private readonly ContractRepository _repo;
    private readonly IEnumerable<IMultaExtractor> _extractors;
    private readonly ContractFileService _files;
    private readonly ILogger<MultaPipeline> _log;

    public MultaPipeline(
        Obtenerarchivosdelamulta.Domain.PipelineOptions opt, // ← totalmente calificado
        PdfTextReader reader,
        ContractRepository repo,
        IEnumerable<IMultaExtractor> extractors,
        ContractFileService files,
        ILogger<MultaPipeline> log)
    {
        _opt = opt;
        _reader = reader;
        _repo = repo;
        _extractors = extractors;
        _files = files;
        _log = log;
    }

    public Task RunAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_opt.InputDir))
        {
            _log.LogError("La carpeta de entrada no existe: {Dir}", _opt.InputDir);
            return Task.CompletedTask;
        }

        var pdfs = Directory.EnumerateFiles(_opt.InputDir, "*.pdf", SearchOption.TopDirectoryOnly).ToList();
        if (pdfs.Count == 0)
        {
            _log.LogWarning("No se encontraron PDFs en {Dir}", _opt.InputDir);
            return Task.CompletedTask;
        }

        Console.WriteLine("matricula,fecha,hora,archivo");

        foreach (var pdfPath in pdfs)
        {
            if (ct.IsCancellationRequested) break;

            var fileName = Path.GetFileName(pdfPath);
            try
            {
                var (matricula, dupSuffix) = ExtractPlateAndSuffix(pdfPath);

                var (fullText, lines) = _reader.Read(pdfPath);

                // 1) Intento de extracción de fecha/hora (sin abortar si falla)
                (string date, string time) dt = ("", "");
                var extractor = _extractors.FirstOrDefault(e => e.CanHandle(fileName));
                bool extracted = extractor != null && extractor.TryExtract(fullText, lines, out dt);
                if (!extracted)
                {
                    foreach (var ex in _extractors)
                    {
                        if (ex.TryExtract(fullText, lines, out dt)) { extracted = true; extractor = ex; break; }
                    }
                }

                Console.WriteLine($"{matricula},{dt.date},{dt.time},{fileName}");

                var fecha = TryParseDate(dt.date);
                var hora = TryParseTime(dt.time);

                // 2) Consulta SQL solo si hay fecha y hora válidas
                List<ContractMatch> matches = new();
                bool sePuedeConsultar = fecha.HasValue && hora.HasValue;
                if (sePuedeConsultar)
                {
                    matches = _repo.QueryExact(matricula, fecha!.Value, hora!.Value).ToList();
                    if (!matches.Any())
                    {
                        Console.WriteLine("  > (sin coincidencias)");
                    }
                    else
                    {
                        int i = 0;
                        foreach (var m in matches)
                        {
                            i++;
                            Console.WriteLine($"  > Match {i}: Suc={m.Sucursal}, Cliente={m.CodCliente}");
                        }
                    }
                }
                else
                {
                    // Aviso en log, pero continuamos a crear carpeta con marcadores “sin fecha/hora”
                    _log.LogWarning("No hay fecha y/o hora válidas para {File}. Se creará carpeta con marcadores.", fileName);
                }

                // 3) Elección (si hay) y copia de contrato
                var contratoNoEncontrado = !matches.Any();
                FileInfo? contrato = null;
                if (matches.Any())
                {
                    var chosen = matches.First();
                    contrato = _files.FindNewestBySucursal(chosen.Sucursal);
                    if (contrato is null)
                    {
                        _log.LogWarning("Contrato no encontrado para sucursal {Sucursal}", chosen.Sucursal);
                        contratoNoEncontrado = true;
                    }
                }

                // 4) Crear carpeta de salida con todas las reglas
                var destDir = _files.EnsureOutputFolder(
                    matricula,
                    fecha,
                    hora,
                    contratoNoEncontrado,
                    dupSuffix  // p.ej. "_2"
                );

                // 5) Copiar siempre el PDF de la multa
                _files.CopyIfExists(pdfPath, destDir);

                // 6) Copiar contrato si existe
                if (contrato is not null)
                {
                    _files.CopyIfExists(contrato.FullName, destDir);
                    _log.LogInformation("Contrato {Contrato} copiado a {Destino}", contrato.Name, destDir.FullName);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[ERROR] {File}", fileName);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Extrae la matrícula (####AAA) y un posible sufijo duplicado "_2" del nombre de archivo.
    /// Ej: "AYTO_9371MGF_2.pdf" → ("9371MGF", "_2")
    ///    "AYTO_9371MGF.pdf"   → ("9371MGF", "")
    /// Fallback: usa el trozo tras el último '_' como antes.
    /// </summary>
    private static (string plate, string dupSuffix) ExtractPlateAndSuffix(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var m = Regex.Match(name, @"(\d{4}[A-Z]{3})(?:_(\d+))?$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            string plate = m.Groups[1].Value.ToUpperInvariant();
            string dup = m.Groups[2].Success ? "_" + m.Groups[2].Value : "";
            return (plate, dup);
        }

        // Fallback al comportamiento previo (posible, pero no ideal)
        var i = name.LastIndexOf('_');
        var raw = (i >= 0 ? name[(i + 1)..] : name).Trim();
        return (raw, "");
    }

    private static DateTime? TryParseDate(string s)
        => DateTime.TryParseExact(s?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
           ? dt.Date : (DateTime?)null;

    private static TimeSpan? TryParseTime(string s)
        => TimeSpan.TryParseExact(s?.Trim(), new[] { "hh\\:mm", "h\\:mm" }, CultureInfo.InvariantCulture, out var ts)
           ? ts : (TimeSpan?)null;
}
