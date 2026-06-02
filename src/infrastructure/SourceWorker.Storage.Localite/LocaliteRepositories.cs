using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Storage.Localite;

public static class LocaliteStorage
{
    public static IServiceCollection AddSourceWorkerLocaliteStorage(this IServiceCollection services, Action<LocaliteStorageOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<LocaliteStorageOptions>().Configure<IConfiguration>((options, configuration) =>
            {
                options.DatabasePath = configuration["SourceWorker:Storage:DatabasePath"];
            });
        }

        services.AddSingleton<LocaliteTableStore>();
        services.AddSingleton<ILocalStorageSchemaService, LocaliteStorageSchemaService>();
        services.AddSingleton<IStorageInfoService, LocaliteStorageInfoService>();
        services.AddSingleton<IStorageHealthService, LocaliteStorageInfoService>();
        services.AddSingleton<IProjectRepository, LocaliteProjectRepository>();
        services.AddSingleton<IScanRunRepository, LocaliteScanRunRepository>();
        services.AddSingleton<IFileIndexRepository, LocaliteFileIndexRepository>();
        services.AddSingleton<IIndexedFileRepository, LocaliteIndexedFileRepository>();
        services.AddSingleton<IGeneratedArtifactRepository, LocaliteGeneratedArtifactRepository>();
        services.AddSingleton<IProjectInstructionRepository, LocaliteProjectInstructionRepository>();
        services.AddSingleton<IJobRepository, LocaliteJobRepository>();
        services.AddSingleton<ICodeSymbolRepository, LocaliteCodeSymbolRepository>();
        services.AddSingleton<ICodeFlowRepository, LocaliteCodeFlowRepository>();
        services.AddSingleton<IRouteInfoRepository, LocaliteRouteInfoRepository>();
        services.AddSingleton<IServiceRegistrationRepository, LocaliteServiceRegistrationRepository>();
        services.AddSingleton<IConfigurationUsageRepository, LocaliteConfigurationUsageRepository>();
        services.AddSingleton<IBackgroundWorkerInfoRepository, LocaliteBackgroundWorkerInfoRepository>();
        services.AddSingleton<ITestInfoRepository, LocaliteTestInfoRepository>();
        services.AddSingleton<IAiProviderConfigurationRepository, LocaliteAiProviderConfigurationRepository>();
        services.AddSingleton<IAiRunRepository, LocaliteAiRunRepository>();
        services.AddSingleton<IDogfoodSessionRepository, LocaliteDogfoodSessionRepository>();
        services.AddSingleton<IDogfoodIssueRepository, LocaliteDogfoodIssueRepository>();
        services.AddSingleton<IProjectAiSettingsRepository, LocaliteProjectAiSettingsRepository>();
        services.AddSingleton<ILogRepository, LocaliteLogRepository>();
        return services;
    }
}

public sealed class LocaliteStorageOptions
{
    public string? DatabasePath { get; set; }
}

