using System.Text.Json.Serialization;

namespace SourceWorker.ApiClient;

public sealed record HealthResponse(string Status, string Service);
public sealed record ProjectDto(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt);
public sealed record CreateProjectResponse(ProjectDto Project);
public sealed record LocalStorageSchemaInfo(string Provider, string Mode, string StoragePath, int SchemaVersion, DateTimeOffset CreatedUtc, DateTimeOffset LastMigratedUtc, string AppStorageFormatVersion);
public sealed record StorageInfo(LocalStorageSchemaInfo Schema, int ProjectCount, int ScanRunCount, int IndexedFileCount, int GeneratedArtifactCount, int JobCount, int InstructionCount, int RecipeCount, bool RepositoryHealth, int CodeSymbolCount, int CodeFlowCount, int RouteCount, int ServiceRegistrationCount, int ConfigurationUsageCount, int TestInfoCount, int AiProviderConfigCount = 0, int AiRunCount = 0);
public sealed record DoctorReport(string ProjectRoot, bool ProjectRootFound, bool GitRepoFound, IReadOnlyList<string> SolutionFiles, IReadOnlyList<string> ProjectFiles, IReadOnlyList<string> TestProjects, bool ContextFolderExists, bool ImportedInstructionsFound, IReadOnlyList<string> Recipes, IReadOnlyList<ProjectInstructionSourcePath> MissingSourcePaths, bool IsContextStale, DateTimeOffset? LastScanTime, IReadOnlyList<string> Warnings);
public sealed record ProjectInstructionSourcePath(string OriginalPath, string NormalizedPath, bool Exists, string Type, bool IsInsideProject, string? WarningMessage);
public sealed record ContextFreshnessReport(Guid ProjectId, bool HasStaleContext, IReadOnlyList<string> StaleContextFiles, IReadOnlyList<string> MissingGeneratedFiles, IReadOnlyList<string> ManuallyModifiedFiles, IReadOnlyList<string> FilesChangedSinceLastGeneration, int FreshnessScore, IReadOnlyList<string> RecommendedActions);
public sealed record GeneratedArtifact(Guid Id, Guid ProjectId, Guid? ScanRunId, string? RecipeId, string RelativePath, string AbsolutePath, string Type, string ContentHash, DateTimeOffset GeneratedUtc, string SourceInputHash, string Status, string GeneratorName, string? Notes);
public sealed record ArtifactLifecycleReport(Guid ProjectId, IReadOnlyList<GeneratedArtifact> Artifacts, IReadOnlyList<string> MissingArtifacts, IReadOnlyList<string> ManuallyModifiedArtifacts, IReadOnlyList<string> StaleArtifacts, IReadOnlyList<string> OrphanedArtifacts, IReadOnlyList<string> DeprecatedArtifacts, bool CleanupRecommended);
public sealed record CleanupPreviewReport(Guid ProjectId, IReadOnlyList<string> SafeDeleteFiles, IReadOnlyList<string> MissingTrackedArtifacts, IReadOnlyList<string> StaleTrackedArtifacts, IReadOnlyList<string> Warnings);
public sealed record CodeSymbol(Guid Id, Guid ProjectId, Guid ScanRunId, string FilePath, string RelativePath, string? Namespace, string Name, string FullName, string Kind, string Accessibility, string? ParentSymbolName, string? ReturnType, IReadOnlyList<string> Parameters, int LineNumber, IReadOnlyList<string> Attributes, bool IsStatic, bool IsAsync);
public sealed record CodeFlow(Guid Id, Guid ProjectId, Guid ScanRunId, string Name, string FlowType, string Trigger, IReadOnlyList<string> InvolvedFiles, IReadOnlyList<string> InvolvedSymbols, IReadOnlyList<CodeFlowStep> Steps, string Confidence, IReadOnlyList<string> Warnings, string? Origin = null);
public sealed record CodeFlowStep(int Order, string Text, string? SymbolName, string? RelativePath);
public sealed record RouteInfo(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string HttpMethod, string RoutePattern, string? HandlerSymbol, int LineNumber);
public sealed record ServiceRegistration(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string RegistrationMethod, string ServiceType, string? ImplementationType, int LineNumber);
public sealed record ConfigurationUsage(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string KeyOrSection, string UsageKind, int LineNumber);
public sealed record TestInfo(Guid Id, Guid ProjectId, Guid ScanRunId, string RelativePath, string ClassName, string MethodName, string TestFramework, int LineNumber);
public sealed record ContextBrowserFile(string RelativePath, string AbsolutePath, string Kind, long SizeBytes, DateTimeOffset LastModifiedUtc);
public sealed record ContextBrowserSummary(Guid ProjectId, IReadOnlyList<ContextBrowserFile> Files, IReadOnlyList<ProjectRecipe> Recipes, IReadOnlyList<CodeFlow> Flows, IReadOnlyList<RouteInfo> Routes, IReadOnlyList<object> Todos, IReadOnlyList<GeneratedArtifact> ProblemArtifacts);
public sealed record AgentHandoffDto(Guid ProjectId, string RelativePath, string Markdown, string RecommendedPackId, IReadOnlyList<AgentHandoffTemplateDto> Templates);
public sealed record AgentHandoffTemplateDto(string Id, string Name, string Header);
public sealed record ScanCompletenessReportDto(Guid ProjectId, Guid? ScanRunId, int Score, IReadOnlyList<ScanStageResultDto> Stages, IReadOnlyList<ScanStageWarningDto> Warnings, string CodeIntelligenceExplanation);
public sealed record ScanStageResultDto(string Name, string Status, int Count, IReadOnlyList<ScanStageWarningDto> Warnings, string? ErrorMessage, TimeSpan? Duration, IReadOnlyList<string> RelatedFiles);
public sealed record ScanStageWarningDto(string Code, string Message, string? Detail);
public sealed record ProjectRecipe(string Id, string Name, string Description, string Language, string OutputPath, string TargetPlatform, IReadOnlyList<string> IncludedSections, IReadOnlyList<string> SourceScopes, IReadOnlyList<string> Rules, IReadOnlyList<string> SkipItems, IReadOnlyList<string> VerificationChecklist, IReadOnlyList<string> GeneratedFiles, bool MermaidEnabled, bool RequireClassAndMethodNames, bool RequireSourceReferences, IReadOnlyDictionary<string, IReadOnlyList<string>> ExtraSections);
public sealed record OutputDestination(string Id, string Type, string Path, bool Primary, string PublishMode);
public sealed record GeneratedOutputSet(string RecipeId, IReadOnlyList<OutputDestination> Destinations, IReadOnlyList<GeneratedOutputFile> Files, IReadOnlyList<string> Warnings);
public sealed record GeneratedOutputFile(string RelativePath, string DestinationId, string ArtifactType, string Status, bool Exists);
public sealed record ContextPackManifest(ContextPack Pack, string RelativePath, IReadOnlyList<string> MissingFiles, bool IsValid);
public sealed record ContextPack(string Id, string Name, string Description, string Audience, string Purpose, IReadOnlyList<ContextPackItem> Items, IReadOnlyList<string> IncludedSelectors, string MaxRecommendedUse, int TokenEstimate, string FreshnessStatus, Guid? SourceScanRunId, DateTimeOffset GeneratedUtc);
public sealed record ContextPackItem(string RelativePath, string Kind, bool Required);
public sealed record ExportRun(string Id, string ExportProfileId, Guid ProjectId, DateTimeOffset StartedUtc, DateTimeOffset FinishedUtc, IReadOnlyList<string> ExportedFiles, IReadOnlyList<string> Warnings);

