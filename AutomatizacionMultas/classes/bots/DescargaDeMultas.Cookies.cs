//DescargaDeMultas.Cookies.cs
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.IO;

namespace AutomatizacionMultas.classes.bots
{
    internal partial class DescargaDeMultas
    {
        private static string CookiesDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "AutomatizacionMultas", "cookies");

        private string CookieFilePath => Path.Combine(CookiesDir, "pyramid.json");

        private void EnsureCookieDirAndMigrate()
        {
            try
            {
                Directory.CreateDirectory(CookiesDir);

                var newPath = CookieFilePath;
                var oldPath = Path.Combine(AppContext.BaseDirectory, "cookies", "pyramid.json");

                if (!File.Exists(newPath) && File.Exists(oldPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                    File.Copy(oldPath, newPath, overwrite: false);
                    Console.WriteLine($"➡️ Cookies migradas a {newPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ No se pudieron migrar las cookies: {ex.Message}");
            }
        }

        private bool IsLoggedIn()
        {
            try
            {
                return Driver.FindElements(By.XPath("//a[@title='Sedes electronicas']")).Count > 0;
            }
            catch { return false; }
        }

        private void EnsureLoggedIn()
        {
            LoadCookies(CookieFilePath, Config.PyramidConnection.Url);
            Driver.Navigate().GoToUrl(Config.PyramidConnection.Url);
            HumanPause(800, 1300);

            if (IsLoggedIn())
            {
                Console.WriteLine("✅ Sesión restaurada mediante cookies.");
                return;
            }

            Console.WriteLine("ℹ️ Cookies no válidas/ausentes. Realizando login…");
            LoginStep();

            try
            {
                new WebDriverWait(Driver, TimeSpan.FromSeconds(20))
                    .Until(d => d.FindElements(By.XPath("//a[@title='Sedes electronicas']")).Count > 0);
            }
            catch { }

            SaveCookies(CookieFilePath);
        }

        private void LoginStep()
        {
            Driver.Navigate().GoToUrl(Config.PyramidConnection.Url);
            SimulateHumanTyping(Wait.Until(d => d.FindElement(By.Name("username"))), Config.PyramidConnection.Username);
            var passInput = Driver.FindElement(By.Name("password"));
            SimulateHumanTyping(passInput, Config.PyramidConnection.Password);
            passInput.SendKeys(Keys.Enter);
        }
    }
}
