//DescargaDeMultas.Table.cs
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Globalization;
using System.Linq;

namespace AutomatizacionMultas.classes.bots
{
    internal partial class DescargaDeMultas
    {
        private IReadOnlyList<IWebElement> GetRows() =>
            Driver.FindElements(By.CssSelector("table tbody tr")).ToList();

        private int CountRows() => GetRows().Count;

        private void WaitProcessingGone(WebDriverWait wait)
        {
            try { wait.Until(d => d.FindElements(By.CssSelector("div.dataTables_processing")) is var p && (p.Count == 0 || !p[0].Displayed)); }
            catch { }
        }

        private bool TryLoadMoreRows(ref int lastCount, int attempts = 3)
        {
            for (int j = 0; j < attempts; j++)
            {
                ((IJavaScriptExecutor)Driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                HumanPause(600, 900);
                WaitProcessingGone(Wait);

                int now = CountRows();
                if (now > lastCount) { lastCount = now; return true; }
            }
            return false;
        }

        private void EnsureAllRowsLoadedByScrolling()
        {
            int last = -1, calmRounds = 0, current = CountRows();

            while (current > last || calmRounds < 2)
            {
                if (current > last) { last = current; calmRounds = 0; }
                else calmRounds++;

                ((IJavaScriptExecutor)Driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                HumanPause(500, 800);
                WaitProcessingGone(Wait);
                current = CountRows();
            }

            ((IJavaScriptExecutor)Driver).ExecuteScript("window.scrollTo(0, 0);");
            HumanPause(300, 500);
        }

        private void SelectMostrarTodos()
        {
            WaitProcessingGone(Wait);
            var selectEl = Wait.Until(d =>
                d.FindElement(By.CssSelector("#cli_notificaciones_length select[name='cli_notificaciones_length']")));

            var sel = new SelectElement(selectEl);
            if (sel.Options.Any(o => o.Text.Trim().Equals("Todos", StringComparison.OrdinalIgnoreCase)))
                sel.SelectByText("Todos");
            else if (sel.Options.Any(o => o.GetAttribute("value") == "-1"))
                sel.SelectByValue("-1");
            else
                sel.SelectByIndex(sel.Options.Count - 1);

            HumanPause(800, 1500);
            try { Wait.Until(d => d.FindElements(By.CssSelector("table tbody tr")).Count > 15); }
            catch { }
        }

        private void SetUpFilters()
        {
            SimulateHumanClick(Wait.Until(d => d.FindElement(By.XPath("//a[@title='Sedes electronicas']"))));
            HumanPause(600, 1200);

            SimulateHumanClick(Wait.Until(d => d.FindElement(By.XPath("//span[normalize-space()='Notificaciones']"))));
            HumanPause(1500, 2500);

            string fechaFija = ParseFixedDate(Config.SaveOptions.FixedDate);

            var desdeInput = Wait.Until(d => d.FindElement(By.Id("beginDateCreated")));
            var hastaInput = Wait.Until(d => d.FindElement(By.Id("endDateCreated")));

            desdeInput.Clear(); SimulateHumanTyping(desdeInput, fechaFija);
            hastaInput.Clear(); SimulateHumanTyping(hastaInput, fechaFija);
            hastaInput.SendKeys(Keys.Enter);

            HumanPause(1400, 2000);
        }

        private static string ParseFixedDate(string? raw)
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
