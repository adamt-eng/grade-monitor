using System;

namespace Grade_Monitor.Helpers;

internal static class LoggingService
{
    /// <summary>
    /// When false, log lines are dropped. Terminal mode disables logging so its rendered UI stays clean.
    /// </summary>
    internal static bool Enabled = true;

    internal static void WriteLog(string log, ConsoleColor consoleColor)
    {
        if (!Enabled)
            return;

        Console.ForegroundColor = consoleColor;
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} {log}");
        Console.ResetColor();
    }
}