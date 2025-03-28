using System.Collections.Generic;
using Discord;

namespace Grade_Monitor.Utilities;

internal static class DiscordHelper
{
    internal static ButtonBuilder CreateRefreshButton() => new ButtonBuilder().WithLabel("Refresh").WithStyle(ButtonStyle.Secondary).WithEmote(new Emoji("\ud83d\udd04")).WithCustomId("refresh");

    internal static MessageComponent CreateMessageComponent(HashSet<string> semesters, string requestedSemester, bool heavyLoad)
    {
        var selectSemesterMenuOptions = new List<SelectMenuOptionBuilder>();

        // Add each semester as a selectable option to selectSemesterMenuOptions
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