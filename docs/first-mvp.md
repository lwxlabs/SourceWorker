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

The API uses an in-process queue for scan jobs. It does not expose AI, MCP, cloud, licensing, or desktop UI features yet.

## Generated Context

`index.yaml` uses format version 2 and includes project identity, latest scan, Git state, context files, recipes, imported instructions, and stale status.

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
- `.ai-context/instructions/imported-instructions.md` when an instruction file has been imported
- `.ai-context/instructions/source-path-validation.md` when an instruction file has been imported

It also runs the rule-based `backend-docs-compact` recipe and writes:

- `.ai-context/recipes/backend-docs-compact.yaml`
- `.ai-context/recipes/ai-handoff.yaml`
- `.ai-context/recipes/project-onboarding.yaml`
- `.ai-context/recipes/todo-plan.yaml`
- `.ai-context/recipes/imported-*.yaml` when an instruction file has been imported
- `instructions/Result/Backend/README.md`
- `instructions/Result/Backend/01-project-structure.md`
- `instructions/Result/Backend/02-architecture.md`
- `instructions/Result/Backend/03-important-files-and-flows.md`
- `instructions/Result/Backend/04-configuration-and-secrets.md`
- `instructions/Result/Backend/05-tasks-and-risks.md`
- `instructions/Result/Backend/06-verification-checklist.md`
