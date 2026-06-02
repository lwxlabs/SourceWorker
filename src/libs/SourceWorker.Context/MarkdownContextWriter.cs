using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed class MarkdownContextWriter(IProjectInstructionRepository? instructions = null, ISourcePathValidator? validator = null, IGeneratedArtifactRepository? artifacts = null, IContextFreshnessService? freshnessService = null, ILocalStorageSchemaService? schemaService = null, IGeneratedArtifactLifecycleService? lifecycleService = null, ICodeSymbolRepository? symbols = null, ICodeFlowRepository? flows = null, IRouteInfoRepository? routes = null, IServiceRegistrationRepository? services = null, IConfigurationUsageRepository? configs = null, ITestInfoRepository? tests = null, IContextPackWriter? packWriter = null) : IContextWriter
{
    public async Task<ContextWriteResult> WriteBasicContextAsync(ProjectScanResult scanResult, CancellationToken cancellationToken)
    {
        var imported = instructions is null ? null : await instructions.GetLatestAsync(scanResult.Project.Id, cancellationToken);
        var validation = imported is null || validator is null
            ? []
            : validator.Validate(scanResult.Project.RootPath, imported.SourcePaths.Select(p => p.OriginalPath));
        var output = Path.Combine(scanResult.Project.RootPath, ".ai-context");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "scans"));
        Directory.CreateDirectory(Path.Combine(output, "branches"));
        Directory.CreateDirectory(Path.Combine(output, "instructions"));
        Directory.CreateDirectory(Path.Combine(output, "instructions", "imported"));
        Directory.CreateDirectory(Path.Combine(output, "docs", "backend"));
        Directory.CreateDirectory(Path.Combine(output, "packs"));
        Directory.CreateDirectory(Path.Combine(output, "exports"));
        Directory.CreateDirectory(Path.Combine(output, "diagrams"));

        var freshness = freshnessService is null
            ? new ContextFreshnessReport(scanResult.Project.Id, false, [], [], [], [], 100, [])
            : await freshnessService.CheckAsync(scanResult.Project, cancellationToken);
        var schema = schemaService is null ? null : await schemaService.GetInfoAsync(cancellationToken);
        var lifecycle = lifecycleService is null ? null : await lifecycleService.AnalyzeAsync(scanResult.Project, cancellationToken);
        var codeSymbols = symbols is null ? [] : await symbols.GetByProjectAsync(scanResult.Project.Id, cancellationToken);
        var codeFlows = flows is null ? [] : await flows.GetByProjectAsync(scanResult.Project.Id, cancellationToken);
        var routeInfos = routes is null ? [] : await routes.GetByProjectAsync(scanResult.Project.Id, cancellationToken);
        var serviceRegs = services is null ? [] : await services.GetByProjectAsync(scanResult.Project.Id, cancellationToken);
        var configUsages = configs is null ? [] : await configs.GetByProjectAsync(scanResult.Project.Id, cancellationToken);
        var testInfos = tests is null ? [] : await tests.GetByProjectAsync(scanResult.Project.Id, cancellationToken);
        var files = new Dictionary<string, string>
        {
            ["CONTEXT.md"] = RootContext(scanResult, imported),
            ["index.yaml"] = Index(scanResult, imported, validation, schema, lifecycle, codeSymbols, codeFlows, routeInfos, serviceRegs, configUsages, testInfos),
            ["agent-handoff.md"] = AgentHandoff(scanResult, imported, validation, freshness, codeSymbols, codeFlows, routeInfos, serviceRegs, configUsages, testInfos),
            ["project.md"] = Project(scanResult),
            ["architecture.md"] = Architecture(scanResult),
            ["current-state.md"] = CurrentState(scanResult, freshness),
            ["tasks.md"] = Tasks(scanResult),
            ["ai-handoff.md"] = Handoff(scanResult, imported, validation, freshness, codeFlows, routeInfos, configUsages),
            ["scans/latest-scan.md"] = LatestScan(scanResult),
            ["branches/current.md"] = CurrentBranch(scanResult)
        };

        foreach (var file in files)
        {
            await WriteGeneratedAsync(scanResult.Project, scanResult.ScanRun.Id, null, output, file.Key, file.Value, GeneratedArtifactType.InternalContextFile, "SourceWorker.Context", cancellationToken, includeMarker: !file.Key.Equals("agent-handoff.md", StringComparison.OrdinalIgnoreCase));
        }
        await WriteGeneratedAsync(scanResult.Project, scanResult.ScanRun.Id, null, output, "instructions/README.md", InstructionsReadme(), GeneratedArtifactType.InstructionImportFile, "SourceWorker.Context", cancellationToken, includeMarker: false);
        if (packWriter is not null) await packWriter.WriteDefaultPacksAsync(scanResult.Project, scanResult.ScanRun.Id, cancellationToken);

        return new ContextWriteResult(output, files.Keys.ToArray());
    }

    private static string RootContext(ProjectScanResult scan, ProjectInstructionDocument? imported)
        => $"""
        # {scan.Project.Name}

        Local AI context generated by SourceWorker.

        - Root path: `{scan.Project.RootPath}`
        - Solution: {scan.SolutionPath ?? "none detected"}
        - Last scan: {scan.ScanRun.CompletedAt:O}
        - Files indexed: {scan.Files.Count}
        - Projects found: {scan.Projects.Count}
        - Test projects found: {scan.Projects.Count(p => p.IsTestProject)}
        - TODO/FIXME/REVIEW items: {scan.Todos.Count}
        - Imported instructions: {(imported is null ? "none" : imported.Title)}

        Start with `project.md`, then `architecture.md`, `current-state.md`, and `tasks.md`.

        ## Output Layout

        - Imported/user-authored instructions: `.ai-context/instructions/`
        - Canonical generated backend docs: `.ai-context/docs/backend/`
        - AI-ready packs: `.ai-context/packs/`
        - Export profiles/history: `.ai-context/exports/`
        - External mirrored backend docs: `instructions/Result/Backend/`
        """;

    private static string AgentHandoff(ProjectScanResult scan, ProjectInstructionDocument? imported, IReadOnlyList<ProjectInstructionSourcePath> validation, ContextFreshnessReport freshness, IReadOnlyList<CodeSymbol> symbols, IReadOnlyList<CodeFlow> flows, IReadOnlyList<RouteInfo> routes, IReadOnlyList<ServiceRegistration> services, IReadOnlyList<ConfigurationUsage> configs, IReadOnlyList<TestInfo> tests)
    {
        var profile = DetectProfile(scan, routes, tests);
        var sourceTodos = SourceTodos(scan).Take(25).ToArray();
        var visibleFlows = UserFacingFlows(scan, flows).Take(10).ToArray();
        var candidateFlows = CandidateDomainFlows(scan, symbols, flows, tests).Take(10).ToArray();
        var importantFiles = ImportantSourceFiles(scan, symbols, tests).Take(20).ToArray();
        var concepts = PublicConcepts(symbols).Take(20).ToArray();
        var artifactHealth = freshness.FreshnessScore >= 85 && !freshness.HasStaleContext ? "Healthy" : "Needs review";
        var handoffReadiness = scan.ScanRun.Status == ScanRunStatus.Completed && importantFiles.Length > 0 ? "Handoff ready" : "Needs scan or review";

        return $"""
        # Agent Handoff

        ## What This Project Appears To Be

        {InferProjectPurpose(scan, imported, symbols, tests)}

        ## What To Give An AI Agent

        - Quick context: `.ai-context/packs/ai-handoff.pack.yaml`
        - Deep architecture: `.ai-context/packs/backend-docs.pack.yaml`
        - Current branch: `.ai-context/packs/current-branch.pack.yaml`
        - Clean handoff file: `.ai-context/agent-handoff.md`

        ## Project Identity

        - Name: {scan.Project.Name}
        - Root path: `{scan.Project.RootPath}`
        - Solution: {scan.SolutionPath ?? "none detected"}
        - Project profile: {ProfileLabel(profile)}
        - Project count: {scan.Projects.Count}
        - Test project count: {scan.Projects.Count(p => p.IsTestProject)}

        ## Source Understanding Summary

        - Handoff readiness: {handoffReadiness}
        - Scan completed: {scan.ScanRun.CompletedAt:O}
        - Files indexed: {scan.Files.Count}
        - C# files: {scan.Files.Count(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) && !f.WasIgnored)}
        - Symbols detected: {symbols.Count}
        - Tests detected: {tests.Count}
        - Routes detected: {routes.Count}{(profile is ProjectHandoffProfile.Library or ProjectHandoffProfile.TestHeavyLibrary && routes.Count == 0 ? " (normal for a library)" : "")}
        - Service registrations detected: {services.Count}{(profile is ProjectHandoffProfile.Library or ProjectHandoffProfile.TestHeavyLibrary && services.Count == 0 ? " (normal for a library)" : "")}
        - Configuration usages detected: {configs.Count}{(profile is ProjectHandoffProfile.Library or ProjectHandoffProfile.TestHeavyLibrary && configs.Count == 0 ? " (normal for a library)" : "")}
        - Source TODO/FIXME/REVIEW items: {sourceTodos.Length}
        - Generated artifact health: {artifactHealth}

        ## Architecture / Structure

        {ArchitectureSummary(scan, profile)}

        {ListOrNone(StructureLines(scan, profile))}

        ## Important Source Files

        {ListOrNone(importantFiles.Select(f => $"- `{f}`"))}

        ## Public API / Main Concepts

        {ListOrNone(concepts.Select(c => $"- {c.Kind} `{c.FullName}` in `{c.RelativePath}:{c.LineNumber}`"))}

        ## Candidate Domain Flows

        {ListOrNone(candidateFlows.Select(f => $"- {f.Name}: {f.Trigger}{(f.Confidence == FlowConfidence.Low ? " (candidate; inspect files to confirm)" : "")}"))}

        ## Detected Project Flows

        {ListOrFallback(visibleFlows.Select(f => $"- {f.Name}: {f.Trigger}"), "- No non-domain application flows were detected. For libraries, use Candidate Domain Flows above.")}

        ## TODO / Risks From Source

        {ListOrFallback(sourceTodos.Select(t => $"- {t.Kind} `{t.RelativePath}:{t.LineNumber}` {CleanTodoText(t.Text)}"), "- No source TODO/FIXME/REVIEW items were detected. Generated context TODOs are excluded by default.")}

        ## Generated Context Available

        - Agent handoff: `.ai-context/agent-handoff.md`
        - Project summary: `.ai-context/project.md`
        - Architecture notes: `.ai-context/architecture.md`
        - Tasks and risks: `.ai-context/tasks.md`
        - Canonical backend docs: `.ai-context/docs/backend/`
        - Context packs: `.ai-context/packs/`

        ## How An Agent Should Work Here

        - Use this handoff to orient yourself before editing.
        - Inspect referenced source files before changing code.
        - Do not guess method, class, route, or config names.
        - Preserve the detected architecture and project boundaries.
        - Keep changes compileable.
        - Run targeted tests relevant to the change.
        - Do not add MCP, cloud, licensing, terminal execution, autonomous editing, or automatic AI file writes unless explicitly requested.

        ## Imported Instructions

        {(imported is null ? "- No user-authored instruction file has been imported for this project." : $"- Imported `{Path.GetFileName(imported.SourceFilePath)}` as `{imported.Title}`.")}

        {ListOrFallback(validation.Where(p => p.WarningMessage is not null).Select(p => $"- Warning for `{p.OriginalPath}`: {p.WarningMessage}"), "")}

        ## Recommended Prompt

        ```text
        You are working in this repository. Use the SourceWorker Agent Handoff below.
        Do not guess method names. Inspect files before editing.
        Preserve the current architecture. Keep changes compileable.
        Run targeted tests only; do not run the full suite unless shared contracts or core behavior changed.

        Task:
        <describe the change you want here>
        ```
        """;
    }

    private static string LegacyAgentHandoff(ProjectScanResult scan, ProjectInstructionDocument? imported, IReadOnlyList<ProjectInstructionSourcePath> validation, ContextFreshnessReport freshness, IReadOnlyList<CodeFlow> flows, IReadOnlyList<RouteInfo> routes)
        => $"""
        # Agent Handoff

        ## Project

        - Name: {scan.Project.Name}
        - Root path: `{scan.Project.RootPath}`
        - Solution: {scan.SolutionPath ?? "none detected"}
        - Project count: {scan.Projects.Count}
        - Test project count: {scan.Projects.Count(p => p.IsTestProject)}

        ## Purpose

        {DetectedPurpose(scan, imported)}

        ## Current State

        - Latest scan completed: {scan.ScanRun.CompletedAt:O}
        - Indexed files: {scan.Files.Count}
        - Context freshness score: {freshness.FreshnessScore}
        - Main technologies: {TechnologySummary(scan)}
        - Open TODO/FIXME/REVIEW items: {scan.Todos.Count}

        ## Architecture

        {DetectArchitectureStyle(scan)}

        {ListOrNone(scan.FolderRoles.Select(f => $"- `{f.RelativePath}`: {f.Description}"))}

        ## Important Files

        {ListOrNone(scan.SolutionFiles.Select(s => $"- `{s.RelativePath}`").Concat(scan.MainEntrypoints.Select(e => $"- `{e}`")).Concat(scan.ReadmeFiles.Take(5).Select(r => $"- `{r}`")).Concat(scan.ConfigFiles.Take(8).Select(c => $"- `{c}`")))}

        ## How To Work In This Repo

        - Use this handoff to orient yourself before editing.
        - Inspect referenced files before changing code.
        - Do not guess method, class, route, or config names.
        - Preserve the detected architecture and project boundaries.
        - Keep changes compileable.
        - Run targeted tests relevant to the change.
        - Do not add MCP, cloud, licensing, terminal execution, autonomous editing, or automatic AI file writes unless explicitly requested.

        ## Current Risks / TODOs

        {ListOrNone(scan.Todos.Take(25).Select(t => $"- {t.Kind} `{t.RelativePath}:{t.LineNumber}` {t.Text}"))}

        ## Detected Flows

        {ListOrNone(flows.Where(f => f.Confidence == FlowConfidence.High).Take(10).Select(f => $"- {f.Name}: {f.Trigger}"))}

        ## Detected Routes

        {ListOrNone(routes.Take(20).Select(r => $"- {r.HttpMethod} `{r.RoutePattern}` in `{r.RelativePath}:{r.LineNumber}`"))}

        ## Generated Context Available

        - Quick coding handoff pack: `.ai-context/packs/ai-handoff.pack.yaml`
        - Backend architecture pack: `.ai-context/packs/backend-docs.pack.yaml`
        - Canonical backend docs: `.ai-context/docs/backend/`
        - Tasks and risks: `.ai-context/tasks.md`
        - Scan details: `.ai-context/scans/latest-scan.md`

        ## Imported Instructions

        {(imported is null ? "- None imported." : $"- Imported `{Path.GetFileName(imported.SourceFilePath)}` as `{imported.Title}`.")}

        {ListOrNone(validation.Where(p => p.WarningMessage is not null).Select(p => $"- Warning for `{p.OriginalPath}`: {p.WarningMessage}"))}

        ## Recommended Next Agent Prompt

        ```text
        You are working in this repository. Use the SourceWorker Agent Handoff below.
        Do not guess method names. Inspect files before editing.
        Preserve the current architecture. Keep changes compileable.
        Run targeted tests only; do not run the full suite unless shared contracts or core behavior changed.

        Task:
        <describe the change you want here>
        ```
        """;

    private static string Project(ProjectScanResult scan)
        => $"""
        # Project

        ## Identity

        - Project name: {scan.Project.Name}
        - Root path: `{scan.Project.RootPath}`
        - Primary solution: {scan.SolutionPath ?? "none detected"}

        ## Solution Files

        {ListOrNone(scan.SolutionFiles.Select(s => $"- `{s.RelativePath}`"))}

        ## Main Technologies

        {ListOrNone(scan.Technologies.Select(t => $"- {t.Kind}: `{t.Evidence}`"))}

        ## Main Folders

        {ListOrNone(scan.FolderRoles.Select(f => $"- `{f.RelativePath}`: {f.Role} - {f.Description}"))}

        ## Projects Found

        {ListOrNone(scan.Projects.Select(p => $"- `{p.RelativePath}` ({p.Sdk})"))}

        ## Test Projects Found

        {ListOrNone(scan.Projects.Where(p => p.IsTestProject).Select(p => $"- `{p.RelativePath}`"))}

        ## Readme And Docs

        {ListOrNone(scan.ReadmeFiles.Select(f => $"- `{f}`"))}
        """;

    private static string Architecture(ProjectScanResult scan)
        => $"""
        # Architecture

        ## Detected Style

        {DetectArchitectureStyle(scan)}

        ## Layers And Folders

        {ListOrNone(scan.FolderRoles.Select(f => $"- `{f.RelativePath}`: {f.Description}"))}

        ## Project Dependencies

        {ListOrNone(scan.Projects.SelectMany(p => p.ProjectReferences.Select(r => $"- `{p.RelativePath}` -> `{r}`")))}

        ## Main Services

        {ListOrNone(scan.Projects.Where(p => p.RelativePath.StartsWith("src/services/", StringComparison.OrdinalIgnoreCase)).Select(p => $"- `{p.RelativePath}`"))}

        ## Main Libraries

        {ListOrNone(scan.Projects.Where(p => p.RelativePath.StartsWith("src/libs/", StringComparison.OrdinalIgnoreCase)).Select(p => $"- `{p.RelativePath}`"))}

        ## Main Infrastructure

        {ListOrNone(scan.Projects.Where(p => p.RelativePath.StartsWith("src/infrastructure/", StringComparison.OrdinalIgnoreCase)).Select(p => $"- `{p.RelativePath}`"))}
        """;

    private static string CurrentState(ProjectScanResult scan, ContextFreshnessReport freshness)
        => $"""
        # Current State

        ## What Exists Now

        - A project tree with {scan.Files.Count} indexed files.
        - {scan.Projects.Count} project files, including {scan.Projects.Count(p => p.IsTestProject)} test projects.
        - Generated local AI context in `.ai-context/`.

        ## What Is Missing

        - AI provider integration is intentionally not present.
        - Desktop dashboard is intentionally not present.
        - MCP, cloud, licensing, and secret storage are intentionally not present.

        ## Last Scan

        - Completed at: {scan.ScanRun.CompletedAt:O}
        - New files: {scan.Files.Count(f => f.ChangeKind == FileChangeKind.New)}
        - Changed files: {scan.Files.Count(f => f.ChangeKind == FileChangeKind.Changed)}
        - Deleted files: {scan.DeletedFiles.Count}
        - Context freshness score: {freshness.FreshnessScore}
        - Has stale context: {freshness.HasStaleContext}

        ## Known Limitations

        - `.gitignore` support is basic.
        - Architecture detection is folder and project-reference based.
        - Recipe output is rule-based, not AI-authored.
        """;

    private static string Tasks(ProjectScanResult scan)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Tasks");
        builder.AppendLine();
        var sourceTodos = SourceTodos(scan);
        foreach (var group in sourceTodos.GroupBy(t => TopFolder(t.RelativePath)).OrderBy(g => g.Key))
        {
            builder.AppendLine($"## {group.Key}");
            builder.AppendLine();
            foreach (var todo in group.OrderByDescending(t => t.Kind).ThenBy(t => t.RelativePath).ThenBy(t => t.LineNumber))
            {
                builder.AppendLine($"- {todo.Kind} `{todo.RelativePath}:{todo.LineNumber}` {CleanTodoText(todo.Text)}");
            }
            builder.AppendLine();
        }

        if (sourceTodos.Count == 0)
        {
            builder.AppendLine("- None detected");
        }

        return builder.ToString();
    }

    private static string Handoff(ProjectScanResult scan, ProjectInstructionDocument? imported, IReadOnlyList<ProjectInstructionSourcePath> validation, ContextFreshnessReport freshness, IReadOnlyList<CodeFlow> flows, IReadOnlyList<RouteInfo> routes, IReadOnlyList<ConfigurationUsage> configs)
        => $"""
        # AI Handoff

        ## Project Overview

        SourceWorker scanned `{scan.Project.RootPath}` and generated this local context. The project appears to use {TechnologySummary(scan)}.

        ## Detected Solution And Project Structure

        - Solution: {scan.SolutionPath ?? "none detected"}
        - Projects: {scan.Projects.Count}
        - Test projects: {scan.Projects.Count(p => p.IsTestProject)}
        - Architecture: {DetectArchitectureStyle(scan)}

        ## Important Files

        {ListOrNone(scan.SolutionFiles.Select(s => $"- `{s.RelativePath}`").Concat(scan.MainEntrypoints.Select(e => $"- `{e}`")).Concat(scan.ConfigFiles.Take(10).Select(c => $"- `{c}`")))}

        ## High Confidence Flows

        {ListOrNone(flows.Where(f => f.Confidence == FlowConfidence.High).Select(f => $"- {f.Name}: {f.Trigger}"))}

        ## Detected Routes

        {ListOrNone(routes.Take(20).Select(r => $"- {r.HttpMethod} `{r.RoutePattern}` in `{r.RelativePath}:{r.LineNumber}`"))}

        ## Detected Configuration Usages

        {ListOrNone(configs.Take(20).Select(c => $"- {c.UsageKind} `{c.KeyOrSection}` in `{c.RelativePath}:{c.LineNumber}`"))}

        ## Current Branch

        - Branch: {scan.Git.Branch ?? "unknown"}
        - Commit: {scan.Git.CommitSha ?? "unknown"}
        - Dirty working tree: {scan.Git.IsDirty}

        ## Context Freshness

        - Score: {freshness.FreshnessScore}
        - Has stale context: {freshness.HasStaleContext}
        - Missing generated files: {freshness.MissingGeneratedFiles.Count}
        - Manually modified files: {freshness.ManuallyModifiedFiles.Count}

        ## Detected Technologies

        {ListOrNone(scan.Technologies.Select(t => $"- {t.Kind}: `{t.Evidence}`"))}

        ## Imported Instruction Summary

        {(imported is null ? "- No instruction document imported." : $"- `{Path.GetFileName(imported.SourceFilePath)}` imported as `{imported.Title}`")}

        ## Active Recipe

        - `backend-docs-compact`

        ## Available Packs And Outputs

        - AI handoff pack: `.ai-context/packs/ai-handoff.pack.yaml`
        - Backend docs pack: `.ai-context/packs/backend-docs.pack.yaml`
        - Onboarding pack: `.ai-context/packs/onboarding.pack.yaml`
        - Current branch pack: `.ai-context/packs/current-branch.pack.yaml`
        - Canonical backend docs: `.ai-context/docs/backend`
        - External backend docs mirror: `instructions/Result/Backend`
        - Export profile: `.ai-context/exports/backend-docs-compact.yaml`

        AI assistants should consume packs first, then read specific source files before making changes.

        ## Important Source Paths

        {ListOrNone((validation.Count > 0 ? validation : imported?.SourcePaths ?? []).Select(p => $"- `{p.OriginalPath}` -> `{p.NormalizedPath}` ({(p.Exists ? "found" : "missing")}){(p.WarningMessage is null ? "" : $" - {p.WarningMessage}")}"))}

        ## Important Rules And Mindset

        {ListOrNone((imported?.Rules ?? []).Select(r => $"- {r.Text}"))}

        ## Todo Summary

        {ListOrNone((imported?.Todos ?? []).Select(t => $"- {(t.IsCompleted ? "[x]" : "[ ]")} {t.Text}").Concat(scan.Todos.Take(20).Select(t => $"- {t.Kind} `{t.RelativePath}:{t.LineNumber}` {t.Text}")))}

        ## Skip Later

        {ListOrNone((imported?.SkipItems ?? []).Select(s => $"- {s.Text}"))}

        ## Verification Checklist

        {ListOrNone((imported?.VerificationItems ?? []).Select(v => $"- {(v.IsCompleted ? "[x]" : "[ ]")} {v.Text}"))}

        ## How To Work In This Repo

        - Read `project.md` for identity and project inventory.
        - Read `architecture.md` before changing boundaries.
        - Check `tasks.md` for TODO/FIXME/REVIEW items.
        - Prefer edits inside the layer that owns the behavior.

        ## What Not To Touch

        - Do not add AI providers, MCP, cloud, licensing, or dashboard code for this milestone.
        - Do not put generated context outside `.ai-context/` or `instructions/Result/Backend/` without explicit approval.
        - Do not store secrets in Localite.

        ## Next Recommended Steps

        - Verify project references and folder roles are correct.
        - Improve ignore handling if generated or vendor files appear in `index.yaml`.
        - Use `backend-docs-compact` output as the first rule-based documentation recipe.

        ## AI Assistant Rules

        - Do not guess.
        - Analyze files before changing code.
        - Do not break existing code.
        - Preserve current architecture.
        - Keep changes compileable.
        - Run tests after changes.
        - Do not do skip-later items.
        - Use detected source file paths.
        - Do not invent method names.
        - Keep Domain dependency-free.
        - Do not add AI/MCP/MAUI/cloud/licensing unless requested.
        - Update docs/Current.md after significant changes.
        """;

    private static string LatestScan(ProjectScanResult scan)
        => $"""
        # Latest Scan

        - Scan run: {scan.ScanRun.Id}
        - Started: {scan.ScanRun.StartedAt:O}
        - Completed: {scan.ScanRun.CompletedAt:O}
        - Files indexed: {scan.Files.Count}
        - New files: {scan.Files.Count(f => f.ChangeKind == FileChangeKind.New)}
        - Changed files: {scan.Files.Count(f => f.ChangeKind == FileChangeKind.Changed)}
        - Unchanged files: {scan.Files.Count(f => f.ChangeKind == FileChangeKind.Unchanged)}
        - Deleted files: {scan.DeletedFiles.Count}
        - TODO/FIXME/REVIEW items: {scan.Todos.Count}

        ## Deleted Files

        {ListOrNone(scan.DeletedFiles.Select(f => $"- `{f}`"))}
        """;

    private static string CurrentBranch(ProjectScanResult scan)
        => $"""
        # Current Branch

        - Branch: {scan.Git.Branch ?? "unknown"}
        - Commit: {scan.Git.CommitSha ?? "unknown"}
        - Dirty working tree: {scan.Git.IsDirty}
        - Captured at: {scan.Git.CapturedAt:O}
        """;

    private static string Index(ProjectScanResult scan, ProjectInstructionDocument? imported, IReadOnlyList<ProjectInstructionSourcePath> validation, LocalStorageSchemaInfo? schema, ArtifactLifecycleReport? lifecycle, IReadOnlyList<CodeSymbol> symbols, IReadOnlyList<CodeFlow> flows, IReadOnlyList<RouteInfo> routes, IReadOnlyList<ServiceRegistration> services, IReadOnlyList<ConfigurationUsage> configs, IReadOnlyList<TestInfo> tests)
    {
        var builder = new StringBuilder();
        builder.AppendLine("formatVersion: 5");
        builder.AppendLine("project:");
        builder.AppendLine($"  id: \"{scan.Project.Id}\"");
        builder.AppendLine($"  name: \"{Escape(scan.Project.Name)}\"");
        builder.AppendLine($"  rootPath: \"{Escape(scan.Project.RootPath)}\"");
        builder.AppendLine("lastScan:");
        builder.AppendLine($"  id: \"{scan.ScanRun.Id}\"");
        builder.AppendLine($"  startedUtc: \"{scan.ScanRun.StartedAt:O}\"");
        builder.AppendLine($"  finishedUtc: \"{scan.ScanRun.CompletedAt:O}\"");
        builder.AppendLine($"  status: {scan.ScanRun.Status}");
        builder.AppendLine("git:");
        builder.AppendLine($"  branch: \"{Escape(scan.Git.Branch ?? string.Empty)}\"");
        builder.AppendLine($"  hasUncommittedChanges: {scan.Git.IsDirty.ToString().ToLowerInvariant()}");
        builder.AppendLine("storage:");
        builder.AppendLine("  provider: Localite");
        builder.AppendLine("  mode: table-backed");
        builder.AppendLine($"  schemaVersion: {schema?.SchemaVersion ?? 1}");
        builder.AppendLine("contextFiles:");
        foreach (var path in new[] { "CONTEXT.md", "agent-handoff.md", "project.md", "architecture.md", "current-state.md", "tasks.md", "ai-handoff.md", "scans/latest-scan.md", "branches/current.md" })
        {
            builder.AppendLine($"  - path: \"{path}\"");
            builder.AppendLine("    type: ContextFile");
            builder.AppendLine("    status: Generated");
        }
        builder.AppendLine("recipes:");
        builder.AppendLine("  - id: \"backend-docs-compact\"");
        builder.AppendLine("    path: \".ai-context/recipes/backend-docs-compact.yaml\"");
        builder.AppendLine("    outputPath: \"instructions/Result/Backend\"");
        builder.AppendLine("stale:");
        builder.AppendLine($"  hasStaleContext: {((lifecycle?.CleanupRecommended ?? false)).ToString().ToLowerInvariant()}");
        builder.AppendLine("  staleFiles: []");
        builder.AppendLine($"  missingGeneratedFiles: [{string.Join(", ", lifecycle?.MissingArtifacts.Select(EscapeYamlList) ?? [])}]");
        builder.AppendLine($"  manuallyModifiedFiles: [{string.Join(", ", lifecycle?.ManuallyModifiedArtifacts.Select(EscapeYamlList) ?? [])}]");
        builder.AppendLine("artifacts:");
        builder.AppendLine($"  total: {lifecycle?.Artifacts.Count ?? 0}");
        builder.AppendLine($"  generated: {lifecycle?.Artifacts.Count(a => a.Status == GeneratedArtifactStatus.Generated) ?? 0}");
        builder.AppendLine($"  stale: {lifecycle?.StaleArtifacts.Count ?? 0}");
        builder.AppendLine($"  missing: {lifecycle?.MissingArtifacts.Count ?? 0}");
        builder.AppendLine($"  manuallyModified: {lifecycle?.ManuallyModifiedArtifacts.Count ?? 0}");
        builder.AppendLine($"  orphaned: {lifecycle?.OrphanedArtifacts.Count ?? 0}");
        builder.AppendLine($"  deprecated: {lifecycle?.DeprecatedArtifacts.Count ?? 0}");
        builder.AppendLine("cleanup:");
        builder.AppendLine($"  recommended: {((lifecycle?.CleanupRecommended ?? false)).ToString().ToLowerInvariant()}");
        builder.AppendLine($"  previewCommand: \"sourceworker cleanup-preview {Escape(scan.Project.RootPath)}\"");
        builder.AppendLine("codeIntelligence:");
        builder.AppendLine("  symbols:");
        builder.AppendLine($"    total: {symbols.Count}");
        builder.AppendLine($"    classes: {symbols.Count(s => s.Kind == CodeSymbolKind.Class)}");
        builder.AppendLine($"    interfaces: {symbols.Count(s => s.Kind == CodeSymbolKind.Interface)}");
        builder.AppendLine($"    methods: {symbols.Count(s => s.Kind == CodeSymbolKind.Method)}");
        builder.AppendLine("  routes:");
        builder.AppendLine($"    total: {routes.Count}");
        builder.AppendLine("  serviceRegistrations:");
        builder.AppendLine($"    total: {services.Count}");
        builder.AppendLine("  configurationUsages:");
        builder.AppendLine($"    total: {configs.Count}");
        builder.AppendLine("  tests:");
        builder.AppendLine($"    total: {tests.Count}");
        builder.AppendLine("flows:");
        builder.AppendLine($"  total: {flows.Count}");
        builder.AppendLine($"  highConfidence: {flows.Count(f => f.Confidence == FlowConfidence.High)}");
        builder.AppendLine($"  mediumConfidence: {flows.Count(f => f.Confidence == FlowConfidence.Medium)}");
        builder.AppendLine($"  lowConfidence: {flows.Count(f => f.Confidence == FlowConfidence.Low)}");
        builder.AppendLine("scanCompleteness:");
        builder.AppendLine($"  csharpFiles: {scan.Files.Count(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) && !f.WasIgnored)}");
        builder.AppendLine($"  symbols: {symbols.Count}");
        builder.AppendLine($"  routes: {routes.Count}");
        builder.AppendLine($"  flows: {flows.Count}");
        builder.AppendLine($"  warning: \"{Escape(scan.Files.Any(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)) && symbols.Count == 0 ? "C# files were found but no symbols were extracted." : "")}\"");
        builder.AppendLine("contextBrowser:");
        builder.AppendLine("  files:");
        builder.AppendLine("    total: 0");
        builder.AppendLine("  searchable:");
        builder.AppendLine("    enabled: true");
        builder.AppendLine("outputs:");
        builder.AppendLine("  internal:");
        builder.AppendLine("    - id: internal-backend-docs");
        builder.AppendLine("      path: \".ai-context/docs/backend\"");
        builder.AppendLine("      files: 7");
        builder.AppendLine("      status: Generated");
        builder.AppendLine("  external:");
        builder.AppendLine("    - id: external-backend-docs");
        builder.AppendLine("      path: \"instructions/Result/Backend\"");
        builder.AppendLine("      files: 7");
        builder.AppendLine("      status: Generated");
        builder.AppendLine("packs:");
        builder.AppendLine("  total: 5");
        builder.AppendLine("  items:");
        foreach (var pack in new[] { ("ai-handoff", ".ai-context/packs/ai-handoff.pack.yaml", 3), ("backend-docs", ".ai-context/packs/backend-docs.pack.yaml", 7), ("onboarding", ".ai-context/packs/onboarding.pack.yaml", 3), ("current-branch", ".ai-context/packs/current-branch.pack.yaml", 3), ("dogfood-feedback", ".ai-context/packs/dogfood-feedback.pack.yaml", 3) })
        {
            builder.AppendLine($"    - id: {pack.Item1}");
            builder.AppendLine($"      path: \"{pack.Item2}\"");
            builder.AppendLine($"      fileCount: {pack.Item3}");
            builder.AppendLine("      freshness: unknown");
        }
        builder.AppendLine("exports:");
        builder.AppendLine("  total: 1");
        builder.AppendLine("  items:");
        builder.AppendLine("    - id: backend-docs-compact");
        builder.AppendLine("      target: MarkdownFolder");
        builder.AppendLine("      path: \"instructions/Result/Backend\"");
        builder.AppendLine($"      lastExportUtc: \"{scan.ScanRun.CompletedAt:O}\"");
        builder.AppendLine("docs:");
        builder.AppendLine("  internalBackendDocs:");
        builder.AppendLine("    path: \".ai-context/docs/backend\"");
        builder.AppendLine("    fileCount: 7");
        builder.AppendLine("    status: Generated");
        if (imported is not null)
        {
            builder.AppendLine("instructions:");
            builder.AppendLine($"  imported: \".ai-context/instructions/imported/imported-instructions.md\"");
            builder.AppendLine($"  sourcePathValidation: \".ai-context/instructions/imported/source-path-validation.md\"");
            builder.AppendLine($"  sourceFile: \"{Escape(imported.SourceFilePath)}\"");
            builder.AppendLine($"  missingSourcePaths: {validation.Count(p => !p.Exists || !p.IsInsideProject)}");
            builder.AppendLine("  sourcePaths:");
            foreach (var path in validation)
            {
                builder.AppendLine($"    - original: \"{Escape(path.OriginalPath)}\"");
                builder.AppendLine($"      normalized: \"{Escape(path.NormalizedPath)}\"");
                builder.AppendLine($"      exists: {path.Exists.ToString().ToLowerInvariant()}");
                builder.AppendLine($"      insideProject: {path.IsInsideProject.ToString().ToLowerInvariant()}");
                if (path.WarningMessage is not null) builder.AppendLine($"      warning: \"{Escape(path.WarningMessage)}\"");
            }
        }
        builder.AppendLine("files:");
        foreach (var file in scan.Files.OrderBy(f => f.RelativePath))
        {
            builder.AppendLine($"  - path: \"{Escape(file.RelativePath)}\"");
            builder.AppendLine($"    absolutePath: \"{Escape(file.Path)}\"");
            builder.AppendLine($"    kind: {file.Kind}");
            builder.AppendLine($"    extension: \"{Escape(file.Extension)}\"");
            builder.AppendLine($"    sizeBytes: {file.SizeBytes}");
            builder.AppendLine($"    lastModifiedUtc: \"{file.LastModifiedUtc:O}\"");
            builder.AppendLine($"    sha256: \"{file.Sha256}\"");
            builder.AppendLine($"    changeKind: {file.ChangeKind}");
        }

        return builder.ToString();
    }

    private async Task WriteGeneratedAsync(Project project, Guid scanRunId, string? recipeId, string root, string relativePath, string body, GeneratedArtifactType type, string generatorName, CancellationToken cancellationToken, bool includeMarker = true)
    {
        var bodyHash = Hash(body);
        var artifactId = Guid.NewGuid();
        var content = includeMarker ? Metadata(artifactId, "Generated") + body : body;
        var absolute = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllTextAsync(absolute, content, cancellationToken);
        if (artifacts is not null)
        {
            var projectRelative = Path.GetRelativePath(project.RootPath, absolute).Replace('\\', '/');
            await artifacts.UpsertAsync(new GeneratedArtifact(artifactId, project.Id, scanRunId, recipeId, projectRelative, absolute, type, bodyHash, DateTimeOffset.UtcNow, bodyHash, GeneratedArtifactStatus.Generated, generatorName, null), cancellationToken);
        }
    }

    public static string Metadata(Guid artifactId, string status)
        => $"<!-- SourceWorker:Generated; ArtifactId={artifactId}; Status={status} -->{Environment.NewLine}{Environment.NewLine}";

    public static string Hash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static string InstructionsReadme()
        => """
        # Instructions

        This folder is for imported or user-authored project instructions.

        - Imported instructions are written under `.ai-context/instructions/imported/`.
        - Generated internal documentation is written under `.ai-context/docs/`.
        - AI-ready bundles are written under `.ai-context/packs/`.
        - External mirrored documentation may be written under `instructions/Result/Backend/`.

        If no instruction file has been imported, this folder may contain only this README.
        """;

    private static ProjectHandoffProfile DetectProfile(ProjectScanResult scan, IReadOnlyList<RouteInfo> routes, IReadOnlyList<TestInfo> tests)
    {
        if (scan.Projects.Any(p => p.RelativePath.Contains("Maui", StringComparison.OrdinalIgnoreCase) || p.RelativePath.Contains("Desktop", StringComparison.OrdinalIgnoreCase))) return ProjectHandoffProfile.DesktopApp;
        if (routes.Count > 0 || scan.Technologies.Any(t => t.Kind == TechnologyKind.AspNetCore)) return ProjectHandoffProfile.WebApi;
        if (scan.Projects.Any(p => p.RelativePath.Contains("Worker", StringComparison.OrdinalIgnoreCase) || p.RelativePath.Contains("Service", StringComparison.OrdinalIgnoreCase))) return ProjectHandoffProfile.WorkerService;
        if (scan.FolderRoles.Any(f => f.Role == "ui") && scan.FolderRoles.Any(f => f.Role == "services")) return ProjectHandoffProfile.FullstackApp;
        if (scan.Projects.Count(p => p.IsTestProject) >= Math.Max(1, scan.Projects.Count / 2) || tests.Count > 25) return ProjectHandoffProfile.TestHeavyLibrary;
        if (scan.Projects.Count > 0 && scan.Projects.Count(p => p.IsTestProject) > 0) return ProjectHandoffProfile.Library;
        return ProjectHandoffProfile.Unknown;
    }

    private static string ProfileLabel(ProjectHandoffProfile profile)
        => profile switch
        {
            ProjectHandoffProfile.WebApi => "Web API",
            ProjectHandoffProfile.WorkerService => "Worker/service",
            ProjectHandoffProfile.DesktopApp => "Desktop app",
            ProjectHandoffProfile.FullstackApp => "Fullstack app",
            ProjectHandoffProfile.TestHeavyLibrary => "Test-heavy library",
            ProjectHandoffProfile.Library => "Library",
            _ => "Unknown"
        };

    private static string InferProjectPurpose(ProjectScanResult scan, ProjectInstructionDocument? imported, IReadOnlyList<CodeSymbol> symbols, IReadOnlyList<TestInfo> tests)
    {
        if (imported is not null && !string.IsNullOrWhiteSpace(imported.Title)) return imported.Title;

        var readme = ReadProjectReadmeSummary(scan);
        if (!string.IsNullOrWhiteSpace(readme)) return readme;

        var projectNames = scan.Projects.Where(p => !p.IsTestProject).Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
        var symbolWords = symbols.Where(s => s.Kind is CodeSymbolKind.Class or CodeSymbolKind.Interface or CodeSymbolKind.Record)
            .Select(s => SplitPascalWords(s.Name))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Select(g => g.Key)
            .Take(10)
            .ToArray();

        if (scan.Project.Name.Contains("Localite", StringComparison.OrdinalIgnoreCase) || projectNames.Any(p => p.Contains("Localite", StringComparison.OrdinalIgnoreCase)))
        {
            var details = new[] { "local persistence", "schema", "query", "save", "delete", "cache", "import/export", "secure local handling", "tests" }
                .Where(term => symbolWords.Any(w => w.Contains(term.Split('/')[0], StringComparison.OrdinalIgnoreCase)) || scan.Files.Any(f => f.RelativePath.Contains(term.Split('/')[0], StringComparison.OrdinalIgnoreCase)))
                .DefaultIfEmpty("local persistence")
                .Distinct(StringComparer.OrdinalIgnoreCase);
            return $"This appears to be a .NET local persistence/library project centered on {string.Join(", ", details)}.";
        }

        if (projectNames.Length > 0)
        {
            var testNote = tests.Count > 0 || scan.Projects.Any(p => p.IsTestProject) ? " with automated tests" : "";
            var concepts = symbolWords.Length == 0 ? "" : $" Main detected concepts include {string.Join(", ", symbolWords.Take(6))}.";
            return $"This appears to be a .NET project built around {string.Join(", ", projectNames)}{testNote}.{concepts}";
        }

        return "This project purpose could not be confidently inferred yet. Use the project name, source folders, and README/docs as context.";
    }

    private static string? ReadProjectReadmeSummary(ProjectScanResult scan)
    {
        var readme = scan.ReadmeFiles
            .Where(path => !IsGeneratedContextPath(path))
            .OrderBy(path => path.Equals("README.md", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(path => path.Length)
            .FirstOrDefault();
        if (readme is null) return null;

        var absolute = Path.Combine(scan.Project.RootPath, readme.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute)) return null;
        var lines = File.ReadLines(absolute)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("<!--", StringComparison.Ordinal))
            .Take(12)
            .ToArray();
        if (lines.Length == 0) return null;

        var title = lines.FirstOrDefault(line => line.StartsWith("#", StringComparison.Ordinal))?.Trim('#', ' ');
        var paragraphs = lines.Where(line => !line.StartsWith("#", StringComparison.Ordinal) && !line.StartsWith("```", StringComparison.Ordinal) && !line.StartsWith("- ", StringComparison.Ordinal)).Take(2).ToArray();
        if (paragraphs.Length > 0) return $"{(string.IsNullOrWhiteSpace(title) ? scan.Project.Name : title)} appears to be: {string.Join(" ", paragraphs)}";
        return string.IsNullOrWhiteSpace(title) ? null : $"This appears to be the `{title}` project. Purpose is inferred from the README title.";
    }

    private static string SplitPascalWords(string value)
        => string.Join(" ", Regex.Matches(value, "[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+").Select(m => m.Value));

    private static string ArchitectureSummary(ProjectScanResult scan, ProjectHandoffProfile profile)
    {
        if (scan.FolderRoles.Any(f => f.Role == "core") && scan.FolderRoles.Any(f => f.Role == "infrastructure"))
            return "Clean architecture style with core/application contracts separated from infrastructure adapters.";

        if (profile is ProjectHandoffProfile.Library or ProjectHandoffProfile.TestHeavyLibrary)
        {
            var libraryProjects = scan.Projects.Where(p => !p.IsTestProject).Select(p => p.RelativePath).Take(8);
            var testProjects = scan.Projects.Where(p => p.IsTestProject).Select(p => p.RelativePath).Take(8);
            return $"""
            Library-style structure. No formal clean architecture layout was detected, but the repo still has useful structure:

            - Main library projects: {InlineList(libraryProjects)}
            - Test projects: {InlineList(testProjects)}
            - Feature/source folders: {InlineList(FeatureFolders(scan).Take(12))}
            """;
        }

        if (scan.FolderRoles.Count > 0) return "Project structure is organized by detected top-level folders rather than a named architecture pattern.";
        if (scan.Projects.Count > 0) return "Project structure is organized primarily by .NET project files.";
        return "Architecture could not be inferred because no solution/project structure was detected yet.";
    }

    private static IEnumerable<string> StructureLines(ProjectScanResult scan, ProjectHandoffProfile profile)
    {
        foreach (var role in scan.FolderRoles)
            yield return $"- `{role.RelativePath}`: {role.Description}";
        if (profile is not (ProjectHandoffProfile.Library or ProjectHandoffProfile.TestHeavyLibrary))
            foreach (var folder in FeatureFolders(scan).Take(16))
                yield return $"- `{folder}`: source/feature folder";
        foreach (var project in scan.Projects.Take(20))
            yield return $"- `{project.RelativePath}`: {(project.IsTestProject ? "test project" : "project")}";
    }

    private static IEnumerable<string> FeatureFolders(ProjectScanResult scan)
        => scan.Files
            .Where(f => f.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) && !IsGeneratedContextPath(f.RelativePath))
            .Select(f => f.RelativePath.Split('/'))
            .Where(parts => parts.Length >= 2)
            .Select(parts => string.Join('/', parts.Take(Math.Min(parts.Length - 1, 3))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path);

    private static IEnumerable<string> ImportantSourceFiles(ProjectScanResult scan, IReadOnlyList<CodeSymbol> symbols, IReadOnlyList<TestInfo> tests)
        => scan.SolutionFiles.Select(s => s.RelativePath)
            .Concat(scan.MainEntrypoints)
            .Concat(scan.ReadmeFiles.Where(path => !IsGeneratedContextPath(path)))
            .Concat(scan.Projects.Select(p => p.RelativePath))
            .Concat(RankedImportantSourceFiles(symbols, tests))
            .Concat(scan.ConfigFiles.Where(path => !IsGeneratedContextPath(path)).Take(8))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<CodeSymbol> PublicConcepts(IReadOnlyList<CodeSymbol> symbols)
        => symbols
            .Where(s => s.Accessibility is "public" or "internal" && s.Kind is CodeSymbolKind.Class or CodeSymbolKind.Interface or CodeSymbolKind.Record or CodeSymbolKind.Enum)
            .OrderBy(s => IsTestPath(s.RelativePath) ? 1 : 0)
            .ThenBy(s => IsFakeOrTestConcept(s.Name) ? 1 : 0)
            .ThenBy(s => s.RelativePath.Contains("/Interfaces/", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(s => s.RelativePath.Contains("/public/", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(s => s.Name.StartsWith("I", StringComparison.Ordinal) && s.Kind == CodeSymbolKind.Interface ? 0 : 1)
            .ThenBy(s => s.Kind)
            .ThenBy(s => s.RelativePath)
            .ThenBy(s => s.Name);

    private static IEnumerable<string> RankedImportantSourceFiles(IReadOnlyList<CodeSymbol> symbols, IReadOnlyList<TestInfo> tests)
    {
        var importantTerms = new[] { "Context", "Store", "LocalDbSet", "DbObjSet", "GraphOp", "SaveOp", "DeleteOp", "ServiceBuilder", "ServiceExtensions", "Sql", "Attribute", "Options", "Interface" };
        return symbols
            .Where(s => !IsTestPath(s.RelativePath))
            .Where(s => importantTerms.Any(term => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || s.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(s => s.Accessibility == "public")
            .ThenBy(s => s.RelativePath)
            .Select(s => s.RelativePath)
            .Concat(tests.Where(t => t.MethodName.Contains("Production", StringComparison.OrdinalIgnoreCase) || t.RelativePath.Contains("Heavy", StringComparison.OrdinalIgnoreCase) || t.RelativePath.Contains("Hardening", StringComparison.OrdinalIgnoreCase)).Select(t => t.RelativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTestPath(string relativePath)
        => relativePath.Contains("/test", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains(".Production.Tests/", StringComparison.OrdinalIgnoreCase);

    private static bool IsFakeOrTestConcept(string name)
        => name.Contains("Fake", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("Mock", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("Sample", StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
           name.EndsWith("Test", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<CodeFlow> UserFacingFlows(ProjectScanResult scan, IReadOnlyList<CodeFlow> flows)
    {
        var isSourceWorker = scan.Project.Name.Equals("SourceWorker", StringComparison.OrdinalIgnoreCase) ||
            scan.Projects.Any(p => p.Name.StartsWith("SourceWorker.", StringComparison.OrdinalIgnoreCase));
        return flows
            .Where(f => f.Origin != FlowOrigin.RecipeGenerated)
            .Where(f => isSourceWorker || f.Origin != FlowOrigin.SourceWorkerInternal)
            .Where(f => f.FlowType != CodeFlowType.CandidateDomainFlow)
            .OrderByDescending(f => f.Confidence)
            .ThenBy(f => f.Name)
            .ToArray();
    }

    private static IReadOnlyList<CodeFlow> CandidateDomainFlows(ProjectScanResult scan, IReadOnlyList<CodeSymbol> symbols, IReadOnlyList<CodeFlow> flows, IReadOnlyList<TestInfo> tests)
    {
        var existing = flows.Where(f => f.FlowType == CodeFlowType.CandidateDomainFlow && f.Origin != FlowOrigin.SourceWorkerInternal).ToList();
        if (existing.Count > 0) return existing.OrderByDescending(f => f.Confidence).ThenBy(f => f.Name).ToArray();

        var terms = new (string Name, string[] Keywords)[]
        {
            ("Create context", ["Context", "Create", "Open"]),
            ("Ensure schema", ["Schema", "Ensure", "Migration"]),
            ("Query data", ["Query", "Find", "Get", "Read"]),
            ("Save graph", ["Save", "Insert", "Update", "Write"]),
            ("Delete graph", ["Delete", "Remove"]),
            ("Export/import data", ["Export", "Import"]),
            ("Cache values", ["Cache"]),
            ("Secure local handling", ["Secure", "Secret", "Encrypt", "Credential"])
        };
        var searchable = symbols.Select(s => $"{s.Name} {s.FullName} {s.RelativePath}")
            .Concat(tests.Select(t => $"{t.ClassName} {t.MethodName} {t.RelativePath}"))
            .Concat(scan.Files.Select(f => f.RelativePath))
            .ToArray();

        return terms
            .Where(term => searchable.Any(value => term.Keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
            .Select(term => new CodeFlow(Guid.NewGuid(), scan.Project.Id, scan.ScanRun.Id, term.Name, CodeFlowType.CandidateDomainFlow, "Inferred from public symbols, tests, and source folder names", [], [], [], FlowConfidence.Low, ["Candidate domain flow inferred without AI; inspect files to confirm."], FlowOrigin.GenericFallback))
            .ToArray();
    }

    private static IReadOnlyList<TodoItem> SourceTodos(ProjectScanResult scan)
        => scan.Todos.Where(t => !IsGeneratedContextPath(t.RelativePath)).ToArray();

    private static bool IsGeneratedContextPath(string relativePath)
        => relativePath.StartsWith(".ai-context/", StringComparison.OrdinalIgnoreCase) ||
           relativePath.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase);

    private static string CleanTodoText(string text)
        => text.Length <= 180 ? text : text[..180] + "...";

    private static string InlineList(IEnumerable<string> values)
    {
        var materialized = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return materialized.Length == 0 ? "none detected" : string.Join(", ", materialized.Select(v => $"`{v}`"));
    }

    private static string DetectArchitectureStyle(ProjectScanResult scan)
        => scan.FolderRoles.Any(f => f.Role == "core") && scan.FolderRoles.Any(f => f.Role == "infrastructure")
            ? "Clean architecture style with core/application contracts separated from infrastructure adapters."
            : "General project structure. No strong architecture convention detected from folder roles yet.";

    private static string TechnologySummary(ProjectScanResult scan)
        => scan.Technologies.Count == 0 ? "no strongly detected technology stack yet" : string.Join(", ", scan.Technologies.Select(t => t.Kind).Distinct());

    private static string DetectedPurpose(ProjectScanResult scan, ProjectInstructionDocument? imported)
    {
        if (imported is not null && !string.IsNullOrWhiteSpace(imported.Title)) return imported.Title;
        var inferred = InferProjectPurpose(scan, imported, [], []);
        if (!string.IsNullOrWhiteSpace(inferred)) return inferred;
        return "Purpose was not explicitly detected. Use the project name, folders, and generated docs as context.";
    }

    private static string TopFolder(string relativePath)
    {
        var index = relativePath.IndexOf('/');
        return index < 0 ? "." : relativePath[..index];
    }

    private static string ListOrNone(IEnumerable<string> lines)
    {
        var materialized = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        return materialized.Length == 0 ? "- None detected" : string.Join(Environment.NewLine, materialized);
    }

    private static string ListOrFallback(IEnumerable<string> lines, string fallback)
    {
        var materialized = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        return materialized.Length == 0 ? fallback : string.Join(Environment.NewLine, materialized);
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string EscapeYamlList(string value) => $"\"{Escape(value)}\"";
}
