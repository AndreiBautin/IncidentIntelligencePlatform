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
}
