using SourceWorker.AI;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.Tests.Application;

public sealed class AiTests
{
    [Fact]
    public void Ai_token_estimate_uses_simple_character_budget()
    {
        var estimator = new SimpleAiTokenEstimator();

        var estimate = estimator.Estimate(new string('x', 401), 100);

        Assert.Equal(101, estimate.PromptTokens);
        Assert.Equal(201, estimate.TotalTokens);
    }

    [Fact]
    public void Ai_safety_detects_obvious_api_key_pattern()
    {
        var safety = new AiSafetyService();

        var check = safety.Check("api_key = sk-abcdefghijklmnopqrstuvwxyz");

        Assert.True(check.HasWarnings);
        Assert.Contains(check.Warnings, warning => warning.Code == "possible-secret");
    }

    [Fact]
    public void Ai_redaction_redacts_secret_like_values()
    {
        var redaction = new AiRedactionService(new AiSafetyService());

        var result = redaction.Redact("password=super-secret-value");

        Assert.Contains("[REDACTED]", result.Text);
        Assert.True(result.RedactionCount > 0);
    }

    [Fact]
    public void Ai_cost_estimate_is_unknown_without_model_pricing()
    {
        var estimator = new SimpleAiTokenEstimator();

        var cost = estimator.EstimateCost(new AiTokenEstimate(100, 100, 200), null);

        Assert.Null(cost.TotalCostUsd);
    }

    [Fact]
    public void Ai_model_catalog_known_model_returns_pricing_and_context_window()
    {
        var catalog = new StaticAiModelCatalogService();

        var model = catalog.GetModels(AiProviderKind.OpenAI).First(m => m.Id == "gpt-4.1-mini");
        var cost = new SimpleAiTokenEstimator().EstimateCost(new AiTokenEstimate(1000, 500, 1500), model);

        Assert.NotNull(cost.TotalCostUsd);
        Assert.True(model.ContextWindowTokens > 0);
    }

    [Fact]
    public async Task Ai_provider_validation_detects_missing_openai_key_and_model()
    {
        var service = new AiProviderValidationService(new SecretStore(), new ConfigRepo(), new Registry());

        var result = await service.ValidateAsync(AiProviderKind.OpenAI, "", null, null, 1200, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "missing-api-key");
        Assert.Contains(result.Issues, i => i.Code == "missing-model");
    }

    [Fact]
    public async Task Ai_provider_validation_detects_invalid_url()
    {
        var service = new AiProviderValidationService(new SecretStore(), new ConfigRepo(), new Registry());

        var result = await service.ValidateAsync(AiProviderKind.OpenAiCompatible, "model", "not-a-url", null, 1200, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, i => i.Code == "invalid-base-url");
    }

    [Fact]
    public async Task Ai_secret_store_masks_public_metadata()
    {
        var store = new SecretStore();

        await store.SaveSecretAsync("openai", "sk-super-secret-key-value", CancellationToken.None);

        Assert.Equal("sk--...lue", store.Mask(await store.GetSecretAsync("openai", CancellationToken.None)));
    }

    [Fact]
    public async Task Ai_provider_diagnostic_returns_failure_shape_for_missing_provider()
    {
        var service = new AiProviderValidationService(new SecretStore(), new ConfigRepo(), new Registry());

        var result = await service.DiagnoseAsync("Missing", CancellationToken.None);

        Assert.Equal(AiProviderStatus.NotConfigured, result.Status);
        Assert.NotNull(result.Error);
    }

    private sealed class SecretStore : IAiSecretStore
    {
        private readonly Dictionary<string, string> values = [];
        public Task SaveSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken) { values[secretName] = secretValue; return Task.CompletedTask; }
        public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken) => Task.FromResult(values.TryGetValue(secretName, out var value) ? value : null);
        public string Mask(string? secretValue) => string.IsNullOrWhiteSpace(secretValue) ? "" : $"{secretValue[..Math.Min(3, secretValue.Length)]}-...{secretValue[^3..]}";
    }

    private sealed class ConfigRepo : IAiProviderConfigurationRepository
    {
        public Task<AiProviderConfiguration> SaveAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task<AiProviderConfiguration?> GetAsync(string providerId, CancellationToken cancellationToken) => Task.FromResult<AiProviderConfiguration?>(null);
        public Task<IReadOnlyList<AiProviderConfiguration>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AiProviderConfiguration>>([]);
    }

    private sealed class Registry : IAiProviderRegistry
    {
        public IReadOnlyList<IAiProvider> ListProviders() => [];
        public IAiProvider? GetProvider(string providerId) => null;
    }
}
