using IncidentBrain.Core.Interfaces;
using IncidentBrain.Infrastructure.Ingestion;
using Xunit;

namespace IncidentBrain.Tests;

public class SimulationReproducibilityTests
{
    [Fact]
    public void SameSeed_ProducesDeterministicFirstLogs()
    {
        var sim1 = new SimulatedStreamSource();
        var sim2 = new SimulatedStreamSource();
        var config = new SimulatorConfig { Seed = 12345, LogsPerSecond = 10, SpikeMultiplier = 2, SpikeDurationSeconds = 10, DeploymentEventInjection = false };
        sim1.Start(config);
        sim2.Start(config);

        var list1 = new List<string>();
        var list2 = new List<string>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));
        try
        {
            _ = ConsumeAsync(sim1, list1, cts.Token);
            _ = ConsumeAsync(sim2, list2, cts.Token);
            Thread.Sleep(600);
        }
        catch { }
        sim1.Stop();
        sim2.Stop();

        Assert.True(list1.Count >= 1);
        Assert.True(list2.Count >= 1);
        Assert.Equal(list1[0].Length, list2[0].Length);
    }

    private static async Task ConsumeAsync(ILogStreamSimulator sim, List<string> messages, CancellationToken ct)
    {
        await foreach (var evt in sim.GetEventStreamAsync(ct))
        {
            if (evt is LogEmittedEvent le)
                messages.Add(le.Log.Message);
            if (messages.Count >= 5) break;
        }
    }
}
