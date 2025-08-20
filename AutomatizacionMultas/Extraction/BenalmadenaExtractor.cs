using System.Text.RegularExpressions;

namespace Obtenerarchivosdelamulta.Extraction;

public sealed class BenalmadenaExtractor : BaseExtractor
{
    public override string Name => "Ayto Benalmádena";

    public override bool CanHandle(string fileName) =>
        ContainsAny(fileName,
            "AYUNTAMIENTO DE BENALMÁDENA",
            "AYUNTAMIENTO DE BENALMADENA",
            "AYTO. DE BENALMÁDENA",
            "AYTO. DE BENALMADENA",
            "BENALMÁDENA",
            "BENALMADENA");

    public override bool TryExtract(string fullText, List<string> lines, out (string date, string time) result)
    {
        // Caso 1: etiqueta típica "Fecha y hora 27/07/2025 11:34"
        var labelRx = new Regex(@"Fecha\s*y\s*hora\s+(\d{2}/\d{2}/\d{4})\s+(\d{1,2})[:\.](\d{2})(?:\s*[A-Za-z])?",
                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var m1 = labelRx.Match(fullText);
        if (m1.Success)
        {
            int h = int.Parse(m1.Groups[2].Value); string mm = m1.Groups[3].Value;
            if (h is >= 0 and <= 23) { result = (CleanDate(m1.Groups[1].Value), $"{h:00}:{mm}"); return true; }
        }

        // Caso 2: misma lógica línea a línea (por si el lector separa diferente)
        foreach (var line in lines)
        {
            var mLine = labelRx.Match(line);
            if (mLine.Success)
            {
                int h = int.Parse(mLine.Groups[2].Value); string mm = mLine.Groups[3].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(mLine.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }
        }

        // Caso 3: fecha y hora en la misma línea sin la etiqueta (muy defensivo)
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

            // ventana corta: fecha seguida de hora cerca
            var near = Regex.Match(line, @"(\d{2}/\d{2}/\d{4}).{0,60}?(\d{1,2})\s*[:\.]\s*(\d{2})(?:\s*[A-Za-z])?");
            if (near.Success)
            {
                int h = int.Parse(near.Groups[2].Value); string mm = near.Groups[3].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(near.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }
        }

        // Caso 4: cerca de “Institución … Benalmadena” (a veces todo está en bloque)
        int idx = fullText.IndexOf("Institución", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = fullText.IndexOf("Institucion", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            int start = idx; int len = Math.Min(1000, fullText.Length - start);
            var window = fullText.Substring(start, len);
            var wmatch = labelRx.Match(window);
            if (wmatch.Success)
            {
                int h = int.Parse(wmatch.Groups[2].Value); string mm = wmatch.Groups[3].Value;
                if (h is >= 0 and <= 23) { result = (CleanDate(wmatch.Groups[1].Value), $"{h:00}:{mm}"); return true; }
            }

            // último intento en la ventana: fecha + hora cerca
            var wdate = dateRx.Match(window);
            if (wdate.Success)
            {
                var wtime = timeRx.Match(window, wdate.Index + wdate.Length);
                if (wtime.Success)
                {
                    int h = int.Parse(wtime.Groups[1].Value); string mm = wtime.Groups[2].Value;
                    if (h is >= 0 and <= 23) { result = (CleanDate(wdate.Groups[1].Value), $"{h:00}:{mm}"); return true; }
                }
            }
        }

        result = default;
        return false;
    }
}
