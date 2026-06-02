namespace SourceWorker.UI.Services;

public interface IProjectFolderPicker
{
    Task<string?> PickFolderAsync(string currentPath = "", CancellationToken cancellationToken = default);
}

public sealed class NoopProjectFolderPicker : IProjectFolderPicker
{
    public Task<string?> PickFolderAsync(string currentPath = "", CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
