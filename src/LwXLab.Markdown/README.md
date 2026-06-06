# LwXLab.Markdown

Blazor Markdown editor/preview component using:

- Monaco Editor for editing
- Markdig for Markdown rendering
- Mermaid.js for flowcharts/diagrams
- highlight.js for code highlighting

Targets: `net8.0`, `net9.0`, `net10.0`.

## Install locally

Add the project to your solution:

```bash
dotnet sln add src/LwXLab.Markdown/LwXLab.Markdown.csproj
```

Reference it from your Blazor app:

```bash
dotnet add YourApp.csproj reference ../LwXLab.Markdown/LwXLab.Markdown.csproj
```

## Program.cs

```csharp
using LwXLab.Markdown.Extensions;

builder.Services.AddLwXLabMarkdown(options =>
{
    options.EnableMermaid = true;
    options.EnableHighlight = true;
});
```

## App.razor / main layout

Add the CSS:

```html
<link href="_content/LwXLab.Markdown/lwxlab-markdown.css" rel="stylesheet" />
```

The JS module is imported automatically by the component.

## Usage

```razor
<LwXLab.MarkDown @bind-Value="markdown"
                 ValueHTMLChanged="OnHtmlChanged"
                 EnableEdit="false"
                 Height="600px"
                 Width="100%"
                 EnableMermaid="true"
                 EnableHighlight="true" />

@code {
    private string? markdown = """
    # Hello

    ```mermaid
    flowchart TD
        A[Start] --> B[End]
    ```
    """;

    private Task OnHtmlChanged(string html)
    {
        // store rendered html, index docs, send to API, etc.
        return Task.CompletedTask;
    }
}
```

## Rich editor mode

```razor
<LwXLab.MarkDown @bind-Value="markdown"
                 ValueHTMLChanged="OnHtmlChanged"
                 EnableEdit="true"
                 EnableMonaco="true"
                 SplitView="true"
                 Preview="false"
                 AutoSaveEnabled="true"
                 AutoSaveId="repo-doc-123"
                 Height="70vh" />
```

## Options

Important component parameters:

- `Value`, `ValueChanged`, `ValueHTML`, `ValueHTMLChanged`
- `EnableEdit`, `ReadOnly`, `Preview`, `SplitView`
- `EnableMonaco`, `EnableMermaid`, `EnableHighlight`
- `EnableTables`, `EnableTaskLists`, `EnableEmoji`, `EnableAutoLinks`, `EnableAdvancedExtensions`
- `Height`, `Width`, `MinHeight`, `MaxHeight`, `CssClass`, `PreviewCssClass`
- `ShowToolbar`, `Toolbar`, `ToolbarButtons`, `HideIcons`
- `ShowStatusBar`, `ShowCharacters`, `ShowWords`, `ShowLines`
- `AutoSaveEnabled`, `AutoSaveId`, `AutoSaveDelay`
- `EditorOptions` for Monaco settings
- `Options` for script/CDN overrides

## Custom toolbar

```razor
<LwXLab.MarkDown @ref="editor" @bind-Value="markdown">
    <Toolbar>
        <button type="button" @onclick="InsertApiBlock">API block</button>
    </Toolbar>
</LwXLab.MarkDown>

@code {
    private LwXLab.MarkDown? editor;
    private string? markdown;

    private async Task InsertApiBlock()
    {
        if (editor is not null)
            await editor.InsertTextAsync("```http\nGET /api/v1/items\n```\n");
    }
}
```

## Notes

- Monaco, Mermaid and highlight.js are loaded from CDN by default. Override URLs with `LwMarkdownOptions` when you want self-hosted static assets.
- For a production public editor, add HTML sanitization before storing/displaying untrusted content. This skeleton keeps HTML enabled because developer documentation often needs raw HTML.
