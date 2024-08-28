using System.Collections.Generic;

namespace Grade_Monitor;

public class Configuration
{
    public string BotToken { get; set; }
    public User User { get; set; }
    public int TimerIntervalInMinutes { get; set; } = 60;
    public int TimerIntervalAfterExceptionsInMinutes { get; set; } = 1;
    public HashSet<Semester> Semesters { get; set; } = [];
}

public class User
{
    public ulong DiscordUserId { get; set; }
    public string StudentId { get; set; }
    public string Password { get; set; }
}
public class Course
{
    public string Name { get; set; }
    public string Url { get; set; }
}
public class Semester
{
    public string Name { get; set; }
    public HashSet<Course> Courses { get; set; }
}