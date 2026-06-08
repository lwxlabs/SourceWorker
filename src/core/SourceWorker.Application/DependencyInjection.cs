using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;

namespace SourceWorker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerApplication(this IServiceCollection services)
    {
        services.AddScoped<IRequestFlow, ServiceProviderRequestFlow>();
        services.AddScoped<IRequestHandler<AddProjectCommand, SourceWorker.Contracts.ProjectDto>, AddProjectCommandHandler>();
        services.AddScoped<IRequestHandler<GetProjectsQuery, IReadOnlyList<SourceWorker.Contracts.ProjectSummaryDto>>, GetProjectsQueryHandler>();
        services.AddScoped<IRequestHandler<DeleteProjectCommand, bool>, DeleteProjectCommandHandler>();
        services.AddScoped<IRequestHandler<StartProjectScanCommand, SourceWorker.Contracts.StartScanResponse>, StartProjectScanCommandHandler>();
        services.AddScoped<IRequestHandler<GetJobsQuery, IReadOnlyList<SourceWorker.Contracts.JobStatusDto>>, GetJobsQueryHandler>();
        services.AddScoped<IRequestHandler<GetProjectJobsQuery, IReadOnlyList<SourceWorker.Contracts.JobStatusDto>>, GetProjectJobsQueryHandler>();
        services.AddScoped<IRequestHandler<GetJobStatusQuery, SourceWorker.Contracts.JobStatusDto?>, GetJobStatusQueryHandler>();
        services.AddScoped<IRequestHandler<GenerateBasicContextCommand, SourceWorker.Contracts.ContextGenerationResponse>, GenerateBasicContextCommandHandler>();
        services.AddScoped<IRequestHandler<ImportProjectInstructionsCommand, SourceWorker.Contracts.ImportInstructionsResponse>, ImportProjectInstructionsCommandHandler>();
        services.AddScoped<IRequestHandler<DoctorProjectQuery, SourceWorker.Domain.DoctorReport>, DoctorProjectQueryHandler>();
        services.AddScoped<IRequestHandler<GetRecipesQuery, IReadOnlyList<SourceWorker.Contracts.RecipeListItemDto>>, GetRecipesQueryHandler>();
        services.AddScoped<IRequestHandler<RunRecipeCommand, SourceWorker.Contracts.RunRecipeResponse>, RunRecipeCommandHandler>();
        services.AddScoped<IRequestHandler<GetProjectChangesQuery, SourceWorker.Domain.ChangeSetReport>, GetProjectChangesQueryHandler>();
        services.AddScoped<IRequestHandler<GetContextStatusQuery, SourceWorker.Domain.ContextFreshnessReport>, GetContextStatusQueryHandler>();
        services.AddScoped<IRequestHandler<GetStorageInfoQuery, SourceWorker.Domain.StorageInfo>, GetStorageInfoQueryHandler>();
        services.AddScoped<IRequestHandler<GetArtifactsQuery, SourceWorker.Domain.ArtifactLifecycleReport>, GetArtifactsQueryHandler>();
        services.AddScoped<IRequestHandler<GetCleanupPreviewQuery, SourceWorker.Domain.CleanupPreviewReport>, GetCleanupPreviewQueryHandler>();
        services.AddScoped<IRequestHandler<CleanupGeneratedCommand, SourceWorker.Domain.CleanupResult>, CleanupGeneratedCommandHandler>();
        services.AddScoped<IRequestHandler<GetSymbolsQuery, IReadOnlyList<SourceWorker.Domain.CodeSymbol>>, GetSymbolsQueryHandler>();
        services.AddScoped<IRequestHandler<GetFlowsQuery, IReadOnlyList<SourceWorker.Domain.CodeFlow>>, GetFlowsQueryHandler>();
        services.AddScoped<IRequestHandler<GetRoutesQuery, IReadOnlyList<SourceWorker.Domain.RouteInfo>>, GetRoutesQueryHandler>();
        services.AddScoped<IRequestHandler<GetCodeLanguagesQuery, SourceWorker.Domain.ProjectLanguageProfile?>, GetCodeLanguagesQueryHandler>();
        services.AddScoped<IRequestHandler<GetCodeAnalyzersQuery, IReadOnlyList<SourceWorker.Domain.CodeAnalyzerDescriptor>>, GetCodeAnalyzersQueryHandler>();
        services.AddScoped<IRequestHandler<GetCodeAnalyzerRunsQuery, IReadOnlyList<SourceWorker.Domain.CodeAnalyzerRun>>, GetCodeAnalyzerRunsQueryHandler>();
        services.AddScoped<IRequestHandler<GetCodeComponentsQuery, IReadOnlyList<SourceWorker.Domain.CodeComponent>>, GetCodeComponentsQueryHandler>();
        services.AddScoped<IRequestHandler<GetFrontendRoutesQuery, IReadOnlyList<SourceWorker.Domain.FrontendRoute>>, GetFrontendRoutesQueryHandler>();
        services.AddScoped<IRequestHandler<GetScriptImportsQuery, IReadOnlyList<SourceWorker.Domain.ScriptImport>>, GetScriptImportsQueryHandler>();
        services.AddScoped<IRequestHandler<GetStyleUsagesQuery, IReadOnlyList<SourceWorker.Domain.StyleUsage>>, GetStyleUsagesQueryHandler>();
        services.AddScoped<IRequestHandler<GetPackageReferencesQuery, IReadOnlyList<SourceWorker.Domain.PackageReferenceInfo>>, GetPackageReferencesQueryHandler>();
        services.AddScoped<IRequestHandler<GetBuildScriptsQuery, IReadOnlyList<SourceWorker.Domain.BuildScriptInfo>>, GetBuildScriptsQueryHandler>();
        services.AddScoped<IRequestHandler<GetCodeAnalysisSettingsQuery, SourceWorker.Domain.CodeAnalysisSettings>, GetCodeAnalysisSettingsQueryHandler>();
        services.AddScoped<IRequestHandler<UpdateCodeAnalysisSettingsCommand, SourceWorker.Domain.CodeAnalysisSettings>, UpdateCodeAnalysisSettingsCommandHandler>();
        services.AddScoped<IRequestHandler<GetContextBrowserQuery, SourceWorker.Domain.ContextBrowserSummary>, GetContextBrowserQueryHandler>();
        services.AddScoped<IRequestHandler<GetContextFilesQuery, IReadOnlyList<SourceWorker.Domain.ContextBrowserFile>>, GetContextFilesQueryHandler>();
        services.AddScoped<IRequestHandler<ReadContextFileQuery, string>, ReadContextFileQueryHandler>();
        services.AddScoped<IRequestHandler<SearchContextQuery, SourceWorker.Domain.ContextBrowserQueryResult>, SearchContextQueryHandler>();
        services.AddScoped<IRequestHandler<GetMarkdownFilesQuery, IReadOnlyList<SourceWorker.Domain.MarkdownFileSummary>>, GetMarkdownFilesQueryHandler>();
        services.AddScoped<IRequestHandler<GetMarkdownFileQuery, SourceWorker.Domain.MarkdownFileContent>, GetMarkdownFileQueryHandler>();
        services.AddScoped<IRequestHandler<SaveMarkdownFileCommand, SourceWorker.Domain.MarkdownFileSaveResult>, SaveMarkdownFileCommandHandler>();
        services.AddScoped<IRequestHandler<ValidateMarkdownCommand, SourceWorker.Domain.MarkdownValidationResult>, ValidateMarkdownCommandHandler>();
        services.AddScoped<IRequestHandler<GetContextBranchesQuery, IReadOnlyList<SourceWorker.Domain.SourceContextBranch>>, GetContextBranchesQueryHandler>();
        services.AddScoped<IRequestHandler<GetContextBranchQuery, SourceWorker.Domain.SourceContextBranch?>, GetContextBranchQueryHandler>();
        services.AddScoped<IRequestHandler<CreateContextBranchCommand, SourceWorker.Domain.SourceContextBranch>, CreateContextBranchCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateContextBranchCommand, SourceWorker.Domain.SourceContextBranch>, UpdateContextBranchCommandHandler>();
        services.AddScoped<IRequestHandler<RemoveContextBranchCommand, bool>, RemoveContextBranchCommandHandler>();
        services.AddScoped<IRequestHandler<ActivateContextBranchCommand, SourceWorker.Domain.SourceContextBranch>, ActivateContextBranchCommandHandler>();
        services.AddScoped<IRequestHandler<GetSnapshotsQuery, IReadOnlyList<SourceWorker.Domain.SourceSnapshot>>, GetSnapshotsQueryHandler>();
        services.AddScoped<IRequestHandler<GetSnapshotQuery, SourceWorker.Domain.SourceSnapshot?>, GetSnapshotQueryHandler>();
        services.AddScoped<IRequestHandler<DeleteSnapshotCommand, bool>, DeleteSnapshotCommandHandler>();
        services.AddScoped<IRequestHandler<GetLatestSnapshotDiffQuery, SourceWorker.Domain.SourceDiffReport>, GetLatestSnapshotDiffQueryHandler>();
        services.AddScoped<IRequestHandler<CompareBranchSnapshotsQuery, SourceWorker.Domain.SourceDiffReport>, CompareBranchSnapshotsQueryHandler>();
        services.AddScoped<IRequestHandler<CompareSnapshotsQuery, SourceWorker.Domain.SourceDiffReport>, CompareSnapshotsQueryHandler>();
        services.AddScoped<IRequestHandler<CompareSnapshotFileQuery, SourceWorker.Domain.SourceTextDiff>, CompareSnapshotFileQueryHandler>();
        services.AddScoped<IRequestHandler<CreateBranchDiffPackCommand, SourceWorker.Domain.BranchDiffPack>, CreateBranchDiffPackCommandHandler>();
        services.AddScoped<IRequestHandler<GetLatestBranchDiffPackQuery, SourceWorker.Domain.BranchDiffPack>, GetLatestBranchDiffPackQueryHandler>();
        services.AddScoped<IRequestHandler<GetAgentHandoffQuery, SourceWorker.Domain.AgentHandoff>, GetAgentHandoffQueryHandler>();
        services.AddScoped<IRequestHandler<GetScanCompletenessQuery, SourceWorker.Domain.ScanCompletenessReport>, GetScanCompletenessQueryHandler>();
        services.AddScoped<IRequestHandler<GetOutputsQuery, SourceWorker.Domain.GeneratedOutputSet>, GetOutputsQueryHandler>();
        services.AddScoped<IRequestHandler<GetPacksQuery, IReadOnlyList<SourceWorker.Domain.ContextPackManifest>>, GetPacksQueryHandler>();
        services.AddScoped<IRequestHandler<GetPackQuery, SourceWorker.Domain.ContextPackManifest?>, GetPackQueryHandler>();
        services.AddScoped<IRequestHandler<ReadPackContentQuery, string>, ReadPackContentQueryHandler>();
        services.AddScoped<IRequestHandler<ExportPackCommand, string>, ExportPackCommandHandler>();
        services.AddScoped<IRequestHandler<ExportPackDefaultCommand, SourceWorker.Contracts.ExportPackResponse>, ExportPackDefaultCommandHandler>();
        services.AddScoped<IRequestHandler<ExportDocsCommand, SourceWorker.Contracts.RunExportResponse>, ExportDocsCommandHandler>();
        services.AddScoped<IRequestHandler<GetAiProvidersQuery, IReadOnlyList<SourceWorker.Domain.AiProviderHealthResult>>, GetAiProvidersQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiProviderStatusesQuery, IReadOnlyList<SourceWorker.Domain.AiProviderHealthResult>>, GetAiProviderStatusesQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiProviderCatalogQuery, SourceWorker.Domain.AiProviderCatalog>, GetAiProviderCatalogQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiProviderPresetsQuery, IReadOnlyList<SourceWorker.Domain.AiProviderPresetInfo>>, GetAiProviderPresetsQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiProviderModelsQuery, SourceWorker.Domain.AiProviderModelList>, GetAiProviderModelsQueryHandler>();
        services.AddScoped<IRequestHandler<ConfigureAiProviderCommand, SourceWorker.Domain.AiProviderConfiguration>, ConfigureAiProviderCommandHandler>();
        services.AddScoped<IRequestHandler<TestAiProviderCommand, SourceWorker.Domain.AiProviderHealthResult>, TestAiProviderCommandHandler>();
        services.AddScoped<IRequestHandler<GetProjectAiPacksQuery, IReadOnlyList<SourceWorker.Domain.ContextPackManifest>>, GetProjectAiPacksQueryHandler>();
        services.AddScoped<IRequestHandler<PreviewAiPromptCommand, SourceWorker.Domain.AiPromptPreview>, PreviewAiPromptCommandHandler>();
        services.AddScoped<IRequestHandler<StartAiRunCommand, SourceWorker.Domain.AiRun>, StartAiRunCommandHandler>();
        services.AddScoped<IRequestHandler<GetAiRunsQuery, IReadOnlyList<SourceWorker.Domain.AiRun>>, GetAiRunsQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiRunQuery, SourceWorker.Domain.AiRun?>, GetAiRunQueryHandler>();
        services.AddScoped<IRequestHandler<ValidateAiProviderCommand, SourceWorker.Domain.AiProviderValidationResult>, ValidateAiProviderCommandHandler>();
        services.AddScoped<IRequestHandler<DiagnoseAiProviderCommand, SourceWorker.Domain.AiProviderConnectionDiagnostic>, DiagnoseAiProviderCommandHandler>();
        services.AddScoped<IRequestHandler<GetAiSettingsQuery, SourceWorker.Domain.AiSettings>, GetAiSettingsQueryHandler>();
        services.AddScoped<IRequestHandler<UpdateAiSettingsCommand, SourceWorker.Domain.AiSettings>, UpdateAiSettingsCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteAiRunsCommand, bool>, DeleteAiRunsCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteAiRunCommand, bool>, DeleteAiRunCommandHandler>();
        services.AddScoped<IRequestHandler<GetAiUsageQuery, SourceWorker.Domain.AiUsageSummary>, GetAiUsageQueryHandler>();
        services.AddScoped<IRequestHandler<GetProjectAiUsageQuery, SourceWorker.Domain.AiUsageSummary>, GetProjectAiUsageQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiPackQualityQuery, SourceWorker.Domain.AiContextPackQualityReport>, GetAiPackQualityQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiExplanationOptionsQuery, SourceWorker.Application.AiExplanationOptions>, GetAiExplanationOptionsQueryHandler>();
        services.AddScoped<IRequestHandler<PreviewAiExplanationCommand, SourceWorker.Domain.AiExplanationPreview>, PreviewAiExplanationCommandHandler>();
        services.AddScoped<IRequestHandler<RunAiExplanationCommand, SourceWorker.Domain.AiExplanationResult>, RunAiExplanationCommandHandler>();
        services.AddScoped<IRequestHandler<GetAiExplanationRunsQuery, IReadOnlyList<SourceWorker.Domain.AiExplanationResult>>, GetAiExplanationRunsQueryHandler>();
        services.AddScoped<IRequestHandler<GetAiExplanationRunQuery, SourceWorker.Domain.AiExplanationResult?>, GetAiExplanationRunQueryHandler>();
        services.AddScoped<IRequestHandler<SaveAiExplanationCommand, SourceWorker.Domain.AiExplanationSaveResult>, SaveAiExplanationCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteAiExplanationRunCommand, bool>, DeleteAiExplanationRunCommandHandler>();
        services.AddScoped<IRequestHandler<GetDogfoodStatusQuery, SourceWorker.Domain.DogfoodProjectReadiness>, GetDogfoodStatusQueryHandler>();
        services.AddScoped<IRequestHandler<GetProjectDogfoodStatusQuery, SourceWorker.Domain.DogfoodProjectReadiness>, GetProjectDogfoodStatusQueryHandler>();
        services.AddScoped<IRequestHandler<GetDiagnosticsSummaryQuery, SourceWorker.Domain.DiagnosticsSummary>, GetDiagnosticsSummaryQueryHandler>();
        services.AddScoped<IRequestHandler<ExportDiagnosticsCommand, SourceWorker.Domain.DiagnosticsExport>, ExportDiagnosticsCommandHandler>();
        services.AddScoped<IRequestHandler<GetDogfoodSessionsQuery, IReadOnlyList<SourceWorker.Domain.DogfoodSession>>, GetDogfoodSessionsQueryHandler>();
        services.AddScoped<IRequestHandler<StartDogfoodSessionCommand, SourceWorker.Domain.DogfoodSession>, StartDogfoodSessionCommandHandler>();
        services.AddScoped<IRequestHandler<GetDogfoodSessionQuery, SourceWorker.Domain.DogfoodSession?>, GetDogfoodSessionQueryHandler>();
        services.AddScoped<IRequestHandler<CompleteDogfoodStepCommand, SourceWorker.Domain.DogfoodSession>, CompleteDogfoodStepCommandHandler>();
        services.AddScoped<IRequestHandler<FinishDogfoodSessionCommand, SourceWorker.Domain.DogfoodSession>, FinishDogfoodSessionCommandHandler>();
        services.AddScoped<IRequestHandler<ExportDogfoodSessionCommand, SourceWorker.Domain.DiagnosticsExport>, ExportDogfoodSessionCommandHandler>();
        services.AddScoped<IRequestHandler<ReportFrictionIssueCommand, SourceWorker.Domain.DogfoodSessionIssue>, ReportFrictionIssueCommandHandler>();
        services.AddScoped<IRequestHandler<GetFrictionIssuesQuery, IReadOnlyList<SourceWorker.Domain.DogfoodSessionIssue>>, GetFrictionIssuesQueryHandler>();
        services.AddScoped<IRequestHandler<UpdateFrictionIssueStatusCommand, SourceWorker.Domain.DogfoodSessionIssue?>, UpdateFrictionIssueStatusCommandHandler>();
        services.AddScoped<IRequestHandler<ExportFrictionIssuesCommand, SourceWorker.Domain.DiagnosticsExport>, ExportFrictionIssuesCommandHandler>();
        services.AddScoped<IRequestHandler<GetNextActionsQuery, IReadOnlyList<SourceWorker.Domain.NextAction>>, GetNextActionsQueryHandler>();
        services.AddScoped<IRequestHandler<ValidateProjectPathCommand, SourceWorker.Domain.ProjectPathValidationResult>, ValidateProjectPathCommandHandler>();
        services.AddScoped<IRequestHandler<GetRecentProjectPathsQuery, IReadOnlyList<SourceWorker.Domain.RecentProjectPath>>, GetRecentProjectPathsQueryHandler>();
        services.AddScoped<IRequestHandler<GetProjectAiSettingsQuery, SourceWorker.Domain.ProjectAiSettings>, GetProjectAiSettingsQueryHandler>();
        services.AddScoped<IRequestHandler<UpdateProjectAiSettingsCommand, SourceWorker.Domain.ProjectAiSettings>, UpdateProjectAiSettingsCommandHandler>();
        services.AddScoped<IRequestHandler<PreviewQuickAskCommand, SourceWorker.Domain.QuickAskResult>, PreviewQuickAskCommandHandler>();
        services.AddScoped<IRequestHandler<RunQuickAskCommand, SourceWorker.Domain.QuickAskResult>, RunQuickAskCommandHandler>();
        services.AddScoped<IRequestHandler<GetLogsQuery, IReadOnlyList<SourceWorker.Domain.LogEntry>>, GetLogsQueryHandler>();
        services.AddScoped<IProjectInstructionImporter, ProjectInstructionImporter>();
        return services;
    }
}

public sealed class ProjectInstructionImporter(IProjectInstructionParser parser, ISourcePathValidator validator) : IProjectInstructionImporter
{
    public async Task<SourceWorker.Domain.ProjectInstructionDocument> ImportAsync(SourceWorker.Domain.Project project, string instructionFilePath, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(instructionFilePath);
        if (!File.Exists(path)) throw new FileNotFoundException("Instruction file was not found.", path);
        var markdown = await File.ReadAllTextAsync(path, cancellationToken);
        var parsed = parser.Parse(project.Id, project.RootPath, path, markdown);
        var validated = validator.Validate(project.RootPath, parsed.SourcePaths.Select(p => p.OriginalPath));
        return parsed with { SourcePaths = validated };
    }
}
