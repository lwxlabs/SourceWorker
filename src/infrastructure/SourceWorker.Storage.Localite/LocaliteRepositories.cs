using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Storage.Localite;

public static class LocaliteStorage
{
    public static IServiceCollection AddSourceWorkerLocaliteStorage(this IServiceCollection services)
    {
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
        return services;
    }
}

public sealed class LocaliteTableStore
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string StoragePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceWorker", "sourceworker.localite.db");

    public LocaliteTableStore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
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
    public static readonly string[] All = [Projects, ScanRuns, IndexedFiles, DetectedTechnologies, GitSnapshots, Todos, GeneratedArtifacts, InstructionDocuments, InstructionSections, InstructionSourcePaths, SourcePathValidationResults, PromptRecipes, WorkerJobs, WorkerJobSteps, DoctorReports];
}

internal sealed record ProjectRecord(Guid Id, string Name, string RootPath, DateTimeOffset CreatedAt, DateTimeOffset? LastScannedAt);
internal sealed record ScanRunRecord(Guid Id, Guid ProjectId, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, ScanRunStatus Status, int FilesIndexed, int TodosFound, int TotalFilesSeen, int IgnoredFileCount, int ChangedFileCount, int DetectedTechnologyCount, string? ErrorMessage);
internal sealed record IndexedFileRecord(Guid Id, Guid ProjectId, Guid ScanRunId, string Path, string RelativePath, string Extension, long SizeBytes, DateTimeOffset LastModifiedUtc, string Sha256, IndexedFileKind Kind, bool WasIgnored, string? IgnoreReason, FileChangeKind ChangeKind);
internal sealed record GeneratedArtifactRecord(Guid Id, Guid ProjectId, Guid? ScanRunId, string? RecipeId, string RelativePath, string AbsolutePath, GeneratedArtifactType Type, string ContentHash, DateTimeOffset GeneratedUtc, string SourceInputHash, GeneratedArtifactStatus Status, string GeneratorName, string? Notes);
internal sealed record WorkerJobRecord(Guid Id, Guid? ProjectId, WorkerJobType Type, WorkerJobStatus Status, int ProgressPercent, string? Message, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? CurrentStep, string? ErrorMessage, IReadOnlyList<string>? Logs);

internal static class ProjectRecordMapper { public static ProjectRecord ToRecord(Project p) => new(p.Id, p.Name, p.RootPath, p.CreatedAt, p.LastScannedAt); public static Project ToDomain(ProjectRecord r) => new(r.Id, r.Name, r.RootPath, r.CreatedAt, r.LastScannedAt); }
internal static class ScanRunRecordMapper { public static ScanRunRecord ToRecord(ScanRun s) => new(s.Id, s.ProjectId, s.StartedAt, s.CompletedAt, s.Status, s.FilesIndexed, s.TodosFound, s.TotalFilesSeen, s.IgnoredFileCount, s.ChangedFileCount, s.DetectedTechnologyCount, s.ErrorMessage); public static ScanRun ToDomain(ScanRunRecord r) => new(r.Id, r.ProjectId, r.StartedAt, r.CompletedAt, r.Status, r.FilesIndexed, r.TodosFound, r.TotalFilesSeen, r.IgnoredFileCount, r.ChangedFileCount, r.DetectedTechnologyCount, r.ErrorMessage); }
internal static class IndexedFileRecordMapper { public static IndexedFileRecord ToRecord(IndexedFile f) => new(f.Id, f.ProjectId, f.ScanRunId, f.Path, f.RelativePath, f.Extension, f.SizeBytes, f.LastModifiedUtc, f.Sha256, f.Kind, f.WasIgnored, f.IgnoreReason, f.ChangeKind); public static IndexedFile ToDomain(IndexedFileRecord r) => new(r.Id, r.ProjectId, r.ScanRunId, r.Path, r.RelativePath, r.Extension, r.SizeBytes, r.LastModifiedUtc, r.Sha256, r.Kind, r.WasIgnored, r.IgnoreReason, r.ChangeKind); }
internal static class GeneratedArtifactRecordMapper { public static GeneratedArtifactRecord ToRecord(GeneratedArtifact a) => new(a.Id, a.ProjectId, a.ScanRunId, a.RecipeId, a.RelativePath, a.AbsolutePath, a.Type, a.ContentHash, a.GeneratedUtc, a.SourceInputHash, a.Status, a.GeneratorName, a.Notes); public static GeneratedArtifact ToDomain(GeneratedArtifactRecord r) => new(r.Id, r.ProjectId, r.ScanRunId, r.RecipeId, r.RelativePath, r.AbsolutePath, r.Type, r.ContentHash, r.GeneratedUtc, r.SourceInputHash, r.Status, r.GeneratorName, r.Notes); }
internal static class JobRecordMapper { public static WorkerJobRecord ToRecord(WorkerJob j) => new(j.Id, j.ProjectId, j.Type, j.Status, j.ProgressPercent, j.Message, j.CreatedAt, j.StartedAt, j.CompletedAt, j.CurrentStep, j.ErrorMessage, j.Logs); public static WorkerJob ToDomain(WorkerJobRecord r) => new(r.Id, r.ProjectId, r.Type, r.Status, r.ProgressPercent, r.Message, r.CreatedAt, r.StartedAt, r.CompletedAt, r.CurrentStep, r.ErrorMessage, r.Logs); }
internal static class InstructionRecordMapper { public static ProjectInstructionDocument ToDomain(ProjectInstructionDocument d) => d; public static ProjectInstructionDocument ToRecord(ProjectInstructionDocument d) => d; }
internal static class RecipeRecordMapper { public static ProjectRecipe ToDomain(ProjectRecipe r) => r; public static ProjectRecipe ToRecord(ProjectRecipe r) => r; }
internal static class DoctorReportRecordMapper { public static DoctorReport ToDomain(DoctorReport r) => r; public static DoctorReport ToRecord(DoctorReport r) => r; }

