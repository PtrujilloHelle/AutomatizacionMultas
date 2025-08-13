//DescargaDeMultas.Cookies.cs
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System;
using System.IO;
using System.Linq; // <- para FirstOrDefault

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
            HumanPause(600, 1100);

            // Usuario: mover ratón, enfocar, escribir con pausas
            var userInput = Wait.Until(d => d.FindElement(By.Name("username")));
            ScrollIntoView(userInput);
            SimulateHumanClick(userInput);
            HumanPause(150, 300);
            SimulateHumanTyping(userInput, Config.PyramidConnection.Username);
            HumanPause(250, 500);

            // Password: igual que arriba
            var passInput = Wait.Until(d => d.FindElement(By.Name("password")));
            ScrollIntoView(passInput);
            SimulateHumanClick(passInput);
            HumanPause(150, 300);
            SimulateHumanTyping(passInput, Config.PyramidConnection.Password);
            HumanPause(250, 500);

            // Envío "humano": preferimos click en botón de submit si existe; si no, Enter
            var submit = Driver.FindElements(By.CssSelector("button[type='submit'], input[type='submit'], button.btn"))
                               .FirstOrDefault();
            if (submit != null)
            {
                ScrollIntoView(submit);
                SimulateHumanClick(submit);
            }
            else
            {
                passInput.SendKeys(Keys.Enter);
            }

            HumanPause(1200, 1800);
        }
    }
}
