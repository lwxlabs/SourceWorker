# SourceWorker Current Progress

Date: 2026-06-01

SourceWorker is a local-first project understanding and AI context handoff app with a protected Local API, Windows-first MAUI Blazor Hybrid desktop app, one-start AppHost fallback, interactive Blazor dashboard, context packs, output routing, generated docs, artifact lifecycle, project scanning, code intelligence, dashboard write actions, MVP 2.1 safe AI provider preview, MVP 2.2 AI/dogfood hardening, MVP 2.3 real dogfood feedback loops, MVP 2.4 desktop publishing, MVP 2.5 Agent Handoff Mode, MVP 2.6 product UX consolidation, and MVP 2.7 corrected project workspace navigation.

Product statement:

```text
SourceWorker's job is to make a codebase understandable and portable for AI agents.
It does not replace the agent; it prepares the context.
```

SourceWorker is not a coding agent, autonomous editor, terminal runner, patch applier, CI replacement, MCP host, or cloud sync tool.

Still intentionally not included:
- MCP
- cloud sync
- licensing
- terminal agent execution
- autonomous code editing
- automatic AI file writes
- full installer/updater
- production code signing

## MVP 2.5 Agent Handoff Refocus

MVP 2.5 refocuses the product around the core daily flow:

```text
Open SourceWorker
→ Select project
→ Scan / understand
→ See scan completeness
→ Build Agent Context Pack
→ Preview what the AI agent will receive
→ Copy / export / open handoff
→ Give it to Codex, Junie, ChatGPT, Cursor, Claude Code, or another tool
```

New primary generated output:

```text
.ai-context/agent-handoff.md
```

This file is clean, human-readable markdown. It does not include the old heavy generated metadata block. It contains:
- project identity
- purpose
- current state
- architecture
- important files
- AI working rules
- TODO/risk summary
- high-confidence flows
- useful generated context packs/docs
- recommended next agent prompt

Metadata strategy changed:
- full lifecycle metadata remains in `.ai-context/index.yaml`
- Localite generated artifact records remain the durable source for artifact status, hashes, stale/manual detection, and generator data
- user-facing markdown no longer repeats the full `GeneratedBy`, `FormatVersion`, `ProjectId`, `ScanRunId`, `InputHash`, and `ContentHash` block
- generated markdown now uses either no marker for high-value handoff files or a compact marker like `<!-- SourceWorker:Generated; ArtifactId=...; Status=Generated -->`

New scan completeness state:
- `ScanCompletenessReport`
- `ScanStageResult`
- `ScanStageStatus`
- `ScanStageWarning`

Tracked stages include project root validation, ignore rules, file indexing, solution/project detection, project/package/reference parsing, Git state, TODO extraction, C# symbols, routes, service registrations, configuration usages, tests, flow extraction, context files, packs, backend docs, and artifact tracking.

Critical zero-state behavior:
- if `.cs` files exist but persisted symbols are 0, scan completeness reports a warning
- possible explanations include ignored files, parser failure, unsupported syntax, failed persistence, or stale UI data
- the Project Dashboard and Agent Handoff page surface this explanation instead of silently showing 0 symbols/routes/flows

UI refocus:
- new project route: `/projects/{projectId}/handoff`
- Agent Handoff is the primary project workflow
- AI Preview is reframed as "Ask With Context"
- context browser defaults to user-facing context only
- technical files and external mirrored docs are hidden by default behind toggles
- backend docs page should treat `.ai-context/docs/backend` as canonical and `instructions/Result/Backend` as an external mirror

## MVP 2.6 Product UX Consolidation

MVP 2.6 keeps the product centered on:

```text
Pick repo -> scan -> understand -> copy handoff for agent
```

Navigation is simplified:

- Primary: Home, Projects, Handoff, Ask, Settings
- Advanced: Scan Health, Context Browser, Docs, Packs, Code Intelligence, Artifacts, Jobs, Diagnostics, Logs, Dogfood

Project open now leads to the Agent Handoff route so a selected repo starts with handoff readiness instead of raw internal diagnostics.

Handoff readiness is separated from generated artifact health:

- Scan completeness answers whether SourceWorker understood the repo.
- Handoff readiness answers whether the user can copy useful context now.
- Generated artifact health answers whether tracked generated files need cleanup/review.

Handoff quality changes:

