using System.CommandLine;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class DistUsyncCommand
{
    public static Command Build()
    {
        var outputOpt = new Option<string?>("--output", "-o")
        {
            Description = "Package root directory (defaults to current directory)",
        };

        var command = new Command("dist-usync", "Distribute the most recently updated uSync folder across all Umbraco targets");
        command.Options.Add(outputOpt);

        command.SetAction((parseResult) =>
        {
            var outputPath = parseResult.GetValue(outputOpt);
            var targetDir = string.IsNullOrEmpty(outputPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(outputPath);

            AnsiConsole.Write(new FigletText("BellaBoot").Color(Color.HotPink));

            var umbracoRoot = Path.Combine(targetDir, "Umbraco");
            if (!Directory.Exists(umbracoRoot))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No [grey]Umbraco/[/] folder found. Run from your package root.");
                return 1;
            }

            var allTargets = Directory.GetDirectories(umbracoRoot)
                .OrderBy(d => d)
                .ToList();

            if (allTargets.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No Umbraco targets found in Umbraco/.[/]");
                return 0;
            }

            // Find the uSync dir with the newest files — that's our source
            var withUsync = allTargets
                .Select(d => (dir: d, usync: Path.Combine(d, "uSync")))
                .Where(x => Directory.Exists(x.usync))
                .Select(x => (x.dir, x.usync, newest: NewestFile(x.usync)))
                .OrderByDescending(x => x.newest)
                .ToList();

            if (withUsync.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No uSync folders found.[/] Start an Umbraco instance and make some changes first.");
                return 0;
            }

            var (sourceDir, sourceUsync, sourceNewest) = withUsync[0];
            var sourceName = Path.GetFileName(sourceDir);

            AnsiConsole.MarkupLine($"Source : [cyan]{sourceName}[/]  [grey](newest file: {sourceNewest:yyyy-MM-dd HH:mm:ss})[/]");
            AnsiConsole.WriteLine();

            var destinations = allTargets
                .Where(d => d != sourceDir)
                .ToList();

            if (destinations.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Only one Umbraco target — nothing to distribute.[/]");
                return 0;
            }

            foreach (var dest in destinations)
            {
                var name = Path.GetFileName(dest);
                var destUsync = Path.Combine(dest, "uSync");
                CopyDirectory(sourceUsync, destUsync);
                AnsiConsole.MarkupLine($"  [green]>[/] {name}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]Done![/] Distributed from [cyan]{sourceName}[/] to {destinations.Count} target(s).");
            return 0;
        });

        return command;
    }

    private static DateTime NewestFile(string dir) =>
        Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(File.GetLastWriteTime)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

    private static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destFile = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }
}
