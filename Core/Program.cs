using Discord;
using Discord.WebSocket;
using Grade_Monitor.Configuration;
using Grade_Monitor.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

namespace Grade_Monitor.Core;

internal class Program
{
    private static readonly Timer Timer = new();

    private static readonly Dictionary<ulong, Session> Sessions = [];

    private static readonly DiscordSocketClient Client = new(new DiscordSocketConfig { GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildScheduledEvents & ~GatewayIntents.GuildInvites });

    internal static ConfigurationManager ConfigurationManager;
    internal static Configuration.Configuration Configuration;

    private static async Task Main()
    {
        // Load configuration
        ConfigurationManager = new ConfigurationManager("config.json");
        Configuration = ConfigurationManager.Load();

        // Log event handler
        Client.Log += message =>
        {
            WriteLog(message.Message, ConsoleColor.Gray);
            return Task.CompletedTask;
        };

        // Slash command handler
        Client.SlashCommandExecuted += async socketSlashCommand =>
        {
            // Acknowledge this interaction
            await socketSlashCommand.DeferAsync(ephemeral: true).ConfigureAwait(false);

            // Extract studentId, password, and discordUserId
            var dataOptions = socketSlashCommand.Data.Options.ToList();
            var studentId = dataOptions[0].Value.ToString();
            var password = dataOptions[1].Value.ToString();
            var discordUserId = socketSlashCommand.User.Id;

            // Save new user
            var user = new User
            {
                DiscordUserId = discordUserId,
                StudentId = studentId,
                Password = password
            };

            // Clear stored semester data to force the application to refetch them
            // This is specifically added for cases where the user has withdrawn/dropped or added courses after using the application
            // The user must reuse the slash command in such cases to register the new courses or to remove the withdrawn/dropped courses
            user.Semesters.Clear();

            // Add user to configuration
            Configuration.Users.Add(user);

            // Update config.json
            ConfigurationManager.Save(Configuration);

            // Initialize new session and store it
            Sessions[discordUserId] = new Session(user: user);

            await GetGrades(discordUserId: discordUserId, interactionType: "SlashCommandExecuted").ConfigureAwait(false);

            await socketSlashCommand.FollowupAsync("You will receive a private message with your grades within a few seconds.", ephemeral: true).ConfigureAwait(false);
        };

        Client.SelectMenuExecuted += async socketMessageComponent =>
        {
            // Acknowledge this interaction
            await socketMessageComponent.DeferAsync(ephemeral: true).ConfigureAwait(false);

            // Extract discordUserId
            var discordUserId = socketMessageComponent.User.Id;

            // Get user selection
            var data = socketMessageComponent.Data;
            var selection = data.Values.First();
            var customId = data.CustomId;

            switch (customId)
            {
                case "select-semester":
                    {
                        // Update semester based on selection
                        Sessions[discordUserId].RequestedSemester = selection;
                        break;
                    }
                case "select-load":
                    {
                        // Update load state based on selection
                        Sessions[discordUserId].HeavyLoad = selection == "Heavy Load";
                        break;
                    }
            }

            await GetGrades(discordUserId: discordUserId, interactionType: $"SelectMenuExecuted ({customId})").ConfigureAwait(false);
        };

        Client.ButtonExecuted += async socketMessageComponent =>
        {
            // Acknowledge this interaction
            await socketMessageComponent.DeferAsync(ephemeral: true).ConfigureAwait(false);
            await GetGrades(discordUserId: socketMessageComponent.User.Id, interactionType: "ButtonExecuted").ConfigureAwait(false);
        };

        Client.Ready += () =>
        {
            static async void OnTimerElapsed(object sender, ElapsedEventArgs e)
            {
                Timer.Interval = Configuration.TimerIntervalInMinutes * 60000;

                foreach (var user in Configuration.Users)
                {
                    var discordUserId = user.DiscordUserId;

                    if (!Sessions.ContainsKey(discordUserId))
                    {
                        Sessions[discordUserId] = new Session(user: user);
                    }

                    await GetGrades(discordUserId, "OnTimerElapsed").ConfigureAwait(false);
                }
            }

            OnTimerElapsed(null, null);

            Timer.Elapsed += OnTimerElapsed;
            Timer.Start();

            return Task.CompletedTask;
        };

        await Client.LoginAsync(TokenType.Bot, Configuration.BotToken).ConfigureAwait(false);
        await Client.StartAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }

    private static string NextRefresh() => $"Next refresh <t:{((DateTimeOffset)DateTime.Now.AddMilliseconds(Timer.Interval)).ToUnixTimeSeconds()}:R> {new Emoji("🕒")}";

