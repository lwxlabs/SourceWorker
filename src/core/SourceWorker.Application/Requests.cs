using LwX.Flow;
using SourceWorker.Contracts;
using SourceWorker.Domain;

namespace SourceWorker.Application;

public sealed record AddProjectCommand(string Path, string? Name = null) : IRequest<ProjectDto>;
public sealed record GetProjectsQuery(bool? IncludeTestProjects = null, bool? IncludeMissing = null, string? Search = null, int? Limit = null) : IRequest<IReadOnlyList<ProjectSummaryDto>>;
public sealed record StartProjectScanCommand(Guid ProjectId) : IRequest<StartScanResponse>;
public sealed record GetJobsQuery : IRequest<IReadOnlyList<JobStatusDto>>;
public sealed record GetProjectJobsQuery(Guid ProjectId) : IRequest<IReadOnlyList<JobStatusDto>>;
public sealed record GetJobStatusQuery(Guid JobId) : IRequest<JobStatusDto?>;
public sealed record GenerateBasicContextCommand(Guid ProjectId) : IRequest<ContextGenerationResponse>;
public sealed record ImportProjectInstructionsCommand(Guid ProjectId, string InstructionFilePath) : IRequest<ImportInstructionsResponse>;
public sealed record DoctorProjectQuery(Guid ProjectId) : IRequest<DoctorReport>;
public sealed record GetRecipesQuery(Guid ProjectId) : IRequest<IReadOnlyList<RecipeListItemDto>>;
public sealed record RunRecipeCommand(Guid ProjectId, string RecipeId, bool Force = false) : IRequest<RunRecipeResponse>;
public sealed record GetProjectChangesQuery(Guid ProjectId) : IRequest<ChangeSetReport>;
public sealed record GetContextStatusQuery(Guid ProjectId) : IRequest<ContextFreshnessReport>;
public sealed record GetStorageInfoQuery : IRequest<StorageInfo>;
public sealed record GetArtifactsQuery(Guid ProjectId) : IRequest<ArtifactLifecycleReport>;
public sealed record GetCleanupPreviewQuery(Guid ProjectId) : IRequest<CleanupPreviewReport>;
public sealed record CleanupGeneratedCommand(Guid ProjectId, bool DryRun, bool Force) : IRequest<CleanupResult>;
public sealed record GetSymbolsQuery(Guid ProjectId) : IRequest<IReadOnlyList<CodeSymbol>>;
public sealed record GetFlowsQuery(Guid ProjectId) : IRequest<IReadOnlyList<CodeFlow>>;
public sealed record GetRoutesQuery(Guid ProjectId) : IRequest<IReadOnlyList<RouteInfo>>;
public sealed record GetContextBrowserQuery(Guid ProjectId) : IRequest<ContextBrowserSummary>;
public sealed record GetContextFilesQuery(Guid ProjectId) : IRequest<IReadOnlyList<ContextBrowserFile>>;
public sealed record ReadContextFileQuery(Guid ProjectId, string RelativePath) : IRequest<string>;
public sealed record SearchContextQuery(Guid ProjectId, string Query) : IRequest<ContextBrowserQueryResult>;
public sealed record GetOutputsQuery(Guid ProjectId) : IRequest<GeneratedOutputSet>;
public sealed record GetPacksQuery(Guid ProjectId) : IRequest<IReadOnlyList<ContextPackManifest>>;
public sealed record GetPackQuery(Guid ProjectId, string PackId) : IRequest<ContextPackManifest?>;
public sealed record ReadPackContentQuery(Guid ProjectId, string PackId) : IRequest<string>;
public sealed record ExportPackCommand(Guid ProjectId, string PackId, string OutputFile) : IRequest<string>;
public sealed record ExportPackDefaultCommand(Guid ProjectId, string PackId) : IRequest<ExportPackResponse>;
public sealed record ExportDocsCommand(Guid ProjectId, string ExportProfileId) : IRequest<RunExportResponse>;
public sealed record GetAiProvidersQuery : IRequest<IReadOnlyList<AiProviderHealthResult>>;
public sealed record GetAiProviderStatusesQuery : IRequest<IReadOnlyList<AiProviderHealthResult>>;
public sealed record ConfigureAiProviderCommand(AiProviderKind Kind, string Model, string? BaseUrl, string? ApiKey, bool IsEnabled = true) : IRequest<AiProviderConfiguration>;
public sealed record TestAiProviderCommand(string ProviderId) : IRequest<AiProviderHealthResult>;
public sealed record GetProjectAiPacksQuery(Guid ProjectId) : IRequest<IReadOnlyList<ContextPackManifest>>;
public sealed record PreviewAiPromptCommand(Guid ProjectId, string ProviderId, string PackId, string? UserInstruction, int MaxOutputTokens = 1200) : IRequest<AiPromptPreview>;
public sealed record StartAiRunCommand(Guid ProjectId, string ProviderId, string PackId, string? UserInstruction, bool ConfirmSafetyWarnings, int MaxOutputTokens = 1200) : IRequest<AiRun>;
public sealed record GetAiRunsQuery(Guid ProjectId) : IRequest<IReadOnlyList<AiRun>>;
public sealed record GetAiRunQuery(Guid ProjectId, Guid RunId) : IRequest<AiRun?>;

