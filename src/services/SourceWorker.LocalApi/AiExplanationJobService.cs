using System.Collections.Concurrent;
using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

public interface IAiExplanationJobService
{
    AiExplanationJobStatus Start(Guid projectId, AiExplanationRequest request);
    AiExplanationJobStatus? Get(Guid projectId, Guid jobId);
    AiExplanationJobStatus? Cancel(Guid projectId, Guid jobId);
}

public sealed class AiExplanationJobService(IServiceScopeFactory scopeFactory) : IAiExplanationJobService
{
    private readonly ConcurrentDictionary<Guid, AiExplanationJobState> jobs = [];

    public AiExplanationJobStatus Start(Guid projectId, AiExplanationRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new AiExplanationJobState(Guid.NewGuid(), projectId, now)
        {
            Phase = "queued",
            Message = "Queued explanation generation.",
            UpdatedUtc = now
        };
        jobs[state.JobId] = state;

        _ = Task.Run(() => RunAsync(state, request with { ProjectId = projectId }), CancellationToken.None);
        return state.ToStatus();
    }

    public AiExplanationJobStatus? Get(Guid projectId, Guid jobId)
        => jobs.TryGetValue(jobId, out var state) && state.ProjectId == projectId ? state.ToStatus() : null;

    public AiExplanationJobStatus? Cancel(Guid projectId, Guid jobId)
    {
        if (!jobs.TryGetValue(jobId, out var state) || state.ProjectId != projectId) return null;
        state.Cancellation.Cancel();
        state.Update("cancelling", "Cancelling explanation generation.", null, null, isCompleted: false, isFailed: false);
        return state.ToStatus();
    }

    private async Task RunAsync(AiExplanationJobState state, AiExplanationRequest request)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAiExplanationService>();

            await foreach (var update in service.RunStreamingAsync(request, state.Cancellation.Token))
            {
                state.Update(update.Phase, update.Message, update.DeltaMarkdown, update.Result, update.IsCompleted, update.IsFailed, update.Progress);
            }
        }
        catch (OperationCanceledException)
        {
            state.Update("cancelled", "Explanation generation was cancelled.", null, state.Result, isCompleted: true, isFailed: true);
        }
        catch (Exception ex)
        {
            state.Update("failed", ex.Message, null, state.Result, isCompleted: true, isFailed: true);
        }
    }
}

public sealed record AiExplanationJobStatus(
    Guid JobId,
    Guid ProjectId,
    string Phase,
    string Message,
    int OutputCharacters,
    DateTimeOffset StartedUtc,
    DateTimeOffset UpdatedUtc,
    TimeSpan Elapsed,
    bool IsCompleted,
    bool IsFailed,
    bool IsCancelled,
    string? MarkdownPreview,
    AiExplanationResult? Result,
    double? Progress = null);

internal sealed class AiExplanationJobState(Guid jobId, Guid projectId, DateTimeOffset startedUtc)
{
    private readonly object gate = new();
    private readonly StringBuilder markdown = new();

    public Guid JobId { get; } = jobId;
    public Guid ProjectId { get; } = projectId;
    public DateTimeOffset StartedUtc { get; } = startedUtc;
    public CancellationTokenSource Cancellation { get; } = new();
    public string Phase { get; set; } = "queued";
    public string Message { get; set; } = "Queued explanation generation.";
    public int OutputCharacters { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = startedUtc;
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }
    public double? Progress { get; set; }
    public AiExplanationResult? Result { get; set; }

    public void Update(string phase, string message, string? deltaMarkdown, AiExplanationResult? result, bool isCompleted, bool isFailed, double? progress = null)
    {
        lock (gate)
        {
            if (!string.IsNullOrEmpty(deltaMarkdown))
            {
                markdown.Append(deltaMarkdown);
            }

            Phase = phase;
            Message = message;
            OutputCharacters = Math.Max(OutputCharacters, markdown.Length);
            UpdatedUtc = DateTimeOffset.UtcNow;
            IsCompleted = isCompleted;
            IsFailed = isFailed;
            Progress = progress ?? (phase == "prompt-processing" ? Progress : null);
            Result = result ?? Result;

            if (Result?.Markdown is { Length: > 0 } resultMarkdown)
            {
                markdown.Clear();
                markdown.Append(resultMarkdown);
                OutputCharacters = markdown.Length;
            }
        }
    }

    public AiExplanationJobStatus ToStatus()
    {
        lock (gate)
        {
            var markdownPreview = markdown.Length == 0 ? null : markdown.ToString();
            var result = Result;
            if (result is not null && string.IsNullOrWhiteSpace(result.Markdown) && !string.IsNullOrWhiteSpace(markdownPreview))
            {
                result = result with { Markdown = markdownPreview };
            }

            return new AiExplanationJobStatus(
                JobId,
                ProjectId,
                Phase,
                Message,
                OutputCharacters,
                StartedUtc,
                UpdatedUtc,
                UpdatedUtc - StartedUtc,
                IsCompleted,
                IsFailed,
                Cancellation.IsCancellationRequested,
                markdownPreview,
                result,
                Progress);
        }
    }
}
