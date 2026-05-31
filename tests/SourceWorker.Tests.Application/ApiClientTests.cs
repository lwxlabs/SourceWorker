using System.Net;
using SourceWorker.ApiClient;

namespace SourceWorker.Tests.Application;

public sealed class ApiClientTests
{
    [Fact]
    public async Task Api_client_deserializes_health_storage_and_projects()
    {
        using var http = new HttpClient(new StubHandler(request => request.RequestUri?.AbsolutePath switch
        {
            "/health" => """{"status":"ok","service":"SourceWorker.LocalApi"}""",
            "/api/storage/info" => """
                {"schema":{"provider":"Localite","mode":"table-backed","storagePath":"local.db","schemaVersion":1,"createdUtc":"2026-05-31T00:00:00Z","lastMigratedUtc":"2026-05-31T00:00:00Z","appStorageFormatVersion":"3"},"projectCount":1,"scanRunCount":2,"indexedFileCount":3,"generatedArtifactCount":4,"jobCount":5,"instructionCount":6,"recipeCount":7,"repositoryHealth":true,"codeSymbolCount":8,"codeFlowCount":9,"routeCount":10,"serviceRegistrationCount":11,"configurationUsageCount":12,"testInfoCount":13}
                """,
            "/api/projects" => """[{"id":"11111111-1111-1111-1111-111111111111","name":"Sample","rootPath":"C:\\Sample","createdAt":"2026-05-31T00:00:00Z","lastScannedAt":null}]""",
            _ => throw new InvalidOperationException($"Unexpected path {request.RequestUri}")
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new SourceWorkerApiClient(http, new SourceWorkerApiClientOptions());

        var health = await client.GetHealthAsync();
        var storage = await client.GetStorageInfoAsync();
        var projects = await client.GetProjectsAsync();

        Assert.Equal("ok", health?.Status);
        Assert.Equal("Localite", storage.Schema.Provider);
        Assert.Equal(8, storage.CodeSymbolCount);
        Assert.Single(projects);
        Assert.Equal("Sample", projects[0].Name);
    }

    [Fact]
    public async Task Api_client_can_register_project()
    {
        using var http = new HttpClient(new StubHandler(request => 
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/projects")
                return """{"project":{"id":"11111111-1111-1111-1111-111111111111","name":"NewProject","rootPath":"C:\\New","createdAt":"2026-05-31T00:00:00Z","lastScannedAt":null}}""";
            throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new SourceWorkerApiClient(http, new SourceWorkerApiClientOptions());
        
        var project = await client.RegisterProjectAsync("C:\\New", "NewProject");
        
        Assert.Equal("NewProject", project.Name);
        Assert.Equal("C:\\New", project.RootPath);
    }

    [Fact]
    public async Task Api_client_can_start_scan()
    {
        var jobId = Guid.NewGuid();
        using var http = new HttpClient(new StubHandler(request => 
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath.EndsWith("/scan") == true)
                return $$"""{"jobId":"{{jobId}}"}""";
            throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new SourceWorkerApiClient(http, new SourceWorkerApiClientOptions());
        
        var response = await client.StartProjectScanAsync(Guid.NewGuid());
        
        Assert.Equal(jobId, response.JobId);
    }

    [Fact]
    public async Task Api_client_can_run_cleanup()
    {
        var projectId = Guid.NewGuid();
        using var http = new HttpClient(new StubHandler(request => 
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath.EndsWith("/cleanup-generated") == true)
                return $$"""{"projectId":"{{projectId}}","dryRun":false,"deletedFiles":["file1.md"],"skippedFiles":[]}""";
            throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new SourceWorkerApiClient(http, new SourceWorkerApiClientOptions());
        
        var response = await client.CleanupGeneratedAsync(projectId, dryRun: false, force: false);
        
        Assert.Equal(projectId, response.ProjectId);
        Assert.Single(response.DeletedFiles);
        Assert.Equal("file1.md", response.DeletedFiles[0]);
    }

    [Fact]
    public async Task Api_client_can_run_export()
    {
        using var http = new HttpClient(new StubHandler(request => 
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath.Contains("/exports/") == true)
                return """{"exportProfileId":"test","outputPath":"out","files":["f1.md"]}""";
            throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new SourceWorkerApiClient(http, new SourceWorkerApiClientOptions());
        
        var response = await client.RunExportAsync(Guid.NewGuid(), "test");
        
        Assert.Equal("test", response.ExportProfileId);
        Assert.Single(response.Files);
    }

    [Fact]
    public async Task ApiClient_ai_methods_include_bearer_token_and_parse_responses()
    {
        var projectId = Guid.NewGuid();
        var sawAuth = false;
        using var http = new HttpClient(new StubHandler(request =>
        {
            sawAuth = request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "test-token";
            return request.RequestUri?.AbsolutePath switch
            {
                "/api/ai/providers" => """[{"providerId":"Ollama","kind":1,"status":0,"message":"Provider is not configured.","checkedUtc":"2026-05-31T00:00:00Z","maskedApiKey":null}]""",
                "/api/ai/providers/configure" => """{"id":"11111111-1111-1111-1111-111111111111","kind":1,"name":"Ollama","model":"llama3.1","baseUrl":"http://localhost:11434","isEnabled":true,"apiKeySecretName":null,"maskedApiKey":null,"updatedUtc":"2026-05-31T00:00:00Z"}""",
                "/api/ai/providers/validate" => """{"providerId":"Ollama","kind":1,"isValid":true,"issues":[],"setupState":{"isConfigured":true,"hasModel":true,"hasSecretWhenRequired":true,"hasValidBaseUrl":true,"isLocalProvider":true,"maskedSecretHint":null}}""",
                "/api/ai/providers/Ollama/test" => """{"providerId":"Ollama","kind":1,"status":1,"message":"ok","checkedUtc":"2026-05-31T00:00:00Z","maskedApiKey":null}""",
                "/api/ai/providers/Ollama/diagnose" => """{"providerId":"Ollama","kind":1,"status":1,"summary":"ok","suggestedActions":[],"error":null,"checkedUtc":"2026-05-31T00:00:00Z"}""",
                "/api/ai/settings" => """{"storeFullPrompt":false,"storeOutput":true,"maxRunsPerProject":100,"deleteFailedRunsAfterDays":30}""",
                "/api/ai/usage" => """{"projectId":null,"totalRuns":0,"successfulRuns":0,"failedRuns":0,"lastRunUtc":null,"mostUsedProvider":null,"mostUsedPack":null,"estimatedTokensUsed":0,"estimatedKnownCostUsd":null,"unknownCostCount":0,"providersConfigured":1,"failedProviderChecks":0}""",
                "/api/dogfood/status" => """{"projectId":null,"projectName":null,"checklist":{"items":[],"status":"Ready","nextActions":[]},"score":100}""",
                "/api/diagnostics/summary" => """{"appVersion":"2.2","apiVersion":"2.2","storage":{"schema":{"provider":"Localite","mode":"table-backed","storagePath":"local.db","schemaVersion":1,"createdUtc":"2026-05-31T00:00:00Z","lastMigratedUtc":"2026-05-31T00:00:00Z","appStorageFormatVersion":"3"},"projectCount":0,"scanRunCount":0,"indexedFileCount":0,"generatedArtifactCount":0,"jobCount":0,"instructionCount":0,"recipeCount":0,"repositoryHealth":true},"runtime":{"selectedHost":"localhost","selectedPort":5000,"baseUrl":"http://localhost:5000","tokenEnabled":true,"tokenCreatedUtc":null,"allowedOrigins":[],"dashboardOrigin":null,"processId":1,"startupUtc":"2026-05-31T00:00:00Z","environmentName":"Test","appVersion":"2.2","apiVersion":"2.2"},"providerStatuses":[],"projectCount":0,"jobCount":0,"aiRunCount":0,"recentErrors":[],"generatedUtc":"2026-05-31T00:00:00Z"}""",
                "/api/diagnostics/export" => """{"fileName":"diag.json","content":"{}","generatedUtc":"2026-05-31T00:00:00Z"}""",
                var path when path == $"/api/projects/{projectId}/ai/packs/backend-docs/quality" => $$"""{"projectId":"{{projectId}}","packId":"backend-docs","readinessScore":100,"isRecommended":true,"issues":[]}""",
                var path when path == $"/api/projects/{projectId}/ai/usage" => $$"""{"projectId":"{{projectId}}","totalRuns":1,"successfulRuns":1,"failedRuns":0,"lastRunUtc":"2026-05-31T00:00:00Z","mostUsedProvider":"Ollama","mostUsedPack":"backend-docs","estimatedTokensUsed":30,"estimatedKnownCostUsd":null,"unknownCostCount":1,"providersConfigured":0,"failedProviderChecks":0}""",
                var path when path == $"/api/projects/{projectId}/dogfood/status" => $$"""{"projectId":"{{projectId}}","projectName":"Sample","checklist":{"items":[],"status":"Ready","nextActions":[]},"score":100}""",
                var path when path == $"/api/projects/{projectId}/ai/prompt-preview" => $$"""{"projectId":"{{projectId}}","projectName":"Sample","providerId":"Ollama","providerKind":1,"model":"llama3.1","packId":"backend-docs","includedFiles":[],"missingFiles":[],"freshnessWarnings":[],"safetyWarnings":[],"tokenEstimate":{"promptTokens":10,"maxOutputTokens":20,"totalTokens":30},"costEstimate":{"promptCostUsd":null,"outputCostUsd":null,"totalCostUsd":null,"currency":"USD"},"sections":[],"includedFileDetails":[],"templateId":"general-project-question","contextWindowTokens":null,"contextWindowUsagePercent":0,"isTooLarge":false,"promptText":"hello","promptHash":"abc"}""",
                var path when path == $"/api/projects/{projectId}/ai/runs" && request.Method == HttpMethod.Post => $$"""{"id":"22222222-2222-2222-2222-222222222222","projectId":"{{projectId}}","providerId":"Ollama","providerKind":1,"model":"llama3.1","packId":"backend-docs","userInstruction":null,"promptHash":"abc","storedPromptText":false,"promptPreview":null,"includedFiles":[],"safetyWarnings":[],"tokenEstimate":{"promptTokens":10,"maxOutputTokens":20,"totalTokens":30},"status":2,"createdUtc":"2026-05-31T00:00:00Z","completedUtc":"2026-05-31T00:00:01Z","error":null,"outputText":"ok","outputHash":"hash"}""",
                var path when path == $"/api/projects/{projectId}/ai/runs" => "[]",
                _ => throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}")
            };
        }))
        { BaseAddress = new Uri("http://localhost:5000") };
        var client = new SourceWorkerApiClient(http, new SourceWorkerApiClientOptions { BearerToken = "test-token" });

        var providers = await client.GetAiProvidersAsync();
        var config = await client.ConfigureAiProviderAsync(new AiProviderConfigurationDto(AiProviderKind.Ollama, "llama3.1", "http://localhost:11434", null));
        var validation = await client.ValidateAiProviderAsync(new AiProviderConfigurationDto(AiProviderKind.Ollama, "llama3.1", "http://localhost:11434", null));
        var health = await client.TestAiProviderAsync("Ollama");
        var diagnostic = await client.DiagnoseAiProviderAsync("Ollama");
        var preview = await client.PreviewAiPromptAsync(projectId, new AiPromptPreviewRequest("Ollama", "backend-docs", "Summarize"));
        var run = await client.StartAiRunAsync(projectId, new AiRunRequest("Ollama", "backend-docs", null, true));
        var runs = await client.GetAiRunsAsync(projectId);
        var settings = await client.GetAiSettingsAsync();
        var usage = await client.GetProjectAiUsageAsync(projectId);
        var quality = await client.GetAiPackQualityAsync(projectId, "backend-docs");
        var dogfood = await client.GetProjectDogfoodStatusAsync(projectId);
        var diagnostics = await client.GetDiagnosticsSummaryAsync();

        Assert.True(sawAuth);
        Assert.Single(providers);
        Assert.Equal("llama3.1", config.Model);
        Assert.True(validation.IsValid);
        Assert.Equal(AiProviderStatus.Healthy, health.Status);
        Assert.Equal(AiProviderStatus.Healthy, diagnostic.Status);
        Assert.Equal("hello", preview.PromptText);
        Assert.Equal("ok", run.OutputText);
        Assert.Empty(runs);
        Assert.True(settings.StoreOutput);
        Assert.Equal(1, usage.TotalRuns);
        Assert.True(quality.IsRecommended);
        Assert.Equal(100, dogfood.Score);
        Assert.Equal("2.2", diagnostics.AppVersion);
    }

    [Fact]
    public async Task Api_client_can_get_runtime_info()
    {
        using var http = new HttpClient(new StubHandler(request => 
        {
            if (request.RequestUri?.AbsolutePath == "/api/runtime/info")
                return """{"selectedHost":"localhost","selectedPort":5000,"baseUrl":"http://localhost:5000","tokenEnabled":true,"tokenCreatedUtc":"2026-05-31T00:00:00Z","allowedOrigins":["*"],"dashboardOrigin":"*","processId":123,"startupUtc":"2026-05-31T00:00:00Z","environmentName":"Development","appVersion":"2.0.0","apiVersion":"2.0"}""";
            throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new SourceWorkerApiClient(http, new SourceWorkerApiClientOptions());
        
        var runtime = await client.GetRuntimeInfoAsync();
        
        Assert.Equal("2.0.0", runtime.AppVersion);
        Assert.Equal("Development", runtime.EnvironmentName);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseFactory(request), System.Text.Encoding.UTF8, "application/json")
            });
    }
}
