# SourceWorker Current Progress

Date: 2026-05-31

SourceWorker is a local-first project context engine with a protected Local API, one-start AppHost, interactive Blazor dashboard, context packs, output routing, generated docs, artifact lifecycle, project scanning, code intelligence, dashboard write actions, MVP 2.1 safe AI provider preview, and MVP 2.2 dogfood readiness hardening.

Still intentionally not included:
- MCP
- cloud sync
- licensing
- terminal agent execution
- autonomous code editing
- automatic AI file writes
- full MAUI desktop host

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
- `SourceWorker.AppHost` starts Local API and Dashboard together with token/base URL discovery.

## MVP 2.2 Dogfood Readiness

MVP 2.2 focuses on making SourceWorker pleasant enough to start using daily on real repositories such as SourceWorker itself, Insights, Localite, and LwX.Flow.

New read-only dogfood/onboarding surfaces:
- `/dogfood`
- `/projects/onboarding`
- `/projects/{projectId}/onboarding`

Dogfood readiness checks:
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

The dashboard home now shows a next-action dogfood card. Project dashboard now links directly to AI preview, backend docs, packs, and onboarding.

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
- `/projects/{projectId}/context`
- `/projects/{projectId}/docs`
- `/projects/{projectId}/packs`
- `/projects/{projectId}/code`
- `/projects/{projectId}/artifacts`
- `/projects/{projectId}/ai`
- `/projects/{projectId}/ai/runs`
- `/dogfood`
- `/projects/onboarding`
- `/projects/{projectId}/onboarding`
- `/jobs`
- `/settings`
- `/settings/ai`
- `/settings/diagnostics`

Dashboard behavior:
- register projects
- scan projects with job progress polling
- export docs and context packs
- preview and run safe cleanup
- browse generated context files
- view backend docs, packs, artifacts, code symbols, routes, flows, jobs, storage, runtime/security info
- configure AI providers
- build AI prompt previews from packs
- run preview-only AI requests
- view AI run history
- validate provider setup
- diagnose provider connection issues
- inspect AI usage and pack readiness
- view secret-safe diagnostics

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

## Generated Context

`.ai-context/` structure:

- `CONTEXT.md`
- `index.yaml`
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

`index.yaml` is format version 5 and includes project, latest scan, Git, storage, context files, recipes, imported instructions, stale status, artifact lifecycle, cleanup, code intelligence, flow confidence, context browser, outputs, packs, exports, and docs state.

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

All protected endpoints require `Authorization: Bearer <token>` when token auth is enabled.

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

These commands passed during MVP 2.2:

```powershell
dotnet build SourceWorker.slnx
dotnet test tests/SourceWorker.Tests.Application/SourceWorker.Tests.Application.csproj --filter Ai
dotnet test tests/SourceWorker.Tests.Application/SourceWorker.Tests.Application.csproj --filter ApiClient
dotnet test tests/SourceWorker.Tests.LocalApi/SourceWorker.Tests.LocalApi.csproj --filter Ai
dotnet build ui/SourceWorker.UI/SourceWorker.UI.csproj
dotnet build ui/SourceWorker.Dashboard/SourceWorker.Dashboard.csproj
```

Full solution tests were intentionally not run for MVP 2.2 because the changed surface was AI/application, Local API AI endpoints, ApiClient AI methods, diagnostics/dogfood endpoints, and dashboard AI/dogfood pages. `CliCommandTests` were intentionally skipped because no CLI command parsing, output, or behavior changed.

## Next Recommended Step

MVP 2.2 establishes dogfood readiness and a more practical AI preview loop. The next strong step is a real dogfood pass:
- Run AppHost.
- Register and scan SourceWorker, Insights, Localite, and LwX.Flow.
- Use `backend-docs` and `ai-handoff` packs on each repo.
- Capture friction in onboarding, diagnostics, and provider setup.
- Improve provider failure messages and pack quality scoring from real failures.
- Do not add patch application or automatic AI writes yet.
