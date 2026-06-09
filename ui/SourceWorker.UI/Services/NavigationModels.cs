using SourceWorker.ApiClient;

namespace SourceWorker.UI.Services;

public enum NavigationScope { Global, Project, AdvancedGlobal, AdvancedProject }
public enum NavigationVisibility { Always, WhenProjectSelected, AdvancedOnly }

public sealed record AppNavigationItem(string Id, string Label, string Href, NavigationScope Scope, NavigationVisibility Visibility);
public sealed record ProjectNavigationItem(string Id, string Label, string RelativePath, NavigationScope Scope, NavigationVisibility Visibility);
public sealed record NavigationSection(string Title, IReadOnlyList<AppNavigationItem> Items);

public static class SourceWorkerNavigation
{
    public static IReadOnlyList<AppNavigationItem> GlobalPrimary { get; } =
    [
        new("home", "Home", "/", NavigationScope.Global, NavigationVisibility.Always),
        new("projects", "Projects", "/projects", NavigationScope.Global, NavigationVisibility.Always),
        new("providers", "Providers", "/providers", NavigationScope.Global, NavigationVisibility.Always)
    ];

    public static IReadOnlyList<AppNavigationItem> GlobalSecondary { get; } =
    [
        new("settings", "Settings", "/settings", NavigationScope.Global, NavigationVisibility.Always)
    ];

    public static IReadOnlyList<AppNavigationItem> AdvancedGlobal { get; } =
    [
        new("diagnostics", "Diagnostics", "/settings/diagnostics", NavigationScope.AdvancedGlobal, NavigationVisibility.AdvancedOnly),
        new("logs", "Logs", "/settings/logs", NavigationScope.AdvancedGlobal, NavigationVisibility.AdvancedOnly),
        new("dogfood", "Dogfood", "/dogfood", NavigationScope.AdvancedGlobal, NavigationVisibility.AdvancedOnly),
        new("all-jobs", "All Jobs", "/jobs", NavigationScope.AdvancedGlobal, NavigationVisibility.AdvancedOnly)
    ];

    public static IReadOnlyList<ProjectNavigationItem> ProjectPrimary { get; } =
    [
        new("overview", "Overview", "", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("handoff", "Handoff", "handoff", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("explanation", "Explanation", "explanation", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("ask", "Ask", "ai", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("docs", "Docs", "docs", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("packs", "Packs", "packs", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("markdown", "Markdown", "markdown", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("branches", "Branches", "branches", NavigationScope.Project, NavigationVisibility.WhenProjectSelected),
        new("ignore", "Ignore", "ignore", NavigationScope.Project, NavigationVisibility.WhenProjectSelected)
    ];

    public static IReadOnlyList<ProjectNavigationItem> ProjectAdvanced { get; } =
    [
        new("scan-health", "Scan Health", "scan-health", NavigationScope.AdvancedProject, NavigationVisibility.WhenProjectSelected),
        new("code", "Code", "code", NavigationScope.AdvancedProject, NavigationVisibility.WhenProjectSelected),
        new("artifacts", "Artifacts", "artifacts", NavigationScope.AdvancedProject, NavigationVisibility.WhenProjectSelected),
        new("jobs", "Jobs", "jobs", NavigationScope.AdvancedProject, NavigationVisibility.WhenProjectSelected),
        new("context", "Context Browser", "context", NavigationScope.AdvancedProject, NavigationVisibility.WhenProjectSelected)
    ];
}

public sealed record ProjectWorkspaceContext(
    Guid ProjectId,
    ProjectDto? Project,
    ScanCompletenessReportDto? ScanCompleteness,
    ContextFreshnessReport? Freshness,
    ArtifactLifecycleReport? Artifacts,
    IReadOnlyList<JobStatusDto> Jobs,
    AgentHandoffDto? Handoff,
    DateTimeOffset LoadedAt)
{
    public DateTimeOffset? LastScanUtc => Project?.LastScannedAt;
    public string HandoffReadiness => ScanCompleteness is { Score: >= 80 } && Handoff is not null ? "Ready" : "Needs scan";
    public string ArtifactHealth => Artifacts is null ? "Unknown" : AttentionCount == 0 ? "Healthy" : "Needs review";
    public int AttentionCount => (Artifacts?.ManuallyModifiedArtifacts.Count ?? 0) + (Artifacts?.MissingArtifacts.Count ?? 0) + (Artifacts?.StaleArtifacts.Count ?? 0) + (Artifacts?.DeprecatedArtifacts.Count ?? 0) + (Artifacts?.OrphanedArtifacts.Count ?? 0);
    public JobStatusDto? RunningJob => Jobs.FirstOrDefault(j => j.Status.Equals("Running", StringComparison.OrdinalIgnoreCase) || j.Status.Equals("Queued", StringComparison.OrdinalIgnoreCase));
}

public interface ISelectedProjectState
{
    Guid? SelectedProjectId { get; }
    ProjectWorkspaceContext? Current { get; }
    event Action? Changed;
    Task<ProjectWorkspaceContext> LoadAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class SelectedProjectState(ISourceWorkerApiClient api) : ISelectedProjectState
{
    public Guid? SelectedProjectId { get; private set; }
    public ProjectWorkspaceContext? Current { get; private set; }
    public event Action? Changed;

    public async Task<ProjectWorkspaceContext> LoadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        SelectedProjectId = projectId;
        var projects = await api.GetProjectsAsync(cancellationToken);
        var project = projects.FirstOrDefault(p => p.Id == projectId);
        var context = new ProjectWorkspaceContext(
            projectId,
            project,
            await Safe(() => api.GetScanCompletenessAsync(projectId, cancellationToken)),
            await Safe(() => api.GetContextStatusAsync(projectId, cancellationToken)),
            await Safe(() => api.GetArtifactsAsync(projectId, cancellationToken)),
            await SafeList(() => api.GetProjectJobsAsync(projectId, cancellationToken)),
            await Safe(() => api.GetAgentHandoffAsync(projectId, cancellationToken)),
            DateTimeOffset.Now);
        Current = context;
        Changed?.Invoke();
        return context;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProjectId is Guid id) await LoadAsync(id, cancellationToken);
    }

    private static async Task<T?> Safe<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch { return default; }
    }

    private static async Task<IReadOnlyList<T>> SafeList<T>(Func<Task<IReadOnlyList<T>>> action)
    {
        try { return await action(); }
        catch { return []; }
    }
}
