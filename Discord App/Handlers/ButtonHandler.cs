using Discord.WebSocket;
using System.Threading.Tasks;

namespace Grade_Monitor.Discord_App.Handlers;

internal class ButtonHandler : IDiscordEventHandler
{
    public Task Initialize(DiscordSocketClient client)
    {
        client.ButtonExecuted += HandleButtonAsync;
        return Task.CompletedTask;
    }

    private static async Task HandleButtonAsync(SocketMessageComponent component)
    {
        // Acknowledge interaction
        await component.DeferAsync(ephemeral: true);

        var discordUserId = component.User.Id;
        var customId = component.Data.CustomId;

        if (customId == "refresh-grades")
            await GradesInteractionService.GetGrades(discordUserId, $"ButtonExecuted ({customId})");
    }
}