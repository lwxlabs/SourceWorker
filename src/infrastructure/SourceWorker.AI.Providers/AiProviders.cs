using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
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
        services.AddSingleton<GeminiProvider>();
        services.AddSingleton<IAiProviderRegistry, AiProviderRegistry>();
        return services;
    }
}

public sealed class AiProviderRegistry : IAiProviderRegistry
{
    private readonly IReadOnlyList<IAiProvider> providers;

    public AiProviderRegistry(OpenAiProvider openAi, OllamaProvider ollama, OpenAiCompatibleProvider compatible, GeminiProvider gemini)
    {
        providers = [openAi, ollama, compatible, gemini];
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

public abstract class OpenAiStyleProvider(IAiSecretStore secrets) : IAiStreamingProvider
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
        using var client = new HttpClient { Timeout = Kind == AiProviderKind.OpenAiCompatible ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(90), BaseAddress = new Uri((configuration.BaseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/") };
        if (!string.IsNullOrWhiteSpace(configuration.ApiKeySecretName))
        {
            var key = await secrets.GetSecretAsync(configuration.ApiKeySecretName, cancellationToken);
            if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        if (Kind == AiProviderKind.OpenAiCompatible)
        {
            return await RunChatCompletionsAsync(client, configuration.Model, prompt, maxOutputTokens, cancellationToken);
        }

        var responsesRequest = new
        {
            model = configuration.Model,
            instructions = "SourceWorker context request. Do not edit files, run commands, or apply patches.",
            input = prompt,
            max_output_tokens = maxOutputTokens,
            temperature = 0.2,
            store = false
        };
        using var responsesResponse = await client.PostAsJsonAsync("responses", responsesRequest, cancellationToken);
        if (responsesResponse.IsSuccessStatusCode)
        {
            var responseText = ExtractResponsesText(await responsesResponse.Content.ReadAsStringAsync(cancellationToken));
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                return new AiRunOutput(responseText, Hash(responseText));
            }

            if (Kind == AiProviderKind.OpenAI)
            {
                throw new InvalidOperationException("OpenAI returned an empty response.");
            }
        }

        if (Kind == AiProviderKind.OpenAI && responsesResponse.StatusCode is not System.Net.HttpStatusCode.NotFound and not System.Net.HttpStatusCode.BadRequest)
        {
            await ThrowProviderErrorAsync("OpenAI Responses API", responsesResponse, cancellationToken);
        }

        return await RunChatCompletionsAsync(client, configuration.Model, prompt, maxOutputTokens, cancellationToken);
    }

    public async IAsyncEnumerable<AiProviderStreamUpdate> StreamAsync(
        AiProviderConfiguration configuration,
        string prompt,
        int maxOutputTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (Kind != AiProviderKind.OpenAiCompatible)
        {
            var nonStreamingOutput = await RunAsync(configuration, prompt, maxOutputTokens, cancellationToken);
            yield return new AiProviderStreamUpdate(nonStreamingOutput.Text, "Provider completed.", nonStreamingOutput.Text.Length, DateTimeOffset.UtcNow, true);
            yield break;
        }

        if (IsLikelyLmStudio(configuration))
        {
            await foreach (var update in StreamLmStudioNativeAsync(configuration, prompt, maxOutputTokens, cancellationToken))
            {
                yield return update;
            }

            yield break;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15), BaseAddress = new Uri((configuration.BaseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/") };
        if (!string.IsNullOrWhiteSpace(configuration.ApiKeySecretName))
        {
            var key = await secrets.GetSecretAsync(configuration.ApiKeySecretName, cancellationToken);
            if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        var chatRequest = new
        {
            model = configuration.Model,
            messages = new[]
            {
                new { role = "system", content = "SourceWorker context request. Do not edit files, run commands, or apply patches." },
                new { role = "user", content = prompt }
            },
            max_tokens = maxOutputTokens,
            temperature = 0.2,
            stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(chatRequest)
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowProviderErrorAsync("OpenAI-compatible streaming chat completions", response, cancellationToken);
        }

        var output = new StringBuilder();
        var reasoningCharacters = 0;
        string? finishReason = null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;

            var payload = line["data:".Length..].Trim();
            if (payload.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                if (output.Length == 0 && reasoningCharacters > 0)
                {
                    throw new InvalidOperationException(BuildReasoningOnlyError(reasoningCharacters, finishReason));
                }

                yield return new AiProviderStreamUpdate(null, "Provider completed.", output.Length, DateTimeOffset.UtcNow, true, "provider-completed");
                yield break;
            }

            var (delta, reasoning, chunkFinishReason) = ExtractChatChunk(payload);
            finishReason = chunkFinishReason ?? finishReason;
            if (!string.IsNullOrEmpty(delta))
            {
                output.Append(delta);
                yield return new AiProviderStreamUpdate(delta, $"Writing answer... {output.Length} characters received.", output.Length, DateTimeOffset.UtcNow, Phase: "answer-streaming");
                continue;
            }

            if (!string.IsNullOrEmpty(reasoning))
            {
                reasoningCharacters += reasoning.Length;
                yield return new AiProviderStreamUpdate(null, $"Reasoning stream active... {reasoningCharacters} reasoning characters received before final answer text.", output.Length, DateTimeOffset.UtcNow, Phase: "reasoning");
            }
        }

        if (output.Length == 0 && reasoningCharacters > 0)
        {
            throw new InvalidOperationException(BuildReasoningOnlyError(reasoningCharacters, finishReason));
        }

        yield return new AiProviderStreamUpdate(null, "Provider stream ended.", output.Length, DateTimeOffset.UtcNow, true, "provider-completed");
    }

    private async IAsyncEnumerable<AiProviderStreamUpdate> StreamLmStudioNativeAsync(
        AiProviderConfiguration configuration,
        string prompt,
        int maxOutputTokens,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30), BaseAddress = new Uri(NormalizeLmStudioBase(configuration.BaseUrl ?? DefaultBaseUrl)) };
        if (!string.IsNullOrWhiteSpace(configuration.ApiKeySecretName))
        {
            var key = await secrets.GetSecretAsync(configuration.ApiKeySecretName, cancellationToken);
            if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        var nativeRequest = new
        {
            model = configuration.Model,
            input = new[]
            {
                new
                {
                    type = "text",
                    content = "System: SourceWorker context request. Do not edit files, run commands, or apply patches. Return final answer text only; do not return only reasoning.\n\nUser:\n" + prompt
                }
            },
            max_output_tokens = maxOutputTokens,
            temperature = 0.2,
            stream = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/chat")
        {
            Content = JsonContent.Create(nativeRequest)
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowProviderErrorAsync("LM Studio native streaming chat", response, cancellationToken);
        }

        var output = new StringBuilder();
        var reasoningCharacters = 0;
        string? eventName = null;
        string? finishReason = null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line["event:".Length..].Trim();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var payload = line["data:".Length..].Trim();
            if (payload.Equals("[DONE]", StringComparison.OrdinalIgnoreCase)) break;

            if (!TryParseLmStudioEvent(payload, eventName, out var update, out var delta, out var reasoning, out var completed, out var eventFinishReason))
            {
                continue;
            }

            finishReason = eventFinishReason ?? finishReason;
            if (!string.IsNullOrEmpty(delta))
            {
                output.Append(delta);
                yield return new AiProviderStreamUpdate(delta, $"Writing answer... {output.Length} characters received.", output.Length, DateTimeOffset.UtcNow, Phase: "answer-streaming");
                continue;
            }

            if (!string.IsNullOrEmpty(reasoning))
            {
                reasoningCharacters += reasoning.Length;
                yield return new AiProviderStreamUpdate(null, $"Reasoning stream active... {reasoningCharacters} reasoning characters received before final answer text.", output.Length, DateTimeOffset.UtcNow, Phase: "reasoning");
                continue;
            }

            if (update is not null)
            {
                yield return update with { OutputCharacters = output.Length };
            }

            if (completed)
            {
                break;
            }
        }

        if (output.Length == 0 && reasoningCharacters > 0)
        {
            throw new InvalidOperationException(BuildReasoningOnlyError(reasoningCharacters, finishReason));
        }

        yield return new AiProviderStreamUpdate(null, "Provider completed.", output.Length, DateTimeOffset.UtcNow, true, "provider-completed");
    }

    private static bool TryParseLmStudioEvent(
        string json,
        string? eventName,
        out AiProviderStreamUpdate? update,
        out string? delta,
        out string? reasoning,
        out bool completed,
        out string? finishReason)
    {
        update = null;
        delta = null;
        reasoning = null;
        completed = false;
        finishReason = null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : eventName;
            finishReason = root.TryGetProperty("finish_reason", out var finishReasonElement) ? ExtractText(finishReasonElement) : null;

            if (string.Equals(type, "prompt_processing.start", StringComparison.OrdinalIgnoreCase))
            {
                update = new AiProviderStreamUpdate(null, "Prompt processing started.", 0, DateTimeOffset.UtcNow, Phase: "prompt-processing", Progress: 0);
                return true;
            }

            if (string.Equals(type, "prompt_processing.progress", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("progress", out var progressElement))
            {
                var progress = progressElement.GetDouble();
                var normalized = progress > 1 ? progress / 100d : progress;
                update = new AiProviderStreamUpdate(null, $"Prompt processing... {normalized:P1}.", 0, DateTimeOffset.UtcNow, Phase: "prompt-processing", Progress: normalized);
                return true;
            }

            if (string.Equals(type, "prompt_processing.end", StringComparison.OrdinalIgnoreCase))
            {
                update = new AiProviderStreamUpdate(null, "Prompt processing complete. Waiting for model output.", 0, DateTimeOffset.UtcNow, Phase: "prompt-processing", Progress: 1);
                return true;
            }

            if (string.Equals(type, "reasoning.delta", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("content", out var reasoningContent))
            {
                reasoning = ExtractText(reasoningContent);
                return true;
            }

            if ((string.Equals(type, "response.output_text.delta", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "output_text.delta", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "text.delta", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "chat.delta", StringComparison.OrdinalIgnoreCase))
                && root.TryGetProperty("content", out var content))
            {
                delta = ExtractText(content);
                return true;
            }

            if (root.TryGetProperty("delta", out var deltaElement))
            {
                delta = ExtractText(deltaElement);
                return !string.IsNullOrEmpty(delta);
            }

            if (root.TryGetProperty("content", out var rootContent)
                && !string.Equals(type, "reasoning.end", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(type, "reasoning.start", StringComparison.OrdinalIgnoreCase))
            {
                delta = ExtractText(rootContent);
                return !string.IsNullOrEmpty(delta);
            }

            if (string.Equals(type, "chat.end", StringComparison.OrdinalIgnoreCase))
            {
                completed = true;
                update = new AiProviderStreamUpdate(null, "Provider completed.", 0, DateTimeOffset.UtcNow, true, "provider-completed");
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsLikelyLmStudio(AiProviderConfiguration configuration)
    {
        if (!string.Equals(configuration.Name, "LM Studio", StringComparison.OrdinalIgnoreCase)
            && !configuration.Name.Contains("LM Studio", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = configuration.BaseUrl ?? "";
            return baseUrl.Contains(":1234", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static string NormalizeLmStudioBase(string baseUrl)
    {
        var value = baseUrl.TrimEnd('/');
        if (value.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^3];
        }

        return value.TrimEnd('/') + "/";
    }

    private static async Task<AiRunOutput> RunChatCompletionsAsync(HttpClient client, string model, string prompt, int maxOutputTokens, CancellationToken cancellationToken)
    {
        var chatRequest = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "SourceWorker context request. Do not edit files, run commands, or apply patches." },
                new { role = "user", content = prompt }
            },
            max_tokens = maxOutputTokens,
            temperature = 0.2
        };
        using var response = await client.PostAsJsonAsync("chat/completions", chatRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowProviderErrorAsync("OpenAI-compatible chat completions", response, cancellationToken);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var text = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("The AI provider returned no text. For LM Studio, check that the loaded model context is large enough and that the Developer Logs do not show a context-size or channel error.");
        }
        return new AiRunOutput(text, Hash(text));
    }

    private static (string? Content, string? Reasoning, string? FinishReason) ExtractChatChunk(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var choice = document.RootElement.GetProperty("choices")[0];
            var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonElement)
                ? ExtractText(finishReasonElement)
                : null;

            if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var content))
            {
                var contentText = ExtractText(content);
                if (!string.IsNullOrEmpty(contentText))
                {
                    return (contentText, null, finishReason);
                }
            }

            if (choice.TryGetProperty("delta", out delta) && delta.TryGetProperty("reasoning_content", out var reasoningContent))
            {
                return (null, ExtractText(reasoningContent), finishReason);
            }

            if (choice.TryGetProperty("delta", out delta) && delta.TryGetProperty("reasoning", out var reasoning))
            {
                return (null, ExtractText(reasoning), finishReason);
            }

            if (choice.TryGetProperty("delta", out delta) && delta.TryGetProperty("text", out var deltaText))
            {
                return (ExtractText(deltaText), null, finishReason);
            }

            if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var messageContent))
            {
                return (ExtractText(messageContent), null, finishReason);
            }

            if (choice.TryGetProperty("text", out var choiceText))
            {
                return (ExtractText(choiceText), null, finishReason);
            }

            if (document.RootElement.TryGetProperty("text", out var rootText))
            {
                return (ExtractText(rootText), null, finishReason);
            }
        }
        catch
        {
            return (null, null, null);
        }

        return (null, null, null);
    }

    private static string? ExtractText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    builder.Append(item.GetString());
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("text", out var text))
                {
                    builder.Append(ExtractText(text));
                }
                else if (item.TryGetProperty("content", out var content))
                {
                    builder.Append(ExtractText(content));
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        return null;
    }

    private static string BuildReasoningOnlyError(int reasoningCharacters, string? finishReason)
    {
        var finish = string.IsNullOrWhiteSpace(finishReason) ? "unknown" : finishReason;
        return "The model finished without final answer text. "
            + $"LM Studio streamed {reasoningCharacters} reasoning characters, then ended with finish_reason '{finish}'. "
            + "Use a non-thinking model, disable thinking/reasoning in LM Studio for this model if available, reduce the selected context, or increase the model output/context budget.";
    }

    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static string ExtractResponsesText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString() ?? "";
        }

        if (document.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        builder.Append(text.GetString());
                    }
                }
            }
            return builder.ToString();
        }

        return "";
    }

    private static async Task ThrowProviderErrorAsync(string endpointName, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body;
        throw new HttpRequestException($"{endpointName} failed with {(int)response.StatusCode} ({response.StatusCode}). {detail}", null, response.StatusCode);
    }
}

