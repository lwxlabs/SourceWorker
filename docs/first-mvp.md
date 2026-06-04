# First MVP

SourceWorker is a local-first project understanding and AI context handoff app. It scans a folder, stores project/job/index state locally, writes `.ai-context/`, and produces a clean Agent Handoff that can be copied into another AI coding assistant.

SourceWorker prepares context; it does not code by itself. It does not apply patches, edit source files from AI output, run terminal agent tasks, or replace Codex/Junie/ChatGPT/Cursor/Claude Code.

## Product UX Direction

The daily product loop is:

```text
Pick repo -> scan -> understand -> copy handoff for agent
```

Normal users should start with Agent Handoff. Technical surfaces such as artifacts, jobs, diagnostics, logs, dogfood sessions, raw recipes, exports, and generated metadata belong under Advanced.

Global navigation is limited to Home, Projects, Settings, and Advanced. Handoff, Ask, Docs, Packs, Scan Health, Code, Artifacts, Jobs, and Context Browser are project workspace tabs that appear only after selecting a project.

## Agent Handoff Mode

The primary generated output is:

```text
.ai-context/agent-handoff.md
```

This file is clean, user-facing markdown. It includes project identity, purpose, current state, architecture, important files, AI working rules, TODO/risk summary, high-confidence flows, available context packs, and a recommended next agent prompt.

The handoff should summarize source reality, not generated internals:

- prefer README/project/symbol/test evidence for purpose
- exclude TODOs from `.ai-context/**` and `instructions/Result/**`
- exclude SourceWorker product flows when scanning other projects
- infer candidate domain flows for library projects

Generated lifecycle metadata is no longer repeated as a heavy block in every markdown file. Full metadata lives in `.ai-context/index.yaml` and Localite artifact records; user-facing markdown uses either no marker or a compact SourceWorker marker.

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

## Local App Host (One-Start)

Starting with MVP 2.0, use the AppHost to start everything:

```powershell
dotnet run --project src/services/SourceWorker.AppHost -- --open
```

## Desktop App

MVP 2.4 adds the Windows-first desktop host:

```powershell
dotnet run --project ui/SourceWorker.Desktop
```

Published exe:

```powershell
.\artifacts\desktop\win-x64\SourceWorker.Desktop.exe
```

Publish:

```powershell
.\tools\publish\Publish-Desktop-Windows.ps1
```

Create shortcut:

```powershell
.\tools\publish\Create-DesktopShortcut.ps1
```

`SourceWorker.Desktop` is the normal user-facing app. It reuses `SourceWorker.UI`, starts `SourceWorker.LocalApi` internally, waits for `/health`, reads the token, configures `SourceWorker.ApiClient`, and shows startup progress/errors inside the desktop window. AppHost remains a development/browser fallback.

## Local API

Start the API:

```powershell
dotnet run --project src/services/SourceWorker.LocalApi
```

Minimal endpoints:

- `GET /health` (Public)
- `GET /api/version` (Public)
- `GET /api/runtime/info` (Public)
- `GET /api/runtime/security` (Protected)
- `POST /api/runtime/token/rotate` (Protected)
- `GET /api/projects` (Protected)
- `POST /api/projects` with `{ "path": "C:\\path\\to\\project", "name": null }`
- `POST /api/projects/{id}/scan`
- `GET /api/jobs`
- `GET /api/jobs/{id}`
- `GET /api/jobs/{id}/logs`
- `GET /api/projects/{id}/jobs`
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

MVP 2.1 adds protected AI preview endpoints:

