//SeleniumBot.cs

using AutomatizacionMultas.classes.configs;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// 👇 NUEVO
using System.Text.Json;
using Cookie = OpenQA.Selenium.Cookie;
// 👇 NUEVO: utilidades compartidas
using AutomatizacionMultas.classes.utils;

namespace AutomatizacionMultas.classes
{
    /// <summary>
    /// Clase base que encapsula toda la “fontanería” de Selenium (crear driver,
    /// opciones, esperas, utilidades de interacción humana, descargas, cookies, etc.).
    /// Los bots concretos (p. ej. DescargaDeMultas) heredan de aquí e implementan ExecuteAsync().
    /// </summary>
    public abstract class SeleniumBot<TConfig> : IAsyncDisposable where TConfig : SeleniumConfig
    {
        // Generador aleatorio para pausas humanas y timings.
        private static readonly Random Rnd = new();

        // Timeout por defecto para WebDriverWait (segundos).
        private readonly int _defaultTimeoutSec;

        // Configuración fuertemente tipada del bot concreto (hereda de SeleniumConfig).
        protected readonly TConfig Config;

        // Driver de Selenium y su espera explícita asociada.
        protected IWebDriver Driver { get; private set; } = default!;
        protected WebDriverWait Wait { get; private set; } = default!;

        /// <summary>
        /// Constructor: recibe la configuración concreta y determina el timeout de espera.
        /// </summary>
        protected SeleniumBot(TConfig cfg, int defaultTimeoutSec = 20)
        {
            Config = cfg ?? throw new ArgumentNullException(nameof(cfg));

            // Si la config trae un timeout explícito, se usa; si no, se queda el por defecto.
            var cfgTimeout = cfg.SeleniumOptions != null ? cfg.SeleniumOptions.DefaultTimeoutSec : defaultTimeoutSec;
            _defaultTimeoutSec = (cfgTimeout > 0) ? cfgTimeout : defaultTimeoutSec;
        }

        /// <summary>
        /// Método de alto nivel que ejecuta el ciclo de vida del bot:
        /// 1) Crear driver  2) Ejecutar la lógica del bot  3) Cerrar/limpiar driver.
        /// </summary>
        public async Task RunAsync()
        {
            CreateDriver();
            try { await ExecuteAsync(); }
            finally { await DisposeAsync(); }
        }

        /// <summary>
        /// Construye las opciones del ChromeDriver (headless, carpeta de descargas, etc.).
        /// Se puede sobrescribir en derivados si hace falta ajustar algo.
        /// </summary>
        protected virtual ChromeOptions BuildChromeOptions()
        {
            var opts = new ChromeOptions();

            // Reducir señales de automatización para algunas webs que bloquean bots.
            opts.AddExcludedArgument("enable-automation");
            opts.AddAdditionalOption("useAutomationExtension", false);
            opts.AddArgument("--disable-blink-features=AutomationControlled");

            // Forzar que los PDFs se abran externamente (para que se descarguen en disco).
            opts.AddUserProfilePreference("plugins.always_open_pdf_externally", true);

            // Determinar la carpeta de descargas:
            // - Si el bot es DescargaDeMultas, prioriza SaveOptions.DownloadDir
            // - Si no, usa SeleniumOptions.DownloadTempDirPath (si viene).
            string? downloadDir = null;

            if (Config is DescargaDeMultasConfig dmCfg)
                downloadDir = dmCfg.SaveOptions.DownloadDir;
            else if (!string.IsNullOrWhiteSpace(Config.SeleniumOptions.DownloadTempDirPath))
                downloadDir = Config.SeleniumOptions.DownloadTempDirPath;

            // Configurar preferencias de descarga si hay ruta definida.
            if (!string.IsNullOrWhiteSpace(downloadDir))
            {
                // Normalizar la ruta y crear el directorio si no existe.
                downloadDir = Path.GetFullPath(downloadDir).Replace('/', '\\');
                Directory.CreateDirectory(downloadDir);

                // Preferencias de Chrome: carpeta por defecto, no preguntar, “upgrade” de directorios.
                opts.AddUserProfilePreference("download.default_directory", downloadDir);
                opts.AddUserProfilePreference("download.prompt_for_download", false);
                opts.AddUserProfilePreference("download.directory_upgrade", true);
            }

            // Modo headless o ventana maximizada según configuración.
            if (Config.SeleniumOptions.Headless)
                opts.AddArgument("--headless=new");
            else
                opts.AddArgument("--start-maximized");

            // En Linux (p. ej. contenedores/CI), estas flags ayudan a la estabilidad.
            if (OperatingSystem.IsLinux())
            {
                opts.AddArgument("--no-sandbox");
                opts.AddArgument("--disable-dev-shm-usage");
            }

            return opts;
        }

