using LwX.Flow;
using Microsoft.AspNetCore.Mvc;
using SourceWorker.Application;
using SourceWorker.Contracts;
using SourceWorker.Domain;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LocalApiRuntimeOptions>(builder.Configuration.GetSection("SourceWorker:LocalApi"));
builder.Services.AddCors();
builder.Services.AddOpenApi();
builder.Services
    .AddSourceWorkerApplication()
    .AddSourceWorkerInfrastructure()
    .AddSourceWorkerJobs();

var app = builder.Build();

var originService = app.Services.GetRequiredService<ILocalApiOriginService>();
app.UseCors(policy => policy
    .WithOrigins(originService.GetAllowedOrigins())
    .AllowAnyMethod()
    .AllowAnyHeader());

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/health") || 
        context.Request.Path.StartsWithSegments("/api/version") ||
        context.Request.Path.StartsWithSegments("/api/runtime/info"))
    {
        await next();
        return;
    }

    var runtimeService = context.RequestServices.GetRequiredService<ILocalApiRuntimeService>();
    if (!runtimeService.GetState().TokenEnabled)
    {
        await next();
        return;
    }

    var tokenService = context.RequestServices.GetRequiredService<ILocalApiTokenService>();
    if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) || 
        !authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    var token = authHeader.ToString()["Bearer ".Length..].Trim();
    if (!tokenService.ValidateToken(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "SourceWorker.LocalApi" }));

app.MapGet("/api/version", (IAppVersionService versionService) => Results.Ok(versionService.GetVersion()));

app.MapGet("/api/runtime/info", (ILocalApiRuntimeService runtimeService) => {
    var state = runtimeService.GetState();
    return Results.Ok(new ApiRuntimeInfoDto(
        state.SelectedHost,
        state.SelectedPort,
        state.BaseUrl,
        state.TokenEnabled,
        state.TokenCreatedUtc,
        state.AllowedOrigins,
        state.DashboardOrigin,
        state.ProcessId,
        state.StartupUtc,
        state.EnvironmentName
    ));
});

app.MapGet("/api/runtime/security", (ILocalApiRuntimeService runtimeService) => {
    var state = runtimeService.GetState();
    return Results.Ok(new ApiSecurityInfoDto(
        state.TokenEnabled,
        state.TokenCreatedUtc,
        state.AllowedOrigins,
        state.DashboardOrigin
    ));
});

app.MapPost("/api/runtime/token/rotate", (ILocalApiTokenService tokenService) => {
    var newToken = tokenService.GenerateAndSaveToken();
    return Results.Ok(new { token = newToken });
});

app.MapGet("/api/projects", async (
    [FromQuery] bool? includeTestProjects,
    [FromQuery] bool? includeMissing,
    [FromQuery] string? search,
    [FromQuery] int? limit,
    IRequestFlow flow, 
    CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetProjectsQuery(includeTestProjects, includeMissing, search, limit), cancellationToken)));

app.MapPost("/api/projects", async (AddProjectRequest request, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(new CreateProjectResponse(await flow.Send(new AddProjectCommand(request.Path, request.Name), cancellationToken))));

app.MapPost("/api/projects/{id:guid}/scan", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Accepted($"/api/projects/{id}/scan", await flow.Send(new StartProjectScanCommand(id), cancellationToken)));

app.MapGet("/api/jobs", async (IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetJobsQuery(), cancellationToken)));

app.MapGet("/api/projects/{id:guid}/jobs", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetProjectJobsQuery(id), cancellationToken)));

app.MapGet("/api/jobs/{id:guid}", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) =>
{
    var job = await flow.Send(new GetJobStatusQuery(id), cancellationToken);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

app.MapGet("/api/jobs/{id:guid}/logs", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) =>
{
    var job = await flow.Send(new GetJobStatusQuery(id), cancellationToken);
    return job is null ? Results.NotFound() : Results.Ok(job.Logs);
});

app.MapGet("/api/projects/{id:guid}/doctor", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new DoctorProjectQuery(id), cancellationToken)));

app.MapGet("/api/projects/{id:guid}/changes", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetProjectChangesQuery(id), cancellationToken)));

app.MapGet("/api/projects/{id:guid}/context-status", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetContextStatusQuery(id), cancellationToken)));