public sealed class AddProjectCommandHandler(IProjectRepository projects) : IRequestHandler<AddProjectCommand, ProjectDto>
{
    public async Task<ProjectDto> Handle(AddProjectCommand request, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(request.Path);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var existing = await projects.GetByPathAsync(root, cancellationToken);
        if (existing is not null) return existing.ToDto();
        var name = string.IsNullOrWhiteSpace(request.Name) ? new DirectoryInfo(root).Name : request.Name.Trim();
        return (await projects.AddAsync(new Project(Guid.NewGuid(), name, root, DateTimeOffset.UtcNow), cancellationToken)).ToDto();
    }
}

public sealed class GetProjectsQueryHandler(
    IProjectRepository projects, 
    IScanRunRepository scanRuns,
    IRouteInfoRepository routes,
    ICodeFlowRepository flows) : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectSummaryDto>>
{
    public async Task<IReadOnlyList<ProjectSummaryDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var all = await projects.ListAsync(cancellationToken);
        var summaries = new List<ProjectSummaryDto>();

        foreach (var p in all)
        {
            var exists = Directory.Exists(p.RootPath);
            if (request.IncludeMissing == false && !exists) continue;
            
            if (!string.IsNullOrEmpty(request.Search) && 
                !p.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase) && 
                !p.RootPath.Contains(request.Search, StringComparison.OrdinalIgnoreCase)) continue;

            var latestScan = await scanRuns.GetLatestAsync(p.Id, cancellationToken);
            
            var isTestProject = p.Name.Contains("Tests", StringComparison.OrdinalIgnoreCase) || p.RootPath.Contains("tests", StringComparison.OrdinalIgnoreCase);
            if (request.IncludeTestProjects == false && isTestProject) continue;

            int routeCount = 0;
            int flowCount = 0;

            if (latestScan != null)
            {
                routeCount = (await routes.GetByProjectAsync(p.Id, cancellationToken)).Count;
                flowCount = (await flows.GetByProjectAsync(p.Id, cancellationToken)).Count;
            }

            summaries.Add(new ProjectSummaryDto(
                p.Id,
                p.Name,
                p.RootPath,
                exists,
                latestScan?.CompletedAt,
                exists ? 80 : 0,
                false,
                false,
                0,
                routeCount,
                flowCount
            ));
        }

        var result = summaries.OrderByDescending(s => s.LastScanUtc).ThenBy(s => s.Name).AsEnumerable();
        if (request.Limit.HasValue) result = result.Take(request.Limit.Value);

        return result.ToArray();
    }
}

