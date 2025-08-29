using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
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

    /// <summary>
    /// Crea el TXT:
    /// - Si hay CodHOC -> "es hoc.txt" con 3 líneas (Numero contrato / Codigo cliente / Codigo HOC)
    /// - Si NO hay CodHOC -> "no es hoc.txt" vacío.
    /// </summary>
    public FileInfo CreateHocNote(DirectoryInfo destDir, string codContrato, string codCliente, string? codHoc)
    {
        if (string.IsNullOrWhiteSpace(codHoc))
        {
            var pathNo = Path.Combine(destDir.FullName, "no es hoc.txt");
            File.WriteAllText(pathNo, string.Empty, Encoding.UTF8); // vacío
            return new FileInfo(pathNo);
        }
        else
        {
            var pathSi = Path.Combine(destDir.FullName, "es hoc.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"Numero contrato: {codContrato}");
            sb.AppendLine($"Codigo cliente: {codCliente}");
            sb.AppendLine($"Codigo HOC: {codHoc}");
            File.WriteAllText(pathSi, sb.ToString(), Encoding.UTF8);
            return new FileInfo(pathSi);
        }
    }

    /// <summary>
    /// Crea un TXT vacío cuyo nombre es la nacionalidad (o "null" si viene vacía).
    /// </summary>
    public FileInfo CreateNationalityNote(DirectoryInfo destDir, string? nacionalidad)
    {
        string baseName = string.IsNullOrWhiteSpace(nacionalidad) ? "null" : nacionalidad.Trim();
        baseName = SanitizeFileName(baseName);
        var path = Path.Combine(destDir.FullName, $"{baseName}.txt");
        File.WriteAllText(path, string.Empty, Encoding.UTF8); // contenido vacío; el valor va en el nombre
        return new FileInfo(path);
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "null";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var clean = new string(chars);
        return Regex.Replace(clean, @"\s+", " ").Trim();
    }
}
