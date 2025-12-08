using Grade_Monitor.Core;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Grade_Monitor.Helpers;

internal static class SeleniumHelper
{
    private static ChromeDriver? _driver;
    private static WebDriverWait? _wait;

    private static readonly TimeSpan DefaultWait = TimeSpan.FromSeconds(10);

    internal static ReadOnlyCollection<Cookie> GetCookies() => _driver == null ? throw new InvalidOperationException("Driver not initialized.") : _driver.Manage().Cookies.AllCookies;

    private static void InitializeDriver()
    {
        if (_driver != null)
            return;

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

        options.BinaryLocation = AppPaths.ChromeExe;

        var service = ChromeDriverService.CreateDefaultService(AppPaths.ChromeDriver);
        service.HideCommandPromptWindow = true;

        _driver = new ChromeDriver(service, options);
        _wait = new WebDriverWait(_driver, DefaultWait);
    }

    private static IWebElement Find(By by)
    {
        if (_wait == null || _driver == null)
            throw new InvalidOperationException("Driver not initialized.");

        return _wait.Until(_ => _driver.FindElement(by));
    }

    private static object? Exec(string script, params object?[] args) => _driver == null ? throw new InvalidOperationException("Driver not initialized.") : ((IJavaScriptExecutor)_driver).ExecuteScript(script, args);

    private static void WaitDocumentLoaded()
    {
        if (_wait == null)
            throw new InvalidOperationException("Driver not initialized.");

        _wait.Until(_ =>
            Exec("return document.readyState")?.ToString() == "complete"
        );
    }

    internal static void FillTextField(string elementName, string text) => Find(By.Name(elementName)).SendKeys(text);

    internal static string SubmitLoginForm()
    {
        var loginButton = Find(By.XPath("//button[@type='submit']"));

        Exec("arguments[0].scrollIntoView(true); arguments[0].click();", loginButton);

        WaitDocumentLoaded();

        return _driver == null ? throw new InvalidOperationException("Driver not initialized.") : _driver.PageSource;
    }

    internal static void SendRecaptchaToken(string token)
    {
        Exec(@"
            const el = document.getElementById('g-recaptcha-response');
            el.style.display = 'block';
            el.value = arguments[0];
        ", token);

        if (_wait == null)
            throw new InvalidOperationException("Driver not initialized.");

        _wait.Until(_ =>
        {
            var val = Exec("return document.getElementById('g-recaptcha-response').value")?.ToString();
            return !string.IsNullOrWhiteSpace(val);
        });
    }

    internal static async Task<string> FetchPage(string url, ulong discordUserId)
    {
        try
        {
            InitializeDriver();

            LoggingService.WriteLog($"{discordUserId}: {url}", ConsoleColor.DarkGreen);

            if (_driver == null)
                throw new InvalidOperationException("Driver not initialized.");

            await _driver.Navigate().GoToUrlAsync(url);

            WaitDocumentLoaded();

            return await Task.FromResult(_driver.PageSource);
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
            _driver?.Quit();
            _driver?.Dispose();
        }
        catch { }
        finally
        {
            _driver = null;
            _wait = null;
        }
    }
}
