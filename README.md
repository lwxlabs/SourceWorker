# SourceWorker

SourceWorker is a local-first project understanding and AI context handoff app for codebases. Its job is to make a repository understandable and portable for AI agents: scan the project, summarize its structure, validate scan completeness, generate clean context packs, and produce an Agent Handoff that can be copied into Codex, Junie, ChatGPT, Cursor, Claude Code, or another coding assistant.

SourceWorker is not a coding agent, terminal runner, patch applier, CI replacement, or autonomous editor. AI output is preview-only and never writes files automatically. SourceWorker may edit markdown files only when the user opens them in the Markdown Workspace and explicitly saves. MCP, cloud sync, licensing, terminal agent execution, autonomous code editing, patch application, full installer/updater, and production code signing are still intentionally not included.

## Primary Product Flow

```text
Open SourceWorker
→ Select or register project
→ Scan / understand
→ Review scan completeness
→ Build Agent Context Pack
→ Preview what the AI agent will receive
→ Copy/export/open handoff
→ Give it to Codex, Junie, ChatGPT, Cursor, Claude Code, or another agent
```

MVP 2.7 corrects the navigation model and consolidates the product UX around one daily loop:

```text
Pick repo -> scan -> understand -> copy handoff for agent
```

Global navigation is intentionally reduced to Home, Projects, Settings, and Advanced. Project-specific pages only appear after selecting a project.

Project workspace navigation:

- Overview
- Handoff
- Ask
- Docs
- Packs
- Scan Health
- Code
- Artifacts
- Jobs

Advanced global pages are Diagnostics, Logs, Dogfood, and All Jobs. Project Jobs are the normal place to watch scan/export progress; global jobs are for diagnostics. Artifacts are presented as Generated Files Health, not as a primary user concept.

The primary generated file is:

```text
.ai-context/agent-handoff.md
```

It is clean markdown without the old heavy generated metadata block. Full lifecycle metadata remains in `.ai-context/index.yaml` and Localite generated artifact records.

Handoff quality rules:

- infer project purpose from README/project names/symbols/tests before falling back to generated docs
- describe useful project structure even when a repo is not clean architecture
- exclude `.ai-context/**` and `instructions/Result/**` TODOs from source TODO summaries
- do not show SourceWorker internal flows in non-SourceWorker projects
- infer candidate domain flows for libraries from public APIs, tests, and source folders
- hide SourceWorker metadata markers in normal markdown previews

## Architecture

- `src/core/SourceWorker.Domain` contains the durable domain vocabulary: projects, scan runs, indexed files, detected technologies, Git snapshots, TODOs, context files, prompt recipes, worker jobs, generated artifacts, storage diagnostics, code symbols, routes, services, configuration usage, tests, flows, and context browser models.
- `src/core/SourceWorker.Application` contains ports and use cases. Commands and queries are modeled with `LwX.Flow`-style request/handler abstractions.
- `src/core/SourceWorker.Contracts` contains API/CLI-facing DTOs.
- `src/libs/SourceWorker.Scanner` contains project scanning plus deterministic C# code intelligence and rule-based flow extraction.
- `src/libs/SourceWorker.Context` writes `.ai-context/` markdown files, validates imported source paths, routes outputs, writes context packs/export profiles, checks freshness, manages artifact lifecycle, exposes context browsing, and owns the safe Markdown Workspace file policy.
- `src/libs/SourceWorker.Recipes` hosts rule-based recipe execution, including `backend-docs-compact`.
- `src/libs/SourceWorker.AI` owns AI prompt building, context-pack prompting, token/cost estimates, safety checks, redaction preview, and AI run orchestration.
- Dogfood, friction capture, next-action, project path validation, project AI defaults, Quick Ask, and log records follow the same clean architecture flow: dependency-free Domain models, Application ports/handlers, Localite repositories, Local API endpoints, ApiClient methods, and dashboard pages.
- `src/infrastructure/SourceWorker.AI.Providers` implements OpenAI, Gemini, Ollama, OpenAI-compatible providers, provider presets, and local AppData secret storage.
- `src/infrastructure/SourceWorker.Storage.Localite` wires table-backed Localite/SQLite storage. Secrets are intentionally not stored.
- `src/infrastructure/SourceWorker.Git` provides basic Git inspection.
- `src/services/SourceWorker.Cli` exposes `scan`, `status`, and `handoff`.
- `src/services/SourceWorker.LocalApi` exposes local HTTP endpoints.
- `src/services/SourceWorker.Worker` provides the in-process queue and project scan worker.
- `ui/SourceWorker.ApiClient` is the typed Local API client boundary for UI hosts.
- `ui/SourceWorker.UI` contains reusable Razor components and pages for future browser/desktop hosts.
- `ui/SourceWorker.Dashboard` is the first Blazor Web App host. It is read-only and Local API-driven.
- `ui/SourceWorker.Desktop` is the Windows-first MAUI Blazor Hybrid app for normal daily use.
- `src/libs/SourceWorker.Hosting` contains reusable Local API/AppHost process orchestration shared by AppHost and Desktop.

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

## Desktop Startup

Development:

```powershell
dotnet run --project ui/SourceWorker.Desktop
```

Published exe:

```powershell
.\artifacts\desktop\win-x64\SourceWorker.Desktop.exe
```

Publish:

```powershell
.\tools\publish\Publish-Desktop-Windows.ps1
```

