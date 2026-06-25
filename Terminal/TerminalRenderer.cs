using Grade_Monitor.Core.Session;
using Grade_Monitor.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System;
using System.Collections.Generic;

namespace Grade_Monitor.Terminal;

/// <summary>
/// Renders the live grade dashboard (header, table, footer) using Spectre.Console.
/// </summary>
internal static class TerminalRenderer
{
    internal static void Render(
        SessionState state,
        IReadOnlyList<CourseGrade> grades,
        IReadOnlySet<string> changedKeys,
        int secondsLeft,
        DateTime lastUpdated,
        string? error,
        int fails)
    {
        AnsiConsole.Clear();

        AnsiConsole.Write(new Rule("[bold yellow]🎓 Grade Monitor[/] [grey]— Terminal Mode[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.WriteLine();

        var mode = state.FetchFinalGrades ? "Final grades" : "All grades";
        AnsiConsole.MarkupLine(
            $"  [grey]Student[/] [bold]{Esc(state.User.StudentId)}[/]    " +
            $"[grey]Semester[/] [bold]{Esc(state.RequestedSemester ?? "—")}[/]    " +
            $"[grey]Mode[/] [bold]{mode}[/]    " +
            $"[grey]CGPA[/] [bold aqua]{Esc(state.Cgpa ?? "—")}[/]");

        if (changedKeys.Count > 0)
            AnsiConsole.MarkupLine("  [black on yellow] 🔔 Grades changed! [/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(BuildTable(grades, changedKeys));
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        AnsiConsole.MarkupLine(
            "  [bold]R[/][grey]efresh[/]   [bold]S[/][grey]emester[/]   [bold]M[/][grey]ode[/]   " +
            "[bold]I[/][grey]nterval[/]   [bold]C[/][grey]redentials[/]   [bold]Q[/][grey]uit[/]");

        AnsiConsole.MarkupLine(
            error != null ? $"  [red]⚠ {Esc(error)}[/]  [grey](attempt #{fails} · retrying in {Clock(secondsLeft)})[/]" : $"  [grey]Last updated {lastUpdated:HH:mm:ss} · next refresh in[/] [bold]{Clock(secondsLeft)}[/]");
    }

    private static Table BuildTable(IReadOnlyList<CourseGrade> grades, IReadOnlySet<string> changedKeys)
    {
        // One row per course with a separator line between rows, so courses are clearly divided.
        // Each course's component scores live in a tight aligned sub-grid to avoid wasted space.
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .ShowRowSeparators();

        table.AddColumn(new TableColumn("[bold]Course[/]"));
        table.AddColumn(new TableColumn("[bold]Grades[/]"));

        if (grades.Count == 0)
        {
            table.AddRow(new Markup("[grey]No courses found.[/]"), new Markup(string.Empty));
            return table;
        }

        foreach (var course in grades)
        {
            var key = $"{course.Code}: {course.Name}";

            var label = $"[bold]{Esc(course.Code)}[/]\n[grey]{Esc(course.Name)}[/]";
            if (changedKeys.Contains(key))
                label = $"[yellow]✨[/] {label}";

            table.AddRow(new Markup(label), BuildGradesCell(course));
        }

        return table;
    }

    private static IRenderable BuildGradesCell(CourseGrade course)
    {
        if (course.FinalGrade is { } letter)
            return TwoColumnGrid().AddRow(new Markup("[grey]Final Grade[/]"), new Markup(FinalMarkup(letter)));

        if (course.Components.Count == 0)
            return new Markup("[grey]No grades yet[/]");

        var grid = TwoColumnGrid();
        foreach (var component in course.Components)
            grid.AddRow(new Markup(Esc(component.Name)), new Markup(ScoreMarkup(component)));

        return grid;
    }

    private static Grid TwoColumnGrid()
    {
        var grid = new Grid();
        grid.AddColumn(new GridColumn().PadRight(4));
        grid.AddColumn(new GridColumn().RightAligned().PadRight(0));
        return grid;
    }

    private static string ScoreMarkup(GradeComponent component)
    {
        if (component.Degree is not { } degree)
            return $"[grey]—/{component.MaxDegree}[/]";

        var ratio = component.MaxDegree > 0 ? (double)degree / component.MaxDegree : 0;
        var color = ratio >= 0.75 ? "green" : ratio >= 0.5 ? "yellow" : "red";

        return $"[{color}]{degree}[/][grey]/{component.MaxDegree}[/]";
    }

    private static string FinalMarkup(string letter)
    {
        if (string.IsNullOrEmpty(letter))
            return "[grey]—[/]";

        var color = char.ToUpperInvariant(letter[0]) switch
        {
            'A' => "green",
            'B' => "aqua",
            'C' => "yellow",
            'D' => "orange3",
            'F' => "red",
            _ => "white"
        };

        return $"[bold {color}]{Esc(letter)}[/]";
    }

    private static string Clock(int seconds)
    {
        if (seconds < 0)
            seconds = 0;

        return $"{seconds / 60:00}:{seconds % 60:00}";
    }

    private static string Esc(string value) => Markup.Escape(value);
}
