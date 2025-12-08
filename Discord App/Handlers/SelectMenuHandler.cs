using Discord.WebSocket;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
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

    private static async Task HandleSelectMenuAsync(SocketMessageComponent component)
    {
        // Acknowledge interaction
        await component.DeferAsync(ephemeral: true);

        var discordUserId = component.User.Id;

        if (!SessionsManager.TryGetSession(discordUserId, out var session))
        {
            await component.FollowupAsync("Session not found.", ephemeral: true);
            return;
        }

        var data = component.Data;
        var selection = data.Values.First();
        var customId = data.CustomId;

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

        await GradesInteractionService.GetGrades(discordUserId, $"SelectMenuExecuted ({customId})");
    }
}