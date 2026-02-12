namespace IncidentBrain.Api;

/// <summary>
/// Runtime state for the current simulation run (e.g. incident sensitivity).
/// Set when simulation starts, cleared when it stops.
/// </summary>
public interface ISimulationRuntimeState
{
    string? IncidentSensitivity { get; set; }
}

public class SimulationRuntimeState : ISimulationRuntimeState
{
    public string? IncidentSensitivity { get; set; }
}
