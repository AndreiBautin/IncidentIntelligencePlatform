namespace IncidentBrain.Api;

public class SimulationOptions
{
    public int LogsPerSecond { get; set; } = 5;
    public double SpikeMultiplier { get; set; } = 3.0;
    public int SpikeDurationSeconds { get; set; } = 60;
    public bool DeploymentEventInjection { get; set; } = true;
    public int? DefaultSeed { get; set; } = 42;
}
