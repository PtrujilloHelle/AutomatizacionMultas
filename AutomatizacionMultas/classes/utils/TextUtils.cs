//TextUtils.cs
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AutomatizacionMultas.classes.utils
{
    public static class TextUtils
    {
        /// <summary>Quita diacríticos pero conserva espacios y demás caracteres.</summary>
        public static string RemoveDiacriticsKeepSpaces(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var formD = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (var ch in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>Quita diacríticos, elimina espacios, mayúsculas y recorta.</summary>
        public static string NormalizeAggressively(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            string s = RemoveDiacriticsKeepSpaces(input);
            s = Regex.Replace(s, @"\s+", "");
            return s.ToUpperInvariant().Trim();
        }

        /// <summary>Contiene con normalización (sin tildes/espacios y mayúsculas).</summary>
        public static bool ContainsNormalized(string? haystack, string? needle)
            => NormalizeAggressively(haystack).Contains(NormalizeAggressively(needle));

        /// <summary>Empieza por con normalización (sin tildes/espacios y mayúsculas).</summary>
        public static bool StartsWithNormalized(string? haystack, string? prefix)
            => NormalizeAggressively(haystack).StartsWith(NormalizeAggressively(prefix));

        /// <summary>Sanitiza un nombre de archivo (sustituye caracteres inválidos por '_', colapsa espacios).</summary>
        public static string SanitizeFileName(string name)
        {
            name ??= "";
            var invalid = Path.GetInvalidFileNameChars();
            var clean = string.Concat(name.Select(ch => invalid.Contains(ch) ? '_' : ch));
            return Regex.Replace(clean, @"\s+", " ").Trim();
        }
    }
}
