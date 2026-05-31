# SourceWorker Desktop Host Plan

This document outlines the transition from the current `AppHost` (CLI-based orchestrator) to a full `SourceWorker.Desktop` application using MAUI Blazor Hybrid.

## Phase 1: AppHost (MVP 2.0) - DONE
- CLI-based bootstrap using `dotnet run --project src/services/SourceWorker.AppHost`.
- Orchestrates `LocalApi` and `Dashboard` processes.
- Passes security tokens and base URLs via environment variables.
- Provides a "one-start" experience for developers.

## Phase 2: MAUI Blazor Hybrid Shell
- Create `ui/SourceWorker.Desktop` project.
- Reference `SourceWorker.UI` (reusing all Razor components).
- The MAUI app will host the Blazor WebView.
- The MAUI app will start the `LocalApi` process internally during its lifecycle.
- Communication between the shell and the API will use `SourceWorker.ApiClient` with direct token/URL configuration.

## Phase 3: Desktop Integration
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

## Current Note: MVP 2.1 AI Preview Before MAUI

The current roadmap intentionally adds safe AI provider preview before the full MAUI host. The reusable UI now has AI settings, prompt preview, and AI run history routes that the future MAUI Blazor Hybrid shell can host unchanged.

MVP 2.1 still does not add autonomous code editing, terminal execution, MCP, cloud sync, licensing, or automatic AI file writes.

## Test Execution Rules For Codex

Codex must not run the full solution test suite by default and must not run `CliCommandTests` unless CLI command parsing, output, or behavior changed. MAUI/AppHost/dashboard-only changes should use targeted project builds and relevant ApiClient/LocalApi tests instead.