app.MapGet("/api/projects/{id:guid}/context-files", async (Guid id, IGeneratedArtifactRepository artifacts, CancellationToken cancellationToken)
    => Results.Ok((await artifacts.GetByProjectAsync(id, cancellationToken)).Where(a => a.Type == SourceWorker.Domain.GeneratedArtifactType.ContextFile)));

app.MapGet("/api/projects/{id:guid}/scan-runs", async (Guid id, IScanRunRepository scanRuns, CancellationToken cancellationToken)
    => Results.Ok(await scanRuns.GetByProjectAsync(id, cancellationToken)));

app.MapGet("/api/projects/{id:guid}/scan-runs/latest", async (Guid id, IScanRunRepository scanRuns, CancellationToken cancellationToken) =>
{
    var scan = await scanRuns.GetLatestAsync(id, cancellationToken);
    return scan is null ? Results.NotFound() : Results.Ok(scan);
});

app.MapGet("/api/storage/info", async (IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetStorageInfoQuery(), cancellationToken)));

app.MapGet("/api/projects/{id:guid}/artifacts", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok(await flow.Send(new GetArtifactsQuery(id), cancellationToken)));

app.MapGet("/api/projects/{id:guid}/cleanup-preview", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok((await flow.Send(new GetCleanupPreviewQuery(id), cancellationToken)).ToDto()));

app.MapPost("/api/projects/{id:guid}/cleanup-generated", async (Guid id, SourceWorker.Contracts.CleanupGeneratedRequest request, IRequestFlow flow, CancellationToken cancellationToken)
    => Results.Ok((await flow.Send(new CleanupGeneratedCommand(id, request.DryRun, request.Force), cancellationToken)).ToDto()));

app.MapGet("/api/projects/{id:guid}/symbols", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new GetSymbolsQuery(id), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/flows", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new GetFlowsQuery(id), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/routes", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new GetRoutesQuery(id), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/service-registrations", async (Guid id, IServiceRegistrationRepository repo, CancellationToken cancellationToken) => Results.Ok(await repo.GetByProjectAsync(id, cancellationToken)));
app.MapGet("/api/projects/{id:guid}/configuration-usages", async (Guid id, IConfigurationUsageRepository repo, CancellationToken cancellationToken) => Results.Ok(await repo.GetByProjectAsync(id, cancellationToken)));
app.MapGet("/api/projects/{id:guid}/tests", async (Guid id, ITestInfoRepository repo, CancellationToken cancellationToken) => Results.Ok(await repo.GetByProjectAsync(id, cancellationToken)));
app.MapGet("/api/projects/{id:guid}/context-browser", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new GetContextBrowserQuery(id), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/context-browser/files", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new GetContextFilesQuery(id), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/context-browser/files/{*relativePath}", async (Guid id, string relativePath, IRequestFlow flow, CancellationToken cancellationToken) => Results.Text(await flow.Send(new ReadContextFileQuery(id, relativePath), cancellationToken), "text/plain"));
app.MapGet("/api/projects/{id:guid}/context-browser/search", async (Guid id, string query, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new SearchContextQuery(id, query), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/outputs", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new GetOutputsQuery(id), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/packs", async (Guid id, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new GetPacksQuery(id), cancellationToken)));
app.MapGet("/api/projects/{id:guid}/packs/{packId}", async (Guid id, string packId, IRequestFlow flow, CancellationToken cancellationToken) =>
{
    var pack = await flow.Send(new GetPackQuery(id, packId), cancellationToken);
    return pack is null ? Results.NotFound() : Results.Ok(pack);
});
app.MapGet("/api/projects/{id:guid}/packs/{packId}/content", async (Guid id, string packId, IRequestFlow flow, CancellationToken cancellationToken) => Results.Text(await flow.Send(new ReadPackContentQuery(id, packId), cancellationToken), "text/markdown"));
app.MapPost("/api/projects/{id:guid}/packs/{packId}/export-default", async (Guid id, string packId, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new ExportPackDefaultCommand(id, packId), cancellationToken)));
app.MapPost("/api/projects/{id:guid}/exports/{exportProfileId}/run", async (Guid id, string exportProfileId, IRequestFlow flow, CancellationToken cancellationToken) => Results.Ok(await flow.Send(new ExportDocsCommand(id, exportProfileId), cancellationToken)));

app.Run();

public partial class Program;
