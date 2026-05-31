# SourceWorker

SourceWorker is a local-first AI context management foundation for codebases. The current engine scans projects, persists local state, imports instruction files, generates `.ai-context`, runs rule-based recipes, routes generated outputs, creates context packs, tracks generated artifacts, extracts lightweight C# code intelligence, and exposes a Blazor dashboard over a hardened Local API with Bearer token authentication.

No AI provider, desktop UI, MCP server, cloud sync, or licensing is included yet.

## Architecture

- `src/core/SourceWorker.Domain` contains the durable domain vocabulary: projects, scan runs, indexed files, detected technologies, Git snapshots, TODOs, context files, prompt recipes, worker jobs, generated artifacts, storage diagnostics, code symbols, routes, services, configuration usage, tests, flows, and context browser models.
- `src/core/SourceWorker.Application` contains ports and use cases. Commands and queries are modeled with `LwX.Flow`-style request/handler abstractions.
- `src/core/SourceWorker.Contracts` contains API/CLI-facing DTOs.
- `src/libs/SourceWorker.Scanner` contains project scanning plus deterministic C# code intelligence and rule-based flow extraction.
- `src/libs/SourceWorker.Context` writes `.ai-context/` markdown files, validates imported source paths, routes outputs, writes context packs/export profiles, checks freshness, manages artifact lifecycle, and exposes read-only context browsing.
- `src/libs/SourceWorker.Recipes` hosts rule-based recipe execution, including `backend-docs-compact`.
- `src/infrastructure/SourceWorker.Storage.Localite` wires table-backed Localite/SQLite storage. Secrets are intentionally not stored.
- `src/infrastructure/SourceWorker.Git` provides basic Git inspection.
- `src/services/SourceWorker.Cli` exposes `scan`, `status`, and `handoff`.
- `src/services/SourceWorker.LocalApi` exposes local HTTP endpoints.
- `src/services/SourceWorker.Worker` provides the in-process queue and project scan worker.
- `ui/SourceWorker.ApiClient` is the typed Local API client boundary for UI hosts.
- `ui/SourceWorker.UI` contains reusable Razor components and pages for future browser/desktop hosts.
- `ui/SourceWorker.Dashboard` is the first Blazor Web App host. It is read-only and Local API-driven.

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
dotnet run --project src/services/SourceWorker.Cli -- symbols C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- flows C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- routes C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- context-files C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- read-context C:\path\to\project .ai-context/ai-handoff.md
dotnet run --project src/services/SourceWorker.Cli -- outputs C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- packs C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- pack-show C:\path\to\project backend-docs
dotnet run --project src/services/SourceWorker.Cli -- pack-export C:\path\to\project backend-docs --out C:\path\to\project\.ai-context\packs\backend-docs.md
dotnet run --project src/services/SourceWorker.Cli -- export-docs C:\path\to\project backend-docs-compact
dotnet run --project src/services/SourceWorker.Cli -- recipes C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- run-recipe C:\path\to\project backend-docs-compact --force
```

## Dashboard

Start the Local API and dashboard:

```powershell
dotnet run --project src/services/SourceWorker.LocalApi
dotnet run --project ui/SourceWorker.Dashboard
```

The UI is interactive for MVP 1.9. It allows registering projects, scanning projects with real-time job progress, exporting docs and packs, previewing context cleanup, and performing safe cleanup with confirmation. It uses a safety-level model to protect destructive actions. It does not read Localite or the filesystem directly; it only uses `SourceWorker.ApiClient`.

## Verification Policy

To ensure stability while maintaining development speed, follow this targeted verification policy:

- **Small UI-only changes**: Run build for affected UI projects and relevant UI/ApiClient tests only.
- **DTO/Property-only changes**: Run affected ApiClient, LocalApi, and Application tests.
- **Scanner changes**: Run scanner tests and only related application/CLI tests.
- **Local API endpoint changes**: Run LocalApi tests and ApiClient tests.
- **Storage/Schema changes**: Run storage, persistence, freshness, and affected API tests.
- **Security/Auth/CORS changes**: Run LocalApi security tests and ApiClient connection tests.
- **CLI command changes**: Run CLI command tests and smoke-test the changed command.
- **Docs-only changes**: No test run required unless docs generation code changed.
- **Major changes / Final verification**: Run full `dotnet test SourceWorker.slnx --no-build`.

## Scanner MVP

The scanner walks a project folder, applies basic `.gitignore` handling, skips common generated directories, detects `.sln`/`.csproj`, README/markdown files, TODO/FIXME/REVIEW comments, and basic technologies: .NET, ASP.NET Core, Blazor, MAUI, Docker, and GitHub Actions.

MVP 1.5 adds lightweight C# code intelligence. It detects classes, interfaces, records, enums, methods, async methods, controller/minimal API routes, service registrations, configuration reads, background workers, tests, and high-confidence rule-based flows. This is deterministic text parsing, not a full compiler or AI analysis layer.

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
  instructions/README.md
  instructions/imported/
  docs/backend/
  packs/*.pack.yaml
  exports/backend-docs-compact.yaml
  diagrams/*.mmd
  scans/latest-scan.md
  branches/current.md
  recipes/backend-docs-compact.yaml
```

Folder responsibilities:

- `.ai-context/instructions/` is for imported/user-authored instructions and validation.
- `.ai-context/docs/` is the canonical internal generated documentation area.
- `.ai-context/packs/` stores AI-ready bundle manifests and exported pack markdown.
- `.ai-context/exports/` stores export profiles/history.
- `instructions/Result/Backend/` remains an external mirrored Markdown export target.

The first rule-based recipe, `backend-docs-compact`, writes canonical backend documentation to `.ai-context/docs/backend/` and mirrors it to `instructions/Result/Backend/`.

Generated markdown includes SourceWorker metadata comments with format version, project id, scan run id, recipe id, generated UTC, input hash, content hash, and status. `index.yaml` is format version 5 and includes storage, artifact lifecycle, code intelligence, flow, context browser, outputs, packs, exports, and docs summaries. Doctor/context-status/artifacts use these records to detect missing, manually modified, orphaned, and deprecated generated files. `cleanup-preview` is read-only; `cleanup-generated` only deletes confidently generated orphan/deprecated files.

See [docs/first-mvp.md](docs/first-mvp.md) for run commands.