public sealed class GetJobsQueryHandler(IJobRepository jobs) : IRequestHandler<GetJobsQuery, IReadOnlyList<JobStatusDto>>
{
    public async Task<IReadOnlyList<JobStatusDto>> Handle(GetJobsQuery request, CancellationToken cancellationToken)
        => (await jobs.ListAsync(cancellationToken)).Select(ProjectMappings.ToDto).OrderByDescending(j => j.Id).ToArray();
}

public sealed class GetProjectJobsQueryHandler(IJobRepository jobs) : IRequestHandler<GetProjectJobsQuery, IReadOnlyList<JobStatusDto>>
{
    public async Task<IReadOnlyList<JobStatusDto>> Handle(GetProjectJobsQuery request, CancellationToken cancellationToken)
        => (await jobs.ListAsync(cancellationToken))
            .Where(j => j.ProjectId == request.ProjectId)
            .Select(ProjectMappings.ToDto)
            .OrderByDescending(j => j.Id)
            .ToArray();
}

public sealed class StartProjectScanCommandHandler(IProjectRepository projects, IJobRepository jobs, ILocalJobQueue queue) : IRequestHandler<StartProjectScanCommand, StartScanResponse>
{
    public async Task<StartScanResponse> Handle(StartProjectScanCommand request, CancellationToken cancellationToken)
    {
        _ = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        var job = new WorkerJob(Guid.NewGuid(), request.ProjectId, WorkerJobType.ProjectScan, WorkerJobStatus.Queued, 0, "Queued", DateTimeOffset.UtcNow);
        await jobs.AddAsync(job, cancellationToken);
        await queue.EnqueueAsync(job.Id, cancellationToken);
        return new StartScanResponse(job.Id);
    }
}

public sealed class GetJobStatusQueryHandler(IJobRepository jobs) : IRequestHandler<GetJobStatusQuery, JobStatusDto?>
{
    public async Task<JobStatusDto?> Handle(GetJobStatusQuery request, CancellationToken cancellationToken)
        => (await jobs.GetAsync(request.JobId, cancellationToken))?.ToDto();
}

public sealed class GenerateBasicContextCommandHandler(IProjectRepository projects, IProjectScanner scanner, IFileIndexRepository fileIndex, IIndexedFileRepository indexedFiles, IScanRunRepository scanRuns, IContextWriter contextWriter, IRecipeRunner recipeRunner, IRecipeFactory recipeFactory, IRecipeRepository recipeRepository, ICSharpCodeIntelligenceScanner codeScanner, ICodeSymbolRepository symbols, ICodeFlowRepository flows, IRouteInfoRepository routes, IServiceRegistrationRepository services, IConfigurationUsageRepository configs, IBackgroundWorkerInfoRepository workers, ITestInfoRepository tests)
    : IRequestHandler<GenerateBasicContextCommand, ContextGenerationResponse>
{
    public async Task<ContextGenerationResponse> Handle(GenerateBasicContextCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        var scan = await scanner.ScanAsync(project, cancellationToken);
        await scanRuns.AddAsync(scan.ScanRun, cancellationToken);
        await fileIndex.ReplaceForScanAsync(scan.ScanRun.Id, scan.Files, scan.Todos, scan.Technologies, cancellationToken);
        await indexedFiles.SaveForScanAsync(project.Id, scan.ScanRun.Id, scan.Files, cancellationToken);
        var code = await codeScanner.ScanAsync(scan, cancellationToken);
        await symbols.SaveAsync(project.Id, scan.ScanRun.Id, code.Symbols, cancellationToken);
        await flows.SaveAsync(project.Id, scan.ScanRun.Id, code.Flows, cancellationToken);
        await routes.SaveAsync(project.Id, scan.ScanRun.Id, code.Routes, cancellationToken);
        await services.SaveAsync(project.Id, scan.ScanRun.Id, code.ServiceRegistrations, cancellationToken);
        await configs.SaveAsync(project.Id, scan.ScanRun.Id, code.ConfigurationUsages, cancellationToken);
        await workers.SaveAsync(project.Id, scan.ScanRun.Id, code.BackgroundWorkers, cancellationToken);
        await tests.SaveAsync(project.Id, scan.ScanRun.Id, code.Tests, cancellationToken);
        foreach (var recipe in recipeFactory.CreateDefaultRecipes(project))
        {
            await recipeRepository.SaveAsync(project, recipe, cancellationToken);
        }

        var written = await contextWriter.WriteBasicContextAsync(scan, cancellationToken);
        await recipeRunner.RunBackendDocsCompactAsync(scan, cancellationToken);
        await projects.UpdateAsync(project with { LastScannedAt = scan.ScanRun.CompletedAt }, cancellationToken);
        return new ContextGenerationResponse(written.OutputPath, written.Files);
    }
}