public sealed class LocaliteTableStore
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string StoragePath { get; }

    public LocaliteTableStore(IOptions<LocaliteStorageOptions> options)
    {
        StoragePath = string.IsNullOrWhiteSpace(options.Value.DatabasePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceWorker", "sourceworker.localite.db")
            : options.Value.DatabasePath!;
        var directory = Path.GetDirectoryName(StoragePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        EnsureSchema();
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={StoragePath}");
        connection.Open();
        return connection;
    }

    public async Task UpsertAsync<T>(string table, Guid id, Guid? projectId, string? secondaryKey, T value, CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = $"""
        INSERT INTO {table} (Id, ProjectId, SecondaryKey, Json, UpdatedUtc)
        VALUES ($id, $projectId, $secondaryKey, $json, $updatedUtc)
        ON CONFLICT(Id) DO UPDATE SET ProjectId = excluded.ProjectId, SecondaryKey = excluded.SecondaryKey, Json = excluded.Json, UpdatedUtc = excluded.UpdatedUtc;
        """;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$projectId", projectId?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$secondaryKey", secondaryKey ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(value, JsonOptions));
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string table, Guid? projectId, CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = projectId is null ? $"SELECT Json FROM {table} ORDER BY UpdatedUtc, Id" : $"SELECT Json FROM {table} WHERE ProjectId = $projectId ORDER BY UpdatedUtc, Id";
        if (projectId is not null) command.Parameters.AddWithValue("$projectId", projectId.Value.ToString());
        var values = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), JsonOptions)!);
        }
        return values;
    }

    public async Task<T?> GetAsync<T>(string table, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT Json FROM {table} WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? JsonSerializer.Deserialize<T>(json, JsonOptions) : default;
    }

    public async Task DeleteByProjectAsync(string table, Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE ProjectId = $projectId";
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAsync(string table, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteProjectSecondaryDuplicatesAsync(string table, Guid projectId, string secondaryKey, Guid keepId, CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE ProjectId = $projectId AND SecondaryKey = $secondaryKey AND Id <> $keepId";
        command.Parameters.AddWithValue("$projectId", projectId.ToString());
        command.Parameters.AddWithValue("$secondaryKey", secondaryKey);
        command.Parameters.AddWithValue("$keepId", keepId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> CountAsync(string table, CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<LocalStorageSchemaInfo> SchemaInfoAsync(CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT CreatedUtc, LastMigratedUtc FROM LocalStorageSchemaInfo WHERE Id = 'sourceworker'";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new LocalStorageSchemaInfo("Localite", "table-backed", StoragePath, SchemaVersion, DateTimeOffset.Parse(reader.GetString(0)), DateTimeOffset.Parse(reader.GetString(1)), "3");
        }
        return new LocalStorageSchemaInfo("Localite", "table-backed", StoragePath, SchemaVersion, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "3");
    }

    private void EnsureSchema()
    {
        using var connection = Open();
        foreach (var table in Tables.All)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {table} (
              Id TEXT PRIMARY KEY,
              ProjectId TEXT NULL,
              SecondaryKey TEXT NULL,
              Json TEXT NOT NULL,
              UpdatedUtc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_{table}_ProjectId ON {table}(ProjectId);
            CREATE INDEX IF NOT EXISTS IX_{table}_SecondaryKey ON {table}(SecondaryKey);
            """;
            command.ExecuteNonQuery();
        }
        using var schema = connection.CreateCommand();
        schema.CommandText = """
        CREATE TABLE IF NOT EXISTS LocalStorageSchemaInfo (
          Id TEXT PRIMARY KEY,
          SchemaVersion INTEGER NOT NULL,
          CreatedUtc TEXT NOT NULL,
          LastMigratedUtc TEXT NOT NULL,
          AppStorageFormatVersion TEXT NOT NULL
        );
        INSERT INTO LocalStorageSchemaInfo (Id, SchemaVersion, CreatedUtc, LastMigratedUtc, AppStorageFormatVersion)
        VALUES ('sourceworker', 1, datetime('now'), datetime('now'), '3')
        ON CONFLICT(Id) DO UPDATE SET SchemaVersion = excluded.SchemaVersion, LastMigratedUtc = excluded.LastMigratedUtc, AppStorageFormatVersion = excluded.AppStorageFormatVersion;
        """;
        schema.ExecuteNonQuery();
    }
}

internal static class Tables
{
    public const string Projects = "ProjectRecord";
    public const string ScanRuns = "ScanRunRecord";
    public const string IndexedFiles = "IndexedFileRecord";
    public const string DetectedTechnologies = "DetectedTechnologyRecord";
    public const string GitSnapshots = "GitSnapshotRecord";
    public const string Todos = "TodoItemRecord";
    public const string GeneratedArtifacts = "GeneratedArtifactRecord";
    public const string InstructionDocuments = "ProjectInstructionDocumentRecord";
    public const string InstructionSections = "ProjectInstructionSectionRecord";
    public const string InstructionSourcePaths = "ProjectInstructionSourcePathRecord";
    public const string SourcePathValidationResults = "SourcePathValidationResultRecord";
    public const string PromptRecipes = "PromptRecipeRecord";
    public const string WorkerJobs = "WorkerJobRecord";
    public const string WorkerJobSteps = "WorkerJobStepRecord";
    public const string DoctorReports = "DoctorReportRecord";
    public const string CodeSymbols = "CodeSymbolRecord";
    public const string CodeFileSummaries = "CodeFileSummaryRecord";
    public const string CodeProjectSummaries = "CodeProjectSummaryRecord";
    public const string CodeFlows = "CodeFlowRecord";
    public const string CodeFlowSteps = "CodeFlowStepRecord";
    public const string Routes = "RouteInfoRecord";
    public const string ConfigurationUsages = "ConfigurationUsageRecord";
    public const string ServiceRegistrations = "ServiceRegistrationRecord";
    public const string BackgroundWorkers = "BackgroundWorkerInfoRecord";
    public const string Tests = "TestInfoRecord";
    public const string AiProviderConfigurations = "AiProviderConfigurationRecord";
    public const string AiRuns = "AiRunRecord";
    public const string AiSettings = "AiSettingsRecord";
    public const string DogfoodSessions = "DogfoodSessionRecord";
    public const string DogfoodIssues = "DogfoodIssueRecord";
    public const string ProjectAiSettings = "ProjectAiSettingsRecord";
    public const string Logs = "LogEntryRecord";
    public static readonly string[] All = [Projects, ScanRuns, IndexedFiles, DetectedTechnologies, GitSnapshots, Todos, GeneratedArtifacts, InstructionDocuments, InstructionSections, InstructionSourcePaths, SourcePathValidationResults, PromptRecipes, WorkerJobs, WorkerJobSteps, DoctorReports, CodeSymbols, CodeFileSummaries, CodeProjectSummaries, CodeFlows, CodeFlowSteps, Routes, ConfigurationUsages, ServiceRegistrations, BackgroundWorkers, Tests, AiProviderConfigurations, AiRuns, AiSettings, DogfoodSessions, DogfoodIssues, ProjectAiSettings, Logs];
}

internal sealed record ProjectRecord(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt);
internal sealed record ScanRunRecord(Guid Id, Guid ProjectId, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, ScanRunStatus Status, int FilesIndexed, int TodosFound, int TotalFilesSeen, int IgnoredFileCount, int ChangedFileCount, int DetectedTechnologyCount, string? ErrorMessage);
internal sealed record IndexedFileRecord(Guid Id, Guid ProjectId, Guid ScanRunId, string Path, string RelativePath, string Extension, long SizeBytes, DateTimeOffset LastModifiedUtc, string Sha256, IndexedFileKind Kind, bool WasIgnored, string? IgnoreReason, FileChangeKind ChangeKind);
internal sealed record GeneratedArtifactRecord(Guid Id, Guid ProjectId, Guid? ScanRunId, string? RecipeId, string RelativePath, string AbsolutePath, GeneratedArtifactType Type, string ContentHash, DateTimeOffset GeneratedUtc, string SourceInputHash, GeneratedArtifactStatus Status, string GeneratorName, string? Notes);
internal sealed record WorkerJobRecord(Guid Id, Guid? ProjectId, WorkerJobType Type, WorkerJobStatus Status, int ProgressPercent, string? Message, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? CurrentStep, string? ErrorMessage, IReadOnlyList<string>? Logs);
internal sealed record AiProviderConfigurationRecord(Guid Id, AiProviderKind Kind, string Name, string Model, string? BaseUrl, bool IsEnabled, string? ApiKeySecretName, string? MaskedApiKey, DateTimeOffset UpdatedUtc);
internal sealed record AiRunRecord(Guid Id, Guid ProjectId, string ProviderId, AiProviderKind ProviderKind, string Model, string PackId, string? UserInstruction, string PromptHash, bool StoredPromptText, string? PromptPreview, IReadOnlyList<string> IncludedFiles, IReadOnlyList<AiSafetyWarning> SafetyWarnings, AiTokenEstimate TokenEstimate, AiRunStatus Status, DateTimeOffset CreatedUtc, DateTimeOffset? CompletedUtc, string? Error, string? OutputText, string? OutputHash);

internal static class ProjectRecordMapper { public static ProjectRecord ToRecord(Project p) => new(p.Id, p.Name, p.RootPath, p.CreatedAt, p.LastScannedAt); public static Project ToDomain(ProjectRecord r) => new(r.Id, r.Name, r.RootPath, r.CreatedAt, r.LastScannedAt); }
internal static class ScanRunRecordMapper { public static ScanRunRecord ToRecord(ScanRun s) => new(s.Id, s.ProjectId, s.StartedAt, s.CompletedAt, s.Status, s.FilesIndexed, s.TodosFound, s.TotalFilesSeen, s.IgnoredFileCount, s.ChangedFileCount, s.DetectedTechnologyCount, s.ErrorMessage); public static ScanRun ToDomain(ScanRunRecord r) => new(r.Id, r.ProjectId, r.StartedAt, r.CompletedAt, r.Status, r.FilesIndexed, r.TodosFound, r.TotalFilesSeen, r.IgnoredFileCount, r.ChangedFileCount, r.DetectedTechnologyCount, r.ErrorMessage); }
internal static class IndexedFileRecordMapper { public static IndexedFileRecord ToRecord(IndexedFile f) => new(f.Id, f.ProjectId, f.ScanRunId, f.Path, f.RelativePath, f.Extension, f.SizeBytes, f.LastModifiedUtc, f.Sha256, f.Kind, f.WasIgnored, f.IgnoreReason, f.ChangeKind); public static IndexedFile ToDomain(IndexedFileRecord r) => new(r.Id, r.ProjectId, r.ScanRunId, r.Path, r.RelativePath, r.Extension, r.SizeBytes, r.LastModifiedUtc, r.Sha256, r.Kind, r.WasIgnored, r.IgnoreReason, r.ChangeKind); }
internal static class GeneratedArtifactRecordMapper { public static GeneratedArtifactRecord ToRecord(GeneratedArtifact a) => new(a.Id, a.ProjectId, a.ScanRunId, a.RecipeId, a.RelativePath, a.AbsolutePath, a.Type, a.ContentHash, a.GeneratedUtc, a.SourceInputHash, a.Status, a.GeneratorName, a.Notes); public static GeneratedArtifact ToDomain(GeneratedArtifactRecord r) => new(r.Id, r.ProjectId, r.ScanRunId, r.RecipeId, r.RelativePath, r.AbsolutePath, r.Type, r.ContentHash, r.GeneratedUtc, r.SourceInputHash, r.Status, r.GeneratorName, r.Notes); }
internal static class JobRecordMapper { public static WorkerJobRecord ToRecord(WorkerJob j) => new(j.Id, j.ProjectId, j.Type, j.Status, j.ProgressPercent, j.Message, j.CreatedAt, j.StartedAt, j.CompletedAt, j.CurrentStep, j.ErrorMessage, j.Logs); public static WorkerJob ToDomain(WorkerJobRecord r) => new(r.Id, r.ProjectId, r.Type, r.Status, r.ProgressPercent, r.Message, r.CreatedAt, r.StartedAt, r.CompletedAt, r.CurrentStep, r.ErrorMessage, r.Logs); }
internal static class InstructionRecordMapper { public static ProjectInstructionDocument ToDomain(ProjectInstructionDocument d) => d; public static ProjectInstructionDocument ToRecord(ProjectInstructionDocument d) => d; }
internal static class RecipeRecordMapper { public static ProjectRecipe ToDomain(ProjectRecipe r) => r; public static ProjectRecipe ToRecord(ProjectRecipe r) => r; }
internal static class DoctorReportRecordMapper { public static DoctorReport ToDomain(DoctorReport r) => r; public static DoctorReport ToRecord(DoctorReport r) => r; }
internal static class AiProviderConfigurationRecordMapper { public static AiProviderConfigurationRecord ToRecord(AiProviderConfiguration c) => new(c.Id, c.Kind, c.Name, c.Model, c.BaseUrl, c.IsEnabled, c.ApiKeySecretName, c.MaskedApiKey, c.UpdatedUtc); public static AiProviderConfiguration ToDomain(AiProviderConfigurationRecord r) => new(r.Id, r.Kind, r.Name, r.Model, r.BaseUrl, r.IsEnabled, r.ApiKeySecretName, r.MaskedApiKey, r.UpdatedUtc); }
internal static class AiRunRecordMapper { public static AiRunRecord ToRecord(AiRun r) => new(r.Id, r.ProjectId, r.ProviderId, r.ProviderKind, r.Model, r.PackId, r.UserInstruction, r.PromptHash, r.StoredPromptText, r.PromptPreview, r.IncludedFiles, r.SafetyWarnings, r.TokenEstimate, r.Status, r.CreatedUtc, r.CompletedUtc, r.Error, r.OutputText, r.OutputHash); public static AiRun ToDomain(AiRunRecord r) => new(r.Id, r.ProjectId, r.ProviderId, r.ProviderKind, r.Model, r.PackId, r.UserInstruction, r.PromptHash, r.StoredPromptText, r.PromptPreview, r.IncludedFiles, r.SafetyWarnings, r.TokenEstimate, r.Status, r.CreatedUtc, r.CompletedUtc, r.Error, r.OutputText, r.OutputHash); }

public sealed class LocaliteProjectRepository(LocaliteTableStore store) : IProjectRepository
{
    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken) { await store.UpsertAsync(Tables.Projects, project.Id, project.Id, project.RootPath, ProjectRecordMapper.ToRecord(project), cancellationToken); return project; }
    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) => (await store.GetAsync<ProjectRecord>(Tables.Projects, id, cancellationToken)) is { } r ? ProjectRecordMapper.ToDomain(r) : null;
    public async Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken) => (await ListAsync(cancellationToken)).FirstOrDefault(p => Path.GetFullPath(p.RootPath).Equals(Path.GetFullPath(rootPath), StringComparison.OrdinalIgnoreCase));
    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) => (await store.ListAsync<ProjectRecord>(Tables.Projects, null, cancellationToken)).Select(ProjectRecordMapper.ToDomain).ToArray();
    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken) { await AddAsync(project, cancellationToken); return project; }
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        foreach (var table in Tables.All.Where(table => !table.Equals(Tables.AiProviderConfigurations, StringComparison.OrdinalIgnoreCase)))
        {
            await store.DeleteByProjectAsync(table, id, cancellationToken);
        }

        await store.DeleteAsync(Tables.Projects, id, cancellationToken);
    }
}

public sealed class LocaliteScanRunRepository(LocaliteTableStore store) : IScanRunRepository
{
    public async Task<ScanRun> AddAsync(ScanRun scanRun, CancellationToken cancellationToken) { await store.UpsertAsync(Tables.ScanRuns, scanRun.Id, scanRun.ProjectId, scanRun.StartedAt.ToString("O"), ScanRunRecordMapper.ToRecord(scanRun), cancellationToken); return scanRun; }
    public async Task<ScanRun?> GetAsync(Guid id, CancellationToken cancellationToken) => (await store.GetAsync<ScanRunRecord>(Tables.ScanRuns, id, cancellationToken)) is { } r ? ScanRunRecordMapper.ToDomain(r) : null;
    public async Task<IReadOnlyList<ScanRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => (await store.ListAsync<ScanRunRecord>(Tables.ScanRuns, projectId, cancellationToken)).Select(ScanRunRecordMapper.ToDomain).OrderByDescending(r => r.StartedAt).ToArray();
    public async Task<ScanRun?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken) => (await GetByProjectAsync(projectId, cancellationToken)).FirstOrDefault();
    public async Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken) { await AddAsync(scanRun, cancellationToken); return scanRun; }
}

public sealed class LocaliteIndexedFileRepository(LocaliteTableStore store) : IIndexedFileRepository
{
    public async Task SaveForScanAsync(Guid projectId, Guid scanRunId, IReadOnlyList<IndexedFile> files, CancellationToken cancellationToken) { foreach (var file in files) await store.UpsertAsync(Tables.IndexedFiles, file.Id, projectId, scanRunId.ToString(), IndexedFileRecordMapper.ToRecord(file), cancellationToken); }
    public async Task<IReadOnlyList<IndexedFile>> GetByScanRunAsync(Guid scanRunId, CancellationToken cancellationToken) => (await store.ListAsync<IndexedFileRecord>(Tables.IndexedFiles, null, cancellationToken)).Where(f => f.ScanRunId == scanRunId).Select(IndexedFileRecordMapper.ToDomain).OrderBy(f => f.RelativePath).ToArray();
    public async Task<IReadOnlyList<IndexedFile>> GetLatestByProjectAsync(Guid projectId, CancellationToken cancellationToken) { var latest = (await store.ListAsync<ScanRunRecord>(Tables.ScanRuns, projectId, cancellationToken)).OrderByDescending(r => r.StartedAt).FirstOrDefault(); return latest is null ? [] : await GetByScanRunAsync(latest.Id, cancellationToken); }
}

public sealed class LocaliteFileIndexRepository(LocaliteTableStore store) : IFileIndexRepository
{
    public async Task ReplaceForScanAsync(Guid scanRunId, IReadOnlyList<IndexedFile> files, IReadOnlyList<TodoItem> todos, IReadOnlyList<DetectedTechnology> technologies, CancellationToken cancellationToken) { foreach (var todo in todos) await store.UpsertAsync(Tables.Todos, todo.Id, todo.ProjectId, scanRunId.ToString(), todo, cancellationToken); foreach (var tech in technologies) await store.UpsertAsync(Tables.DetectedTechnologies, tech.Id, tech.ProjectId, scanRunId.ToString(), tech, cancellationToken); }
}

public sealed class LocaliteGeneratedArtifactRepository(LocaliteTableStore store) : IGeneratedArtifactRepository
{
    public async Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken)
    {
        var existing = await GetByPathAsync(artifact.ProjectId, artifact.RelativePath, cancellationToken);
        var value = existing is null ? artifact : artifact with { Id = existing.Id };
        await store.UpsertAsync(Tables.GeneratedArtifacts, value.Id, value.ProjectId, value.RelativePath, GeneratedArtifactRecordMapper.ToRecord(value), cancellationToken);
        await store.DeleteProjectSecondaryDuplicatesAsync(Tables.GeneratedArtifacts, value.ProjectId, value.RelativePath, value.Id, cancellationToken);
        return value;
    }

    public async Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
        => (await store.ListAsync<GeneratedArtifactRecord>(Tables.GeneratedArtifacts, projectId, cancellationToken))
            .Select(GeneratedArtifactRecordMapper.ToDomain)
            .GroupBy(a => a.RelativePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(a => a.GeneratedUtc).First())
            .OrderBy(a => a.RelativePath)
            .ToArray();

    public async Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken)
        => (await store.ListAsync<GeneratedArtifactRecord>(Tables.GeneratedArtifacts, projectId, cancellationToken))
            .Select(GeneratedArtifactRecordMapper.ToDomain)
            .Where(a => a.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.GeneratedUtc)
            .FirstOrDefault();
}

public sealed class LocaliteProjectInstructionRepository(LocaliteTableStore store) : IProjectInstructionRepository
{
    public async Task<ProjectInstructionDocument?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken) => (await store.ListAsync<ProjectInstructionDocument>(Tables.InstructionDocuments, projectId, cancellationToken)).OrderByDescending(d => d.ImportedAt).FirstOrDefault();
    public async Task<ProjectInstructionDocument> SaveAsync(ProjectInstructionDocument document, CancellationToken cancellationToken) { await store.UpsertAsync(Tables.InstructionDocuments, document.Id, document.ProjectId, document.SourceFilePath, document, cancellationToken); foreach (var section in document.Sections) await store.UpsertAsync(Tables.InstructionSections, Guid.NewGuid(), document.ProjectId, document.Id.ToString(), section, cancellationToken); foreach (var path in document.SourcePaths) await store.UpsertAsync(Tables.InstructionSourcePaths, Guid.NewGuid(), document.ProjectId, document.Id.ToString(), path, cancellationToken); return document; }
}

public sealed class LocaliteJobRepository(LocaliteTableStore store) : IJobRepository
{
    public async Task<WorkerJob> AddAsync(WorkerJob job, CancellationToken cancellationToken) { await store.UpsertAsync(Tables.WorkerJobs, job.Id, job.ProjectId, job.Type.ToString(), JobRecordMapper.ToRecord(job), cancellationToken); return job; }
    public async Task<WorkerJob?> GetAsync(Guid id, CancellationToken cancellationToken) => (await store.GetAsync<WorkerJobRecord>(Tables.WorkerJobs, id, cancellationToken)) is { } r ? JobRecordMapper.ToDomain(r) : null;
    public async Task<IReadOnlyList<WorkerJob>> ListAsync(CancellationToken cancellationToken) => (await store.ListAsync<WorkerJobRecord>(Tables.WorkerJobs, null, cancellationToken)).Select(JobRecordMapper.ToDomain).OrderByDescending(j => j.CreatedAt).ToArray();
    public async Task<WorkerJob> UpdateAsync(WorkerJob job, CancellationToken cancellationToken) { await AddAsync(job, cancellationToken); return job; }
    public Task AddStepAsync(WorkerJobStep step, CancellationToken cancellationToken) => store.UpsertAsync(Tables.WorkerJobSteps, step.Id, null, step.JobId.ToString(), step, cancellationToken);
}

public sealed class LocaliteStorageSchemaService(LocaliteTableStore store) : ILocalStorageSchemaService { public Task<LocalStorageSchemaInfo> GetInfoAsync(CancellationToken cancellationToken) => store.SchemaInfoAsync(cancellationToken); }

public sealed class LocaliteStorageInfoService(LocaliteTableStore store) : IStorageInfoService, IStorageHealthService
{
    public async Task<StorageInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        var projects = await store.ListAsync<ProjectRecord>(Tables.Projects, null, cancellationToken);
        var scanRuns = await store.ListAsync<ScanRunRecord>(Tables.ScanRuns, null, cancellationToken);
        var latestScanIds = scanRuns
            .GroupBy(s => s.ProjectId)
            .Select(g => g.OrderByDescending(s => s.StartedAt).First().Id)
            .ToHashSet();
        var currentIndexedFileCount = (await store.ListAsync<IndexedFileRecord>(Tables.IndexedFiles, null, cancellationToken))
            .Count(f => latestScanIds.Contains(f.ScanRunId));
        var currentArtifactCount = (await store.ListAsync<GeneratedArtifactRecord>(Tables.GeneratedArtifacts, null, cancellationToken))
            .Select(GeneratedArtifactRecordMapper.ToDomain)
            .GroupBy(a => $"{a.ProjectId:N}:{a.RelativePath}", StringComparer.OrdinalIgnoreCase)
            .Count();

        return new(await store.SchemaInfoAsync(cancellationToken), projects.Count, scanRuns.Count, currentIndexedFileCount, currentArtifactCount, await store.CountAsync(Tables.WorkerJobs, cancellationToken), await store.CountAsync(Tables.InstructionDocuments, cancellationToken), await store.CountAsync(Tables.PromptRecipes, cancellationToken), true, await store.CountAsync(Tables.CodeSymbols, cancellationToken), await store.CountAsync(Tables.CodeFlows, cancellationToken), await store.CountAsync(Tables.Routes, cancellationToken), await store.CountAsync(Tables.ServiceRegistrations, cancellationToken), await store.CountAsync(Tables.ConfigurationUsages, cancellationToken), await store.CountAsync(Tables.Tests, cancellationToken), await store.CountAsync(Tables.AiProviderConfigurations, cancellationToken), await store.CountAsync(Tables.AiRuns, cancellationToken));
    }

    public Task<bool> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(File.Exists(store.StoragePath));
}

public sealed class LocaliteCodeSymbolRepository(LocaliteTableStore store) : ICodeSymbolRepository
{
    public async Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeSymbol> symbols, CancellationToken cancellationToken) { await store.DeleteByProjectAsync(Tables.CodeSymbols, projectId, cancellationToken); foreach (var item in symbols) await store.UpsertAsync(Tables.CodeSymbols, item.Id, projectId, scanRunId.ToString(), item, cancellationToken); }
    public Task<IReadOnlyList<CodeSymbol>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.ListAsync<CodeSymbol>(Tables.CodeSymbols, projectId, cancellationToken);
}
public sealed class LocaliteCodeFlowRepository(LocaliteTableStore store) : ICodeFlowRepository
{
    public async Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<CodeFlow> flows, CancellationToken cancellationToken) { await store.DeleteByProjectAsync(Tables.CodeFlows, projectId, cancellationToken); foreach (var item in flows) await store.UpsertAsync(Tables.CodeFlows, item.Id, projectId, scanRunId.ToString(), item, cancellationToken); }
    public Task<IReadOnlyList<CodeFlow>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.ListAsync<CodeFlow>(Tables.CodeFlows, projectId, cancellationToken);
}
public sealed class LocaliteRouteInfoRepository(LocaliteTableStore store) : IRouteInfoRepository
{
    public async Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<RouteInfo> routes, CancellationToken cancellationToken) { await store.DeleteByProjectAsync(Tables.Routes, projectId, cancellationToken); foreach (var item in routes) await store.UpsertAsync(Tables.Routes, item.Id, projectId, scanRunId.ToString(), item, cancellationToken); }
    public Task<IReadOnlyList<RouteInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.ListAsync<RouteInfo>(Tables.Routes, projectId, cancellationToken);
}
public sealed class LocaliteServiceRegistrationRepository(LocaliteTableStore store) : IServiceRegistrationRepository
{
    public async Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ServiceRegistration> registrations, CancellationToken cancellationToken) { await store.DeleteByProjectAsync(Tables.ServiceRegistrations, projectId, cancellationToken); foreach (var item in registrations) await store.UpsertAsync(Tables.ServiceRegistrations, item.Id, projectId, scanRunId.ToString(), item, cancellationToken); }
    public Task<IReadOnlyList<ServiceRegistration>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.ListAsync<ServiceRegistration>(Tables.ServiceRegistrations, projectId, cancellationToken);
}
public sealed class LocaliteConfigurationUsageRepository(LocaliteTableStore store) : IConfigurationUsageRepository
{
    public async Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<ConfigurationUsage> usages, CancellationToken cancellationToken) { await store.DeleteByProjectAsync(Tables.ConfigurationUsages, projectId, cancellationToken); foreach (var item in usages) await store.UpsertAsync(Tables.ConfigurationUsages, item.Id, projectId, scanRunId.ToString(), item, cancellationToken); }
    public Task<IReadOnlyList<ConfigurationUsage>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.ListAsync<ConfigurationUsage>(Tables.ConfigurationUsages, projectId, cancellationToken);
}
public sealed class LocaliteBackgroundWorkerInfoRepository(LocaliteTableStore store) : IBackgroundWorkerInfoRepository
{
    public async Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<BackgroundWorkerInfo> workers, CancellationToken cancellationToken) { await store.DeleteByProjectAsync(Tables.BackgroundWorkers, projectId, cancellationToken); foreach (var item in workers) await store.UpsertAsync(Tables.BackgroundWorkers, item.Id, projectId, scanRunId.ToString(), item, cancellationToken); }
    public Task<IReadOnlyList<BackgroundWorkerInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.ListAsync<BackgroundWorkerInfo>(Tables.BackgroundWorkers, projectId, cancellationToken);
}
public sealed class LocaliteTestInfoRepository(LocaliteTableStore store) : ITestInfoRepository
{
    public async Task SaveAsync(Guid projectId, Guid scanRunId, IReadOnlyList<TestInfo> tests, CancellationToken cancellationToken) { await store.DeleteByProjectAsync(Tables.Tests, projectId, cancellationToken); foreach (var item in tests) await store.UpsertAsync(Tables.Tests, item.Id, projectId, scanRunId.ToString(), item, cancellationToken); }
    public Task<IReadOnlyList<TestInfo>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.ListAsync<TestInfo>(Tables.Tests, projectId, cancellationToken);
}

public sealed class LocaliteAiProviderConfigurationRepository(LocaliteTableStore store) : IAiProviderConfigurationRepository
{
    public async Task<AiProviderConfiguration> SaveAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(configuration.Kind.ToString(), cancellationToken);
        var value = existing is null ? configuration : configuration with { Id = existing.Id };
        await store.UpsertAsync(Tables.AiProviderConfigurations, value.Id, null, value.Kind.ToString(), AiProviderConfigurationRecordMapper.ToRecord(value), cancellationToken);
        return value;
    }

    public async Task<AiProviderConfiguration?> GetAsync(string providerId, CancellationToken cancellationToken)
        => (await ListAsync(cancellationToken)).FirstOrDefault(c => c.Kind.ToString().Equals(providerId, StringComparison.OrdinalIgnoreCase) || c.Id.ToString().Equals(providerId, StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<AiProviderConfiguration>> ListAsync(CancellationToken cancellationToken)
        => (await store.ListAsync<AiProviderConfigurationRecord>(Tables.AiProviderConfigurations, null, cancellationToken)).Select(AiProviderConfigurationRecordMapper.ToDomain).OrderBy(c => c.Kind.ToString()).ToArray();
}

public sealed class LocaliteAiRunRepository(LocaliteTableStore store) : IAiRunRepository
{
    public async Task<AiRun> SaveAsync(AiRun run, CancellationToken cancellationToken)
    {
        await store.UpsertAsync(Tables.AiRuns, run.Id, run.ProjectId, run.CreatedUtc.ToString("O"), AiRunRecordMapper.ToRecord(run), cancellationToken);
        return run;
    }

    public async Task<AiRun?> GetAsync(Guid runId, CancellationToken cancellationToken)
        => (await store.GetAsync<AiRunRecord>(Tables.AiRuns, runId, cancellationToken)) is { } record ? AiRunRecordMapper.ToDomain(record) : null;

    public async Task<IReadOnlyList<AiRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken)
        => (await store.ListAsync<AiRunRecord>(Tables.AiRuns, projectId, cancellationToken)).Select(AiRunRecordMapper.ToDomain).OrderByDescending(r => r.CreatedUtc).ToArray();

    public Task DeleteAsync(Guid runId, CancellationToken cancellationToken) => store.DeleteAsync(Tables.AiRuns, runId, cancellationToken);
    public Task DeleteByProjectAsync(Guid projectId, CancellationToken cancellationToken) => store.DeleteByProjectAsync(Tables.AiRuns, projectId, cancellationToken);
}

public sealed class LocaliteDogfoodSessionRepository(LocaliteTableStore store) : IDogfoodSessionRepository
{
    public async Task<DogfoodSession> SaveAsync(DogfoodSession session, CancellationToken cancellationToken)
    {
        await store.UpsertAsync(Tables.DogfoodSessions, session.Id, null, session.StartedUtc.ToString("O"), session, cancellationToken);
        return session;
    }

    public Task<DogfoodSession?> GetAsync(Guid id, CancellationToken cancellationToken) => store.GetAsync<DogfoodSession>(Tables.DogfoodSessions, id, cancellationToken);
    public async Task<IReadOnlyList<DogfoodSession>> ListAsync(CancellationToken cancellationToken) => (await store.ListAsync<DogfoodSession>(Tables.DogfoodSessions, null, cancellationToken)).OrderByDescending(s => s.StartedUtc).ToArray();
}

public sealed class LocaliteDogfoodIssueRepository(LocaliteTableStore store) : IDogfoodIssueRepository
{
    public async Task<DogfoodSessionIssue> SaveAsync(DogfoodSessionIssue issue, CancellationToken cancellationToken)
    {
        await store.UpsertAsync(Tables.DogfoodIssues, issue.Id, issue.ProjectId, issue.CreatedUtc.ToString("O"), issue, cancellationToken);
        return issue;
    }

    public Task<DogfoodSessionIssue?> GetAsync(Guid id, CancellationToken cancellationToken) => store.GetAsync<DogfoodSessionIssue>(Tables.DogfoodIssues, id, cancellationToken);
    public async Task<IReadOnlyList<DogfoodSessionIssue>> ListAsync(CancellationToken cancellationToken) => (await store.ListAsync<DogfoodSessionIssue>(Tables.DogfoodIssues, null, cancellationToken)).OrderByDescending(i => i.CreatedUtc).ToArray();
}

public sealed class LocaliteProjectAiSettingsRepository(LocaliteTableStore store) : IProjectAiSettingsRepository
{
    public async Task<ProjectAiSettings> GetAsync(Guid projectId, CancellationToken cancellationToken)
        => await store.GetAsync<ProjectAiSettings>(Tables.ProjectAiSettings, projectId, cancellationToken) ?? new ProjectAiSettings(projectId, null, null, "ai-handoff", "general-project-question", 1200, null, null, 70);

    public async Task<ProjectAiSettings> SaveAsync(ProjectAiSettings settings, CancellationToken cancellationToken)
    {
        await store.UpsertAsync(Tables.ProjectAiSettings, settings.ProjectId, settings.ProjectId, "ai-settings", settings, cancellationToken);
        return settings;
    }
}

public sealed class LocaliteLogRepository(LocaliteTableStore store) : ILogRepository
{
    public async Task<LogEntry> AddAsync(LogEntry entry, CancellationToken cancellationToken)
    {
        await store.UpsertAsync(Tables.Logs, entry.Id, entry.ProjectId, entry.CreatedUtc.ToString("O"), entry, cancellationToken);
        return entry;
    }

    public async Task<IReadOnlyList<LogEntry>> ListAsync(CancellationToken cancellationToken)
        => (await store.ListAsync<LogEntry>(Tables.Logs, null, cancellationToken)).OrderByDescending(l => l.CreatedUtc).Take(200).ToArray();
}
