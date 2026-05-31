using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Recipes;

public sealed class BasicRecipeRunner(IProjectScanner scanner, IContextWriter contextWriter) : IRecipeRunner
{
    public async Task<ContextWriteResult> RunBasicHandoffAsync(Project project, CancellationToken cancellationToken)
        => await contextWriter.WriteBasicContextAsync(await scanner.ScanAsync(project, cancellationToken), cancellationToken);
}
