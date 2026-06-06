using LwXLab.Markdown.Options;
using LwXLab.Markdown.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LwXLab.Markdown.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLwXLabMarkdown(this IServiceCollection services, Action<LwMarkdownOptions>? configure = null)
    {
        var options = new LwMarkdownOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<LwMarkdownRenderer>();
        return services;
    }
}
