using IncidentBrain.Core.Domain;

namespace IncidentBrain.Core.Interfaces;

public interface ILogAnalyzer
{
    IReadOnlyList<IncidentCluster> ClusterMessages(IReadOnlyList<LogEntry> logs, double similarityThreshold = 0.5);
    string? ExtractTopPattern(IReadOnlyList<LogEntry> logs);
}
