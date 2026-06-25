using Discord.WebSocket;
using Grade_Monitor.Configuration;
using Grade_Monitor.Core;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
using Grade_Monitor.Models;
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

    private static async Task HandleCommandAsync(SocketSlashCommand command)
    {
        // Acknowledge interaction
        await command.DeferAsync(ephemeral: true);

        var options = command.Data.Options;
        if (options == null || options.Count < 2)
        {
            await command.FollowupAsync("Invalid command parameters.", ephemeral: true);
            return;
        }

        var param1 = options.ElementAt(0).Value.ToString();
        var param2 = options.ElementAt(1).Value.ToString();

        RefreshTimerHelper.RefreshTimer.Stop();

        switch (command.CommandName)
        {
            case "update-interval":
            {
                if (!int.TryParse(param1, out var interval) ||
                    !int.TryParse(param2, out var retryInterval))
                {
                    await command.FollowupAsync("Invalid interval values.", ephemeral: true);
                    break;
                }

                App.Config.TimerIntervalInMinutes = interval;
                App.Config.TimerIntervalAfterExceptionsInMinutes = retryInterval;

                ConfigurationManager.Save(App.Config);

                await command.FollowupAsync("Intervals updated successfully.", ephemeral: true);
                break;
            }
            case "get-grades":
            {
                if (string.IsNullOrWhiteSpace(param1) ||
                    string.IsNullOrWhiteSpace(param2))
                {
                    await command.FollowupAsync("Student ID or password is missing.", ephemeral: true);
                    break;
                }

                var discordUserId = command.User.Id;

                User user;

                // Session being null indicates that the user was not registered to the app
                // and thus their data is not saved in config.json
                if (!SessionsManager.TryGetSession(discordUserId, out var session))
                {
                    user = new User
                    {
                        DiscordUserId = discordUserId,
                        StudentId = param1,
                        Password = param2
                    };

                    App.Config.Users.Add(user);
                }
                else
                {
                    user = session.User;
                    user.StudentId = param1;
                    user.Password = param2;
                }

                ConfigurationManager.Save(App.Config);

                await command.FollowupAsync(
                    "You will receive a private message with your grades within a few seconds.",
                    ephemeral: true);

                await GradesInteractionService.GetGrades(discordUserId, "SlashCommandExecuted");
                break;
            }
        }

        SessionsManager.ClearSessions();

        RefreshTimerHelper.RefreshTimer.Start();
    }
}