- purpose inference reads README/project names/symbols/tests before using generated docs
- non-clean-architecture repos still get useful structure summaries
- library/test-heavy repos do not treat missing routes/DI/config as scary warnings
- TODO/FIXME/REVIEW summaries exclude `.ai-context/**` and `instructions/Result/**`
- SourceWorker internal flows are excluded unless the scanned project is SourceWorker
- library projects get candidate domain flows inferred from public symbols, tests, and source folders
- normal markdown previews hide compact SourceWorker metadata markers by default

Packs and docs are presented as product concepts:

- `ai-handoff` = Quick Agent Handoff
- `backend-docs` = Architecture Deep Dive
- `onboarding` = Project Onboarding
- `current-branch` = Current Branch Context
- `dogfood-feedback` = SourceWorker improvement feedback only

## MVP 2.7 Correct Navigation And Project Workspace

MVP 2.7 fixes the navigation model so project-specific pages are not global sidebar items.

Global navigation:

- Home
- Projects
- Settings
- Advanced

Advanced global pages:

- Diagnostics
- Logs
- Dogfood
- All Jobs

Project workspace navigation appears only after selecting a project:

- Overview
- Handoff
- Ask
- Docs
- Packs
- Scan Health
- Code
- Artifacts
- Jobs
- Context Browser

Project workspace header shows selected project identity, root path, last scan, scan completeness, handoff readiness, generated-file health, running job status, scan/rescan, ask, and copy-handoff actions.

Project Jobs are the normal place to watch scan/export progress for a selected repo. Global All Jobs remains available under Advanced for diagnostics only.

Artifacts are reframed as Generated Files Health. The default view shows issue counts and needs-attention groups first; healthy generated files and technical artifact records are collapsed.

Handoff quality improvements:

- Public API/Main Concepts now prefers non-test public interfaces/classes from the main project.
- test fakes/mocks/sample classes are deprioritized
- Localite-style library handoffs emphasize interfaces, context/store APIs, public operations, and tests instead of fake test entities
- Imported Instructions no longer prints duplicate empty-state text

## Current Architecture

- `SourceWorker.Domain` is dependency-free and owns project, scan, instruction, recipe, job, artifact, freshness, cleanup, storage diagnostic, output routing, export, context pack, code intelligence, Local API, and AI preview domain models.
- `SourceWorker.Application` owns LwX.Flow request handlers and ports. CLI, Local API, and UI-facing endpoints call application use cases only.
- `SourceWorker.Scanner` scans project shape, file hashes, technologies, Git state, TODOs, lightweight C# symbols, routes, services, config usages, tests, and rule-based flows.
- `SourceWorker.Context` writes `.ai-context`, validates imported source paths, routes generated outputs, writes context packs/export profiles, checks freshness, analyzes artifacts, performs safe cleanup, and exposes read-only context browsing.
- `SourceWorker.Recipes` owns rule-based recipes including `backend-docs-compact`.
- `SourceWorker.AI` is the AI core library. It builds prompts from context packs, estimates tokens/costs, runs safety checks, redacts secret-like values, and orchestrates preview-only AI runs.
- `SourceWorker.Storage.Localite` uses explicit table-backed Localite/SQLite storage.
- `SourceWorker.AI.Providers` is the provider infrastructure library for OpenAI, Ollama, OpenAI-compatible endpoints, and local AppData AI secret storage.
- `SourceWorker.Infrastructure` composes storage, scanner, context, recipes, AI core, AI providers, Git, and Local API runtime services.
- `SourceWorker.LocalApi` is the protected HTTP boundary.
- `SourceWorker.ApiClient` is the dashboard/client boundary. It has no Blazor dependency.
- `SourceWorker.UI` contains reusable Razor components/pages.
- `SourceWorker.Dashboard` is the current Blazor Web App host.
- `SourceWorker.Desktop` is the Windows-first MAUI Blazor Hybrid app. It hosts `SourceWorker.UI`, starts/manages Local API internally, configures `SourceWorker.ApiClient`, and can be published as an `.exe`.
- `SourceWorker.Hosting` is the shared local orchestration library used by Desktop and AppHost.
- `SourceWorker.AppHost` starts Local API and Dashboard together with token/base URL discovery for development/browser fallback.

## MVP 2.4 Desktop App

MVP 2.4 adds `ui/SourceWorker.Desktop`, a Windows-first MAUI Blazor Hybrid host for normal daily use. It is not a browser dashboard wrapper; it uses `BlazorWebView` to host the reusable `SourceWorker.UI` components directly.