- `GET /api/ai/providers`
- `GET /api/ai/providers/status`
- `POST /api/ai/providers/configure`
- `POST /api/ai/providers/validate`
- `POST /api/ai/providers/{providerId}/test`
- `POST /api/ai/providers/{providerId}/diagnose`
- `GET /api/ai/settings`
- `PUT /api/ai/settings`
- `GET /api/ai/usage`
- `GET /api/projects/{id}/ai/packs`
- `GET /api/projects/{id}/ai/packs/{packId}/quality`
- `POST /api/projects/{id}/ai/prompt-preview`
- `POST /api/projects/{id}/ai/runs`
- `GET /api/projects/{id}/ai/runs`
- `GET /api/projects/{id}/ai/runs/{runId}`
- `DELETE /api/projects/{id}/ai/runs`
- `DELETE /api/projects/{id}/ai/runs/{runId}`
- `GET /api/projects/{id}/ai/usage`
- `GET /api/dogfood/status`
- `GET /api/projects/{id}/dogfood/status`
- `GET /api/diagnostics/summary`
- `POST /api/diagnostics/export`
- `POST /api/projects/validate-path`
- `GET /api/projects/recent-paths`
- `GET /api/projects/{id}/ai/settings`
- `PUT /api/projects/{id}/ai/settings`
- `POST /api/projects/{id}/ai/quick-ask/preview`
- `POST /api/projects/{id}/ai/quick-ask/run`
- `GET /api/projects/{id}/ai/explanation/options`
- `POST /api/projects/{id}/ai/explanation/preview`
- `POST /api/projects/{id}/ai/explanation/run`
- `GET /api/projects/{id}/ai/explanation/runs`
- `GET /api/projects/{id}/ai/explanation/runs/{runId}`
- `POST /api/projects/{id}/ai/explanation/runs/{runId}/save`
- `DELETE /api/projects/{id}/ai/explanation/runs/{runId}`
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

AI endpoints are preview-only. They do not write files. API keys are masked in responses and are not written to `.ai-context`.

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
- `ui/SourceWorker.Desktop`: Windows-first MAUI Blazor Hybrid host.

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
- `/settings/ai`
- `/projects/{projectId}/ai`
- `/projects/{projectId}/ai/runs`
- `/dogfood`
- `/dogfood/sessions`
- `/dogfood/sessions/new`
- `/dogfood/sessions/{sessionId}`
- `/dogfood/issues`
- `/projects/onboarding`
- `/projects/{projectId}/onboarding`
- `/settings/diagnostics`
- `/settings/logs`

Desktop-only shell pieces:

- startup progress and startup error page
- Local API lifecycle state
- bottom desktop status bar
- single-instance mutex
- native project path browse seam
- in-app notification abstraction

The UI is interactive for MVP 1.9. It supports project registration, scanning with job progress, doc/pack exports, and safe cleanup with confirmation. It is Local API-driven and does not access Localite, scanner, infrastructure, application services, or project files directly.

MVP 2.1 adds AI preview UI. Users can configure OpenAI, Ollama, or OpenAI-compatible providers, choose a context pack, preview the final prompt, see safety warnings and rough token/cost estimates, run an AI request, and view/copy the output. AI results are preview-only and are not applied to files.

MVP 2.2 adds dogfood readiness and AI hardening for daily use: provider validation, provider diagnostics, model catalog/pricing metadata, prompt sections, included-file token estimates, pack quality scoring, AI usage summaries, diagnostics summary/export preview, and onboarding/dogfood dashboard routes.

MVP 2.3 adds the first practical dogfood feedback loop: dogfood sessions, friction issue capture/export, next recommended actions, project path validation, project-level AI defaults, Quick Ask on the project dashboard, and a lightweight local logs page. The UI remains Local API-driven and AI output remains preview-only.

MVP 2.8 adds the AI Explanation page:

- route: `/projects/{projectId}/explanation`
- output: `.ai-context/docs/backend/07-ai-explanation.md`
- preview builds prompt, included file list, warnings, and token estimate only
- run calls the configured AI provider and stores an explanation run
- save writes the generated docs file only after explicit user confirmation
- existing manually modified explanation files require overwrite confirmation
- source files are never written from AI output

MVP 2.9 adds Markdown Workspace and Provider Management:

- route: `/projects/{projectId}/markdown`
- real Markdown editor using `PSC.Blazor.Components.MarkdownEditor`
- Mermaid rendering and syntax highlighting
- safe Local API-backed markdown listing, reading, validation, and saving
- edits are limited to `.md` and `.markdown` files inside the selected project root
- source code, YAML/JSON, `.env`, database files, `.git`, `bin`, `obj`, `.vs`, and `.idea` are blocked
- no autosave; the user explicitly clicks Save
- Explanation output can be edited before saving `07-ai-explanation.md`
- global `/providers` page for OpenAI, Gemini, Ollama, LM Studio, and OpenAI-compatible presets
- provider secrets stay in local AppData secret storage and are returned only as masked metadata

## AI Provider Preview

