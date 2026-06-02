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
            .AddTestSourceWorkerInfrastructure()
            .AddSourceWorkerJobs(addHostedWorker: false)
            .BuildServiceProvider();

        var flow = services.GetRequiredService<IRequestFlow>();
        var first = await flow.Send(new AddProjectCommand(root, "App Test"));
        var second = await flow.Send(new AddProjectCommand(root, "Different"));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("App Test", second.Name);
    }

    [Fact]
    public async Task DeleteProjectCommand_removes_registered_project()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-delete-" + Guid.NewGuid());
        Directory.CreateDirectory(root);

        var services = new ServiceCollection()
            .AddSourceWorkerApplication()
            .AddTestSourceWorkerInfrastructure()
            .AddSourceWorkerJobs(addHostedWorker: false)
            .BuildServiceProvider();

        var flow = services.GetRequiredService<IRequestFlow>();
        var added = await flow.Send(new AddProjectCommand(root, "Delete Me"));
        var deleted = await flow.Send(new DeleteProjectCommand(added.Id));
        var projects = await flow.Send(new GetProjectsQuery());

        Assert.True(deleted);
        Assert.DoesNotContain(projects, p => p.Id == added.Id);
    }

    [Fact]
    public async Task GetProjectsQuery_orders_newest_projects_first()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-order-" + Guid.NewGuid());
        var oldRoot = Path.Combine(root, "Old");
        var newRoot = Path.Combine(root, "New");
        Directory.CreateDirectory(oldRoot);
        Directory.CreateDirectory(newRoot);

        var services = new ServiceCollection()
            .AddSourceWorkerApplication()
            .AddTestSourceWorkerInfrastructure()
            .AddSourceWorkerJobs(addHostedWorker: false)
            .BuildServiceProvider();

        var flow = services.GetRequiredService<IRequestFlow>();
        var oldProject = await flow.Send(new AddProjectCommand(oldRoot, "Old Project"));
        await Task.Delay(20);
        var newProject = await flow.Send(new AddProjectCommand(newRoot, "New Project"));

        var projects = await flow.Send(new GetProjectsQuery(IncludeMissing: true, Search: root));

        Assert.True(projects.Count >= 2);
        Assert.Equal(newProject.Id, projects[0].Id);
        Assert.Equal(oldProject.Id, projects[1].Id);
    }
}

