using Grade_Monitor.Configuration;
using Grade_Monitor.Core;
using Grade_Monitor.Core.Session;
using Grade_Monitor.Helpers;
using Grade_Monitor.Models;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grade_Monitor.Terminal;

/// <summary>
/// Terminal frontend: a self-contained, interactive grade dashboard that requires no Discord bot.
/// Mirrors every Discord feature — login, semester selection, mode selection, manual refresh,
/// configurable intervals, automatic refresh with retry-on-error, and change notifications.
/// </summary>
internal sealed class TerminalApp
{
    private enum UserAction
    {
        Tick,
        Refresh,
        Semester,
        Mode,
        Interval,
        Credentials,
        ToggleHidden,
        Quit
    }

    private SessionState _state = null!;
    private IReadOnlyList<CourseGrade> _grades = [];
    private Dictionary<string, string> _signatures = [];
    private HashSet<string> _changedKeys = [];
    private bool _hasBaseline;

    private string? _error;
    private int _fails;
    private DateTime _lastUpdated;
    private int _secondsLeft;

    internal async Task RunAsync()
    {
        // Keep the rendered dashboard clean; route the API/HTTP logs away from the console.
        LoggingService.Enabled = false;
        Console.OutputEncoding = Encoding.UTF8;

        var user = EnsureUser();
        _state = new SessionState(user);

        AnsiConsole.Cursor.Hide();
        try
        {
            await RefreshAsync("Logging in and fetching grades…");

            var running = true;
            while (running)
            {
                TerminalRenderer.Render(_state, _grades, _changedKeys, App.Config.HideGrades, _secondsLeft, _lastUpdated, _error, _fails);

                switch (await WaitForActionAsync())
                {
                    case UserAction.Quit:
                        running = false;
                        break;
                    case UserAction.Refresh:
                        await RefreshAsync("Refreshing grades…");
                        break;
                    case UserAction.Semester:
                        if (ChooseSemester())
                        {
                            _hasBaseline = false;
                            await RefreshAsync("Fetching grades…");
                        }

                        break;
                    case UserAction.Mode:
                        ChooseMode();
                        _hasBaseline = false;
                        await RefreshAsync("Fetching grades…");
                        break;
                    case UserAction.Interval:
                        ChooseInterval();
                        _secondsLeft = NormalSeconds();
                        break;
                    case UserAction.Credentials:
                        ChangeCredentials();
                        _hasBaseline = false;
                        await RefreshAsync("Logging in…");
                        break;
                    case UserAction.ToggleHidden:
                        App.Config.HideGrades = !App.Config.HideGrades;
                        ConfigurationManager.Save(App.Config);
                        break;
                    case UserAction.Tick:
                        _secondsLeft--;
                        if (_secondsLeft <= 0)
                            await RefreshAsync("Refreshing grades…");
                        break;
                }
            }
        }
        finally
        {
            AnsiConsole.Cursor.Show();
        }

        AnsiConsole.MarkupLine("[grey]Stopped monitoring. Goodbye![/]");
    }

