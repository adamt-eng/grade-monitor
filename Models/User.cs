namespace Grade_Monitor.Models;

public class User
{
    /// <summary>The owning Discord user. Unused (left at 0) for the terminal-mode user.</summary>
    public ulong DiscordUserId { get; set; }

    public required string StudentId { get; set; }
    public required string Password { get; set; }
    public string? AccessToken { get; set; }
}
