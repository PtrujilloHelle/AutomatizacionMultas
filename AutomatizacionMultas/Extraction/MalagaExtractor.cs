using System.Text.RegularExpressions;

namespace Obtenerarchivosdelamulta.Extraction;

public sealed class MalagaExtractor : BaseExtractor
{
    public override string Name => "Ayto Málaga";
    public override bool CanHandle(string fileName) =>
        ContainsAny(fileName, "AYUNTAMIENTO DE MÁLAGA", "AYUNTAMIENTO DE MALAGA");

    public override bool TryExtract(string fullText, List<string> lines, out (string date, string time) result)
    {
        var dateRx = new Regex(@"\b(\d{2}/\d{2}/\d{4})\b", RegexOptions.CultureInvariant);
        var timeRx = new Regex(@"\b(\d{1,2})\s*[:\.]\s*(\d{2})(?:\s*[A-Za-z])?\b", RegexOptions.CultureInvariant);

        foreach (var line in lines)
        {
            var ld = dateRx.Match(line);
            var lt = timeRx.Match(line);
            if (ld.Success && lt.Success)
            {
                int h = int.Parse(lt.Groups[1].Value); string mm = lt.Groups[2].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(ld.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }

            var near = Regex.Match(line, @"(\d{2}/\d{2}/\d{4}).{0,40}?(\d{1,2})\s*[:\.]\s*(\d{2})(?:\s*[A-Za-z])?\b");
            if (near.Success)
            {
                int h = int.Parse(near.Groups[2].Value); string mm = near.Groups[3].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(near.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }
        }

        // Fallback: ventana en texto completo
        var allDates = dateRx.Matches(fullText);
        foreach (Match dm in allDates)
        {
            int start = dm.Index + dm.Length;
            int len = Math.Min(240, fullText.Length - start);
            if (len <= 0) break;
            string window = fullText.Substring(start, len);
            var tm = timeRx.Match(window);
            if (tm.Success)
            {
                int h = int.Parse(tm.Groups[1].Value); string mm = tm.Groups[2].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(dm.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }
        }
        result = default; return false;
    }
}
