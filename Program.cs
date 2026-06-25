using Grade_Monitor.Core;
using Grade_Monitor.Discord_App;
using Grade_Monitor.Terminal;
using Spectre.Console;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Grade_Monitor;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var mode = ResolveMode(args);

        if (mode == RunMode.Terminal)
            await new TerminalApp().RunAsync();
        else
            await new DiscordApp().StartAsync();
    }

    private static RunMode ResolveMode(string[] args)
    {
        // Allow skipping the picker with a flag (e.g. for autostart / a VPS service).
        foreach (var arg in args.Select(a => a.Trim().ToLowerInvariant()))
        {
            if (arg is "--terminal" or "-t" or "terminal")
                return RunMode.Terminal;
            if (arg is "--discord" or "-d" or "discord")
                return RunMode.Discord;
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;

        AnsiConsole.Write(new Rule("[bold yellow]🎓 Grade Monitor[/]").RuleStyle("grey"));

        return AnsiConsole.Prompt(
            new SelectionPrompt<RunMode>()
                .Title("How would you like to run [bold]Grade Monitor[/]?")
                .HighlightStyle(Style.Parse("yellow"))
                .UseConverter(m => m == RunMode.Terminal
                    ? "💻 Terminal mode  [grey](no Discord required)[/]"
                    : "🤖 Discord bot")
                .AddChoices(RunMode.Terminal, RunMode.Discord));
    }
}
