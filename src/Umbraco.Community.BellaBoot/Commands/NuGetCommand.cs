using System.CommandLine;
using System.Xml;
using System.Xml.Linq;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class NuGetCommand
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

        var command = new Command("nuget", "Pack the backend as a local NuGet and spin up an Umbraco test instance that installs it");
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

            // Find backend csproj
            var backendCsproj = Directory.GetDirectories(targetDir, "*.Backend")
                .SelectMany(d => Directory.GetFiles(d, "*.csproj"))
                .FirstOrDefault();

            if (backendCsproj is null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No [grey]*.Backend/*.csproj[/] found. Run from your package root after scaffolding.");
                return 1;
            }

            // Increment patch version and get package metadata
            var (newVersion, packageId) = IncrementPatchVersion(backendCsproj);
            AnsiConsole.MarkupLine($"  Package     : [cyan]{packageId}[/] [grey]{newVersion}[/]");

            // Resolve Umbraco version
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
            var folderSuffix = isExplicitVersion ? $"{versionSelection}-nuget" : "_installing";

            AnsiConsole.MarkupLine($"  Version     : [cyan]{versionSelection}[/]");
            AnsiConsole.MarkupLine($"  Models mode : [cyan]{modelsMode}[/]");
            AnsiConsole.WriteLine();

            // Pack backend to /nuget
            var nugetDir = Path.Combine(targetDir, "nuget");
            Directory.CreateDirectory(nugetDir);

            var (packCode, packOutput) = await AnsiConsole.Status()
                .StartAsync($"Packing {packageId} {newVersion}...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await TargetCommand.RunAsync(
                        "dotnet", ["pack", backendCsproj, "-o", nugetDir, "-c", "Release"], targetDir, ct);
                });

            if (packCode != 0)
            {
                AnsiConsole.MarkupLine("[red]dotnet pack failed[/]");
                AnsiConsole.MarkupLine($"[grey]{packOutput}[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]Packed[/] → [grey]nuget/{packageId}.{newVersion}.nupkg[/]");
            AnsiConsole.WriteLine();

            // Detect / install Umbraco templates
            var (listCode, listOutput) = await TargetCommand.RunAsync(
                "dotnet", ["new", "list", "umbraco"], targetDir, ct);

            bool templatesInstalled = listCode == 0
                && !listOutput.Contains("No templates found", StringComparison.OrdinalIgnoreCase);

            if (!templatesInstalled)
            {
                AnsiConsole.MarkupLine("[yellow]Umbraco.Templates not found[/] — installing...");

                var (installCode, installOutput) = await AnsiConsole.Status()
                    .StartAsync("Installing Umbraco.Templates...", async ctx =>
                    {
                        ctx.Spinner(Spinner.Known.Dots);
                        return await TargetCommand.RunAsync(
                            "dotnet", ["new", "install", "Umbraco.Templates"], targetDir, ct);
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

            var umbracoProjectDir = Path.Combine(targetDir, "Umbraco", folderSuffix);

            var newArgs = new List<string>
            {
                "new", "umbraco",
                "-n", isExplicitVersion ? $"Umbraco-{versionSelection}-nuget" : "Umbraco",
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
                    return await TargetCommand.RunAsync("dotnet", [.. newArgs], targetDir, ct);
                });

            if (newCode != 0)
            {
                AnsiConsole.MarkupLine("[red]dotnet new umbraco failed[/]");
                AnsiConsole.MarkupLine($"[grey]{newOutput}[/]");
                return 1;
            }

            // Rename _installing to <version>-nuget for Latest/LTS
            string resolvedVersion = versionSelection;
            var finalProjectDir = umbracoProjectDir;
            if (!isExplicitVersion)
            {
                var tempCsproj = Directory.GetFiles(umbracoProjectDir, "*.csproj").FirstOrDefault();
                var detected = tempCsproj is not null ? ExtractUmbracoVersion(tempCsproj) : null;
                if (detected is not null)
                {
                    resolvedVersion = detected;
                    finalProjectDir = Path.Combine(targetDir, "Umbraco", $"{resolvedVersion}-nuget");
                    Directory.Move(umbracoProjectDir, finalProjectDir);
                    var movedCsproj = Path.Combine(finalProjectDir, Path.GetFileName(tempCsproj!));
                    File.Move(movedCsproj, Path.Combine(finalProjectDir, $"Umbraco-{resolvedVersion}-nuget.csproj"));
                }
            }

            // Write nuget.config so the project can restore from the local feed
            WriteNugetConfig(finalProjectDir, nugetDir);

            // Install uSync
            var (usyncCode, _) = await AnsiConsole.Status()
                .StartAsync("Installing uSync...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await TargetCommand.RunAsync(
                        "dotnet", ["add", "package", "uSync"], finalProjectDir, ct);
                });

            if (usyncCode != 0)
                AnsiConsole.MarkupLine("[yellow]Warning:[/] uSync install failed — add manually with [grey]dotnet add package uSync[/]");

            // Install local NuGet package
            var (pkgCode, pkgOutput) = await AnsiConsole.Status()
                .StartAsync($"Installing {packageId} {newVersion} from local feed...", async ctx =>
                {
                    ctx.Spinner(Spinner.Known.Dots);
                    return await TargetCommand.RunAsync(
                        "dotnet", ["add", "package", packageId, "--source", nugetDir, "--version", newVersion],
                        finalProjectDir, ct);
                });

            if (pkgCode != 0)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not install [cyan]{packageId}[/] — add manually.");
                AnsiConsole.MarkupLine($"[grey]{pkgOutput}[/]");
            }

            // Add to solution
            var slnxPath = Directory.GetFiles(targetDir, "*.slnx").FirstOrDefault();
            if (slnxPath is not null)
                TargetCommand.AddProjectToSolution(slnxPath, finalProjectDir, targetDir);

            AnsiConsole.WriteLine();
            var displayVersion = $"{resolvedVersion}-nuget";
            AnsiConsole.MarkupLine($"[bold green]Done![/] Umbraco [cyan]{displayVersion}[/] with [cyan]{packageId} {newVersion}[/] at [grey]Umbraco/{displayVersion}/[/]");
            AnsiConsole.MarkupLine($"[grey]cd Umbraco/{displayVersion} && dotnet run[/]");

            return 0;
        });

        return command;
    }

    private static (string newVersion, string packageId) IncrementPatchVersion(string csprojPath)
    {
        var doc = XDocument.Load(csprojPath);
        var versionEl = doc.Descendants("Version").FirstOrDefault();
        var packageIdEl = doc.Descendants("PackageId").FirstOrDefault();

        var current = versionEl?.Value ?? "0.0.0";
        var parts = current.Split('.');
        if (parts.Length >= 3 && int.TryParse(parts[^1], out var patch))
            parts[^1] = (patch + 1).ToString();
        var newVersion = string.Join('.', parts);

        if (versionEl is not null)
            versionEl.Value = newVersion;

        var xmlSettings = new XmlWriterSettings { Indent = true };
        using var writer = XmlWriter.Create(csprojPath, xmlSettings);
        doc.Save(writer);

        var packageId = packageIdEl?.Value ?? Path.GetFileNameWithoutExtension(csprojPath);
        return (newVersion, packageId);
    }

    private static void WriteNugetConfig(string projectDir, string nugetDir)
    {
        var relativePath = Path.GetRelativePath(projectDir, nugetDir).Replace('\\', '/');
        var content = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <add key="local" value="{relativePath}" />
              </packageSources>
            </configuration>
            """;
        File.WriteAllText(Path.Combine(projectDir, "nuget.config"), content, System.Text.Encoding.UTF8);
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