public sealed class ImportProjectInstructionsCommandHandler(IProjectRepository projects, IProjectInstructionImporter importer, IProjectInstructionRepository instructionRepository, IRecipeFactory recipeFactory, IRecipeRepository recipes, IInstructionContextWriter writer, ISourcePathValidator validator)
    : IRequestHandler<ImportProjectInstructionsCommand, ImportInstructionsResponse>
{
    public async Task<ImportInstructionsResponse> Handle(ImportProjectInstructionsCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        var document = await importer.ImportAsync(project, request.InstructionFilePath, cancellationToken);
        await instructionRepository.SaveAsync(document, cancellationToken);
        await writer.WriteImportedInstructionsAsync(project, document, cancellationToken);

        var validation = validator.Validate(project.RootPath, document.SourcePaths.Select(p => p.OriginalPath));
        await writer.WriteSourcePathValidationAsync(project, new SourcePathValidationReport(project.Id, validation, validation.Where(p => p.WarningMessage is not null).Select(p => p.WarningMessage!).ToArray()), cancellationToken);

        foreach (var recipe in recipeFactory.CreateDefaultRecipes(project))
        {
            await recipes.SaveAsync(project, recipe, cancellationToken);
        }

        var importedRecipe = recipeFactory.CreateImportedRecipe(project, document);
        await recipes.SaveAsync(project, importedRecipe, cancellationToken);
        return new ImportInstructionsResponse(document.Id, ".ai-context/instructions/imported/imported-instructions.md", ".ai-context/instructions/imported/source-path-validation.md", importedRecipe.Id);
    }
}

public sealed class DoctorProjectQueryHandler(IProjectRepository projects, IDoctorService doctor) : IRequestHandler<DoctorProjectQuery, DoctorReport>
{
    public async Task<DoctorReport> Handle(DoctorProjectQuery request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        return await doctor.CheckAsync(project, cancellationToken);
    }
}

public sealed class GetRecipesQueryHandler(IProjectRepository projects, IRecipeFactory recipeFactory, IRecipeRepository recipes) : IRequestHandler<GetRecipesQuery, IReadOnlyList<RecipeListItemDto>>
{
    public async Task<IReadOnlyList<RecipeListItemDto>> Handle(GetRecipesQuery request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        foreach (var recipe in recipeFactory.CreateDefaultRecipes(project))
        {
            await recipes.SaveAsync(project, recipe, cancellationToken);
        }

        return (await recipes.ListAsync(project, cancellationToken))
            .OrderBy(recipe => recipe.Id)
            .Select(recipe => new RecipeListItemDto(recipe.Id, recipe.Name, recipe.Description, recipe.OutputPath))
            .ToArray();
    }
}

public sealed class RunRecipeCommandHandler(IProjectRepository projects, IProjectScanner scanner, IRecipeFactory recipeFactory, IRecipeRepository recipes, IRecipeRunner runner, IProjectInstructionRepository instructions)
    : IRequestHandler<RunRecipeCommand, RunRecipeResponse>
{
    public async Task<RunRecipeResponse> Handle(RunRecipeCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        foreach (var recipe in recipeFactory.CreateDefaultRecipes(project))
        {
            await recipes.SaveAsync(project, recipe, cancellationToken);
        }

        var selected = await recipes.GetAsync(project, request.RecipeId, cancellationToken) ?? throw new InvalidOperationException($"Recipe '{request.RecipeId}' was not found.");
        var scan = await scanner.ScanAsync(project, cancellationToken);
        var document = await instructions.GetLatestAsync(project.Id, cancellationToken);
        var result = await runner.RunAsync(selected, scan, document, request.Force, cancellationToken);
        return new RunRecipeResponse(selected.Id, result.OutputPath, result.Files);
    }
}

