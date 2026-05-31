# First MVP

SourceWorker is currently a local-first skeleton. It scans a folder, stores basic project/job/index state locally, and writes markdown context into `.ai-context/`.

## CLI

Run a scan and write context:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- scan C:\path\to\project
```

Print known jobs:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- status
```

Print the handoff file path after generating context:

```powershell
dotnet run --project src/services/SourceWorker.Cli -- handoff C:\path\to\project
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

It also runs the rule-based `backend-docs-compact` recipe and writes:

- `.ai-context/recipes/backend-docs-compact.yaml`
- `instructions/Result/Backend/README.md`
- `instructions/Result/Backend/01-project-structure.md`
- `instructions/Result/Backend/02-architecture.md`
- `instructions/Result/Backend/03-workers-and-services.md`
- `instructions/Result/Backend/04-configuration.md`
- `instructions/Result/Backend/05-tasks-and-risks.md`
