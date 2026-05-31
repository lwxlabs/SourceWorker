using SourceWorker.Domain;

namespace SourceWorker.Application;

public interface IProjectRepository
{
    Task<Project> AddAsync(Project project, CancellationToken cancellationToken);
    Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken);
    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken);
}

public interface IScanRunRepository
{
    Task<ScanRun> AddAsync(ScanRun scanRun, CancellationToken cancellationToken);
    Task<ScanRun?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ScanRun?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken);
}

public interface IFileIndexRepository
{
    Task ReplaceForScanAsync(Guid scanRunId, IReadOnlyList<IndexedFile> files, IReadOnlyList<TodoItem> todos, IReadOnlyList<DetectedTechnology> technologies, CancellationToken cancellationToken);
}

public interface IIndexedFileRepository
{
    Task SaveForScanAsync(Guid projectId, Guid scanRunId, IReadOnlyList<IndexedFile> files, CancellationToken cancellationToken);
    Task<IReadOnlyList<IndexedFile>> GetByScanRunAsync(Guid scanRunId, CancellationToken cancellationToken);
    Task<IReadOnlyList<IndexedFile>> GetLatestByProjectAsync(Guid projectId, CancellationToken cancellationToken);
}

public interface IGeneratedArtifactRepository
{
    Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken);
    Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken);
}

public interface IJobRepository
{
    Task<WorkerJob> AddAsync(WorkerJob job, CancellationToken cancellationToken);
    Task<WorkerJob?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkerJob>> ListAsync(CancellationToken cancellationToken);
    Task<WorkerJob> UpdateAsync(WorkerJob job, CancellationToken cancellationToken);
    Task AddStepAsync(WorkerJobStep step, CancellationToken cancellationToken);
}

public interface IProjectScanner
{
    Task<ProjectScanResult> ScanAsync(Project project, CancellationToken cancellationToken);
}

public interface IGitInspector
{
    Task<GitSnapshot> InspectAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextWriter
{
    Task<ContextWriteResult> WriteBasicContextAsync(ProjectScanResult scanResult, CancellationToken cancellationToken);
}

public interface IRecipeRunner
{
    Task<ContextWriteResult> RunBasicHandoffAsync(Project project, CancellationToken cancellationToken);
    Task<ContextWriteResult> RunBackendDocsCompactAsync(ProjectScanResult scanResult, CancellationToken cancellationToken);
    Task<ContextWriteResult> RunAsync(ProjectRecipe recipe, ProjectScanResult scanResult, ProjectInstructionDocument? instructions, bool force, CancellationToken cancellationToken);
}

public interface IProjectInstructionParser
{
    ProjectInstructionDocument Parse(Guid projectId, string projectRootPath, string instructionFilePath, string markdown);
}

public interface IProjectInstructionRepository
{
    Task<ProjectInstructionDocument?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken);
    Task<ProjectInstructionDocument> SaveAsync(ProjectInstructionDocument document, CancellationToken cancellationToken);
}

public interface IProjectInstructionImporter
{
    Task<ProjectInstructionDocument> ImportAsync(Project project, string instructionFilePath, CancellationToken cancellationToken);
}

public interface IRecipeRepository
{
    Task SaveAsync(Project project, ProjectRecipe recipe, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProjectRecipe>> ListAsync(Project project, CancellationToken cancellationToken);
    Task<ProjectRecipe?> GetAsync(Project project, string recipeId, CancellationToken cancellationToken);
}

public interface IRecipeFactory
{
    IReadOnlyList<ProjectRecipe> CreateDefaultRecipes(Project project);
    ProjectRecipe CreateImportedRecipe(Project project, ProjectInstructionDocument instructions);
}

public interface ISourcePathValidator
{
    IReadOnlyList<ProjectInstructionSourcePath> Validate(string projectRootPath, IEnumerable<string> paths);
}

public interface IInstructionContextWriter
{
    Task WriteImportedInstructionsAsync(Project project, ProjectInstructionDocument instructions, CancellationToken cancellationToken);
    Task WriteSourcePathValidationAsync(Project project, SourcePathValidationReport report, CancellationToken cancellationToken);
}

public interface IDoctorService
{
    Task<DoctorReport> CheckAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextFreshnessService
{
    Task<ContextFreshnessReport> CheckAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextIndexWriter
{
    Task WriteAsync(ProjectScanResult scanResult, ContextFreshnessReport freshness, CancellationToken cancellationToken);
}

public interface IChangeSetService
{
    Task<ChangeSetReport> GetChangesAsync(Project project, CancellationToken cancellationToken);
}

public interface IContextStatusService
{
    Task<ContextFreshnessReport> GetStatusAsync(Project project, CancellationToken cancellationToken);
}

public interface ILocalStorageSchemaService
{
    Task<LocalStorageSchemaInfo> GetInfoAsync(CancellationToken cancellationToken);
}

public interface IStorageInfoService
{
    Task<StorageInfo> GetInfoAsync(CancellationToken cancellationToken);
}

public interface IGeneratedArtifactLifecycleService
{
    Task<ArtifactLifecycleReport> AnalyzeAsync(Project project, CancellationToken cancellationToken);
}

public interface IGeneratedArtifactCleanupService
{
    Task<CleanupPreviewReport> PreviewAsync(Project project, CancellationToken cancellationToken);
    Task<CleanupResult> CleanupAsync(Project project, bool dryRun, bool force, CancellationToken cancellationToken);
}

public interface IArtifactStatusService
{
    Task<ArtifactLifecycleReport> GetArtifactsAsync(Project project, CancellationToken cancellationToken);
}

public interface IStorageHealthService
{
    Task<bool> CheckAsync(CancellationToken cancellationToken);
}

public interface ILocalJobQueue
{
    ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

public sealed record ProjectScanResult(
    Project Project,
    ScanRun ScanRun,
    IReadOnlyList<IndexedFile> Files,
    IReadOnlyList<TodoItem> Todos,
    IReadOnlyList<DetectedTechnology> Technologies,
    GitSnapshot Git,
    string? SolutionName,
    string? SolutionPath,
    IReadOnlyList<SolutionFile> SolutionFiles,
    IReadOnlyList<ProjectFile> Projects,
    IReadOnlyList<string> ReadmeFiles,
    IReadOnlyList<string> ConfigFiles,
    IReadOnlyList<string> DockerFiles,
    IReadOnlyList<FolderRole> FolderRoles,
    IReadOnlyList<string> MainEntrypoints,
    IReadOnlyList<string> DeletedFiles);
public sealed record ContextWriteResult(string OutputPath, IReadOnlyList<string> Files);
