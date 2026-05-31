using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerAiCore(this IServiceCollection services)
    {
        services.AddSingleton<IAiModelCatalogService, StaticAiModelCatalogService>();
        services.AddSingleton<IAiTokenEstimator, SimpleAiTokenEstimator>();
        services.AddSingleton<IAiSafetyService, AiSafetyService>();
        services.AddSingleton<IAiRedactionService, AiRedactionService>();
        services.AddSingleton<IAiPromptBuilder, AiContextPackPromptBuilder>();
        services.AddSingleton<IAiPromptPreviewService, AiPromptPreviewService>();
        services.AddSingleton<IAiRunService, AiRunService>();
        return services;
    }
}

public sealed class StaticAiModelCatalogService : IAiModelCatalogService
{
    public IReadOnlyList<AiModelInfo> GetModels(AiProviderKind providerKind) => providerKind switch
    {
        AiProviderKind.OpenAI =>
        [
            new("gpt-4.1-mini", "GPT-4.1 mini", providerKind, [AiModelCapability.Chat], 0.40m, 1.60m),
            new("gpt-4.1", "GPT-4.1", providerKind, [AiModelCapability.Chat], 2.00m, 8.00m)
        ],
        AiProviderKind.Ollama => [new("llama3.1", "llama3.1", providerKind, [AiModelCapability.Chat, AiModelCapability.Local])],
        _ => [new("model", "Configured model", providerKind, [AiModelCapability.Chat])]
    };
}

public sealed class SimpleAiTokenEstimator : IAiTokenEstimator
{
    public AiTokenEstimate Estimate(string prompt, int maxOutputTokens)
    {
        var promptTokens = Math.Max(1, (int)Math.Ceiling((prompt?.Length ?? 0) / 4.0));
        return new AiTokenEstimate(promptTokens, Math.Max(0, maxOutputTokens), promptTokens + Math.Max(0, maxOutputTokens));
    }

    public AiCostEstimate EstimateCost(AiTokenEstimate tokens, AiModelInfo? model)
    {
        if (model?.InputUsdPerMillionTokens is null || model.OutputUsdPerMillionTokens is null)
        {
            return new AiCostEstimate(null, null, null);
        }

        var prompt = tokens.PromptTokens / 1_000_000m * model.InputUsdPerMillionTokens.Value;
        var output = tokens.MaxOutputTokens / 1_000_000m * model.OutputUsdPerMillionTokens.Value;
        return new AiCostEstimate(decimal.Round(prompt, 6), decimal.Round(output, 6), decimal.Round(prompt + output, 6));
    }
}

public sealed class AiSafetyService : IAiSafetyService
{
    private static readonly Regex[] SecretPatterns =
    [
        new(@"sk-[A-Za-z0-9_\-]{16,}", RegexOptions.Compiled),
        new(@"(?i)(api[_-]?key|access[_-]?token|secret)\s*[:=]\s*['""]?[^'""\s]{8,}", RegexOptions.Compiled),
        new(@"(?i)(password|pwd)\s*=\s*[^;\r\n]{6,}", RegexOptions.Compiled),
        new(@"-----BEGIN (RSA |EC |OPENSSH |)PRIVATE KEY-----", RegexOptions.Compiled),
        new(@"(?i)Server=.*;.*(Password|Pwd)=", RegexOptions.Compiled)
    ];

