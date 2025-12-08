using Grade_Monitor.Discord_App;
using System.Threading.Tasks;

namespace Grade_Monitor;

internal class Program
{
    private static async Task Main()
    {
        var app = new DiscordApp();
        await app.StartAsync();
    }
}