public sealed record ProjectSummaryDto(
    Guid Id,
    string Name,
    string Path,
    bool Exists,
    DateTimeOffset? LastScanUtc,
    int ContextFreshnessScore,
    bool HasStaleContext,
    bool HasManualGeneratedFiles,
    int TechnologyCount,
    int RouteCount,
    int FlowCount
);

public sealed record ApiRuntimeInfoDto(
    string SelectedHost,
    int SelectedPort,
    string BaseUrl,
    bool TokenEnabled,
    DateTimeOffset? TokenCreatedUtc,
    string[] AllowedOrigins,
    string? DashboardOrigin,
    int ProcessId,
    DateTimeOffset StartupUtc,
    string EnvironmentName,
    string AppVersion,
    string ApiVersion
);

public sealed record ApiSecurityInfoDto(
    bool TokenEnabled,
    DateTimeOffset? TokenCreatedUtc,
    string[] AllowedOrigins,
    string? DashboardOrigin
);

public sealed record VersionInfoDto(
    string AppName,
    string AppVersion,
    string ApiVersion,
    string SupportedApiClientVersion,
    int StorageSchemaVersion,
    int ContextFormatVersion,
    string BuildConfiguration
);

public sealed record ApiErrorInfo(string Message, string? Code = null, string? Detail = null);
public sealed record ApiConnectionStatus(bool Connected, string? Error = null, ApiRuntimeInfoDto? Runtime = null);

