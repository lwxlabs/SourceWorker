using Microsoft.Extensions.Logging;
using SourceWorker.ApiClient;
using SourceWorker.Desktop.Services;
using SourceWorker.Hosting;
using SourceWorker.UI.Services;
#if SOURCEWORKER_PRIVATE_EXTENSIONS
using LwXLab.AI.Engine.Extensions;
using LwXLab.FileEditor.Extensions;
using LwXLab.FileEditor.Services;
#endif
using LwXLab.Markdown.Extensions;

namespace SourceWorker.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Services.AddLogging(logging => logging.AddDebug());
        builder.Services.AddSourceWorkerHosting();
        builder.Services.AddSourceWorkerApiClient(options =>
        {
            options.BaseUrl = "http://localhost:5000";
            options.LaunchMode = "Desktop";
        });

        builder.Services.AddScoped<ToastService>();
        builder.Services.AddScoped<DashboardActionService>();
        builder.Services.AddScoped<ISelectedProjectState, SelectedProjectState>();
        builder.Services.AddLwXLabMarkdown();
#if SOURCEWORKER_PRIVATE_EXTENSIONS
        builder.Services.AddLwXLabAiEngine();
        builder.Services.AddLwOpenAiCompatibleProvider(options =>
        {
            options.Id = "lm-studio-local";
            options.DisplayName = "LM Studio";
            options.BaseUrl = Environment.GetEnvironmentVariable("SOURCEWORKER_AI_BASE_URL") ?? "http://localhost:1234/v1";
            options.Model = Environment.GetEnvironmentVariable("SOURCEWORKER_AI_MODEL") ?? string.Empty;
            options.IsLocal = true;
        });
        builder.Services.AddLwXLabFileEditor(options =>
        {
            options.EnableMenuBar = true;
            options.EnableCommandPalette = true;
            options.EnableFind = true;
            options.EnableReplace = true;
            options.EnableWorkspaceSearch = true;
            options.EnableProblemsPanel = true;
            options.EnableEditorSplitting = true;
            options.EnableLocalDraftBackup = true;
            options.EnableUnsavedChangesGuard = true;
            options.EnableAiActions = true;
            options.EnableAiPanel = true;
            options.EnableAiContextMenus = true;
            options.EnableAiApply = true;
            options.AutoSaveMode = LwXLab.FileEditor.Models.LwAutoSaveMode.Off;
            options.SidebarWidth = "300px";
        });
        builder.Services.AddScoped<ProjectFileEditorContext>();
        builder.Services.AddScoped<ILwFileEditorStorage, SourceWorkerFileEditorStorage>();
#endif
        builder.Services.AddSingleton<IDesktopLocalApiLifecycleService, DesktopLocalApiLifecycleService>();
        builder.Services.AddSingleton<IDesktopRuntimeStateService>(provider => (DesktopLocalApiLifecycleService)provider.GetRequiredService<IDesktopLocalApiLifecycleService>());
        builder.Services.AddSingleton<IDesktopStartupService, DesktopStartupService>();
        builder.Services.AddSingleton<IDesktopApiConnectionService, DesktopApiConnectionService>();
        builder.Services.AddSingleton<IDesktopStartupDiagnosticsService, DesktopStartupDiagnosticsService>();
        builder.Services.AddSingleton<IDesktopShellService, DesktopShellService>();
        builder.Services.AddSingleton<IDesktopWindowService, DesktopWindowService>();
        builder.Services.AddSingleton<IDesktopNotificationService, InAppDesktopNotificationService>();
        builder.Services.AddSingleton<IDesktopClipboardService, DesktopClipboardService>();
        builder.Services.AddSingleton<DesktopFolderPickerService>();
        builder.Services.AddSingleton<IDesktopFileDialogService>(provider => provider.GetRequiredService<DesktopFolderPickerService>());
        builder.Services.AddSingleton<IProjectFolderPicker>(provider => provider.GetRequiredService<DesktopFolderPickerService>());
        builder.Services.AddSingleton<IDesktopExternalLinkService, DesktopExternalLinkService>();
        builder.Services.AddSingleton<ISingleInstanceService, SingleInstanceService>();

        var app = builder.Build();
        var singleInstance = app.Services.GetRequiredService<ISingleInstanceService>();
        if (!singleInstance.TryAcquire())
        {
            throw new InvalidOperationException("SourceWorker Desktop is already running.");
        }

        return app;
    }
}
