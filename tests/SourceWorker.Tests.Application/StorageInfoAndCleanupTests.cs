using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

namespace SourceWorker.Tests.Application;

public sealed class StorageInfoAndCleanupTests
{
    [Fact]
    public async Task Storage_info_and_cleanup_lifecycle_work()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-cleanup-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "instructions", "Result", "Backend"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Program.cs"), "");

        var sp = new ServiceCollection()
            .AddSourceWorkerApplication()
            .AddSourceWorkerInfrastructure()
            .AddSourceWorkerJobs(addHostedWorker: false)
            .BuildServiceProvider();

        var flow = sp.GetRequiredService<IRequestFlow>();
        var project = await flow.Send(new AddProjectCommand(root, "Cleanup"));
        await flow.Send(new GenerateBasicContextCommand(project.Id));

        var deprecated = Path.Combine(root, "instructions", "Result", "Backend", "03-workers-and-services.md");
        await File.WriteAllTextAsync(deprecated, """
            <!--
            GeneratedBy: SourceWorker
            -->
            old
            """);

        var info = await flow.Send(new GetStorageInfoQuery());
        var artifacts = await flow.Send(new GetArtifactsQuery(project.Id));
        var preview = await flow.Send(new GetCleanupPreviewQuery(project.Id));
        var dryRun = await flow.Send(new CleanupGeneratedCommand(project.Id, DryRun: true, Force: false));
        Assert.True(File.Exists(deprecated));
        var cleanup = await flow.Send(new CleanupGeneratedCommand(project.Id, DryRun: false, Force: true));

        Assert.Equal("Localite", info.Schema.Provider);
        Assert.Equal("table-backed", info.Schema.Mode);
        Assert.True(info.Schema.SchemaVersion >= 1);
        Assert.Contains(artifacts.DeprecatedArtifacts, p => p.EndsWith("03-workers-and-services.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(preview.SafeDeleteFiles, p => p.EndsWith("03-workers-and-services.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dryRun.DeletedFiles, p => p.EndsWith("03-workers-and-services.md", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cleanup.DeletedFiles, p => p.EndsWith("03-workers-and-services.md", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(deprecated));
    }
}