public sealed record JobStatusDto(Guid Id, Guid? ProjectId, string Type, string Status, int ProgressPercent, string? Message, string? CurrentStep, string? ErrorMessage, IReadOnlyList<string> Logs);

public sealed record StartScanResponse(Guid JobId);
public sealed record RunExportResponse(string ExportProfileId, string OutputPath, IReadOnlyList<string> Files);
public sealed record ExportPackResponse(string PackId, string OutputPath, bool Success);

public sealed record CleanupCandidateDto(string RelativePath, string Kind, bool SafeToDelete);
public sealed record CleanupPreviewDto(Guid ProjectId, IReadOnlyList<CleanupCandidateDto> Candidates, int SafeToDeleteCount, string RecommendedCommand);
public sealed record CleanupGeneratedResponse(Guid ProjectId, bool DryRun, IReadOnlyList<string> DeletedFiles, IReadOnlyList<string> SkippedFiles);

public enum DashboardActionKind
{
    ScanProject,
    RunRecipe,
    ExportDocs,
    ExportPack,
    CleanupPreview,
    CleanupGenerated,
    CleanupForce,
    RegisterProject
}

public enum DashboardActionSafetyLevel
{
    ReadOnly,
    SafeWrite,
    GeneratedFileWrite,
    DestructiveGeneratedCleanup,
    DangerousDisabled
}

public sealed record DashboardActionAvailability(
    DashboardActionKind Kind,
    DashboardActionSafetyLevel SafetyLevel,
    bool IsEnabled,
    string? DisabledReason = null,
    DashboardActionRequirement[]? Requirements = null
);

public sealed record DashboardActionRequirement(string Key, string Description, bool IsMet);

