# Umbraco.Community.BellaBoot

CLI tool for scaffolding and developing Umbraco Bellissima (v14+) packages.

```
dotnet tool install -g Umbraco.Community.BellaBoot
```

To update to the latest version:

```
dotnet tool update -g Umbraco.Community.BellaBoot
```

## Commands

### `bellaboot new <name>`
Scaffolds a new package project in the current directory.
Creates `<name>.Backend/` (multi-targeting NuGet package), `<name>.Extension/` (Vite + TypeScript), and `<name>.slnx`.

### `bellaboot target [version]`
Spins up an Umbraco test instance at `Umbraco/<version>/` with uSync installed and a project reference to the backend. Installs Umbraco templates if not present. Accepts `Latest`, `LTS`, or an explicit version (e.g. `17.3.5`).

### `bellaboot nuget [version]`
Increments the backend patch version, packs it to `nuget/`, spins up a fresh Umbraco instance at `Umbraco/<version>-nuget/`, and installs the package from the local feed.

### `bellaboot dev [version]`
Starts `dotnet watch run` on the selected Umbraco instance and `vite build --watch` on the extension project, with the vite output pointed directly at that Umbraco's `wwwroot/App_Plugins/`. Pass `--frontend-only` to skip dotnet watch.

### `bellaboot dist-usync`
Finds the Umbraco instance with the most recently modified uSync folder and copies it to all other instances.
