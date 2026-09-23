using IncidentBrain.Core.Analysis;
using IncidentBrain.Core.Domain;
using Xunit;

namespace IncidentBrain.Tests;

public class IncidentCreationPolicyTests
{
    [Theory]
    [InlineData(null, 10, 10)]
    [InlineData("", 10, 10)]
    [InlineData("medium", 10, 10)]
    [InlineData("low", 10, 15)]
    [InlineData("HIGH", 10, 5)]
    public void Cluster_threshold_follows_sensitivity(string? sensitivity, int configured, int expected)
    {
        Assert.Equal(expected, IncidentCreationPolicy.EffectiveClusterSizeThreshold(sensitivity, configured));
    }

    [Fact]
    public void Low_sensitivity_drops_P3_spikes()
    {
        var spikes = new[]
        {
            new SpikeResult { Service = "a", Severity = Severity.P1 },
            new SpikeResult { Service = "b", Severity = Severity.P3 },
            new SpikeResult { Service = "c", Severity = Severity.P2 }
        };
        var selected = IncidentCreationPolicy.SelectSpikes(spikes, "low");
        Assert.Equal(2, selected.Count);
        Assert.DoesNotContain(selected, s => s.Severity == Severity.P3);
    }

    [Fact]
    public void Default_sensitivity_keeps_every_spike()
    {
        var spikes = new[]
        {
            new SpikeResult { Service = "a", Severity = Severity.P3 }
        };
        Assert.Single(IncidentCreationPolicy.SelectSpikes(spikes, null));
    }

    [Theory]
    [InlineData(10, Severity.P1)]
    [InlineData(5, Severity.P2)]
    [InlineData(4, Severity.P3)]
    public void Cluster_size_maps_to_severity(int count, Severity expected)
    {
        Assert.Equal(expected, IncidentCreationPolicy.SeverityForClusterSize(count));
    }

    /// <summary>
    /// Reproduces a real production case: a busy simulated stream produced far more spikes than the
    /// fixed per-tick creation budget every analysis run, and concatenating spikes before clusters let
    /// that volume alone starve a real, deduplicated ten-message DJ Visualizer cluster out of every
    /// tick for over a minute. A cluster candidate must never sit further back than second regardless
    /// of how many spikes arrived first.
    /// </summary>
    [Fact]
    public void Interleave_puts_a_cluster_candidate_no_further_back_than_second_however_many_spikes_lead()
    {
        var manySpikes = Enumerable.Range(0, 9).Select(i => $"spike{i}").ToList();
        var oneCluster = new List<string> { "cluster0" };

        var interleaved = IncidentCreationPolicy.InterleaveByCreationBudget(manySpikes, oneCluster);

        Assert.Equal("spike0", interleaved[0]);
        Assert.Equal("cluster0", interleaved[1]);
        Assert.Equal(10, interleaved.Count);
    }

    [Fact]
    public void Interleave_keeps_every_candidate_when_neither_side_is_empty()
    {
        var spikes = new List<string> { "s0", "s1" };
        var clusters = new List<string> { "c0", "c1", "c2" };

        var interleaved = IncidentCreationPolicy.InterleaveByCreationBudget(spikes, clusters);

        Assert.Equal(new[] { "s0", "c0", "s1", "c1", "c2" }, interleaved);
    }
}
