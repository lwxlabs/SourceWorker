using LwX.Flow;
using SourceWorker.Application;
using SourceWorker.Contracts;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services
    .AddSourceWorkerApplication()
    .AddSourceWorkerInfrastructure()
    .AddSourceWorkerJobs();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "SourceWorker.LocalApi" }));

app.MapGet("/api/projects", async (IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetProjectsQuery(), cancellationToken)));

app.MapPost("/api/projects", async (AddProjectRequest request, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(new CreateProjectResponse(await flow.Send(new AddProjectCommand(request.Path, request.Name), cancellationToken))));

app.MapPost("/api/projects/{id:guid}/scan", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Accepted($"/api/projects/{id}/scan", await flow.Send(new StartProjectScanCommand(id), cancellationToken)));

app.MapGet("/api/jobs/{id:guid}", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) =>
{
    var job = await flow.Send(new GetJobStatusQuery(id), cancellationToken);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.Run();

public partial class Program;
