using Discord.WebSocket;
using Grade_Monitor.Core.Session;
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

        switch (customId)
        {
            case "refresh-grades":
            {
                await GradesInteractionService.GetGrades(discordUserId, $"ButtonExecuted ({customId})");
                break;
            }
            case "refetch-courses":
            {
                if (!SessionsManager.TryGetSession(discordUserId, out var session))
                {
                    await component.FollowupAsync(
                        "Unable to find your credentials, please use the command `/get-grades` again.",
                        ephemeral: true);
                    break;
                }

                // Clear stored semester data to force the application to refetch all semesters and courses
                // This is specifically added for cases where the user has withdrawn/dropped or added courses after using the application
                session.User.Semesters.Clear();

                await GradesInteractionService.GetGrades(discordUserId, $"ButtonExecuted ({customId})");
                break;
            }
        }
    }
}