public sealed class GetProjectChangesQueryHandler(IProjectRepository projects, IChangeSetService changeSet) : IRequestHandler<GetProjectChangesQuery, ChangeSetReport>
{
    public async Task<ChangeSetReport> Handle(GetProjectChangesQuery request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        return await changeSet.GetChangesAsync(project, cancellationToken);
    }
}

public sealed class GetContextStatusQueryHandler(IProjectRepository projects, IContextStatusService status) : IRequestHandler<GetContextStatusQuery, ContextFreshnessReport>
{
    public async Task<ContextFreshnessReport> Handle(GetContextStatusQuery request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        return await status.GetStatusAsync(project, cancellationToken);
    }
}

public sealed class GetStorageInfoQueryHandler(IStorageInfoService storage) : IRequestHandler<GetStorageInfoQuery, StorageInfo>
{
    public Task<StorageInfo> Handle(GetStorageInfoQuery request, CancellationToken cancellationToken) => storage.GetInfoAsync(cancellationToken);
}

public sealed class GetArtifactsQueryHandler(IProjectRepository projects, IGeneratedArtifactLifecycleService lifecycle) : IRequestHandler<GetArtifactsQuery, ArtifactLifecycleReport>
{
    public async Task<ArtifactLifecycleReport> Handle(GetArtifactsQuery request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        return await lifecycle.AnalyzeAsync(project, cancellationToken);
    }
}

public sealed class GetCleanupPreviewQueryHandler(IProjectRepository projects, IGeneratedArtifactCleanupService cleanup) : IRequestHandler<GetCleanupPreviewQuery, CleanupPreviewReport>
{
    public async Task<CleanupPreviewReport> Handle(GetCleanupPreviewQuery request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        return await cleanup.PreviewAsync(project, cancellationToken);
    }
}

public sealed class CleanupGeneratedCommandHandler(IProjectRepository projects, IGeneratedArtifactCleanupService cleanup) : IRequestHandler<CleanupGeneratedCommand, CleanupResult>
{
    public async Task<CleanupResult> Handle(CleanupGeneratedCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        return await cleanup.CleanupAsync(project, request.DryRun, request.Force, cancellationToken);
    }
}

