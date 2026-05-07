using System.CommandLine;
using Spectre.Console;
using Umbraco.Community.BellaBoot.Commands;

var root = new RootCommand("BellaBoot — scaffold Umbraco Bellissima packages from the terminal");

root.Subcommands.Add(NewCommand.Build());
root.Subcommands.Add(TargetCommand.Build());
root.Subcommands.Add(DistUsyncCommand.Build());
root.Subcommands.Add(NuGetCommand.Build());
root.Subcommands.Add(DevCommand.Build());
root.Subcommands.Add(HelpCommand.Build());
root.Subcommands.Add(AiHelpCommand.Build());

root.SetAction((_) =>
{
    AnsiConsole.Write(new FigletText("BellaBoot").Color(Color.HotPink));
    AnsiConsole.MarkupLine("Run [grey]bellaboot help[/] for command reference.");
    AnsiConsole.MarkupLine("Run [grey]bellaboot ai-help[/] for an AI-agent-oriented summary.");
    return 0;
});

return root.Parse(args).Invoke();
