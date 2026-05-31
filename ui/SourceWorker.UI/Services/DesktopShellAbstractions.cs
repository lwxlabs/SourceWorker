namespace SourceWorker.UI.Services;

public interface IProjectFolderPicker
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken = default);
}

public sealed class NoopProjectFolderPicker : IProjectFolderPicker
{
    public Task<string?> PickFolderAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}
