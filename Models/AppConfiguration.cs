using System.Collections.Generic;

namespace Grade_Monitor.Models;

public class AppConfiguration
{
    public required string BotToken { get; set; }

    // Used by the active login flow (image CAPTCHA recognition via ocr.space).
    public string OcrSpaceApiKey { get; set; } = "";

    // Retained for the legacy reCAPTCHA solver (CaptchaSolver.cs); unused by the active flow.
    public string CaptchaSolverApiKey { get; set; } = "";
    public int TimerIntervalInMinutes { get; set; } = 60;
    public int TimerIntervalAfterExceptionsInMinutes { get; set; } = 1;
    public HashSet<User> Users { get; set; } = [];
    public Dictionary<string, string> Courses { get; set; } = [];
}