        /// <summary>
        /// Crea el ChromeDriver con las opciones calculadas y configura la espera explícita.
        /// Desactiva el implicit wait para evitar interferencias con WebDriverWait.
        /// </summary>
        private void CreateDriver()
        {
            var options = BuildChromeOptions();
            Driver = new ChromeDriver(options);
            Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(_defaultTimeoutSec));

            // Se recomienda 0 en implicit para trabajar solo con esperas explícitas.
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        }

        /// <summary>
        /// Método que implementa el bot concreto (login, navegación, clicks, scraping, etc.).
        /// </summary>
        protected abstract Task ExecuteAsync();

        // ───────────────────────────
        // Utilidades de “simulación humana”
        // ───────────────────────────

        /// <summary>Pausa aleatoria entre min y max ms.</summary>
        public void HumanPause(int min, int max) => Thread.Sleep(Rnd.Next(min, max));

        /// <summary>Escribe texto carácter a carácter con pequeñas pausas.</summary>
        public void SimulateHumanTyping(IWebElement el, string text)
        {
            foreach (char c in text) { el.SendKeys(c.ToString()); Thread.Sleep(Rnd.Next(60, 140)); }
        }

        /// <summary>Click con pequeño “move+pause” para parecer humano.</summary>
        public void SimulateHumanClick(IWebElement el)
        {
            new OpenQA.Selenium.Interactions.Actions(Driver)
                .MoveToElement(el)
                .Pause(TimeSpan.FromMilliseconds(Rnd.Next(80, 200)))
                .Click()
                .Perform();
            HumanPause(250, 600);
        }

