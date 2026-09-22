using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;
using IncidentBrain.Infrastructure;
using IncidentBrain.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IncidentBrain.Tests;

public class SqliteStoreRetentionTests
{
    [Fact]
    public async Task DeleteOldestLogs_keeps_newer_rows()
    {
        await using var harness = await Harness.CreateAsync();
        var older = MakeLog(DateTime.UtcNow.AddHours(-2), "svc", "old");
        var newer = MakeLog(DateTime.UtcNow, "svc", "new");
        await harness.Store.AddLogsAsync(new[] { older, newer });
        Assert.Equal(2, await harness.Store.CountLogsAsync());
        await harness.Store.DeleteOldestLogsAsync(1);
        Assert.Equal(1, await harness.Store.CountLogsAsync());
        var remaining = await harness.Store.GetRecentLogsAsync(count: 10);
        Assert.Single(remaining);
        Assert.Equal("new", remaining[0].Message);
    }

    [Fact]
    public async Task AutoResolveStale_marks_old_open_incidents_resolved()
    {
        await using var harness = await Harness.CreateAsync();
        var stale = MakeIncident(DateTime.UtcNow.AddHours(-3), IncidentStatus.Open);
        var fresh = MakeIncident(DateTime.UtcNow, IncidentStatus.Open);
        await harness.Store.AddIncidentAsync(stale);
        await harness.Store.AddIncidentAsync(fresh);
        var resolved = await harness.Store.AutoResolveStaleAsync(TimeSpan.FromHours(1));
        Assert.Equal(1, resolved);
        Assert.Equal(IncidentStatus.Resolved, (await harness.Store.GetByIdAsync(stale.Id))!.Status);
        Assert.Equal(IncidentStatus.Open, (await harness.Store.GetByIdAsync(fresh.Id))!.Status);
    }

    [Fact]
    public async Task Retention_deletes_oldest_resolved_first()
    {
        await using var harness = await Harness.CreateAsync();
        var oldResolved = MakeIncident(DateTime.UtcNow.AddDays(-2), IncidentStatus.Resolved);
        var newerResolved = MakeIncident(DateTime.UtcNow.AddDays(-1), IncidentStatus.Resolved);
        var open = MakeIncident(DateTime.UtcNow, IncidentStatus.Open);
        await harness.Store.AddIncidentAsync(oldResolved);
        await harness.Store.AddIncidentAsync(newerResolved);
        await harness.Store.AddIncidentAsync(open);
        var ids = await harness.Store.GetOldestResolvedIncidentIdsAsync(1);
        Assert.Equal(oldResolved.Id, Assert.Single(ids));
        await harness.Store.DeleteIncidentsByIdsAsync(ids);
        Assert.Null(await harness.Store.GetByIdAsync(oldResolved.Id));
        Assert.NotNull(await harness.Store.GetByIdAsync(open.Id));
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

    private static Incident MakeIncident(DateTime updated, IncidentStatus status) => new()
    {
        Id = Guid.NewGuid().ToString(),
        AffectedService = "svc",
        StartTime = updated,
        ErrorCount = 1,
        TopErrorPattern = "err",
        Severity = Severity.P3,
        Status = status,
        CreatedAt = updated,
        UpdatedAt = updated
    };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public IIncidentStore Store { get; }

        private Harness(SqliteConnection connection, IIncidentStore store)
        {
            _connection = connection;
            Store = store;
        }

        public static async Task<Harness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new Harness(connection, new SqliteIncidentStore(db));
        }

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }
}
