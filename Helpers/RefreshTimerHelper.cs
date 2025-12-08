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

    private static void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        // This timer refreshes every one second, but grades are not surely fetched every 1 second
        // Each user has a timestamp at which their grades will be fetched
        // And the fetch requests are made as far away as possible from each other to minimize any interference

        var interval = DiscordApp.Config.TimerIntervalInMinutes * 60;

        // Get user count
        var userCount = DiscordApp.Config.Users.Count;

        // No users, no point in proceeding
        if (userCount == 0)
        {
            return;
        }

        // Divide the total interval specified in the configuration by the user count
        // This will give us the time that is between each user's auto-refresh request
        // This makes sure that the auto-refresh requests are as far away as possible from each other
        var intervalPerUser = interval / userCount;

        // Track user index
        var index = 0;

        foreach (var user in DiscordApp.Config.Users)
        {
            var discordUserId = user.DiscordUserId;

            // If user is not stored in Sessions
            if (!SessionManager.TryGetSession(discordUserId, out var session))
            {
                var timer = intervalPerUser * index;

                session = new Session(user: user) { Timer = timer };

                SessionManager.AddSession(discordUserId, session);

                LoggingService.WriteLog($"{discordUserId}: Created session, starting monitoring in {timer} seconds.", ConsoleColor.Cyan);
            }

            // It is time to fetch grades for this user
            if (session.Timer == 0)
            {
                session.Timer = interval;
                GradesHelper.GetGrades(discordUserId, "OnTimerElapsed").Wait();
            }

            --session.Timer;

            ++index;
        }
    }
}