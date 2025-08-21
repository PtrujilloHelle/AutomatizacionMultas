//PlateExtractor.cs
using System.Text.RegularExpressions;
using static AutomatizacionMultas.classes.utils.TextUtils;

namespace AutomatizacionMultas.classes.utils
{
    /// <summary>
    /// Reglas de extracción de matrículas (general y variantes por organismo).
    /// </summary>
    public static class PlateExtractor
    {
        private const int WindowBefore = 120;
        private const int WindowSpan = 800;

        // 4 dígitos + 3 consonantes (sin vocales ni Q), con tolerancia a espacios/guiones
        private static readonly Regex RxPlateCore = new(
            @"\b\d{4}\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]{3}\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Versión “suave”: dígitos/letras separados por varios caracteres
        private static readonly Regex RxPlateLoose = new(
            @"\d\s*[-]?\s*\d\s*[-]?\s*\d\s*[-]?\s*\d\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]\s*[-]?\s*[BCDFGHJKLMNPRSTVWXYZ]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Estricto sobre texto compactado
        private static readonly Regex RxPlateStrictCompact = new(
            @"\d{4}[BCDFGHJKLMNPRSTVWXYZ]{3}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Extractor general (sirve para la mayoría de organismos).</summary>
        public static string? FindGeneral(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Normalizamos espacios para una búsqueda limpia
            string norm = Regex.Replace(text, @"\s+", " ").ToUpperInvariant();

            // 1) Ventanas alrededor de “MATRÍCULA/MATRICULA”
            foreach (var key in new[] { "MATRICULA", "MATRÍCULA" })
            {
                int idx = norm.IndexOf(key, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    int start = Math.Max(0, idx - WindowBefore);
                    int len = Math.Min(WindowSpan, norm.Length - start);
                    var slice = norm.Substring(start, len);

                    var mCtx = RxPlateCore.Match(slice);
                    if (mCtx.Success) return Clean(mCtx.Value);

                    idx = norm.IndexOf(key, idx + key.Length, StringComparison.Ordinal);
                }
            }

            // 2) Búsqueda global “core”
            var m = RxPlateCore.Match(norm);
            if (m.Success) return Clean(m.Value);

            // 3) Fallback: estricto sobre texto compactado
            string compact = Regex.Replace(text, @"[^A-Z0-9]", "", RegexOptions.IgnoreCase).ToUpperInvariant();
            var m2 = RxPlateStrictCompact.Match(compact);
            return m2.Success ? m2.Value : null;
        }

        /// <summary>Extractor específico Benalmádena (tolerante a separadores/saltos raros).</summary>
        public static string? FindBenalmadena(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Quitamos diacríticos pero mantenemos espacios (para detectar “MATRICULA” aunque cambie la tipografía)
            string norm = RemoveDiacriticsKeepSpaces(text).ToUpperInvariant();

            // 1) Por líneas: “MATRICULA .... 1234 ABC”
            foreach (var raw in Regex.Split(norm, @"\r?\n"))
            {
                var line = Regex.Replace(raw, @"\s+", " ");
                if (line.Contains("MATRICULA"))
                {
                    var m = RxPlateCore.Match(line);
                    if (m.Success) return Clean(m.Value);
                }
            }

            // 2) Ventana alrededor de la primera “MATRICULA” (patrón suelto)
            int id = norm.IndexOf("MATRICULA", StringComparison.Ordinal);
            if (id >= 0)
            {
                int start = Math.Max(0, id - WindowBefore);
                string window = norm.Substring(start, Math.Min(WindowSpan, norm.Length - start));
                var soft = RxPlateLoose.Match(window);
                if (soft.Success)
                {
                    string compact = Regex.Replace(soft.Value, @"[^A-Z0-9]", "");
                    if (RxPlateStrictCompact.IsMatch(compact))
                        return compact;
                }
            }

            // 3) Fallback global (compactado)
            string compactAll = Regex.Replace(norm, @"[^A-Z0-9]", "");
            var m3 = RxPlateStrictCompact.Match(compactAll);
            return m3.Success ? m3.Value : null;
        }

        private static string Clean(string raw) => Regex.Replace(raw, @"[^A-Z0-9]", "");
    }
}
