using System.Collections.Generic;

namespace Grade_Monitor.Models;

public class AppConfiguration
{
    public required string CaptchaSolverApiKey { get; set; }
    public required string BotToken { get; set; }
    public int TimerIntervalInMinutes { get; set; } = 60;
    public int TimerIntervalAfterExceptionsInMinutes { get; set; } = 1;
    public HashSet<User> Users { get; set; } = [];
    public Dictionary<string, string> Courses { get; set; } = [];
}