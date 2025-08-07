using Grade_Monitor.Core;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Grade_Monitor.Utilities;

internal static class SeleniumHelper
{
    private static ChromeDriver _driver;

    internal static ReadOnlyCollection<Cookie> GetCookies() => _driver.Manage().Cookies.AllCookies;

    private static void InitializeDriver()
    {
        if (_driver == null)
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless=new");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--disable-crash-reporter");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-in-process-stack-traces");
            options.AddArgument("--log-level=3");
            options.AddArgument("--disable-logging");

            _driver = new ChromeDriver(options);
        }
    }

    internal static void FillTextField(string elementName, string text) => _driver.FindElement(By.Name(elementName)).SendKeys(text);

    internal static string SubmitLoginForm()
    {
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

        var loginButton = wait.Until(driver => driver.FindElement(By.XPath("//button[@type='submit']")));

        ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView(true); arguments[0].click();", loginButton);

        wait.Until(webDriver => ((IJavaScriptExecutor)webDriver).ExecuteScript("return document.readyState")!.ToString() == "complete");

        return _driver.PageSource;
    }

    internal static void SendRecaptchaToken(string token)
    {
        ((IJavaScriptExecutor)_driver).ExecuteScript($"document.getElementById('g-recaptcha-response').style.display = 'block';document.getElementById('g-recaptcha-response').innerHTML = '{token}';");

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(webDriver => !string.IsNullOrWhiteSpace(((IJavaScriptExecutor)webDriver).ExecuteScript("return document.getElementById('g-recaptcha-response').value")!.ToString()));
    }

    internal static Task<string> FetchPage(string url, ulong discordUserId)
    {
        InitializeDriver();

        Program.WriteLog($"{discordUserId}: {url}", ConsoleColor.DarkGreen);

        _driver!.Navigate().GoToUrl(url);

        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
        wait.Until(webDriver => ((IJavaScriptExecutor)webDriver).ExecuteScript("return document.readyState")!.ToString() == "complete");

        return Task.FromResult(_driver.PageSource);
    }
}
