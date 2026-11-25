# SourceWorker

Local-first codebase analysis and AI context handoff for .NET developers. Scan a project, understand its structure, inspect changes and generate context for coding assistants.

## Features

- Project scanning, technology detection and C# code intelligence
- Architecture summaries, context packs and agent handoffs
- Scan completeness, context snapshots and diffs
- Local SQLite storage, Markdown workspace and optional AI providers
- CLI, local HTTP API and Windows desktop UI

## Quick start

Requires the .NET 10 SDK, Git and PowerShell 7.

```powershell
pwsh -File tools/Restore-PublicDependencies.ps1
dotnet restore SourceWorker.Public.slnx --configfile NuGet.Public.config -p:SourceWorkerPrivateExtensions=false
dotnet build SourceWorker.Public.slnx -c Release --no-restore -p:SourceWorkerPrivateExtensions=false
dotnet run --project src/services/SourceWorker.Cli -c Release --no-build -p:SourceWorkerPrivateExtensions=false -- scan /path/to/project
dotnet run --project src/services/SourceWorker.Cli -c Release --no-build -p:SourceWorkerPrivateExtensions=false -- handoff /path/to/project
```

Generated context is stored in the selected project's `.ai-context/` directory. Review context before sharing it with an AI provider.

## Windows desktop

Install the .NET MAUI Windows workload and run:

```powershell
dotnet workload install maui-windows
dotnet restore ui/SourceWorker.Desktop --configfile NuGet.Public.config -r win-x64 -p:SourceWorkerPrivateExtensions=false
dotnet run --project ui/SourceWorker.Desktop -c Release --no-restore -r win-x64 -p:SourceWorkerPrivateExtensions=false
```

The public build includes a basic file workspace with explicit save. The optional advanced file editor and agent terminal require separate development libraries and are not included in the public build. The original development solution uses those local dependencies.

## Tests

```powershell
dotnet test SourceWorker.Public.slnx -c Release -p:SourceWorkerPrivateExtensions=false
```

SourceWorker runs locally. Keep the API on loopback with token authentication enabled. AI providers are optional; remote providers receive only the context submitted to them.

See [architecture and development notes](docs/Current.md).

## License

Licensed under the [MIT License](LICENSE).
