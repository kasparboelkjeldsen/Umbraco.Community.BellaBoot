using System.CommandLine;
using Spectre.Console;

namespace Umbraco.Community.BellaBoot.Commands;

public static class AiHelpCommand
{
    private const string Text = """
        BellaBoot — Umbraco Bellissima (v14+) package scaffolding CLI
        dotnet tool install -g Umbraco.Community.BellaBoot

        COMMANDS

        bellaboot new <name> [--output|-o <dir>] [--author <name>]
          Creates <name>.slnx, <name>.Backend/, <name>.Extension/ in the output directory.
          Backend: multi-targeting NuGet (net8/9/10), MSBuild targets that copy ui/ to Umbraco
            wwwroot/App_Plugins/<namelower>.frontend on build, JSON schema, ServiceComposer stub.
            Building Backend also triggers npm run build in Extension (AfterTargets).
          Extension: Vite + TypeScript, esproj for VS, outputs to Backend/ui/ by default.
            Set OUT_DIR env var to redirect output to a live Umbraco wwwroot (used by dev command).

        bellaboot target [version] [--output|-o <dir>] [--models-mode|-mm <mode>]
          Installs Umbraco (Latest/LTS/semver) to Umbraco/<version>/ with uSync, adds a
          ProjectReference to the backend, and adds the project to the solution in /Umbraco/.
          Umbraco project is named Umbraco-<version>.

        bellaboot nuget [version] [--output|-o <dir>] [--models-mode|-mm <mode>]
          Increments backend patch version, runs dotnet pack to nuget/, installs Umbraco to
          Umbraco/<version>-nuget/ with uSync, installs the packed package from the local feed,
          writes a nuget.config pointing at nuget/. Project named Umbraco-<version>-nuget.

        bellaboot dev [version] [--output|-o <dir>] [--frontend-only|-f]
          Picks a target from Umbraco/ (auto if one, prompts if many, or use version arg).
          Starts dotnet watch run in the Umbraco project dir and npm run dev in Extension with
          OUT_DIR=Umbraco/<version>/wwwroot/App_Plugins/<namelower>.frontend, streaming both
          to stdout with [dotnet]/[vite] prefixes. --frontend-only skips dotnet watch.

        bellaboot dist-usync [--output|-o <dir>]
          Scans Umbraco/*/ for uSync/ folders, finds the one with the newest file, copies it
          to all other Umbraco instances (creates uSync/ if missing).

        bellaboot delete <version> [--output|-o <dir>]   (alias: del)
          Removes Umbraco/<version>/ and Umbraco/<version>-nuget/ from disk and removes
          their project entries from the .slnx solution file (drops the /Umbraco/ folder
          element if it becomes empty).

        STRUCTURE (after new + target)
          <name>.slnx
          <name>.Backend/
            <name>.Backend.csproj       multi-targeting NuGet package
            buildTransitive/            <name>.props  <name>.targets
            ui/                         compiled frontend output (gitignored in Umbraco)
            Composers/ServiceComposer.cs
          <name>.Extension/
            <name>.Extension.esproj
            src/index.ts
            public/umbraco-package.json
            vite.config.ts              reads OUT_DIR env var
          Umbraco/
            <version>/                  Umbraco project with uSync + ProjectReference to Backend
            <version>-nuget/            Umbraco project with uSync + local NuGet package
        """;

    public static Command Build()
    {
        var command = new Command("ai-help", "Print a compact description of all commands for AI-agent consumption");
        command.SetAction((_) =>
        {
            AnsiConsole.WriteLine(Text);
            return 0;
        });

        return command;
    }
}
