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
    /// <summary>Base class that hides all Selenium plumbing.</summary>
    public abstract class SeleniumBot<TConfig> : IAsyncDisposable where TConfig : SeleniumConfig
    {
        private static readonly Random Rnd = new();
        private readonly int _defaultTimeoutSec;
        protected readonly TConfig Config;

        protected IWebDriver Driver { get; private set; } = default!;
        protected WebDriverWait Wait { get; private set; } = default!;

        protected SeleniumBot(TConfig cfg, int defaultTimeoutSec = 20)
        {
            Config = cfg ?? throw new ArgumentNullException(nameof(cfg));

            var cfgTimeout = cfg.SeleniumOptions != null ? cfg.SeleniumOptions.DefaultTimeoutSec : defaultTimeoutSec;
            _defaultTimeoutSec = (cfgTimeout > 0) ? cfgTimeout : defaultTimeoutSec;
        }

        public async Task RunAsync()
        {
            CreateDriver();
            try { await ExecuteAsync(); }
            finally { await DisposeAsync(); }
        }

        protected virtual ChromeOptions BuildChromeOptions()
        {
            var opts = new ChromeOptions();
            opts.AddExcludedArgument("enable-automation");
            opts.AddAdditionalOption("useAutomationExtension", false);
            opts.AddArgument("--disable-blink-features=AutomationControlled");
            opts.AddUserProfilePreference("plugins.always_open_pdf_externally", true);

            string? downloadDir = null;

            if (Config is DescargaDeMultasConfig dmCfg)
                downloadDir = dmCfg.SaveOptions.DownloadDir;
            else if (!string.IsNullOrWhiteSpace(Config.SeleniumOptions.DownloadTempDirPath))
                downloadDir = Config.SeleniumOptions.DownloadTempDirPath;

            if (!string.IsNullOrWhiteSpace(downloadDir))
            {
                downloadDir = Path.GetFullPath(downloadDir).Replace('/', '\\');
                Directory.CreateDirectory(downloadDir);

                opts.AddUserProfilePreference("download.default_directory", downloadDir);
                opts.AddUserProfilePreference("download.prompt_for_download", false);
                opts.AddUserProfilePreference("download.directory_upgrade", true);
            }

            if (Config.SeleniumOptions.Headless)
                opts.AddArgument("--headless=new");
            else
                opts.AddArgument("--start-maximized");

            if (OperatingSystem.IsLinux())
            {
                opts.AddArgument("--no-sandbox");
                opts.AddArgument("--disable-dev-shm-usage");
            }

            return opts;
        }

        private void CreateDriver()
        {
            var options = BuildChromeOptions();
            Driver = new ChromeDriver(options);
            Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(_defaultTimeoutSec));
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.Zero;
        }

        protected abstract Task ExecuteAsync();

        public void HumanPause(int min, int max) => Thread.Sleep(Rnd.Next(min, max));

        public void SimulateHumanTyping(IWebElement el, string text)
        {
            foreach (char c in text) { el.SendKeys(c.ToString()); Thread.Sleep(Rnd.Next(60, 140)); }
        }

        public void SimulateHumanClick(IWebElement el)
        {
            new OpenQA.Selenium.Interactions.Actions(Driver)
                .MoveToElement(el)
                .Pause(TimeSpan.FromMilliseconds(Rnd.Next(80, 200)))
                .Click()
                .Perform();
            HumanPause(250, 600);
        }

        public void ScrollIntoView(IWebElement el)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].scrollIntoView({block:'center'});", el);
            HumanPause(200, 400);
        }

        public string SafeText(string t) => (t ?? "").Trim();

        public string? WaitNewZip(string folder, HashSet<string> previous, TimeSpan timeout)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (Directory.GetFiles(folder, "*.crdownload").Any()) { Thread.Sleep(400); continue; }
                var nuevo = Directory.GetFiles(folder, "*.zip").FirstOrDefault(z => !previous.Contains(z));
                if (nuevo != null && IsFileReady(nuevo)) return nuevo;
                Thread.Sleep(400);
            }
            return null;
        }

        public bool IsFileReady(string path)
        {
            try { using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); return fs.Length > 0; }
            catch { return false; }
        }

        // 🔁 Wrapper: mantenemos la firma anterior y delegamos en TextUtils
        public string SanitizeFileName(string name) => TextUtils.SanitizeFileName(name);

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

        public void MoveWithRetry(string src, string dst, int retries, int delayMs)
        {
            dst = EnsureUniquePath(dst);

            for (int i = 0; i < retries; i++)
            {
                try { File.Move(src, dst); return; }
                catch { Thread.Sleep(delayMs); }
            }
            File.Move(src, dst);
        }

        /* ───────────── COOKIES ───────────── */
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

        public void LoadCookies(string path, string baseUrl)
        {
            try
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine("⚠️ No hay cookies guardadas.");
                    return;
                }

                Driver.Navigate().GoToUrl(GetOrigin(baseUrl));
                Driver.Manage().Cookies.DeleteAllCookies();

                var json = File.ReadAllText(path);
                var cookies = JsonSerializer.Deserialize<List<SerializableCookie>>(json) ?? new();

                int ok = 0, fail = 0;
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

        protected static string GetOrigin(string url)
        {
            var uri = new Uri(url);
            return $"{uri.Scheme}://{uri.Host}";
        }

        public ValueTask DisposeAsync()
        {
            Driver?.Quit();
            Driver?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