public sealed class GetSymbolsQueryHandler(ICodeSymbolRepository repo) : IRequestHandler<GetSymbolsQuery, IReadOnlyList<CodeSymbol>> { public Task<IReadOnlyList<CodeSymbol>> Handle(GetSymbolsQuery request, CancellationToken cancellationToken) => repo.GetByProjectAsync(request.ProjectId, cancellationToken); }
public sealed class GetFlowsQueryHandler(ICodeFlowRepository repo) : IRequestHandler<GetFlowsQuery, IReadOnlyList<CodeFlow>> { public Task<IReadOnlyList<CodeFlow>> Handle(GetFlowsQuery request, CancellationToken cancellationToken) => repo.GetByProjectAsync(request.ProjectId, cancellationToken); }
public sealed class GetRoutesQueryHandler(IRouteInfoRepository repo) : IRequestHandler<GetRoutesQuery, IReadOnlyList<RouteInfo>> { public Task<IReadOnlyList<RouteInfo>> Handle(GetRoutesQuery request, CancellationToken cancellationToken) => repo.GetByProjectAsync(request.ProjectId, cancellationToken); }
public sealed class GetContextBrowserQueryHandler(IProjectRepository projects, IContextBrowserService browser) : IRequestHandler<GetContextBrowserQuery, ContextBrowserSummary> { public async Task<ContextBrowserSummary> Handle(GetContextBrowserQuery request, CancellationToken cancellationToken) => await browser.GetSummaryAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), cancellationToken); }
public sealed class GetContextFilesQueryHandler(IProjectRepository projects, IContextBrowserService browser) : IRequestHandler<GetContextFilesQuery, IReadOnlyList<ContextBrowserFile>> { public async Task<IReadOnlyList<ContextBrowserFile>> Handle(GetContextFilesQuery request, CancellationToken cancellationToken) => await browser.ListFilesAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), cancellationToken); }
public sealed class ReadContextFileQueryHandler(IProjectRepository projects, IContextBrowserService browser) : IRequestHandler<ReadContextFileQuery, string> { public async Task<string> Handle(ReadContextFileQuery request, CancellationToken cancellationToken) => await browser.ReadFileAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), request.RelativePath, cancellationToken); }
public sealed class SearchContextQueryHandler(IProjectRepository projects, IContextBrowserService browser) : IRequestHandler<SearchContextQuery, ContextBrowserQueryResult> { public async Task<ContextBrowserQueryResult> Handle(SearchContextQuery request, CancellationToken cancellationToken) => await browser.SearchAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), request.Query, cancellationToken); }
public sealed class GetOutputsQueryHandler(IProjectRepository projects, IGeneratedOutputService outputs) : IRequestHandler<GetOutputsQuery, GeneratedOutputSet> { public async Task<GeneratedOutputSet> Handle(GetOutputsQuery request, CancellationToken cancellationToken) => await outputs.GetOutputSetAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), "backend-docs-compact", cancellationToken); }
public sealed class GetPacksQueryHandler(IProjectRepository projects, IContextPackService packs) : IRequestHandler<GetPacksQuery, IReadOnlyList<ContextPackManifest>> { public async Task<IReadOnlyList<ContextPackManifest>> Handle(GetPacksQuery request, CancellationToken cancellationToken) => await packs.ListAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), cancellationToken); }
public sealed class GetPackQueryHandler(IProjectRepository projects, IContextPackService packs) : IRequestHandler<GetPackQuery, ContextPackManifest?> { public async Task<ContextPackManifest?> Handle(GetPackQuery request, CancellationToken cancellationToken) => await packs.GetAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), request.PackId, cancellationToken); }
public sealed class ReadPackContentQueryHandler(IProjectRepository projects, IContextPackReader reader) : IRequestHandler<ReadPackContentQuery, string> { public async Task<string> Handle(ReadPackContentQuery request, CancellationToken cancellationToken) => await reader.ReadPackContentAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), request.PackId, cancellationToken); }
public sealed class ExportPackCommandHandler(IProjectRepository projects, IContextPackReader reader) : IRequestHandler<ExportPackCommand, string>
{
    public async Task<string> Handle(ExportPackCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var full = Path.GetFullPath(request.OutputFile);
        var root = Path.GetFullPath(project.RootPath);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Pack export path must be inside the project root.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, await reader.ReadPackContentAsync(project, request.PackId, cancellationToken), cancellationToken);
        return full;
    }
}

public sealed class ExportPackDefaultCommandHandler(IProjectRepository projects, IContextPackReader reader) : IRequestHandler<ExportPackDefaultCommand, ExportPackResponse>
{
    public async Task<ExportPackResponse> Handle(ExportPackDefaultCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var content = await reader.ReadPackContentAsync(project, request.PackId, cancellationToken);
        var dir = Path.Combine(project.RootPath, ".ai-context", "packs");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        var fileName = $"{request.PackId}.md";
        var fullPath = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
        return new ExportPackResponse(request.PackId, fullPath, true);
    }
}