Desktop startup behavior:
- starts desktop shell
- checks for/reuses an existing healthy Local API on `http://localhost:5000`
- starts `SourceWorker.LocalApi` if needed
- waits for `/health`
- reads the same AppData API token used by AppHost
- configures `SourceWorker.ApiClient` with base URL and bearer token
- shows startup progress and rich startup errors
- stops the Local API process on shutdown only when Desktop started it

Desktop-specific pieces:
- `IDesktopLocalApiLifecycleService`
- `IDesktopRuntimeStateService`
- `IDesktopApiConnectionService`
- `IDesktopStartupDiagnosticsService`
- `IDesktopShellService`
- `IDesktopWindowService`
- `IDesktopNotificationService`
- `IDesktopClipboardService`
- `IDesktopFileDialogService`
- `IDesktopExternalLinkService`
- `ISingleInstanceService`

Desktop UI additions:
- startup progress/error screen
- bottom Local API status bar
- Desktop launch mode in settings
- Browse button seam for project registration
- single-instance mutex
- in-app notification abstraction

Publish output:

```text
artifacts/desktop/win-x64/
  SourceWorker.Desktop.exe
  LocalApi/
    SourceWorker.LocalApi.exe
```

Normal desktop development command:

```powershell
dotnet run --project ui/SourceWorker.Desktop
```

Publish command:

```powershell
.\tools\publish\Publish-Desktop-Windows.ps1
```

Shortcut command:

```powershell
.\tools\publish\Create-DesktopShortcut.ps1
```

Known MVP 2.4 limitations:
- no full installer/MSIX yet
- no update mechanism yet
- no production code signing yet
- tray/focus-existing-window behavior is only prepared, not complete
- folder picker uses an MVP file-picker fallback where native folder picking is not available
- OS notifications are represented by an abstraction and in-app/no-op implementation

## MVP 2.3 Dogfood Feedback Loop

MVP 2.3 focuses on making SourceWorker usable every day on real repositories such as SourceWorker itself, Insights, Localite, and LwX.Flow. It adds session tracking, friction capture, next-action recommendations, path validation, project-level AI defaults, Quick Ask, and local logs. It still does not add autonomous code editing or automatic AI file writes.

Dogfood/onboarding surfaces:
- `/dogfood`
- `/dogfood/sessions`
- `/dogfood/sessions/new`
- `/dogfood/sessions/{sessionId}`
- `/dogfood/issues`
- `/projects/onboarding`
- `/projects/{projectId}/onboarding`

Dogfood readiness/session concepts:
- AppHost detected
- Local API connected
- token configured
- at least one project registered
- latest scan exists
- context freshness available
- at least one context pack exists
- AI provider configured
- AI provider health/check readiness
- at least one prompt preview or AI run
- at least one AI run completed
- no AI file writes detected
- selected projects and per-project readiness
- completed/failed session steps
- friction issues and notes
- generated recommendations and feedback export

New MVP 2.3 models and services:
- `DogfoodSession`, `DogfoodSessionProject`, `DogfoodSessionStep`, `DogfoodSessionIssue`, and `DogfoodSessionNote`
- `DogfoodFrictionCategory`, `DogfoodSeverity`, and `DogfoodOutcome`
- `NextAction`, `NextActionKind`, `NextActionPriority`, `NextActionSource`, and `NextActionTarget`
- `ProjectPathValidationResult` and `RecentProjectPath`
- `ProjectAiSettings`, `QuickAskRequest`, and `QuickAskResult`
- `AiRunNote`, `AiRunRating`, and `AiRunComparison`
- `LogEntry`, `CorrelationId`, and `OperationContext`

The dashboard home now shows next recommended actions. The project dashboard links directly to AI preview, backend docs, packs, onboarding, and includes a Quick Ask card that uses project AI defaults.

## One-Start Command

```powershell
dotnet run --project src/services/SourceWorker.AppHost -- --open
```

This starts Local API, waits for `/health`, discovers the bearer token from AppData, starts the dashboard, passes `SOURCEWORKER_API_BASE_URL` and `SOURCEWORKER_API_TOKEN`, and optionally opens the browser.

## CLI Commands

Existing CLI commands remain stable:

- `scan <projectPath>`
- `scan <projectPath> --instructions <instructionFilePath>`
- `import-instructions <projectPath> <instructionFilePath>`
- `doctor <projectPath>` / `doctor <projectPath> --json`
- `changes <projectPath>` / `changes <projectPath> --json`
- `context-status <projectPath>`
- `artifacts <projectPath>` / `artifacts <projectPath> --json`
- `cleanup-preview <projectPath>` / `cleanup-preview <projectPath> --json`
- `cleanup-generated <projectPath> --dry-run`
- `cleanup-generated <projectPath> --force`
- `storage-info` / `storage-info --json`
- `symbols <projectPath>` / `symbols <projectPath> --json`
- `flows <projectPath>` / `flows <projectPath> --json`
- `routes <projectPath>` / `routes <projectPath> --json`
- `context-files <projectPath>` / `context-files <projectPath> --json`
- `read-context <projectPath> <relativePath>`
- `outputs <projectPath>` / `outputs <projectPath> --json`
- `packs <projectPath>` / `packs <projectPath> --json`
- `pack-show <projectPath> <packId>` / `pack-show <projectPath> <packId> --json`
- `pack-export <projectPath> <packId> --out <outputFile>`
- `export-docs <projectPath> <exportProfileId>`
- `recipes <projectPath>`
- `run-recipe <projectPath> <recipeId>` / `run-recipe <projectPath> <recipeId> --force`
- `status`
- `handoff <projectPath>`

No AI CLI commands were added in MVP 2.1. AI preview is Local API and dashboard driven.

## Dashboard Routes

- `/`
- `/projects`
- `/projects/{projectId}`
- `/projects/{projectId}/handoff`
- `/projects/{projectId}/context`
- `/projects/{projectId}/docs`
- `/projects/{projectId}/packs`
- `/projects/{projectId}/code`
- `/projects/{projectId}/artifacts`
- `/projects/{projectId}/ai`
- `/projects/{projectId}/ai/runs`
- `/dogfood`
- `/dogfood/sessions`
- `/dogfood/sessions/new`
- `/dogfood/sessions/{sessionId}`
- `/dogfood/issues`
- `/projects/onboarding`
- `/projects/{projectId}/onboarding`
- `/jobs`
- `/settings`
- `/settings/ai`
- `/settings/diagnostics`
- `/settings/logs`

Dashboard behavior:
- register projects
- scan projects with job progress polling
- inspect Agent Handoff readiness and copy clean handoff markdown for another AI agent
- view scan completeness and zero-state warnings for code intelligence
- export docs and context packs
- preview and run safe cleanup
- browse generated context files
- view backend docs, packs, artifacts, code symbols, routes, flows, jobs, storage, runtime/security info
- configure AI providers
- build "Ask With Context" prompt previews from packs
- run preview-only AI requests
- view AI run history
- validate provider setup
- diagnose provider connection issues
- inspect AI usage and pack readiness
- view secret-safe diagnostics
- start and review dogfood sessions
- capture friction issues and export issue summaries
- validate project paths before registration
- view recent local logs
- use Quick Ask from a project dashboard

## Localite Storage State

- Provider: Localite
- Mode: table-backed SQLite
- Schema version: 1
- App storage format version: 3
- Current database path on this machine: `C:\Users\louie\AppData\Local\SourceWorker\sourceworker.localite.db`

Storage includes records for projects, scan runs, indexed files, detected technologies, Git snapshots, TODOs, generated artifacts, instruction documents/sections/source paths, source path validation results, prompt recipes, worker jobs/steps, doctor reports, code symbols, flows, routes, service registrations, configuration usages, background workers, tests, AI provider configurations, and AI runs.

Secrets are not stored in Localite. AI provider secret values are handled by `LocalAiSecretStore` under AppData for MVP use and only masked key metadata is exposed. OS secret store integration is still planned.

`storage-info` now includes AI provider configuration count and AI run count.

## AI Provider Preview State

Supported providers:
- OpenAI
- Ollama
- OpenAI-compatible

Provider setup:
- OpenAI: API key and model name.
- Ollama: base URL, default `http://localhost:11434`, model name, no key by default.
- OpenAI-compatible: base URL, model name, optional API key.

Prompt preview:
- selects a project and context pack (`ai-handoff`, `backend-docs`, `onboarding`, or `current-branch`)
- includes pack metadata and pack files
- accepts optional user instruction
- injects safe AI assistant rules
- includes freshness and missing-file warnings
- estimates tokens with characters divided by 4
- estimates cost only when model price metadata exists
- shows final prompt text before any AI request

