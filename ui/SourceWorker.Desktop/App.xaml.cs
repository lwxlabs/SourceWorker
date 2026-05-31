using SourceWorker.Desktop.Services;

namespace SourceWorker.Desktop;

public partial class App : Application
{
    private readonly ISingleInstanceService singleInstance;
    private readonly IDesktopLocalApiLifecycleService lifecycle;

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
        window.Destroying += async (_, _) =>
        {
            singleInstance.Release();
            await lifecycle.StopAsync();
        };
        return window;
    }
}
