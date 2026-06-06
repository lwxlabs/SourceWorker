namespace SourceWorker.UI.Services;

public sealed class ToastService
{
    public event Action? OnChange;
    private readonly List<ToastMessage> _messages = new();
    private readonly object gate = new();

    public IReadOnlyList<ToastMessage> Messages
    {
        get
        {
            lock (gate)
            {
                return _messages.ToArray();
            }
        }
    }

    public void ShowSuccess(string message, int durationMs = 5000) => Show(message, ToastLevel.Success, durationMs);
    public void ShowError(string message, int durationMs = 5000) => Show(message, ToastLevel.Error, durationMs);
    public void ShowInfo(string message, int durationMs = 5000) => Show(message, ToastLevel.Info, durationMs);
    public void ShowWarning(string message, int durationMs = 5000) => Show(message, ToastLevel.Warning, durationMs);

    private void Show(string message, ToastLevel level, int durationMs)
    {
        var toast = new ToastMessage(Guid.NewGuid(), message, level, DateTime.UtcNow);
        lock (gate)
        {
            _messages.Add(toast);
        }

        NotifyStateChanged();

        Task.Delay(durationMs).ContinueWith(_ => Remove(toast.Id));
    }

    public void Remove(Guid id)
    {
        var removed = false;
        lock (gate)
        {
            var toast = _messages.FirstOrDefault(m => m.Id == id);
            if (toast != null)
            {
                _messages.Remove(toast);
                removed = true;
            }
        }

        if (removed)
        {
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}

public sealed record ToastMessage(Guid Id, string Message, ToastLevel Level, DateTime Timestamp);

public enum ToastLevel
{
    Info,
    Success,
    Warning,
    Error
}
