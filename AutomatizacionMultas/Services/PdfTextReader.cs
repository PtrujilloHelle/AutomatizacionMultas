using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Obtenerarchivosdelamulta.Services;

public sealed class PdfTextReader
{
    public (string fullText, List<string> lines) Read(string path)
    {
        var sb = new StringBuilder();
        var all = new List<string>();

        using var doc = PdfDocument.Open(path);
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);

            var words = page.GetWords().ToList();
            if (words.Count == 0) continue;

            const double yTol = 2.0;
            var rows = new List<List<Word>>();
            foreach (var w in words.OrderByDescending(w => w.BoundingBox.Bottom))
            {
                bool placed = false;
                foreach (var row in rows)
                {
                    var y = row[0].BoundingBox.Bottom;
                    if (Math.Abs(y - w.BoundingBox.Bottom) <= yTol) { row.Add(w); placed = true; break; }
                }
                if (!placed) rows.Add(new List<Word> { w });
            }

            foreach (var row in rows)
            {
                var line = string.Join(" ", row.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));
                all.Add(Normalize(line));
            }
        }
        return (Normalize(sb.ToString()), all);
    }

    public static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var n = input.Replace('\u00A0', ' ').Replace('\u2007', ' ').Replace('\u202F', ' ')
                     .Replace('·', '.').Replace('‧', '.');
        return Regex.Replace(n, @"\s+", " ").Trim();
    }
}
