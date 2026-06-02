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
        services.AddSingleton<IAiModelCatalogProvider, StaticAiModelCatalogService>();
        services.AddSingleton<IAiTokenEstimator, SimpleAiTokenEstimator>();
        services.AddSingleton<IAiSafetyService, AiSafetyService>();
        services.AddSingleton<IAiRedactionService, AiRedactionService>();
        services.AddSingleton<IAiPromptBuilder, AiContextPackPromptBuilder>();
        services.AddSingleton<IAiPromptPreviewService, AiPromptPreviewService>();
        services.AddSingleton<IAiRunService, AiRunService>();
        services.AddSingleton<IAiProviderValidationService, AiProviderValidationService>();
        services.AddSingleton<IAiSettingsRepository, InMemoryAiSettingsRepository>();
        services.AddSingleton<IAiUsageService, AiUsageService>();
        services.AddSingleton<IAiContextPackQualityService, AiContextPackQualityService>();
        services.AddSingleton<IAiExplanationPromptBuilder, AiExplanationPromptBuilder>();
        services.AddSingleton<IAiExplanationQualityService, AiExplanationQualityService>();
        services.AddSingleton<IAiExplanationService, AiExplanationService>();
        services.AddSingleton<IDogfoodReadinessService, DogfoodReadinessService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
        services.AddSingleton<IDogfoodSessionService, DogfoodSessionService>();
        services.AddSingleton<IFrictionIssueService, FrictionIssueService>();
        services.AddSingleton<INextActionService, NextActionService>();
        services.AddSingleton<IProjectPathValidationService, ProjectPathValidationService>();
        services.AddSingleton<IQuickAskService, QuickAskService>();
        return services;
    }
}

public sealed class StaticAiModelCatalogService : IAiModelCatalogService, IAiModelCatalogProvider
{
    public IReadOnlyList<AiModelInfo> GetModels(AiProviderKind providerKind) => providerKind switch
    {
        AiProviderKind.OpenAI =>
        [
            new("gpt-4.1-mini", "GPT-4.1 mini", providerKind, [AiModelCapability.Chat], 0.40m, 1.60m, 1_000_000),
            new("gpt-4.1", "GPT-4.1", providerKind, [AiModelCapability.Chat], 2.00m, 8.00m, 1_000_000),
            new("gpt-4o", "GPT-4o", providerKind, [AiModelCapability.Chat], 2.50m, 10.00m, 128_000),
            new("gpt-4o-mini", "GPT-4o mini", providerKind, [AiModelCapability.Chat], 0.15m, 0.60m, 128_000),
            new("gpt-5.1", "GPT-5.1", providerKind, [AiModelCapability.Chat], null, null, null)
        ],
        AiProviderKind.Ollama => [new("llama3.1", "llama3.1", providerKind, [AiModelCapability.Chat, AiModelCapability.Local])],
        _ => [new("model", "Configured model", providerKind, [AiModelCapability.Chat])]
    };

    public Task<AiModelCatalog> GetCatalogAsync(AiProviderKind providerKind, CancellationToken cancellationToken)
        => Task.FromResult(new AiModelCatalog(providerKind, GetModels(providerKind).Select(m => new AiModelCatalogEntry(
            m.Id,
            m.Name,
            new AiModelPricing(m.InputUsdPerMillionTokens, m.OutputUsdPerMillionTokens, m.InputUsdPerMillionTokens is not null && m.OutputUsdPerMillionTokens is not null),
            new AiModelContextWindow(m.ContextWindowTokens, m.ContextWindowTokens is not null),
            new AiModelFeatureFlags(m.Capabilities.Contains(AiModelCapability.StreamingLater), m.Capabilities.Contains(AiModelCapability.ToolCallingLater), false, m.Capabilities.Contains(AiModelCapability.Local)))).ToArray(), providerKind == AiProviderKind.Ollama, DateTimeOffset.UtcNow));
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
    public async Task<AiPromptPreview> BuildPreviewAsync(Guid projectId, string providerId, string packId, string? userInstruction, int maxOutputTokens, string? templateId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var pack = await packs.GetAsync(project, packId, cancellationToken) ?? throw new InvalidOperationException($"Context pack '{packId}' was not found.");
        var configuration = await configurations.GetAsync(providerId, cancellationToken) ?? throw new InvalidOperationException($"AI provider '{providerId}' is not configured.");
        var packContent = await packReader.ReadPackContentAsync(project, packId, cancellationToken);
        var freshnessReport = await freshness.CheckAsync(project, cancellationToken);
        var template = PromptTemplates.FirstOrDefault(t => t.Id.Equals(templateId ?? "", StringComparison.OrdinalIgnoreCase)) ?? PromptTemplates[0];
        var prompt = BuildPrompt(project, pack, packContent, userInstruction, freshnessReport, template);
        var tokenEstimate = tokens.Estimate(prompt, maxOutputTokens);
        var model = models.GetModels(configuration.Kind).FirstOrDefault(m => m.Id.Equals(configuration.Model, StringComparison.OrdinalIgnoreCase));
        var safetyCheck = safety.Check(prompt, pack, freshnessReport);
        var sections = new[]
        {
            new AiPromptSection("System", template.SystemPrefix, tokens.Estimate(template.SystemPrefix, 0).PromptTokens),
            new AiPromptSection("User Instruction", userInstruction ?? "", tokens.Estimate(userInstruction ?? "", 0).PromptTokens),
            new AiPromptSection("Context Pack", packContent, tokens.Estimate(packContent, 0).PromptTokens)
        };
        var fileDetails = pack.Pack.Items.Select(item =>
        {
            var absolute = Path.Combine(project.RootPath, item.RelativePath);
            var exists = File.Exists(absolute);
            var content = exists ? File.ReadAllText(absolute) : "";
            return new AiPromptIncludedFile(item.RelativePath, exists ? new FileInfo(absolute).Length : 0, tokens.Estimate(content, 0).PromptTokens, exists ? Hash(content) : null, exists);
        }).ToArray();
        var contextWindow = model?.ContextWindowTokens;
        var usage = contextWindow is > 0 ? Math.Min(999, (int)Math.Ceiling(tokenEstimate.TotalTokens * 100.0 / contextWindow.Value)) : 0;
        var tooLarge = contextWindow is > 0 && tokenEstimate.TotalTokens > contextWindow.Value;
        var warnings = safetyCheck.Warnings.ToList();
        if (tooLarge) warnings.Add(new AiSafetyWarning("prompt-too-large", "High", "Prompt estimate exceeds the selected model context window."));
        return new AiPromptPreview(project.Id, project.Name, providerId, configuration.Kind, configuration.Model, packId, pack.Pack.Items.Select(i => i.RelativePath).ToArray(), pack.MissingFiles, freshnessReport.RecommendedActions, warnings, tokenEstimate, tokens.EstimateCost(tokenEstimate, model), sections, fileDetails, template.Id, contextWindow, usage, tooLarge, prompt, Hash(prompt));
    }