public sealed class GeminiProvider(IAiSecretStore secrets) : IAiProvider
{
    public string Id => AiProviderKind.Gemini.ToString();
    public AiProviderKind Kind => AiProviderKind.Gemini;

    public async Task<AiProviderHealthResult> CheckHealthAsync(AiProviderConfiguration configuration, CancellationToken cancellationToken)
    {
        if (!configuration.IsEnabled) return new AiProviderHealthResult(Id, Kind, AiProviderStatus.Disabled, "Provider is disabled.", DateTimeOffset.UtcNow, configuration.MaskedApiKey);
        if (string.IsNullOrWhiteSpace(configuration.Model)) return new AiProviderHealthResult(Id, Kind, AiProviderStatus.NotConfigured, "Gemini model is missing.", DateTimeOffset.UtcNow, configuration.MaskedApiKey);
        if (configuration.ApiKeySecretName is null || string.IsNullOrWhiteSpace(await secrets.GetSecretAsync(configuration.ApiKeySecretName, cancellationToken)))
        {
            return new AiProviderHealthResult(Id, Kind, AiProviderStatus.NotConfigured, "Gemini API key is missing.", DateTimeOffset.UtcNow, configuration.MaskedApiKey);
        }
        return new AiProviderHealthResult(Id, Kind, AiProviderStatus.Healthy, "Gemini configuration is present. Network test runs during request.", DateTimeOffset.UtcNow, configuration.MaskedApiKey);
    }

