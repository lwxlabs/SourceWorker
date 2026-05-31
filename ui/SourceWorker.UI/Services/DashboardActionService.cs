using SourceWorker.ApiClient;

namespace SourceWorker.UI.Services;

public sealed class DashboardActionService
{
    public DashboardActionAvailability GetAvailability(DashboardActionKind kind)
    {
        return kind switch
        {
            DashboardActionKind.ScanProject => new(kind, DashboardActionSafetyLevel.SafeWrite, true),
            DashboardActionKind.RunRecipe => new(kind, DashboardActionSafetyLevel.GeneratedFileWrite, true),
            DashboardActionKind.ExportDocs => new(kind, DashboardActionSafetyLevel.GeneratedFileWrite, true),
            DashboardActionKind.ExportPack => new(kind, DashboardActionSafetyLevel.GeneratedFileWrite, true),
            DashboardActionKind.CleanupPreview => new(kind, DashboardActionSafetyLevel.ReadOnly, true),
            DashboardActionKind.CleanupGenerated => new(kind, DashboardActionSafetyLevel.DestructiveGeneratedCleanup, true),
            DashboardActionKind.CleanupForce => new(kind, DashboardActionSafetyLevel.DangerousDisabled, false, "Force cleanup is currently disabled in the dashboard for safety."),
            DashboardActionKind.RegisterProject => new(kind, DashboardActionSafetyLevel.SafeWrite, true),
            _ => new(kind, DashboardActionSafetyLevel.ReadOnly, false, "Unknown action.")
        };
    }
}
