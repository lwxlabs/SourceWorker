using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

namespace SourceWorker.Tests.Application;

public sealed class PersistenceAndFreshnessTests
{
    [Fact]
    public async Task Persists_scan_files_and_generated_artifacts_and_detects_manual_edit()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-persist-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Program.cs"), "public class Program { public void Run() { } } // TODO: sample");

        var sp = new ServiceCollection()
            .AddSourceWorkerApplication()
            .AddTestSourceWorkerInfrastructure()
            .AddSourceWorkerJobs(addHostedWorker: false)
            .BuildServiceProvider();

        var flow = sp.GetRequiredService<LwX.Flow.IRequestFlow>();
        var project = await flow.Send(new AddProjectCommand(root, "Sample"));
        await flow.Send(new GenerateBasicContextCommand(project.Id));
        await flow.Send(new GenerateBasicContextCommand(project.Id));

        var scanRuns = sp.GetRequiredService<IScanRunRepository>();
        var files = sp.GetRequiredService<IIndexedFileRepository>();
        var artifacts = sp.GetRequiredService<IGeneratedArtifactRepository>();
        var latest = await scanRuns.GetLatestAsync(project.Id, CancellationToken.None);
        Assert.NotNull(latest);
        Assert.NotEmpty(await files.GetByScanRunAsync(latest!.Id, CancellationToken.None));
        var projectArtifacts = await artifacts.GetByProjectAsync(project.Id, CancellationToken.None);
        Assert.NotEmpty(projectArtifacts);
        Assert.Equal(projectArtifacts.Count, projectArtifacts.Select(a => a.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.NotEmpty(await sp.GetRequiredService<ICodeSymbolRepository>().GetByProjectAsync(project.Id, CancellationToken.None));
        Assert.NotEmpty(await sp.GetRequiredService<ICodeFlowRepository>().GetByProjectAsync(project.Id, CancellationToken.None));

        var backendReadme = Path.Combine(root, "instructions", "Result", "Backend", "README.md");
        await File.AppendAllTextAsync(backendReadme, "\nmanual edit");
        var status = await flow.Send(new GetContextStatusQuery(project.Id));
        Assert.Contains(status.ManuallyModifiedFiles, path => path.EndsWith("README.md", StringComparison.OrdinalIgnoreCase));
    }
}

