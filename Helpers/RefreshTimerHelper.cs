using Grade_Monitor.Core;
using Grade_Monitor.Discord_App;
using System;
using System.Timers;

namespace Grade_Monitor.Helpers;

internal static class RefreshTimerHelper
{
    internal static readonly Timer RefreshTimer = new();

    internal static void InitializeRefreshTimer()
    {
        RefreshTimer.Interval = 1000;
        RefreshTimer.Elapsed += OnTimerElapsed;
        RefreshTimer.Start();
    }

    private static void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        // This timer ticks every second and determines when each user should have their grades refreshed.
        // Each user is assigned a refresh offset so that their requests are evenly distributed across the
        // configured interval. This avoids simultaneous grade-fetching and reduces server load.
        // The timer counts down per user; when a user’s counter reaches zero, a refresh is triggered
        // and the counter is reset to the full interval.

        var intervalSeconds = DiscordApp.AppConfig.TimerIntervalInMinutes * 60;

        var userCount = DiscordApp.AppConfig.Users.Count;

        if (userCount == 0)
            return;

        // Split the total refresh interval across all users.
        // This determines how many seconds should separate each user’s scheduled refresh,
        // ensuring their automatic grade checks are evenly spaced and do not occur at the same time.
        var intervalPerUser = intervalSeconds / userCount;

        var index = 0;

        foreach (var user in DiscordApp.AppConfig.Users)
        {
            var discordUserId = user.DiscordUserId;

            // If user is not stored in Sessions
            if (!SessionsManager.TryGetSession(discordUserId, out var session))
            {
                var offset = intervalPerUser * index;

                session = new Session(user: user)
                {
                    Offset = offset
                };

                SessionsManager.AddSession(discordUserId, session);

                LoggingService.WriteLog($"{discordUserId}: Created session, starting monitoring in {offset} seconds.", ConsoleColor.Cyan);
            }

            // It is time to fetch grades for this user
            if (session.Offset == 0)
            {
                session.Offset = intervalSeconds;
                GradesHelper.GetGrades(discordUserId, "OnTimerElapsed").Wait();
            }

            --session.Offset;

            ++index;
        }
    }
}