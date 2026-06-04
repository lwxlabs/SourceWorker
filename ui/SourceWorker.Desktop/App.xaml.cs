using SourceWorker.Desktop.Services;

namespace SourceWorker.Desktop;

public partial class App : Application
{
    private readonly ISingleInstanceService singleInstance;
    private readonly IDesktopLocalApiLifecycleService lifecycle;
    private int shutdownStarted;

    public App(ISingleInstanceService singleInstance, IDesktopLocalApiLifecycleService lifecycle)
    {
        this.singleInstance = singleInstance;
        this.lifecycle = lifecycle;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "SourceWorker"
        };
        window.Destroying += (_, _) => ShutdownOwnedServices();
        return window;
    }

    protected override void OnSleep()
    {
        ShutdownOwnedServices();
        base.OnSleep();
    }

    private void ShutdownOwnedServices()
    {
        if (Interlocked.Exchange(ref shutdownStarted, 1) == 1)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            lifecycle.StopAsync(timeout.Token).GetAwaiter().GetResult();
        }
        catch
        {
            // Shutdown must not prevent the desktop app from closing.
        }
        finally
        {
            singleInstance.Release();
        }
    }
}
