using SourceWorker.UI.Services;

namespace SourceWorker.Tests.Application;

public sealed class NavigationTests
{
    [Fact]
    public void Navigation_global_sidebar_excludes_project_specific_items()
    {
        var global = SourceWorkerNavigation.GlobalPrimary.Concat(SourceWorkerNavigation.GlobalSecondary).Select(i => i.Label).ToArray();

        Assert.Contains("Home", global);
        Assert.Contains("Projects", global);
        Assert.Contains("Settings", global);
        Assert.DoesNotContain("Handoff", global);
        Assert.DoesNotContain("Ask", global);
        Assert.DoesNotContain("Docs", global);
        Assert.DoesNotContain("Packs", global);
        Assert.DoesNotContain("Artifacts", global);
        Assert.DoesNotContain("Jobs", global);
    }

    [Fact]
    public void Navigation_project_workspace_contains_project_specific_tabs()
    {
        var projectTabs = SourceWorkerNavigation.ProjectPrimary.Concat(SourceWorkerNavigation.ProjectAdvanced).Select(i => i.Label).ToArray();

        Assert.Contains("Overview", projectTabs);
        Assert.Contains("Handoff", projectTabs);
        Assert.Contains("Ask", projectTabs);
        Assert.Contains("Docs", projectTabs);
        Assert.Contains("Packs", projectTabs);
        Assert.Contains("Scan Health", projectTabs);
        Assert.Contains("Code", projectTabs);
        Assert.Contains("Artifacts", projectTabs);
        Assert.Contains("Jobs", projectTabs);
        Assert.Contains("Context Browser", projectTabs);
    }

    [Fact]
    public void Navigation_advanced_global_contains_only_global_debug_tools()
    {
        var advanced = SourceWorkerNavigation.AdvancedGlobal.Select(i => i.Label).ToArray();

        Assert.Contains("Diagnostics", advanced);
        Assert.Contains("Logs", advanced);
        Assert.Contains("Dogfood", advanced);
        Assert.Contains("All Jobs", advanced);
        Assert.DoesNotContain("Handoff", advanced);
        Assert.DoesNotContain("Ask", advanced);
        Assert.DoesNotContain("Packs", advanced);
    }
}

