using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.AI;
using SourceWorker.AI.Providers;
using SourceWorker.Context;
using SourceWorker.Git;
using SourceWorker.Recipes;
using SourceWorker.Scanner;
using SourceWorker.Storage.Localite;

namespace SourceWorker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerInfrastructure(this IServiceCollection services, Action<LocaliteStorageOptions>? configureStorage = null)
    {
        services.AddSourceWorkerLocaliteStorage(configureStorage);
        services.AddSourceWorkerAiCore();
        services.AddSourceWorkerAiProviders();
        services.AddSingleton<IGitInspector, BasicGitInspector>();
        services.AddSingleton<IProjectScanner, BasicProjectScanner>();
        services.AddSingleton<IFlowExtractor, RuleBasedFlowExtractor>();
        services.AddSingleton<IFlowDiagramGenerator, FlowDiagramGenerator>();
        services.AddSingleton<CSharpCodeIntelligenceScanner>();
        services.AddSingleton<ICSharpCodeIntelligenceScanner>(sp => sp.GetRequiredService<CSharpCodeIntelligenceScanner>());
        services.AddSingleton<ICodeAnalyzer, CSharpCodeAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, RazorBlazorAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, MauiAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, JavaScriptAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, TypeScriptAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, VueAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, HtmlAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, CssAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, NodePackageAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, ConfigAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, DockerAnalyzer>();
        services.AddSingleton<ICodeAnalyzer, GitHubActionsAnalyzer>();
        services.AddSingleton<ICodeAnalyzerRegistry, CodeAnalyzerRegistry>();
        services.AddSingleton<ICodeAnalysisSettingsService, CodeAnalysisSettingsService>();
        services.AddSingleton<ILanguageDetectionService, LanguageDetectionService>();
        services.AddSingleton<ICodeIntelligencePipeline, CodeIntelligencePipeline>();
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
        services.AddSingleton<IContextBrowserService, ContextBrowserService>();
        services.AddSingleton<IContextSearchService, ContextBrowserService>();
        services.AddSingleton<IMermaidMarkdownValidator, MermaidMarkdownValidator>();
        services.AddSingleton<IMarkdownWorkspaceService, MarkdownWorkspaceService>();
        services.AddSingleton<IMarkdownFileReader>(sp => (MarkdownWorkspaceService)sp.GetRequiredService<IMarkdownWorkspaceService>());
        services.AddSingleton<IMarkdownFileWriter>(sp => (MarkdownWorkspaceService)sp.GetRequiredService<IMarkdownWorkspaceService>());
        services.AddSingleton<IMarkdownSavePolicyService>(sp => (MarkdownWorkspaceService)sp.GetRequiredService<IMarkdownWorkspaceService>());
        services.AddSingleton<ISourceContextBranchService, SourceContextBranchService>();
        services.AddSingleton<ISourceSnapshotService, SourceSnapshotService>();
        services.AddSingleton<ISourceDiffService, SourceDiffService>();
        services.AddSingleton<ITextDiffService, TextDiffService>();
        services.AddSingleton<IBranchDiffPackService, BranchDiffPackService>();
        services.AddSingleton<IOptionalGitMetadataService, OptionalGitMetadataService>();
        services.AddSingleton<IAgentHandoffService, AgentHandoffService>();
        services.AddSingleton<IScanCompletenessService, ScanCompletenessService>();
        services.AddSingleton<IOutputProfileService, OutputProfileService>();
        services.AddSingleton<IOutputRoutingService, OutputProfileService>();
        services.AddSingleton<IGeneratedOutputService, GeneratedOutputService>();
        services.AddSingleton<IExportProfileService, ExportProfileService>();
        services.AddSingleton<IExportRunner, ExportRunner>();
        services.AddSingleton<IContextPackService, ContextPackService>();
        services.AddSingleton<IContextPackWriter, ContextPackService>();
        services.AddSingleton<IContextPackReader, ContextPackService>();
        services.AddSingleton<IContextPackValidator, ContextPackService>();
        services.AddSingleton<IRecipeFactory, RecipeFactory>();
        services.AddSingleton<IRecipeRepository, FileRecipeRepository>();
        services.AddSingleton<IRecipeRunner, BasicRecipeRunner>();
        
        services.AddSingleton<ILocalApiTokenService, LocalApiTokenService>();
        services.AddSingleton<ILocalApiOriginService, LocalApiOriginService>();
        services.AddSingleton<ILocalApiRuntimeService, LocalApiRuntimeService>();
        services.AddSingleton<IAppVersionService, AppVersionService>();
        
        return services;
    }
}
