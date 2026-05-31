using System.Text;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Recipes;

public sealed class RecipeFactory : IRecipeFactory
{
    public IReadOnlyList<ProjectRecipe> CreateDefaultRecipes(Project project)
        =>
        [
            Create("ai-handoff", "AI Handoff", "Paste-ready assistant context.", ".ai-context", ["overview", "structure", "rules", "tasks"], ["."], true, true, true),
            Create("backend-docs-compact", "Backend Docs Compact", "Compact backend handoff documentation.", "instructions/Result/Backend", ["structure", "architecture", "configuration", "tasks"], ["src", "tests"], false, true, true),
            Create("project-onboarding", "Project Onboarding", "Short onboarding context for a new developer.", ".ai-context/onboarding", ["overview", "setup", "architecture"], ["."], false, false, true),
            Create("todo-plan", "Todo Plan", "TODO and verification oriented plan.", ".ai-context/todo-plan", ["todo", "skip", "verification"], ["."], false, false, true)
        ];

    public ProjectRecipe CreateImportedRecipe(Project project, ProjectInstructionDocument instructions)
    {
        var slug = Path.GetFileNameWithoutExtension(instructions.SourceFilePath).ToLowerInvariant().Replace(' ', '-').Replace('_', '-');
        var known = new HashSet<string>(["files", "important files and classes", "mindset", "todo", "sql server steps", "finally", "skip for now", "later"], StringComparer.OrdinalIgnoreCase);
        var extra = instructions.Sections
            .Where(s => !known.Any(k => s.NormalizedTitle.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .ToDictionary(s => s.Title, s => (IReadOnlyList<string>)s.Items.Select(i => i.Text).ToArray());

        return new ProjectRecipe(
            $"imported-{slug}",
            $"Imported {instructions.Title}",
            $"Generated from {Path.GetFileName(instructions.SourceFilePath)}.",
            "en",
            ".ai-context",
            "local",
            instructions.Sections.Select(s => s.Title).ToArray(),
            instructions.SourcePaths.Select(p => p.NormalizedPath).ToArray(),
            instructions.Rules.Select(r => r.Text).ToArray(),
            instructions.SkipItems.Select(s => s.Text).ToArray(),
            instructions.VerificationItems.Select(v => v.Text).ToArray(),
            [".ai-context/instructions/imported-instructions.md", ".ai-context/instructions/source-path-validation.md", ".ai-context/ai-handoff.md"],
            false,
            true,
            true,
            extra);
    }

    private static ProjectRecipe Create(string id, string name, string description, string outputPath, IReadOnlyList<string> sections, IReadOnlyList<string> scopes, bool mermaid, bool names, bool refs)
        => new(id, name, description, "en", outputPath, "local", sections, scopes, [], [], [], [], mermaid, names, refs, new Dictionary<string, IReadOnlyList<string>>());
}

public sealed class FileRecipeRepository : IRecipeRepository
{
    public async Task SaveAsync(Project project, ProjectRecipe recipe, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(project.RootPath, ".ai-context", "recipes");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"{recipe.Id}.yaml"), ToYaml(recipe), cancellationToken);
    }

    public Task<IReadOnlyList<ProjectRecipe>> ListAsync(Project project, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(project.RootPath, ".ai-context", "recipes");
        if (!Directory.Exists(directory)) return Task.FromResult<IReadOnlyList<ProjectRecipe>>([]);
        var recipes = Directory.EnumerateFiles(directory, "*.yaml")
            .Select(ReadMinimal)
            .Where(recipe => recipe is not null)
            .Cast<ProjectRecipe>()
            .ToArray();
        return Task.FromResult<IReadOnlyList<ProjectRecipe>>(recipes);
    }

    public async Task<ProjectRecipe?> GetAsync(Project project, string recipeId, CancellationToken cancellationToken)
    {
        var recipes = await ListAsync(project, cancellationToken);
        return recipes.FirstOrDefault(r => r.Id.Equals(recipeId, StringComparison.OrdinalIgnoreCase));
    }

    public static string ToYaml(ProjectRecipe recipe)
    {
        var builder = new StringBuilder();
        Scalar("id", recipe.Id);
        Scalar("name", recipe.Name);
        Scalar("description", recipe.Description);
        Scalar("language", recipe.Language);
        Scalar("outputPath", recipe.OutputPath);
        Scalar("targetPlatform", recipe.TargetPlatform);
        Scalar("mermaidEnabled", recipe.MermaidEnabled.ToString().ToLowerInvariant());
        Scalar("requireClassAndMethodNames", recipe.RequireClassAndMethodNames.ToString().ToLowerInvariant());
        Scalar("requireSourceReferences", recipe.RequireSourceReferences.ToString().ToLowerInvariant());
        List("includedSections", recipe.IncludedSections);
        List("sourceScopes", recipe.SourceScopes);
        List("rules", recipe.Rules);
        List("skipItems", recipe.SkipItems);
        List("verificationChecklist", recipe.VerificationChecklist);
        List("generatedFiles", recipe.GeneratedFiles);
        if (recipe.ExtraSections.Count > 0)
        {
            builder.AppendLine("extraSections:");
            foreach (var section in recipe.ExtraSections)
            {
                builder.AppendLine($"  {Escape(section.Key)}:");
                foreach (var item in section.Value) builder.AppendLine($"    - \"{Escape(item)}\"");
            }
        }

        return builder.ToString();

        void Scalar(string key, string value) => builder.AppendLine($"{key}: \"{Escape(value)}\"");
        void List(string key, IEnumerable<string> values)
        {
            builder.AppendLine($"{key}:");
            var any = false;
            foreach (var value in values)
            {
                any = true;
                builder.AppendLine($"  - \"{Escape(value)}\"");
            }
            if (!any) builder.AppendLine("  []");
        }
    }

    private static ProjectRecipe? ReadMinimal(string path)
    {
        var values = File.ReadLines(path)
            .Where(line => line.Contains(':') && !line.TrimStart().StartsWith('-'))
            .Select(line => line.Split(':', 2))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim().Trim('"'), StringComparer.OrdinalIgnoreCase);

        return values.TryGetValue("id", out var id)
            ? new ProjectRecipe(id, values.GetValueOrDefault("name", id), values.GetValueOrDefault("description", ""), values.GetValueOrDefault("language", "en"), values.GetValueOrDefault("outputPath", ".ai-context"), values.GetValueOrDefault("targetPlatform", "local"), [], [], [], [], [], [], false, false, false, new Dictionary<string, IReadOnlyList<string>>())
            : null;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
