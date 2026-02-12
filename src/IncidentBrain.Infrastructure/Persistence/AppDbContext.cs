using Microsoft.EntityFrameworkCore;
using IncidentBrain.Core.Domain;

namespace IncidentBrain.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<LogEntry> LogEntries => Set<LogEntry>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentCluster> IncidentClusters => Set<IncidentCluster>();
    public DbSet<DeploymentEvent> DeploymentEvents => Set<DeploymentEvent>();
    public DbSet<AnalysisMetadata> AnalysisMetadata => Set<AnalysisMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Service);
        });

        modelBuilder.Entity<Incident>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Severity);
            e.HasIndex(x => x.ClusterId);
            e.HasIndex(x => x.StartTime);
            e.Property(x => x.SuggestedSteps).HasConversion(
                v => JsonHelper.Serialize(v),
                v => JsonHelper.DeserializeList(v));
        });

        modelBuilder.Entity<IncidentCluster>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MessageHashes).HasConversion(
                v => JsonHelper.Serialize(v),
                v => JsonHelper.DeserializeList(v));
        });

        modelBuilder.Entity<DeploymentEvent>(e =>
        {
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<AnalysisMetadata>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IncidentId);
        });
    }
}

internal static class JsonHelper
{
    public static string Serialize(IReadOnlyList<string> list) => System.Text.Json.JsonSerializer.Serialize(list);
    public static List<string> DeserializeList(string json)
    {
        var o = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
        return o ?? new List<string>();
    }
}