    public AiRunSafetyCheck Check(string content, ContextPackManifest? pack = null, ContextFreshnessReport? freshness = null)
    {
        var warnings = new List<AiSafetyWarning>();
        if (SecretPatterns.Any(pattern => pattern.IsMatch(content)))
        {
            warnings.Add(new AiSafetyWarning("possible-secret", "High", "Prompt content appears to contain a secret-like value."));
        }

        if (content.Contains(".env", StringComparison.OrdinalIgnoreCase) || content.Contains("private key", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(new AiSafetyWarning("sensitive-file-reference", "Medium", "Prompt references secret-prone files or private keys."));
        }

        if (pack?.MissingFiles.Count > 0)
        {
            warnings.Add(new AiSafetyWarning("missing-pack-files", "Medium", "The selected context pack has missing files."));
        }

        if (freshness is not null && freshness.FreshnessScore < 70)
        {
            warnings.Add(new AiSafetyWarning("low-context-freshness", "Medium", $"Context freshness is {freshness.FreshnessScore}."));
        }

        if (freshness is not null && (freshness.ManuallyModifiedFiles.Count > 0 || freshness.StaleContextFiles.Count > 0))
        {
            warnings.Add(new AiSafetyWarning("context-needs-review", "Medium", "Generated context has stale or manually modified files."));
        }

        return new AiRunSafetyCheck(warnings.Count > 0, warnings, warnings.Count > 0);
    }
}

public sealed class AiRedactionService(IAiSafetyService safety) : IAiRedactionService
{
    private static readonly Regex SecretValuePattern = new(@"(?i)((api[_-]?key|access[_-]?token|secret|password|pwd)\s*[:=]\s*['""]?)([^'""\s;]+)", RegexOptions.Compiled);
    private static readonly Regex OpenAiKeyPattern = new(@"sk-[A-Za-z0-9_\-]{16,}", RegexOptions.Compiled);

