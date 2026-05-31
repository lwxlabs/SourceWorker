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

    private sealed class StubHandler(Func<HttpRequestMessage, string> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseFactory(request), System.Text.Encoding.UTF8, "application/json")
            });
    }
}
