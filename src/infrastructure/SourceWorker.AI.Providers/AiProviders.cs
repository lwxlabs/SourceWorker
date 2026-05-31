using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SourceWorker.Application;
using SourceWorker.Domain;

namespace SourceWorker.AI.Providers;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerAiProviders(this IServiceCollection services)
    {
        services.AddSingleton<IAiSecretStore, LocalAiSecretStore>();
        services.AddSingleton<OpenAiProvider>();
        services.AddSingleton<OllamaProvider>();
        services.AddSingleton<OpenAiCompatibleProvider>();
        services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
        return services;
    }
}

public sealed class AiProviderRegistry : IAiProviderRegistry
{
    private readonly IReadOnlyList<IAiProvider> providers;

    public AiProviderRegistry(OpenAiProvider openAi, OllamaProvider ollama, OpenAiCompatibleProvider compatible)
    {
        providers = [openAi, ollama, compatible];
    }

    public IReadOnlyList<IAiProvider> ListProviders() => providers;
    public IAiProvider? GetProvider(string providerId)
        => providers.FirstOrDefault(p => p.Id.Equals(providerId, StringComparison.OrdinalIgnoreCase) || p.Kind.ToString().Equals(providerId, StringComparison.OrdinalIgnoreCase));
}

public sealed class LocalAiSecretStore : IAiSecretStore
{
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceWorker", "secrets", "ai");

    public async Task SaveSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var path = GetPath(secretName);
        var text = Convert.ToBase64String(Encoding.UTF8.GetBytes(secretValue));
        await File.WriteAllTextAsync(path, text, cancellationToken);
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        var path = GetPath(secretName);
        if (!File.Exists(path)) return null;
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(text));
        }
        catch
        {
            return text;
        }
    }

    public string Mask(string? secretValue)
    {
        if (string.IsNullOrWhiteSpace(secretValue)) return "";
        return secretValue.Length <= 8 ? "****" : $"{secretValue[..Math.Min(3, secretValue.Length)]}-...{secretValue[^4..]}";
    }

    private string GetPath(string secretName)
    {
        var safe = string.Concat(secretName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-'));
        return Path.Combine(root, safe + ".secret");
    }
}

public sealed class OpenAiProvider(IAiSecretStore secrets) : OpenAiStyleProvider(secrets)
{
    public override string Id => AiProviderKind.OpenAI.ToString();
    public override AiProviderKind Kind => AiProviderKind.OpenAI;
    protected override string DefaultBaseUrl => "https://api.openai.com/v1";
}

public sealed class OpenAiCompatibleProvider(IAiSecretStore secrets) : OpenAiStyleProvider(secrets)
{
    public override string Id => AiProviderKind.OpenAiCompatible.ToString();
    public override AiProviderKind Kind => AiProviderKind.OpenAiCompatible;
    protected override string DefaultBaseUrl => "http://localhost:1234/v1";
}

public abstract class OpenAiStyleProvider(IAiSecretStore secrets) : IAiProvider
{
    public abstract string Id { get; }
    public abstract AiProviderKind Kind { get; }
    protected abstract string DefaultBaseUrl { get; }

    public async Task<AiProviderHealthResult> CheckHealthAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!configuration.IsEnabled) return new AiProviderHealthResult(Id, Kind, AiProviderStatus.Disabled, "Provider is disabled.", DateTimeOffset.UtcNow, configuration.MaskedApiKey);
        if (!string.IsNullOrWhiteSpace(configuration.ApiKeySecretName) && string.IsNullOrWhiteSpace(await secrets.GetSecretAsync(configuration.ApiKeySecretName, cancellationToken)))
        {
            return new AiProviderHealthResult(Id, Kind, AiProviderStatus.NotConfigured, "API key is missing.", DateTimeOffset.UtcNow, configuration.MaskedApiKey);
        }
        return new AiProviderHealthResult(Id, Kind, AiProviderStatus.Healthy, "Provider configuration is present. Network test runs during request.", DateTimeOffset.UtcNow, configuration.MaskedApiKey);
    }

    public async Task<AiRunOutput> RunAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60), BaseAddress = new Uri((configuration.BaseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/") };
        if (!string.IsNullOrWhiteSpace(configuration.ApiKeySecretName))
        {
            var key = await secrets.GetSecretAsync(configuration.ApiKeySecretName, cancellationToken);
            if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        var request = new
        {
            model = configuration.Model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = maxOutputTokens
        };
        using var response = await client.PostAsJsonAsync("chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var text = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return new AiRunOutput(text, Hash(text));
    }

    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

public sealed class OllamaProvider : IAiProvider
{
    public string Id => AiProviderKind.Ollama.ToString();
    public AiProviderKind Kind => AiProviderKind.Ollama;

    public async Task<AiProviderHealthResult> CheckHealthAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!configuration.IsEnabled) return new AiProviderHealthResult(Id, Kind, AiProviderStatus.Disabled, "Provider is disabled.", DateTimeOffset.UtcNow);
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5), BaseAddress = new Uri((configuration.BaseUrl ?? "http://localhost:11434").TrimEnd('/') + "/") };
            using var response = await client.GetAsync("api/tags", cancellationToken);
            return response.IsSuccessStatusCode
                ? new AiProviderHealthResult(Id, Kind, AiProviderStatus.Healthy, "Ollama responded.", DateTimeOffset.UtcNow)
                : new AiProviderHealthResult(Id, Kind, AiProviderStatus.Unhealthy, $"Ollama returned {(int)response.StatusCode}.", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new AiProviderHealthResult(Id, Kind, AiProviderStatus.Unhealthy, ex.Message, DateTimeOffset.UtcNow);
        }
    }

    public async Task<AiRunOutput> RunAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3), BaseAddress = new Uri((configuration.BaseUrl ?? "http://localhost:11434").TrimEnd('/') + "/") };
        using var response = await client.PostAsJsonAsync("api/generate", new { model = configuration.Model, prompt, stream = false, options = new { num_predict = maxOutputTokens } }, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var text = document.RootElement.TryGetProperty("response", out var value) ? value.GetString() ?? "" : "";
        return new AiRunOutput(text, OpenAiStyleProvider.Hash(text));
    }
}
