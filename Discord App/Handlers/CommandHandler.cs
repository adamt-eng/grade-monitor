using Discord.WebSocket;
using Grade_Monitor.Configuration;
using Grade_Monitor.Core;
using Grade_Monitor.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Discord_App.Handlers;

internal class CommandHandler : IDiscordEventHandler
{
    public Task Initialize(DiscordSocketClient client)
    {
        client.SlashCommandExecuted += HandleCommandAsync;
        return Task.CompletedTask;
    }

    private static Task HandleCommandAsync(SocketSlashCommand socketSlashCommand)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Acknowledge this interaction
                await socketSlashCommand.DeferAsync(ephemeral: true).ConfigureAwait(false);

                var commandParameters = socketSlashCommand.Data.Options.ToList();
                var param1 = commandParameters[0].Value;
                var param2 = commandParameters[1].Value;

                RefreshTimerHelper.RefreshTimer.Stop();

                switch (socketSlashCommand.CommandName)
                {
                    case "update-interval":
                    {
                        DiscordApp.Config.TimerIntervalInMinutes = Convert.ToInt32(param1);
                        DiscordApp.Config.TimerIntervalAfterExceptionsInMinutes = Convert.ToInt32(param2);

                        // Update config.json
                        DiscordApp.ConfigManager.Save(DiscordApp.Config);

                        await socketSlashCommand.FollowupAsync("Intervals updated successfully.", ephemeral: true).ConfigureAwait(false);
                        break;
                    }
                    case "get-grades":
                    {
                        var discordUserId = socketSlashCommand.User.Id;

                        SessionManager.TryGetSession(discordUserId, out var session);

                        // Session being null indicates that the user was not registered to the app
                        // and thus their data is not saved in config.json
                        var user = session == null ? new User { DiscordUserId = discordUserId } : session.User;

                        // Save/update user information
                        user.StudentId = param1.ToString();
                        user.Password = param2.ToString();

                        // Add user to configuration
                        if (session == null)
                        {
                            DiscordApp.Config.Users.Add(user);
                        }

                        // Update config.json
                        DiscordApp.ConfigManager.Save(DiscordApp.Config);

                        await socketSlashCommand.FollowupAsync("You will receive a private message with your grades within a few seconds.", ephemeral: true).ConfigureAwait(false);

                        await GradesHelper.GetGrades(discordUserId: discordUserId, interactionType: "SlashCommandExecuted").ConfigureAwait(false);
                        break;
                    }
                }

                SessionManager.ClearSessions();

                RefreshTimerHelper.RefreshTimer.Start();
            }
            catch (Exception ex)
            {
                LoggingService.WriteLog($"SlashCommandExecuted Exception: {ex}", ConsoleColor.Red);
            }
        });
        return Task.CompletedTask;
    }
}