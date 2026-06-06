using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using LwXLab.Markdown.Options;

namespace LwXLab.Markdown.Services;

public sealed class LwMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;

    public LwMarkdownRenderer(LwMarkdownOptions options)
    {
        var builder = new MarkdownPipelineBuilder();

        if (options.EnableAdvancedExtensions)
            builder.UseAdvancedExtensions();
        else
        {
            if (options.EnablePipeTables) builder.UsePipeTables();
            if (options.EnableGridTables) builder.UseGridTables();
            if (options.EnableTaskLists) builder.UseTaskLists();
            if (options.EnableAutoLinks) builder.UseAutoLinks();
            if (options.EnableEmoji) builder.UseEmojiAndSmiley();
        }

        builder.UseAutoIdentifiers(AutoIdentifierOptions.GitHub);

        if (options.EnableBootstrap)
            builder.UseBootstrap();

        _pipeline = builder.Build();
    }

    public string Render(string? markdown)
    {
        markdown ??= string.Empty;
        return Markdig.Markdown.ToHtml(markdown, _pipeline);
    }
}
