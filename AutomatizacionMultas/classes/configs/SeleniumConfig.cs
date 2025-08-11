//SeleniumConfig.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomatizacionMultas.classes.configs
{
    public class SeleniumConfig
    {
        public SeleniumConfig() { }
        public SeleniumOptionsConfig SeleniumOptions { get; set; } = default!;
    }

    public class SeleniumOptionsConfig
    {
        public bool Headless { get; set; }
        public string DownloadTempDirPath { get; set; } = string.Empty;

        // NUEVO: timeout configurable para WebDriverWait (segundos)
        public int DefaultTimeoutSec { get; set; } = 20;
    }
}
