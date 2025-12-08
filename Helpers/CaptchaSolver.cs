using Grade_Monitor.Discord_App;
using SolveCaptcha.Captcha;
using System;
using System.Threading.Tasks;

namespace Grade_Monitor.Helpers;

internal static class CaptchaSolver
{
    private const string AsuRecaptchaKey = "6Lf17rUUAAAAAKR0rgH6aM7g0xjtzmxBK6w2T5j1";

    private static readonly string[] RetryableErrors =
    [
        "CAPCHA_NOT_READY",
        "ERROR_CAPTCHA_UNSOLVABLE",
        "ERROR_BAD_DUPLICATES",
        "ERROR_NO_SLOT_AVAILABLE"
    ];

    internal static async Task<string> SolveRecaptchaAsync(string pageName, int maxRetries = 5)
    {
        LoggingService.WriteLog("Starting SolveCaptcha..", ConsoleColor.DarkGreen);

        var solver = new SolveCaptcha.SolveCaptcha(DiscordApp.AppConfig.CaptchaSolverApiKey)
        {
            RecaptchaTimeout = 600,
            PollingInterval = 10
        };

        var captcha = new ReCaptcha();
        captcha.SetSiteKey(AsuRecaptchaKey);
        captcha.SetUrl($"https://eng.asu.edu.eg/{pageName}");
        captcha.SetAction("verify");

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            LoggingService.WriteLog($"SolveCaptcha {attempt}/{maxRetries}..", ConsoleColor.DarkGreen);

            try
            {
                await solver.Solve(captcha);
                break;
            }
            catch (Exception ex)
            {
                if (Array.Exists(RetryableErrors, e => e == ex.Message))
                {
                    LoggingService.WriteLog($"Retry {attempt}/{maxRetries}: {ex.Message}", ConsoleColor.DarkYellow);
                    await Task.Delay(5000);
                    continue;
                }

                throw;
            }
        }

        var token = captcha.Code;

        return string.IsNullOrWhiteSpace(token) ? throw new Exception("SolveCaptcha returned an empty token.") : token;
    }
}