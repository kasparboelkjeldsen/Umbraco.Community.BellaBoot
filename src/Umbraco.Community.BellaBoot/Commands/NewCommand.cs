using System.CommandLine;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class NewCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name")
        {
            Description = "Package name (e.g. MyPackage or Umbraco.Community.MyPackage)",
        };
        var outputOpt = new Option<string?>("--output", "-o")
        {
            Description = "Target directory (defaults to current directory)",
        };
        var authorOpt = new Option<string?>("--author")
        {
            Description = "Package author name",
        };

        var command = new Command("new", "Scaffold a new Umbraco Bellissima package project");
        command.Arguments.Add(nameArg);
        command.Options.Add(outputOpt);
        command.Options.Add(authorOpt);

        command.SetAction((parseResult) =>
        {
            var name = parseResult.GetValue(nameArg)!;
            var outputPath = parseResult.GetValue(outputOpt);
            var author = parseResult.GetValue(authorOpt) ?? string.Empty;

            AnsiConsole.Write(new FigletText("BellaBoot").Color(Color.HotPink));
            AnsiConsole.MarkupLine($"Scaffolding [bold green]{name}[/]");
            AnsiConsole.WriteLine();

            var targetDir = string.IsNullOrEmpty(outputPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(outputPath);

            var backendDir = Path.Combine(targetDir, $"{name}.Backend");
            var extensionDir = Path.Combine(targetDir, $"{name}.Extension");

            if (Directory.Exists(backendDir))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] [grey]{backendDir}[/] already exists.");
                return 1;
            }

            ScaffoldBackend(name, author, backendDir);
            ScaffoldExtension(name, extensionDir);

            WriteFile(targetDir, $"{name}.slnx",
                T("Solution.slnx").With("{name}", name));

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.HotPink)
                .AddColumn("[bold]Created[/]")
                .AddColumn("[bold]Notes[/]");

            table.AddRow($"[green]{name}.slnx[/]", "Solution file");
            table.AddRow($"[green]{name}.Backend/[/]", "Multi-targeting NuGet package, schema wired, ui/ as pack output");
            table.AddRow($"[green]{name}.Extension/[/]", "Vite + TypeScript, outputs to Backend/ui/ on build");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[grey]cd {name}.Extension && npm install && npm run build[/]");
            AnsiConsole.MarkupLine("Run [grey]bellaboot target[/] to spin up an Umbraco test instance.");

            return 0;
        });

        return command;
    }

    // -------------------------------------------------------------------------
    // Backend
    // -------------------------------------------------------------------------

    private static void ScaffoldBackend(string name, string author, string backendDir)
    {
        var msBuildName = MsBuildSafe(name);

        Directory.CreateDirectory(backendDir);
        Directory.CreateDirectory(Path.Combine(backendDir, "buildTransitive"));
        Directory.CreateDirectory(Path.Combine(backendDir, "ui"));
        Directory.CreateDirectory(Path.Combine(backendDir, "Composers"));
        Directory.CreateDirectory(Path.Combine(backendDir, "Constants"));
        Directory.CreateDirectory(Path.Combine(backendDir, "Models"));
        Directory.CreateDirectory(Path.Combine(backendDir, "Helpers"));

        WriteFile(backendDir, $"{name}.Backend.csproj",
            T("Backend.Csproj.xml").With("{name}", name).With("{author}", author));

        WriteFile(backendDir, $"appsettings-schema.{name}.json",
            T("Backend.Schema.json").With("{name}", name));

        WriteFile(backendDir, "README.md", $"# {name}\n");

        WriteFile(Path.Combine(backendDir, "buildTransitive"), $"{name}.props",
            T("Backend.Props.xml").With("{name}", name));

        WriteFile(Path.Combine(backendDir, "buildTransitive"), $"{name}.targets",
            T("Backend.Targets.xml").With("{mb}", msBuildName).With("{name}", name).With("{namelower}", name.ToLowerInvariant()));

        WriteFile(Path.Combine(backendDir, "ui"), "umbraco-package.json",
            T("Backend.UmbracoPackage.json").With("{name}", name).With("{namelower}", name.ToLowerInvariant()));

        WriteFile(Path.Combine(backendDir, "Composers"), "ServiceComposer.cs",
            T("Backend.ServiceComposer.cs").With("{ns}", NamespaceSafe(name)));
    }

    // -------------------------------------------------------------------------
    // Extension
    // -------------------------------------------------------------------------

    private static void ScaffoldExtension(string name, string extensionDir)
    {
        Directory.CreateDirectory(extensionDir);
        Directory.CreateDirectory(Path.Combine(extensionDir, "src"));
        Directory.CreateDirectory(Path.Combine(extensionDir, "public"));

        WriteFile(extensionDir, "package.json",
            T("Extension.PackageJson.json").With("{namelower}", name.ToLowerInvariant()));

        WriteFile(extensionDir, "vite.config.ts",
            T("Extension.ViteConfig.ts").With("{name}", name));

        WriteFile(extensionDir, "tsconfig.json",
            T("Extension.TsConfig.json"));

        WriteFile(extensionDir, $"{name}.Extension.esproj",
            T("Extension.Esproj.xml"));

        WriteFile(extensionDir, ".gitignore",
            T("Extension.Gitignore.txt"));

        WriteFile(Path.Combine(extensionDir, "src"), "index.ts",
            T("Extension.Index.ts"));

        WriteFile(Path.Combine(extensionDir, "public"), "umbraco-package.json",
            T("Backend.UmbracoPackage.json").With("{name}", name).With("{namelower}", name.ToLowerInvariant()));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void WriteFile(string dir, string filename, string content) =>
        File.WriteAllText(Path.Combine(dir, filename), content, System.Text.Encoding.UTF8);

    // Dots and special chars are not valid in MSBuild property/item/target names
    private static string MsBuildSafe(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit));

    // Dots are valid namespace separators; everything else that isn't a letter/digit becomes _
    private static string NamespaceSafe(string name) =>
        string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '.' ? c : '_'));

    // Fluent shorthand: T("Backend.Csproj.xml").With("{name}", value)
    private static string T(string templateName) =>
        TemplateLoader.Load(templateName);
}

file static class StringExtensions
{
    public static string With(this string s, string token, string value) =>
        s.Replace(token, value);
}