Safety:
- detects obvious API keys/tokens/private keys/connection strings
- warns on `.env`, secret-prone references, low freshness, stale/manual generated files, and missing pack files
- warnings require confirmation before an AI run
- redaction preview can replace secret-like values with `[REDACTED]`

AI runs:
- persist provider/model/pack/user instruction metadata
- persist prompt hash, included files, warnings, token estimate, status, created/completed UTC, error, output text, and output hash
- do not write files
- are copy/preview oriented only

MVP 2.2 hardening:
- `AiProviderValidationResult`, `AiProviderValidationIssue`, `AiProviderSetupState`, and `AiProviderConnectionDiagnostic` model provider setup and failure states.
- OpenAI validation checks missing key/model and URL/token fields.
- Ollama validation checks base URL/model and diagnostics give “start Ollama / pull model” guidance.
- OpenAI-compatible validation checks base URL/model and returns clearer diagnostic shape for bad responses.
- `ISecretStore`, `ISecretStoreProvider`, `ISecretStoreHealthService`, and `ISecretMetadataService` prepare for OS secret store integration.
- `AppDataSecretStore` is the current MVP store and exposes only masked metadata publicly.
- `NoopSecretStore` exists for tests.
- Static model catalog includes known OpenAI models and pricing/context-window metadata where known.
- Prompt preview now includes sections, included-file token estimates, missing files, prompt hash, cost estimate, context-window usage, too-large warning, max output token setting, and template selection.
- Prompt templates include general project question, continue development, explain architecture, find risks, improve docs, create next steps, review branch, bug investigation, and test planning.
- AI run history shows run duration/error/output and supports simple status filtering.
- AI settings include history/privacy settings: store prompt, store output, max runs per project, and failed-run retention.
- AI usage summaries exist globally and per project.
- Pack quality scoring checks missing files and freshness before AI runs.
- Diagnostics summary/export excludes secrets, source code, and prompts by default.

MVP 2.3 daily-use additions:
- Dogfood session services create default daily-use steps: choose projects, register missing projects, scan, check freshness, open docs/packs, preview `ai-handoff`, preview `backend-docs`, run AI preview, check diagnostics, capture friction, and finish.
- Friction issues can be reported with route, optional project id, category, severity, title, expected/actual behavior, and status.
- Next-action generation uses project status, provider status, freshness, jobs, diagnostics, dogfood issues, and pack readiness to suggest the next useful action.
- Project path validation catches missing paths, unsafe system folders, unreadable paths, duplicate-style cases, missing `.git`, and missing `.sln` as warning/error states before registration.
- Project AI defaults store default provider, model, pack, prompt template, max output tokens, prompt/output retention flags, and freshness threshold.
- Quick Ask builds a prompt preview and can run a preview-only AI request using those project defaults.
- Local logs provide a lightweight dogfood debugging surface for recent jobs, provider diagnostics, and local app events.

## Generated Context

`.ai-context/` structure:

- `CONTEXT.md`
- `index.yaml`
- `agent-handoff.md`
- `project.md`
- `architecture.md`
- `current-state.md`
- `tasks.md`
- `ai-handoff.md`
- `instructions/README.md`
- `instructions/imported/imported-instructions.md` when imported
- `instructions/imported/source-path-validation.md` when imported
- `docs/backend/*.md`
- `packs/*.pack.yaml`
- `dogfood/` feedback exports when explicitly exported
- `exports/backend-docs-compact.yaml`
- `diagrams/*.mmd`
- `recipes/*.yaml`
- `scans/latest-scan.md`
- `branches/current.md`

Folder responsibilities:
- `.ai-context/instructions/` is imported/user-authored intent.
- `.ai-context/docs/` is canonical generated documentation.
- `.ai-context/packs/` is AI-ready pack manifests and markdown exports.
- `.ai-context/exports/` is export profile/history state.
- `instructions/Result/Backend/` is the external Markdown mirror.

User-facing markdown is now clean by default. Heavy generated metadata is not repeated in every file; artifact metadata belongs in `index.yaml` and Localite records. The context browser defaults to user-facing context and hides technical YAML/raw scan/export mirror files unless the user enables the technical/export toggles.

`index.yaml` is format version 5 and includes project, latest scan, Git, storage, context files, recipes, imported instructions, stale status, artifact lifecycle, cleanup, code intelligence, flow confidence, context browser, outputs, packs, exports, and docs state.

