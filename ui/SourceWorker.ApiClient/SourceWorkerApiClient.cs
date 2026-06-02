using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SourceWorker.ApiClient;

public sealed class SourceWorkerApiClient(HttpClient httpClient, SourceWorkerApiClientOptions options) : ISourceWorkerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public Task<HealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default)
        => GetAsync<HealthResponse>("/health", cancellationToken);

    public async Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken cancellationToken = default)
        => await GetAsync<ProjectDto[]>("/api/projects", cancellationToken) ?? [];

    public async Task<IReadOnlyList<ProjectSummaryDto>> GetProjectsSummaryAsync(bool includeTestProjects = false, bool includeMissing = false, string? search = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = $"/api/projects?includeTestProjects={includeTestProjects.ToString().ToLower()}&includeMissing={includeMissing.ToString().ToLower()}";
        if (!string.IsNullOrEmpty(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (limit.HasValue) query += $"&limit={limit.Value}";
        
        return await GetAsync<ProjectSummaryDto[]>(query, cancellationToken) ?? [];
    }

    public async Task<ProjectDto> RegisterProjectAsync(string path, string? name = null, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync("/api/projects", new { path, name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateProjectResponse>(JsonOptions, cancellationToken);
        return result?.Project ?? throw new InvalidOperationException("Failed to register project.");
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.DeleteAsync($"/api/projects/{projectId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<StartScanResponse> StartProjectScanAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/projects/{projectId}/scan", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StartScanResponse>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Failed to start scan.");
    }

    public Task<ApiRuntimeInfoDto> GetRuntimeInfoAsync(CancellationToken cancellationToken = default)
        => Required<ApiRuntimeInfoDto>("/api/runtime/info", cancellationToken);

    public Task<ApiSecurityInfoDto> GetSecurityInfoAsync(CancellationToken cancellationToken = default)
        => Required<ApiSecurityInfoDto>("/api/runtime/security", cancellationToken);

    public async Task<string> RotateTokenAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync("/api/runtime/token/rotate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
        return result.GetProperty("token").GetString() ?? "";
    }

    public Task<VersionInfoDto> GetVersionAsync(CancellationToken cancellationToken = default)
        => Required<VersionInfoDto>("/api/version", cancellationToken);

    public async Task<ApiConnectionStatus> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var runtime = await GetRuntimeInfoAsync(cancellationToken);
            return new ApiConnectionStatus(true, null, runtime);
        }
        catch (Exception ex)
        {
            return new ApiConnectionStatus(false, ex.Message);
        }
    }

    public async Task<IReadOnlyList<JobStatusDto>> GetJobsAsync(CancellationToken cancellationToken = default)
        => await GetAsync<JobStatusDto[]>("/api/jobs", cancellationToken) ?? [];

    public async Task<IReadOnlyList<JobStatusDto>> GetProjectJobsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<JobStatusDto[]>($"/api/projects/{projectId}/jobs", cancellationToken) ?? [];

    public Task<JobStatusDto?> GetJobStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
        => GetAsync<JobStatusDto>($"/api/jobs/{jobId}", cancellationToken);

    public async Task<IReadOnlyList<string>> GetJobLogsAsync(Guid jobId, CancellationToken cancellationToken = default)
        => await GetAsync<string[]>($"/api/jobs/{jobId}/logs", cancellationToken) ?? [];

    public Task<DoctorReport> GetDoctorAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<DoctorReport>($"/api/projects/{projectId}/doctor", cancellationToken);

    public Task<ContextFreshnessReport> GetContextStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<ContextFreshnessReport>($"/api/projects/{projectId}/context-status", cancellationToken);

    public Task<AgentHandoffDto> GetAgentHandoffAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<AgentHandoffDto>($"/api/projects/{projectId}/agent-handoff", cancellationToken);

    public Task<ScanCompletenessReportDto> GetScanCompletenessAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<ScanCompletenessReportDto>($"/api/projects/{projectId}/scan-completeness", cancellationToken);

    public Task<StorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default)
        => Required<StorageInfo>("/api/storage/info", cancellationToken);

    public Task<ArtifactLifecycleReport> GetArtifactsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<ArtifactLifecycleReport>($"/api/projects/{projectId}/artifacts", cancellationToken);

    public Task<CleanupPreviewDto> GetCleanupPreviewAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<CleanupPreviewDto>($"/api/projects/{projectId}/cleanup-preview", cancellationToken);

    public async Task<CleanupGeneratedResponse> CleanupGeneratedAsync(Guid projectId, bool dryRun = false, bool force = false, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/cleanup-generated", new { dryRun, force }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CleanupGeneratedResponse>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Failed to cleanup.");
    }

    public async Task<IReadOnlyList<CodeSymbol>> GetSymbolsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<CodeSymbol[]>($"/api/projects/{projectId}/symbols", cancellationToken) ?? [];

    public async Task<IReadOnlyList<CodeFlow>> GetFlowsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<CodeFlow[]>($"/api/projects/{projectId}/flows", cancellationToken) ?? [];

    public async Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<RouteInfo[]>($"/api/projects/{projectId}/routes", cancellationToken) ?? [];

    public async Task<IReadOnlyList<ServiceRegistration>> GetServiceRegistrationsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<ServiceRegistration[]>($"/api/projects/{projectId}/service-registrations", cancellationToken) ?? [];

    public async Task<IReadOnlyList<ConfigurationUsage>> GetConfigurationUsagesAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<ConfigurationUsage[]>($"/api/projects/{projectId}/configuration-usages", cancellationToken) ?? [];

    public async Task<IReadOnlyList<TestInfo>> GetTestsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<TestInfo[]>($"/api/projects/{projectId}/tests", cancellationToken) ?? [];

    public Task<ContextBrowserSummary> GetContextBrowserAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<ContextBrowserSummary>($"/api/projects/{projectId}/context-browser", cancellationToken);

    public async Task<IReadOnlyList<ContextBrowserFile>> GetContextFilesAsync(Guid projectId, bool showTechnical = false, bool showExports = false, CancellationToken cancellationToken = default)
        => await GetAsync<ContextBrowserFile[]>($"/api/projects/{projectId}/context-browser/files?showTechnical={showTechnical.ToString().ToLowerInvariant()}&showExports={showExports.ToString().ToLowerInvariant()}", cancellationToken) ?? [];

    public async Task<string> ReadContextAsync(Guid projectId, string relativePath, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        return await httpClient.GetStringAsync($"/api/projects/{projectId}/context-browser/files/{EscapeRelativePath(relativePath)}", cancellationToken);
    }

    public Task<GeneratedOutputSet> GetOutputsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<GeneratedOutputSet>($"/api/projects/{projectId}/outputs", cancellationToken);

    public async Task<IReadOnlyList<ContextPackManifest>> GetPacksAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<ContextPackManifest[]>($"/api/projects/{projectId}/packs", cancellationToken) ?? [];

    public Task<ContextPackManifest?> GetPackAsync(Guid projectId, string packId, CancellationToken cancellationToken = default)
        => GetAsync<ContextPackManifest>($"/api/projects/{projectId}/packs/{Uri.EscapeDataString(packId)}", cancellationToken);

    public async Task<string> GetPackContentAsync(Guid projectId, string packId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        return await httpClient.GetStringAsync($"/api/projects/{projectId}/packs/{Uri.EscapeDataString(packId)}/content", cancellationToken);
    }

    public async Task<ExportPackResponse> ExportPackToDefaultAsync(Guid projectId, string packId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/projects/{projectId}/packs/{Uri.EscapeDataString(packId)}/export-default", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExportPackResponse>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Failed to export pack.");
    }

    public async Task<RunExportResponse> RunExportAsync(Guid projectId, string exportProfileId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/projects/{projectId}/exports/{Uri.EscapeDataString(exportProfileId)}/run", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RunExportResponse>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Export response was empty.");
    }

    public async Task<IReadOnlyList<AiProviderStatusDto>> GetAiProvidersAsync(CancellationToken cancellationToken = default)
        => await GetAsync<AiProviderStatusDto[]>("/api/ai/providers", cancellationToken) ?? [];

    public async Task<IReadOnlyList<AiProviderStatusDto>> GetAiProviderStatusesAsync(CancellationToken cancellationToken = default)
        => await GetAsync<AiProviderStatusDto[]>("/api/ai/providers/status", cancellationToken) ?? [];

    public async Task<AiProviderConfigurationResultDto> ConfigureAiProviderAsync(AiProviderConfigurationDto configuration, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync("/api/ai/providers/configure", configuration, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiProviderConfigurationResultDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Provider configuration response was empty.");
    }

    public async Task<AiProviderValidationResultDto> ValidateAiProviderAsync(AiProviderConfigurationDto configuration, int maxOutputTokens = 1200, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync("/api/ai/providers/validate", new { configuration.Kind, configuration.Model, configuration.BaseUrl, configuration.ApiKey, MaxOutputTokens = maxOutputTokens }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiProviderValidationResultDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Provider validation response was empty.");
    }

    public async Task<AiProviderStatusDto> TestAiProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/ai/providers/{Uri.EscapeDataString(providerId)}/test", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiProviderStatusDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Provider test response was empty.");
    }

    public async Task<AiProviderConnectionDiagnosticDto> DiagnoseAiProviderAsync(string providerId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/ai/providers/{Uri.EscapeDataString(providerId)}/diagnose", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiProviderConnectionDiagnosticDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Provider diagnostic response was empty.");
    }

    public async Task<IReadOnlyList<ContextPackManifest>> GetAiPacksAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<ContextPackManifest[]>($"/api/projects/{projectId}/ai/packs", cancellationToken) ?? [];

    public async Task<AiPromptPreviewResponse> PreviewAiPromptAsync(Guid projectId, AiPromptPreviewRequest request, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/ai/prompt-preview", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiPromptPreviewResponse>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Prompt preview response was empty.");
    }

    public async Task<AiRunDetailDto> StartAiRunAsync(Guid projectId, AiRunRequest request, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/ai/runs", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiRunDetailDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("AI run response was empty.");
    }

    public async Task<IReadOnlyList<AiRunSummaryDto>> GetAiRunsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<AiRunSummaryDto[]>($"/api/projects/{projectId}/ai/runs", cancellationToken) ?? [];

    public Task<AiRunDetailDto?> GetAiRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken = default)
        => GetAsync<AiRunDetailDto>($"/api/projects/{projectId}/ai/runs/{runId}", cancellationToken);

    public Task<AiSettingsDto> GetAiSettingsAsync(CancellationToken cancellationToken = default)
        => Required<AiSettingsDto>("/api/ai/settings", cancellationToken);

    public async Task<AiSettingsDto> UpdateAiSettingsAsync(AiSettingsDto settings, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PutAsJsonAsync("/api/ai/settings", settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiSettingsDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("AI settings response was empty.");
    }

    public async Task<bool> DeleteAiRunsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.DeleteAsync($"/api/projects/{projectId}/ai/runs", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAiRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.DeleteAsync($"/api/projects/{projectId}/ai/runs/{runId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public Task<AiUsageSummaryDto> GetAiUsageAsync(CancellationToken cancellationToken = default)
        => Required<AiUsageSummaryDto>("/api/ai/usage", cancellationToken);

    public Task<AiUsageSummaryDto> GetProjectAiUsageAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<AiUsageSummaryDto>($"/api/projects/{projectId}/ai/usage", cancellationToken);

    public Task<AiContextPackQualityReportDto> GetAiPackQualityAsync(Guid projectId, string packId, CancellationToken cancellationToken = default)
        => Required<AiContextPackQualityReportDto>($"/api/projects/{projectId}/ai/packs/{Uri.EscapeDataString(packId)}/quality", cancellationToken);

    public Task<AiExplanationOptionsDto> GetAiExplanationOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<AiExplanationOptionsDto>($"/api/projects/{projectId}/ai/explanation/options", cancellationToken);

    public async Task<AiExplanationPreviewDto> PreviewAiExplanationAsync(Guid projectId, AiExplanationRequestDto request, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/ai/explanation/preview", request with { ProjectId = projectId }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiExplanationPreviewDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("AI explanation preview response was empty.");
    }

    public async Task<AiExplanationResultDto> RunAiExplanationAsync(Guid projectId, AiExplanationRequestDto request, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/ai/explanation/run", request with { ProjectId = projectId }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiExplanationResultDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("AI explanation run response was empty.");
    }

    public async Task<IReadOnlyList<AiExplanationResultDto>> GetAiExplanationRunsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<AiExplanationResultDto[]>($"/api/projects/{projectId}/ai/explanation/runs", cancellationToken) ?? [];

    public Task<AiExplanationResultDto?> GetAiExplanationRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken = default)
        => GetAsync<AiExplanationResultDto>($"/api/projects/{projectId}/ai/explanation/runs/{runId}", cancellationToken);

    public async Task<AiExplanationSaveResultDto> SaveAiExplanationAsync(Guid projectId, Guid runId, bool confirmOverwrite = false, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/ai/explanation/runs/{runId}/save", new AiExplanationSaveRequestDto(confirmOverwrite), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiExplanationSaveResultDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("AI explanation save response was empty.");
    }

    public async Task<bool> DeleteAiExplanationRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.DeleteAsync($"/api/projects/{projectId}/ai/explanation/runs/{runId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public Task<DogfoodProjectReadinessDto> GetDogfoodStatusAsync(CancellationToken cancellationToken = default)
        => Required<DogfoodProjectReadinessDto>("/api/dogfood/status", cancellationToken);

    public Task<DogfoodProjectReadinessDto> GetProjectDogfoodStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<DogfoodProjectReadinessDto>($"/api/projects/{projectId}/dogfood/status", cancellationToken);

    public Task<DiagnosticsSummaryDto> GetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default)
        => Required<DiagnosticsSummaryDto>("/api/diagnostics/summary", cancellationToken);

    public async Task<DiagnosticsExportDto> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync("/api/diagnostics/export", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiagnosticsExportDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Diagnostics export response was empty.");
    }

    public async Task<IReadOnlyList<DogfoodSessionDto>> GetDogfoodSessionsAsync(CancellationToken cancellationToken = default)
        => await GetAsync<DogfoodSessionDto[]>("/api/dogfood/sessions", cancellationToken) ?? [];

    public async Task<DogfoodSessionDto> StartDogfoodSessionAsync(IReadOnlyList<DogfoodSessionProjectDto> projects, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync("/api/dogfood/sessions", new StartDogfoodSessionRequestDto(projects), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DogfoodSessionDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Dogfood session response was empty.");
    }

    public Task<DogfoodSessionDto?> GetDogfoodSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        => GetAsync<DogfoodSessionDto>($"/api/dogfood/sessions/{sessionId}", cancellationToken);

    public async Task<DogfoodSessionDto> CompleteDogfoodStepAsync(Guid sessionId, string stepId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/dogfood/sessions/{sessionId}/steps/{Uri.EscapeDataString(stepId)}/complete", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DogfoodSessionDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Dogfood step response was empty.");
    }

    public async Task<DogfoodSessionDto> FinishDogfoodSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/dogfood/sessions/{sessionId}/finish", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DogfoodSessionDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Dogfood finish response was empty.");
    }

    public async Task<DiagnosticsExportDto> ExportDogfoodSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync($"/api/dogfood/sessions/{sessionId}/export", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiagnosticsExportDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Dogfood export response was empty.");
    }

    public async Task<IReadOnlyList<DogfoodSessionIssueDto>> GetFrictionIssuesAsync(CancellationToken cancellationToken = default)
        => await GetAsync<DogfoodSessionIssueDto[]>("/api/dogfood/issues", cancellationToken) ?? [];

    public async Task<DogfoodSessionIssueDto> ReportFrictionIssueAsync(DogfoodSessionIssueDto issue, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync("/api/dogfood/issues", issue, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DogfoodSessionIssueDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Issue response was empty.");
    }

    public async Task<DogfoodSessionIssueDto?> UpdateFrictionIssueStatusAsync(Guid issueId, string status, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PutAsJsonAsync($"/api/dogfood/issues/{issueId}/status", new UpdateIssueStatusRequestDto(status), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DogfoodSessionIssueDto>(JsonOptions, cancellationToken);
    }

    public async Task<DiagnosticsExportDto> ExportFrictionIssuesAsync(CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsync("/api/dogfood/issues/export", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiagnosticsExportDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Issues export response was empty.");
    }

    public async Task<IReadOnlyList<NextActionDto>> GetNextActionsAsync(CancellationToken cancellationToken = default)
        => await GetAsync<NextActionDto[]>("/api/next-actions", cancellationToken) ?? [];

    public async Task<IReadOnlyList<NextActionDto>> GetProjectNextActionsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<NextActionDto[]>($"/api/projects/{projectId}/next-actions", cancellationToken) ?? [];

    public async Task<ProjectPathValidationResultDto> ValidateProjectPathAsync(string path, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync("/api/projects/validate-path", new { path }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectPathValidationResultDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Path validation response was empty.");
    }

    public async Task<IReadOnlyList<RecentProjectPathDto>> GetRecentProjectPathsAsync(CancellationToken cancellationToken = default)
        => await GetAsync<RecentProjectPathDto[]>("/api/projects/recent-paths", cancellationToken) ?? [];

    public Task<ProjectAiSettingsDto> GetProjectAiSettingsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<ProjectAiSettingsDto>($"/api/projects/{projectId}/ai/settings", cancellationToken);

    public async Task<ProjectAiSettingsDto> UpdateProjectAiSettingsAsync(ProjectAiSettingsDto settings, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PutAsJsonAsync($"/api/projects/{settings.ProjectId}/ai/settings", settings, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectAiSettingsDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Project AI settings response was empty.");
    }

    public async Task<QuickAskResultDto> PreviewQuickAskAsync(Guid projectId, QuickAskRequestDto request, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/ai/quick-ask/preview", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuickAskResultDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Quick ask preview response was empty.");
    }

    public async Task<QuickAskResultDto> RunQuickAskAsync(Guid projectId, QuickAskRequestDto request, CancellationToken cancellationToken = default)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.PostAsJsonAsync($"/api/projects/{projectId}/ai/quick-ask/run", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuickAskResultDto>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Quick ask response was empty.");
    }

    public async Task<IReadOnlyList<LogEntryDto>> GetLogsAsync(CancellationToken cancellationToken = default)
        => await GetAsync<LogEntryDto[]>("/api/logs", cancellationToken) ?? [];

    private async Task<T> Required<T>(string path, CancellationToken cancellationToken)
        => await GetAsync<T>(path, cancellationToken) ?? throw new InvalidOperationException($"Local API returned an empty response for '{path}'.");

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        await ApplyTokenAsync();
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private async Task ApplyTokenAsync()
    {
        string? token = options.BearerToken;
        if (options.TokenProvider != null)
        {
            token = await options.TokenProvider.GetTokenAsync();
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private static string EscapeRelativePath(string relativePath)
        => string.Join("/", relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new LenientStringConverter());
        return options;
    }

    private sealed class LenientStringConverter : System.Text.Json.Serialization.JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var integer) ? integer.ToString(System.Globalization.CultureInfo.InvariantCulture) : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => JsonDocument.ParseValue(ref reader).RootElement.GetRawText()
            };

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
