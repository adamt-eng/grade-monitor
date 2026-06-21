using Grade_Monitor.Helpers;
using Grade_Monitor.Models;
using System;

namespace Grade_Monitor.Configuration;

internal static class ConfigurationBootstrap
{
    internal static AppConfiguration AskUserForConfiguration()
    {
        var botToken = PromptForValue(
            "Please enter your Discord app's authorization token: ",
            "App token cannot be empty."
        );

        Console.Clear();

        return new AppConfiguration
        {
            BotToken = botToken
        };
    }

    private static string PromptForValue(string promptMessage, string errorMessage)
    {
        while (true)
        {
            LoggingService.WriteLog(promptMessage, ConsoleColor.Yellow);

            var input = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(input))
                return input.Trim();

            LoggingService.WriteLog(errorMessage, ConsoleColor.Red);
        }
    }
}
