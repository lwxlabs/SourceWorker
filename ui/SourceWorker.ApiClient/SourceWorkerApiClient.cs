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

    public Task<DoctorReport> GetDoctorAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<DoctorReport>($"/api/projects/{projectId}/doctor", cancellationToken);

    public Task<ContextFreshnessReport> GetContextStatusAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<ContextFreshnessReport>($"/api/projects/{projectId}/context-status", cancellationToken);

    public Task<StorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default)
        => Required<StorageInfo>("/api/storage/info", cancellationToken);

    public Task<ArtifactLifecycleReport> GetArtifactsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<ArtifactLifecycleReport>($"/api/projects/{projectId}/artifacts", cancellationToken);

    public Task<CleanupPreviewReport> GetCleanupPreviewAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<CleanupPreviewReport>($"/api/projects/{projectId}/cleanup-preview", cancellationToken);

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

    public async Task<IReadOnlyList<ContextBrowserFile>> GetContextFilesAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<ContextBrowserFile[]>($"/api/projects/{projectId}/context-browser/files", cancellationToken) ?? [];

    public async Task<string> ReadContextAsync(Guid projectId, string relativePath, CancellationToken cancellationToken = default)
        => await httpClient.GetStringAsync($"/api/projects/{projectId}/context-browser/files/{EscapeRelativePath(relativePath)}", cancellationToken);

    public Task<GeneratedOutputSet> GetOutputsAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Required<GeneratedOutputSet>($"/api/projects/{projectId}/outputs", cancellationToken);

    public async Task<IReadOnlyList<ContextPackManifest>> GetPacksAsync(Guid projectId, CancellationToken cancellationToken = default)
        => await GetAsync<ContextPackManifest[]>($"/api/projects/{projectId}/packs", cancellationToken) ?? [];

    public Task<ContextPackManifest?> GetPackAsync(Guid projectId, string packId, CancellationToken cancellationToken = default)
        => GetAsync<ContextPackManifest>($"/api/projects/{projectId}/packs/{Uri.EscapeDataString(packId)}", cancellationToken);

    public async Task<string> GetPackContentAsync(Guid projectId, string packId, CancellationToken cancellationToken = default)
        => await httpClient.GetStringAsync($"/api/projects/{projectId}/packs/{Uri.EscapeDataString(packId)}/content", cancellationToken);

    public async Task<ExportRun> RunExportAsync(Guid projectId, string exportProfileId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"/api/projects/{projectId}/exports/{Uri.EscapeDataString(exportProfileId)}/run", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExportRun>(JsonOptions, cancellationToken) ?? throw new InvalidOperationException("Export response was empty.");
    }

    private async Task<T> Required<T>(string path, CancellationToken cancellationToken)
        => await GetAsync<T>(path, cancellationToken) ?? throw new InvalidOperationException($"Local API returned an empty response for '{path}'.");

    private async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        ApplyToken();
        using var response = await httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return default;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    private void ApplyToken()
    {
        if (string.IsNullOrWhiteSpace(options.BearerToken)) return;
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
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