Default packs are `ai-handoff`, `backend-docs`, `onboarding`, `current-branch`, and `dogfood-feedback`. The dogfood pack is intended to carry exported dogfood issues/session summaries, diagnostics-safe summaries, and current handoff context into the next planning pass.

## Code Intelligence And Flow Extraction

The scanner uses deterministic text parsing, not Roslyn semantic analysis yet.

Detected:
- classes/interfaces/records/structs/enums
- methods and async methods
- controller attributes and minimal API route mappings
- DI registrations
- configuration usage
- hosted/background worker classes
- tests and test methods
- high-confidence rule-based flows

Flows include trigger text, involved files, involved symbols, ordered steps, confidence, warnings, and Mermaid flowcharts for high-confidence flows.

## Local API Endpoints

Public:
- `GET /health`
- `GET /api/version`
- `GET /api/runtime/info`

Protected engine endpoints include projects, jobs, doctor, changes, context status, context files, scan runs, storage info, artifacts, cleanup, symbols, flows, routes, service registrations, configuration usages, tests, context browser, outputs, packs, and exports.

MVP 2.1 protected AI endpoints:
- `GET /api/ai/providers`
- `GET /api/ai/providers/status`
- `POST /api/ai/providers/configure`
- `POST /api/ai/providers/{providerId}/test`
- `GET /api/projects/{id}/ai/packs`
- `POST /api/projects/{id}/ai/prompt-preview`
- `POST /api/projects/{id}/ai/runs`
- `GET /api/projects/{id}/ai/runs`
- `GET /api/projects/{id}/ai/runs/{runId}`

MVP 2.2 protected endpoints:
- `POST /api/ai/providers/validate`
- `POST /api/ai/providers/{providerId}/diagnose`
- `GET /api/ai/settings`
- `PUT /api/ai/settings`
- `GET /api/ai/usage`
- `DELETE /api/projects/{id}/ai/runs`
- `DELETE /api/projects/{id}/ai/runs/{runId}`
- `GET /api/projects/{id}/ai/usage`
- `GET /api/projects/{id}/ai/packs/{packId}/quality`
- `GET /api/dogfood/status`
- `GET /api/projects/{id}/dogfood/status`
- `GET /api/diagnostics/summary`
- `POST /api/diagnostics/export`

MVP 2.3 protected endpoints:
- `POST /api/projects/validate-path`
- `GET /api/projects/recent-paths`
- `GET /api/projects/{id}/ai/settings`
- `PUT /api/projects/{id}/ai/settings`
- `POST /api/projects/{id}/ai/quick-ask/preview`
- `POST /api/projects/{id}/ai/quick-ask/run`
- `GET /api/dogfood/sessions`
- `POST /api/dogfood/sessions`
- `GET /api/dogfood/sessions/{sessionId}`
- `POST /api/dogfood/sessions/{sessionId}/steps/{stepId}/complete`
- `POST /api/dogfood/sessions/{sessionId}/finish`
- `POST /api/dogfood/sessions/{sessionId}/export`
- `GET /api/dogfood/issues`
- `POST /api/dogfood/issues`
- `PUT /api/dogfood/issues/{issueId}/status`
- `POST /api/dogfood/issues/export`
- `GET /api/next-actions`
- `GET /api/projects/{id}/next-actions`
- `GET /api/logs`

All protected endpoints require `Authorization: Bearer <token>` when token auth is enabled.

No new Local API endpoints were required for MVP 2.4. Desktop consumes the existing runtime, project, dashboard, dogfood, diagnostics, AI, and context endpoints through `SourceWorker.ApiClient`.

## Test Coverage

Existing tests cover scanner behavior, instruction parsing, context generation, persistence/freshness, storage/cleanup, application flows, ApiClient behavior, Local API health/security/project/action endpoints, and AppHost orchestration.

MVP 2.1 adds targeted coverage for:
- AI token estimation
- AI safety detection
- AI redaction preview
- AI cost estimate unknown state
- ApiClient AI methods and bearer token usage
- Local API AI endpoint authorization
- provider configuration
- prompt preview endpoint
- AI run creation endpoint

MVP 2.2 adds targeted coverage for:
- OpenAI missing key/model validation
- invalid URL validation
- masked secret metadata
- provider diagnostic failure shape
- known model pricing/context-window metadata
- expanded ApiClient AI calls and bearer usage
- expanded Local API AI/dogfood/diagnostics endpoints

