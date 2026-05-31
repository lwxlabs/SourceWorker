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
        services.AddSingleton<IRecipeRunner, BasicRecipeRunner>();
        return services;
    }
}
