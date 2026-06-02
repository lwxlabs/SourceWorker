using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using SourceWorker.ApiClient;
using SourceWorker.Hosting;
using SourceWorker.UI.Services;

namespace SourceWorker.Desktop.Services;

public interface IDesktopLocalApiLifecycleService
{
    Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    DesktopRuntimeState GetRuntimeState();
}

public interface IDesktopStartupService
{
    Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default);
}

public interface IDesktopRuntimeStateService
{
    DesktopRuntimeState GetState();
}

public interface IDesktopApiConnectionService
{
    Task<ApiConnectionStatus> CheckAsync(CancellationToken cancellationToken = default);
}

public interface IDesktopStartupDiagnosticsService
{
    string BuildDiagnostics(DesktopStartupState state, DesktopRuntimeState runtime);
}

public interface IDesktopShellService { string EnvironmentName { get; } }
public interface IDesktopWindowService { void ShowMainWindow(); }
public interface IDesktopNotificationService { Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default); }
public interface IDesktopClipboardService { Task SetTextAsync(string text, CancellationToken cancellationToken = default); }
public interface IDesktopFileDialogService { Task<string?> PickFolderAsync(string currentPath = "", CancellationToken cancellationToken = default); }
public interface IDesktopExternalLinkService { Task OpenAsync(string url, CancellationToken cancellationToken = default); }
public interface ISingleInstanceService { bool TryAcquire(); void Release(); }

