namespace IncidentBrain.Core.Domain;

public class IncidentCluster
{
    public string Id { get; set; } = string.Empty;
    public string RepresentativeMessage { get; set; } = string.Empty;
    public IReadOnlyList<string> MessageHashes { get; set; } = Array.Empty<string>();
    public int Count { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public double SimilarityThresholdUsed { get; set; }
}
