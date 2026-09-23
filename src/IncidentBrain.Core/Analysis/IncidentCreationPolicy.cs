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

    /// <summary>
    /// Orders spike-derived and cluster-derived incident candidates for the fixed per-tick creation
    /// budget. A simulated (or simply noisy) source can produce more spikes in one analysis run than
    /// the budget holds, and concatenating spikes-then-clusters lets that volume alone decide the
    /// order: every slot goes to a spike before a cluster - which needs ten times the evidence a P3
    /// spike does - is ever considered, tick after tick, for as long as the noise keeps up. Confirmed
    /// live: a real ten-message DJ Visualizer cluster went uncreated for over a minute of analysis
    /// ticks against a busy simulated stream. Interleaving means a genuine, deduplicated cluster is at
    /// worst the second candidate considered, regardless of how many spikes arrived the same tick.
    /// </summary>
    public static IReadOnlyList<T> InterleaveByCreationBudget<T>(IReadOnlyList<T> spikeCandidates, IReadOnlyList<T> clusterCandidates)
    {
        var interleaved = new List<T>(spikeCandidates.Count + clusterCandidates.Count);
        var max = Math.Max(spikeCandidates.Count, clusterCandidates.Count);
        for (var i = 0; i < max; i++)
        {
            if (i < spikeCandidates.Count) interleaved.Add(spikeCandidates[i]);
            if (i < clusterCandidates.Count) interleaved.Add(clusterCandidates[i]);
        }
        return interleaved;
    }
}
