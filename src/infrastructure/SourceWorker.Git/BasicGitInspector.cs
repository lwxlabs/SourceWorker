using System.Diagnostics;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Git;

public sealed class BasicGitInspector : IGitInspector
{
    public async Task<GitSnapshot> InspectAsync(Project project, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Path.Combine(project.RootPath, ".git")))
        {
            return new GitSnapshot(Guid.NewGuid(), project.Id, null, null, false, DateTimeOffset.UtcNow);
        }

        var branch = await Git(project.RootPath, "branch --show-current", cancellationToken);
        var sha = await Git(project.RootPath, "rev-parse --short HEAD", cancellationToken);
        var status = await Git(project.RootPath, "status --porcelain", cancellationToken);
        return new GitSnapshot(Guid.NewGuid(), project.Id, EmptyToNull(branch), EmptyToNull(sha), !string.IsNullOrWhiteSpace(status), DateTimeOffset.UtcNow);
    }

    private static async Task<string> Git(string workingDirectory, string arguments, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null) return string.Empty;
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 ? output.Trim() : string.Empty;
    }

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
