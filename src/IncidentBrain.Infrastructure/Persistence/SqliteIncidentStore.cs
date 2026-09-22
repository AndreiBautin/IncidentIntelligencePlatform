using Microsoft.EntityFrameworkCore;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;
using IncidentBrain.Infrastructure.Persistence;

namespace IncidentBrain.Infrastructure;

public class SqliteIncidentStore : IIncidentStore
{
    private readonly AppDbContext _db;

    public SqliteIncidentStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Incident?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Incident>> ListAsync(IncidentFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var q = _db.Incidents.AsNoTracking();
        if (filter != null)
        {
            if (filter.Status.HasValue) q = q.Where(x => x.Status == filter.Status.Value);
            if (filter.Severity.HasValue) q = q.Where(x => x.Severity == filter.Severity.Value);
            if (filter.From.HasValue) q = q.Where(x => x.StartTime >= filter.From.Value);
            if (filter.To.HasValue) q = q.Where(x => x.StartTime <= filter.To.Value);
            if (!string.IsNullOrWhiteSpace(filter.Service)) q = q.Where(x => x.AffectedService.Contains(filter.Service!));
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var term = filter.Search.Trim();
                q = q.Where(x => (x.AffectedService != null && x.AffectedService.Contains(term)) || (x.TopErrorPattern != null && x.TopErrorPattern.Contains(term)) || (x.Summary != null && x.Summary.Contains(term)));
            }
        }
        return await q.OrderByDescending(x => x.StartTime).ToListAsync(cancellationToken);
    }

    public async Task<string> AddIncidentAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(incident.Id) ? Guid.NewGuid().ToString() : incident.Id;
        var entity = new Incident
        {
            Id = id,
            AffectedService = incident.AffectedService,
            StartTime = incident.StartTime,
            EndTime = incident.EndTime,
            ErrorCount = incident.ErrorCount,
            TopErrorPattern = incident.TopErrorPattern,
            Severity = incident.Severity,
            Status = incident.Status,
            Summary = incident.Summary,
            SuggestedSteps = incident.SuggestedSteps.ToList(),
            ClusterId = incident.ClusterId,
            CreatedAt = incident.CreatedAt,
            UpdatedAt = incident.UpdatedAt,
            AnalysisMetadataId = incident.AnalysisMetadataId
        };
        _db.Incidents.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task UpdateIncidentAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Incidents.FindAsync(new object[] { incident.Id }, cancellationToken);
        if (existing == null) return;
        existing.AffectedService = incident.AffectedService;
        existing.EndTime = incident.EndTime;
        existing.ErrorCount = incident.ErrorCount;
        existing.TopErrorPattern = incident.TopErrorPattern;
        existing.Severity = incident.Severity;
        existing.Status = incident.Status;
        existing.Summary = incident.Summary;
        existing.SuggestedSteps = incident.SuggestedSteps.ToList();
        existing.UpdatedAt = incident.UpdatedAt;
        existing.AnalysisMetadataId = incident.AnalysisMetadataId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AddLogsAsync(IEnumerable<LogEntry> logs, CancellationToken cancellationToken = default)
    {
        var list = logs.Select(l => new LogEntry
        {
            Id = string.IsNullOrEmpty(l.Id) ? Guid.NewGuid().ToString() : l.Id,
            Timestamp = l.Timestamp,
            Service = l.Service,
            Level = l.Level,
            Message = l.Message,
            RawPayload = l.RawPayload,
            Source = l.Source,
            DeploymentId = l.DeploymentId
        }).ToList();
        await _db.LogEntries.AddRangeAsync(list, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LogEntry>> GetRecentLogsAsync(string? service = null, int count = 1000, DateTime? since = null, CancellationToken cancellationToken = default)
    {
        IQueryable<LogEntry> q = _db.LogEntries.AsNoTracking();
        if (!string.IsNullOrEmpty(service)) q = q.Where(x => x.Service == service);
        if (since.HasValue) q = q.Where(x => x.Timestamp >= since.Value);
        return await q.OrderByDescending(x => x.Timestamp).Take(count).ToListAsync(cancellationToken);
    }

    public async Task<LogStats> GetLogStatsAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var q = _db.LogEntries.AsNoTracking();
        if (since.HasValue) q = q.Where(x => x.Timestamp >= since.Value);
        var totalLogs = await q.CountAsync(cancellationToken);
        var totalErrors = await q.Where(x => string.Equals(x.Level, "error", StringComparison.OrdinalIgnoreCase)).CountAsync(cancellationToken);
        var totalRequests = await q.Where(x => string.Equals(x.Level, "info", StringComparison.OrdinalIgnoreCase)).CountAsync(cancellationToken);
        return new LogStats { TotalLogs = totalLogs, TotalErrors = totalErrors, TotalRequests = totalRequests };
    }

    public async Task<string> AddClusterAsync(IncidentCluster cluster, CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(cluster.Id) ? Guid.NewGuid().ToString() : cluster.Id;
        var existing = await _db.IncidentClusters.FindAsync(new object[] { id }, cancellationToken);
        if (existing != null)
            return id;
        var entity = new IncidentCluster
        {
            Id = id,
            RepresentativeMessage = cluster.RepresentativeMessage,
            MessageHashes = cluster.MessageHashes.ToList(),
            Count = cluster.Count,
            FirstSeen = cluster.FirstSeen,
            LastSeen = cluster.LastSeen,
            SimilarityThresholdUsed = cluster.SimilarityThresholdUsed
        };
        _db.IncidentClusters.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task<IReadOnlyList<IncidentCluster>> GetClustersAsync(DateTime? since = null, CancellationToken cancellationToken = default)
    {
        var q = _db.IncidentClusters.AsNoTracking();
        if (since.HasValue) q = q.Where(x => x.LastSeen >= since.Value);
        return await q.ToListAsync(cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _db.AnalysisMetadata.ExecuteDeleteAsync(cancellationToken);
        await _db.Incidents.ExecuteDeleteAsync(cancellationToken);
        await _db.IncidentClusters.ExecuteDeleteAsync(cancellationToken);
        await _db.LogEntries.ExecuteDeleteAsync(cancellationToken);
    }

    public async Task AddDeploymentEventAsync(DeploymentEvent deploymentEvent, CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(deploymentEvent.Id) ? Guid.NewGuid().ToString() : deploymentEvent.Id;
        _db.DeploymentEvents.Add(new DeploymentEvent
        {
            Id = id,
            Service = deploymentEvent.Service,
            DeployedAt = deploymentEvent.DeployedAt,
            Version = deploymentEvent.Version
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAnalysisMetadataAsync(AnalysisMetadata metadata, CancellationToken cancellationToken = default)
    {
        var id = string.IsNullOrEmpty(metadata.Id) ? Guid.NewGuid().ToString() : metadata.Id;
        var entity = new AnalysisMetadata
        {
            Id = id,
            IncidentId = metadata.IncidentId,
            EngineVersion = metadata.EngineVersion,
            ProcessedAt = metadata.ProcessedAt,
            ConfigSnapshot = metadata.ConfigSnapshot,
            CooldownUntil = metadata.CooldownUntil
        };
        _db.AnalysisMetadata.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnalysisMetadata?> GetAnalysisMetadataForIncidentAsync(string incidentId, CancellationToken cancellationToken = default)
    {
        return await _db.AnalysisMetadata
            .AsNoTracking()
            .Where(x => x.IncidentId == incidentId)
            .OrderByDescending(x => x.ProcessedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountActiveIncidentsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Incidents.CountAsync(x => x.Status != IncidentStatus.Resolved, cancellationToken);
    }

    public async Task<int> CountResolvedIncidentsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Incidents.CountAsync(x => x.Status == IncidentStatus.Resolved, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetOldestResolvedIncidentIdsAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _db.Incidents
            .AsNoTracking()
            .Where(x => x.Status == IncidentStatus.Resolved)
            .OrderBy(x => x.EndTime ?? x.UpdatedAt)
            .Take(take)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteIncidentsByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return;
        await _db.AnalysisMetadata.Where(x => idList.Contains(x.IncidentId)).ExecuteDeleteAsync(cancellationToken);
        await _db.Incidents.Where(x => idList.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<long> CountLogsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.LogEntries.CountAsync(cancellationToken);
    }

    public async Task DeleteOldestLogsAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return;
        var toDelete = await _db.LogEntries.OrderBy(x => x.Timestamp).Take(count).Select(x => x.Id).ToListAsync(cancellationToken);
        if (toDelete.Count == 0) return;
        await _db.LogEntries.Where(x => toDelete.Contains(x.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> AutoResolveStaleAsync(TimeSpan idleThreshold, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - idleThreshold;
        var stale = await _db.Incidents
            .Where(x => x.Status != IncidentStatus.Resolved && x.UpdatedAt < cutoff)
            .ToListAsync(cancellationToken);
        var resolved = 0;
        foreach (var inc in stale)
        {
            inc.Status = IncidentStatus.Resolved;
            inc.EndTime = DateTime.UtcNow;
            inc.UpdatedAt = DateTime.UtcNow;
            resolved++;
        }
        if (stale.Count > 0)
            await _db.SaveChangesAsync(cancellationToken);
        return resolved;
    }
}
