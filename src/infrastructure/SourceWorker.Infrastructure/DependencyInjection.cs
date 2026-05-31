using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Context;
using SourceWorker.Git;
using SourceWorker.Recipes;
using SourceWorker.Scanner;
using SourceWorker.Storage.Localite;

namespace SourceWorker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerInfrastructure(this IServiceCollection services)
    {
        services.AddSourceWorkerLocaliteStorage();
        services.AddSingleton<IGitInspector, BasicGitInspector>();
        services.AddSingleton<IProjectScanner, BasicProjectScanner>();
        services.AddSingleton<IContextWriter, MarkdownContextWriter>();
        services.AddSingleton<IProjectInstructionParser, ProjectInstructionParser>();
        services.AddSingleton<ISourcePathValidator, SourcePathValidator>();
        services.AddSingleton<IInstructionContextWriter, InstructionContextWriter>();
        services.AddSingleton<IDoctorService, DoctorService>();
        services.AddSingleton<IContextFreshnessService, ContextFreshnessService>();
        services.AddSingleton<IContextStatusService, ContextFreshnessService>();
        services.AddSingleton<IChangeSetService, ChangeSetService>();
        services.AddSingleton<IGeneratedArtifactLifecycleService, GeneratedArtifactLifecycleService>();
        services.AddSingleton<IArtifactStatusService, GeneratedArtifactLifecycleService>();
        services.AddSingleton<IGeneratedArtifactCleanupService, GeneratedArtifactCleanupService>();
        services.AddSingleton<IRecipeFactory, RecipeFactory>();
        services.AddSingleton<IRecipeRepository, FileRecipeRepository>();
        services.AddSingleton<IRecipeRunner, BasicRecipeRunner>();
        return services;
    }
}
