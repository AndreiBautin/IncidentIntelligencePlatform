namespace IncidentBrain.Core.Domain;

public class Incident
{
    public string Id { get; set; } = string.Empty;
    public string AffectedService { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int ErrorCount { get; set; }
    public string TopErrorPattern { get; set; } = string.Empty;
    public Severity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> SuggestedSteps { get; set; } = Array.Empty<string>();
    public string? ClusterId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? AnalysisMetadataId { get; set; }
}