- AI core logic lives in `src/libs/SourceWorker.AI`.
- Provider and secret infrastructure lives in `src/infrastructure/SourceWorker.AI.Providers`.
- OpenAI uses the Responses API first with `store=false`, with chat completions fallback where needed.
- Gemini uses `models/{model}:generateContent` with `x-goog-api-key`.
- OpenAI-compatible providers support custom endpoints and the LM Studio preset at `http://localhost:1234/v1`.
- Ollama uses local `/api/tags` and `/api/chat` with default base URL `http://localhost:11434/api`.
- Token estimate is deterministic: characters divided by 4, rounded up.
- Safety checks detect obvious API keys, token values, private keys, connection strings, missing pack files, stale context, and manually modified generated files.
- Local secret storage is under AppData for MVP use. OS secret store integration is planned later.

## Generated Context

`index.yaml` uses format version 5 and includes project identity, latest scan, Git state, table-backed Localite storage metadata, context files, recipes, imported instructions, stale status, artifact lifecycle counts, cleanup recommendations, code intelligence counts, flow counts, context browser availability, output routes, packs, exports, and internal docs state.

The scan writes:

- `.ai-context/CONTEXT.md`
- `.ai-context/index.yaml`
- `.ai-context/agent-handoff.md`
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
- `.ai-context/docs/backend/07-ai-explanation.md` when explicitly generated and saved
- `.ai-context/recipes/backend-docs-compact.yaml`
- `.ai-context/recipes/ai-handoff.yaml`
- `.ai-context/recipes/project-onboarding.yaml`
- `.ai-context/recipes/todo-plan.yaml`
- `.ai-context/recipes/imported-*.yaml` when an instruction file has been imported
- `.ai-context/packs/ai-handoff.pack.yaml`
- `.ai-context/packs/backend-docs.pack.yaml`
- `.ai-context/packs/onboarding.pack.yaml`
- `.ai-context/packs/current-branch.pack.yaml`
- `.ai-context/packs/dogfood-feedback.pack.yaml`
- `.ai-context/packs/ai-explanation.pack.yaml`
- `.ai-context/exports/backend-docs-compact.yaml`
- `.ai-context/diagrams/*.mmd`
- `instructions/Result/Backend/README.md`
- `instructions/Result/Backend/01-project-structure.md`
- `instructions/Result/Backend/02-architecture.md`
- `instructions/Result/Backend/03-important-files-and-flows.md`
- `instructions/Result/Backend/04-configuration-and-secrets.md`
- `instructions/Result/Backend/05-tasks-and-risks.md`
- `instructions/Result/Backend/06-verification-checklist.md`

AI Explanation Writer can additionally write, after explicit save:

- `.ai-context/docs/backend/07-ai-explanation.md`

`backend-docs-compact` uses scan data, imported instructions, detected routes, service registrations, configuration usage, tests, background workers, and rule-based flows. Flow diagrams are generated as simple Mermaid flowcharts for high-confidence flows.

`.ai-context/docs/backend/` is the canonical generated documentation location. `instructions/Result/Backend/` is an external Markdown mirror/export target.

## Test Execution Rules For Codex

Codex must not run the full solution test suite by default.

Codex must not run `CliCommandTests` unless `src/services/SourceWorker.Cli/Program.cs`, CLI parsing, CLI output, or CLI behavior changed.

Do not run `CliCommandTests` for AppHost-only changes, Dashboard/UI-only changes, ApiClient-only changes, DTO/property-only changes, docs-only changes, CSS/layout changes, AI provider UI changes unless CLI commands were added, or Local API endpoint changes unless CLI consumes that endpoint.

Targeted verification:

- AI application logic: `dotnet test tests/SourceWorker.Tests.Application/SourceWorker.Tests.Application.csproj --filter Ai`
- AI ApiClient changes: `dotnet test tests/SourceWorker.Tests.Application/SourceWorker.Tests.Application.csproj --filter ApiClient`
- AI Local API endpoints: `dotnet test tests/SourceWorker.Tests.LocalApi/SourceWorker.Tests.LocalApi.csproj --filter Ai`
- Dogfood application logic: `dotnet test tests/SourceWorker.Tests.Application/SourceWorker.Tests.Application.csproj --filter Dogfood`
- Dogfood Local API endpoints: `dotnet test tests/SourceWorker.Tests.LocalApi/SourceWorker.Tests.LocalApi.csproj --filter Dogfood`
- UI-only changes: build `ui/SourceWorker.UI` and `ui/SourceWorker.Dashboard`
- Docs-only: no tests required
