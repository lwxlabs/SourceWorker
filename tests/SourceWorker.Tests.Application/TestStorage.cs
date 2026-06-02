using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Infrastructure;

namespace SourceWorker.Tests.Application;

internal static class TestStorage
{
    public static IServiceCollection AddTestSourceWorkerInfrastructure(this IServiceCollection services)
        => services.AddSourceWorkerInfrastructure(options => options.DatabasePath = CreateDatabasePath());

    public static string CreateDatabasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-test-db", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "sourceworker-test.localite.db");
        var realPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceWorker", "sourceworker.localite.db");
        if (Path.GetFullPath(path).Equals(Path.GetFullPath(realPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test storage resolved to the real SourceWorker database.");
        }

        return path;
    }
}

