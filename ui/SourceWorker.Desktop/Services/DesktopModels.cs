namespace SourceWorker.Desktop.Services;

public enum DesktopStartupStatus
{
    Starting,
    Ready,
    Failed
}

public sealed record DesktopStartupState(
    DesktopStartupStatus Status,
    string CurrentStep,
    IReadOnlyList<string> Steps,
    string? ErrorMessage,
    string? SuggestedFix,
    TimeSpan Duration)
{
    public static DesktopStartupState StartingShell()
        => new(DesktopStartupStatus.Starting, "starting desktop shell", ["starting desktop shell"], null, null, TimeSpan.Zero);
}

public sealed record DesktopSettings(
    bool StartLocalApiWithDesktop = true,
    bool StopLocalApiOnExit = true,
    bool OpenLastProjectOnStartup = true,
    bool MinimizeToBackground = false,
    bool StartWithWindows = false);
