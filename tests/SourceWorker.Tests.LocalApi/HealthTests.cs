using Microsoft.AspNetCore.Mvc.Testing;

namespace SourceWorker.Tests.LocalApi;

public sealed class HealthTests
{
    [Fact]
    public async Task Health_returns_ok()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}
