# SourceWorker Current Progress

Date: 2026-05-31

SourceWorker is still local-engine only. No AI provider, MAUI/Desktop UI, MCP, cloud sync, licensing, or terminal agent execution has been added.

## Implemented Features

- .NET 10 clean architecture solution with Domain, Application, Contracts, Infrastructure, Scanner, Context, Recipes, Worker, CLI, Local API, and tests.
- Scanner detects solution files, project files, project references, README/docs, config files, Docker files, GitHub Actions, test projects, folder roles, entrypoints, Git state, technologies, TODO/FIXME/REVIEW items, SHA256 hashes, and change states.
- Scan runs are persisted and preserved so latest scan data can be queried.
- Indexed files are persisted per scan run with absolute path, relative path, extension, file kind, size, last modified UTC, SHA256, ignored status, ignore reason, and change state.
- Generated artifacts are persisted with project id, scan run id, recipe id, relative/absolute path, type, content hash, generated UTC, source input hash, status, generator name, and notes.
- Markdown instruction/todo files can be imported with deterministic heading/list parsing.
- Imported instructions produce structured source paths, rules/mindset, todos, skip-later items, verification items, and extra sections.
- Source path validation marks imported paths as found/missing/outside project and file/folder/unknown.
- Rule-based recipes exist for `ai-handoff`, `backend-docs-compact`, `project-onboarding`, and `todo-plan`.
- Imported instruction files generate an `imported-*` recipe YAML file.
- Context freshness detects missing generated files, manually modified generated files, stale files, and computes a 0-100 freshness score.
- Generated markdown includes metadata comments with `GeneratedBy`, `FormatVersion`, `ProjectId`, `ScanRunId`, `RecipeId`, `GeneratedUtc`, `InputHash`, `ContentHash`, and `Status`.
- Recipe generation protects manual edits by default; `run-recipe --force` overwrites.

## Current Commands

- `scan <projectPath>`
- `scan <projectPath> --instructions <instructionFilePath>`
- `import-instructions <projectPath> <instructionFilePath>`
- `doctor <projectPath>`
- `doctor <projectPath> --json`
- `changes <projectPath>`
- `changes <projectPath> --json`
- `context-status <projectPath>`
- `recipes <projectPath>`
- `run-recipe <projectPath> <recipeId>`
- `run-recipe <projectPath> <recipeId> --force`
- `status`
- `handoff <projectPath>`

## Current Persistence State

- Localite is used for local persistence.
- Repositories exist for projects, scan runs, indexed files, file index data, generated artifacts, jobs, and imported instruction documents.
- Storage is still implemented on top of Localite key/value JSON collections rather than Localite ORM table classes. The application layer already depends on explicit repository interfaces, so the next persistence pass can move implementation internals to true table-backed Localite without changing CLI/API behavior.
- Secrets are not stored.

## Generated Files

`.ai-context/` includes:

- `CONTEXT.md`
- `index.yaml` with `formatVersion: 2`
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

`backend-docs-compact` writes:

- `instructions/Result/Backend/README.md`
- `01-project-structure.md`
- `02-architecture.md`
- `03-important-files-and-flows.md`
- `04-configuration-and-secrets.md`
- `05-tasks-and-risks.md`
- `06-verification-checklist.md`

## Local API

Minimal engine endpoints include:

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

## Current Tests

- Build: `dotnet build SourceWorker.slnx`
- Test: `dotnet test SourceWorker.slnx --no-build`
- Tests cover scanner behavior, instruction parsing, source path validation, recipe YAML generation, context writing, persistence of scan/file/artifact data, freshness/manual-edit detection, application commands, CLI smoke flow, and Local API engine endpoints.

## Latest Verification

These commands passed against this repository:

```powershell
dotnet build SourceWorker.slnx
dotnet test SourceWorker.slnx --no-build
dotnet run --project src/services/SourceWorker.Cli -- scan C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- doctor C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- changes C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
dotnet run --project src/services/SourceWorker.Cli -- context-status C:\Users\louie\source\lwxlabs\projects\lwxlabs\SourceWorker
```

## Next Recommended Step

Move the Localite repository internals from JSON collection storage to true Localite ORM table-backed storage for projects, scan runs, indexed files, detected technologies, TODOs, instructions, recipes, generated artifacts, jobs, and doctor reports. Keep the repository interfaces and CLI/API behavior stable while doing that migration.
