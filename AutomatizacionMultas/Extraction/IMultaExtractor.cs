using UglyToad.PdfPig.Content;

namespace Obtenerarchivosdelamulta.Extraction;

public interface IMultaExtractor
{
    string Name { get; }
    bool CanHandle(string fileName);
    bool TryExtract(string fullText, List<string> lines, out (string date, string time) result);
}
