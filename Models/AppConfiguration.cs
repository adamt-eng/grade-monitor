using System.Collections.Generic;

namespace Grade_Monitor.Models;

public class AppConfiguration
{
    /// <summary>Discord bot token. Only required when running in Discord mode.</summary>
    public string? BotToken { get; set; }

    public int TimerIntervalInMinutes { get; set; } = 60;
    public int TimerIntervalAfterExceptionsInMinutes { get; set; } = 1;

    /// <summary>Users monitored in Discord mode.</summary>
    public HashSet<User> Users { get; set; } = [];

    /// <summary>The single local user monitored in terminal mode.</summary>
    public User? TerminalUser { get; set; }

    /// <summary>When true, terminal mode masks all grades and the CGPA (privacy toggle).</summary>
    public bool HideGrades { get; set; }
}
