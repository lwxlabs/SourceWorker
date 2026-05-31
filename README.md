# SourceWorker

SourceWorker is a local-first AI context management foundation for codebases. The current engine scans projects, persists local state, imports instruction files, generates `.ai-context`, runs rule-based recipes, routes generated outputs, creates context packs, tracks generated artifacts, extracts lightweight C# code intelligence, and exposes a Blazor dashboard over a hardened Local API with Bearer token authentication.

MVP 2.1 adds a safe AI provider preview layer. AI output is preview-only and never writes files automatically. MCP, cloud sync, licensing, terminal agent execution, autonomous code editing, and full MAUI desktop hosting are still intentionally not included.

## Architecture

- `src/core/SourceWorker.Domain` contains the durable domain vocabulary: projects, scan runs, indexed files, detected technologies, Git snapshots, TODOs, context files, prompt recipes, worker jobs, generated artifacts, storage diagnostics, code symbols, routes, services, configuration usage, tests, flows, and context browser models.
- `src/core/SourceWorker.Application` contains ports and use cases. Commands and queries are modeled with `LwX.Flow`-style request/handler abstractions.
- `src/core/SourceWorker.Contracts` contains API/CLI-facing DTOs.
- `src/libs/SourceWorker.Scanner` contains project scanning plus deterministic C# code intelligence and rule-based flow extraction.
- `src/libs/SourceWorker.Context` writes `.ai-context/` markdown files, validates imported source paths, routes outputs, writes context packs/export profiles, checks freshness, manages artifact lifecycle, and exposes read-only context browsing.
- `src/libs/SourceWorker.Recipes` hosts rule-based recipe execution, including `backend-docs-compact`.
- `src/libs/SourceWorker.AI` owns AI prompt building, context-pack prompting, token/cost estimates, safety checks, redaction preview, and AI run orchestration.
- `src/infrastructure/SourceWorker.AI.Providers` implements OpenAI, Ollama, OpenAI-compatible providers, and local AppData secret storage.
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

## Startup

The easiest way to start everything is using the **AppHost**:

```powershell
dotnet run --project src/services/SourceWorker.AppHost -- --open
```

This will:
1. Start the **Local API**.
2. Wait for it to become healthy.
3. Start the **Dashboard** with auto-discovery.
4. Open your browser to the dashboard.

### Manual Startup (Alternative)

If you need to run components separately:

```powershell
dotnet run --project src/services/SourceWorker.LocalApi
dotnet run --project ui/SourceWorker.Dashboard
```

## Dashboard

The UI is interactive for MVP 1.9. It allows registering projects, scanning projects with real-time job progress, exporting docs and packs, previewing context cleanup, and performing safe cleanup with confirmation. It uses a safety-level model to protect destructive actions. It does not read Localite or the filesystem directly; it only uses `SourceWorker.ApiClient`.

MVP 2.1 adds dashboard routes for safe AI preview:

- `/settings/ai`: configure OpenAI, Ollama, or OpenAI-compatible providers and test provider health.
- `/projects/{projectId}/ai`: select a context pack, preview the generated prompt, see safety warnings and token estimates, then run a preview-only AI request.
- `/projects/{projectId}/ai/runs`: view AI run history and output previews.
- `/dogfood`: view the first real-use checklist for daily local dogfooding.
- `/projects/onboarding` and `/projects/{projectId}/onboarding`: guided project onboarding.
- `/settings/diagnostics`: secret-safe local diagnostics summary/export preview.

API keys are never written to `.ai-context`, generated docs, diagnostics, or logs. The current `LocalAiSecretStore` stores secrets under AppData for MVP use; OS secret store integration is planned for production hardening.

## AI Provider Preview

Supported MVP providers:

- OpenAI: API key plus model name.
- Ollama: local base URL, default `http://localhost:11434`, no key required.
- OpenAI-compatible: configurable base URL, optional API key, model name.

Prompt preview is built from a selected context pack, pack metadata, optional user instruction, safe AI assistant rules, project metadata, freshness warnings, source path warnings, and a max output token budget. Token estimates use a deterministic `characters / 4` rule. Cost is shown only when static model price metadata exists; otherwise it is unknown.

Safety checks warn on obvious API keys, tokens, private keys, connection strings, secret-prone files, low context freshness, missing pack files, stale generated files, and manually modified generated files. Warnings require user confirmation before a run. AI runs persist run metadata, prompt hash, included files, warnings, token estimate, status, error, and output text/hash. They do not write source or generated files.

MVP 2.2 hardens daily use:

- Provider validation for missing keys, missing models, invalid URLs, token limits, and local Ollama setup.
- Provider diagnostics with suggested next actions.
- Static model catalog and pricing/context-window metadata for known OpenAI models, plus unknown-cost fallback.
- Prompt preview sections, included-file token estimates, prompt hash, cost estimate, and context-window usage.
- Project/global AI usage summaries.
- Pack readiness/quality scoring before AI runs.
- AI history/privacy settings.
- Dogfood readiness and diagnostics endpoints.

## Dogfood Tutorial

Use SourceWorker on SourceWorker:

```powershell
dotnet run --project src/services/SourceWorker.AppHost -- --open
```

Then:

1. Register the SourceWorker repo.
2. Scan.
3. Open docs.
4. Open packs.
5. Configure Ollama or OpenAI.
6. Preview the `backend-docs` pack.
7. Ask: “What are the next 5 improvements before using this daily?”
8. Run AI preview.
9. Copy output.
10. Verify no files changed from the AI run.

Use SourceWorker on Insights:

1. Register the Insights path.
2. Scan.
3. Run backend docs export.
4. Preview the `backend-docs` pack.
5. Ask AI to explain scan/discovery/scoring flows.
6. Copy useful answers into manual notes.

## Test Execution Rules For Codex

Codex must not run the full solution test suite by default.

Codex must not run `CliCommandTests` unless `src/services/SourceWorker.Cli/Program.cs`, CLI command parsing, CLI output, or CLI behavior changed. Do not run `CliCommandTests` for AppHost-only, dashboard/UI-only, ApiClient-only, DTO/property-only, docs-only, CSS/layout, AI provider UI changes unless CLI commands were added, or Local API endpoint changes unless CLI consumes that endpoint.

Targeted examples:

- AI provider domain/application changes: run AI provider tests and affected Application tests.
- AI provider Local API endpoints: run LocalApi AI endpoint tests and ApiClient AI tests.
- AI dashboard UI: build `ui/SourceWorker.UI` and `ui/SourceWorker.Dashboard`; run ApiClient AI tests if the client changed.
- Docs-only: no tests required.
- Shared contract changes: run affected ApiClient and LocalApi tests; full tests only if broad contracts break many projects.
- Final milestone: build solution, run targeted AI/ApiClient/LocalApi tests, and only run full tests if shared architecture, storage, security middleware, or application base abstractions changed.

Every Codex summary must include what changed, which targeted tests were run, why they were enough, whether full tests were intentionally skipped, and whether `CliCommandTests` were skipped and why.

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
