using Discord.WebSocket;
using Grade_Monitor.Core;
using Grade_Monitor.Helpers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Discord_App.Handlers;

internal class SelectMenuHandler : IDiscordEventHandler
{
    public Task Initialize(DiscordSocketClient client)
    {
        client.SelectMenuExecuted += HandleSelectMenuAsync;
        return Task.CompletedTask;
    }

    private static Task HandleSelectMenuAsync(SocketMessageComponent socketMessageComponent)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Acknowledge this interaction
                await socketMessageComponent.DeferAsync(ephemeral: true).ConfigureAwait(false);

                // Extract discordUserId
                var discordUserId = socketMessageComponent.User.Id;

                // Get user selection
                var data = socketMessageComponent.Data;
                var selection = data.Values.First();
                var customId = data.CustomId;

                var sessionFound = SessionManager.TryGetSession(discordUserId, out var session);

                if (!sessionFound)
                {
                    return;
                }

                switch (customId)
                {
                    case "select-semester":
                    {
                        session.RequestedSemester = selection;
                        break;
                    }
                    case "select-mode":
                    {
                        session.FetchFinalGrades = selection == "Mode 1: Final Grades";
                        break;
                    }
                }

                await GradesHelper.GetGrades(discordUserId: discordUserId, interactionType: $"SelectMenuExecuted ({customId})").ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                LoggingService.WriteLog($"SelectMenuExecuted Exception: {ex}", ConsoleColor.Red);
            }
        });
        return Task.CompletedTask;
    }
}