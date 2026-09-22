using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;

namespace IncidentBrain.Infrastructure.AI;

public class MockAIService : IAIService
{
    public Task<string> GenerateIncidentSummaryAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default)
    {
        var summary = $"Elevated errors in {incident.AffectedService}: {incident.TopErrorPattern}. " +
                      $"Error count: {incident.ErrorCount}. Severity: {incident.Severity}. Status: {incident.Status}.";
        return Task.FromResult(summary);
    }

    public Task<IReadOnlyList<string>> GenerateInvestigationStepsAsync(Incident incident, IReadOnlyList<string> contextPatterns, CancellationToken cancellationToken = default)
    {
        var steps = new List<string>
        {
            "Check recent deployments for " + incident.AffectedService,
            "Inspect error rate and latency for " + incident.AffectedService,
            "Review logs matching pattern: " + (incident.TopErrorPattern.Length > 50 ? incident.TopErrorPattern[..50] + "..." : incident.TopErrorPattern),
            "Verify dependencies and downstream services",
            "Consider rollback if deployment-related"
        };
        return Task.FromResult<IReadOnlyList<string>>(steps);
    }
}
