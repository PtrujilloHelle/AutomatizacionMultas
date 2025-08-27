using System;
using System.Globalization;

namespace AutomatizacionMultas.classes.utils
{
    internal static class ParseFixedDateHelper
    {
        public static string ParseFixedDate(string? raw)
        {
            raw = raw?.Trim();
            if (string.IsNullOrEmpty(raw))
                return DateTime.Today.ToString("dd/MM/yyyy");

            if (DateTime.TryParseExact(raw, "dd/MM/yyyy", null,
                                       DateTimeStyles.None, out var dt))
                return dt.ToString("dd/MM/yyyy");

            if (DateTime.TryParse(raw, out dt))
                return dt.ToString("dd/MM/yyyy");

            return DateTime.Today.ToString("dd/MM/yyyy");
        }
    }
}