    public AiRedactionResult Redact(string content)
    {
        var count = 0;
        var redacted = SecretValuePattern.Replace(content, match => { count++; return match.Groups[1].Value + "[REDACTED]"; });
        redacted = OpenAiKeyPattern.Replace(redacted, _ => { count++; return "sk-[REDACTED]"; });
        var check = safety.Check(content);
        return new AiRedactionResult(redacted, check.Warnings, count);
    }
}

public sealed class AiContextPackPromptBuilder(
    IProjectRepository projects,
    IContextPackService packs,
    IContextPackReader packReader,
    IContextFreshnessService freshness,
    IAiProviderConfigurationRepository configurations,
    IAiModelCatalogService models,
    IAiTokenEstimator tokens,
    IAiSafetyService safety) : IAiPromptBuilder
{
    public async Task<AiPromptPreview> BuildPreviewAsync(Guid projectId, string providerId, string packId, string? userInstruction, int maxOutputTokens, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var pack = await packs.GetAsync(project, packId, cancellationToken) ?? throw new InvalidOperationException($"Context pack '{packId}' was not found.");
        var configuration = await configurations.GetAsync(providerId, cancellationToken) ?? throw new InvalidOperationException($"AI provider '{providerId}' is not configured.");
        var packContent = await packReader.ReadPackContentAsync(project, packId, cancellationToken);
        var freshnessReport = await freshness.CheckAsync(project, cancellationToken);
        var prompt = BuildPrompt(project, pack, packContent, userInstruction, freshnessReport);
        var tokenEstimate = tokens.Estimate(prompt, maxOutputTokens);
        var model = models.GetModels(configuration.Kind).FirstOrDefault(m => m.Id.Equals(configuration.Model, StringComparison.OrdinalIgnoreCase));
        var safetyCheck = safety.Check(prompt, pack, freshnessReport);
        return new AiPromptPreview(project.Id, project.Name, providerId, configuration.Kind, configuration.Model, packId, pack.Pack.Items.Select(i => i.RelativePath).ToArray(), pack.MissingFiles, freshnessReport.RecommendedActions, safetyCheck.Warnings, tokenEstimate, tokens.EstimateCost(tokenEstimate, model), prompt, Hash(prompt));
    }

    private static string BuildPrompt(Project project, ContextPackManifest pack, string content, string? userInstruction, ContextFreshnessReport freshness)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are helping with a SourceWorker local project context pack.");
        builder.AppendLine("Safe AI assistant rules:");
        builder.AppendLine("- Do not guess. Use detected source file paths.");
        builder.AppendLine("- Do not invent method names.");
        builder.AppendLine("- Preserve clean architecture and keep Domain dependency-free.");
        builder.AppendLine("- Do not write files or suggest automatic file writes.");
        builder.AppendLine("- Do not add AI/MCP/MAUI/cloud/licensing unless explicitly requested.");
        builder.AppendLine("- Recommend build/tests for any proposed changes.");
        builder.AppendLine();
        builder.AppendLine($"Project: {project.Name}");
        builder.AppendLine($"Pack: {pack.Pack.Id} - {pack.Pack.Name}");
        builder.AppendLine($"Freshness score: {freshness.FreshnessScore}");
        if (pack.MissingFiles.Count > 0) builder.AppendLine($"Missing pack files: {string.Join(", ", pack.MissingFiles)}");
        if (!string.IsNullOrWhiteSpace(userInstruction))
        {
            builder.AppendLine();
            builder.AppendLine("User instruction:");
            builder.AppendLine(userInstruction);
        }
        builder.AppendLine();
        builder.AppendLine("Context pack content:");
        builder.AppendLine(content);
        return builder.ToString();
    }

    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

public sealed class AiPromptPreviewService(IAiPromptBuilder builder) : IAiPromptPreviewService
{
    public Task<AiPromptPreview> PreviewAsync(Guid projectId, string providerId, string packId, string? userInstruction, int maxOutputTokens, CancellationToken cancellationToken)
        => builder.BuildPreviewAsync(projectId, providerId, packId, userInstruction, maxOutputTokens, cancellationToken);
}

public sealed class AiRunService(IAiPromptPreviewService preview, IAiProviderConfigurationRepository configurations, IAiProviderRegistry registry, IAiRunRepository runs) : IAiRunService
{
    public async Task<AiRun> StartAsync(AiRunInput input, CancellationToken cancellationToken)
    {
        var configuration = await configurations.GetAsync(input.ProviderId, cancellationToken) ?? throw new InvalidOperationException("AI provider is not configured.");
        var prompt = await preview.PreviewAsync(input.ProjectId, input.ProviderId, input.PackId, input.UserInstruction, input.MaxOutputTokens, cancellationToken);
        if (prompt.SafetyWarnings.Count > 0 && !input.ConfirmSafetyWarnings)
        {
            var blocked = NewRun(input, configuration, prompt, AiRunStatus.SafetyBlocked, null, "Safety warnings require confirmation.");
            return await runs.SaveAsync(blocked, cancellationToken);
        }

        var running = await runs.SaveAsync(NewRun(input, configuration, prompt, AiRunStatus.Running, null, null), cancellationToken);
        try
        {
            var provider = registry.GetProvider(input.ProviderId) ?? throw new InvalidOperationException("AI provider implementation was not found.");
            var output = await provider.RunAsync(configuration, prompt.PromptText, input.MaxOutputTokens, cancellationToken);
            return await runs.SaveAsync(running with { Status = AiRunStatus.Completed, CompletedUtc = DateTimeOffset.UtcNow, OutputText = output.Text, OutputHash = output.OutputHash }, cancellationToken);
        }
        catch (Exception ex)
        {
            return await runs.SaveAsync(running with { Status = AiRunStatus.Failed, CompletedUtc = DateTimeOffset.UtcNow, Error = ex.Message }, cancellationToken);
        }
    }

    public Task<IReadOnlyList<AiRun>> GetRunsAsync(Guid projectId, CancellationToken cancellationToken) => runs.GetByProjectAsync(projectId, cancellationToken);
    public Task<AiRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken) => runs.GetAsync(runId, cancellationToken);

    private static AiRun NewRun(AiRunInput input, AiProviderConfiguration configuration, AiPromptPreview prompt, AiRunStatus status, string? output, string? error)
        => new(Guid.NewGuid(), input.ProjectId, input.ProviderId, configuration.Kind, configuration.Model, input.PackId, input.UserInstruction, prompt.PromptHash, false, null, prompt.IncludedFiles, prompt.SafetyWarnings, prompt.TokenEstimate, status, DateTimeOffset.UtcNow, status is AiRunStatus.Completed or AiRunStatus.Failed or AiRunStatus.SafetyBlocked ? DateTimeOffset.UtcNow : null, error, output, output is null ? null : AiContextPackPromptBuilder.Hash(output));
}
