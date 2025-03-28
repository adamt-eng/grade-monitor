using System.Collections.Generic;

namespace Grade_Monitor.Configuration;

public class Configuration
{
    public string BotToken { get; set; }
    public int TimerIntervalInMinutes { get; set; } = 60;
    public int TimerIntervalAfterExceptionsInMinutes { get; set; } = 1;
    public HashSet<User> Users { get; set; } = [];
    public Dictionary<string, string> Courses { get; set; } = [];
}

public class User
{
    public ulong DiscordUserId { get; set; }
    public string StudentId { get; set; }
    public string Password { get; set; }
    public string LaravelSession { get; set; }
    public string XsrfToken { get; set; }
    public Dictionary<string, HashSet<string>> Semesters { get; set; } = [];
}