namespace LwXLab.Markdown.Options;

public sealed class LwMarkdownOptions
{
    public bool EnableMermaid { get; set; } = true;
    public bool EnableHighlight { get; set; } = true;
    public bool EnableMonaco { get; set; } = true;
    public bool EnableTables { get; set; } = true;
    public bool EnableTaskLists { get; set; } = true;
    public bool EnableEmoji { get; set; } = true;
    public bool EnableAutoLinks { get; set; } = true;
    public bool EnablePipeTables { get; set; } = true;
    public bool EnableGridTables { get; set; } = true;
    public bool EnableBootstrap { get; set; } = false;
    public bool EnableAdvancedExtensions { get; set; } = true;
    public bool EnableHtml { get; set; } = true;

    public string MonacoLoaderUrl { get; set; } = "https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs/loader.js";
    public string MonacoBaseUrl { get; set; } = "https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/vs";
    public string MermaidUrl { get; set; } = "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs";
    public string HighlightUrl { get; set; } = "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.11.1/highlight.min.js";
    public string HighlightThemeUrl { get; set; } = "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.11.1/styles/github.min.css";

    public void UseLocalAssets(string baseDir = "_content/LwXLab.Markdown/lib")
    {
        MonacoLoaderUrl = $"{baseDir}/monaco-editor/min/vs/loader.js";
        MonacoBaseUrl = $"{baseDir}/monaco-editor/min/vs";
        MermaidUrl = $"{baseDir}/mermaid/mermaid.esm.min.mjs";
        HighlightUrl = $"{baseDir}/highlight.js/highlight.min.js";
        HighlightThemeUrl = $"{baseDir}/highlight.js/styles/github.min.css";
    }
}
