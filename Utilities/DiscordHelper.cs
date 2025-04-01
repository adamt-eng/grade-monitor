using System;
using System.Collections.Generic;
using System.Linq;
using Discord;

namespace Grade_Monitor.Utilities;

internal static class DiscordHelper
{
    internal static ButtonBuilder CreateRefreshButton() => new ButtonBuilder().WithLabel("Refresh").WithStyle(ButtonStyle.Secondary).WithEmote(new Emoji("\ud83d\udd04")).WithCustomId("refresh");

    internal static MessageComponent CreateMessageComponent(HashSet<string> semesters, string requestedSemester, bool heavyLoad)
    {
        var selectSemesterMenuOptions = new List<SelectMenuOptionBuilder>();

        // Sort the semesters in chronological order
        var orderedSemesters = semesters.OrderBy(static s =>
        {
            var semester = s.Split(' ');

            var season = semester[0];
            var year = int.Parse(semester[1]);

            // Assign a numerical value to each season for proper ordering
            var seasonOrder = season switch
            {
                "Spring" => 0,
                "Summer" => 1,
                "Fall" => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(s))
            };

            // Combine year and season order
            return year * 3 + seasonOrder;
        }).ToList();

        // Add each semester as a selectable option to selectSemesterMenuOptions
        foreach (var semester in orderedSemesters)
        {
            selectSemesterMenuOptions.Add(new SelectMenuOptionBuilder
            {
                Label = semester,
                Value = semester,
                Emote = semester.Contains("Summer") ? new Emoji("☀️") : semester.Contains("Spring") ? new Emoji("🌸") : new Emoji("🍂"),
                IsDefault = semester == requestedSemester
            });
        }

        // Create the select menu; select-semester using the previously set selectSemesterMenuOptions
        var selectSemesterMenu = new SelectMenuBuilder
        {
            CustomId = "select-semester",
            MinValues = 1,
            MaxValues = 1,
            Options = selectSemesterMenuOptions
        };

        // Create the select menu; select-load
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

        // Return the component builder containing:
        // the select-semester select menu
        // the select-load select menu
        // the refresh button
        return new ComponentBuilder
        {
            ActionRows =
            [
                new ActionRowBuilder().WithSelectMenu(selectSemesterMenu),
                new ActionRowBuilder().WithSelectMenu(selectLoadMenu),
                new ActionRowBuilder().WithButton(CreateRefreshButton())
            ]
        }.Build();
    }
}