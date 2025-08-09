using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Discord;
using Discord.WebSocket;
using Grade_Monitor.Configuration;
using Grade_Monitor.Utilities;

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

        Client.SlashCommandExecuted += async socketSlashCommand =>
        {
            // Acknowledge this interaction
            await socketSlashCommand.DeferAsync(ephemeral: true).ConfigureAwait(false);

            var dataOptions = socketSlashCommand.Data.Options.ToList();
            var option1 = dataOptions[0].Value;
            var option2 = dataOptions[1].Value;

            if (socketSlashCommand.Data.Name == "update-interval")
            {
                Timer.Stop();

                Configuration.TimerIntervalInMinutes = Convert.ToInt32(option1);
                Configuration.TimerIntervalAfterExceptionsInMinutes = Convert.ToInt32(option2);
                ConfigurationManager.Save(Configuration);

                Sessions.Clear();

                Timer.Start();

                await socketSlashCommand.FollowupAsync("Intervals updated successfully.", ephemeral: true).ConfigureAwait(false);
            }
            else if (socketSlashCommand.Data.Name == "get-grades")
            {
                Timer.Stop();

                var discordUserId = socketSlashCommand.User.Id;

                Sessions.TryGetValue(discordUserId, out var session);

                User user;
                // Session being null indicates that the user was not registered to the app
                // and thus their data is not saved in config.json
                if (session == null)
                {
                    user = new User { DiscordUserId = discordUserId };
                }
                else
                {
                    user = session.User;
                    user.Semesters.Clear(); // Clearing semesters forces the app to refetch them
                }

                // Save user information
                if (socketSlashCommand.CommandName == "get-grades")
                {
                    user.StudentId = option1.ToString();
                    user.Password = option2.ToString();
                }

                // Add user to configuration
                if (session == null)
                {
                    Configuration.Users.Add(user);
                }

                // Update config.json
                ConfigurationManager.Save(Configuration);

                // Initialize new session and store it
                Sessions[discordUserId] = new Session(user: user);

                await socketSlashCommand.FollowupAsync("You will receive a private message with your grades within a few seconds.", ephemeral: true).ConfigureAwait(false);

                await GetGrades(discordUserId: discordUserId, interactionType: "SlashCommandExecuted").ConfigureAwait(false);

                Timer.Start();
            }
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
                        Sessions[discordUserId].RequestedSemester = selection;
                        break;
                    }
                case "select-mode":
                    {
                        Sessions[discordUserId].FetchFinalGrades = selection == "Mode 1: Final Grades";
                        break;
                    }
            }

            await GetGrades(discordUserId: discordUserId, interactionType: $"SelectMenuExecuted ({customId})").ConfigureAwait(false);
        };

        Client.ButtonExecuted += async socketMessageComponent =>
        {
            // Acknowledge this interaction
            await socketMessageComponent.DeferAsync(ephemeral: true).ConfigureAwait(false);

            switch (socketMessageComponent.Data.CustomId)
            {
                case "refresh-grades": await GetGrades(discordUserId: socketMessageComponent.User.Id, interactionType: "ButtonExecuted (Refresh Grades)").ConfigureAwait(false); break;
                case "refetch-courses":
                {
                    Timer.Stop();

                    var discordUserId = socketMessageComponent.User.Id;

                    Sessions.TryGetValue(discordUserId, out var session);

                    if (session == null)
                    {
                        await socketMessageComponent.FollowupAsync("Unable to find your credentials, please use the command `/get-grades` again.", ephemeral: true).ConfigureAwait(false);
                        Timer.Start();
                        break;
                    }

                    // Clear stored semester data to force the application to refetch them
                    // This is specifically added for cases where the user has withdrawn/dropped or added courses after using the application
                    session.User.Semesters.Clear();

                    await GetGrades(discordUserId: discordUserId, interactionType: "ButtonExecuted (Refetch Courses)").ConfigureAwait(false);

                    Timer.Start();
                    break;
                }
            }
        };

        Client.Ready += () =>
        {
            DiscordHelper.EnsureCommandsExist(Client);

            Timer.Interval = 1000;
            Timer.Elapsed += OnTimerElapsed;
            Timer.Start();

            return Task.CompletedTask;
        };

        await Client.LoginAsync(TokenType.Bot, Configuration.BotToken).ConfigureAwait(false);
        await Client.StartAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }

    private static void OnTimerElapsed(object sender, ElapsedEventArgs e)
    {
        // This timer refreshes every one second, but grades are not surely fetched every 1 second
        // Each user has a timestamp at which their grades will be fetched
        // And the fetch requests are made as far away as possible from each other to minimize any interference

        var interval = Configuration.TimerIntervalInMinutes * 60;

        // Get user count
        var userCount = Configuration.Users.Count;

        // No users, no point in proceeding
        if (userCount == 0)
        {
            return;
        }

        // Divide the total interval specified in the configuration by the user count
        // This will give us the time that is between each user's auto-refresh request
        // This makes sure that the auto-refresh requests are as far away as possible from each other
        var intervalPerUser = interval / userCount;

        // Track user index
        var index = 0;

        foreach (var user in Configuration.Users)
        {
            var discordUserId = user.DiscordUserId;

            // If user is not stored in Sessions
            if (!Sessions.TryGetValue(discordUserId, out var session))
            {
                var timer = intervalPerUser * index;

                session = new Session(user: user) { Timer = timer };
                Sessions[discordUserId] = session;

                WriteLog($"{discordUserId}: Created session, starting monitoring in {timer} seconds.", ConsoleColor.Cyan);
            }

            // It is time to fetch grades for this user
            if (session.Timer == 0)
            {
                session.Timer = interval;
                GetGrades(discordUserId, "OnTimerElapsed").Wait();
            }

            --session.Timer;

            ++index;
        }
    }

    private static string NextRefresh(double intervalInSeconds) => $"Next refresh <t:{((DateTimeOffset)DateTime.Now.AddSeconds(intervalInSeconds)).ToUnixTimeSeconds()}:R> 🕒";

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
            // Log interaction
            Console.WriteLine();
            WriteLog($"{discordUserId}: {interactionType}", ConsoleColor.Cyan);

            // Stopwatch to keep track of how fast grades are being fetched
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Get the DM channel between the user and the bot
            var user = await Client.GetUserAsync(discordUserId).ConfigureAwait(false);
            var dmChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);

            // Get all messages in the DM channel
            var messages = (await dmChannel.GetMessagesAsync().FlattenAsync().ConfigureAwait(false)).ToList();

            // Filter to only messages sent from the bot, this is to avoid bugs caused by the user sending messages in the DM channel
            messages = [.. messages.Where(m => m.Author.Id == 1227871966057205841)];

            // The DM channel should contain only one message.
            // This message will be stored in:
            IUserMessage message = null;

            // That message must contain exactly one embed with the grades.
            // The embed will be stored in:
            EmbedBuilder previousEmbedBuilder = null;

            // If these conditions aren’t met, message remains null and will be handled after this block.
            if (messages.Count == 1)
            {
                // Get the message
                message = (IUserMessage)messages.First();

                // If the message contains exactly one embed, store it for comparison.
                // This embed holds the last known grades to detect changes later.
                if (message.Embeds.Count == 1)
                {
                    previousEmbedBuilder = message.Embeds.First().ToEmbedBuilder();
                }
                // If no embed is found, treat it as an error by setting message = null so it can be handled after this block.
                else
                {
                    message = null;
                }
            }

            // message will be null if:
            // 1. An error occurred while logging in or accessing the faculty site.
            // 2. The DM channel contained more than one message from the bot (can only happen if the app is misused).
            // Regardless of the reason, if message == null all messages sent by the bot will be deleted to force a new message with no bugs to be sent
            if (message == null)
            {
                // Delete all messages sent by the bot in the DM channel
                foreach (var item in messages)
                {
                    await item.DeleteAsync().ConfigureAwait(false);
                }
            }

            // Get user session
            var session = Sessions[discordUserId];

            try
            {
                // Attempt login
                if (await session.Login().ConfigureAwait(false))
                {
                    await session.LoadStudentData().ConfigureAwait(false);

                    session.DetermineRequestedSemester(message);

                    var gradesReport = await session.FetchGradesReport().ConfigureAwait(false);

                    stopwatch.Stop();
                    WriteLog($"{discordUserId}: Elapsed time: {stopwatch.ElapsedMilliseconds}ms", ConsoleColor.Yellow);

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

                    var components = DiscordHelper.CreateMessageComponent([.. session.User.Semesters.Keys], session.RequestedSemester, session.FetchFinalGrades);

                    // If the call was from the timer and the embeds are identical (grades not changed)
                    // Or if the user changed their selection of semester or mode
                    // Therefore, simply update the already sent message with the requested data
                    if (message != null && (interactionType.Contains("SelectMenuExecuted") || interactionType == "OnTimerElapsed" && EmbedsIdentical(embedBuilder, previousEmbedBuilder)))
                    {
                        // Silently update the timestamp in the already sent message to indicate that the app is functioning as expected
                        await message.ModifyAsync(Update).ConfigureAwait(false);

                        void Update(MessageProperties properties)
                        {
                            properties.Content = NextRefresh(session.Timer);
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

                        await user.SendMessageAsync(text: NextRefresh(session.Timer), embed: embedBuilder.Build(), components: components).ConfigureAwait(false);
                    }

                    // Reset fails counter
                    session.Fails = 0;

                    static bool EmbedsIdentical(EmbedBuilder embed1, EmbedBuilder embed2) => embed1.Fields.Count == embed2.Fields.Count && embed1.Fields.All(field1 => field1.Value.ToString() == embed2.Fields.First(field2 => field2.Name == field1.Name).Value.ToString());
                }
            }
            catch (Exception exception)
            {
                WriteLog($"{discordUserId}: Exception 1: {exception.Message}", ConsoleColor.Red);

                if (exception.Message == "Faculty server is currently down." || exception.Message.Contains("FetchPage"))
                {
                    // Update timer interval to the value of TimerIntervalAfterExceptionsInMinutes
                    session.Timer = Configuration.TimerIntervalAfterExceptionsInMinutes * 60;
                }

                ++session.Fails;

                var text = $"{NextRefresh(session.Timer)}\n\nAttempt #{session.Fails} 🔂\n\nError: {exception.Message}";

                if (message == null)
                {
                    await user.SendMessageAsync(text: text, components: DiscordHelper.CreateMessageComponent()).ConfigureAwait(false);
                }
                else
                {
                    await message.ModifyAsync(x => x.Content = text).ConfigureAwait(false);

                    if (session.Fails == 1)
                    {
                        await user.SendMessageAsync(text: $"<@{user.Id}>").ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            WriteLog($"{discordUserId}: Exception 2: {exception.Message}", ConsoleColor.Red);
        }
    }
}