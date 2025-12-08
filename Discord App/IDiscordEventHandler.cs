using Discord.WebSocket;
using System.Threading.Tasks;

namespace Grade_Monitor.Discord_App;

internal interface IDiscordEventHandler
{
    internal Task Initialize(DiscordSocketClient client);
}