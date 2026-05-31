using SourceWorker.AI;
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
}
