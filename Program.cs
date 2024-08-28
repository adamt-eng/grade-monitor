using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

namespace Grade_Monitor;

internal class Program
{
    private static bool _botInitialized;

    private static string _nextRefresh = string.Empty;

    private static readonly Timer Timer = new();

    private static readonly Dictionary<ulong, Session> Sessions = [];
    private static readonly DiscordSocketClient Client = new(new DiscordSocketConfig { GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildScheduledEvents & ~GatewayIntents.GuildInvites });

    internal static Configuration Configuration;
    internal static ConfigurationManager ConfigurationManager;

    private static async Task Main()
    {
        ConfigurationManager = new ConfigurationManager("config.json");
        Configuration = ConfigurationManager.LoadSettings();

        Client.Log += message =>
        {
            WriteLog(message.Message, ConsoleColor.Gray);
            return Task.CompletedTask;
        };

        Client.SlashCommandExecuted += async socketSlashCommand =>
        {
            // Acknowledge this interaction
            await socketSlashCommand.DeferAsync(ephemeral: true).ConfigureAwait(false);

            // Extract studentId and password
            var dataOptions = socketSlashCommand.Data.Options.ToList();
            var studentId = dataOptions[0].Value.ToString();
            var password = dataOptions[1].Value.ToString();

            var discordUserId = socketSlashCommand.User.Id;

            // Save to config.json
            Configuration.User = new User { DiscordUserId = discordUserId, StudentId = studentId, Password = password };
            ConfigurationManager.SaveSettings(Configuration);

            // Initialize new session and store it
            Sessions[discordUserId] = new Session(studentId: studentId, password: password);

            GetGrades(discordUserId: discordUserId, interactionType: "SlashCommandExecuted");

            await socketSlashCommand.FollowupAsync("You will receive a private message with your grades within a few seconds.", ephemeral: true).ConfigureAwait(false);
        };

        Client.SelectMenuExecuted += async socketMessageComponent =>
        {
            // Acknowledge this interaction
            await socketMessageComponent.DeferAsync(ephemeral: true).ConfigureAwait(false);

            var discordUserId = socketMessageComponent.User.Id;

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

            GetGrades(discordUserId: discordUserId, interactionType: $"SelectMenuExecuted ({customId})");
        };

        Client.ButtonExecuted += async socketMessageComponent =>
        {
            // Acknowledge this interaction
            await socketMessageComponent.DeferAsync(ephemeral: true).ConfigureAwait(false);
            GetGrades(discordUserId: socketMessageComponent.User.Id, interactionType: "ButtonExecuted");
        };

        Client.Ready += async () =>
        {
            var guild = Client.Guilds.First();

            if ((await guild.GetApplicationCommandsAsync().ConfigureAwait(false)).All(command => command.Name != "get-grades"))
            {
                await guild.CreateApplicationCommandAsync(new SlashCommandBuilder
                {
                    Name = "get-grades",
                    Description = "Get your grades.",
                    Options =
                    {
                        new SlashCommandOptionBuilder { Name = "student-id", Description = "Student ID", Type = ApplicationCommandOptionType.String, IsRequired = true },
                        new SlashCommandOptionBuilder { Name = "password", Description = "Password", Type = ApplicationCommandOptionType.String, IsRequired = true }
                    }
                }.Build()).ConfigureAwait(false);
            }

            if (!_botInitialized)
            {
                _botInitialized = true;

                static void OnTimerElapsed(object sender, ElapsedEventArgs e)
                {
                    Timer.Interval = Configuration.TimerIntervalInMinutes * 60000;

                    UpdateTimestamp();

                    var user = Configuration.User;

                    if (user != null)
                    {
                        var discordUserId = user.DiscordUserId;

                        if (!Sessions.ContainsKey(discordUserId))
                        {
                            Sessions[discordUserId] = new Session(studentId: user.StudentId, password: user.Password);
                        }

                        GetGrades(discordUserId, sender == null ? "Ready" : "OnTimerElapsed");
                    }
                }

                OnTimerElapsed(null, null);

                Timer.Elapsed += OnTimerElapsed;
                Timer.Start();
            }
        };

        await Client.LoginAsync(TokenType.Bot, Configuration.BotToken).ConfigureAwait(false);
        await Client.StartAsync().ConfigureAwait(false);
        await Task.Delay(-1).ConfigureAwait(false);
    }
    private static void UpdateTimestamp()
    {
        var offset = 10; // In ideal conditions, takes on average 10 seconds to read grades

        _nextRefresh = $"Next refresh <t:{((DateTimeOffset)DateTime.Now.AddMilliseconds(Timer.Interval).AddSeconds(offset)).ToUnixTimeSeconds()}:R> {new Emoji("🕒")}";
    }
    internal static void WriteLog(string log, ConsoleColor consoleColor)
    {
        Console.ForegroundColor = consoleColor;
        Console.WriteLine($"{DateTime.Now:dd/MM/yyyy HH:mm:ss} {log}");
        Console.ResetColor();
    }
    private static async void GetGrades(ulong discordUserId, string interactionType)
    {
        try
        {
            // New line for seperation
            Console.WriteLine();

            // Log interaction
            WriteLog($"{interactionType} {discordUserId}", ConsoleColor.Cyan);

            // Stopwatch to keep track of how fast we're obtaining grades
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var user = await Client.GetUserAsync(discordUserId).ConfigureAwait(false);
            var dmChannel = await user.CreateDMChannelAsync().ConfigureAwait(false);
            var messages = (await dmChannel.GetMessagesAsync().FlattenAsync().ConfigureAwait(false)).ToList();

            var message = (IUserMessage)null;
            var previousEmbedBuilder = (EmbedBuilder)null;

            if (messages.Count == 1)
            {
                message = (IUserMessage)messages.First();

                if (message.Embeds.Count == 1)
                {
                    previousEmbedBuilder = message.Embeds.First().ToEmbedBuilder();
                }
                else
                {
                    message = null;
                }
            }

            if (message == null)
            {
                foreach (var item in messages)
                {
                    await item.DeleteAsync().ConfigureAwait(false);
                }
            }

            var session = Sessions[discordUserId];

            try
            {
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
                        Title = $"Grades Report For {session.StudentId}",
                        Description = $"**||__Cumulative GPA: {session.Cgpa}__||**",
                        Timestamp = DateTime.Now,
                        Fields = embedFieldBuilders
                    };

                    var components = GenerateComponentBuilder(session.Semesters, session.RequestedSemester, session.HeavyLoad);

                    // If the call was from the timer and the embeds are identical (aka grades not changed)
                    // Or if the user changed the semester or load selection
                    if (message != null && (interactionType.Contains("SelectMenuExecuted") || interactionType == "OnTimerElapsed" && embedBuilder.IdenticalTo(previousEmbedBuilder)))
                    {
                        // Silently update the timestamp in the message to indicate that the app is working

                        await message.ModifyAsync(Update).ConfigureAwait(false);

                        void Update(MessageProperties properties)
                        {
                            properties.Content = _nextRefresh;
                            properties.Embed = embedBuilder.Build();
                            properties.Components = components;
                        }
                    }
                    // Else indicates that one of the following occurred:
                    // 1. Grades Updated
                    // 2. Refresh Button Executed
                    // 3. Registration Slash Command Executed
                    // 4. 0 messages or more than 1 message were in the DM Channel
                    else
                    {
                        if (message != null)
                        {
                            await message.DeleteAsync().ConfigureAwait(false);
                        }

                        await user.SendMessageAsync(text: _nextRefresh, embed: embedBuilder.Build(), components: components).ConfigureAwait(false);
                    }

                    session.Fails = 0;
                }
                else
                {
                    await user.SendMessageAsync(text: "`Incorrect Student ID or Password.`", components: new ComponentBuilder().WithButton(RefreshButton()).Build()).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                WriteLog($"Exception: {exception}", ConsoleColor.Red);

                Timer.Interval = Configuration.TimerIntervalAfterExceptionsInMinutes * 60000;

                UpdateTimestamp();

                session.Fails++;

                var text = $"{_nextRefresh}🔂 ({session.Fails})";

                if (message != null)
                {
                    await message.ModifyAsync(x => x.Content = text).ConfigureAwait(false);
                }
                else
                {
                    await user.SendMessageAsync(text: $"{text}\n\n`Faculty server is currently down.`", components: new ComponentBuilder().WithButton(RefreshButton()).Build()).ConfigureAwait(false);
                }
            }
        }
        catch { }
    }

    private static ButtonBuilder RefreshButton() => new ButtonBuilder().WithLabel("Refresh").WithStyle(ButtonStyle.Secondary).WithEmote(new Emoji("\ud83d\udd04")).WithCustomId("refresh");

    private static MessageComponent GenerateComponentBuilder(HashSet<string> semesters, string requestedSemester, bool heavyLoad)
    {
        var selectSemesterMenuOptions = new List<SelectMenuOptionBuilder>();

        foreach (var semester in semesters)
        {
            selectSemesterMenuOptions.Add(new SelectMenuOptionBuilder
            {
                Label = semester,
                Value = semester,
                Emote = semester.Contains("Summer") ? new Emoji("☀️") : semester.Contains("Spring") ? new Emoji("🌸") : new Emoji("🍂"),
                IsDefault = semester == requestedSemester
            });
        }

        var selectSemesterMenu = new SelectMenuBuilder
        {
            CustomId = "select-semester",
            MinValues = 1,
            MaxValues = 1,
            Options = selectSemesterMenuOptions
        };

        var selectLoadMenu = new SelectMenuBuilder
        {
            CustomId = "select-load",
            MinValues = 1,
            MaxValues = 1,
            Options =
            [
                new SelectMenuOptionBuilder
                {
                    Label = "Heavy Load",
                    Value = "Heavy Load",
                    Description = "Use when faculty servers are under heavy load",
                    Emote = new Emoji("\ud83d\udd34"),
                    IsDefault = heavyLoad
                },
                new SelectMenuOptionBuilder
                {
                    Label = "Normal Load",
                    Value = "Normal Load",
                    Description = "Use for detailed course grades",
                    Emote = new Emoji("\ud83d\udfe2"),
                    IsDefault = !heavyLoad
                }
            ]
        };

        return new ComponentBuilder
        {
            ActionRows =
            [
                new ActionRowBuilder().WithSelectMenu(selectSemesterMenu),
                new ActionRowBuilder().WithSelectMenu(selectLoadMenu),
                new ActionRowBuilder().WithButton(RefreshButton())
            ]
        }.Build();
    }
    internal static async Task<string> FetchPage(string url, HttpClient httpClient)
    {
        var retryCount = 10;
        var retryDelay = 3000;
        var attempt = 0;

        while (attempt < retryCount)
        {
            try
            {
                WriteLog(url, ConsoleColor.DarkGreen);
                using var response = await httpClient.GetAsync(url).ConfigureAwait(false);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : throw new Exception(response.ReasonPhrase);
            }
            catch (Exception ex)
            {
                WriteLog($"Exception thrown: {ex.Message}", ConsoleColor.Red);

                attempt++;

                if (attempt == retryCount)
                {
                    throw new Exception($"Failed to fetch page after {attempt} attempts. Last error: {ex.Message}", ex);
                }

                await Task.Delay(retryDelay * attempt).ConfigureAwait(false);
            }
        }

        throw new Exception("Unreachable Code.");
    }
}