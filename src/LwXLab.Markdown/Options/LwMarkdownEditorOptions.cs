namespace LwXLab.Markdown.Options;

public sealed class LwMarkdownEditorOptions
{
    public string Theme { get; set; } = "vs";
    public string Language { get; set; } = "markdown";
    public bool AutomaticLayout { get; set; } = true;
    public bool LineNumbers { get; set; } = true;
    public bool LineWrapping { get; set; } = true;
    public bool Minimap { get; set; } = false;
    public int TabSize { get; set; } = 2;
    public bool WordBasedSuggestions { get; set; } = true;
    public bool FormatOnPaste { get; set; } = true;
    public bool FormatOnType { get; set; } = false;
    public bool RenderWhitespace { get; set; } = false;
    public bool ScrollBeyondLastLine { get; set; } = false;
    public bool StickyScroll { get; set; } = false;
    public string FontFamily { get; set; } = "Cascadia Code, Consolas, monospace";
    public int FontSize { get; set; } = 14;
}
