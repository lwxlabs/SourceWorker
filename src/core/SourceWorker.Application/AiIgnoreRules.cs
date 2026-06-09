using System.Text.RegularExpressions;

namespace SourceWorker.Application;

public static partial class AiIgnoreRules
{
    public const string FileIgnoreRelativePath = ".ai-context/ai-ignore.md";
    public const string SnippetIgnoreRelativePath = ".ai-context/ai-snippet-ignore.md";

    public static string DefaultFileIgnoreMarkdown =>
        """
        # SourceWorker AI Ignore

        enabled: true

        # One pattern per line. The syntax follows the useful subset of .gitignore:
        # - folders: temp/
        # - extensions: *.log
        # - files: path/to/file.generated.cs
        # - globs: generated-*.json or generated-*
        # - exceptions: !src/KeepThis.cs
        #
        # Later matching rules win, so put exceptions after the ignore rule they undo.

        # Examples
        # logs/
        # *.tmp
        # *.cs
        # !src/Program.cs
        # generated-*.json
        # generated-*
        # !generated-keep.json
        """;

    public static string DefaultSnippetIgnoreMarkdown =>
        """
        # SourceWorker AI Snippet Ignore

        enabled: true

        # These rules do not remove files from scans. They only remove noisy lines from
        # snapshot compare previews, scan diff markdown, and provider prompt snippets.
        #
        # Rule syntax:
        # - contains: text
        # - starts-with: text
        # - regex: pattern

        contains: <!-- SourceWorker:Generated
        contains: Completed at:
        contains: Started:
        contains: Completed:
        regex: ^\s*(Started|Completed)( at)?:\s*
        """;

    public static IReadOnlyList<string> LoadFileIgnorePatterns(string root)
        => ParseFileIgnore(File.Exists(Path.Combine(root, FileIgnoreRelativePath))
            ? File.ReadAllText(Path.Combine(root, FileIgnoreRelativePath))
            : "");

    public static IReadOnlyList<string> ParseFileIgnore(string markdown)
    {
        var lines = CandidateLines(markdown).ToArray();
        if (!IsEnabled(lines)) return [];
        return lines
            .Where(line => !IsEnabledDirective(line))
            .Where(line => !line.Contains(':', StringComparison.Ordinal) || line.StartsWith("!", StringComparison.Ordinal))
            .ToArray();
    }

    public static bool IsFileIgnored(string relativePath, bool isDirectory, IReadOnlyList<string> patterns)
    {
        var ignored = false;
        foreach (var pattern in patterns)
        {
            var isNegated = pattern.StartsWith('!');
            var normalized = (isNegated ? pattern[1..] : pattern).Trim('/').Replace('\\', '/');
            if (normalized.Length == 0) continue;
            if (MatchesFileIgnorePattern(relativePath.Replace('\\', '/'), isDirectory, normalized, pattern.EndsWith('/')))
            {
                ignored = !isNegated;
            }
        }

        return ignored;
    }

    public static string ApplyProjectSnippetIgnores(string root, string text)
        => ApplySnippetIgnores(File.Exists(Path.Combine(root, SnippetIgnoreRelativePath))
            ? File.ReadAllText(Path.Combine(root, SnippetIgnoreRelativePath))
            : "", text);

    public static string ApplySnippetIgnores(string rulesMarkdown, string text)
    {
        var lines = CandidateLines(rulesMarkdown).ToArray();
        if (!IsEnabled(lines)) return text;
        var rules = lines.Where(line => !IsEnabledDirective(line)).Select(ParseSnippetRule).Where(rule => rule is not null).Cast<SnippetRule>().ToArray();
        if (rules.Length == 0) return text;

        var kept = new List<string>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (!rules.Any(rule => rule.Matches(line))) kept.Add(line);
        }

