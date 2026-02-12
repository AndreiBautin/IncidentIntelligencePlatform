using IncidentBrain.Core.Domain;

namespace IncidentBrain.Core.Interfaces;

public interface IIncidentStore
{
    Task<Incident?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Incident>> ListAsync(IncidentFilter? filter = null, CancellationToken cancellationToken = default);
    Task<string> AddIncidentAsync(Incident incident, CancellationToken cancellationToken = default);
    Task UpdateIncidentAsync(Incident incident, CancellationToken cancellationToken = default);
    Task AddLogsAsync(IEnumerable<LogEntry> logs, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LogEntry>> GetRecentLogsAsync(string? service = null, int count = 1000, DateTime? since = null, CancellationToken cancellationToken = default);
    Task<LogStats> GetLogStatsAsync(DateTime? since = null, CancellationToken cancellationToken = default);
    Task<string> AddClusterAsync(IncidentCluster cluster, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidentCluster>> GetClustersAsync(DateTime? since = null, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    Task AddDeploymentEventAsync(DeploymentEvent deploymentEvent, CancellationToken cancellationToken = default);
    Task SaveAnalysisMetadataAsync(AnalysisMetadata metadata, CancellationToken cancellationToken = default);
    Task<AnalysisMetadata?> GetAnalysisMetadataForIncidentAsync(string incidentId, CancellationToken cancellationToken = default);
    Task<int> CountActiveIncidentsAsync(CancellationToken cancellationToken = default);
    Task<int> CountResolvedIncidentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetOldestResolvedIncidentIdsAsync(int take, CancellationToken cancellationToken = default);
    Task DeleteIncidentsByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    Task<long> CountLogsAsync(CancellationToken cancellationToken = default);
    Task DeleteOldestLogsAsync(int count, CancellationToken cancellationToken = default);
    Task<int> AutoResolveStaleAsync(TimeSpan idleThreshold, CancellationToken cancellationToken = default);
}

public class IncidentFilter
{
    public IncidentStatus? Status { get; set; }
    public Severity? Severity { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public string? Service { get; set; }
}

public class LogStats
{
    public long TotalLogs { get; set; }
    public long TotalErrors { get; set; }
    public long TotalRequests { get; set; }
}
