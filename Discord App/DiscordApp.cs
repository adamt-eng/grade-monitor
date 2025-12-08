using Discord;
using Discord.WebSocket;
using Grade_Monitor.Configuration;
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

    internal static ConfigurationManager ConfigManager;
    internal static Configuration.Configuration Config;

    internal async Task StartAsync()
    {
        // Load configuration
        ConfigManager = new ConfigurationManager("config.json");
        Config = ConfigManager.Load();

        Client.Log += message =>
        {
            _ = Task.Run(() =>
            {
                LoggingService.WriteLog(message.Message, ConsoleColor.Gray);
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        };

        foreach (var handler in _handlers)
        {
            await handler.Initialize(Client);
        }

        Client.Ready += () =>
        {
            DiscordHelper.EnsureCommandsExist(Client);

            RefreshTimerHelper.InitializeRefreshTimer();

            return Task.CompletedTask;
        };

        await Client.LoginAsync(TokenType.Bot, Config.BotToken).ConfigureAwait(false);
        await Client.StartAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }
}