public sealed class ExportDocsCommandHandler(IProjectRepository projects, IExportRunner runner) : IRequestHandler<ExportDocsCommand, RunExportResponse> 
{ 
    public async Task<RunExportResponse> Handle(ExportDocsCommand request, CancellationToken cancellationToken) 
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var run = await runner.RunAsync(project, request.ExportProfileId, cancellationToken);
        return new RunExportResponse(run.ExportProfileId, "instructions/Result/Backend", run.ExportedFiles);
    }
}

public sealed class GetAiProvidersQueryHandler(IAiProviderRegistry registry, IAiProviderConfigurationRepository configurations) : IRequestHandler<GetAiProvidersQuery, IReadOnlyList<AiProviderHealthResult>>
{
    public async Task<IReadOnlyList<AiProviderHealthResult>> Handle(GetAiProvidersQuery request, CancellationToken cancellationToken)
    {
        var configured = await configurations.ListAsync(cancellationToken);
        return registry.ListProviders()
            .Select(provider =>
            {
                var config = configured.FirstOrDefault(c => c.Kind == provider.Kind);
                return new AiProviderHealthResult(provider.Id, provider.Kind, config is null ? AiProviderStatus.NotConfigured : (config.IsEnabled ? AiProviderStatus.NotConfigured : AiProviderStatus.Disabled), config is null ? "Provider is not configured." : "Provider is configured.", DateTimeOffset.UtcNow, config?.MaskedApiKey);
            })
            .ToArray();
    }
}

public sealed class GetAiProviderStatusesQueryHandler(IAiProviderRegistry registry, IAiProviderConfigurationRepository configurations) : IRequestHandler<GetAiProviderStatusesQuery, IReadOnlyList<AiProviderHealthResult>>
{
    public async Task<IReadOnlyList<AiProviderHealthResult>> Handle(GetAiProviderStatusesQuery request, CancellationToken cancellationToken)
    {
        var results = new List<AiProviderHealthResult>();
        foreach (var configuration in await configurations.ListAsync(cancellationToken))
        {
            var provider = registry.GetProvider(configuration.Kind.ToString());
            results.Add(provider is null
                ? new AiProviderHealthResult(configuration.Kind.ToString(), configuration.Kind, AiProviderStatus.Unhealthy, "Provider implementation was not found.", DateTimeOffset.UtcNow, configuration.MaskedApiKey)
                : await provider.CheckHealthAsync(configuration, cancellationToken));
        }
        return results;
    }
}

public sealed class ConfigureAiProviderCommandHandler(IAiProviderConfigurationRepository configurations, IAiSecretStore secrets) : IRequestHandler<ConfigureAiProviderCommand, AiProviderConfiguration>
{
    public async Task<AiProviderConfiguration> Handle(ConfigureAiProviderCommand request, CancellationToken cancellationToken)
    {
        var secretName = request.Kind is AiProviderKind.Ollama ? null : $"ai:{request.Kind}:api-key";
        string? masked = null;
        if (!string.IsNullOrWhiteSpace(secretName) && !string.IsNullOrWhiteSpace(request.ApiKey))
        {
            await secrets.SaveSecretAsync(secretName, request.ApiKey, cancellationToken);
            masked = secrets.Mask(request.ApiKey);
        }
        else if (!string.IsNullOrWhiteSpace(secretName))
        {
            var existing = await secrets.GetSecretAsync(secretName, cancellationToken);
            masked = secrets.Mask(existing);
        }

        return await configurations.SaveAsync(new AiProviderConfiguration(Guid.NewGuid(), request.Kind, request.Kind.ToString(), request.Model, request.BaseUrl, request.IsEnabled, secretName, masked, DateTimeOffset.UtcNow), cancellationToken);
    }
}