    internal static void WriteLog(string log, ConsoleColor consoleColor)
    {
        Console.ForegroundColor = consoleColor;
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} {log}");
        Console.ResetColor();
    }

    private static async Task GetGrades(ulong discordUserId, string interactionType)
    {
        try
        {
            // New line to seperate events
            Console.WriteLine();

            // Log interaction
            WriteLog($"{interactionType} {discordUserId}", ConsoleColor.Cyan);

            // Stopwatch to keep track of how fast we're reading grades
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Get the DM channel between the user and the bot
            var user = await Client.GetUserAsync(discordUserId).ConfigureAwait(false);
            var dmChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
            var messages = (await dmChannel.GetMessagesAsync().FlattenAsync().ConfigureAwait(false)).ToList();

            var message = (IUserMessage)null;
            var previousEmbedBuilder = (EmbedBuilder)null;

            // If there was a previous message sent between the user and the bot
            if (messages.Count == 1)
            {
                message = (IUserMessage)messages.First();

                // Store the embed builder from that message (previousEmbedBuilder)
                // The previousEmbedBuilder contains the last saved grades which we need to detect if grades were updated
                if (message.Embeds.Count == 1)
                {
                    previousEmbedBuilder = message.Embeds.First().ToEmbedBuilder();
                }
                // If it doesn't have an embed then that indicates that an error has occurred while logging in or accessing faculty site
                // Set message to null (explained later)
                else
                {
                    message = null;
                }
            }

            // message will be null only if one of two things happened:
            // 1. An error has occurred while logging in or accessing faculty site
            // 2. There was more than one message in the DM channel (not supposed to happen except due to misusage of the app)
            if (message == null)
            {
                // Delete all messages in the DM channel
                foreach (var item in messages)
                {
                    await item.DeleteAsync().ConfigureAwait(false);
                }
            }
            // If message was null, a new message is sent regardless of if the grades were updated

            // Get specified user session
            var session = Sessions[discordUserId];

            try
            {
                // Attempt login
                if (await session.Login().ConfigureAwait(false))
                {
                    await session.InitializeMembers(message).ConfigureAwait(false);

                    var gradesReport = await session.FetchGradesReport().ConfigureAwait(false);

                    stopwatch.Stop();
                    WriteLog($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms", ConsoleColor.Yellow);

                    // Loops on each grade, the key is the course name and the value is the course's grade details
                    // Adds an EmbedFieldBuilder with each course data to a list
                    var embedFieldBuilders = new List<EmbedFieldBuilder>();
                    foreach (var course in gradesReport)
                    {
                        embedFieldBuilders.Add(new EmbedFieldBuilder
                        {
                            Name = course.Key,
                            Value = course.Value,
                            IsInline = false
                        });
                    }

                    var embedBuilder = new EmbedBuilder
                    {
                        Title = $"Grades Report For {session.User.StudentId}",
                        Description = $"**||__Cumulative GPA: {session.Cgpa}__||**",
                        Timestamp = DateTime.Now,
                        Fields = embedFieldBuilders
                    };

                    var components = DiscordHelper.CreateMessageComponent(session.User.Semesters.Keys.ToHashSet(), session.RequestedSemester, session.HeavyLoad);

                    // If the call was from the timer and the embeds are identical (grades not changed)
                    // Or if the user changed their selection of semester or load
                    // Therefore, simply update the already sent message with the requested data
                    if (message != null &&
                        (interactionType.Contains("SelectMenuExecuted") ||
                         interactionType == "OnTimerElapsed" && embedBuilder.IdenticalTo(previousEmbedBuilder)))
                    {
                        // Silently update the timestamp in the already sent message to indicate that the app is functioning as expected
                        await message.ModifyAsync(Update).ConfigureAwait(false);

                        void Update(MessageProperties properties)
                        {
                            properties.Content = NextRefresh();
                            properties.Embed = embedBuilder.Build();
                            properties.Components = components;
                        }
                    }
                    // Else indicates that one of the following occurred:
                    // 1. Grades Updated
                    // 2. Refresh Button Executed
                    // 3. Registration Slash Command Executed
                    else
                    {
                        if (message != null)
                        {
                            await message.DeleteAsync().ConfigureAwait(false);
                        }

                        await user.SendMessageAsync(text: NextRefresh(), embed: embedBuilder.Build(), components: components).ConfigureAwait(false);
                    }

                    // Reset fails counter
                    session.Fails = 0;
                }
                else
                {
                    await user.SendMessageAsync(text: "`Incorrect Student ID or Password.`", components: new ComponentBuilder().WithButton(DiscordHelper.CreateRefreshButton()).Build()).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                WriteLog($"Exception 1: {exception}", ConsoleColor.Red);

                // Update timer interval to the value of TimerIntervalAfterExceptionsInMinutes
                Timer.Interval = Configuration.TimerIntervalAfterExceptionsInMinutes * 60000;

                // Tracking the fails count allows us to track how long the faculty server has been down
                ++session.Fails;
                var text = $"{NextRefresh()}🔂 ({session.Fails})";

                if (message != null)
                {
                    await message.ModifyAsync(x => x.Content = text).ConfigureAwait(false);
                }
                else
                {
                    await user.SendMessageAsync(text: $"{text}\n\n`Faculty server is currently down.`", components: new ComponentBuilder().WithButton(DiscordHelper.CreateRefreshButton()).Build()).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            WriteLog($"Exception 2: {exception}", ConsoleColor.Red);
        }
    }
}