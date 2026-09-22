using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Analysis;
using Xunit;

namespace IncidentBrain.Tests;

public class SpikeDetectionTests
{
    [Fact]
    public void DetectSpikes_WhenRateAboveBaseline_ReturnsSpike()
    {
        var options = new SpikeDetectionOptions { BaselineMultiplier = 2.0, P1Threshold = 5.0, P2Threshold = 2.0 };
        var engine = new SpikeDetectionEngine(options);
        var baseTime = new DateTime(2025, 2, 11, 10, 0, 0, DateTimeKind.Utc);
        var logs = new List<LogEntry>();
        for (var i = 0; i < 5; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(i), "api-gateway"));
        for (var i = 0; i < 15; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(5), "api-gateway"));
        var spikes = engine.DetectSpikes(logs);
        Assert.True(spikes.Count >= 1);
        Assert.Contains(spikes, s => s.Service == "api-gateway" && s.Ratio >= 2.0);
    }

    [Fact]
    public void DetectSpikes_empty_returns_empty()
    {
        var engine = new SpikeDetectionEngine();
        Assert.Empty(engine.DetectSpikes(Array.Empty<LogEntry>()));
    }

    [Fact]
    public void DetectSpikes_single_bucket_is_not_a_spike()
    {
        var engine = new SpikeDetectionEngine();
        var ts = new DateTime(2025, 2, 11, 10, 0, 0, DateTimeKind.Utc);
        var logs = Enumerable.Range(0, 20).Select(_ => MakeLog(ts, "api-gateway")).ToList();
        Assert.Empty(engine.DetectSpikes(logs));
    }

    [Fact]
    public void DetectSpikes_does_not_bleed_across_services()
    {
        var options = new SpikeDetectionOptions { BaselineMultiplier = 2.0, P1Threshold = 5.0, P2Threshold = 2.0 };
        var engine = new SpikeDetectionEngine(options);
        var baseTime = new DateTime(2025, 2, 11, 10, 0, 0, DateTimeKind.Utc);
        var logs = new List<LogEntry>();
        for (var i = 0; i < 5; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(i), "quiet-service"));
        for (var i = 0; i < 5; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(i), "noisy-service"));
        for (var i = 0; i < 20; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(5), "noisy-service"));
        var spikes = engine.DetectSpikes(logs);
        Assert.DoesNotContain(spikes, s => s.Service == "quiet-service");
        Assert.Contains(spikes, s => s.Service == "noisy-service");
    }

    [Fact]
    public void Severity_HighRatio_ReturnsP1()
    {
        var options = new SpikeDetectionOptions { BaselineMultiplier = 1.5, P1Threshold = 5.0, P2Threshold = 2.0, P3Threshold = 1.5 };
        var engine = new SpikeDetectionEngine(options);
        var baseTime = new DateTime(2025, 2, 11, 10, 0, 0, DateTimeKind.Utc);
        var logs = new List<LogEntry>();
        for (var i = 0; i < 5; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(i), "svc"));
        for (var i = 0; i < 40; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(5), "svc"));
        var spikes = engine.DetectSpikes(logs);
        Assert.Contains(spikes, s => s.Severity == Severity.P1);
    }

    private static LogEntry MakeLog(DateTime ts, string service) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Timestamp = ts,
        Service = service,
        Level = "error",
        Message = "error",
        Source = LogSource.Simulated
    };
}
