# SourceWorker Current Progress

Date: 2026-05-31

SourceWorker is a local-first project context engine. It intentionally does not include AI providers, MAUI/Desktop UI, MCP, cloud sync, licensing, or terminal agent execution.

## Architecture

- `SourceWorker.Domain` is dependency-free and holds the project, scan, instruction, recipe, job, artifact, freshness, cleanup, and storage diagnostic models.
- `SourceWorker.Application` owns use cases and interfaces. CLI and API talk to application requests only.
- `SourceWorker.Scanner` scans project shape, file hashes, technologies, Git state, TODOs, and change state.
- `SourceWorker.Context` writes `.ai-context`, parses imported markdown instructions, validates source paths, checks freshness, analyzes artifact lifecycle, and performs safe cleanup.
- `SourceWorker.Recipes` owns rule-based recipes including `backend-docs-compact`.
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
- Domain models and storage records are separate.
- Mapping helpers live in `SourceWorker.Storage.Localite`.
- Secrets are not stored.

## Generated Context

`.ai-context/` remains stable:

- `CONTEXT.md`
- `index.yaml`
- `project.md`
- `architecture.md`
- `current-state.md`
- `tasks.md`
- `ai-handoff.md`
- `instructions/imported-instructions.md` when imported
- `instructions/source-path-validation.md` when imported
- `recipes/*.yaml`
- `scans/latest-scan.md`
- `branches/current.md`

`index.yaml` is format version 3 and includes:

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

Generated markdown files include metadata comments with generator, format version, project id, scan run id, recipe id, generated UTC, input hash, content hash, and status.

## Backend Recipe Output

`backend-docs-compact` writes:

- `instructions/Result/Backend/README.md`
- `01-project-structure.md`
- `02-architecture.md`
- `03-important-files-and-flows.md`
- `04-configuration-and-secrets.md`
- `05-tasks-and-risks.md`
- `06-verification-checklist.md`

Manual edits are protected. `run-recipe` skips manually modified generated files unless `--force` is used.

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

## Latest Verification

These commands passed:

```powershell
dotnet build SourceWorker.slnx
dotnet test SourceWorker.slnx --no-build
dotnet run --project src/services/SourceWorker.Cli -- scan C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- doctor C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- context-status C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- artifacts C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- cleanup-preview C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- storage-info
```

Latest `storage-info` after verification showed table-backed Localite storage at `C:\Users\louie\AppData\Local\SourceWorker\sourceworker.localite.db`, schema version 1, with persisted project, scan run, indexed file, generated artifact, and instruction records.

## Next Recommended Step

Now that storage and artifact lifecycle are stable enough for engine work, the next best step is to improve scan precision and recipe usefulness before UI or AI:

- store detected technologies, Git snapshots, TODOs, recipes, and doctor reports with richer query APIs;
- add per-project storage isolation for tests and future multi-project use;
- improve `.gitignore` compatibility;
- add stronger stale detection based on source input hashes per artifact;
- add a read-only context browser endpoint for dashboard preparation.
