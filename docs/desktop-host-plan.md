# SourceWorker Desktop Host Plan

This document outlines the transition from the current `AppHost` (CLI-based orchestrator) to a full `SourceWorker.Desktop` application using MAUI Blazor Hybrid.

## Phase 1: AppHost (MVP 2.0) - DONE
- CLI-based bootstrap using `dotnet run --project src/services/SourceWorker.AppHost`.
- Orchestrates `LocalApi` and `Dashboard` processes.
- Passes security tokens and base URLs via environment variables.
- Provides a "one-start" experience for developers.

## Phase 2: MAUI Blazor Hybrid Shell - MVP 2.4 DONE
- Create `ui/SourceWorker.Desktop` project.
- Reference `SourceWorker.UI` (reusing all Razor components).
- The MAUI app will host the Blazor WebView.
- The MAUI app will start the `LocalApi` process internally during its lifecycle.
- Communication between the shell and the API will use `SourceWorker.ApiClient` with direct token/URL configuration.

MVP 2.4 implemented:
- `ui/SourceWorker.Desktop` Windows-first MAUI Blazor Hybrid app.
- `src/libs/SourceWorker.Hosting` shared orchestration library for AppHost and Desktop.
- Desktop startup progress/error components.
- Internal Local API lifecycle with health probing, token discovery, owned-process shutdown, and published-exe support.
- Desktop status bar, single-instance mutex, folder picker seam, notification/clipboard/link abstractions.
- Publish scripts for `artifacts/desktop/win-x64/SourceWorker.Desktop.exe`.

## Phase 3: Desktop Integration - NEXT
- **System Tray / Menu Bar**: Allow the app to run in the background.
- **Single Instance**: Prevent multiple instances of the Local API from starting.
- **Notifications**: Show OS-level notifications for long-running jobs (e.g., project scan completed).
- **Auto-start**: Option to start with the OS.

## Phase 4: Installer & Updates
- Bundle `LocalApi` and `Dashboard` assets (or just the MAUI app with the API).
- Use MSIX (Windows), DMG (macOS), or similar for distribution.
- Implement an auto-update mechanism for both the desktop shell and the Local API engine.

## Implementation Details for MAUI
- **Process Management**: Reuses `IProcessLauncher` and `IAppHostOrchestrator` logic, but adapted for the MAUI lifecycle (OnStart, OnSleep, OnResume).
- **Service Registration**: The MAUI project will register `ISourceWorkerApiClient` pointing to the locally managed API.
- **Security**: The MAUI app will read/write the same `api-token.txt` used by the Local API.

## Current Note: MVP 2.4 Desktop Before Installer

The current roadmap now has a working Windows-first MAUI Blazor Hybrid shell. It is not a full installer and does not include production code signing or update handling yet.

MVP 2.4 still does not add autonomous code editing, terminal execution, MCP, cloud sync, licensing, patch application, or automatic AI file writes.

MVP 2.2 adds dogfood readiness, provider validation, diagnostics, and AI preview polish in the reusable Blazor UI. These pages remain reusable by a future MAUI Blazor Hybrid host.

MVP 2.3 adds dogfood sessions, friction capture, next recommended actions, project path validation, project AI defaults, Quick Ask, and a lightweight logs page. These features are still implemented in the reusable `SourceWorker.UI` and `SourceWorker.ApiClient` boundary so the future MAUI host can reuse them without changing the local engine.

Development:

```powershell
dotnet run --project ui/SourceWorker.Desktop
```

Publish:

```powershell
.\tools\publish\Publish-Desktop-Windows.ps1
```

Run published exe:

```powershell
.\artifacts\desktop\win-x64\SourceWorker.Desktop.exe
```

## Test Execution Rules For Codex

Codex must not run the full solution test suite by default and must not run `CliCommandTests` unless CLI command parsing, output, or behavior changed. MAUI/AppHost/dashboard-only changes should use targeted project builds and relevant ApiClient/LocalApi tests instead.
