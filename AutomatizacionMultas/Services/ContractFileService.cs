using System.Globalization;
using Obtenerarchivosdelamulta.Domain;   // <-- importante

namespace Obtenerarchivosdelamulta.Services;

public sealed class ContractFileService
{
    private readonly string _contractsRoot;
    private readonly string _outputRoot;

    public ContractFileService(PipelineOptions opt)
    {
        _contractsRoot = opt.ContractsRoot;
        _outputRoot = opt.OutputRoot;
    }

    public FileInfo? FindNewestBySucursal(string sucursalCode)
    {
        if (string.IsNullOrWhiteSpace(sucursalCode)) return null;
        var dir = new DirectoryInfo(_contractsRoot);
        var pattern = $"{sucursalCode}_*.pdf";
        return dir.EnumerateFiles(pattern, SearchOption.TopDirectoryOnly)
                  .OrderByDescending(f => f.LastWriteTimeUtc)
                  .FirstOrDefault();
    }

    /// <summary>
    /// Crea carpeta con reglas:
    /// - Si faltan fecha y hora → MATRICULA-sin fecha ni hora
    /// - Si falta fecha → MATRICULA-sin fecha-HHMM
    /// - Si falta hora → MATRICULA-DDMMAAAA-sin hora
    /// - Si no hay contrato → añade " - contrato no encontrado"
    /// - Si hay duplicado → añade sufijo (p.ej. "_2") al final del nombre.
    /// </summary>
    public DirectoryInfo EnsureOutputFolder(
        string matricula,
        DateTime? fecha,
        TimeSpan? hora,
        bool contratoNoEncontrado,
        string? duplicateSuffix)
    {
        string folderName;

        if (!fecha.HasValue && !hora.HasValue)
        {
            folderName = $"{matricula}-sin fecha ni hora";
        }
        else if (!fecha.HasValue && hora.HasValue)
        {
            folderName = $"{matricula}-sin fecha-{hora.Value:hhmm}";
        }
        else if (fecha.HasValue && !hora.HasValue)
        {
            folderName = $"{matricula}-{fecha.Value:ddMMyyyy}-sin hora";
        }
        else
        {
            folderName = $"{matricula}-{fecha!.Value:ddMMyyyy}-{hora!.Value:hhmm}";
        }

        if (contratoNoEncontrado)
        {
            folderName += " - contrato no encontrado";
        }

        if (!string.IsNullOrWhiteSpace(duplicateSuffix))
        {
            folderName += duplicateSuffix; // p.ej. "_2"
        }

        return Directory.CreateDirectory(Path.Combine(_outputRoot, folderName));
    }

    public FileInfo? CopyIfExists(string? srcPath, DirectoryInfo destDir, string? overrideName = null)
    {
        if (string.IsNullOrWhiteSpace(srcPath) || !File.Exists(srcPath)) return null;
        var fileName = overrideName ?? Path.GetFileName(srcPath);
        var destPath = Path.Combine(destDir.FullName, fileName);
        File.Copy(srcPath, destPath, overwrite: true);
        return new FileInfo(destPath);
    }
}