    public async Task<AiRunOutput> RunAsync(AiProviderConfiguration configuration, string prompt, int maxOutputTokens, CancellationToken cancellationToken)
    {
        var key = configuration.ApiKeySecretName is null ? null : await secrets.GetSecretAsync(configuration.ApiKeySecretName, cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Gemini API key is missing.");

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90), BaseAddress = new Uri((configuration.BaseUrl ?? "https://generativelanguage.googleapis.com/v1beta").TrimEnd('/') + "/") };
        client.DefaultRequestHeaders.Add("x-goog-api-key", key);
        var request = new
        {
            system_instruction = new { parts = new[] { new { text = "SourceWorker context request. Do not edit files, run commands, or apply patches." } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.2, maxOutputTokens = maxOutputTokens }
        };
        using var response = await client.PostAsJsonAsync($"models/{Uri.EscapeDataString(configuration.Model)}:generateContent", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        var text = ExtractText(responseText);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Gemini returned no candidates or text.");
        return new AiRunOutput(text, OpenAiStyleProvider.Hash(text));
    }

    private static string ExtractText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array) return "";
        var builder = new StringBuilder();
        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts)) continue;
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text)) builder.Append(text.GetString());
            }
        }
        return builder.ToString();
    }
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
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5), BaseAddress = new Uri(NormalizeBase(configuration.BaseUrl)) };
            using var response = await client.GetAsync("tags", cancellationToken);
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
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3), BaseAddress = new Uri(NormalizeBase(configuration.BaseUrl)) };
        using var response = await client.PostAsJsonAsync("chat", new
        {
            model = configuration.Model,
            messages = new[]
            {
                new { role = "system", content = "SourceWorker context request. Do not edit files, run commands, or apply patches." },
                new { role = "user", content = prompt }
            },
            stream = false,
            options = new { num_predict = maxOutputTokens }
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var text = document.RootElement.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content)
            ? content.GetString() ?? ""
            : document.RootElement.TryGetProperty("response", out var value) ? value.GetString() ?? "" : "";
        return new AiRunOutput(text, OpenAiStyleProvider.Hash(text));
    }

    private static string NormalizeBase(string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434/api" : baseUrl.TrimEnd('/');
        return value.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? value + "/" : value + "/api/";
    }
}
