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
        services.AddSingleton<AppDataSecretStore>();
        services.AddSingleton<ISecretStore>(sp => sp.GetRequiredService<AppDataSecretStore>());
        services.AddSingleton<ISecretStoreProvider>(sp => sp.GetRequiredService<AppDataSecretStore>());
        services.AddSingleton<ISecretStoreHealthService>(sp => sp.GetRequiredService<AppDataSecretStore>());
        services.AddSingleton<ISecretMetadataService>(sp => sp.GetRequiredService<AppDataSecretStore>());
        services.AddSingleton<IAiSecretStore>(sp => sp.GetRequiredService<AppDataSecretStore>());
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

public sealed class AppDataSecretStore : IAiSecretStore, ISecretStore, ISecretStoreProvider, ISecretStoreHealthService, ISecretMetadataService
{
    private readonly string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SourceWorker", "secrets", "ai");
    public string StorageKind => "AppData MVP";
    public ISecretStore Store => this;

    public Task SaveSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken) => SaveAsync(secretName, secretValue, cancellationToken);
    public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken) => GetAsync(secretName, cancellationToken);

    public async Task SaveAsync(string name, string value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var path = GetPath(name);
        var text = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        await File.WriteAllTextAsync(path, text, cancellationToken);
    }

    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken)
    {
        var path = GetPath(name);
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

    public Task ClearAsync(string name, CancellationToken cancellationToken)
    {
        var path = GetPath(name);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<SecretStoreHealth> CheckAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        return Task.FromResult(new SecretStoreHealth(StorageKind, Directory.Exists(root), "AppData secret store is available. OS credential store hardening is planned."));
    }

    public async Task<SecretMetadata> GetMetadataAsync(string name, CancellationToken cancellationToken)
    {
        var path = GetPath(name);
        var exists = File.Exists(path);
        var value = exists ? await GetAsync(name, cancellationToken) : null;
        return new SecretMetadata(name, exists, exists ? File.GetCreationTimeUtc(path) : null, exists ? File.GetLastWriteTimeUtc(path) : null, Mask(value), StorageKind);
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

public sealed class NoopSecretStore : IAiSecretStore, ISecretStore
{
    private readonly Dictionary<string, string> values = [];
    public Task SaveSecretAsync(string secretName, string secretValue, CancellationToken cancellationToken) => SaveAsync(secretName, secretValue, cancellationToken);
    public Task<string?> GetSecretAsync(string secretName, CancellationToken cancellationToken) => GetAsync(secretName, cancellationToken);
    public string Mask(string? secretValue) => string.IsNullOrWhiteSpace(secretValue) ? "" : "****";
    public Task SaveAsync(string name, string value, CancellationToken cancellationToken) { values[name] = value; return Task.CompletedTask; }
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken) => Task.FromResult(values.TryGetValue(name, out var value) ? value : null);
    public Task ClearAsync(string name, CancellationToken cancellationToken) { values.Remove(name); return Task.CompletedTask; }
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
