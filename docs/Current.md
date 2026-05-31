# SourceWorker Current Progress

Date: 2026-05-31

SourceWorker is a local-first project context engine.

MVP 1.9 Dashboard Write Actions:
- Project registration through the dashboard.
- Scan project button with real-time job progress polling.
- Export docs to configured external mirrors.
- Export packs to default `.ai-context/packs` location.
- Cleanup preview and safe cleanup with confirmation.
- Dashboard action safety model (safety levels).
- Reusable UI services: `ToastService`, `DashboardActionService`.
- Reusable UI components: `JobProgressPanel`, `ActionResultPanel`, `ConfirmActionDialog`, `DangerousActionWarning`.
- Targeted Verification Policy for faster development.

## Architecture

- `SourceWorker.Domain` is dependency-free and holds the project, scan, instruction, recipe, job, artifact, freshness, cleanup, storage diagnostic, output routing, export, context pack, and code intelligence models.
- `SourceWorker.Application` owns use cases and interfaces. CLI and API talk to application requests only.
- `SourceWorker.Scanner` scans project shape, file hashes, technologies, Git state, TODOs, change state, and lightweight C# code intelligence.
- `SourceWorker.Context` writes `.ai-context`, parses imported markdown instructions, validates source paths, routes outputs, writes context packs/export profiles, checks freshness, analyzes artifact lifecycle, performs safe cleanup, and exposes read-only context browsing.
- `SourceWorker.Recipes` owns rule-based recipes including `backend-docs-compact`, now using scan data plus code intelligence and publishing to internal docs plus external mirrors.
- `SourceWorker.Storage.Localite` now uses explicit table-backed SQLite storage under the Localite infrastructure boundary.
- `SourceWorker.Cli` is thin command routing.
- `SourceWorker.LocalApi` exposes minimal engine endpoints.
- `SourceWorker.ApiClient` is the UI boundary for Local API access. It has no Blazor dependency.
- `SourceWorker.UI` contains reusable Razor components/pages intended for both the web dashboard and future MAUI Blazor Hybrid host.
- `SourceWorker.Dashboard` is the first Blazor Web App host. It is read-only and does not reference storage, scanner, infrastructure, or application services.

## Commands

- `scan <projectPath>`
- `scan <projectPath> --instructions <instructionFilePath>`
- `import-instructions <projectPath> <instructionFilePath>`
- `doctor <projectPath>`
- `doctor <projectPath> --json`
- `changes <projectPath>`
- `changes <projectPath> --json`
- `context-status <projectPath>`
- `artifacts <projectPath>`
- `artifacts <projectPath> --json`
- `cleanup-preview <projectPath>`
- `cleanup-preview <projectPath> --json`
- `cleanup-generated <projectPath> --dry-run`
- `cleanup-generated <projectPath> --force`
- `storage-info`
- `storage-info --json`
- `symbols <projectPath>`
- `symbols <projectPath> --json`
- `flows <projectPath>`
- `flows <projectPath> --json`
- `routes <projectPath>`
- `routes <projectPath> --json`
- `context-files <projectPath>`
- `context-files <projectPath> --json`
- `read-context <projectPath> <relativePath>`
- `outputs <projectPath>`
- `outputs <projectPath> --json`
- `packs <projectPath>`
- `packs <projectPath> --json`
- `pack-show <projectPath> <packId>`
- `pack-show <projectPath> <packId> --json`
- `pack-export <projectPath> <packId> --out <outputFile>`
- `export-docs <projectPath> <exportProfileId>`
- `recipes <projectPath>`
- `run-recipe <projectPath> <recipeId>`
- `run-recipe <projectPath> <recipeId> --force`
- `status`
- `handoff <projectPath>`

## Dashboard

Projects:

- `ui/SourceWorker.ApiClient`
- `ui/SourceWorker.UI`
- `ui/SourceWorker.Dashboard`

Routes:

- `/`
- `/projects`
- `/projects/{projectId}`
- `/projects/{projectId}/context`
- `/projects/{projectId}/docs`
- `/projects/{projectId}/packs`
- `/projects/{projectId}/code`
- `/projects/{projectId}/artifacts`
- `/settings`

Dashboard behavior:

- shows Local API connection status (connected, unauthorized, disconnected).
- registers new projects via local path with validation.
- lists projects with advanced filtering (test, missing, search).
- shows project dashboard metrics, context freshness, and doctor summaries.
- tracks real-time job progress (polling) for scans and background tasks.
- exports documentation to configured external mirrors.
- exports context packs to default `.ai-context/packs` location.
- provides artifact lifecycle management (missing, modified, stale, orphaned).
- runs safe cleanup with mandatory preview and confirmation.
- enforces a safety-level model for all dashboard actions.
- browses generated context files and previews documentation.
- shows code intelligence: symbols, routes, flows, and test info.
- shows storage provider/path/schema in settings.
- shows **Running Mode** (Standalone / AppHost) and connection diagnostics in settings.

## Local App Host (One-Start)

SourceWorker 2.0 introduces the **AppHost**, a central orchestrator that simplifies the startup process:

```powershell
dotnet run --project src/services/SourceWorker.AppHost -- --open
```

Behavior:
- Starts **Local API** first and waits for `/health`.
- Automatically discovers the API token from `AppData`.
- Starts the **Dashboard** with `SOURCEWORKER_API_BASE_URL` and `SOURCEWORKER_API_TOKEN` environment variables.
- Detects and passes the `SOURCEWORKER_LAUNCH_MODE`.
- Optionally opens the browser to the dashboard.
- Cleanly stops all child processes on exit.

## Dashboard Action Safety Model

SourceWorker 1.9 introduces a safety model to protect against accidental data loss or unwanted file mutations.

Safety Levels:
- **ReadOnly**: Safe information retrieval (e.g., Get Health, Get Projects).
- **SafeWrite**: Low-risk registration or metadata changes (e.g., Register Project).
- **GeneratedFileWrite**: Writing to `.ai-context` or configured mirrors (e.g., Scan Project, Export Docs).
- **DestructiveGeneratedCleanup**: Deleting confidently identified orphaned/stale generated files (e.g., Cleanup Generated). Requires confirmation.
- **DangerousDisabled**: Actions that are currently restricted or require manual CLI execution (e.g., Force Cleanup).

Dashboard components use the `DashboardActionService` to evaluate if an action is allowed based on the project state and the required safety level.

Architecture rules currently enforced:

- UI uses `SourceWorker.ApiClient`.
- UI does not reference `SourceWorker.Storage.Localite`.
- UI does not reference `SourceWorker.Infrastructure`.
- UI does not reference `SourceWorker.Scanner`.
- UI does not read files directly.
- Dashboard write actions enabled (register, scan, export, cleanup).
- Safety level model enforces action permissions.

## Localite Table-Backed Storage

- Storage provider: Localite.
- Storage mode: table-backed.
- Schema version: 1.
- App storage format version: 3.
- Current database path on this machine: `C:\Users\louie\AppData\Local\SourceWorker\sourceworker.localite.db`.
- Explicit storage tables exist for project, scan run, indexed file, detected technology, Git snapshot, todo item, generated artifact, instruction document, instruction section, instruction source path, source path validation result, prompt recipe, worker job, worker job step, and doctor report records.
- Code intelligence storage also includes records for code symbols, code file/project summaries, code flows, flow steps, routes, configuration usage, service registrations, background workers, and tests.
- Domain models and storage records are separate.
- Mapping helpers live in `SourceWorker.Storage.Localite`.
- Secrets are not stored.

## Code Intelligence

MVP 1.5 uses deterministic text parsing for C# source files. It is intentionally lightweight and does not perform full compiler/Roslyn semantic analysis yet.

Detected items:

- classes, interfaces, records, structs, and enums
- public/internal/private/protected methods
- async methods
- constructors where found by the method scanner
- ASP.NET controller route attributes such as `[Route]`, `[HttpGet]`, and `[HttpPost]`
- minimal API mappings such as `MapGet`, `MapPost`, `MapPut`, `MapDelete`, and `MapPatch`
- dependency injection registrations such as `AddSingleton`, `AddScoped`, and `AddTransient`
- configuration reads such as `GetSection`, `GetValue`, and options binding
- background workers and hosted services
- test classes and test methods using common attributes such as `[Fact]`, `[Theory]`, and `[Test]`

The CLI can query this directly with `symbols`, `flows`, and `routes`. Storage diagnostics include counts for code symbols, flows, routes, service registrations, configuration usages, and tests.

## Flow Extraction

SourceWorker now creates rule-based `CodeFlow` records for high-confidence local engine workflows. Current flow types include:

- Scan project
- Run recipe
- Artifact cleanup
- Storage info
- Local API endpoint handling when routes are detected
- Worker/background job flow when background workers are detected

Flows contain trigger text, involved files, involved symbols, ordered steps, confidence, and warnings. Mermaid flowcharts are generated for high-confidence flows and embedded in backend documentation.

The extractor is conservative: it uses detected classes/methods/files and should not invent method names.

## Generated Context

`.ai-context/` now separates inputs, generated docs, packs, exports, and diagrams:

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
- `docs/backend/README.md`
- `docs/backend/01-project-structure.md`
- `docs/backend/02-architecture.md`
- `docs/backend/03-important-files-and-flows.md`
- `docs/backend/04-configuration-and-secrets.md`
- `docs/backend/05-tasks-and-risks.md`
- `docs/backend/06-verification-checklist.md`
- `packs/ai-handoff.pack.yaml`
- `packs/backend-docs.pack.yaml`
- `packs/onboarding.pack.yaml`
- `packs/current-branch.pack.yaml`
- `exports/backend-docs-compact.yaml`
- `diagrams/*.mmd`
- `recipes/*.yaml`
- `scans/latest-scan.md`
- `branches/current.md`

Folder responsibilities:

- `.ai-context/instructions/` is for imported/user-authored instructions and validation.
- `.ai-context/docs/` is for canonical internal generated documentation.
- `.ai-context/packs/` is for AI-ready context bundle manifests and markdown exports.
- `.ai-context/exports/` is for export profiles/history.
- `instructions/Result/Backend/` is an external Markdown mirror/export target.

`index.yaml` is format version 5 and includes:

- project identity
- latest scan
- Git state
- storage provider/mode/schema version
- context files
- recipes
- imported instruction state
- stale status
- artifact lifecycle counts
- cleanup recommendation and preview command
- code intelligence counts
- detected flow confidence counts
- context browser file/search availability
- output routes
- context packs
- export profiles
- internal docs state

Generated markdown files include metadata comments with generator, format version, project id, scan run id, recipe id, generated UTC, input hash, content hash, and status.

## Backend Recipe Output

`backend-docs-compact` writes:

- `.ai-context/docs/backend/README.md`
- `.ai-context/docs/backend/01-project-structure.md`
- `.ai-context/docs/backend/02-architecture.md`
- `.ai-context/docs/backend/03-important-files-and-flows.md`
- `.ai-context/docs/backend/04-configuration-and-secrets.md`
- `.ai-context/docs/backend/05-tasks-and-risks.md`
- `.ai-context/docs/backend/06-verification-checklist.md`
- `instructions/Result/Backend/README.md`
- `01-project-structure.md`
- `02-architecture.md`
- `03-important-files-and-flows.md`
- `04-configuration-and-secrets.md`
- `05-tasks-and-risks.md`
- `06-verification-checklist.md`
- `.ai-context/exports/backend-docs-compact.yaml`
- `.ai-context/packs/backend-docs.pack.yaml`

Manual edits are protected. `run-recipe` skips manually modified generated files unless `--force` is used.

Backend docs now include detected routes, service registrations, configuration usage, background workers, tests, code flows, and Mermaid diagrams. `.ai-context/docs/backend/` is canonical. `instructions/Result/Backend/` is a mirrored external export for human-readable docs.

## Output Routing And Packs

Built-in output profile:

- `backend-docs-compact`
- primary destination: `.ai-context/docs/backend`
- external destination: `instructions/Result/Backend`
- publish mode: mirror

Built-in context packs:

- `ai-handoff`
- `backend-docs`
- `onboarding`
- `current-branch`

`pack-export` creates a single markdown bundle by concatenating the pack files with clear source separators. `export-docs` mirrors the configured internal docs to the configured external output path only; it does not accept arbitrary export paths.

## Artifact Lifecycle

SourceWorker detects:

- `Missing`: tracked generated file no longer exists.
- `ManuallyModified`: file exists but body hash differs from stored generated hash.
- `Stale`: tracked artifact marked stale.
- `Orphaned`: generated markdown exists but is not expected by the current recipe/index shape.
- `Deprecated`: known older backend recipe outputs such as previous worker/configuration filenames.

Cleanup behavior:

- `cleanup-preview` never deletes.
- `cleanup-generated --dry-run` reports what would be deleted.
- `cleanup-generated --force` deletes only confidently generated orphan/deprecated files.
- Manually modified and unknown user files are not deleted by default.

