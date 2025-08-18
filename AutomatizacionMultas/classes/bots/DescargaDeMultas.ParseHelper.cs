using System;
using System.Globalization;

namespace AutomatizacionMultas.classes.bots
{
    internal static class DescargaDeMultas_ParseFixedDateHelper
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
