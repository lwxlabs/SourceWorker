using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SourceWorker.ApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddSourceWorkerApiClient(this IServiceCollection services, Action<SourceWorkerApiClientOptions>? configure = null)
    {
        if (configure is not null) services.Configure(configure);
        else services.Configure<SourceWorkerApiClientOptions>(_ => { });
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SourceWorkerApiClientOptions>>().Value);

        services.AddHttpClient("SourceWorker", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<SourceWorkerApiClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
            client.Timeout = options.Timeout;
        });
        services.AddScoped<ISourceWorkerApiClient>(provider =>
        {
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("SourceWorker");
            var options = provider.GetRequiredService<IOptions<SourceWorkerApiClientOptions>>().Value;
            return new SourceWorkerApiClient(client, options);
        });
        return services;
    }
}
