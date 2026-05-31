# SourceWorker Current Progress

Date: 2026-05-31

SourceWorker is a local-first project context engine. It intentionally does not include AI providers, MAUI/Desktop UI, MCP, cloud sync, licensing, or terminal agent execution.

MVP 1.6 adds output routing and context packs. SourceWorker now separates imported/user-authored instructions from generated internal docs, AI-ready packs, export profiles, and external Markdown exports.

## Architecture

- `SourceWorker.Domain` is dependency-free and holds the project, scan, instruction, recipe, job, artifact, freshness, cleanup, storage diagnostic, output routing, export, context pack, and code intelligence models.
- `SourceWorker.Application` owns use cases and interfaces. CLI and API talk to application requests only.
- `SourceWorker.Scanner` scans project shape, file hashes, technologies, Git state, TODOs, change state, and lightweight C# code intelligence.
- `SourceWorker.Context` writes `.ai-context`, parses imported markdown instructions, validates source paths, routes outputs, writes context packs/export profiles, checks freshness, analyzes artifact lifecycle, performs safe cleanup, and exposes read-only context browsing.
- `SourceWorker.Recipes` owns rule-based recipes including `backend-docs-compact`, now using scan data plus code intelligence and publishing to internal docs plus external mirrors.
- `SourceWorker.Storage.Localite` now uses explicit table-backed SQLite storage under the Localite infrastructure boundary.
- `SourceWorker.Cli` is thin command routing.
- `SourceWorker.LocalApi` exposes minimal engine endpoints.

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

- `GET /health`
- `GET /api/projects`
- `POST /api/projects`
- `POST /api/projects/{id}/scan`
- `GET /api/jobs/{id}`
- `GET /api/projects/{id}/doctor`
- `GET /api/projects/{id}/changes`
- `GET /api/projects/{id}/context-status`
- `GET /api/projects/{id}/context-files`
- `GET /api/projects/{id}/scan-runs`
- `GET /api/projects/{id}/scan-runs/latest`
- `GET /api/storage/info`
- `GET /api/projects/{id}/artifacts`
- `GET /api/projects/{id}/cleanup-preview`
- `POST /api/projects/{id}/cleanup-generated`
- `GET /api/projects/{id}/symbols`
- `GET /api/projects/{id}/flows`
- `GET /api/projects/{id}/routes`
- `GET /api/projects/{id}/service-registrations`
- `GET /api/projects/{id}/configuration-usages`
- `GET /api/projects/{id}/tests`
- `GET /api/projects/{id}/context-browser`
- `GET /api/projects/{id}/context-browser/files`
- `GET /api/projects/{id}/context-browser/files/{*relativePath}`
- `GET /api/projects/{id}/context-browser/search?query=...`
- `GET /api/projects/{id}/outputs`
- `GET /api/projects/{id}/packs`
- `GET /api/projects/{id}/packs/{packId}`
- `GET /api/projects/{id}/packs/{packId}/content`
- `POST /api/projects/{id}/exports/{exportProfileId}/run`

Context browser endpoints are read-only. They only read known generated context/doc files under `.ai-context` and `instructions/Result/Backend`; they do not expose arbitrary filesystem reads.

## Tests

Current tests cover:

- scanner detection and ignore behavior
- instruction parser behavior
- source path validation
- recipe YAML generation
- context writer output
- application command flows
- CLI smoke flow
- Local API engine endpoints
- table-backed storage info
- persisted scan/index/artifact records
- artifact lifecycle and cleanup dry-run/force behavior
- manual edit freshness detection
- lightweight C# code intelligence detection
- rule-based flow extraction
- Mermaid generation for high-confidence flows
- code intelligence persistence
- CLI symbols/flows/routes/context browser commands
- Local API code intelligence and context browser endpoints
- output routing and context pack commands
- Local API output/pack/export endpoints
- internal docs plus external docs mirror

## Latest Verification

These commands passed:

```powershell
dotnet build SourceWorker.slnx
dotnet test SourceWorker.slnx --no-build
dotnet run --project src/services/SourceWorker.Cli -- scan C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- symbols C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- flows C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- routes C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- context-files C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- read-context C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker .ai-context/ai-handoff.md
dotnet run --project src/services/SourceWorker.Cli -- outputs C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- packs C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- pack-show C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker backend-docs
dotnet run --project src/services/SourceWorker.Cli -- pack-export C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker backend-docs --out C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker\.ai-context\packs\backend-docs.md
dotnet run --project src/services/SourceWorker.Cli -- export-docs C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker backend-docs-compact
dotnet run --project src/services/SourceWorker.Cli -- doctor C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- context-status C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- artifacts C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- cleanup-preview C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- storage-info
```

Latest `storage-info` after verification showed table-backed Localite storage at `C:\Users\louie\AppData\Local\SourceWorker\sourceworker.localite.db`, schema version 1, with persisted project, scan run, indexed file, generated artifact, and instruction records.

## Next Recommended Step

Now that storage, artifact lifecycle, code intelligence, output routing, and context packs are stable enough for product work, the next best step is either a simple read-only dashboard or a safe AI provider preview. If continuing engine-first, improve precision before either:

- add per-project storage isolation for tests and future multi-project use;
- improve `.gitignore` compatibility and C# parsing accuracy;
- add Roslyn-backed symbol extraction behind the existing interfaces if deterministic regex parsing becomes too shallow;
- add richer query/filter APIs for symbols, routes, flows, artifacts, TODOs, and generated context files;
- add stronger stale detection based on source input hashes per artifact and per recipe;
- add pack token estimation and pack freshness scoring;
- then build either a minimal dashboard over the read-only context browser endpoints or an AI provider preview that consumes structured scan/code intelligence data and context packs instead of raw folders.
