using Discord.WebSocket;
using Grade_Monitor.Core;
using Grade_Monitor.Helpers;
using System;
using System.Threading.Tasks;

namespace Grade_Monitor.Discord_App.Handlers;

internal class ButtonHandler : IDiscordEventHandler
{
    public Task Initialize(DiscordSocketClient client)
    {
        client.ButtonExecuted += HandleButtonAsync;
        return Task.CompletedTask;
    }

    private static Task HandleButtonAsync(SocketMessageComponent socketMessageComponent)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Acknowledge this interaction
                await socketMessageComponent.DeferAsync(ephemeral: true).ConfigureAwait(false);

                switch (socketMessageComponent.Data.CustomId)
                {
                    case "refresh-grades":
                        await GradesHelper.GetGrades(discordUserId: socketMessageComponent.User.Id, interactionType: "ButtonExecuted (Refresh Grades)").ConfigureAwait(false);
                        break;
                    case "refetch-courses":
                    {
                        RefreshTimerHelper.RefreshTimer.Stop();

                        var discordUserId = socketMessageComponent.User.Id;

                        SessionManager.TryGetSession(discordUserId, out var session);

                        if (session == null)
                        {
                            await socketMessageComponent.FollowupAsync("Unable to find your credentials, please use the command `/get-grades` again.", ephemeral: true).ConfigureAwait(false);
                            RefreshTimerHelper.RefreshTimer.Start();
                            break;
                        }

                        // Clear stored semester data to force the application to refetch all semesters and courses
                        // This is specifically added for cases where the user has withdrawn/dropped or added courses after using the application
                        session.User.Semesters.Clear();

                        await GradesHelper.GetGrades(discordUserId: discordUserId, interactionType: "ButtonExecuted (Refetch Courses)").ConfigureAwait(false);

                        RefreshTimerHelper.RefreshTimer.Start();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.WriteLog($"ButtonExecuted Exception: {ex}", ConsoleColor.Red);
            }
        });
        return Task.CompletedTask;
    }
}