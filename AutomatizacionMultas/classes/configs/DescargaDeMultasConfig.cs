//DescargaDeMultasConfig.cs

namespace AutomatizacionMultas.classes.configs
{
    public class DescargaDeMultasConfig: SeleniumConfig
    {
        public PyramidConnectionConfig PyramidConnection { get; set; } = default!;
        public SaveOptions SaveOptions { get; set; } = default!;
        public ScrapMapping ScrapMapping { get; set; } = default!;
    }

    public class SaveOptions
    {
        public string PdfRoot { get; set; } = "";
        public string DownloadDir { get; set; } = "";
        public bool OnePdfPerZip { get; set; }
        public string? FixedDate { get; set; }
        public string[] ExcludedPdfStarts { get; set; } = System.Array.Empty<string>();
    }
    public class ScrapMapping
    {
        public int ColMatricula { get; set; }
        public int ColOrganismo { get; set; }
    }
    public class PyramidConnectionConfig : ConnectionConfig
    {

    }
    public class ConnectionConfig
    {
        public string Url { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}   