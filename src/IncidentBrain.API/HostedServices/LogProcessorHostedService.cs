using IncidentBrain.Api;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;
using IncidentBrain.Core.Analysis;
using IncidentBrain.Infrastructure.Cost;
using IncidentBrain.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IncidentBrain.Api.HostedServices;

public class LogProcessorHostedService : BackgroundService
{
    private readonly ILogStreamSimulator _simulator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogAnalyzer _analyzer;
    private readonly SpikeDetectionEngine _spikeEngine;
    private readonly IAIService _aiService;
    private readonly BudgetGuard _budgetGuard;
    private readonly AnalysisBatchLimiter _limiter;
    private readonly IncidentStreamBroadcaster _broadcaster;
    private readonly LogProcessorOptions _options;
    private readonly ISimulationRuntimeState _runtimeState;
    private readonly RetentionOptions? _retention;
    private readonly ILogger<LogProcessorHostedService> _logger;
    private readonly List<LogEntry> _buffer = new();
    private readonly object _bufferLock = new();
    private DateTime _lastLogFlush = DateTime.UtcNow;
    private DateTime _lastAnalysis = DateTime.UtcNow;
    private const int LogFlushIntervalSeconds = 1;
    private const int AnalysisIntervalSeconds = 10;
    private const int IncidentCreationDelayMs = 3000;
    private const int MaxIncidentsPerAnalysisRun = 4;

    public LogProcessorHostedService(
        ILogStreamSimulator simulator,
        IServiceScopeFactory scopeFactory,
        ILogAnalyzer analyzer,
        SpikeDetectionEngine spikeEngine,
        IAIService aiService,
        BudgetGuard budgetGuard,
        AnalysisBatchLimiter limiter,
        IncidentStreamBroadcaster broadcaster,
        IOptions<LogProcessorOptions> options,
        ISimulationRuntimeState runtimeState,
        IOptions<RetentionOptions>? retention,
        ILogger<LogProcessorHostedService> logger)
    {
        _simulator = simulator;
        _scopeFactory = scopeFactory;
        _analyzer = analyzer;
        _spikeEngine = spikeEngine;
        _aiService = aiService;
        _budgetGuard = budgetGuard;
        _limiter = limiter;
        _broadcaster = broadcaster;
        _options = options.Value;
        _runtimeState = runtimeState;
        _retention = retention?.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var evt in _simulator.GetEventStreamAsync(stoppingToken))
                {
                    if (evt is LogEmittedEvent logEvt)
                    {
                        lock (_bufferLock)
                        {
                            _buffer.Add(logEvt.Log);
                            if (_buffer.Count >= _options.MaxLogsPerBatch)
                                _ = FlushLogsOnlyAsync(stoppingToken);
                            else if (_buffer.Count > 0 && (DateTime.UtcNow - _lastLogFlush).TotalSeconds >= LogFlushIntervalSeconds)
                                _ = FlushLogsOnlyAsync(stoppingToken);
                        }
                        if ((DateTime.UtcNow - _lastAnalysis).TotalSeconds >= AnalysisIntervalSeconds)
                            _ = AnalyzeFromStoreAsync(stoppingToken);
                    }
                    else if (evt is DeploymentEmittedEvent depEvt)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var store = scope.ServiceProvider.GetRequiredService<IIncidentStore>();
                        await store.AddDeploymentEventAsync(depEvt.Deployment, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { /* log */ }

            var now = DateTime.UtcNow;
            bool shouldFlush = false;
            lock (_bufferLock)
            {
                if (_buffer.Count > 0 && (now - _lastLogFlush).TotalSeconds >= LogFlushIntervalSeconds)
                    shouldFlush = true;
            }
            if (shouldFlush)
                await FlushLogsOnlyAsync(stoppingToken);
            if (shouldFlush || (now - _lastAnalysis).TotalSeconds >= AnalysisIntervalSeconds)
                await AnalyzeFromStoreAsync(stoppingToken);

            await Task.Delay(500, stoppingToken);
        }
    }

    private async Task FlushLogsOnlyAsync(CancellationToken ct)
    {
        List<LogEntry> batch;
        lock (_bufferLock)
        {
            batch = _buffer.ToList();
            _buffer.Clear();
            _lastLogFlush = DateTime.UtcNow;
        }
        if (batch.Count == 0) return;
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIncidentStore>();
        var limited = _limiter.LimitBatch(batch);
        await store.AddLogsAsync(limited, ct);
        if (_retention?.MaxLogEntries > 0)
        {
            var total = await store.CountLogsAsync(ct);
            if (total > _retention.MaxLogEntries)
            {
                var toRemove = (int)(total - _retention.MaxLogEntries);
                await store.DeleteOldestLogsAsync(toRemove, ct);
            }
        }
    }

