namespace IncidentBrain.Core.Domain;

public class AnalysisMetadata
{
    public string Id { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string EngineVersion { get; set; } = "1.0";
    public DateTime ProcessedAt { get; set; }
    public string? ConfigSnapshot { get; set; }
    public DateTime? CooldownUntil { get; set; }
}
