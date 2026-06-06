namespace LwXLab.Markdown.Models;

public sealed class LwMarkdownButton
{
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? IconCssClass { get; set; }
    public string? Before { get; set; }
    public string? After { get; set; }
    public string? Placeholder { get; set; }
    public bool Visible { get; set; } = true;
}
