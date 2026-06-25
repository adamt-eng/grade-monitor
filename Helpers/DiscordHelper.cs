using Discord;
using Discord.WebSocket;
using Grade_Monitor.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor.Helpers;

internal static class DiscordHelper
{
    private const string NoGrades = "No grades available yet.";

    /// <summary>Reads the semester currently selected in an existing grades message, if any.</summary>
    internal static string? ReadSelectedSemester(IUserMessage? message)
    {
        if (message == null)
            return null;

        var selectMenus = message.Components.OfType<ActionRowComponent>()
            .SelectMany(r => r.Components.OfType<SelectMenuComponent>())
            .ToList();

        return selectMenus.Count > 0
            ? selectMenus[0].Options.FirstOrDefault(o => o.IsDefault == true)?.Value
            : null;
    }

    /// <summary>Renders a course's grade as the body text of a Discord embed field.</summary>
    internal static string FormatCourseValue(CourseGrade course)
    {
        if (course.FinalGrade is { } letter)
            return $"||**__Course Grade: {letter}__**||";

        if (course.Components.Count > 0)
            return string.Join("\n", course.Components.Select(c => $"{c.Name}: {(c.Degree?.ToString() ?? "—")}/{c.MaxDegree}"));

        return NoGrades;
    }

    internal static async Task EnsureCommandsExistAsync(DiscordSocketClient client)
    {
        var existing = await client.GetGlobalApplicationCommandsAsync();
        var names = existing.Select(c => c.Name).ToHashSet();

        if (!names.Contains("get-grades"))
            await CreateGetGradesCommandAsync(client);

        if (!names.Contains("update-interval"))
            await CreateUpdateIntervalCommandAsync(client);
    }

    private static Task<SocketApplicationCommand> CreateGetGradesCommandAsync(DiscordSocketClient client)
    {
        var cmd = new SlashCommandBuilder()
            .WithName("get-grades")
            .WithDescription("Get grades using student id and password.")
            .AddOption("student-id", ApplicationCommandOptionType.String, "Please type your student id.", isRequired: true)
            .AddOption("password", ApplicationCommandOptionType.String, "Please type your password.", isRequired: true);

        return client.CreateGlobalApplicationCommandAsync(cmd.Build());
    }

    private static Task<SocketApplicationCommand> CreateUpdateIntervalCommandAsync(DiscordSocketClient client)
    {
        var cmd = new SlashCommandBuilder()
            .WithName("update-interval")
            .WithDescription("Set how often your grades are refreshed automatically.")
            .AddOption("normal-interval", ApplicationCommandOptionType.Integer,
                "Interval (in minutes) to refresh your grades under normal conditions.",
                isRequired: true, minValue: 5)
            .AddOption("interval-after-errors", ApplicationCommandOptionType.Integer,
                "Interval (in minutes) to retry after an error.", isRequired: true, minValue: 1);

        return client.CreateGlobalApplicationCommandAsync(cmd.Build());
    }

    private static ButtonBuilder RefreshGradesButton() =>
        new ButtonBuilder()
            .WithLabel("Refresh Grades")
            .WithStyle(ButtonStyle.Secondary)
            .WithEmote(new Emoji("\uD83D\uDD04"))
            .WithCustomId("refresh-grades");

    internal static MessageComponent CreateMessageComponent() =>
        CreateMessageComponent(null, null, false);

    internal static MessageComponent CreateMessageComponent(HashSet<string>? semesters, string? requestedSemester, bool fetchFinalGradesOnly)
    {
        var cb = new ComponentBuilder();

        if (semesters is { Count: > 0 })
        {
            cb.AddRow(new ActionRowBuilder().WithSelectMenu(BuildSemesterMenu(semesters, requestedSemester)));
            cb.AddRow(new ActionRowBuilder().WithSelectMenu(BuildModeMenu(fetchFinalGradesOnly)));
        }

        cb.AddRow(new ActionRowBuilder()
            .WithButton(RefreshGradesButton()));

        return cb.Build();
    }

    private static SelectMenuBuilder BuildSemesterMenu(IEnumerable<string> semesters, string? requested)
    {
        var ordered = SemesterOrdering.Order(semesters);

        var menu = new SelectMenuBuilder
        {
            CustomId = "select-semester",
            MinValues = 1,
            MaxValues = 1
        };

        foreach (var sem in ordered)
        {
            menu.AddOption(new SelectMenuOptionBuilder
            {
                Label = sem,
                Value = sem,
                Emote = SemesterEmoji(sem),
                IsDefault = sem == requested
            });
        }

        return menu;
    }

    private static SelectMenuBuilder BuildModeMenu(bool fetchFinalGradesOnly)
    {
        var menu = new SelectMenuBuilder
        {
            CustomId = "select-mode",
            MinValues = 1,
            MaxValues = 1
        };

        menu.AddOption(new SelectMenuOptionBuilder
        {
            Label = "Mode 1: Final Grades",
            Value = "Mode 1: Final Grades",
            Description = "Fetches only final course grade.",
            Emote = new Emoji("\uD83D\uDD34"),
            IsDefault = fetchFinalGradesOnly
        });

        menu.AddOption(new SelectMenuOptionBuilder
        {
            Label = "Mode 2: All Grades",
            Value = "Mode 2: All Grades",
            Description = "Fetches all available course grades.",
            Emote = new Emoji("\uD83D\uDFE2"),
            IsDefault = !fetchFinalGradesOnly
        });

        return menu;
    }

    private static Emoji SemesterEmoji(string sem)
    {
        if (sem.Contains("Summer"))
            return new Emoji("☀️");

        return sem.Contains("Spring") ? new Emoji("🌸") : new Emoji("🍂");
    }
}