MVP 2.3 adds targeted coverage for:
- dogfood session creation and step completion
- friction issue creation and status update
- next-action generation for empty/initial app states
- project path validation for missing and warning-level paths
- Local API dogfood/session/friction/next-action/log endpoints
- ApiClient dogfood, friction, project path validation, next-action, and log methods

MVP 2.4 adds targeted coverage for:
- shared Hosting/AppHost orchestration
- Local API lifecycle reuse/start/owned shutdown behavior
- Desktop project build
- UI/Dashboard builds after the folder picker seam
- ApiClient regression coverage

## Test Execution Rules For Codex

Codex must not run the full solution test suite by default.

Codex must not run `CliCommandTests` unless:
- `src/services/SourceWorker.Cli/Program.cs` changed
- CLI command parsing changed
- CLI command output changed
- CLI behavior changed

Do not run `CliCommandTests` for:
- AppHost-only changes
- Dashboard/UI-only changes
- ApiClient-only changes
- DTO/property-only changes
- docs-only changes
- CSS/layout changes
- AI provider UI changes unless CLI commands were added
- Local API endpoint changes unless CLI consumes that endpoint

Targeted examples:
- AI provider domain/application changes: run AI provider tests and affected Application tests.
- AI provider Local API endpoints: run LocalApi AI endpoint tests and ApiClient AI tests.
- AI dashboard UI: build `ui/SourceWorker.UI` and `ui/SourceWorker.Dashboard`; run ApiClient AI tests if client changed.
- Docs-only: no tests required.
- Shared contract changes: run affected ApiClient and LocalApi tests; full tests only if broad contracts break many projects.
- Final milestone: build solution, run targeted AI/ApiClient/LocalApi tests, and only run full `dotnet test SourceWorker.slnx --no-build` if shared architecture, storage, security middleware, or application base abstractions changed.

Every Codex summary must include:
- what changed
- which targeted tests were run
- why those tests were enough
- whether full tests were intentionally skipped
- whether `CliCommandTests` were skipped and why

## Latest Verification

These commands passed during MVP 2.4:

```powershell
dotnet build SourceWorker.slnx
dotnet build ui/SourceWorker.Desktop/SourceWorker.Desktop.csproj
dotnet test tests/SourceWorker.Tests.AppHost/SourceWorker.Tests.AppHost.csproj
dotnet test tests/SourceWorker.Tests.Application/SourceWorker.Tests.Application.csproj --filter ApiClient
dotnet test tests/SourceWorker.Tests.LocalApi/SourceWorker.Tests.LocalApi.csproj
dotnet build ui/SourceWorker.UI/SourceWorker.UI.csproj
dotnet build ui/SourceWorker.Dashboard/SourceWorker.Dashboard.csproj
.\tools\publish\Publish-Desktop-Windows.ps1
.\tools\publish\Create-DesktopShortcut.ps1
```

MVP 2.4 startup fix:
- Resolved MAUI crash in `App.CreateWindow` by ensuring `MainPage` is initialized first using `new Window(new MainPage())`.
- Removed obsolete `MainPage` property usage to comply with .NET 10 MAUI patterns.
- Verified zero warnings and zero errors in `SourceWorker.Desktop` build.

Published exe created:

```text
C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker\artifacts\desktop\win-x64\SourceWorker.Desktop.exe
```

Desktop shortcut created:

```text
C:\Users\louie\Desktop\SourceWorker.lnk
```

Full solution tests were intentionally not run for MVP 2.4 because the changed surface was Hosting/AppHost orchestration, Desktop, UI host seams, publish scripts, and docs. `CliCommandTests` were intentionally skipped because no CLI command parsing, output, or behavior changed.

## Next Recommended Step

MVP 2.4 establishes the Windows-first desktop app. The next strong step is a real desktop dogfood pass:
- Start `SourceWorker.Desktop.exe` from `artifacts/desktop/win-x64` or the Desktop shortcut.
- Register SourceWorker, Insights, Localite, and LwX.Flow.
- Scan all four.
- Open backend docs and packs.
- Configure Ollama or OpenAI.
- Preview `ai-handoff` and `backend-docs`.
- Run one preview-only AI request and one Quick Ask.
- Capture friction issues from confusing pages or errors.
- Export dogfood feedback and use it to prioritize MVP 2.5.
- Tighten the native folder picker, tray/minimize behavior, startup diagnostics, and Desktop settings from real use.
- Do not add patch application or automatic AI writes yet.
