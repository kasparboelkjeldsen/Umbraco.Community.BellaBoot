using System.CommandLine;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class TargetCommand
{
    public static Command Build()
    {
        var versionArg = new Argument<string?>("version")
        {
            Description = "Umbraco version to target: Latest, LTS, or a semver string (e.g. 17.3.4)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var outputOpt = new Option<string?>("--output", "-o")
        {
            Description = "Package root directory (defaults to current directory)",
        };
        var modelsModeOpt = new Option<string>("--models-mode", "-mm")
        {
            Description = "Umbraco models generation mode",
            DefaultValueFactory = _ => "SourceCodeAuto",
        };
        var emailOpt = new Option<string>("--email")
        {
            Description = "Default admin email",
            DefaultValueFactory = _ => "admin@example.com",
        };
        var passwordOpt = new Option<string>("--password")
        {
            Description = "Default admin password",
            DefaultValueFactory = _ => "1234567890",
        };
        var friendlyNameOpt = new Option<string>("--friendly-name")
        {
            Description = "Default admin display name",
            DefaultValueFactory = _ => "Administrator",
        };

        var command = new Command("target", "Spin up an Umbraco test instance for your package");
        command.Arguments.Add(versionArg);
        command.Options.Add(outputOpt);
        command.Options.Add(modelsModeOpt);
        command.Options.Add(emailOpt);
        command.Options.Add(passwordOpt);
        command.Options.Add(friendlyNameOpt);

        command.SetAction(async (parseResult, ct) =>
        {
            var versionInput = parseResult.GetValue(versionArg);
            var outputPath = parseResult.GetValue(outputOpt);
            var modelsMode = parseResult.GetValue(modelsModeOpt)!;
            var email = parseResult.GetValue(emailOpt)!;
            var password = parseResult.GetValue(passwordOpt)!;
            var friendlyName = parseResult.GetValue(friendlyNameOpt)!;

            AnsiConsole.Write(new FigletText("BellaBoot").Color(Color.HotPink));

            var targetDir = string.IsNullOrEmpty(outputPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(outputPath);

            // Warn if this doesn't look like a package project root
            var hasBackend = Directory.GetDirectories(targetDir, "*.Backend").Length > 0;
            var hasExtension = Directory.GetDirectories(targetDir, "*.Extension").Length > 0;
            if (!hasBackend || !hasExtension)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] No [grey]*.Backend[/] or [grey]*.Extension[/] folders found here.");
                AnsiConsole.MarkupLine("[grey]Run this command from your package project root after scaffolding.[/]");
                AnsiConsole.WriteLine();
            }

            // Resolve version
            string versionSelection;
            if (!string.IsNullOrEmpty(versionInput))
            {
                versionSelection = versionInput;
            }
            else
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Which [bold]Umbraco version[/] do you want to target?")
                        .AddChoices("Latest", "LTS", "Enter a version number"));

                versionSelection = choice == "Enter a version number"
                    ? AnsiConsole.Ask<string>("Version number [grey](e.g. 17.3.4)[/]:")
                    : choice;
            }

            bool isExplicitVersion = versionSelection is not ("Latest" or "LTS");

            // For Latest, try to resolve the exact version number upfront from the releases page
            string? knownVersion = isExplicitVersion ? versionSelection : null;
            if (versionSelection == "Latest")
            {
                var fetched = await AnsiConsole.Status()
                    .StartAsync("Checking latest release...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        return await FetchLatestVersionAsync(ct);
                    });
                if (fetched is not null)
                    knownVersion = fetched;
            }

            // Check if target already exists when version is known
            if (knownVersion is not null)
            {
                var existingDir = Path.Combine(targetDir, "Umbraco", knownVersion);
                if (Directory.Exists(existingDir))
                {
                    AnsiConsole.MarkupLine($"[yellow]Umbraco/{knownVersion}/ already exists.[/] Nothing to do.");
                    return 0;
                }
            }

            AnsiConsole.MarkupLine($"  Version     : [cyan]{knownVersion ?? versionSelection}[/]");
            AnsiConsole.MarkupLine($"  Models mode : [cyan]{modelsMode}[/]");
            AnsiConsole.WriteLine();

            // Check whether Umbraco templates are installed
            var (listCode, listOutput) = await RunAsync("dotnet", ["new", "list", "umbraco"], targetDir, ct);
            bool templatesInstalled = listCode == 0
                && !listOutput.Contains("No templates found", StringComparison.OrdinalIgnoreCase);

            if (!templatesInstalled)
            {
                AnsiConsole.MarkupLine("[yellow]Umbraco.Templates not found[/] — installing...");

                var (installCode, installOutput) = await AnsiConsole.Status()
                    .StartAsync("Installing Umbraco.Templates...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        return await RunAsync("dotnet", ["new", "install", "Umbraco.Templates"], targetDir, ct);
                    });

                if (installCode != 0)
                {
                    AnsiConsole.MarkupLine("[red]Failed to install Umbraco.Templates[/]");
                    AnsiConsole.MarkupLine($"[grey]{installOutput}[/]");
                    return 1;
                }

                AnsiConsole.MarkupLine("[green]Umbraco.Templates installed[/]");
                AnsiConsole.WriteLine();
            }

            Directory.CreateDirectory(Path.Combine(targetDir, "Umbraco"));

            var umbracoProjectDir = Path.Combine(
                targetDir, "Umbraco", knownVersion ?? "_installing");

            var newArgs = new List<string>
            {
                "new", "umbraco",
                "-n", knownVersion is not null ? $"Umbraco-{knownVersion}" : "Umbraco",
                "-o", umbracoProjectDir,
                "--models-mode", modelsMode,
                "--email", email,
                "--password", password,
                "--friendly-name", friendlyName,
            };

            if (versionSelection == "LTS") newArgs.AddRange(["-r", "LTS"]);
            else if (versionSelection == "Latest") newArgs.AddRange(["-r", "Latest"]);

            var (newCode, newOutput) = await AnsiConsole.Status()
                .StartAsync($"Installing Umbraco {versionSelection}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await RunAsync("dotnet", [.. newArgs], targetDir, ct);
                });

            if (newCode != 0)
            {
                AnsiConsole.MarkupLine("[red]dotnet new umbraco failed[/]");
                AnsiConsole.MarkupLine($"[grey]{newOutput}[/]");
                return 1;
            }

            // Detect actual version from generated project and rename folder (only needed as fallback
            // when we couldn't resolve the version upfront, e.g. LTS or failed fetch)
            string resolvedVersion = knownVersion ?? versionSelection;
            var finalProjectDir = umbracoProjectDir;
            if (knownVersion is null && !isExplicitVersion)
            {
                var tempCsproj = Directory.GetFiles(umbracoProjectDir, "*.csproj").FirstOrDefault();
                var detected = tempCsproj is not null ? ExtractUmbracoVersion(tempCsproj) : null;
                if (detected is not null)
                {
                    resolvedVersion = detected;
                    finalProjectDir = Path.Combine(targetDir, "Umbraco", resolvedVersion);
                    Directory.Move(umbracoProjectDir, finalProjectDir);
                    var movedCsproj = Path.Combine(finalProjectDir, Path.GetFileName(tempCsproj!));
                    File.Move(movedCsproj, Path.Combine(finalProjectDir, $"Umbraco-{resolvedVersion}.csproj"));
                }
            }

            var (usyncCode, usyncOutput) = await AnsiConsole.Status()
                .StartAsync("Installing uSync...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await RunAsync("dotnet", ["add", "package", "uSync"], finalProjectDir, ct);
                });

            if (usyncCode != 0)
            {
                AnsiConsole.MarkupLine("[yellow]Warning:[/] uSync install failed — add manually with [grey]dotnet add package uSync[/]");
                AnsiConsole.MarkupLine($"[grey]{usyncOutput}[/]");
            }

            // Add Umbraco project to solution file
            var slnxPath = Directory.GetFiles(targetDir, "*.slnx").FirstOrDefault();
            if (slnxPath is not null)
            {
                AddProjectToSolution(slnxPath, finalProjectDir, targetDir);
            }

            // Add project reference from Umbraco to Backend
            var backendCsproj = Directory.GetDirectories(targetDir, "*.Backend")
                .SelectMany(d => Directory.GetFiles(d, "*.csproj"))
                .FirstOrDefault();

            if (backendCsproj is not null)
            {
                var (refCode, refOutput) = await AnsiConsole.Status()
                    .StartAsync("Adding backend reference...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        return await RunAsync("dotnet", ["add", "reference", backendCsproj], finalProjectDir, ct);
                    });

                if (refCode != 0)
                {
                    AnsiConsole.MarkupLine("[yellow]Warning:[/] Could not add backend reference — add manually.");
                    AnsiConsole.MarkupLine($"[grey]{refOutput}[/]");
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold green]Done![/] Umbraco [cyan]{resolvedVersion}[/] + uSync ready at [grey]Umbraco/{resolvedVersion}/[/]");
            AnsiConsole.MarkupLine($"[grey]cd Umbraco/{resolvedVersion} && dotnet run[/]");

            return 0;
        });

        return command;
    }

    internal static void AddProjectToSolution(string slnxPath, string projectDir, string solutionDir)
    {
        var doc = XDocument.Load(slnxPath);
        var root = doc.Root!;

        var folder = root.Elements("Folder")
            .FirstOrDefault(f => string.Equals(f.Attribute("Name")?.Value, "/Umbraco/", StringComparison.OrdinalIgnoreCase));

        if (folder is null)
        {
            folder = new XElement("Folder", new XAttribute("Name", "/Umbraco/"));
            root.Add(folder);
        }

        var csprojFile = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault()
            ?? Path.Combine(projectDir, "Umbraco.csproj");
        var relativePath = Path.GetRelativePath(solutionDir, csprojFile).Replace('\\', '/');

        folder.Add(new XElement("Project", new XAttribute("Path", relativePath)));

        var xmlSettings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true };
        using var writer = XmlWriter.Create(slnxPath, xmlSettings);
        doc.Save(writer);
    }

    internal static async Task<(int exitCode, string output)> RunAsync(
        string fileName, string[] args, string workingDir, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        return (process.ExitCode, stdout + stderr);
    }

    private static async Task<string?> FetchLatestVersionAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BellaBoot/1.0");
            var html = await http.GetStringAsync("https://releases.umbraco.com/all-releases", ct);
            var match = Regex.Match(html, @"class=""release-version""[^>]*>\s*v?(\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractUmbracoVersion(string csprojPath)
    {
        try
        {
            var csproj = XDocument.Load(csprojPath);
            var inlineVersion = csproj.Descendants("PackageReference")
                .FirstOrDefault(e => string.Equals(
                    e.Attribute("Include")?.Value, "Umbraco.Cms",
                    StringComparison.OrdinalIgnoreCase))
                ?.Attribute("Version")?.Value;

            if (inlineVersion is not null)
                return inlineVersion;

            // CPM projects keep versions in Directory.Packages.props
            var propsPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "Directory.Packages.props");
            if (!File.Exists(propsPath))
                return null;

            var props = XDocument.Load(propsPath);
            return props.Descendants("PackageVersion")
                .FirstOrDefault(e => string.Equals(
                    e.Attribute("Include")?.Value, "Umbraco.Cms",
                    StringComparison.OrdinalIgnoreCase))
                ?.Attribute("Version")?.Value;
        }
        catch
        {
            return null;
        }
    }
}