Create shortcut:

```powershell
.\tools\publish\Create-DesktopShortcut.ps1
```

The desktop app starts the Local API internally, waits for health, configures the API client with the local token, and hosts the reusable UI through `BlazorWebView`. After publishing and creating the shortcut, no command is needed for normal use. Windows is first; macOS comes later.

## Development Startup

The easiest way to start everything is using the **AppHost**:

```powershell
dotnet run --project src/services/SourceWorker.AppHost -- --open
```

AppHost remains useful for development and browser/debug workflows. It will:
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
- `/projects/{projectId}/explanation`: generate a durable AI Project Explanation from scanned context, then explicitly save it as generated docs.
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

MVP 2.3 adds daily dogfood workflow polish:

- Dogfood sessions for SourceWorker, Insights, Localite, LwX.Flow, or any selected projects.
- In-app friction issue capture with category, severity, status, and markdown export.
- Local next recommended actions across the home, project, dogfood, AI, and diagnostics surfaces.
- Project path validation before registration, including duplicate, missing, system-folder, no-git, and no-solution warnings.
- Project-level AI defaults and a Quick Ask shortcut from the project dashboard.
- Local logs and correlation-ready records for dogfood debugging.

## AI Explanation Writer

MVP 2.8 adds an explicit explanation workflow for deeper human-readable understanding.

Output path:

```text
.ai-context/docs/backend/07-ai-explanation.md
```

This file is not generated by normal scan. The user must open a project, open the Explanation tab, select provider/model/context/profile, preview the prompt and warnings, generate the explanation, review it, and explicitly click save.

SourceWorker may write this generated docs file after explicit confirmation. It still does not write source files, apply patches, run terminal tasks, or automatically apply AI output.

Profiles:
- Developer Onboarding
- Architecture Deep Dive
- Public API Guide
- Maintenance Guide
- Test And Quality Guide
- Full Project Explanation

The explanation prompt uses bounded context: Agent Handoff, deterministic backend docs, README, ranked public API/source snippets, and representative tests. It excludes secret-prone files, generated pack YAML, databases, `bin/`, `obj/`, `.git/`, `.vs/`, `.idea/`, `.env`, API keys, tokens, and private keys.

The saved explanation is tracked as a generated artifact and appears in the Docs page after `06-verification-checklist.md`.

## Markdown Workspace And Providers

MVP 2.9 adds a real in-app Markdown workspace and a global Providers page.

Markdown Workspace:

- route: `/projects/{projectId}/markdown`
- uses `PSC.Blazor.Components.MarkdownEditor`
- supports Mermaid rendering and syntax highlighting
- lists safe project markdown grouped as Agent Handoff, Backend Docs, AI Explanation, Context Docs, Instructions, User Markdown, External Mirror, and Technical Markdown
- allows editing `.md` and `.markdown` only
- blocks source code, YAML/JSON, `.env`, database files, `.git`, `bin`, `obj`, `.vs`, and `.idea`
- validates Mermaid fenced blocks before save
- requires explicit save and never autosaves

Docs and Explanation integration:

- backend docs remain canonical under `.ai-context/docs/backend`
- `07-ai-explanation.md` can be edited before saving from the Explanation page
- Docs can open backend files in the Markdown Workspace
- external mirrored docs under `instructions/Result/Backend` are not the default editing target

Providers:

- global route: `/providers`
- online providers: OpenAI, Gemini, and OpenAI-compatible online endpoints
- local providers: Ollama, LM Studio through OpenAI-compatible, LocalAI/vLLM-style presets
- OpenAI uses the Responses API first with `store=false`, with chat completions fallback where needed
- Gemini uses `generateContent`
- LM Studio uses the OpenAI-compatible preset at `http://localhost:1234/v1`
- Ollama uses local `/api/tags` and `/api/chat`
- provider secrets stay in local AppData secret storage and API/UI responses expose masked metadata only

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

MVP 2.3 dogfood loop:

1. Open `/dogfood/sessions/new`.
2. Select SourceWorker, Insights, Localite, and LwX.Flow or enter your own paths.
3. Register missing projects.
4. Scan each project.
5. Open generated docs and packs.
6. Preview `ai-handoff` and `backend-docs`.
7. Run one preview-only AI request.
8. Capture at least one friction issue if anything feels awkward.
9. Export dogfood feedback for the next planning pass.

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

The first rule-based recipe, `backend-docs-compact`, writes canonical backend documentation to `.ai-context/docs/backend/` and mirrors it to `instructions/Result/Backend/`. Default packs now include `ai-handoff`, `backend-docs`, `onboarding`, `current-branch`, `dogfood-feedback`, and `ai-explanation`.

AI Explanation Writer can additionally write, after explicit save:

- `.ai-context/docs/backend/07-ai-explanation.md`

User-facing generated markdown avoids the old heavy generated metadata block; full lifecycle metadata belongs in `index.yaml` and Localite generated artifact records. `index.yaml` is format version 5 and includes storage, artifact lifecycle, code intelligence, flow, context browser, outputs, packs, exports, and docs summaries. Doctor/context-status/artifacts use these records to detect missing, manually modified, orphaned, and deprecated generated files. `cleanup-preview` is read-only; `cleanup-generated` only deletes confidently generated orphan/deprecated files.

See [docs/first-mvp.md](docs/first-mvp.md) for run commands.
