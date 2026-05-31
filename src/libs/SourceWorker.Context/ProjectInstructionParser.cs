using System.Text;
using System.Text.RegularExpressions;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Context;

public sealed partial class ProjectInstructionParser : IProjectInstructionParser
{
    public ProjectInstructionDocument Parse(Guid projectId, string projectRootPath, string instructionFilePath, string markdown)
    {
        var sections = ParseSections(markdown);
        var sourcePaths = ExtractSourcePaths(sections);
        var rules = ExtractItems(sections, IsMindsetSection).Select(i => new ProjectInstructionRule(i.Text, i.SourceSection)).ToArray();
        var todos = ExtractItems(sections, IsTodoSection).Select(i => new ProjectInstructionTodo(i.Text, i.IsChecked == true, i.SourceSection)).ToArray();
        var skip = ExtractItems(sections, IsSkipSection).Select(i => new ProjectInstructionSkipItem(i.Text, i.SourceSection)).ToArray();
        var verification = ExtractItems(sections, IsVerificationSection).Select(i => new ProjectInstructionVerificationItem(i.Text, i.IsChecked == true, i.SourceSection)).ToArray();
        var title = sections.FirstOrDefault(s => s.Level == 1)?.Title ?? Path.GetFileNameWithoutExtension(instructionFilePath);

        return new ProjectInstructionDocument(
            Guid.NewGuid(),
            projectId,
            instructionFilePath,
            title,
            DateTimeOffset.UtcNow,
            sections,
            sourcePaths,
            rules,
            todos,
            skip,
            verification);
    }

    private static IReadOnlyList<ProjectInstructionSection> ParseSections(string markdown)
    {
        var sections = new List<MutableSection>();
        MutableSection? current = null;
        var inCodeBlock = false;
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inCodeBlock = !inCodeBlock;
                current ??= NewSection("Preamble", 0);
                current.Raw.AppendLine(line);
                continue;
            }

            if (!inCodeBlock)
            {
                var heading = HeadingRegex().Match(line);
                if (heading.Success)
                {
                    current = NewSection(heading.Groups["title"].Value.Trim(), heading.Groups["hash"].Value.Length);
                    sections.Add(current);
                    continue;
                }
            }

            current ??= NewSection("Preamble", 0);
            if (!sections.Contains(current)) sections.Add(current);
            current.Raw.AppendLine(line);

            if (!inCodeBlock)
            {
                var item = ListItemRegex().Match(line);
                if (item.Success)
                {
                    var checkbox = item.Groups["check"].Success;
                    current.Items.Add(new ProjectInstructionItem(
                        item.Groups["text"].Value.Trim(),
                        checkbox,
                        checkbox ? item.Groups["check"].Value.Equals("x", StringComparison.OrdinalIgnoreCase) : null,
                        item.Groups["indent"].Value.Length,
                        i + 1));
                }
            }
        }

        return sections.Select(s => new ProjectInstructionSection(s.Title, Normalize(s.Title), s.Level, s.Items, s.Raw.ToString().Trim())).ToArray();
    }

    private static IReadOnlyList<ProjectInstructionSourcePath> ExtractSourcePaths(IReadOnlyList<ProjectInstructionSection> sections)
        => ExtractItems(sections, IsFilesSection)
            .Select(i => ExtractPathText(i.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(text => new ProjectInstructionSourcePath(text, NormalizePath(text), false, SourcePathType.Unknown, false, null))
            .ToArray();

    private static IEnumerable<(string Text, bool? IsChecked, string SourceSection)> ExtractItems(IReadOnlyList<ProjectInstructionSection> sections, Func<string, bool> sectionPredicate)
        => sections.Where(s => sectionPredicate(s.NormalizedTitle))
            .SelectMany(s => s.Items.Select(i => (i.Text, i.IsChecked, s.Title)));

    private static string ExtractPathText(string text)
    {
        var backtick = BacktickRegex().Match(text);
        if (backtick.Success) return backtick.Groups["path"].Value.Trim();
        var colonIndex = text.IndexOf(':');
        return colonIndex >= 0 ? text[..colonIndex].Trim() : text.Trim();
    }

    private static bool IsFilesSection(string title) => title.Contains("file", StringComparison.Ordinal) || title.Contains("class", StringComparison.Ordinal) || title.Contains("source", StringComparison.Ordinal);
    private static bool IsMindsetSection(string title) => title.Contains("mindset", StringComparison.Ordinal) || title.Contains("rule", StringComparison.Ordinal);
    private static bool IsTodoSection(string title) => title.Contains("todo", StringComparison.Ordinal) || title.Contains("sql server", StringComparison.Ordinal);
    private static bool IsSkipSection(string title) => title.Contains("skip", StringComparison.Ordinal) || title.Contains("later", StringComparison.Ordinal);
    private static bool IsVerificationSection(string title) => title.Contains("finally", StringComparison.Ordinal) || title.Contains("verify", StringComparison.Ordinal) || title.Contains("verification", StringComparison.Ordinal);
    private static string Normalize(string value) => value.Trim().ToLowerInvariant();
    private static string NormalizePath(string value) => value.Trim().Trim('`', '"', '\'').Replace('\\', '/');

    private static MutableSection NewSection(string title, int level) => new(title, level);

    private sealed class MutableSection(string title, int level)
    {
        public string Title { get; } = title;
        public int Level { get; } = level;
        public List<ProjectInstructionItem> Items { get; } = [];
        public StringBuilder Raw { get; } = new();
    }

    [GeneratedRegex(@"^(?<hash>#{1,6})\s+(?<title>.+)$", RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(?<indent>\s*)(?:[-*+]|\d+[.)])\s+(?:\[(?<check>[ xX])\]\s+)?(?<text>.+)$", RegexOptions.Compiled)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"`(?<path>[^`]+)`", RegexOptions.Compiled)]
    private static partial Regex BacktickRegex();
}
