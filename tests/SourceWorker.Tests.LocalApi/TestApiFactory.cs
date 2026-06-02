using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SourceWorker.Tests.LocalApi;

internal static class TestApiFactory
{
    public static WebApplicationFactory<Program> Create(bool? requireToken = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["SourceWorker:Storage:DatabasePath"] = CreateDatabasePath()
        };
        if (requireToken is not null)
        {
            values["SourceWorker:LocalApi:RequireToken"] = requireToken.Value ? "true" : "false";
        }

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values)));
    }

    private static string CreateDatabasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-localapi-test-db", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "sourceworker-localapi-test.localite.db");
        var realPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceWorker", "sourceworker.localite.db");
        if (Path.GetFullPath(path).Equals(Path.GetFullPath(realPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("LocalApi test storage resolved to the real SourceWorker database.");
        }

        return path;
    }
}
