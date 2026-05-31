# First MVP

SourceWorker is currently a local-first skeleton. It scans a folder, stores basic project/job/index state locally, and writes markdown context into `.ai-context/`.

## CLI

Run a scan and write context:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- scan C:\path\to\project
```

Run a scan and import a markdown instruction/todo file in one pass:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- scan C:\path\to\project --instructions C:\path\to\todo-louie.md
```

Import instructions without rescanning:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- import-instructions C:\path\to\project C:\path\to\todo-louie.md
```

Print known jobs:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- status
```

Print the handoff file path after generating context:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- handoff C:\path\to\project
```

Check project/context health:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- doctor C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- doctor C:\path\to\project --json
```

Inspect scan file changes and context freshness:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- changes C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- changes C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- context-status C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- artifacts C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- cleanup-preview C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- cleanup-generated C:\path\to\project --dry-run
dotnet run --project src/services/SourceWorker.Cli -- storage-info --json
```

Inspect code intelligence and generated context:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- symbols C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- symbols C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- flows C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- flows C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- routes C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- routes C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- context-files C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- context-files C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- read-context C:\path\to\project .ai-context\ai-handoff.md
```

Inspect outputs and context packs:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- outputs C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- outputs C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- packs C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- packs C:\path\to\project --json
dotnet run --project src/services/SourceWorker.Cli -- pack-show C:\path\to\project backend-docs
dotnet run --project src/services/SourceWorker.Cli -- pack-show C:\path\to\project backend-docs --json
dotnet run --project src/services/SourceWorker.Cli -- pack-export C:\path\to\project backend-docs --out C:\path\to\project\.ai-context\packs\backend-docs.md
dotnet run --project src/services/SourceWorker.Cli -- export-docs C:\path\to\project backend-docs-compact
```

List and run local rule-based recipes:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- recipes C:\path\to\project
dotnet run --project src/services/SourceWorker.Cli -- run-recipe C:\path\to\project backend-docs-compact
dotnet run --project src/services/SourceWorker.Cli -- run-recipe C:\path\to\project backend-docs-compact --force
```

## Local API

Start the API:

```powershell
dotnet run --project src/services/SourceWorker.LocalApi
```

Minimal endpoints:

- `GET /health`
- `GET /api/projects`
- `POST /api/projects` with `{ "path": "C:\\path\\to\\project", "name": null }`
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

The API uses an in-process queue for scan jobs. It does not expose AI, MCP, cloud, licensing, or desktop UI features yet.

## Dashboard

Start the read-only Blazor dashboard:

```powershell
dotnet run --project src/services/SourceWorker.LocalApi
dotnet run --project ui/SourceWorker.Dashboard
```

Projects:

- `ui/SourceWorker.ApiClient`: typed Local API client.
- `ui/SourceWorker.UI`: reusable Razor components/pages.
- `ui/SourceWorker.Dashboard`: first Blazor Web App host.

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

The UI is read-only and Local API-driven. It does not access Localite, scanner, infrastructure, application services, or project files directly.

## Generated Context

`index.yaml` uses format version 5 and includes project identity, latest scan, Git state, table-backed Localite storage metadata, context files, recipes, imported instructions, stale status, artifact lifecycle counts, cleanup recommendations, code intelligence counts, flow counts, context browser availability, output routes, packs, exports, and internal docs state.

The scan writes:

- `.ai-context/CONTEXT.md`
- `.ai-context/index.yaml`
- `.ai-context/project.md`
- `.ai-context/architecture.md`
- `.ai-context/current-state.md`
- `.ai-context/tasks.md`
- `.ai-context/ai-handoff.md`
- `.ai-context/scans/latest-scan.md`
- `.ai-context/branches/current.md`
- `.ai-context/instructions/README.md`
- `.ai-context/instructions/imported/imported-instructions.md` when an instruction file has been imported
- `.ai-context/instructions/imported/source-path-validation.md` when an instruction file has been imported

It also runs the rule-based `backend-docs-compact` recipe and writes:

- `.ai-context/docs/backend/README.md`
- `.ai-context/docs/backend/01-project-structure.md`
- `.ai-context/docs/backend/02-architecture.md`
- `.ai-context/docs/backend/03-important-files-and-flows.md`
- `.ai-context/docs/backend/04-configuration-and-secrets.md`
- `.ai-context/docs/backend/05-tasks-and-risks.md`
- `.ai-context/docs/backend/06-verification-checklist.md`
- `.ai-context/recipes/backend-docs-compact.yaml`
- `.ai-context/recipes/ai-handoff.yaml`
- `.ai-context/recipes/project-onboarding.yaml`
- `.ai-context/recipes/todo-plan.yaml`
- `.ai-context/recipes/imported-*.yaml` when an instruction file has been imported
- `.ai-context/packs/ai-handoff.pack.yaml`
- `.ai-context/packs/backend-docs.pack.yaml`
- `.ai-context/packs/onboarding.pack.yaml`
- `.ai-context/packs/current-branch.pack.yaml`
- `.ai-context/exports/backend-docs-compact.yaml`
- `.ai-context/diagrams/*.mmd`
- `instructions/Result/Backend/README.md`
- `instructions/Result/Backend/01-project-structure.md`
- `instructions/Result/Backend/02-architecture.md`
- `instructions/Result/Backend/03-important-files-and-flows.md`
- `instructions/Result/Backend/04-configuration-and-secrets.md`
- `instructions/Result/Backend/05-tasks-and-risks.md`
- `instructions/Result/Backend/06-verification-checklist.md`

`backend-docs-compact` uses scan data, imported instructions, detected routes, service registrations, configuration usage, tests, background workers, and rule-based flows. Flow diagrams are generated as simple Mermaid flowcharts for high-confidence flows.

`.ai-context/docs/backend/` is the canonical generated documentation location. `instructions/Result/Backend/` is an external Markdown mirror/export target.
