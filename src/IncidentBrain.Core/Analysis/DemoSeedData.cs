namespace IncidentBrain.Core.Analysis;

/// <summary>
/// Two illustrative, already-resolved incidents for a store that has never held anything. The
/// simulated stream only starts once a dashboard connects, and this store has no persistent disk,
/// so a first-time visitor can otherwise land on a genuinely empty dashboard for the several
/// seconds (or, right after a redeploy, indefinitely) it takes the stream and the analysis tick to
/// produce something real. Resolved rather than Open, so they read as history rather than an
/// outage nobody is fixing, and the idle-auto-resolve hosted service has nothing to do with them.
/// Dates are relative to <paramref name="now"/> so the seed never reads as stale.
/// </summary>
public static class DemoSeedData
{
    public static IReadOnlyList<Domain.Incident> BuildExampleIncidents(DateTime now)
    {
        return new List<Domain.Incident>
        {
            new()
            {
                Id = "example-payment-timeout",
                AffectedService = "payment-service",
                StartTime = now.AddHours(-6),
                EndTime = now.AddHours(-6).AddMinutes(9),
                ErrorCount = 14,
                TopErrorPattern = "System.Data.SqlClient.SqlException: Connection timeout to database",
                Severity = Domain.Severity.P1,
                Status = Domain.IncidentStatus.Resolved,
                Summary = "Elevated errors in payment-service: System.Data.SqlClient.SqlException: Connection timeout to database. Error count: 14. Severity: P1. Status: Resolved.",
                SuggestedSteps = new[]
                {
                    "Check recent deployments for payment-service",
                    "Inspect error rate and latency for payment-service",
                    "Review logs matching pattern: System.Data.SqlClient.SqlException: Connection tim...",
                    "Verify dependencies and downstream services",
                    "Consider rollback if deployment-related",
                },
                CreatedAt = now.AddHours(-6).AddMinutes(1),
                UpdatedAt = now.AddHours(-6).AddMinutes(9),
            },
            new()
            {
                Id = "example-cdn-edge-ioexception",
                AffectedService = "cdn-edge",
                StartTime = now.AddHours(-2).AddMinutes(-15),
                EndTime = now.AddHours(-2),
                ErrorCount = 6,
                TopErrorPattern = "System.IO.IOException: Unable to read data from the transport connection",
                Severity = Domain.Severity.P2,
                Status = Domain.IncidentStatus.Resolved,
                Summary = "Elevated errors in cdn-edge: System.IO.IOException: Unable to read data from the transport connection. Error count: 6. Severity: P2. Status: Resolved.",
                SuggestedSteps = new[]
                {
                    "Check recent deployments for cdn-edge",
                    "Inspect error rate and latency for cdn-edge",
                    "Review logs matching pattern: System.IO.IOException: Unable to read data fr...",
                    "Verify dependencies and downstream services",
                },
                CreatedAt = now.AddHours(-2).AddMinutes(-14),
                UpdatedAt = now.AddHours(-2),
            },
        };
    }
}
