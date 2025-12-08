using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Grade_Monitor.Helpers;

internal static class SeleniumHelper
{
    private static ChromeDriver? _driverBacking;
    private static WebDriverWait? _waitBacking;

    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(10);

    private static ChromeDriver Driver => _driverBacking ??= CreateDriver();
    private static WebDriverWait Wait => _waitBacking ??= new WebDriverWait(Driver, DefaultWait);

    private static ChromeDriver CreateDriver()
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

        return new ChromeDriver(options);
    }

    internal static ReadOnlyCollection<Cookie> GetCookies() =>
        Driver.Manage().Cookies.AllCookies;

    private static IWebElement Find(By by) =>
        Wait.Until(_ => Driver.FindElement(by));

    private static object? Exec(string script, params object?[] args) =>
        ((IJavaScriptExecutor)Driver).ExecuteScript(script, args);

    private static void WaitDocumentLoaded() =>
        Wait.Until(_ => Exec("return document.readyState")?.ToString() == "complete");

    internal static void FillTextField(string elementName, string text) =>
        Find(By.Name(elementName)).SendKeys(text);

    internal static string SubmitLoginForm()
    {
        var loginButton = Find(By.XPath("//button[@type='submit']"));

        Exec("arguments[0].scrollIntoView(true); arguments[0].click();", loginButton);

        WaitDocumentLoaded();

        return Driver.PageSource;
    }

    internal static void SendRecaptchaToken(string token)
    {
        Exec(@"
            const el = document.getElementById('g-recaptcha-response');
            el.style.display = 'block';
            el.value = arguments[0];
        ", token);

        Wait.Until(_ =>
        {
            var val = Exec("return document.getElementById('g-recaptcha-response').value")?.ToString();
            return !string.IsNullOrWhiteSpace(val);
        });
    }

    internal static async Task<string> FetchPage(string url, ulong discordUserId)
    {
        try
        {
            LoggingService.WriteLog($"{discordUserId}: {url}", ConsoleColor.DarkGreen);

            await Driver.Navigate().GoToUrlAsync(url);
            WaitDocumentLoaded();

            return Driver.PageSource;
        }
        catch
        {
            LoggingService.WriteLog($"{discordUserId}: Exception (FetchPage - SeleniumHelper)", ConsoleColor.Red);
            throw;
        }
    }

    internal static void Shutdown()
    {
        try
        {
            _driverBacking?.Quit();
            _driverBacking?.Dispose();
        }
        finally
        {
            _driverBacking = null;
            _waitBacking = null;
        }
    }
}
