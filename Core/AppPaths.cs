using System;
using System.IO;

namespace Grade_Monitor.Core;

internal static class AppPaths
{
    private static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;

    internal static readonly string ChromeExe = Path.Combine(BaseDirectory, "chrome-win64", "chrome.exe");
    internal static readonly string ChromeDriver = Path.Combine(BaseDirectory, "chromedriver-win64");

    internal static readonly string Config = Path.Combine(BaseDirectory, "config.json");
}