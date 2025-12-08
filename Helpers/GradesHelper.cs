using Discord;
using Grade_Monitor.Core;
using Grade_Monitor.Discord_App;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Helpers;

internal static class GradesHelper
{
    internal static async Task GetGrades(ulong discordUserId, string interactionType)
    {
        try
        {
            // Log interaction
            Console.WriteLine();
            LoggingService.WriteLog($"{discordUserId}: {interactionType}", ConsoleColor.Cyan);

            // Stopwatch to keep track of how fast grades are being fetched
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            // Get the DM channel between the user and the bot
            var user = await DiscordApp.Client.GetUserAsync(discordUserId);
            var dmChannel = await user.CreateDMChannelAsync();

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
                    await item.DeleteAsync();
                }
            }

            // Get user session
            var sessionFound = SessionManager.TryGetSession(discordUserId, out var session);

            if (!sessionFound)
            {
                return;
            }

            try
            {
                // Attempt login
                if (await session.Login().ConfigureAwait(false))
                {
                    await session.LoadStudentData();

                    session.DetermineRequestedSemester(message);

                    var gradesReport = await session.FetchGradesReport();

                    stopwatch.Stop();
                    LoggingService.WriteLog($"{discordUserId}: Elapsed time: {stopwatch.ElapsedMilliseconds}ms", ConsoleColor.Yellow);

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
                        await message.ModifyAsync(Update);

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
                            await message.DeleteAsync();
                        }

                        await user.SendMessageAsync(text: NextRefresh(session.Timer), embed: embedBuilder.Build(), components: components);
                    }

                    // Reset fails counter
                    session.Fails = 0;

                    static bool EmbedsIdentical(EmbedBuilder embed1, EmbedBuilder embed2) => embed1.Fields.Count == embed2.Fields.Count && embed1.Fields.All(field1 => field1.Value.ToString() == embed2.Fields.First(field2 => field2.Name == field1.Name).Value.ToString());
                }
            }
            catch (Exception exception)
            {
                LoggingService.WriteLog($"{discordUserId}: Exception 1: {exception.Message}", ConsoleColor.Red);

                if (exception.Message == "Faculty server is currently down." || exception.Message.Contains("FetchPage"))
                {
                    // Update timer interval to the value of TimerIntervalAfterExceptionsInMinutes
                    session.Timer = DiscordApp.AppConfig.TimerIntervalAfterExceptionsInMinutes * 60;
                }

                ++session.Fails;

                var text = $"{NextRefresh(session.Timer)}\n\nAttempt #{session.Fails} 🔂\n\nError: {exception.Message}";

                if (message == null)
                {
                    await user.SendMessageAsync(text: text, components: DiscordHelper.CreateMessageComponent());
                }
                else
                {
                    await message.ModifyAsync(x => x.Content = text);

                    if (session.Fails == 1)
                    {
                        await user.SendMessageAsync(text: $"<@{user.Id}>");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            LoggingService.WriteLog($"{discordUserId}: Exception 2: {exception.Message}", ConsoleColor.Red);
        }

        return;

        static string NextRefresh(double intervalInSeconds) => $"Next refresh <t:{((DateTimeOffset)DateTime.Now.AddSeconds(intervalInSeconds)).ToUnixTimeSeconds()}:R> 🕒";
    }
}