using System.CommandLine;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class HelpCommand
{
    public static Command Build()
    {
        var command = new Command("help", "Show command reference");
        command.SetAction((_) =>
        {
            AnsiConsole.Write(new FigletText("BellaBoot").Color(Color.HotPink));
            AnsiConsole.MarkupLine("[bold]Umbraco Bellissima package scaffolding CLI[/]");
            AnsiConsole.WriteLine();

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.HotPink)
                .AddColumn("[bold]Command[/]")
                .AddColumn("[bold]Description[/]")
                .AddColumn("[bold]Key options[/]");

            table.AddRow(
                "[green]new[/] [grey]<name>[/]",
                "Scaffold a new package project",
                "[grey]--output -o, --author[/]");

            table.AddRow(
                "[green]target[/] [grey][version][/]",
                "Spin up an Umbraco test instance\nwith uSync + backend reference",
                "[grey]--output -o, --models-mode -mm[/]");

            table.AddRow(
                "[green]nuget[/] [grey][version][/]",
                "Bump backend version, pack locally,\nspin up Umbraco with the package installed",
                "[grey]--output -o, --models-mode -mm[/]");

            table.AddRow(
                "[green]dev[/] [grey][version][/]",
                "dotnet watch + vite watch against\na live Umbraco instance",
                "[grey]--output -o, --frontend-only -f[/]");

            table.AddRow(
                "[green]dist-usync[/]",
                "Copy the newest uSync folder\nto all other Umbraco instances",
                "[grey]--output -o[/]");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]version[/] accepts [grey]Latest[/], [grey]LTS[/], or a semver string such as [grey]17.3.5[/]");
            AnsiConsole.MarkupLine("Run [grey]bellaboot ai-help[/] for an AI-agent-oriented summary.");

            return 0;
        });

        return command;
    }
}
