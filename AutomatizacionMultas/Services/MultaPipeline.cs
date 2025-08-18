using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

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
                string matricula = ExtractPlateFromFilename(pdfPath);

                var (fullText, lines) = _reader.Read(pdfPath);

                (string date, string time) dt = ("", "");   // ← inicializado
                var extractor = _extractors.FirstOrDefault(e => e.CanHandle(fileName));
                if (extractor is null || !extractor.TryExtract(fullText, lines, out dt))
                {
                    extractor = null;
                    foreach (var ex in _extractors)
                    {
                        if (ex.TryExtract(fullText, lines, out dt)) { extractor = ex; break; }
                    }
                    if (extractor is null)
                        throw new InvalidOperationException("No se pudo extraer fecha/hora con ningún extractor.");
                }

                Console.WriteLine($"{matricula},{dt.date},{dt.time},{fileName}");

                var fecha = TryParseDate(dt.date);
                var hora = TryParseTime(dt.time);
                if (!fecha.HasValue || !hora.HasValue)
                {
                    _log.LogWarning("Fecha/Hora inválidas para {File}", fileName);
                    continue;
                }

                // Consulta exacta en SQL → Sucursal/Cliente
                var matches = _repo.QueryExact(matricula, fecha.Value, hora.Value); // ver impl. abajo

                if (!matches.Any())
                {
                    Console.WriteLine("  > (sin coincidencias)");
                    continue;
                }

                int i = 0;
                foreach (var m in matches)
                {
                    i++;
                    Console.WriteLine($"  > Match {i}: Suc={m.Sucursal}, Cliente={m.CodCliente}");
                }

                var chosen = matches.First();
                var contrato = _files.FindNewestBySucursal(chosen.Sucursal);

                var destDir = _files.EnsureOutputFolder(matricula, fecha.Value, hora.Value);

                _files.CopyIfExists(pdfPath, destDir);

                if (contrato is not null)
                {
                    _files.CopyIfExists(contrato.FullName, destDir);
                    _log.LogInformation("Contrato {Contrato} copiado a {Destino}", contrato.Name, destDir.FullName);
                }
                else
                {
                    _log.LogWarning("Contrato no encontrado para sucursal {Sucursal}", chosen.Sucursal);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[ERROR] {File}", fileName);
            }
        }

        return Task.CompletedTask;
    }

    private static string ExtractPlateFromFilename(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var i = name.LastIndexOf('_');
        return (i >= 0 ? name[(i + 1)..] : name).Trim();
    }

    private static DateTime? TryParseDate(string s)
        => DateTime.TryParseExact(s?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
           ? dt.Date : (DateTime?)null;

    private static TimeSpan? TryParseTime(string s)
        => TimeSpan.TryParseExact(s?.Trim(), new[] { "hh\\:mm", "h\\:mm" }, CultureInfo.InvariantCulture, out var ts)
           ? ts : (TimeSpan?)null;
}
