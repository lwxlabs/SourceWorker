namespace SourceWorker.ApiClient;

public sealed class SourceWorkerApiClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
    public string? BearerToken { get; set; }
}
