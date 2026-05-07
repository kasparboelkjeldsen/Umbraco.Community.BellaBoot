# BellaBoot — Claude Instructions

## Memory

Always maintain the memory folder at `C:\Users\kaskje\.claude\projects\x--kasparboelkjeldsen-Umbraco-Community-BellaBoot\memory\`.
Use it to persist project context, decisions, and patterns across sessions.
Read relevant memory files at the start of each session before doing any work.
Write to memory whenever you learn something non-obvious about the project, the user's preferences, or design decisions made.

## Project Overview

BellaBoot is a .NET global CLI tool (`dotnet tool install -g Umbraco.Community.BellaBoot`) that replicates the functionality of the [Bellissima Bootstrapper](x:\kasparboelkjeldsen\Umbraco.Community.Bellissima.Bootstrapper) — a project scaffolding toolkit for building Umbraco Bellissima (v14+) packages.

The Bellissima Bootstrapper reference project lives at `x:\kasparboelkjeldsen\Umbraco.Community.Bellissima.Bootstrapper` — always keep that in context when building new features.

## Tech Stack

- .NET console app packaged as a global dotnet tool
- [System.CommandLine](https://github.com/dotnet/command-line-api) — command/argument/option parsing
- [Spectre.Console](https://spectreconsole.net/) — rich terminal UI (prompts, tables, progress, markup)

## Project Structure

```
Umbraco.Community.BellaBoot.sln
src/
  Umbraco.Community.BellaBoot/   # CLI tool project
```

## Coding Conventions

- No unnecessary comments — code should be self-documenting
- No premature abstractions — build for what is needed now
- Prefer Spectre.Console for all user-facing output; never use raw `Console.Write`
- Commands go in `src/Umbraco.Community.BellaBoot/Commands/`
- Keep `Program.cs` minimal — just wiring up the command tree

## End Goal

`dotnet tool install -g Umbraco.Community.BellaBoot`
`bellaboot new MyPackage`
→ scaffolds a complete Umbraco Bellissima package project
