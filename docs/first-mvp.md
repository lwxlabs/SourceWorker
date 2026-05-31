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
