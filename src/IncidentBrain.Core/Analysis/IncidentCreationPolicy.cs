using IncidentBrain.Core.Domain;

namespace IncidentBrain.Core.Analysis;

/// <summary>
/// Pure rules for turning spikes and clusters into incidents.
/// Lives in Core so the hosted service does not own the policy.
/// </summary>
public static class IncidentCreationPolicy
{
    public static int EffectiveClusterSizeThreshold(string? sensitivity, int configuredThreshold)
    {
        return sensitivity?.Trim().ToLowerInvariant() switch
        {
            "low" => 15,
            "high" => 5,
            _ => configuredThreshold
        };
    }

    public static IReadOnlyList<SpikeResult> SelectSpikes(IReadOnlyList<SpikeResult> spikes, string? sensitivity)
    {
        if (string.Equals(sensitivity?.Trim(), "low", StringComparison.OrdinalIgnoreCase))
            return spikes.Where(s => s.Severity is Severity.P1 or Severity.P2).ToList();
        return spikes;
    }

    public static Severity SeverityForClusterSize(int count)
    {
        if (count >= 10) return Severity.P1;
        if (count >= 5) return Severity.P2;
        return Severity.P3;
    }
}
