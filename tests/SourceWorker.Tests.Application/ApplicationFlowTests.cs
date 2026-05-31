using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

namespace SourceWorker.Tests.Application;

public sealed class ApplicationFlowTests
{
    [Fact]
    public async Task AddProjectCommand_adds_project_once_per_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-app-" + Guid.NewGuid());
        Directory.CreateDirectory(root);

        var services = new ServiceCollection()
            .AddSourceWorkerApplication()
            .AddSourceWorkerInfrastructure()
            .AddSourceWorkerJobs(addHostedWorker: false)
            .BuildServiceProvider();

        var flow = services.GetRequiredService<IRequestFlow>();
        var first = await flow.Send(new AddProjectCommand(root, "App Test"));
        var second = await flow.Send(new AddProjectCommand(root, "Different"));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("App Test", second.Name);
    }
}
