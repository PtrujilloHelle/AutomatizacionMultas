using System.Text.RegularExpressions;

namespace Obtenerarchivosdelamulta.Extraction;

public abstract class BaseExtractor : IMultaExtractor
{
    public abstract string Name { get; }
    public abstract bool CanHandle(string fileName);
    public abstract bool TryExtract(string fullText, List<string> lines, out (string date, string time) result);

    protected static string CleanDate(string s) => Regex.Replace(s ?? "", @"\s*", "").Replace('-', '/');
    protected static string CleanTime(string s) => Regex.Replace(s ?? "", @"\s*", "").Replace('.', ':');
    protected static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack?.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
}