public sealed class DesktopLocalApiLifecycleService(
    ILocalApiLifecycleService localApi,
    SourceWorkerApiClientOptions options) : IDesktopLocalApiLifecycleService, IDesktopRuntimeStateService
{
    private DesktopStartupState lastStartup = DesktopStartupState.StartingShell();
    private LocalApiLifecycleResult? result;

    public async Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTimeOffset.UtcNow;
        try
        {
            lastStartup = new DesktopStartupState(DesktopStartupStatus.Starting, "starting Local API", ["starting desktop shell", "starting Local API"], null, null, TimeSpan.Zero);
            result = await localApi.StartAsync(new LocalApiLaunchOptions(ReuseExisting: true, LaunchMode: LaunchMode.Desktop), cancellationToken);
            options.BaseUrl = result.BaseUrl;
            options.BearerToken = result.Token;
            options.LaunchMode = "Desktop";
            var steps = result.StartupSteps.Concat(["loading dashboard UI", "ready"]).ToArray();
            lastStartup = new DesktopStartupState(DesktopStartupStatus.Ready, "ready", steps, null, null, DateTimeOffset.UtcNow - started);
            return lastStartup;
        }
        catch (Exception ex)
        {
            lastStartup = new DesktopStartupState(
                DesktopStartupStatus.Failed,
                "failed",
                lastStartup.Steps.Append("failed").ToArray(),
                ex.Message,
                "Retry startup, check whether port 5000 is already in use, or run `dotnet run --project src/services/SourceWorker.LocalApi` for details.",
                DateTimeOffset.UtcNow - started);
            return lastStartup;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => localApi.StopAsync(cancellationToken);

    public DesktopRuntimeState GetRuntimeState() => GetState();

    public DesktopRuntimeState GetState()
        => new(
            DesktopMode: true,
            LocalApiManagedByDesktop: result?.StartedByOwner == true,
            LocalApiProcessId: result?.Process.ProcessId,
            LocalApiBaseUrl: result?.BaseUrl ?? options.BaseUrl,
            TokenConfigured: !string.IsNullOrWhiteSpace(options.BearerToken),
            StoragePath: null,
            AppVersion: "2.4.0",
            UiVersion: "2.4.0",
            ApiVersion: null,
            StartupDuration: lastStartup.Duration,
            LastStartupError: lastStartup.ErrorMessage,
            OwnedLocalApiProcess: result?.StartedByOwner == true,
            StartupSteps: lastStartup.Steps);
}

public sealed class DesktopStartupService(IDesktopLocalApiLifecycleService lifecycle) : IDesktopStartupService
{
    public Task<DesktopStartupState> StartAsync(CancellationToken cancellationToken = default) => lifecycle.StartAsync(cancellationToken);
}

public sealed class DesktopApiConnectionService(ISourceWorkerApiClient api) : IDesktopApiConnectionService
{
    public Task<ApiConnectionStatus> CheckAsync(CancellationToken cancellationToken = default) => api.CheckConnectionAsync(cancellationToken);
}

public sealed class DesktopStartupDiagnosticsService : IDesktopStartupDiagnosticsService
{
    public string BuildDiagnostics(DesktopStartupState state, DesktopRuntimeState runtime)
        => $"""
        SourceWorker Desktop Diagnostics
        Status: {state.Status}
        Current step: {state.CurrentStep}
        Local API URL: {runtime.LocalApiBaseUrl}
        Token configured: {runtime.TokenConfigured}
        Owned Local API: {runtime.OwnedLocalApiProcess}
        Startup duration: {state.Duration}
        Error: {state.ErrorMessage ?? "none"}
        """;
}

public sealed class DesktopShellService : IDesktopShellService { public string EnvironmentName => "Desktop"; }
public sealed class DesktopWindowService : IDesktopWindowService { public void ShowMainWindow() { } }
public sealed class InAppDesktopNotificationService : IDesktopNotificationService { public Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
public sealed class DesktopClipboardService : IDesktopClipboardService { public Task SetTextAsync(string text, CancellationToken cancellationToken = default) => Clipboard.SetTextAsync(text); }
public sealed class DesktopExternalLinkService : IDesktopExternalLinkService { public Task OpenAsync(string url, CancellationToken cancellationToken = default) => Launcher.OpenAsync(url); }

public sealed class DesktopFolderPickerService : IDesktopFileDialogService, IProjectFolderPicker
{
    public Task<string?> PickFolderAsync(string currentPath = "", CancellationToken cancellationToken = default)
        => Task.FromResult(PickFolderNative(currentPath));

    Task<string?> IDesktopFileDialogService.PickFolderAsync(string currentPath, CancellationToken cancellationToken)
        => PickFolderAsync(currentPath, cancellationToken);

    Task<string?> IProjectFolderPicker.PickFolderAsync(string currentPath, CancellationToken cancellationToken)
        => PickFolderAsync(currentPath, cancellationToken);

    private static string? PickFolderNative(string initialPath = "")
    {
        IFileOpenDialog? dialog = null;
        IShellItem? initialFolder = null;

        try
        {
            dialog = (IFileOpenDialog)(object)new FileOpenDialogRCW();
            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST);
            dialog.SetTitle("Choose a SourceWorker project folder");

            if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
            {
                var hr = SHCreateItemFromParsingName(initialPath, IntPtr.Zero, typeof(IShellItem).GUID, out var folderPtr);
                if (hr == 0 && folderPtr != IntPtr.Zero)
                {
                    initialFolder = (IShellItem)Marshal.GetObjectForIUnknown(folderPtr);
                    Marshal.Release(folderPtr);
                    dialog.SetFolder(initialFolder);
                    dialog.SetDefaultFolder(initialFolder);
                }
            }

            if (dialog.Show(IntPtr.Zero) != 0) return null;
            dialog.GetResult(out var result);
            result.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var selectedPath);
            return selectedPath;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (initialFolder != null) Marshal.ReleaseComObject(initialFolder);
            if (dialog != null) Marshal.ReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string pszPath, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private sealed class FileOpenDialogRCW { }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
        void GetResults(IntPtr ppenum);
        void GetSelectedItems(IntPtr ppsai);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [Flags]
    private enum FOS : uint
    {
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_PATHMUSTEXIST = 0x00000800
    }

    private enum SIGDN : uint
    {
        SIGDN_FILESYSPATH = 0x80058000
    }
}

public sealed class SingleInstanceService : ISingleInstanceService, IDisposable
{
    private Mutex? mutex;

    public bool TryAcquire()
    {
        mutex = new Mutex(initiallyOwned: true, "SourceWorker.Desktop.SingleInstance", out var created);
        return created;
    }

    public void Release()
    {
        try { mutex?.ReleaseMutex(); } catch { }
        mutex?.Dispose();
        mutex = null;
    }

    public void Dispose() => Release();
}