    private async Task AnalyzeFromStoreAsync(CancellationToken ct)
    {
        _lastAnalysis = DateTime.UtcNow;
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IIncidentStore>();
        var since = DateTime.UtcNow.AddMinutes(-10);
        var recent = await store.GetRecentLogsAsync(null, 2000, since, ct);
        if (recent.Count == 0) return;
        if (!_budgetGuard.AllowAnalysisRun(recent.Count, _options.MaxSummaryLength)) return;

        var recentErrors = recent.Where(l => string.Equals(l.Level, "error", StringComparison.OrdinalIgnoreCase)).ToList();
        var clusters = _analyzer.ClusterMessages(recentErrors, _options.ClusterSimilarityThreshold);
        var spikes = _spikeEngine.DetectSpikes(recent);

        var sensitivity = _runtimeState.IncidentSensitivity;
        var effectiveClusterThreshold = IncidentCreationPolicy.EffectiveClusterSizeThreshold(sensitivity, _options.ClusterSizeThreshold);
        var spikesToCreate = IncidentCreationPolicy.SelectSpikes(spikes, sensitivity);

        var toCreate = new List<(Incident Incident, IncidentCluster? Cluster)>();

        foreach (var spike in spikesToCreate)
        {
            toCreate.Add((new Incident
            {
                Id = Guid.NewGuid().ToString(),
                AffectedService = spike.Service,
                StartTime = spike.BucketStart,
                ErrorCount = spike.CurrentCount,
                TopErrorPattern = _analyzer.ExtractTopPattern(recent.Where(l => l.Service == spike.Service).ToList()) ?? "Spike detected",
                Severity = spike.Severity,
                Status = IncidentStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, null));
        }

        foreach (var cluster in clusters.Where(c => c.Count >= effectiveClusterThreshold))
        {
            var existing = await store.ListAsync(new IncidentFilter { From = cluster.FirstSeen.AddMinutes(-5), To = cluster.LastSeen.AddMinutes(5) }, ct);
            if (existing.Any(i => i.ClusterId == cluster.Id)) continue;
            var serviceForCluster = recentErrors.FirstOrDefault(l => l.Message == cluster.RepresentativeMessage)?.Service ?? "unknown";
            toCreate.Add((new Incident
            {
                Id = Guid.NewGuid().ToString(),
                AffectedService = serviceForCluster,
                StartTime = cluster.FirstSeen,
                ErrorCount = cluster.Count,
                TopErrorPattern = cluster.RepresentativeMessage,
                Severity = IncidentCreationPolicy.SeverityForClusterSize(cluster.Count),
                Status = IncidentStatus.Open,
                ClusterId = cluster.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }, cluster));
        }

        var capped = toCreate.Take(MaxIncidentsPerAnalysisRun).ToList();
        if (capped.Count > 0)
            _logger.LogInformation("Clustering: creating {Count} incidents from {Clusters} clusters and {Spikes} spikes, threshold={Threshold}", capped.Count, toCreate.Count(x => x.Cluster != null), toCreate.Count(x => x.Cluster == null), effectiveClusterThreshold);
        foreach (var (incident, cluster) in capped)
        {
            if (cluster != null)
            {
                var clusterId = await store.AddClusterAsync(cluster, ct);
                incident.ClusterId = clusterId;
            }
            if (_simulator.IsRunning)
                incident.Severity = (Severity)Random.Shared.Next(0, 3);
            await EnrichWithAiAndSaveAsync(incident, recent, store, ct);
            await Task.Delay(IncidentCreationDelayMs, ct);
        }

        _budgetGuard.RecordRun(recent.Count, _options.MaxSummaryLength);
    }

    private async Task EnrichWithAiAndSaveAsync(Incident incident, IReadOnlyList<LogEntry> recent, IIncidentStore store, CancellationToken ct)
    {
        if (_retention?.MaxActiveIncidents > 0)
        {
            var active = await store.CountActiveIncidentsAsync(ct);
            while (active >= _retention.MaxActiveIncidents)
            {
                var toRemove = await store.GetOldestResolvedIncidentIdsAsync(active - _retention.MaxActiveIncidents + 1, ct);
                if (toRemove.Count == 0) break;
                await store.DeleteIncidentsByIdsAsync(toRemove, ct);
                active = await store.CountActiveIncidentsAsync(ct);
            }
        }
        var patterns = recent.Select(l => l.Message).Distinct().Take(20).ToList();
        incident.Summary = _limiter.TruncateSummary(await _aiService.GenerateIncidentSummaryAsync(incident, patterns, ct));
        incident.SuggestedSteps = await _aiService.GenerateInvestigationStepsAsync(incident, patterns, ct);
        await store.AddIncidentAsync(incident, ct);
        await _broadcaster.BroadcastAsync(new { type = "IncidentCreated", id = incident.Id }, ct);
    }
}

public class LogProcessorOptions
{
    public const string Section = "Analysis";
    public double ClusterSimilarityThreshold { get; set; } = 0.5;
    public int ClusterSizeThreshold { get; set; } = 10;
    public int MaxLogsPerBatch { get; set; } = 500;
    public int MaxSummaryLength { get; set; } = 500;
}
