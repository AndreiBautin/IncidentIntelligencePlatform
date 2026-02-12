using IncidentBrain.Core.Domain;

namespace IncidentBrain.Core.Interfaces;

public interface ILogStreamSimulator
{
    bool IsRunning { get; }
    void Start(SimulatorConfig config);
    void Stop();
    IAsyncEnumerable<SimulatorEvent> GetEventStreamAsync(CancellationToken cancellationToken = default);
}

public class SimulatorConfig
{
    public int? Seed { get; set; }
    public int LogsPerSecond { get; set; } = 5;
    /// <summary>When set (e.g. 600), emit one log every N ms instead of using LogsPerSecond bursts.</summary>
    public int LogIntervalMs { get; set; }
    /// <summary>Seconds after stream start before the error spike begins. 0 = spike from start.</summary>
    public int SpikeDelaySeconds { get; set; } = 30;
    public double SpikeMultiplier { get; set; } = 3.0;
    public int SpikeDurationSeconds { get; set; } = 60;
    public bool DeploymentEventInjection { get; set; } = true;
}

public abstract class SimulatorEvent { }

public class LogEmittedEvent : SimulatorEvent
{
    public LogEntry Log { get; set; } = null!;
}

public class DeploymentEmittedEvent : SimulatorEvent
{
    public DeploymentEvent Deployment { get; set; } = null!;
}
