using SolveCaptcha.Captcha;
using System;
using System.Threading.Tasks;
using Grade_Monitor.Core;

namespace Grade_Monitor.Utilities;

internal static class CaptchaSolver
{
    internal static void SolveRecaptcha(string pageName)
    {
        var solver = new SolveCaptcha.SolveCaptcha(Program.Configuration.CaptchaSolverApiKey) { RecaptchaTimeout = 600, PollingInterval = 10 };
        var asuRecaptchaKey = "6Lf17rUUAAAAAKR0rgH6aM7g0xjtzmxBK6w2T5j1";

        var captcha = new ReCaptcha();
        captcha.SetSiteKey(asuRecaptchaKey);
        captcha.SetUrl($"https://eng.asu.edu.eg/{pageName}");
        captcha.SetAction("verify");

        try
        {
            solver.Solve(captcha).Wait();
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (message is "CAPCHA_NOT_READY" or "ERROR_CAPTCHA_UNSOLVABLE" or "ERROR_BAD_DUPLICATES" or "ERROR_NO_SLOT_AVAILABLE")
            {
                Task.Delay(5000).Wait();
                SolveRecaptcha(pageName);
                return;
            }

            throw;
        }

        var token = captcha.Code;

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Exception("SolveCaptcha returned an empty token.");
        }

        SeleniumHelper.SendRecaptchaToken(token);
    }
}