        /// <summary>Scroll hasta centrar el elemento en la ventana.</summary>
        public void ScrollIntoView(IWebElement el)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", el);
            HumanPause(200, 400);
        }

        /// <summary>Trimeado seguro (evita null).</summary>
        public string SafeText(string t) => (t ?? "").Trim();

        // ───────────────────────────
        // Descargas: esperar a que aparezca el ZIP nuevo y que esté “listo”
        // ───────────────────────────

        /// <summary>
        /// Espera a que aparezca un nuevo .zip en la carpeta, distinto de los ya existentes,
        /// ignorando mientras existan .crdownload (descarga en curso).
        /// </summary>
        /// <param name="folder">Carpeta de descargas.</param>
        /// <param name="previous">Conjunto de zips que ya estaban antes del click.</param>
        /// <param name="timeout">Tiempo máximo de espera.</param>
        /// <returns>Ruta del zip nuevo o null si expira.</returns>
        public string? WaitNewZip(string folder, HashSet<string> previous, TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                // Si hay .crdownload, el navegador sigue descargando → esperar.
                if (Directory.GetFiles(folder, "*.crdownload").Any()) { Thread.Sleep(400); continue; }

                // Buscar un .zip que no estuviera antes.
                var nuevo = Directory.GetFiles(folder, "*.zip").FirstOrDefault(z => !previous.Contains(z));

                // Comprobar que el archivo está listo (no bloqueado y con tamaño > 0).
                if (nuevo != null && IsFileReady(nuevo)) return nuevo;

                Thread.Sleep(400);
            }
            return null;
        }

        /// <summary>
        /// Intenta abrir el archivo en exclusivo; si no puede, es que aún lo están escribiendo.
        /// </summary>
        public bool IsFileReady(string path)
        {
            try { using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); return fs.Length > 0; }
            catch { return false; }
        }

        // 🔁 Wrapper: mantenemos la firma anterior y delegamos en TextUtils
        /// <summary>Sanitiza un nombre de archivo delegando en TextUtils.</summary>
        public string SanitizeFileName(string name) => TextUtils.SanitizeFileName(name);

        /// <summary>
        /// Si la ruta ya existe, genera una variante única: nombre_2.ext, nombre_3.ext, …
        /// </summary>
        protected static string EnsureUniquePath(string path)
        {
            string dir = Path.GetDirectoryName(path)!;
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);

            int dup = 2;
            while (File.Exists(path))
                path = Path.Combine(dir, $"{name}_{dup++}{ext}");
            return path;
        }

        /// <summary>
        /// Mueve un archivo con reintentos y respetando nombres únicos para evitar colisiones.
        /// </summary>
        public void MoveWithRetry(string src, string dst, int retries, int delayMs)
        {
            // Asegurar que el destino no pisa un archivo existente (crea sufijo si hace falta).
            dst = EnsureUniquePath(dst);

            for (int i = 0; i < retries; i++)
            {
                try { File.Move(src, dst); return; }
                catch { Thread.Sleep(delayMs); }
            }
            // Último intento (si vuelve a fallar, que lance la excepción).
            File.Move(src, dst);
        }

        /* ───────────── COOKIES ───────────── */

        /// <summary>
        /// Modelo serializable de cookie (nombre, valor, dominio, ruta, expiración) para persistir en JSON.
        /// </summary>
        public record SerializableCookie
        {
            public string Name { get; init; } = "";
            public string Value { get; init; } = "";
            public string Domain { get; init; } = "";
            public string Path { get; init; } = "/";
            public DateTime? Expiry { get; init; }

            public SerializableCookie() { }
            public SerializableCookie(Cookie c)
            {
                Name = c.Name; Value = c.Value; Domain = c.Domain; Path = c.Path; Expiry = c.Expiry;
            }

            public Cookie ToSeleniumCookie() => new Cookie(Name, Value, Domain, Path, Expiry);
        }

        /// <summary>
        /// Serializa y guarda todas las cookies del navegador en un JSON en disco.
        /// </summary>
        public void SaveCookies(string path)
        {
            try
            {
                var cookies = Driver.Manage().Cookies.AllCookies.Select(c => new SerializableCookie(c)).ToList();

                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(path, JsonSerializer.Serialize(cookies, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"🍪 Cookies guardadas en {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error guardando cookies: {ex.Message}");
            }
        }

        /// <summary>
        /// Carga cookies desde JSON e intenta inyectarlas en el navegador.
        /// Navega primero al ORIGIN (esquema+host) para permitir añadir cookies de ese dominio.
        /// </summary>
        public void LoadCookies(string path, string baseUrl)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine("⚠️ No hay cookies guardadas.");
                    return;
                }

                // Ir al ORIGIN (p. ej. https://host) para poder añadir cookies de ese dominio.
                Driver.Navigate().GoToUrl(GetOrigin(baseUrl));

                // Limpiar cookies existentes antes de inyectar.
                Driver.Manage().Cookies.DeleteAllCookies();

                // Leer JSON y deserializar a la clase SerializableCookie.
                var json = File.ReadAllText(path);
                var cookies = JsonSerializer.Deserialize<List<SerializableCookie>>(json) ?? new();

                int ok = 0, fail = 0;

                // Intentar añadir cada cookie; puede fallar si no encaja el dominio, expiración, etc.
                foreach (var sc in cookies)
                {
                    try { Driver.Manage().Cookies.AddCookie(sc.ToSeleniumCookie()); ok++; }
                    catch { fail++; }
                }
                Console.WriteLine($"✅ Cookies cargadas: {ok} (fallidas: {fail}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error cargando cookies: {ex.Message}");
            }
        }

        /// <summary>
        /// Devuelve el ORIGIN (scheme + host) de una URL (p. ej. "https://dominio").
        /// Útil para poder setear cookies de ese dominio.
        /// </summary>
        protected static string GetOrigin(string url)
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}";
        }

        /// <summary>
        /// Cierre y liberación del driver (Quit + Dispose).
        /// Se llama siempre desde RunAsync() en el finally.
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Driver?.Quit();
            Driver?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
