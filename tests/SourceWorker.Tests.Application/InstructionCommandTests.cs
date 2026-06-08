using LwX.Flow;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Infrastructure;
using SourceWorker.Worker;

namespace SourceWorker.Tests.Application;

public sealed class InstructionCommandTests
{
    [Fact]
    public async Task Import_scan_doctor_recipes_and_run_recipe_work_together()
    {
        var root = Path.Combine(Path.GetTempPath(), "sourceworker-instructions-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "src"));
        await File.WriteAllTextAsync(Path.Combine(root, "Sample.sln"), "");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(root, "src", "Program.cs"), "// TODO: app");
        var instructionFile = Path.Combine(root, "todo-louie.md");
        await File.WriteAllTextAsync(instructionFile, """
            ## files
            - `src/Program.cs`
            - `missing.cs`
            ## mindset
            - do not guess
            ## todo
            - [ ] implement this
            ## skip for now
            - dashboard
            ## finally
            - [ ] run tests
            """);

        var services = new ServiceCollection()
            .AddSourceWorkerApplication()
            .AddTestSourceWorkerInfrastructure()
            .AddSourceWorkerJobs(addHostedWorker: false)
            .BuildServiceProvider();

        var flow = services.GetRequiredService<IRequestFlow>();
        var project = await flow.Send(new AddProjectCommand(root, "Sample"));
        var import = await flow.Send(new ImportProjectInstructionsCommand(project.Id, instructionFile));
        await flow.Send(new GenerateBasicContextCommand(project.Id));
        var recipes = await flow.Send(new GetRecipesQuery(project.Id));
        var run = await flow.Send(new RunRecipeCommand(project.Id, "backend-docs-compact"));
        var doctor = await flow.Send(new DoctorProjectQuery(project.Id));

        Assert.Equal("imported-todo-louie", import.RecipeId);
        Assert.Contains(recipes, recipe => recipe.Id == "backend-docs-compact");
        Assert.Contains(recipes, recipe => recipe.Id == "imported-todo-louie");
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "instructions", "README.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "instructions", "imported", "imported-instructions.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "instructions", "imported", "source-path-validation.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "docs", "backend", "00-project-index.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "docs", "backend", "README.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "docs", "backend", "08-code-snippets.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "docs", "backend", "09-reading-guide.md")));
        Assert.True(File.Exists(Path.Combine(root, "instructions", "Result", "Backend", "00-project-index.md")));
        Assert.True(File.Exists(Path.Combine(root, "instructions", "Result", "Backend", "README.md")));
        Assert.True(File.Exists(Path.Combine(root, "instructions", "Result", "Backend", "08-code-snippets.md")));
        Assert.True(File.Exists(Path.Combine(root, "instructions", "Result", "Backend", "09-reading-guide.md")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "packs", "backend-docs.pack.yaml")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "packs", "dogfood-feedback.pack.yaml")));
        Assert.True(File.Exists(Path.Combine(root, ".ai-context", "exports", "backend-docs-compact.yaml")));
        Assert.Contains("00-project-index.md", await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "packs", "backend-docs.pack.yaml")));
        Assert.Contains("09-reading-guide.md", await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "exports", "backend-docs-compact.yaml")));
        Assert.Contains("do not guess", await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "ai-handoff.md")));
        Assert.Contains("missing.cs", await File.ReadAllTextAsync(Path.Combine(root, ".ai-context", "index.yaml")));
        Assert.Equal("backend-docs-compact", run.RecipeId);
        Assert.True(doctor.ImportedInstructionsFound);
        Assert.Contains(doctor.MissingSourcePaths, path => path.OriginalPath == "missing.cs");
    }
}

