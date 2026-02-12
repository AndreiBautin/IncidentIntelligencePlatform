using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;
using IncidentBrain.Infrastructure;
using IncidentBrain.Infrastructure.Analysis;
using IncidentBrain.Infrastructure.AI;
using IncidentBrain.Infrastructure.Cost;
using IncidentBrain.Infrastructure.Persistence;
using Xunit;

namespace IncidentBrain.Tests.Integration;

public class SimulationToIncidentTests
{
    [Fact]
    public async Task InjectedLogs_SpikeDetection_CreatesIncident_WithMockSummary()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        IIncidentStore store = new SqliteIncidentStore(db);
        var analyzer = new TfIdfLogAnalyzer();
        var spikeEngine = new SpikeDetectionEngine(new SpikeDetectionOptions { BaselineMultiplier = 2.0, P2Threshold = 2.0 });
        var aiService = new MockAIService();
        var limiter = new AnalysisBatchLimiter(Options.Create(new CostMonitorOptions { MaxLogsPerBatch = 500, MaxSummaryLength = 500 }));

        var baseTime = new DateTime(2025, 2, 11, 10, 0, 0, DateTimeKind.Utc);
        var logs = new List<LogEntry>();
        for (var i = 0; i < 5; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(i), "api-gateway", "Request OK"));
        for (var i = 0; i < 20; i++)
            logs.Add(MakeLog(baseTime.AddMinutes(5), "api-gateway", "Connection timeout to DB"));
        await store.AddLogsAsync(logs);

        var recent = await store.GetRecentLogsAsync(null, 1000, baseTime.AddMinutes(-10));
        var spikes = spikeEngine.DetectSpikes(recent);
        Assert.True(spikes.Count >= 1);

        var spike = spikes.First();
        var cluster = analyzer.ClusterMessages(recent, 0.5).OrderByDescending(c => c.Count).FirstOrDefault();
        var topPattern = cluster?.RepresentativeMessage ?? "Spike detected";
        var incident = new Incident
        {
            Id = Guid.NewGuid().ToString(),
            AffectedService = spike.Service,
            StartTime = spike.BucketStart,
            ErrorCount = spike.CurrentCount,
            TopErrorPattern = topPattern,
            Severity = spike.Severity,
            Status = IncidentStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var patterns = recent.Select(l => l.Message).Distinct().Take(20).ToList();
        incident.Summary = limiter.TruncateSummary(await aiService.GenerateIncidentSummaryAsync(incident, patterns));
        incident.SuggestedSteps = await aiService.GenerateInvestigationStepsAsync(incident, patterns);
        Assert.False(string.IsNullOrEmpty(incident.Summary));
        Assert.True(incident.SuggestedSteps.Count >= 1);

        var id = await store.AddIncidentAsync(incident);
        var retrieved = await store.GetByIdAsync(id);
        Assert.NotNull(retrieved);
        Assert.Equal(incident.Summary, retrieved.Summary);
        Assert.True(retrieved.SuggestedSteps.Count >= 1);
    }

    private static LogEntry MakeLog(DateTime ts, string service, string message) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Timestamp = ts,
        Service = service,
        Level = "error",
        Message = message,
        Source = LogSource.Simulated
    };
}