public sealed class TestAiProviderCommandHandler(IAiProviderRegistry registry, IAiProviderConfigurationRepository configurations) : IRequestHandler<TestAiProviderCommand, AiProviderHealthResult>
{
    public async Task<AiProviderHealthResult> Handle(TestAiProviderCommand request, CancellationToken cancellationToken)
    {
        var configuration = await configurations.GetAsync(request.ProviderId, cancellationToken);
        if (configuration is null) return new AiProviderHealthResult(request.ProviderId, AiProviderKind.OpenAiCompatible, AiProviderStatus.NotConfigured, "Provider is not configured.", DateTimeOffset.UtcNow);
        var provider = registry.GetProvider(request.ProviderId);
        return provider is null
            ? new AiProviderHealthResult(request.ProviderId, configuration.Kind, AiProviderStatus.Unhealthy, "Provider implementation was not found.", DateTimeOffset.UtcNow, configuration.MaskedApiKey)
            : await provider.CheckHealthAsync(configuration, cancellationToken);
    }
}

public sealed class GetProjectAiPacksQueryHandler(IProjectRepository projects, IContextPackService packs) : IRequestHandler<GetProjectAiPacksQuery, IReadOnlyList<ContextPackManifest>>
{
    public async Task<IReadOnlyList<ContextPackManifest>> Handle(GetProjectAiPacksQuery request, CancellationToken cancellationToken)
        => await packs.ListAsync(await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found."), cancellationToken);
}

public sealed class PreviewAiPromptCommandHandler(IAiPromptPreviewService preview) : IRequestHandler<PreviewAiPromptCommand, AiPromptPreview>
{
    public Task<AiPromptPreview> Handle(PreviewAiPromptCommand request, CancellationToken cancellationToken)
        => preview.PreviewAsync(request.ProjectId, request.ProviderId, request.PackId, request.UserInstruction, request.MaxOutputTokens, cancellationToken);
}

public sealed class StartAiRunCommandHandler(IAiRunService runs) : IRequestHandler<StartAiRunCommand, AiRun>
{
    public Task<AiRun> Handle(StartAiRunCommand request, CancellationToken cancellationToken)
        => runs.StartAsync(new AiRunInput(request.ProjectId, request.ProviderId, AiProviderKind.OpenAiCompatible, "", request.PackId, request.UserInstruction, request.ConfirmSafetyWarnings, request.MaxOutputTokens), cancellationToken);
}

public sealed class GetAiRunsQueryHandler(IAiRunService runs) : IRequestHandler<GetAiRunsQuery, IReadOnlyList<AiRun>>
{
    public Task<IReadOnlyList<AiRun>> Handle(GetAiRunsQuery request, CancellationToken cancellationToken) => runs.GetRunsAsync(request.ProjectId, cancellationToken);
}

public sealed class GetAiRunQueryHandler(IAiRunService runs) : IRequestHandler<GetAiRunQuery, AiRun?>
{
    public async Task<AiRun?> Handle(GetAiRunQuery request, CancellationToken cancellationToken)
    {
        var run = await runs.GetRunAsync(request.RunId, cancellationToken);
        return run?.ProjectId == request.ProjectId ? run : null;
    }
}

public static class ProjectMappings
{
    public static ProjectDto ToDto(this Project project) => new(project.Id, project.Name, project.RootPath, project.CreatedAt, project.LastScannedAt);
    public static JobStatusDto ToDto(this WorkerJob job) => new(job.Id, job.ProjectId, job.Type, job.Status, job.ProgressPercent, job.Message, job.CurrentStep, job.ErrorMessage, job.Logs ?? []);

    public static CleanupPreviewDto ToDto(this CleanupPreviewReport report) => new(
        report.ProjectId,
        report.SafeDeleteFiles.Select(f => new CleanupCandidateDto(f, "Orphaned/Deprecated", true))
            .Concat(report.StaleTrackedArtifacts.Select(f => new CleanupCandidateDto(f, "Stale", false)))
            .Concat(report.MissingTrackedArtifacts.Select(f => new CleanupCandidateDto(f, "Missing Record", false)))
            .ToArray(),
        report.SafeDeleteFiles.Count,
        $"cleanup-generated {report.ProjectId}"
    );

    public static CleanupGeneratedResponse ToDto(this CleanupResult result) => new(
        result.ProjectId,
        result.DryRun,
        result.DeletedFiles,
        result.SkippedFiles
    );
}