public sealed class LocaliteProjectRepository(LocaliteTableStore store) : IProjectRepository
{
    public async Task<Project> AddAsync(Project project, CancellationToken cancellationToken) { await store.UpsertAsync(Tables.Projects, project.Id, project.Id, project.RootPath, ProjectRecordMapper.ToRecord(project), cancellationToken); return project; }
    public async Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) => (await store.GetAsync<ProjectRecord>(Tables.Projects, id, cancellationToken)) is { } r ? ProjectRecordMapper.ToDomain(r) : null;
    public async Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken) => (await ListAsync(cancellationToken)).FirstOrDefault(p => Path.GetFullPath(p.RootPath).Equals(Path.GetFullPath(rootPath), StringComparison.OrdinalIgnoreCase));
    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) => (await store.ListAsync<ProjectRecord>(Tables.Projects, null, cancellationToken)).Select(ProjectRecordMapper.ToDomain).ToArray();
    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken) { await AddAsync(project, cancellationToken); return project; }
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
    public async Task<GeneratedArtifact> UpsertAsync(GeneratedArtifact artifact, CancellationToken cancellationToken) { await store.UpsertAsync(Tables.GeneratedArtifacts, artifact.Id, artifact.ProjectId, artifact.RelativePath, GeneratedArtifactRecordMapper.ToRecord(artifact), cancellationToken); return artifact; }
    public async Task<IReadOnlyList<GeneratedArtifact>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => (await store.ListAsync<GeneratedArtifactRecord>(Tables.GeneratedArtifacts, projectId, cancellationToken)).Select(GeneratedArtifactRecordMapper.ToDomain).OrderBy(a => a.RelativePath).ToArray();
    public async Task<GeneratedArtifact?> GetByPathAsync(Guid projectId, string relativePath, CancellationToken cancellationToken) => (await GetByProjectAsync(projectId, cancellationToken)).FirstOrDefault(a => a.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
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
        => new(await store.SchemaInfoAsync(cancellationToken), await store.CountAsync(Tables.Projects, cancellationToken), await store.CountAsync(Tables.ScanRuns, cancellationToken), await store.CountAsync(Tables.IndexedFiles, cancellationToken), await store.CountAsync(Tables.GeneratedArtifacts, cancellationToken), await store.CountAsync(Tables.WorkerJobs, cancellationToken), await store.CountAsync(Tables.InstructionDocuments, cancellationToken), await store.CountAsync(Tables.PromptRecipes, cancellationToken), true);
    public Task<bool> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(File.Exists(store.StoragePath));
}
