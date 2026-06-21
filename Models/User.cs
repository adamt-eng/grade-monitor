namespace Grade_Monitor.Models;

public class User
{
    public required ulong DiscordUserId { get; set; }
    public required string StudentId { get; set; }
    public required string Password { get; set; }
    public string? AccessToken { get; set; }
}
