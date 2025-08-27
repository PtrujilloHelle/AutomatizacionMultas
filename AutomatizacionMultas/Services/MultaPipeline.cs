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
    private readonly Obtenerarchivosdelamulta.Domain.PipelineOptions _opt;
    private readonly PdfTextReader _reader;
    private readonly ContractRepository _repo;
    private readonly IEnumerable<IMultaExtractor> _extractors;
    private readonly ContractFileService _files;
    private readonly ILogger<MultaPipeline> _log;

    public MultaPipeline(
        Obtenerarchivosdelamulta.Domain.PipelineOptions opt,
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

    public async Task RunAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_opt.InputDir))
        {
            _log.LogError("La carpeta de entrada no existe: {Dir}", _opt.InputDir);
            return;
        }

        var pdfs = Directory.EnumerateFiles(_opt.InputDir, "*.pdf", SearchOption.TopDirectoryOnly).ToList();
        if (pdfs.Count == 0)
        {
            _log.LogWarning("No se encontraron PDFs en {Dir}", _opt.InputDir);
            return;
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

                // 1) Intento de extracción de fecha/hora
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
                ContractMatch? match = null;
                if (fecha.HasValue && hora.HasValue)
                {
                    match = await _repo.QueryExactSingleAsync(matricula, fecha.Value, hora.Value, ct);

                    if (match is null)
                    {
                        Console.WriteLine("  > (sin coincidencias)");
                    }
                    else
                    {
                        Console.WriteLine($"  > Match: Suc={match.Value.Sucursal}, Cliente={match.Value.CodCliente}");
                    }
                }
                else
                {
                    _log.LogWarning("No hay fecha y/o hora válidas para {File}. Se creará carpeta con marcadores.", fileName);
                }

                // 3) Elección (si hay) y copia de contrato
                var contratoNoEncontrado = match is null;
                FileInfo? contrato = null;
                if (match is not null)
                {
                    contrato = _files.FindNewestBySucursal(match.Value.Sucursal);
                    if (contrato is null)
                    {
                        _log.LogWarning("Contrato no encontrado para sucursal {Sucursal}", match.Value.Sucursal);
                        contratoNoEncontrado = true;
                    }
                }

                // 4) Crear carpeta de salida
                var destDir = _files.EnsureOutputFolder(
                    matricula,
                    fecha,
                    hora,
                    contratoNoEncontrado,
                    dupSuffix
                );

                // 5) Copiar siempre el PDF
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
    }

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
