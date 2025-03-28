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

        // Slash command handler
        Client.SlashCommandExecuted += async socketSlashCommand =>
        {
            // Acknowledge this interaction
            await socketSlashCommand.DeferAsync(ephemeral: true).ConfigureAwait(false);

            var discordUserId = socketSlashCommand.User.Id;
            var dataOptions = socketSlashCommand.Data.Options.ToList();
            var option1 = dataOptions[0].Value.ToString();
            var option2 = dataOptions[1].Value.ToString();

            if (socketSlashCommand.CommandName == "add-cookies")
            {
                var session = Sessions[discordUserId];

                if (session != null)
                {
                    var user = session.User;
                    user.XsrfToken = option1;
                    user.LaravelSession = option2;

                    // Update config.json
                    ConfigurationManager.Save(Configuration);

                    await socketSlashCommand.FollowupAsync("Cookies added successfully.", ephemeral: true).ConfigureAwait(false);
                }
                else
                {
                    await socketSlashCommand.FollowupAsync("User not registered.", ephemeral: true).ConfigureAwait(false);
                }
            }
            else
            {
                // Save new user
                var user = new User
                {
                    DiscordUserId = discordUserId,
                    StudentId = option1,
                    Password = option2
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
            var interval = Configuration.TimerIntervalInMinutes * 60;
            
            async void OnTimerElapsed(object sender, ElapsedEventArgs e)
            {
                // Get user count
                var userCount = Configuration.Users.Count;

                // Divide the total interval specified in the configuration by the user count
                // This will give us the time that is between each user's auto-refresh request
                // This makes sure that the auto-refresh requests are as far away as possible from each other
                var intervalPerUser = interval / userCount;

                // Track user index
                var index = 0;

                foreach (var user in Configuration.Users)
                {
                    var discordUserId = user.DiscordUserId;

                    if (!Sessions.TryGetValue(discordUserId, out var session))
                    {
                        var timer = intervalPerUser * index;

                        session = new Session(user: user) { Timer = timer };
                        Sessions[discordUserId] = session;

                        WriteLog($"{discordUserId}: Created session, starting monitoring in {timer} seconds.", ConsoleColor.Cyan);
                    }

                    if (session.Timer == 0)
                    {
                        session.Timer = interval;
                        await GetGrades(discordUserId, "OnTimerElapsed").ConfigureAwait(false);
                    }

                    --session.Timer;

                    ++index;
                }
            }

            Timer.Interval = 1000;
            Timer.Elapsed += OnTimerElapsed;
            Timer.Start();

            return Task.CompletedTask;
        };

        await Client.LoginAsync(TokenType.Bot, Configuration.BotToken).ConfigureAwait(false);
        await Client.StartAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }

    private static string NextRefresh(double intervalInSeconds) => $"Next refresh <t:{((DateTimeOffset)DateTime.Now.AddSeconds(intervalInSeconds)).ToUnixTimeSeconds()}:R> {new Emoji("🕒")}";

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
            WriteLog($"{discordUserId}: {interactionType}", ConsoleColor.Cyan);

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
                }
                else
                {
                    await user.SendMessageAsync(text: "`Incorrect Student ID or Password.`", components: new ComponentBuilder().WithButton(DiscordHelper.CreateRefreshButton()).Build()).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                WriteLog($"{discordUserId}: Exception 1: {exception.Message}", ConsoleColor.Red);

                // Tracking the fails count allows us to track how long the faculty server has been down
                ++session.Fails;
                var text = $"{NextRefresh(session.Timer)}🔂 ({session.Fails})";

                if (exception.Message == "Filling questionnaire is required.")
                {
                    text += $" {exception.Message}";
                }
                else
                {
                    // Update timer interval to the value of TimerIntervalAfterExceptionsInMinutes
                    session.Timer = Configuration.TimerIntervalAfterExceptionsInMinutes * 60;
                }

                if (message != null)
                {
                    await message.ModifyAsync(x => x.Content = text).ConfigureAwait(false);
                }
                else if (exception.Message == "laravel_session and XSRF-TOKEN required from user.")
                {
                    await user.SendMessageAsync(text: "`Please use the 'add-cookies' slash command to bypass CAPTCHA.`", components: new ComponentBuilder().WithButton(DiscordHelper.CreateRefreshButton()).Build()).ConfigureAwait(false);
                }
                else
                {
                    await user.SendMessageAsync(text: $"{text}\n\n`Faculty server is currently down.`", components: new ComponentBuilder().WithButton(DiscordHelper.CreateRefreshButton()).Build()).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            WriteLog($"{discordUserId}: Exception 2: {exception.Message}", ConsoleColor.Red);
        }
    }
}