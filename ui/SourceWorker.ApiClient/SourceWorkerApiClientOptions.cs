namespace SourceWorker.ApiClient;

public sealed class SourceWorkerApiClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public string? BearerToken { get; set; }
    public ITokenProvider? TokenProvider { get; set; }
    public string LaunchMode { get; set; } = "Standalone";
}
