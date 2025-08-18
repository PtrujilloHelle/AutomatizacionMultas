using System.Text.RegularExpressions;

namespace Obtenerarchivosdelamulta.Extraction;

public sealed class FuengirolaExtractor : BaseExtractor
{
    public override string Name => "Ayto Fuengirola";
    public override bool CanHandle(string fileName) =>
        ContainsAny(fileName, "AYUNTAMIENTO DE FUENGIROLA");

    public override bool TryExtract(string fullText, List<string> lines, out (string date, string time) result)
    {
        var labelRx = new Regex(@"Fecha\s*y\s*hora\s*(\d{2}/\d{2}/\d{4})\s+(\d{1,2})[:\.](\d{2})",
                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var m = labelRx.Match(fullText);
        if (m.Success)
        {
            int h = int.Parse(m.Groups[2].Value); string mm = m.Groups[3].Value;
            if (h is >= 0 and <= 23) { result = (CleanDate(m.Groups[1].Value), $"{h:00}:{mm}"); return true; }
        }

        var dateRx = new Regex(@"\b(\d{2}/\d{2}/\d{4})\b", RegexOptions.CultureInvariant);
        var timeRx = new Regex(@"\b(\d{1,2})\s*[:\.]\s*(\d{2})\b", RegexOptions.CultureInvariant);

        foreach (var line in lines)
        {
            if (line.IndexOf("Fecha y hora", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var l2 = labelRx.Match(line);
                if (l2.Success)
                {
                    int h = int.Parse(l2.Groups[2].Value); string mm = l2.Groups[3].Value;
                    result = (CleanDate(l2.Groups[1].Value), $"{h:00}:{mm}");
                    return true;
                }
            }

            var ld = dateRx.Match(line);
            var lt = timeRx.Match(line);
            if (ld.Success && lt.Success)
            {
                int h = int.Parse(lt.Groups[1].Value); string mm = lt.Groups[2].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(ld.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }

            var near = Regex.Match(line, @"(\d{2}/\d{2}/\d{4}).{0,60}?(\d{1,2})\s*[:\.]\s*(\d{2})\b");
            if (near.Success)
            {
                int h = int.Parse(near.Groups[2].Value); string mm = near.Groups[3].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(near.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }
        }

        // ventana si aparece “Institución…” en el texto
        int idx = fullText.IndexOf("Institución Ayuntamiento de Fuengirola", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            int start = idx; int len = Math.Min(800, fullText.Length - start);
            var window = fullText.Substring(start, len);
            var wmatch = labelRx.Match(window);
            if (wmatch.Success)
            {
                int h = int.Parse(wmatch.Groups[2].Value); string mm = wmatch.Groups[3].Value;
                result = (CleanDate(wmatch.Groups[1].Value), $"{h:00}:{mm}");
                return true;
            }
        }

        result = default; return false;
    }
}