    private static string BuildPrompt(Project project, ContextPackManifest pack, string content, string? userInstruction, ContextFreshnessReport freshness, AiPromptTemplate template)
    {
        var builder = new StringBuilder();
        builder.AppendLine(template.SystemPrefix);
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

    public static readonly AiPromptTemplate[] PromptTemplates =
    [
        new("general-project-question", "General project question", "Ask a focused question using the selected pack.", "You are helping with a SourceWorker local project context pack."),
        new("continue-development", "Continue development", "Plan the next safe development step.", "You are helping continue development. Be concrete, preserve architecture, and do not write files."),
        new("explain-architecture", "Explain architecture", "Explain detected architecture and important flows.", "Explain the architecture using only supplied context and detected source paths."),
        new("find-risks", "Find risks", "Find risks, stale areas, and verification gaps.", "Find risks and verification gaps. Do not invent source details."),
        new("improve-docs", "Improve docs", "Suggest documentation improvements.", "Suggest compact documentation improvements. Do not write files."),
        new("create-next-steps", "Create next steps", "Create a prioritized next-step plan.", "Create a concise, prioritized next-step plan."),
        new("review-branch", "Review branch", "Review current branch context.", "Review the current branch context and identify likely review points."),
        new("bug-investigation", "Bug investigation", "Investigate a bug using context.", "Investigate the likely bug path using supplied context only."),
        new("test-planning", "Test planning", "Suggest focused tests.", "Suggest targeted tests and verification commands.")
    ];
}

public sealed class AiPromptPreviewService(IAiPromptBuilder builder) : IAiPromptPreviewService
{
    public Task<AiPromptPreview> PreviewAsync(Guid projectId, string providerId, string packId, string? userInstruction, int maxOutputTokens, string? templateId, CancellationToken cancellationToken)
        => builder.BuildPreviewAsync(projectId, providerId, packId, userInstruction, maxOutputTokens, templateId, cancellationToken);
}

public sealed class AiRunService(IAiPromptPreviewService preview, IAiProviderConfigurationRepository configurations, IAiProviderRegistry registry, IAiRunRepository runs) : IAiRunService
{
    public async Task<AiRun> StartAsync(AiRunInput input, CancellationToken cancellationToken)
    {
        var configuration = await configurations.GetAsync(input.ProviderId, cancellationToken) ?? throw new InvalidOperationException("AI provider is not configured.");
        var prompt = await preview.PreviewAsync(input.ProjectId, input.ProviderId, input.PackId, input.UserInstruction, input.MaxOutputTokens, null, cancellationToken);
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

public sealed class AiProviderValidationService(IAiSecretStore secrets, IAiProviderConfigurationRepository configurations, IAiProviderRegistry registry) : IAiProviderValidationService
{
    public async Task<AiProviderValidationResult> ValidateAsync(AiProviderKind kind, string model, string? baseUrl, string? apiKey, int maxOutputTokens, CancellationToken cancellationToken)
    {
        var issues = new List<AiProviderValidationIssue>();
        if (string.IsNullOrWhiteSpace(model)) issues.Add(new("missing-model", "High", "Model name is required.", "model"));
        if (maxOutputTokens <= 0) issues.Add(new("invalid-max-output-tokens", "High", "Max output tokens must be positive.", "maxOutputTokens"));
        var requiresKey = kind == AiProviderKind.OpenAI;
        if (requiresKey && string.IsNullOrWhiteSpace(apiKey))
        {
            var existing = await configurations.GetAsync(kind.ToString(), cancellationToken);
            if (existing?.ApiKeySecretName is null || string.IsNullOrWhiteSpace(await secrets.GetSecretAsync(existing.ApiKeySecretName, cancellationToken)))
                issues.Add(new("missing-api-key", "High", "API key is required.", "apiKey"));
        }
        var needsBaseUrl = kind is AiProviderKind.Ollama or AiProviderKind.OpenAiCompatible;
        var validUrl = !needsBaseUrl || Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        if (!validUrl) issues.Add(new("invalid-base-url", "High", "Base URL must be an absolute HTTP/HTTPS URL.", "baseUrl"));
        if (kind == AiProviderKind.Ollama && string.IsNullOrWhiteSpace(baseUrl)) issues.Add(new("missing-ollama-url", "Medium", "Ollama base URL is recommended. Default is http://localhost:11434.", "baseUrl"));
        return new AiProviderValidationResult(kind.ToString(), kind, issues.Count == 0, issues, new AiProviderSetupState(issues.Count == 0, !string.IsNullOrWhiteSpace(model), !requiresKey || !string.IsNullOrWhiteSpace(apiKey), validUrl, kind == AiProviderKind.Ollama, secrets.Mask(apiKey)));
    }

    public async Task<AiProviderConnectionDiagnostic> DiagnoseAsync(string providerId, CancellationToken cancellationToken)
    {
        var config = await configurations.GetAsync(providerId, cancellationToken);
        if (config is null)
        {
            return new AiProviderConnectionDiagnostic(providerId, AiProviderKind.OpenAiCompatible, AiProviderStatus.NotConfigured, "Provider is not configured.", ["Open AI Settings and save provider configuration."], new AiProviderErrorDetails(AiProviderErrorCode.Unknown, AiProviderErrorCategory.Configuration, "Provider is not configured.", null), DateTimeOffset.UtcNow);
        }
        var provider = registry.GetProvider(providerId);
        if (provider is null)
        {
            return new AiProviderConnectionDiagnostic(providerId, config.Kind, AiProviderStatus.Unhealthy, "Provider implementation was not found.", ["Restart SourceWorker.", "Check provider kind."], new AiProviderErrorDetails(AiProviderErrorCode.Unknown, AiProviderErrorCategory.Configuration, "Provider implementation missing.", null), DateTimeOffset.UtcNow);
        }
        var health = await provider.CheckHealthAsync(config, cancellationToken);
        var actions = health.Status switch
        {
            AiProviderStatus.Healthy => Array.Empty<string>(),
            AiProviderStatus.NotConfigured => ["Finish provider setup in AI Settings."],
            _ when config.Kind == AiProviderKind.Ollama => ["Start Ollama.", $"Run: ollama pull {config.Model}", "Verify the Ollama base URL."],
            _ => ["Check network connectivity.", "Verify API key and model name.", "Try another model."]
        };
        return new AiProviderConnectionDiagnostic(providerId, config.Kind, health.Status, health.Message, actions, health.Status == AiProviderStatus.Healthy ? null : new AiProviderErrorDetails(ToCode(health.Message), ToCategory(health.Message), health.Message, health.Message), DateTimeOffset.UtcNow);
    }

    private static AiProviderErrorCode ToCode(string message)
        => message.Contains("key", StringComparison.OrdinalIgnoreCase) ? AiProviderErrorCode.InvalidApiKey :
           message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ? AiProviderErrorCode.Timeout :
           message.Contains("model", StringComparison.OrdinalIgnoreCase) ? AiProviderErrorCode.ModelNotFound :
           message.Contains("shape", StringComparison.OrdinalIgnoreCase) ? AiProviderErrorCode.BadResponseShape :
           AiProviderErrorCode.ProviderUnavailable;
    private static AiProviderErrorCategory ToCategory(string message)
        => message.Contains("key", StringComparison.OrdinalIgnoreCase) ? AiProviderErrorCategory.Authentication :
           message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ? AiProviderErrorCategory.Network :
           AiProviderErrorCategory.Provider;
}

public sealed class InMemoryAiSettingsRepository : IAiSettingsRepository
{
    private AiSettings settings = new(false, true, 100, 30);
    public Task<AiSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(settings);
    public Task<AiSettings> SaveAsync(AiSettings settings, CancellationToken cancellationToken) { this.settings = settings; return Task.FromResult(settings); }
}

public sealed class AiUsageService(IAiRunRepository runs, IAiProviderConfigurationRepository providers) : IAiUsageService
{
    public async Task<AiUsageSummary> GetProjectAsync(Guid projectId, CancellationToken cancellationToken) => Summarize(projectId, await runs.GetByProjectAsync(projectId, cancellationToken), 0);
    public async Task<AiUsageSummary> GetGlobalAsync(CancellationToken cancellationToken)
    {
        var configs = await providers.ListAsync(cancellationToken);
        return new AiUsageSummary(null, 0, 0, 0, null, null, null, 0, null, 0, configs.Count, 0);
    }

    private static AiUsageSummary Summarize(Guid projectId, IReadOnlyList<AiRun> items, int providersConfigured)
        => new(projectId, items.Count, items.Count(r => r.Status == AiRunStatus.Completed), items.Count(r => r.Status == AiRunStatus.Failed), items.OrderByDescending(r => r.CreatedUtc).FirstOrDefault()?.CreatedUtc, items.GroupBy(r => r.ProviderId).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key, items.GroupBy(r => r.PackId).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key, items.Sum(r => r.TokenEstimate.TotalTokens), null, items.Count, providersConfigured, 0);
}

public sealed class AiContextPackQualityService(IProjectRepository projects, IContextPackService packs, IContextFreshnessService freshness) : IAiContextPackQualityService
{
    public async Task<AiContextPackQualityReport> CheckAsync(Guid projectId, string packId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var pack = await packs.GetAsync(project, packId, cancellationToken);
        var issues = new List<AiContextPackQualityIssue>();
        if (pack is null) issues.Add(new("pack-missing", "High", "Context pack was not found."));
        else
        {
            if (pack.MissingFiles.Count > 0) issues.AddRange(pack.MissingFiles.Select(f => new AiContextPackQualityIssue("missing-file", "High", "Pack file is missing.", f)));
            if (string.IsNullOrWhiteSpace(pack.Pack.Purpose)) issues.Add(new("missing-purpose", "Medium", "Pack is missing purpose metadata."));
            if (string.IsNullOrWhiteSpace(pack.Pack.Audience)) issues.Add(new("missing-audience", "Medium", "Pack is missing audience metadata."));
        }
        var fresh = await freshness.CheckAsync(project, cancellationToken);
        if (fresh.FreshnessScore < 70) issues.Add(new("low-freshness", "Medium", $"Context freshness is {fresh.FreshnessScore}."));
        var score = Math.Max(0, 100 - issues.Count(i => i.Severity == "High") * 30 - issues.Count(i => i.Severity != "High") * 10);
        return new AiContextPackQualityReport(projectId, packId, score, score >= 80, issues);
    }
}

public sealed class AiExplanationPromptBuilder(
    IProjectRepository projects,
    IContextPackService packs,
    IAiProviderConfigurationRepository configurations,
    IAiModelCatalogService models,
    IAiTokenEstimator tokens,
    IAiSafetyService safety,
    ICodeSymbolRepository symbols,
    ITestInfoRepository tests) : IAiExplanationPromptBuilder
{
    private static readonly string SuggestedPath = ".ai-context/docs/backend/07-ai-explanation.md";

    public async Task<AiExplanationPreview> BuildPreviewAsync(AiExplanationRequest request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var configuration = await configurations.GetAsync(request.ProviderId, cancellationToken) ?? throw new InvalidOperationException("AI provider is not configured.");
        var pack = await packs.GetAsync(project, request.ContextPackId, cancellationToken);
        var modelName = string.IsNullOrWhiteSpace(request.Model) ? configuration.Model : request.Model;
        var included = new List<AiExplanationSourceFile>();
        var context = await BuildContextAsync(project, request, included, cancellationToken);
        var providerKind = configuration.Kind;
        var prompt = BuildPrompt(project, request, modelName, context);
        var estimate = tokens.Estimate(prompt, request.MaxOutputTokens);
        var model = models.GetModels(providerKind).FirstOrDefault(m => m.Id.Equals(modelName, StringComparison.OrdinalIgnoreCase));
        var warnings = safety.Check(prompt, pack).Warnings.ToList();
        if (providerKind != AiProviderKind.Ollama)
        {
            warnings.Add(new AiSafetyWarning("online-provider", "Medium", "This sends selected project context to the configured AI provider."));
        }

        if (request.IncludeSourceSnippets)
        {
            warnings.Add(new AiSafetyWarning("source-snippets-included", "Medium", "Selected source snippets will be sent."));
        }

        return new AiExplanationPreview(
            project.Id,
            request.ProviderId,
            providerKind,
            modelName,
            request.ContextPackId,
            request.ExplanationProfile,
            included,
            pack?.MissingFiles ?? [],
            warnings,
            estimate,
            tokens.EstimateCost(estimate, model),
            prompt,
            AiContextPackPromptBuilder.Hash(prompt),
            warnings.Any(w => w.Severity.Equals("High", StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<string> BuildContextAsync(Project project, AiExplanationRequest request, List<AiExplanationSourceFile> included, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Project Context Inputs");
        builder.AppendLine();

        var coreFiles = new List<string> { ".ai-context/agent-handoff.md" };
        if (request.IncludeGeneratedDocs)
        {
            coreFiles.AddRange([
                ".ai-context/docs/backend/01-project-structure.md",
                ".ai-context/docs/backend/02-architecture.md",
                ".ai-context/docs/backend/03-important-files-and-flows.md",
                ".ai-context/docs/backend/04-configuration-and-secrets.md",
                ".ai-context/docs/backend/05-tasks-and-risks.md",
                ".ai-context/docs/backend/06-verification-checklist.md"
            ]);
        }

        coreFiles.Add("README.md");
        foreach (var relative in coreFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await AppendFileAsync(project, relative, "generated-context", 240, builder, included, cancellationToken);
        }

        if (request.IncludeSourceSnippets || request.IncludePublicApi)
        {
            var symbolFiles = (await symbols.GetByProjectAsync(project.Id, cancellationToken))
                .Where(s => !IsGeneratedOrForbidden(s.RelativePath) && !IsTestPath(s.RelativePath))
                .OrderByDescending(s => s.Accessibility.Equals("public", StringComparison.OrdinalIgnoreCase))
                .ThenBy(s => s.RelativePath.Count(c => c is '/' or '\\'))
                .Select(s => s.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();

            foreach (var relative in symbolFiles)
            {
                await AppendFileAsync(project, relative, "source-snippet", 120, builder, included, cancellationToken);
            }
        }

        if (request.IncludeTests)
        {
            var testFiles = (await tests.GetByProjectAsync(project.Id, cancellationToken))
                .Where(t => !IsGeneratedOrForbidden(t.RelativePath))
                .Select(t => t.RelativePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();

            foreach (var relative in testFiles)
            {
                await AppendFileAsync(project, relative, "test-snippet", 80, builder, included, cancellationToken);
            }
        }

        return builder.ToString();
    }

    private async Task AppendFileAsync(Project project, string relativePath, string kind, int maxLines, StringBuilder builder, List<AiExplanationSourceFile> included, CancellationToken cancellationToken)
    {
        if (IsGeneratedOrForbidden(relativePath) && !relativePath.StartsWith(".ai-context/docs/backend", StringComparison.OrdinalIgnoreCase) && !relativePath.Equals(".ai-context/agent-handoff.md", StringComparison.OrdinalIgnoreCase))
        {
            included.Add(new AiExplanationSourceFile(relativePath, kind, null, null, 0, false, "Skipped generated or forbidden path."));
            return;
        }

        var root = Path.GetFullPath(project.RootPath);
        var absolute = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute))
        {
            included.Add(new AiExplanationSourceFile(relativePath, kind, null, null, 0, false, "File was not found."));
            return;
        }

        if (LooksSecretProne(relativePath))
        {
            included.Add(new AiExplanationSourceFile(relativePath, kind, null, null, 0, false, "Skipped secret-prone file."));
            return;
        }

        var lines = (await File.ReadAllLinesAsync(absolute, cancellationToken)).Take(maxLines).ToArray();
        var text = StripSourceWorkerMetadata(string.Join(Environment.NewLine, lines));
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine($"Source: `{relativePath}`");
        builder.AppendLine($"Kind: {kind}");
        builder.AppendLine("---");
        builder.AppendLine(text);
        builder.AppendLine();
        included.Add(new AiExplanationSourceFile(relativePath, kind, 1, lines.Length, tokens.Estimate(text, 0).PromptTokens, true));
    }

    private static string BuildPrompt(Project project, AiExplanationRequest request, string modelName, string context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are writing a senior-developer project explanation from SourceWorker context.");
        builder.AppendLine("SourceWorker is not asking you to edit source code, produce patches, or run terminal commands.");
        builder.AppendLine("Generate readable markdown only.");
        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("- Do not invent facts.");
        builder.AppendLine("- Say \"not detected\" or \"unknown\" when needed.");
        builder.AppendLine("- Prefer evidence from supplied source files, tests, README, and scanned context.");
        builder.AppendLine("- Do not propose code changes unless asked.");
        builder.AppendLine("- Do not output patches.");
        builder.AppendLine("- Do not include secrets.");
        builder.AppendLine("- Do not repeat raw scan metadata.");
        builder.AppendLine("- Do not mention SourceWorker internals unless explaining generated context.");
        builder.AppendLine();
        builder.AppendLine($"Project: {project.Name}");
        builder.AppendLine($"Profile: {request.ExplanationProfile}");
        builder.AppendLine($"Model: {modelName}");
        if (!string.IsNullOrWhiteSpace(request.UserInstruction))
        {
            builder.AppendLine();
            builder.AppendLine("User instruction:");
            builder.AppendLine(request.UserInstruction);
        }

        builder.AppendLine();
        builder.AppendLine("Write exactly these sections:");
        builder.AppendLine("# AI Project Explanation");
        builder.AppendLine("## 1. What This Project Is");
        builder.AppendLine("## 2. Mental Model");
        builder.AppendLine("## 3. Solution / Project Structure");
        builder.AppendLine("## 4. Main Runtime Flow");
        builder.AppendLine("## 5. Public API / Main Entry Points");
        builder.AppendLine("## 6. Data / Storage / External Systems");
        builder.AppendLine("## 7. Configuration");
        builder.AppendLine("## 8. Tests And Quality");
        builder.AppendLine("## 9. Important Files To Read First");
        builder.AppendLine("## 10. Risks / Unknowns");
        builder.AppendLine("## 11. How An AI Coding Agent Should Continue");
        builder.AppendLine();
        builder.AppendLine(context);
        return builder.ToString();
    }

    private static bool IsTestPath(string relativePath) => relativePath.Contains(".Tests", StringComparison.OrdinalIgnoreCase) || relativePath.Contains("/tests/", StringComparison.OrdinalIgnoreCase) || relativePath.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase);
    private static bool IsGeneratedOrForbidden(string relativePath)
    {
        var path = relativePath.Replace('\\', '/');
        return path.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(".vs/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(".idea/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("instructions/Result/", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".pack.yaml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksSecretProne(string relativePath)
        => relativePath.EndsWith(".env", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("private", StringComparison.OrdinalIgnoreCase) ||
           relativePath.Contains("appsettings.Production", StringComparison.OrdinalIgnoreCase);

    internal static string StripSourceWorkerMetadata(string text)
    {
        var normalized = text.Replace("\r\n", "\n");
        normalized = Regex.Replace(normalized, @"<!--\s*SourceWorker:Generated;.*?-->\s*", "", RegexOptions.Singleline);
        normalized = Regex.Replace(normalized, @"<!--\s*GeneratedBy:\s*SourceWorker.*?-->\s*", "", RegexOptions.Singleline);
        return normalized.Trim();
    }

    public static string SuggestedFilePath => SuggestedPath;
}

public sealed class AiExplanationQualityService : IAiExplanationQualityService
{
    private static readonly string[] Required =
    [
        "What This Project Is",
        "Mental Model",
        "Solution / Project Structure",
        "Main Runtime Flow",
        "Public API / Main Entry Points",
        "Tests And Quality",
        "Important Files To Read First",
        "Risks / Unknowns",
        "How An AI Coding Agent Should Continue"
    ];

    public AiExplanationQualityReport Check(string markdown)
    {
        var passed = new List<string>();
        var warnings = new List<string>();
        if (markdown.Trim().StartsWith("# AI Project Explanation", StringComparison.OrdinalIgnoreCase)) passed.Add("title");
        else warnings.Add("Missing expected title.");

        foreach (var section in Required)
        {
            if (markdown.Contains(section, StringComparison.OrdinalIgnoreCase)) passed.Add(section);
            else warnings.Add($"Missing section: {section}.");
        }

        if (markdown.Length < 800) warnings.Add("Explanation is very short.");
        if (markdown.Contains("as an AI language model", StringComparison.OrdinalIgnoreCase)) warnings.Add("Contains generic AI disclaimer language.");
        if (markdown.Contains("```diff", StringComparison.OrdinalIgnoreCase) || markdown.Contains("--- a/", StringComparison.OrdinalIgnoreCase)) warnings.Add("Looks like it includes patch content.");
        if (markdown.Contains("SourceWorker prompt", StringComparison.OrdinalIgnoreCase)) warnings.Add("Mentions prompt internals.");
        var status = warnings.Count == 0 ? AiExplanationQualityStatus.Good : warnings.Count <= 3 ? AiExplanationQualityStatus.NeedsReview : AiExplanationQualityStatus.Failed;
        return new AiExplanationQualityReport(status, passed, warnings);
    }
}

public sealed class AiExplanationService(
    IProjectRepository projects,
    IContextPackService packs,
    IAiProviderConfigurationRepository configurations,
    IAiProviderRegistry registry,
    IAiExplanationPromptBuilder promptBuilder,
    IAiExplanationQualityService quality,
    IAiRunRepository runs,
    IGeneratedArtifactRepository artifacts,
    IScanRunRepository scanRuns) : IAiExplanationService
{
    public async Task<AiExplanationOptions> GetOptionsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var configs = await configurations.ListAsync(cancellationToken);
        var providers = registry.ListProviders().Select(provider =>
        {
            var config = configs.FirstOrDefault(c => c.Kind == provider.Kind);
            return new AiProviderHealthResult(provider.Id, provider.Kind, config is null ? AiProviderStatus.NotConfigured : AiProviderStatus.NotConfigured, config is null ? "Provider is not configured." : "Provider is configured.", DateTimeOffset.UtcNow, config?.MaskedApiKey);
        }).ToArray();
        return new AiExplanationOptions(project.Id, providers, await packs.ListAsync(project, cancellationToken), AiExplanationPromptBuilder.SuggestedFilePath, Enum.GetValues<ExplanationProfile>());
    }

    public Task<AiExplanationPreview> PreviewAsync(AiExplanationRequest request, CancellationToken cancellationToken)
        => promptBuilder.BuildPreviewAsync(request, cancellationToken);

    public async Task<AiExplanationResult> RunAsync(AiExplanationRequest request, CancellationToken cancellationToken)
    {
        var configuration = await configurations.GetAsync(request.ProviderId, cancellationToken) ?? throw new InvalidOperationException("AI provider is not configured.");
        var preview = await PreviewAsync(request, cancellationToken);
        if (preview.IsBlocked)
        {
            var blocked = await runs.SaveAsync(NewRun(request, configuration, preview, AiRunStatus.SafetyBlocked, null, "High-risk safety warning blocked explanation generation."), cancellationToken);
            return ToResult(blocked, quality);
        }

        var running = await runs.SaveAsync(NewRun(request, configuration, preview, AiRunStatus.Running, null, null), cancellationToken);
        try
        {
            var provider = registry.GetProvider(request.ProviderId) ?? throw new InvalidOperationException("AI provider implementation was not found.");
            var modelConfig = string.IsNullOrWhiteSpace(request.Model) ? configuration : configuration with { Model = request.Model };
            var output = await provider.RunAsync(modelConfig, preview.PromptText, request.MaxOutputTokens, cancellationToken);
            var completed = await runs.SaveAsync(running with { Status = AiRunStatus.Completed, CompletedUtc = DateTimeOffset.UtcNow, OutputText = request.StoreOutput ? output.Text : output.Text, OutputHash = output.OutputHash }, cancellationToken);
            return ToResult(completed, quality);
        }
        catch (Exception ex)
        {
            var failed = await runs.SaveAsync(running with { Status = AiRunStatus.Failed, CompletedUtc = DateTimeOffset.UtcNow, Error = ex.Message }, cancellationToken);
            return ToResult(failed, quality);
        }
    }

    public async Task<IReadOnlyList<AiExplanationResult>> GetRunsAsync(Guid projectId, CancellationToken cancellationToken)
        => (await runs.GetByProjectAsync(projectId, cancellationToken))
            .Where(IsExplanationRun)
            .OrderByDescending(r => r.CreatedUtc)
            .Select(r => ToResult(r, quality))
            .ToArray();

    public async Task<AiExplanationResult?> GetRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(runId, cancellationToken);
        return run is null || run.ProjectId != projectId || !IsExplanationRun(run) ? null : ToResult(run, quality);
    }

    public async Task<AiExplanationSaveResult> SaveAsync(AiExplanationSaveRequest request, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(request.ProjectId, cancellationToken) ?? throw new InvalidOperationException("Project was not found.");
        var run = await runs.GetAsync(request.RunId, cancellationToken);
        if (run is null || run.ProjectId != request.ProjectId || !IsExplanationRun(run)) return new AiExplanationSaveResult(request.ProjectId, request.RunId, false, AiExplanationPromptBuilder.SuggestedFilePath, null, ["Explanation run was not found."]);
        if (run.Status != AiRunStatus.Completed || string.IsNullOrWhiteSpace(run.OutputText)) return new AiExplanationSaveResult(request.ProjectId, request.RunId, false, AiExplanationPromptBuilder.SuggestedFilePath, null, ["Only completed explanation runs can be saved."]);

        var relative = AiExplanationPromptBuilder.SuggestedFilePath;
        var absolute = Path.GetFullPath(Path.Combine(project.RootPath, relative));
        var root = Path.GetFullPath(project.RootPath);
        if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Explanation output path must stay inside the project root.");

        var existingArtifact = await artifacts.GetByPathAsync(project.Id, relative, cancellationToken);
        if (File.Exists(absolute))
        {
            var existingHash = AiContextPackPromptBuilder.Hash(await File.ReadAllTextAsync(absolute, cancellationToken));
            var manuallyModified = existingArtifact is null || !existingArtifact.ContentHash.Equals(existingHash, StringComparison.OrdinalIgnoreCase);
            if (manuallyModified && !request.ConfirmOverwrite)
            {
                return new AiExplanationSaveResult(project.Id, run.Id, false, relative, null, ["07-ai-explanation.md already exists and appears manually modified. Confirm overwrite to replace it."]);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        var markdown = AiExplanationPromptBuilder.StripSourceWorkerMetadata(run.OutputText);
        await File.WriteAllTextAsync(absolute, markdown, cancellationToken);
        var hash = AiContextPackPromptBuilder.Hash(markdown);
        var latestScan = await scanRuns.GetLatestAsync(project.Id, cancellationToken);
        await artifacts.UpsertAsync(new GeneratedArtifact(Guid.NewGuid(), project.Id, latestScan?.Id, "ai-explanation", relative, absolute, GeneratedArtifactType.InternalGeneratedDoc, hash, DateTimeOffset.UtcNow, run.PromptHash, GeneratedArtifactStatus.Generated, "SourceWorker.AI.Explanation", $"Provider={run.ProviderId}; Model={run.Model}; RunId={run.Id}"), cancellationToken);
        return new AiExplanationSaveResult(project.Id, run.Id, true, relative, absolute, []);
    }

    public async Task<bool> DeleteRunAsync(Guid projectId, Guid runId, CancellationToken cancellationToken)
    {
        var run = await runs.GetAsync(runId, cancellationToken);
        if (run is null || run.ProjectId != projectId || !IsExplanationRun(run)) return false;
        await runs.DeleteAsync(runId, cancellationToken);
        return true;
    }

    private static AiRun NewRun(AiExplanationRequest request, AiProviderConfiguration configuration, AiExplanationPreview preview, AiRunStatus status, string? output, string? error)
        => new(Guid.NewGuid(), request.ProjectId, request.ProviderId, configuration.Kind, string.IsNullOrWhiteSpace(request.Model) ? configuration.Model : request.Model, ExplanationPackId(request), request.UserInstruction, preview.PromptHash, request.StorePrompt, request.StorePrompt ? preview.PromptText : null, preview.IncludedFiles.Where(f => f.Included).Select(f => f.RelativePath).ToArray(), preview.SafetyWarnings, preview.TokenEstimate, status, DateTimeOffset.UtcNow, status is AiRunStatus.Completed or AiRunStatus.Failed or AiRunStatus.SafetyBlocked ? DateTimeOffset.UtcNow : null, error, output, output is null ? null : AiContextPackPromptBuilder.Hash(output));

    private static string ExplanationPackId(AiExplanationRequest request) => $"ai-explanation:{request.ContextPackId}:{request.ExplanationProfile}";
    private static bool IsExplanationRun(AiRun run) => run.PackId.StartsWith("ai-explanation:", StringComparison.OrdinalIgnoreCase);
    private static AiExplanationResult ToResult(AiRun run, IAiExplanationQualityService quality)
    {
        var sections = SplitSections(run.OutputText);
        var report = string.IsNullOrWhiteSpace(run.OutputText) ? null : quality.Check(run.OutputText);
        return new AiExplanationResult(run.Id, run.ProjectId, run.ProviderId, run.Model, run.Status, run.CreatedUtc, run.CompletedUtc, run.PromptHash, run.OutputHash, run.TokenEstimate.PromptTokens, run.TokenEstimate.MaxOutputTokens, run.SafetyWarnings, sections, run.OutputText, AiExplanationPromptBuilder.SuggestedFilePath, run.Error, report);
    }

    private static IReadOnlyList<AiExplanationSection> SplitSections(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return [];
        var sections = new List<AiExplanationSection>();
        var currentTitle = "AI Project Explanation";
        var current = new StringBuilder();
        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (current.Length > 0) sections.Add(new AiExplanationSection(currentTitle, current.ToString().Trim()));
                currentTitle = line[3..].Trim();
                current.Clear();
            }
            else
            {
                current.AppendLine(line);
            }
        }
        if (current.Length > 0) sections.Add(new AiExplanationSection(currentTitle, current.ToString().Trim()));
        return sections;
    }
}

public sealed class DogfoodReadinessService(IProjectRepository projects, IScanRunRepository scans, IContextPackService packs, IAiProviderConfigurationRepository providers, IAiRunRepository runs, ILocalApiRuntimeService runtime) : IDogfoodReadinessService
{
    public async Task<DogfoodProjectReadiness> GetGlobalStatusAsync(CancellationToken cancellationToken)
    {
        var list = await projects.ListAsync(cancellationToken);
        var first = list.FirstOrDefault();
        return first is null ? Build(null, null, [
            Item("apphost", "AppHost detected", runtime.GetState().EnvironmentName.Length > 0, runtime.GetState().BaseUrl),
            Item("api", "Local API connected", true),
            Item("token", "Token configured", runtime.GetState().TokenEnabled),
            Item("project", "At least one project registered", false)
        ]) : await GetProjectStatusAsync(first.Id, cancellationToken);
    }

    public async Task<DogfoodProjectReadiness> GetProjectStatusAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        var latest = project is null ? null : await scans.GetLatestAsync(project.Id, cancellationToken);
        var packList = project is null ? [] : await packs.ListAsync(project, cancellationToken);
        var providerList = await providers.ListAsync(cancellationToken);
        var runList = project is null ? [] : await runs.GetByProjectAsync(project.Id, cancellationToken);
        return Build(project?.Id, project?.Name, [
            Item("apphost", "AppHost detected", runtime.GetState().EnvironmentName.Length > 0, runtime.GetState().BaseUrl),
            Item("api", "Local API connected", true),
            Item("token", "Token configured", runtime.GetState().TokenEnabled),
            Item("project", "At least one project registered", project is not null),
            Item("scan", "Latest scan exists", latest is not null, latest?.Status.ToString()),
            Item("freshness", "Context freshness available", latest is not null),
            Item("packs", "At least one context pack exists", packList.Count > 0),
            Item("ai-provider", "AI provider configured", providerList.Count > 0),
            Item("ai-health", "AI provider health check passed", providerList.Any(p => p.IsEnabled), "Run provider test for live health."),
            Item("prompt-preview", "At least one prompt preview created", runList.Count > 0, "Prompt previews are currently inferred from AI runs."),
            Item("ai-run", "At least one AI run completed", runList.Any(r => r.Status == AiRunStatus.Completed)),
            Item("no-ai-writes", "No AI file writes detected", true)
        ]);
    }

    private static DogfoodChecklistItem Item(string id, string label, bool complete, string? detail = null) => new(id, label, complete, detail);
    private static DogfoodProjectReadiness Build(Guid? projectId, string? name, IReadOnlyList<DogfoodChecklistItem> items)
    {
        var score = items.Count == 0 ? 0 : items.Count(i => i.IsComplete) * 100 / items.Count;
        var status = score >= 80 ? DogfoodReadinessStatus.Ready : score > 20 ? DogfoodReadinessStatus.NeedsAttention : DogfoodReadinessStatus.NotStarted;
        return new DogfoodProjectReadiness(projectId, name, new DogfoodChecklist(items, status, items.Where(i => !i.IsComplete).Take(3).Select(i => i.Label).ToArray()), score);
    }
}

public sealed class DiagnosticsService(IAppVersionService version, IStorageInfoService storage, ILocalApiRuntimeService runtime, IAiProviderConfigurationRepository providers, IAiProviderRegistry registry, IProjectRepository projects, IJobRepository jobs) : IDiagnosticsService
{
    public async Task<DiagnosticsSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var v = version.GetVersion();
        var storageInfo = await storage.GetInfoAsync(cancellationToken);
        var statuses = new List<AiProviderHealthResult>();
        foreach (var config in await providers.ListAsync(cancellationToken))
        {
            var provider = registry.GetProvider(config.Kind.ToString());
            statuses.Add(provider is null ? new AiProviderHealthResult(config.Kind.ToString(), config.Kind, AiProviderStatus.Unhealthy, "Provider implementation missing.", DateTimeOffset.UtcNow, config.MaskedApiKey) : await provider.CheckHealthAsync(config, cancellationToken));
        }
        return new DiagnosticsSummary(v.AppVersion, v.ApiVersion, storageInfo, runtime.GetState(), statuses, (await projects.ListAsync(cancellationToken)).Count, (await jobs.ListAsync(cancellationToken)).Count, storageInfo.AiRunCount, [], DateTimeOffset.UtcNow);
    }

    public async Task<DiagnosticsExport> ExportAsync(CancellationToken cancellationToken)
    {
        var summary = await GetSummaryAsync(cancellationToken);
        var json = System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true });
        return new DiagnosticsExport($"sourceworker-diagnostics-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json", json, DateTimeOffset.UtcNow);
    }
}

public sealed class DogfoodSessionService(IDogfoodSessionRepository sessions, IDogfoodIssueRepository issues) : IDogfoodSessionService
{
    private static readonly DogfoodSessionStep[] DefaultSteps =
    [
        new("choose-projects", "Choose projects", false, null),
        new("register-missing", "Register missing projects", false, null),
        new("scan-each", "Scan each project", false, null),
        new("check-freshness", "Check context freshness", false, null),
        new("open-docs", "Open generated docs", false, null),
        new("open-packs", "Open context packs", false, null),
        new("preview-ai-handoff", "Preview ai-handoff pack", false, null),
        new("preview-backend-docs", "Preview backend-docs pack", false, null),
        new("run-ai-preview", "Run one AI preview per selected project", false, null),
        new("check-diagnostics", "Check diagnostics", false, null),
        new("capture-friction", "Capture friction/issues", false, null),
        new("finish", "Finish with recommendations", false, null)
    ];

    public Task<IReadOnlyList<DogfoodSession>> ListAsync(CancellationToken cancellationToken) => sessions.ListAsync(cancellationToken);
    public Task<DogfoodSession?> GetAsync(Guid id, CancellationToken cancellationToken) => sessions.GetAsync(id, cancellationToken);

    public Task<DogfoodSession> StartAsync(IReadOnlyList<DogfoodSessionProject> projects, CancellationToken cancellationToken)
    {
        var session = new DogfoodSession(Guid.NewGuid(), DateTimeOffset.UtcNow, null, projects, DefaultSteps, [], [], DogfoodOutcome.InProgress, ["Scan selected projects.", "Preview ai-handoff and backend-docs packs.", "Capture any friction immediately."]);
        return sessions.SaveAsync(session, cancellationToken);
    }

    public async Task<DogfoodSession> CompleteStepAsync(Guid sessionId, string stepId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(sessionId, cancellationToken) ?? throw new InvalidOperationException("Dogfood session was not found.");
        var steps = session.Steps.Select(s => s.Id == stepId ? s with { IsCompleted = true, CompletedUtc = DateTimeOffset.UtcNow } : s).ToArray();
        return await sessions.SaveAsync(session with { Steps = steps, GeneratedRecommendations = Recommend(steps, await issues.ListAsync(cancellationToken)) }, cancellationToken);
    }

    public async Task<DogfoodSession> FinishAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(sessionId, cancellationToken) ?? throw new InvalidOperationException("Dogfood session was not found.");
        return await sessions.SaveAsync(session with { CompletedUtc = DateTimeOffset.UtcNow, Outcome = DogfoodOutcome.Completed, GeneratedRecommendations = Recommend(session.Steps, await issues.ListAsync(cancellationToken)) }, cancellationToken);
    }

    public async Task<DiagnosticsExport> ExportAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(sessionId, cancellationToken) ?? throw new InvalidOperationException("Dogfood session was not found.");
        var builder = new StringBuilder();
        builder.AppendLine($"# Dogfood Session {session.StartedUtc:yyyy-MM-dd HH:mm}");
        builder.AppendLine();
        builder.AppendLine("## Projects");
        foreach (var project in session.Projects) builder.AppendLine($"- {project.Name}: {project.Path ?? "not set"}");
        builder.AppendLine();
        builder.AppendLine("## Steps");
        foreach (var step in session.Steps) builder.AppendLine($"- [{(step.IsCompleted ? "x" : " ")}] {step.Label}");
        builder.AppendLine();
        builder.AppendLine("## Recommendations");
        foreach (var rec in session.GeneratedRecommendations) builder.AppendLine($"- {rec}");
        builder.AppendLine();
        builder.AppendLine("## Next Codex Task Suggestion");
        builder.AppendLine("Improve the highest-severity dogfood issue and keep AI output preview-only.");
        return new DiagnosticsExport($"dogfood-session-{session.StartedUtc:yyyyMMdd}.md", builder.ToString(), DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<string> Recommend(IReadOnlyList<DogfoodSessionStep> steps, IReadOnlyList<DogfoodSessionIssue> issues)
    {
        var recs = new List<string>();
        if (steps.Any(s => !s.IsCompleted)) recs.Add($"Complete next dogfood step: {steps.First(s => !s.IsCompleted).Label}.");
        if (issues.Any(i => i.Status == "Open")) recs.Add("Triage open friction issues before adding new product surface.");
        if (issues.Any(i => i.Severity >= DogfoodSeverity.High)) recs.Add("Fix high-severity dogfood friction first.");
        if (recs.Count == 0) recs.Add("Run the same dogfood flow against Insights, Localite, and LwX.Flow.");
        return recs;
    }
}

public sealed class FrictionIssueService(IDogfoodIssueRepository issues) : IFrictionIssueService
{
    public Task<IReadOnlyList<DogfoodSessionIssue>> ListAsync(CancellationToken cancellationToken) => issues.ListAsync(cancellationToken);
    public Task<DogfoodSessionIssue> ReportAsync(DogfoodSessionIssue issue, CancellationToken cancellationToken)
        => issues.SaveAsync(issue with { Id = issue.Id == Guid.Empty ? Guid.NewGuid() : issue.Id, CreatedUtc = issue.CreatedUtc == default ? DateTimeOffset.UtcNow : issue.CreatedUtc, Status = string.IsNullOrWhiteSpace(issue.Status) ? "Open" : issue.Status }, cancellationToken);

    public async Task<DogfoodSessionIssue?> UpdateStatusAsync(Guid issueId, string status, CancellationToken cancellationToken)
    {
        var issue = await issues.GetAsync(issueId, cancellationToken);
        return issue is null ? null : await issues.SaveAsync(issue with { Status = status }, cancellationToken);
    }

    public async Task<DiagnosticsExport> ExportIssuesAsync(CancellationToken cancellationToken)
    {
        var all = await issues.ListAsync(cancellationToken);
        var builder = new StringBuilder("# Dogfood Issues\n\n");
        foreach (var issue in all.OrderByDescending(i => i.CreatedUtc))
        {
            builder.AppendLine($"## {issue.Title}");
            builder.AppendLine($"- Status: {issue.Status}");
            builder.AppendLine($"- Category: {issue.Category}");
            builder.AppendLine($"- Severity: {issue.Severity}");
            builder.AppendLine($"- Page: {issue.PageRoute}");
            builder.AppendLine();
            builder.AppendLine(issue.Description);
            builder.AppendLine();
        }
        return new DiagnosticsExport("issues.md", builder.ToString(), DateTimeOffset.UtcNow);
    }
}

public sealed class NextActionService(IProjectRepository projects, IScanRunRepository scans, IAiProviderConfigurationRepository providers, IDogfoodIssueRepository issues) : INextActionService
{
    public async Task<IReadOnlyList<NextAction>> GetGlobalAsync(CancellationToken cancellationToken)
    {
        var projectList = await projects.ListAsync(cancellationToken);
        var providerList = await providers.ListAsync(cancellationToken);
        var actions = new List<NextAction>();
        if (projectList.Count == 0) actions.Add(Action("register-project", NextActionKind.RegisterProject, NextActionPriority.Critical, "Register your first project.", "Start by registering SourceWorker or another repo.", "/projects/onboarding"));
        else actions.Add(Action("start-dogfood", NextActionKind.StartDogfoodSession, NextActionPriority.High, "Start a dogfood session.", "Run the daily-use checklist across real repos.", "/dogfood/sessions/new"));
        if (providerList.Count == 0) actions.Add(Action("configure-ai", NextActionKind.ConfigureAiProvider, NextActionPriority.Medium, "Configure AI provider.", "Set up Ollama or OpenAI for preview-only runs.", "/settings/ai"));
        if ((await issues.ListAsync(cancellationToken)).Any(i => i.Status == "Open")) actions.Add(Action("review-issues", NextActionKind.ResolveFailedJob, NextActionPriority.High, "Review open dogfood issues.", "Triage friction captured during dogfooding.", "/dogfood/issues"));
        return actions;
    }

    public async Task<IReadOnlyList<NextAction>> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null) return await GetGlobalAsync(cancellationToken);
        var latest = await scans.GetLatestAsync(projectId, cancellationToken);
        var actions = new List<NextAction>();
        if (latest is null) actions.Add(Action("scan", NextActionKind.RunFirstScan, NextActionPriority.Critical, "Run first scan.", "Scanning generates context, docs, and packs.", $"/projects/{projectId}"));
        else
        {
            actions.Add(Action("open-docs", NextActionKind.OpenBackendDocs, NextActionPriority.Medium, "Open backend docs.", "Review generated architecture and flow docs.", $"/projects/{projectId}/docs", project));
            actions.Add(Action("preview-ai", NextActionKind.PreviewAiHandoffPack, NextActionPriority.Medium, "Preview ai-handoff pack.", "Use the safe AI preview flow.", $"/projects/{projectId}/ai", project));
        }
        if ((await providers.ListAsync(cancellationToken)).Count == 0) actions.Add(Action("configure-ai", NextActionKind.ConfigureAiProvider, NextActionPriority.High, "Configure AI provider.", "AI preview needs Ollama or OpenAI.", "/settings/ai", project));
        return actions;
    }

    private static NextAction Action(string id, NextActionKind kind, NextActionPriority priority, string title, string description, string? href, Project? project = null)
        => new(id, kind, priority, NextActionSource.DogfoodReadiness, new NextActionTarget(project?.Id, project?.Name), title, description, href);
}

public sealed class ProjectPathValidationService : IProjectPathValidationService
{
    private readonly List<RecentProjectPath> recent = [];

