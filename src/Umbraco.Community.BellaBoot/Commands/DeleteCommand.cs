using System.CommandLine;
using System.Xml;
using System.Xml.Linq;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class DeleteCommand
{
    public static Command Build()
    {
        var versionArg = new Argument<string>("version")
        {
            Description = "Umbraco target version to remove (e.g. 17.3.5)",
        };
        var outputOpt = new Option<string?>("--output", "-o")
        {
            Description = "Package root directory (defaults to current directory)",
        };

        var command = new Command("delete", "Remove a target Umbraco instance and its nuget variant");
        command.Aliases.Add("del");
        command.Arguments.Add(versionArg);
        command.Options.Add(outputOpt);

        command.SetAction((parseResult) =>
        {
            var version = parseResult.GetValue(versionArg)!;
            var outputPath = parseResult.GetValue(outputOpt);

            AnsiConsole.Write(new FigletText("BellaBoot").Color(Color.HotPink));

            var targetDir = string.IsNullOrEmpty(outputPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(outputPath);

            var umbracoRoot = Path.Combine(targetDir, "Umbraco");
            var candidates = new[]
            {
                Path.Combine(umbracoRoot, version),
                Path.Combine(umbracoRoot, $"{version}-nuget"),
            };

            var toDelete = candidates.Where(Directory.Exists).ToList();

            if (toDelete.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Nothing to delete:[/] no [grey]Umbraco/{version}[/] or [grey]Umbraco/{version}-nuget[/] found.");
                return 0;
            }

            foreach (var dir in toDelete)
            {
                var name = Path.GetFileName(dir);
                AnsiConsole.MarkupLine($"Deleting [grey]Umbraco/{name}/[/]...");
                Directory.Delete(dir, recursive: true);
                AnsiConsole.MarkupLine($"  [green]✓[/] Deleted");
            }

            // Remove from solution file
            var slnxPath = Directory.GetFiles(targetDir, "*.slnx").FirstOrDefault();
            if (slnxPath is not null)
            {
                var removed = RemoveFromSolution(slnxPath, version);
                if (removed > 0)
                    AnsiConsole.MarkupLine($"  [green]✓[/] Removed {removed} entr{(removed == 1 ? "y" : "ies")} from [grey]{Path.GetFileName(slnxPath)}[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]Done![/] Removed Umbraco [cyan]{version}[/] target(s).");
            return 0;
        });

        return command;
    }

    private static int RemoveFromSolution(string slnxPath, string version)
    {
        var doc = XDocument.Load(slnxPath);
        var root = doc.Root!;

        var folder = root.Elements("Folder")
            .FirstOrDefault(f => string.Equals(f.Attribute("Name")?.Value, "/Umbraco/", StringComparison.OrdinalIgnoreCase));

        if (folder is null) return 0;

        var toRemove = folder.Elements("Project")
            .Where(p =>
            {
                var path = p.Attribute("Path")?.Value ?? "";
                return path.StartsWith($"Umbraco/{version}/", StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith($"Umbraco/{version}-nuget/", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        foreach (var el in toRemove)
            el.Remove();

        // Drop the folder element entirely if it's now empty
        if (!folder.Elements("Project").Any())
            folder.Remove();

        var xmlSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true };
        using var writer = XmlWriter.Create(slnxPath, xmlSettings);
        doc.Save(writer);

        return toRemove.Count;
    }
}