## Local API

- `GET /health` (Public)
- `GET /api/version` (Public)
- `GET /api/runtime/info` (Public)
- `GET /api/runtime/security` (Protected)
- `POST /api/runtime/token/rotate` (Protected)
- `GET /api/projects` (Protected)
  - `includeTestProjects=false`
  - `includeMissing=false`
  - `search=`
  - `limit=50`
- `POST /api/projects` (Protected)
- `POST /api/projects/{id}/scan` (Protected)
- `GET /api/jobs` (Protected)
- `GET /api/projects/{id}/jobs` (Protected)
- `GET /api/jobs/{id}` (Protected)
- `GET /api/jobs/{id}/logs` (Protected)
- `GET /api/projects/{id}/doctor` (Protected)
- `GET /api/projects/{id}/changes` (Protected)
- `GET /api/projects/{id}/context-status` (Protected)
- `GET /api/projects/{id}/context-files` (Protected)
- `GET /api/projects/{id}/scan-runs` (Protected)
- `GET /api/projects/{id}/scan-runs/latest` (Protected)
- `GET /api/storage/info` (Protected)
- `GET /api/projects/{id}/artifacts` (Protected)
- `GET /api/projects/{id}/cleanup-preview` (Protected)
- `POST /api/projects/{id}/cleanup-generated` (Protected)
- `GET /api/projects/{id}/symbols` (Protected)
- `GET /api/projects/{id}/flows` (Protected)
- `GET /api/projects/{id}/routes` (Protected)
- `GET /api/projects/{id}/service-registrations` (Protected)
- `GET /api/projects/{id}/configuration-usages` (Protected)
- `GET /api/projects/{id}/tests` (Protected)
- `GET /api/projects/{id}/context-browser` (Protected)
- `GET /api/projects/{id}/context-browser/files` (Protected)
- `GET /api/projects/{id}/context-browser/files/{*relativePath}` (Protected)
- `GET /api/projects/{id}/context-browser/search?query=...` (Protected)
- `GET /api/projects/{id}/outputs` (Protected)
- `GET /api/projects/{id}/packs` (Protected)
- `GET /api/projects/{id}/packs/{packId}` (Protected)
- `GET /api/projects/{id}/packs/{packId}/content` (Protected)
- `POST /api/projects/{id}/packs/{packId}/export-default` (Protected)
- `POST /api/projects/{id}/exports/{exportProfileId}/run` (Protected)

Protected endpoints require `Authorization: Bearer <token>` when token auth is enabled.

## Tests

Current tests cover:
- ... (existing coverage)
- Local API security (public vs protected, token validation)
- CORS origin validation
- Project filtering, search, and registration logic
- Job management: starting scans, tracking progress, and log retrieval
- Export flows: documentation mirrors and default pack exports
- Artifact cleanup: preview generation and safe deletion logic
- ApiClient token provider and connection diagnostics

## Verification Policy

To ensure stability while maintaining development speed, follow this targeted verification policy:

- **AppHost/Process orchestration changes**: Run AppHost tests and ApiClient connection tests.
- **Small UI-only changes**: Run build for affected UI projects and relevant UI/ApiClient tests only.
- **DTO/Property-only changes**: Run affected ApiClient, LocalApi, and Application tests.
- **Scanner changes**: Run scanner tests and only related application/CLI tests.
- **Local API endpoint changes**: Run LocalApi tests and ApiClient tests.
- **Storage/Schema changes**: Run storage, persistence, freshness, and affected API tests.
- **Security/Auth/CORS changes**: Run LocalApi security tests and ApiClient connection tests.
- **CLI command changes**: Run CLI command tests only when CLI Program.cs or CLI command behavior changed. Do not run CliCommandTests for AppHost-only, UI-only, docs-only, or DTO-only changes.
- **Docs-only changes**: No test run required unless docs generation code changed.
- **Major changes / Final verification**: Run full `dotnet test SourceWorker.slnx --no-build`.

## Latest Verification

These commands passed:

```powershell
dotnet build SourceWorker.slnx
dotnet run --project src/services/SourceWorker.AppHost -- --open
```

## Next Recommended Step

MVP 2.0 established the local host foundation. The next step is MVP 2.1:
- MAUI Blazor Hybrid host skeleton in `ui/SourceWorker.Desktop`.
- AI provider integration foundation (Local Models / Ollama / OpenAI).
- Context pack refinement for LLM consumption.