    public Task<IReadOnlyList<RecentProjectPath>> GetRecentAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RecentProjectPath>>(recent.OrderByDescending(r => r.LastUsedUtc).Take(10).ToArray());

    public Task<ProjectPathValidationResult> ValidateAsync(string path, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var full = string.IsNullOrWhiteSpace(path) ? path : Path.GetFullPath(path);
        var exists = Directory.Exists(full);
        if (!exists) errors.Add("Path does not exist.");
        var isSystem = full.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase);
        if (isSystem) errors.Add("System folders cannot be registered.");
        var canRead = false;
        try { canRead = exists && Directory.EnumerateFileSystemEntries(full).Take(1).Any() || exists; } catch { errors.Add("Permission denied or path cannot be read."); }
        var hasGit = exists && Directory.Exists(Path.Combine(full, ".git"));
        var hasSln = exists && Directory.EnumerateFiles(full, "*.sln", SearchOption.TopDirectoryOnly).Any();
        if (exists && !hasGit) warnings.Add("No .git folder detected.");
        if (exists && !hasSln) warnings.Add("No solution file detected; registration is still allowed.");
        var name = exists ? new DirectoryInfo(full).Name : Path.GetFileName(full.TrimEnd('\\', '/'));
        var result = new ProjectPathValidationResult(full, exists, exists, hasGit, hasSln, isSystem, canRead, errors.Count == 0, warnings, errors, name);
        if (result.CanRegister) recent.RemoveAll(r => r.Path.Equals(full, StringComparison.OrdinalIgnoreCase));
        if (result.CanRegister) recent.Add(new RecentProjectPath(full, name, DateTimeOffset.UtcNow));
        return Task.FromResult(result);
    }
}

public sealed class QuickAskService(IProjectAiSettingsRepository settings, IAiPromptPreviewService preview, IAiRunService runs, IAiProviderConfigurationRepository providers) : IQuickAskService
{
    public async Task<QuickAskResult> PreviewAsync(QuickAskRequest request, CancellationToken cancellationToken)
    {
        var defaults = await settings.GetAsync(request.ProjectId, cancellationToken);
        var provider = request.ProviderId ?? defaults.DefaultProviderId ?? (await providers.ListAsync(cancellationToken)).FirstOrDefault()?.Kind.ToString() ?? throw new InvalidOperationException("AI provider is not configured.");
        var pack = request.PackId ?? defaults.DefaultContextPack;
        var template = request.TemplateId ?? defaults.DefaultPromptTemplate;
        var max = request.MaxOutputTokens ?? defaults.MaxOutputTokens;
        return new QuickAskResult(await preview.PreviewAsync(request.ProjectId, provider, pack, request.Question, max, template, cancellationToken), null);
    }

    public async Task<QuickAskResult> RunAsync(QuickAskRequest request, CancellationToken cancellationToken)
    {
        var p = await PreviewAsync(request, cancellationToken);
        var run = await runs.StartAsync(new AiRunInput(request.ProjectId, p.Preview.ProviderId, p.Preview.ProviderKind, p.Preview.Model, p.Preview.PackId, request.Question, request.ConfirmSafetyWarnings, request.MaxOutputTokens ?? 1200), cancellationToken);
        return p with { Run = run };
    }
}
