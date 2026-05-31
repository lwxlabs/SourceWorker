# SourceWorker

SourceWorker is a local-first AI context management foundation for codebases. This first skeleton builds the engine shape only: domain models, application request flow, local storage, scanning, context markdown output, a CLI, a local API, and an in-process worker queue.

No AI provider, desktop UI, MCP server, cloud sync, licensing, or secret storage is included yet.

## Architecture

- `src/core/SourceWorker.Domain` contains the durable domain vocabulary: projects, scan runs, indexed files, detected technologies, Git snapshots, TODOs, context files, prompt recipes, and worker jobs.
- `src/core/SourceWorker.Application` contains ports and use cases. Commands and queries are modeled with `LwX.Flow`-style request/handler abstractions.
- `src/core/SourceWorker.Contracts` contains API/CLI-facing DTOs.
- `src/libs/SourceWorker.Scanner` contains the MVP scanner.
- `src/libs/SourceWorker.Context` writes `.ai-context/` markdown files.
- `src/libs/SourceWorker.Recipes` hosts recipe execution placeholders, starting with a basic handoff.
- `src/infrastructure/SourceWorker.Storage.Localite` wires Localite-backed local storage. Secrets are intentionally not stored.
- `src/infrastructure/SourceWorker.Git` provides basic Git inspection.
- `src/services/SourceWorker.Cli` exposes `scan`, `status`, and `handoff`.
- `src/services/SourceWorker.LocalApi` exposes local HTTP endpoints.
- `src/services/SourceWorker.Worker` provides the in-process queue and project scan worker.

## CLI

```powershell
dotnet run --project src/services/SourceWorker.Cli -- scan C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- scan C:\path\to\project --instructions C:\path\to\todo-louie.md
dotnet run --project src/services/SourceWorker.Cli -- import-instructions C:\path\to\project C:\path\to\todo-louie.md
dotnet run --project src/services/SourceWorker.Cli -- doctor C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- changes C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- context-status C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- artifacts C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- cleanup-preview C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- cleanup-generated C:\path\to\project --dry-run
dotnet run --project src/services/SourceWorker.Cli -- storage-info --json
dotnet run --project src/services/SourceWorker.Cli -- recipes C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- run-recipe C:\path\to\project backend-docs-compact --force
```

## Scanner MVP

The scanner walks a project folder, applies basic `.gitignore` handling, skips common generated directories, detects `.sln`/`.csproj`, README/markdown files, TODO/FIXME/REVIEW comments, and basic technologies: .NET, ASP.NET Core, Blazor, MAUI, Docker, and GitHub Actions.

## Context Output

Scanning writes:

```text
.ai-context/
  CONTEXT.md
  index.yaml
  project.md
  architecture.md
  current-state.md
  tasks.md
  ai-handoff.md
  scans/latest-scan.md
  branches/current.md
  recipes/backend-docs-compact.yaml
```

The first rule-based recipe, `backend-docs-compact`, also writes compact backend documentation to `instructions/Result/Backend/`.

Generated markdown includes SourceWorker metadata comments with format version, project id, scan run id, recipe id, generated UTC, input hash, content hash, and status. Doctor/context-status/artifacts use these records to detect missing, manually modified, orphaned, and deprecated generated files. `cleanup-preview` is read-only; `cleanup-generated` only deletes confidently generated orphan/deprecated files.

See [docs/first-mvp.md](docs/first-mvp.md) for run commands.
