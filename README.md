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

See [docs/first-mvp.md](docs/first-mvp.md) for run commands.
