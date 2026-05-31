using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;

namespace SourceWorker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerApplication(this IServiceCollection services)
    {
        services.AddScoped<IRequestFlow, ServiceProviderRequestFlow>();
        services.AddScoped<IRequestHandler<AddProjectCommand, SourceWorker.Contracts.ProjectDto>, AddProjectCommandHandler>();
        services.AddScoped<IRequestHandler<GetProjectsQuery, IReadOnlyList<SourceWorker.Contracts.ProjectDto>>, GetProjectsQueryHandler>();
        services.AddScoped<IRequestHandler<StartProjectScanCommand, SourceWorker.Contracts.StartScanResponse>, StartProjectScanCommandHandler>();
        services.AddScoped<IRequestHandler<GetJobStatusQuery, SourceWorker.Contracts.JobStatusDto?>, GetJobStatusQueryHandler>();
        services.AddScoped<IRequestHandler<GenerateBasicContextCommand, SourceWorker.Contracts.ContextGenerationResponse>, GenerateBasicContextCommandHandler>();
        return services;
    }
}
