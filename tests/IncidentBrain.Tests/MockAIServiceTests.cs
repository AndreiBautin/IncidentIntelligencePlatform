using IncidentBrain.Core.Domain;
using IncidentBrain.Infrastructure.AI;
using Xunit;

namespace IncidentBrain.Tests;

public class MockAIServiceTests
{
    [Fact]
    public async Task Summary_names_service_pattern_and_count()
    {
        var ai = new MockAIService();
        var incident = new Incident
        {
            AffectedService = "billing-api",
            TopErrorPattern = "timeout talking to payments",
            ErrorCount = 42,
            Severity = Severity.P2,
            Status = IncidentStatus.Open
        };
        var summary = await ai.GenerateIncidentSummaryAsync(incident, new[] { "timeout talking to payments" });
        Assert.Contains("billing-api", summary);
        Assert.Contains("timeout talking to payments", summary);
        Assert.Contains("42", summary);
    }

    [Fact]
    public async Task Steps_are_non_empty_and_mention_the_service()
    {
        var ai = new MockAIService();
        var incident = new Incident { AffectedService = "billing-api", TopErrorPattern = "boom" };
        var steps = await ai.GenerateInvestigationStepsAsync(incident, Array.Empty<string>());
        Assert.True(steps.Count >= 3);
        Assert.Contains(steps, s => s.Contains("billing-api"));
    }
}
