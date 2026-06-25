using Discord;
using Discord.WebSocket;
using Grade_Monitor.Configuration;
using Grade_Monitor.Core;
using Grade_Monitor.Discord_App.Handlers;
using Grade_Monitor.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Grade_Monitor.Discord_App;

internal class DiscordApp
{
    internal static readonly DiscordSocketClient Client = new(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildScheduledEvents & ~GatewayIntents.GuildInvites
    });

    private readonly IEnumerable<IDiscordEventHandler> _handlers =
    [
        new ButtonHandler(),
        new CommandHandler(),
        new SelectMenuHandler()
    ];

    internal async Task StartAsync()
    {
        // A bot token is only needed for Discord mode; prompt for it the first time.
        if (string.IsNullOrWhiteSpace(App.Config.BotToken))
        {
            App.Config.BotToken = ConfigurationBootstrap.PromptBotToken();
            ConfigurationManager.Save(App.Config);
            Console.Clear();
        }

        Client.Log += message =>
        {
            LoggingService.WriteLog(message.Message, ConsoleColor.Gray);
            return Task.CompletedTask;
        };

        foreach (var handler in _handlers)
        {
            await handler.Initialize(Client);
        }

        Client.Ready += async () =>
        {
            await DiscordHelper.EnsureCommandsExistAsync(Client);

            RefreshTimerHelper.InitializeRefreshTimer();
        };

        await Client.LoginAsync(TokenType.Bot, App.Config.BotToken);
        await Client.StartAsync();
        await Task.Delay(-1);
    }
}