    private async Task RefreshAsync(string label)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("yellow"))
            .StartAsync(label, async _ => await FetchAsync());

        _secondsLeft = _error == null ? NormalSeconds() : ErrorSeconds();
    }

    private async Task FetchAsync()
    {
        try
        {
            await AppServices.SessionManager.InitializeSessionAsync(_state);
            var grades = await AppServices.SessionManager.FetchGradesAsync(_state);

            var signatures = grades.ToDictionary(Key, Signature);

            _changedKeys = _hasBaseline ? ComputeChanges(_signatures, signatures) : [];
            _hasBaseline = true;
            _signatures = signatures;

            _grades = grades;
            _error = null;
            _fails = 0;
            _lastUpdated = DateTime.Now;

            if (_changedKeys.Count > 0)
                Beep();
        }
        catch (Exception exception)
        {
            _error = exception.Message;
            _fails++;
        }
    }

    private static HashSet<string> ComputeChanges(
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current)
    {
        var changed = new HashSet<string>();

        foreach (var (key, signature) in current)
            if (!previous.TryGetValue(key, out var old) || old != signature)
                changed.Add(key);

        return changed;
    }

    private static string Key(CourseGrade course) => $"{course.Code}: {course.Name}";

    private static string Signature(CourseGrade course) =>
        course.FinalGrade is { } letter
            ? $"final:{letter}"
            : string.Join("|", course.Components.Select(c => $"{c.Name}={c.Degree?.ToString() ?? "-"}/{c.MaxDegree}"));

    private async Task<UserAction> WaitForActionAsync()
    {
        // Poll for a keypress for roughly one second, so the countdown ticks once per loop.
        for (var i = 0; i < 10; i++)
        {
            if (Console.KeyAvailable)
            {
                switch (Console.ReadKey(intercept: true).Key)
                {
                    case ConsoleKey.R: return UserAction.Refresh;
                    case ConsoleKey.S: return UserAction.Semester;
                    case ConsoleKey.M: return UserAction.Mode;
                    case ConsoleKey.I: return UserAction.Interval;
                    case ConsoleKey.C: return UserAction.Credentials;
                    case ConsoleKey.H: return UserAction.ToggleHidden;
                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        return UserAction.Quit;
                }
            }

            await Task.Delay(100);
        }

        return UserAction.Tick;
    }

    private bool ChooseSemester()
    {
        var ordered = SemesterOrdering.Order(_state.Semesters).ToList();
        if (ordered.Count == 0)
            return false;

        AnsiConsole.Cursor.Show();
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select a semester[/]")
                .PageSize(15)
                .HighlightStyle(Style.Parse("yellow"))
                .UseConverter(s => s == _state.RequestedSemester ? $"{s} [grey](current)[/]" : s)
                .AddChoices(ordered));
        AnsiConsole.Cursor.Hide();

        _state.RequestedSemester = selection;
        return true;
    }

    private void ChooseMode()
    {
        const string allGrades = "Mode 2: All Grades";
        const string finalGrades = "Mode 1: Final Grades";

        AnsiConsole.Cursor.Show();
        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select a grade mode[/]")
                .HighlightStyle(Style.Parse("yellow"))
                .AddChoices(allGrades, finalGrades));
        AnsiConsole.Cursor.Hide();

        _state.FetchFinalGrades = selection == finalGrades;
    }

    private static void ChooseInterval()
    {
        AnsiConsole.Cursor.Show();

        var normal = AnsiConsole.Prompt(
            new TextPrompt<int>("[green]Normal refresh interval[/] [grey](minutes)[/]:")
                .DefaultValue(App.Config.TimerIntervalInMinutes)
                .Validate(value => value >= 5 ? ValidationResult.Success() : ValidationResult.Error("[red]Must be at least 5 minutes[/]")));

        var retry = AnsiConsole.Prompt(
            new TextPrompt<int>("[green]Retry interval after errors[/] [grey](minutes)[/]:")
                .DefaultValue(App.Config.TimerIntervalAfterExceptionsInMinutes)
                .Validate(value => value >= 1 ? ValidationResult.Success() : ValidationResult.Error("[red]Must be at least 1 minute[/]")));

        AnsiConsole.Cursor.Hide();

        App.Config.TimerIntervalInMinutes = normal;
        App.Config.TimerIntervalAfterExceptionsInMinutes = retry;
        ConfigurationManager.Save(App.Config);
    }

    private void ChangeCredentials()
    {
        AnsiConsole.Cursor.Show();
        var (studentId, password) = PromptCredentials();
        AnsiConsole.Cursor.Hide();

        _state.User.StudentId = studentId;
        _state.User.Password = password;
        _state.User.AccessToken = null; // force a fresh login with the new credentials
        ConfigurationManager.Save(App.Config);
    }

    private static User EnsureUser()
    {
        if (App.Config.TerminalUser is { } existing)
            return existing;

        AnsiConsole.Write(new Rule("[bold yellow]🎓 Grade Monitor — Terminal Setup[/]").RuleStyle("grey"));
        AnsiConsole.MarkupLine("[grey]Enter your faculty portal credentials. They are stored locally in config.json.[/]");
        AnsiConsole.WriteLine();

        var (studentId, password) = PromptCredentials();

        var user = new User { StudentId = studentId, Password = password };
        App.Config.TerminalUser = user;
        ConfigurationManager.Save(App.Config);

        return user;
    }

    private static (string StudentId, string Password) PromptCredentials()
    {
        var studentId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Student ID[/]:")
                .Validate(value => string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Error("[red]Student ID cannot be empty[/]")
                    : ValidationResult.Success()));

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Password[/]:")
                .Secret()
                .Validate(value => string.IsNullOrWhiteSpace(value)
                    ? ValidationResult.Error("[red]Password cannot be empty[/]")
                    : ValidationResult.Success()));

        return (studentId.Trim(), password);
    }

    private static int NormalSeconds() => App.Config.TimerIntervalInMinutes * 60;
    private static int ErrorSeconds() => App.Config.TimerIntervalAfterExceptionsInMinutes * 60;

    private static void Beep()
    {
        try
        {
            Console.Beep();
        }
        catch
        {
            // Beeping is best-effort; ignore platforms/terminals that don't support it.
        }
    }
}