        return string.Join(Environment.NewLine, kept).Trim();
    }

    public static void EnsureDefaultFiles(string root)
    {
        Ensure(root, FileIgnoreRelativePath, DefaultFileIgnoreMarkdown);
        Ensure(root, SnippetIgnoreRelativePath, DefaultSnippetIgnoreMarkdown);
    }

    private static void Ensure(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static IEnumerable<string> CandidateLines(string markdown)
    {
        var inFence = false;
        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || line.Length == 0 || line.StartsWith('#')) continue;
            line = BulletPrefixRegex().Replace(line, "").Trim();
            if (line.Length > 0 && !line.StartsWith('#')) yield return line;
        }
    }

    private static bool IsEnabled(IReadOnlyList<string> lines)
    {
        var directive = lines.LastOrDefault(IsEnabledDirective);
        return directive is null || !directive.EndsWith("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnabledDirective(string line)
        => line.StartsWith("enabled:", StringComparison.OrdinalIgnoreCase);

    private static SnippetRule? ParseSnippetRule(string line)
    {
        var index = line.IndexOf(':');
        if (index <= 0) return new SnippetRule("contains", line);
        var kind = line[..index].Trim().ToLowerInvariant();
        var value = line[(index + 1)..].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : new SnippetRule(kind, value);
    }

    private static bool MatchesFileIgnorePattern(string relativePath, bool isDirectory, string normalized, bool directoryOnly)
    {
        if (directoryOnly && !isDirectory) return false;
        if (directoryOnly && relativePath.Equals(normalized, StringComparison.OrdinalIgnoreCase)) return true;
        if (relativePath.Equals(normalized, StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith(normalized + '/', StringComparison.OrdinalIgnoreCase) || Path.GetFileName(relativePath).Equals(normalized, StringComparison.OrdinalIgnoreCase)) return true;
        if (normalized.StartsWith("*.") && relativePath.EndsWith(normalized[1..], StringComparison.OrdinalIgnoreCase)) return true;
        if (normalized.IndexOfAny(['*', '?']) >= 0 && MatchesGlob(relativePath, normalized)) return true;
        if (normalized.Contains("**/*.", StringComparison.Ordinal))
        {
            var prefix = normalized[..normalized.IndexOf("**/*.", StringComparison.Ordinal)].TrimEnd('/');
            var suffix = normalized[(normalized.IndexOf("**/*.", StringComparison.Ordinal) + 4)..];
            return relativePath.StartsWith(prefix + '/', StringComparison.OrdinalIgnoreCase) && relativePath.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool MatchesGlob(string relativePath, string normalized)
    {
        var target = normalized.Contains('/', StringComparison.Ordinal) ? relativePath : Path.GetFileName(relativePath);
        var regex = "^" + GlobToRegex(normalized) + "$";
        return Regex.IsMatch(target, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    private static string GlobToRegex(string pattern)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < pattern.Length; i++)
        {
            var character = pattern[i];
            if (character == '*')
            {
                if (i + 1 < pattern.Length && pattern[i + 1] == '*')
                {
                    builder.Append(".*");
                    i++;
                }
                else
                {
                    builder.Append("[^/]*");
                }
            }
            else if (character == '?')
            {
                builder.Append("[^/]");
            }
            else
            {
                builder.Append(Regex.Escape(character.ToString()));
            }
        }

        return builder.ToString();
    }

    private sealed record SnippetRule(string Kind, string Value)
    {
        public bool Matches(string line)
            => Kind switch
            {
                "starts-with" => line.TrimStart().StartsWith(Value, StringComparison.OrdinalIgnoreCase),
                "regex" => SafeRegexMatch(line, Value),
                _ => line.Contains(Value, StringComparison.OrdinalIgnoreCase)
            };

        private static bool SafeRegexMatch(string line, string pattern)
        {
            try { return Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase); }
            catch (ArgumentException) { return false; }
        }
    }

    [GeneratedRegex(@"^[-*]\s+|\d+\.\s+|\[[ xX]\]\s+")]
    private static partial Regex BulletPrefixRegex();
}
