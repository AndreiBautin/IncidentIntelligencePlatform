namespace IncidentBrain.Core.Domain;

public class DeploymentEvent
{
    public string Id { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public DateTime DeployedAt { get; set; }
    public string? Version { get; set; }
}
