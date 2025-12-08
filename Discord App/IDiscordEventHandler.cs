using System.Threading.Tasks;
using Discord.WebSocket;

namespace Grade_Monitor.Discord_App;

internal interface IDiscordEventHandler
{
    internal Task Initialize(DiscordSocketClient client);
}