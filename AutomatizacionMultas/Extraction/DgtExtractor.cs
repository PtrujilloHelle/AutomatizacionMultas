using System.Text.RegularExpressions;

namespace Obtenerarchivosdelamulta.Extraction;

public sealed class DgtExtractor : BaseExtractor
{
    public override string Name => "DGT";
    public override bool CanHandle(string fileName) =>
        ContainsAny(fileName, "DIRECCIÓN GENERAL DE TRÁFICO", "DIRECCION GENERAL DE TRAFICO");

    public override bool TryExtract(string text, List<string> _, out (string date, string time) result)
    {
        var rx = new Regex(
            @"FECHA\s+Y\s+HORA\s+DE\s+LA\s+INFRACCI[ÓO]N:\s*(\d{2}\s*/\s*\d{2}\s*/\s*\d{4})\s*[-–—]\s*(\d{2}\s*:\s*\d{2})\s*h",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var m = rx.Match(text);
        if (m.Success)
        {
            result = (CleanDate(m.Groups[1].Value), CleanTime(m.Groups[2].Value));
            return true;
        }
        result = default;
        return false;
    }
}
