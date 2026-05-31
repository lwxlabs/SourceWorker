using LwX.Flow;
using SourceWorker.Contracts;
using SourceWorker.Domain;

namespace SourceWorker.Application;

public sealed record AddProjectCommand(string Path, string? Name = null) : IRequest<ProjectDto>;
public sealed record GetProjectsQuery : IRequest<IReadOnlyList<ProjectDto>>;
public sealed record StartProjectScanCommand(Guid ProjectId) : IRequest<StartScanResponse>;
public sealed record GetJobStatusQuery(Guid JobId) : IRequest<JobStatusDto?>;
public sealed record GenerateBasicContextCommand(Guid ProjectId) : IRequest<ContextGenerationResponse>;
public sealed record ImportProjectInstructionsCommand(Guid ProjectId, string InstructionFilePath) : IRequest<ImportInstructionsResponse>;
public sealed record DoctorProjectQuery(Guid ProjectId) : IRequest<DoctorReport>;
public sealed record GetRecipesQuery(Guid ProjectId) : IRequest<IReadOnlyList<RecipeListItemDto>>;
public sealed record RunRecipeCommand(Guid ProjectId, string RecipeId, bool Force = false) : IRequest<RunRecipeResponse>;
public sealed record GetProjectChangesQuery(Guid ProjectId) : IRequest<ChangeSetReport>;
public sealed record GetContextStatusQuery(Guid ProjectId) : IRequest<ContextFreshnessReport>;

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

public sealed class GetProjectsQueryHandler(IProjectRepository projects) : IRequestHandler<GetProjectsQuery, IReadOnlyList<ProjectDto>>
{
    public async Task<IReadOnlyList<ProjectDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
        => (await projects.ListAsync(cancellationToken)).Select(ProjectMappings.ToDto).ToArray();
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

public sealed class GenerateBasicContextCommandHandler(IProjectRepository projects, IProjectScanner scanner, IFileIndexRepository fileIndex, IIndexedFileRepository indexedFiles, IScanRunRepository scanRuns, IContextWriter contextWriter, IRecipeRunner recipeRunner, IRecipeFactory recipeFactory, IRecipeRepository recipeRepository)
    : IRequestHandler<GenerateBasicContextCommand, ContextGenerationResponse>
{
    public async Task<ContextGenerationResponse> Handle(GenerateBasicContextCommand request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException($"Project '{request.ProjectId}' was not found.");
        var scan = await scanner.ScanAsync(project, cancellationToken);
        await scanRuns.AddAsync(scan.ScanRun, cancellationToken);
        await fileIndex.ReplaceForScanAsync(scan.ScanRun.Id, scan.Files, scan.Todos, scan.Technologies, cancellationToken);
        await indexedFiles.SaveForScanAsync(project.Id, scan.ScanRun.Id, scan.Files, cancellationToken);
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
        return new ImportInstructionsResponse(document.Id, ".ai-context/instructions/imported-instructions.md", ".ai-context/instructions/source-path-validation.md", importedRecipe.Id);
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

public static class ProjectMappings
{
    public static ProjectDto ToDto(this Project project) => new(project.Id, project.Name, project.RootPath, project.CreatedAt, project.LastScannedAt);
    public static JobStatusDto ToDto(this WorkerJob job) => new(job.Id, job.ProjectId, job.Type, job.Status, job.ProgressPercent, job.Message, job.CurrentStep, job.ErrorMessage, job.Logs ?? []);
}
