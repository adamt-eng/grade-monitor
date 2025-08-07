using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using Discord.WebSocket;

namespace Grade_Monitor.Utilities;

internal static class DiscordHelper
{
    internal static void EnsureCommandsExist(DiscordSocketClient client)
    {
        var getGradesCommandExists = client.GetGlobalApplicationCommandsAsync().Result.Any(command => command.Name == "get-grades");
        if (!getGradesCommandExists)
        {
            var globalCommand = new SlashCommandBuilder
            {
                Name = "get-grades",
                Description = "Get grades using student id and password."
            };

            globalCommand.AddOptions
            (
                new SlashCommandOptionBuilder
                {
                    Name = "student-id",
                    Description = "Please type your student id.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true
                },
                new SlashCommandOptionBuilder
                {
                    Name = "password",
                    Description = "Please type your password.",
                    Type = ApplicationCommandOptionType.String,
                    IsRequired = true
                }
            );

            client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
        }

        var updateIntervalCommandExists = client.GetGlobalApplicationCommandsAsync().Result.Any(command => command.Name == "update-interval");
        if (!updateIntervalCommandExists)
        {
            var globalCommand = new SlashCommandBuilder
            {
                Name = "update-interval",
                Description = "Set how often your grades are refreshed automatically."
            };

            globalCommand.AddOptions
            (
                new SlashCommandOptionBuilder
                {
                    Name = "normal-interval",
                    Description = "Interval (in minutes) to refresh your grades under normal conditions.",
                    Type = ApplicationCommandOptionType.Integer,
                    IsRequired = true,
                    MinValue = 5
                },
                new SlashCommandOptionBuilder
                {
                    Name = "interval-after-errors",
                    Description = "Interval (in minutes) to retry refreshing grades after an error occurs.",
                    Type = ApplicationCommandOptionType.Integer,
                    IsRequired = true,
                    MinValue = 1
                }
            );

            client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
        }
    }

    private static ButtonBuilder RefreshGradesButton() => new ButtonBuilder().WithLabel("Refresh Grades").WithStyle(ButtonStyle.Secondary).WithEmote(new Emoji("\ud83d\udd04")).WithCustomId("refresh-grades");
    private static ButtonBuilder RefetchCoursesButton() => new ButtonBuilder().WithLabel("Refetch Courses").WithStyle(ButtonStyle.Secondary).WithEmote(new Emoji("\ud83d\udd04")).WithCustomId("refetch-courses");

    internal static MessageComponent CreateMessageComponent() => CreateMessageComponent(semesters: null, requestedSemester: null, fetchFinalGradesOnly: false);
    internal static MessageComponent CreateMessageComponent(HashSet<string> semesters, string requestedSemester, bool fetchFinalGradesOnly)
    {
        var builder = new ComponentBuilder();

        if ((semesters != null || requestedSemester != null) && semesters is { Count: > 0 })
        {
            var selectSemesterMenu = new SelectMenuBuilder
            {
                CustomId = "select-semester",
                MinValues = 1,
                MaxValues = 1,
                Options = [.. semesters.OrderBy(semester =>
                {
                    var semesterSplit = semester.Split(' ');
                    
                    return int.Parse(semesterSplit[1]) * 3 + semesterSplit[0] switch
                    {
                        "Spring" => 0,
                        "Summer" => 1,
                        "Fall" => 2,
                        _ => throw new ArgumentOutOfRangeException(nameof(semester))
                    };
                })
                .Select(semester => new SelectMenuOptionBuilder
                {
                    Label = semester,
                    Value = semester,
                    Emote = semester.Contains("Summer") ? new Emoji("☀️") : semester.Contains("Spring") ? new Emoji("🌸") : new Emoji("🍂"),
                    IsDefault = semester == requestedSemester
                })]
            };

            var selectModeMenu = new SelectMenuBuilder
            {
                CustomId = "select-mode",
                MinValues = 1,
                MaxValues = 1,
                Options =
                [
                    new SelectMenuOptionBuilder
                    {
                        Label = "Mode 1: Final Grades",
                        Value = "Mode 1: Final Grades",
                        Description = "Fetches only final course grade. This can be faster as it visits fewer pages.",
                        Emote = new Emoji("\ud83d\udd34"),
                        IsDefault = fetchFinalGradesOnly
                    },
                    new SelectMenuOptionBuilder
                    {
                        Label = "Mode 2: All Grades",
                        Value = "Mode 2: All Grades",
                        Description = "Fetches all course grades such as final, midterm, activities, etc.",
                        Emote = new Emoji("\ud83d\udfe2"),
                        IsDefault = !fetchFinalGradesOnly
                    }
                ]
            };

            builder.AddRow(new ActionRowBuilder().WithSelectMenu(selectSemesterMenu));
            builder.AddRow(new ActionRowBuilder().WithSelectMenu(selectModeMenu));
        }

        builder.AddRow(new ActionRowBuilder().WithButton(RefreshGradesButton()).WithButton(RefetchCoursesButton()));

        return builder.Build();
    }
}