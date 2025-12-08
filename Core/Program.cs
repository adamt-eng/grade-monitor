using System.Threading.Tasks;
using Grade_Monitor.Discord_App;

namespace Grade_Monitor.Core;

internal class Program
{
    private static async Task Main()
    {
        var app = new DiscordApp();
        await app.StartAsync();
    }
}