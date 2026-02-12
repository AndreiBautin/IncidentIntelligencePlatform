namespace IncidentBrain.Core.Domain;

public class LogEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Service { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? RawPayload { get; set; }
    public LogSource Source { get; set; }
    public string? DeploymentId { get; set; }
}