public enum AiProviderKind { OpenAI, Ollama, OpenAiCompatible, AzureOpenAILater, AnthropicLater, LMStudioLater, Gemini }
public enum AiProviderType { OpenAI, Gemini, OpenAICompatible, Ollama }
public enum ProviderPreset { OpenAI, Gemini, LMStudio, Ollama, OpenAICompatibleCustom, LocalAI, vLLM, OpenRouterLater }
public enum MarkdownWorkspaceScope { ProjectContext, BackendDocs, Instructions, ExternalMirror, UserMarkdown, AllSafeMarkdown }
public enum MarkdownFileKind { AgentHandoff, BackendDoc, AiExplanation, ContextDoc, ImportedInstruction, ExternalMirrorDoc, UserMarkdown, TechnicalMarkdown }
public enum MarkdownSaveMode { Normal, ForceOverwriteGenerated, ForceOverwriteManualGenerated }
public sealed record MarkdownFileSummaryDto(Guid ProjectId, string RelativePath, string DisplayName, MarkdownFileKind Kind, MarkdownWorkspaceScope Scope, long SizeBytes, DateTimeOffset LastModifiedUtc, bool IsGenerated, bool IsUserEditable, bool IsSourceControlled, bool IsExternalMirror, bool IsTechnical, bool HasSourceWorkerMarker, bool IsManuallyModifiedGeneratedFile, string? Warning);
public sealed record MarkdownFileContentDto(MarkdownFileSummaryDto Summary, string Markdown, string? RenderedHtml, string ContentHash, DateTimeOffset LastLoadedUtc);
public sealed record MarkdownFileSaveRequestDto(string RelativePath, string Markdown, string? ExpectedContentHash, MarkdownSaveMode SaveMode = MarkdownSaveMode.Normal);
public sealed record MarkdownFileSaveResultDto(bool Saved, string RelativePath, string NewContentHash, DateTimeOffset LastModifiedUtc, string? Warning, bool RequiresConfirmation);
public sealed record MarkdownValidationResultDto(bool IsValid, IReadOnlyList<string> Warnings);
public sealed record AiProviderCapabilitiesDto(bool SupportsResponsesApi, bool SupportsChatCompletions, bool SupportsGenerateContent, bool SupportsStreaming, bool SupportsStructuredOutput, bool SupportsToolUse, bool SupportsVision, bool SupportsEmbeddings, bool RecommendedForExplanation);
public sealed record AiProviderCatalogEntryDto(AiProviderType ProviderType, string DisplayName, string DefaultBaseUrl, IReadOnlyList<string> SuggestedModels, bool RequiresApiKey, bool IsLocal, bool DataLeavesMachine, AiProviderCapabilitiesDto Capabilities);
public sealed record AiProviderPresetInfoDto(ProviderPreset Preset, string DisplayName, AiProviderType ProviderType, string DefaultBaseUrl, string? DefaultModel, bool ApiKeyRequired, bool IsLocal, bool DataLeavesMachine, string Guidance);
public sealed record AiProviderCatalogDto(IReadOnlyList<AiProviderCatalogEntryDto> Providers, IReadOnlyList<AiProviderPresetInfoDto> Presets, DateTimeOffset GeneratedUtc);
public sealed record AiProviderModelListDto(string ProviderId, IReadOnlyList<string> Models, bool IsDiscovered, string? Warning);
public enum AiProviderStatus { NotConfigured, Healthy, Unhealthy, Disabled }
public enum AiRunStatus { Queued, Running, Completed, Failed, Cancelled, SafetyBlocked }
public enum ExplanationProfile { DeveloperOnboarding, ArchitectureDeepDive, PublicApiGuide, MaintenanceGuide, TestAndQualityGuide, FullProjectExplanation }
public enum AiExplanationQualityStatus { Good, NeedsReview, Failed }
public sealed record AiProviderConfigurationDto(AiProviderKind Kind, string Model, string? BaseUrl, string? ApiKey, bool IsEnabled = true);
public sealed record AiProviderConfigurationResultDto(Guid Id, AiProviderKind Kind, string Name, string Model, string? BaseUrl, bool IsEnabled, string? ApiKeySecretName, string? MaskedApiKey, DateTimeOffset UpdatedUtc);
public sealed record AiProviderStatusDto(string ProviderId, AiProviderKind Kind, AiProviderStatus Status, string Message, DateTimeOffset CheckedUtc, string? MaskedApiKey);
public sealed record AiSafetyWarningDto(string Code, string Severity, string Message, string? RelativePath);
public sealed record AiTokenEstimateDto(int PromptTokens, int MaxOutputTokens, int TotalTokens);
public sealed record AiCostEstimateDto(decimal? PromptCostUsd, decimal? OutputCostUsd, decimal? TotalCostUsd, string Currency);
public sealed record AiPromptPreviewRequest(string ProviderId, string PackId, string? UserInstruction, int MaxOutputTokens = 1200, string? TemplateId = null);
public sealed record AiPromptSectionDto(string Title, string Content, int TokenEstimate);
public sealed record AiPromptIncludedFileDto(string RelativePath, long SizeBytes, int TokenEstimate, string? ContentHash, bool Exists);
public sealed record AiPromptPreviewResponse(Guid ProjectId, string ProjectName, string ProviderId, AiProviderKind ProviderKind, string Model, string PackId, IReadOnlyList<string> IncludedFiles, IReadOnlyList<string> MissingFiles, IReadOnlyList<string> FreshnessWarnings, IReadOnlyList<AiSafetyWarningDto> SafetyWarnings, AiTokenEstimateDto TokenEstimate, AiCostEstimateDto CostEstimate, IReadOnlyList<AiPromptSectionDto>? Sections, IReadOnlyList<AiPromptIncludedFileDto>? IncludedFileDetails, string? TemplateId, int? ContextWindowTokens, int ContextWindowUsagePercent, bool IsTooLarge, string PromptText, string PromptHash);
public sealed record AiRunRequest(string ProviderId, string PackId, string? UserInstruction, bool ConfirmSafetyWarnings, int MaxOutputTokens = 1200);
public sealed record AiRunSummaryDto(Guid Id, Guid ProjectId, string ProviderId, AiProviderKind ProviderKind, string Model, string PackId, string? UserInstruction, string PromptHash, bool StoredPromptText, string? PromptPreview, IReadOnlyList<string> IncludedFiles, IReadOnlyList<AiSafetyWarningDto> SafetyWarnings, AiTokenEstimateDto TokenEstimate, AiRunStatus Status, DateTimeOffset CreatedUtc, DateTimeOffset? CompletedUtc, string? Error, string? OutputText, string? OutputHash);
public sealed record AiRunDetailDto(Guid Id, Guid ProjectId, string ProviderId, AiProviderKind ProviderKind, string Model, string PackId, string? UserInstruction, string PromptHash, bool StoredPromptText, string? PromptPreview, IReadOnlyList<string> IncludedFiles, IReadOnlyList<AiSafetyWarningDto> SafetyWarnings, AiTokenEstimateDto TokenEstimate, AiRunStatus Status, DateTimeOffset CreatedUtc, DateTimeOffset? CompletedUtc, string? Error, string? OutputText, string? OutputHash);
public sealed record AiProviderValidationIssueDto(string Code, string Severity, string Message, string? Field);
public sealed record AiProviderSetupStateDto(bool IsConfigured, bool HasModel, bool HasSecretWhenRequired, bool HasValidBaseUrl, bool IsLocalProvider, string? MaskedSecretHint);
public sealed record AiProviderValidationResultDto(string ProviderId, AiProviderKind Kind, bool IsValid, IReadOnlyList<AiProviderValidationIssueDto> Issues, AiProviderSetupStateDto SetupState);
public sealed record AiProviderErrorDetailsDto(string Code, string Category, string Message, string? TechnicalDetail);
public sealed record AiProviderConnectionDiagnosticDto(string ProviderId, AiProviderKind Kind, AiProviderStatus Status, string Summary, IReadOnlyList<string> SuggestedActions, AiProviderErrorDetailsDto? Error, DateTimeOffset CheckedUtc);
public sealed class AiSettingsDto
{
    public bool StoreFullPrompt { get; set; }
    public bool StoreOutput { get; set; }
    public int MaxRunsPerProject { get; set; }
    public int DeleteFailedRunsAfterDays { get; set; }
    public AiSettingsDto() { }
    public AiSettingsDto(bool storeFullPrompt, bool storeOutput, int maxRunsPerProject, int deleteFailedRunsAfterDays)
    {
        StoreFullPrompt = storeFullPrompt;
        StoreOutput = storeOutput;
        MaxRunsPerProject = maxRunsPerProject;
        DeleteFailedRunsAfterDays = deleteFailedRunsAfterDays;
    }
}
public sealed record AiUsageSummaryDto(Guid? ProjectId, int TotalRuns, int SuccessfulRuns, int FailedRuns, DateTimeOffset? LastRunUtc, string? MostUsedProvider, string? MostUsedPack, int EstimatedTokensUsed, decimal? EstimatedKnownCostUsd, int UnknownCostCount, int ProvidersConfigured, int FailedProviderChecks);
public sealed record AiContextPackQualityIssueDto(string Code, string Severity, string Message, string? RelativePath);
public sealed record AiContextPackQualityReportDto(Guid ProjectId, string PackId, int ReadinessScore, bool IsRecommended, IReadOnlyList<AiContextPackQualityIssueDto> Issues);
public sealed record AiExplanationOptionsDto(Guid ProjectId, IReadOnlyList<AiProviderStatusDto> Providers, IReadOnlyList<ContextPackManifest> Packs, string SuggestedFilePath, IReadOnlyList<ExplanationProfile> Profiles);
public sealed record AiExplanationRequestDto(Guid ProjectId, string ProviderId, string Model, string ContextPackId, ExplanationProfile ExplanationProfile, string? UserInstruction, bool IncludeSourceSnippets, bool IncludeTests, bool IncludeGeneratedDocs, bool IncludePublicApi, bool IncludeFlows, bool IncludeArchitecture, int MaxInputTokens, int MaxOutputTokens, bool StorePrompt, bool StoreOutput);
public sealed record AiExplanationSourceFileDto(string RelativePath, string Kind, int? StartLine, int? EndLine, int TokenEstimate, bool Included, string? Warning);
public sealed record AiExplanationPreviewDto(Guid ProjectId, string ProviderId, AiProviderKind ProviderKind, string Model, string ContextPackId, ExplanationProfile ExplanationProfile, IReadOnlyList<AiExplanationSourceFileDto> IncludedFiles, IReadOnlyList<string> MissingFiles, IReadOnlyList<AiSafetyWarningDto> SafetyWarnings, AiTokenEstimateDto TokenEstimate, AiCostEstimateDto CostEstimate, string PromptText, string PromptHash, bool IsBlocked);
public sealed record AiExplanationStreamUpdateDto(string Phase, string Message, string? DeltaMarkdown, int OutputCharacters, TimeSpan Elapsed, AiExplanationResultDto? Result, bool IsCompleted, bool IsFailed);
public sealed record AiExplanationJobStatusDto(Guid JobId, Guid ProjectId, string Phase, string Message, int OutputCharacters, DateTimeOffset StartedUtc, DateTimeOffset UpdatedUtc, TimeSpan Elapsed, bool IsCompleted, bool IsFailed, bool IsCancelled, string? MarkdownPreview, AiExplanationResultDto? Result);
public sealed record AiExplanationSectionDto(string Title, string Markdown);
public sealed record AiExplanationQualityReportDto(AiExplanationQualityStatus Status, IReadOnlyList<string> PassedChecks, IReadOnlyList<string> Warnings);
public sealed record AiExplanationResultDto(Guid Id, Guid ProjectId, string ProviderId, string Model, AiRunStatus Status, DateTimeOffset CreatedUtc, DateTimeOffset? CompletedUtc, string PromptHash, string? OutputHash, int EstimatedInputTokens, int EstimatedOutputTokens, IReadOnlyList<AiSafetyWarningDto> Warnings, IReadOnlyList<AiExplanationSectionDto> Sections, string? Markdown, string SuggestedFilePath, string? Error, AiExplanationQualityReportDto? Quality);
public sealed record AiExplanationSaveRequestDto(bool ConfirmOverwrite = false, string? MarkdownOverride = null);
public sealed record AiExplanationSaveResultDto(Guid ProjectId, Guid RunId, bool Saved, string SuggestedFilePath, string? SavedFilePath, IReadOnlyList<string> Warnings);
public sealed record DogfoodChecklistItemDto(string Id, string Label, bool IsComplete, string? Detail);
public sealed record DogfoodChecklistDto(IReadOnlyList<DogfoodChecklistItemDto> Items, string Status, IReadOnlyList<string> NextActions);
public sealed record DogfoodProjectReadinessDto(Guid? ProjectId, string? ProjectName, DogfoodChecklistDto Checklist, int Score);
public sealed record DiagnosticsSummaryDto(string AppVersion, string ApiVersion, StorageInfo Storage, ApiRuntimeInfoDto Runtime, IReadOnlyList<AiProviderStatusDto> ProviderStatuses, int ProjectCount, int JobCount, int AiRunCount, IReadOnlyList<string> RecentErrors, DateTimeOffset GeneratedUtc);
public sealed record DiagnosticsExportDto(string FileName, string Content, DateTimeOffset GeneratedUtc);
public sealed record DogfoodSessionDto(Guid Id, DateTimeOffset StartedUtc, DateTimeOffset? CompletedUtc, IReadOnlyList<DogfoodSessionProjectDto> Projects, IReadOnlyList<DogfoodSessionStepDto> Steps, IReadOnlyList<DogfoodSessionIssueDto> Issues, IReadOnlyList<DogfoodSessionNoteDto> Notes, string Outcome, IReadOnlyList<string> GeneratedRecommendations);
public sealed record DogfoodSessionProjectDto(Guid? ProjectId, string Name, string? Path, bool IsRegistered, bool IsScanned);
public sealed record DogfoodSessionStepDto(string Id, string Label, bool IsCompleted, DateTimeOffset? CompletedUtc, string? Detail);
public sealed record DogfoodSessionIssueDto(Guid Id, Guid? SessionId, Guid? ProjectId, string PageRoute, string Category, string Severity, string Title, string Description, string? ExpectedBehavior, string? ActualBehavior, string Status, DateTimeOffset CreatedUtc, string? OperationId);
public sealed record DogfoodSessionNoteDto(Guid Id, string Text, DateTimeOffset CreatedUtc);
public sealed record StartDogfoodSessionRequestDto(IReadOnlyList<DogfoodSessionProjectDto> Projects);
public sealed record UpdateIssueStatusRequestDto(string Status);
public sealed record NextActionDto(string Id, string Kind, string Priority, string Source, NextActionTargetDto Target, string Title, string Description, string? Href);
public sealed record NextActionTargetDto(Guid? ProjectId, string? ProjectName);
public sealed record ProjectPathValidationResultDto(string Path, bool Exists, bool IsDirectory, bool IsGitRepository, bool HasSolutionFile, bool IsSystemPath, bool CanRead, bool CanRegister, IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors, string SuggestedName);
public sealed record RecentProjectPathDto(string Path, string SuggestedName, DateTimeOffset LastUsedUtc);
public sealed record ProjectAiSettingsDto(Guid ProjectId, string? DefaultProviderId, string? DefaultModel, string DefaultContextPack, string DefaultPromptTemplate, int MaxOutputTokens, bool? StorePrompt, bool? StoreOutput, int FreshnessWarningThreshold);
public sealed record QuickAskRequestDto(Guid ProjectId, string Question, string? ProviderId, string? PackId, string? TemplateId, int? MaxOutputTokens, bool ConfirmSafetyWarnings);
public sealed record QuickAskResultDto(AiPromptPreviewResponse Preview, AiRunDetailDto? Run);
public sealed record LogEntryDto(Guid Id, string Level, string Source, string Message, DateTimeOffset CreatedUtc, string? CorrelationId, Guid? ProjectId);
