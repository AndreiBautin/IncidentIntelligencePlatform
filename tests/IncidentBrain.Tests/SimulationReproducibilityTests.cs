using IncidentBrain.Core.Interfaces;
using IncidentBrain.Infrastructure.Ingestion;
using Xunit;

namespace IncidentBrain.Tests;

public class SimulationReproducibilityTests
{
    [Fact]
    public async Task SameSeed_ProducesDeterministicFirstLogs()
    {
        var sim1 = new SimulatedStreamSource();
        var sim2 = new SimulatedStreamSource();
        var config = new SimulatorConfig
        {
            Seed = 12345,
            LogsPerSecond = 20,
            LogIntervalMs = 0,
            SpikeDelaySeconds = 60,
            SpikeDurationSeconds = 10,
            DeploymentEventInjection = false
        };
        sim1.Start(config);
        sim2.Start(config);
        var list1 = await TakeMessages(sim1, 3);
        var list2 = await TakeMessages(sim2, 3);
        sim1.Stop();
        sim2.Stop();
        Assert.Equal(3, list1.Count);
        Assert.Equal(list1, list2);
    }

    private static async Task<List<string>> TakeMessages(ILogStreamSimulator sim, int count)
    {
        var messages = new List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var evt in sim.GetEventStreamAsync(cts.Token))
        {
            if (evt is LogEmittedEvent le)
                messages.Add(le.Log.Message);
            if (messages.Count >= count)
                break;
        }
        return messages;
    }
}
