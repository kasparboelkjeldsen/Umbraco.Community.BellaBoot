using System.CommandLine;
using System.Diagnostics;
using System.Xml.Linq;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class DevCommand
{
    public static Command Build()
    {
        var versionArg = new Argument<string?>("version")
        {
            Description = "Umbraco target folder to use (e.g. 17.3.5). Defaults to latest or prompts if multiple exist.",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var frontendOnlyOpt = new Option<bool>("--frontend-only", "-f")
        {
            Description = "Only run vite watch — skip dotnet watch (useful when running Umbraco in the VS debugger)",
        };
        var outputOpt = new Option<string?>("--output", "-o")
        {
            Description = "Package root directory (defaults to current directory)",
        };

        var command = new Command("dev", "Start dotnet watch + vite watch pointed at a live Umbraco instance");
        command.Arguments.Add(versionArg);
        command.Options.Add(frontendOnlyOpt);
        command.Options.Add(outputOpt);

        command.SetAction(async (parseResult, ct) =>
        {
            var versionInput = parseResult.GetValue(versionArg);
            var frontendOnly = parseResult.GetValue(frontendOnlyOpt);
            var outputPath = parseResult.GetValue(outputOpt);

            AnsiConsole.Write(new FigletText("BellaBoot").Color(Color.HotPink));

            var targetDir = string.IsNullOrEmpty(outputPath)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(outputPath);

            // Locate extension dir
            var extensionDir = Directory.GetDirectories(targetDir, "*.Extension").FirstOrDefault();
            if (extensionDir is null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No [grey]*.Extension[/] folder found. Run from your package root.");
                return 1;
            }

            // Derive namelower from backend csproj PackageId
            var backendCsproj = Directory.GetDirectories(targetDir, "*.Backend")
                .SelectMany(d => Directory.GetFiles(d, "*.csproj"))
                .FirstOrDefault();

            var namelower = backendCsproj is not null
                ? ReadPackageId(backendCsproj).ToLowerInvariant()
                : Path.GetFileName(extensionDir).Replace(".Extension", "").ToLowerInvariant();

            // Find available Umbraco instances
            var umbracoRoot = Path.Combine(targetDir, "Umbraco");
            if (!Directory.Exists(umbracoRoot))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No [grey]Umbraco/[/] folder found. Run [grey]bellaboot target[/] first.");
                return 1;
            }

            var instances = Directory.GetDirectories(umbracoRoot)
                .Select(d => Path.GetFileName(d)!)
                .OrderByDescending(n => n)
                .ToList();

            if (instances.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No Umbraco instances found.[/] Run [grey]bellaboot target[/] first.");
                return 0;
            }

            // Resolve which instance to use
            string selectedVersion;
            if (!string.IsNullOrEmpty(versionInput))
            {
                if (!instances.Contains(versionInput))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] [grey]Umbraco/{versionInput}[/] does not exist.");
                    AnsiConsole.MarkupLine($"Available: {string.Join(", ", instances.Select(i => $"[grey]{i}[/]"))}");
                    return 1;
                }
                selectedVersion = versionInput;
            }
            else if (instances.Count == 1)
            {
                selectedVersion = instances[0];
                AnsiConsole.MarkupLine($"Auto-selected [cyan]{selectedVersion}[/]");
            }
            else
            {
                selectedVersion = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Which [bold]Umbraco instance[/] do you want to target?")
                        .AddChoices(instances));
            }

            var umbracoDir = Path.Combine(umbracoRoot, selectedVersion);
            var wwwrootPath = Path.Combine(umbracoDir, "wwwroot", "App_Plugins", $"{namelower}.frontend");

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  Umbraco  : [cyan]{selectedVersion}[/]");
            AnsiConsole.MarkupLine($"  Frontend : [cyan]{namelower}.frontend[/]");
            AnsiConsole.MarkupLine($"  Mode     : [cyan]{(frontendOnly ? "frontend only" : "dotnet watch + vite")}[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press Ctrl+C to stop.[/]");
            AnsiConsole.WriteLine();

            // Ensure npm dependencies are installed
            if (!Directory.Exists(Path.Combine(extensionDir, "node_modules")))
            {
                AnsiConsole.MarkupLine("[yellow]node_modules not found[/] — running npm install...");
                var (installFile, installArgs) = NpmCommand(["install"]);
                var (installCode, installOutput) = await TargetCommand.RunAsync(installFile, installArgs, extensionDir, ct);
                if (installCode != 0)
                {
                    AnsiConsole.MarkupLine("[red]npm install failed[/]");
                    AnsiConsole.MarkupLine($"[grey]{installOutput}[/]");
                    return 1;
                }
                AnsiConsole.MarkupLine("[green]npm install done[/]");
                AnsiConsole.WriteLine();
            }

            var processes = new List<Process>();

            if (!frontendOnly)
            {
                var dotnetProc = StartStreaming(
                    "dotnet", ["watch", "run"], umbracoDir,
                    "dotnet", "blue", env: null);
                processes.Add(dotnetProc);
            }

            var (npmFile, npmArgs) = NpmCommand(["run", "dev"]);
            var viteProc = StartStreaming(
                npmFile, npmArgs, extensionDir,
                "vite", "cyan",
                env: new Dictionary<string, string> { ["OUT_DIR"] = wwwrootPath });
            processes.Add(viteProc);

            ct.Register(() =>
            {
                foreach (var p in processes.Where(p => !p.HasExited))
                    p.Kill(entireProcessTree: true);
            });

            await Task.WhenAll(processes.Select(p => p.WaitForExitAsync(CancellationToken.None)));
            return 0;
        });

        return command;
    }

    private static Process StartStreaming(
        string fileName, string[] args, string workingDir,
        string prefix, string color, Dictionary<string, string>? env)
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
        if (env is not null)
            foreach (var (k, v) in env)
                psi.Environment[k] = v;

        var process = Process.Start(psi)!;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                AnsiConsole.MarkupLine($"[{color}][[{prefix}]][/] {Markup.Escape(e.Data)}");
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                AnsiConsole.MarkupLine($"[{color}][[{prefix}]][/] [red]{Markup.Escape(e.Data)}[/]");
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static (string fileName, string[] args) NpmCommand(string[] npmArgs) =>
        OperatingSystem.IsWindows()
            ? ("cmd.exe", ["/c", "npm", .. npmArgs])
            : ("npm", npmArgs);

    private static string ReadPackageId(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            return doc.Descendants("PackageId").FirstOrDefault()?.Value
                ?? Path.GetFileNameWithoutExtension(csprojPath);
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(csprojPath);
        }
    }
}
