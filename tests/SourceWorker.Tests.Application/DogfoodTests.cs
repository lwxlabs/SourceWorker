using SourceWorker.AI;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Tests.Application;

public sealed class DogfoodTests
{
    [Fact]
    public async Task Dogfood_session_creation_and_step_completion_work()
    {
        var sessions = new SessionRepo();
        var service = new DogfoodSessionService(sessions, new IssueRepo());

        var session = await service.StartAsync([new DogfoodSessionProject(null, "SourceWorker", null, false, false)], CancellationToken.None);
        var updated = await service.CompleteStepAsync(session.Id, "choose-projects", CancellationToken.None);

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Contains(updated.Steps, s => s.Id == "choose-projects" && s.IsCompleted);
    }

    [Fact]
    public async Task Dogfood_friction_issue_creation_and_status_update_work()
    {
        var repo = new IssueRepo();
        var service = new FrictionIssueService(repo);

        var issue = await service.ReportAsync(new DogfoodSessionIssue(Guid.Empty, null, null, "/dogfood", DogfoodFrictionCategory.UIConfusion, DogfoodSeverity.Medium, "Confusing button", "I was unsure what to click.", null, null, "", default), CancellationToken.None);
        var fixedIssue = await service.UpdateStatusAsync(issue.Id, "Fixed", CancellationToken.None);

        Assert.Equal("Open", issue.Status);
        Assert.Equal("Fixed", fixedIssue?.Status);
    }

    [Fact]
    public async Task Dogfood_next_action_generation_suggests_register_project_when_empty()
    {
        var service = new NextActionService(new ProjectRepo([]), new ScanRepo(), new ConfigRepo(), new IssueRepo());

        var actions = await service.GetGlobalAsync(CancellationToken.None);

        Assert.Contains(actions, a => a.Kind == NextActionKind.RegisterProject);
    }

    [Fact]
    public async Task Dogfood_project_path_validation_detects_missing_path()
    {
        var service = new ProjectPathValidationService();

        var result = await service.ValidateAsync(Path.Combine(Path.GetTempPath(), "sourceworker-missing-" + Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.CanRegister);
        Assert.Contains(result.Errors, e => e.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Dogfood_project_path_validation_allows_folder_without_solution_with_warning()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-path-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var service = new ProjectPathValidationService();

        var result = await service.ValidateAsync(root, CancellationToken.None);

        Assert.True(result.CanRegister);
        Assert.Contains(result.Warnings, w => w.Contains("No solution", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class SessionRepo : IDogfoodSessionRepository
    {
        private readonly Dictionary<Guid, DogfoodSession> sessions = [];
        public Task<DogfoodSession> SaveAsync(DogfoodSession session, CancellationToken cancellationToken) { sessions[session.Id] = session; return Task.FromResult(session); }
        public Task<DogfoodSession?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(sessions.TryGetValue(id, out var value) ? value : null);
        public Task<IReadOnlyList<DogfoodSession>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DogfoodSession>>(sessions.Values.ToArray());
    }

    private sealed class IssueRepo : IDogfoodIssueRepository
    {
        private readonly Dictionary<Guid, DogfoodSessionIssue> issues = [];
        public Task<DogfoodSessionIssue> SaveAsync(DogfoodSessionIssue issue, CancellationToken cancellationToken) { issues[issue.Id] = issue; return Task.FromResult(issue); }
        public Task<IReadOnlyList<DogfoodSessionIssue>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DogfoodSessionIssue>>(issues.Values.ToArray());
        public Task<DogfoodSessionIssue?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(issues.TryGetValue(id, out var value) ? value : null);
    }

    private sealed class ProjectRepo(IReadOnlyList<Project> projects) : IProjectRepository
    {
        public Task<Project> AddAsync(Project project, CancellationToken cancellationToken) => Task.FromResult(project);
        public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(projects.FirstOrDefault(p => p.Id == id));
        public Task<Project?> GetByPathAsync(string rootPath, CancellationToken cancellationToken) => Task.FromResult(projects.FirstOrDefault(p => p.RootPath == rootPath));
        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(projects);
        public Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken) => Task.FromResult(project);
    }

    private sealed class ScanRepo : IScanRunRepository
    {
        public Task<ScanRun> AddAsync(ScanRun scanRun, CancellationToken cancellationToken) => Task.FromResult(scanRun);
        public Task<ScanRun?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<ScanRun?>(null);
        public Task<IReadOnlyList<ScanRun>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ScanRun>>([]);
        public Task<ScanRun?> GetLatestAsync(Guid projectId, CancellationToken cancellationToken) => Task.FromResult<ScanRun?>(null);
        public Task<ScanRun> UpdateAsync(ScanRun scanRun, CancellationToken cancellationToken) => Task.FromResult(scanRun);
    }

    private sealed class ConfigRepo : IAiProviderConfigurationRepository
    {
        public Task<AiProviderConfiguration> SaveAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task<AiProviderConfiguration?> GetAsync(string providerId, CancellationToken cancellationToken) => Task.FromResult<AiProviderConfiguration?>(null);
        public Task<IReadOnlyList<AiProviderConfiguration>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiProviderConfiguration>>([]);
    }
}
