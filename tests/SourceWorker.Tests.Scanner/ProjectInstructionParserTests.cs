using SourceWorker.Context;
using SourceWorker.Domain;
using SourceWorker.Recipes;

namespace SourceWorker.Tests.Scanner;

public sealed class ProjectInstructionParserTests
{
    [Fact]
    public void Parse_extracts_structured_instruction_sections()
    {
        var markdown = """
        # todo-louie

        ## files
        - `src/App/Program.cs`
        - missing/file.cs

        ## mindset
        - do not guess
        - preserve architecture

        ## todo
        - [ ] implement import
        1. test parser

        ## skip for now / later
        - dashboard

        ## finally
        - [ ] run tests

        ## mystery
        - keep this

        ```txt
        ## not a section
        - not a parsed item
        ```
        """;

        var doc = new ProjectInstructionParser().Parse(Guid.NewGuid(), "C:\\project", "C:\\todo-louie.md", markdown);

        Assert.Contains(doc.Sections, s => s.Title == "files");
        Assert.Contains(doc.SourcePaths, p => p.OriginalPath == "src/App/Program.cs");
        Assert.Contains(doc.Rules, r => r.Text == "do not guess");
        Assert.Contains(doc.Todos, t => t.Text == "implement import");
        Assert.Contains(doc.SkipItems, s => s.Text == "dashboard");
        Assert.Contains(doc.VerificationItems, v => v.Text == "run tests");
        Assert.Contains(doc.Sections, s => s.Title == "mystery");
        Assert.DoesNotContain(doc.Sections, s => s.Title == "not a section");
    }

    [Fact]
    public void Source_path_validator_marks_existing_missing_and_outside_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-paths-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "Program.cs"), "");
        var outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid() + ".cs");
        File.WriteAllText(outside, "");

        var results = new SourcePathValidator().Validate(root, ["src/Program.cs", "missing.cs", outside]);

        Assert.Contains(results, p => p.OriginalPath == "src/Program.cs" && p.Exists && p.Type == SourcePathType.File && p.IsInsideProject);
        Assert.Contains(results, p => p.OriginalPath == "missing.cs" && !p.Exists && p.WarningMessage == "Path was not found.");
        Assert.Contains(results, p => p.OriginalPath == outside && !p.IsInsideProject && p.WarningMessage == "Path is outside the project root.");
    }

    [Fact]
    public async Task Imported_recipe_is_created_as_yaml()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-recipe-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var project = new Project(Guid.NewGuid(), "Sample", root, DateTimeOffset.UtcNow);
        var doc = new ProjectInstructionParser().Parse(project.Id, root, Path.Combine(root, "todo-louie.md"), """
            ## files
            - `src/Program.cs`
            ## mindset
            - do not guess
            ## todo
            - build import
            ## finally
            - run tests
            """);

        var recipe = new RecipeFactory().CreateImportedRecipe(project, doc);
        await new FileRecipeRepository().SaveAsync(project, recipe, CancellationToken.None);

        var yaml = await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "recipes", "imported-todo-louie.yaml"));
        Assert.Contains("id: \"imported-todo-louie\"", yaml);
        Assert.Contains("do not guess", yaml);
        Assert.Contains("run tests", yaml);
    }
}
