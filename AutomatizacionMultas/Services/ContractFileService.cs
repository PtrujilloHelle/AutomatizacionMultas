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

    public DirectoryInfo EnsureOutputFolder(string matricula, DateTime fecha, TimeSpan hora)
    {
        var folderName = $"{matricula}-{fecha:ddMMyyyy}-{hora:hhmm}";
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
