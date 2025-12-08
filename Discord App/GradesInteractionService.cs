using Discord;
using Grade_Monitor.Core;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Discord_App;

internal static class GradesInteractionService
{
    internal static async Task GetGrades(ulong discordUserId, string interactionType)
    {
        try
        {
            // Log interaction
            Console.WriteLine();
            LoggingService.WriteLog($"{discordUserId}: {interactionType}", ConsoleColor.Cyan);

            // Stopwatch to keep track of the time it takes to fetch grades
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var user = await DiscordApp.Client.GetUserAsync(discordUserId);
            var (message, previousEmbedBuilder) = await GetGradesReportMessage(user);

            // Get user session
            if (!SessionsManager.TryGetSession(discordUserId, out var sessionState))
            {
                return;
            }

            try
            {
                // Initialize session (login + student data + semesters)
                await AppServices.SessionManager.InitializeSessionAsync(sessionState, message);

                // Fetch grades
                var gradesReport = await AppServices.SessionManager.FetchGradesAsync(sessionState);

                stopwatch.Stop();

                LoggingService.WriteLog($"{discordUserId}: Fetched grades in {stopwatch.ElapsedMilliseconds}ms", ConsoleColor.Yellow);

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
                    Title = $"Grades Report For {sessionState.User.StudentId}",
                    Description = $"**||__Cumulative GPA: {sessionState.Cgpa}__||**",
                    Timestamp = DateTime.Now,
                    Fields = embedFieldBuilders
                };

                var components = DiscordHelper.CreateMessageComponent(
                    semesters: [.. sessionState.User.Semesters.Keys],
                    requestedSemester: sessionState.RequestedSemester,
                    fetchFinalGradesOnly: sessionState.FetchFinalGrades
                );

                // If the call was from the timer and the embeds are identical (grades not changed)
                // Or if the user changed their selection of semester or mode
                // Do not send a new message, just update the existing one silently
                if (message != null && previousEmbedBuilder != null &&
                    (interactionType.Contains("SelectMenuExecuted") ||
                     (interactionType == "OnTimerElapsed" && EmbedsIdentical(embedBuilder, previousEmbedBuilder))))
                {
                    // Silently update the timestamp in the already sent message to indicate that the app is functioning as expected
                    await message.ModifyAsync(Update);

                    void Update(MessageProperties properties)
                    {
                        properties.Content = NextRefresh(sessionState.Offset);
                        properties.Embed = embedBuilder.Build();
                        properties.Components = components;
                    }
                }
                else
                {
                    // Else indicates that one of the following occurred:
                    // 1. Grade Changes Detected
                    // 2. refresh Button Executed
                    // 3. /get-grades Command Executed
                    // 4. User error; user sent a message in the DM channel, disrupting the expected single-message format

                    if (message != null)
                    {
                        await message.DeleteAsync();
                    }

                    await user.SendMessageAsync(
                        text: NextRefresh(sessionState.Offset),
                        embed: embedBuilder.Build(),
                        components: components
                    );
                }

                // Reset fails counter
                sessionState.Fails = 0;

                static bool EmbedsIdentical(EmbedBuilder embed1, EmbedBuilder embed2) =>
                    embed1.Fields.Count == embed2.Fields.Count &&
                    embed1.Fields.All(field1 =>
                        field1.Value.ToString() ==
                        embed2.Fields.First(field2 => field2.Name == field1.Name).Value.ToString());
            }
            catch (Exception exception)
            {
                LoggingService.WriteLog($"{discordUserId}: Exception 1: {exception.Message}", ConsoleColor.Red);

                if (exception.Message.Contains("FetchPage"))
                {
                    // Update timer interval to the value of TimerIntervalAfterExceptionsInMinutes
                    sessionState.Offset = DiscordApp.AppConfig.TimerIntervalAfterExceptionsInMinutes * 60;
                }

                ++sessionState.Fails;

                var text = $"""
                            {NextRefresh(sessionState.Offset)}

                            Attempt #{sessionState.Fails} 🔂

                            Error: {exception.Message}
                            """;

                if (message == null)
                {
                    await user.SendMessageAsync(text: text, components: DiscordHelper.CreateMessageComponent());
                }
                else
                {
                    await message.ModifyAsync(x => x.Content = text);

                    // Only notify user after 10 consecutive failures
                    if (sessionState.Fails == 10)
                    {
                        await user.SendMessageAsync(text: $"<@{user.Id}>");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            LoggingService.WriteLog($"{discordUserId}: Exception 2: {exception}", ConsoleColor.Red);
        }

        return;

        static string NextRefresh(double offset) => $"Next refresh <t:{((DateTimeOffset)DateTime.Now.AddSeconds(offset)).ToUnixTimeSeconds()}:R> 🕒";
    }

    private static async Task<(IUserMessage? message, EmbedBuilder? previousEmbedBuilder)> GetGradesReportMessage(IUser user)
    {
        var dmChannel = await user.CreateDMChannelAsync();

        var messages = (await dmChannel.GetMessagesAsync(limit: 5).FlattenAsync()).ToList();

        messages = [.. messages.Where(m => m.Author.Id == DiscordApp.Client.CurrentUser.Id)];

        IUserMessage? message = null;
        EmbedBuilder? previousEmbedBuilder = null;

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
                await item.DeleteAsync();
            }
        }

        return (message, previousEmbedBuilder);
    }
}
