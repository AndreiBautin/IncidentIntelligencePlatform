using IncidentBrain.Core.Domain;
using IncidentBrain.Infrastructure.Analysis;
using Xunit;

namespace IncidentBrain.Tests;

public class TfIdfTests
{
    [Fact]
    public void ClusterMessages_SimilarMessages_Grouped()
    {
        var analyzer = new TfIdfLogAnalyzer();
        var logs = new List<LogEntry>
        {
            MakeLog("1", "Connection timeout to DB"),
            MakeLog("2", "Connection timeout to DB"),
            MakeLog("3", "Connection timeout to database"),
            MakeLog("4", "Something completely different happened here"),
        };
        var clusters = analyzer.ClusterMessages(logs, 0.4);
        Assert.True(clusters.Count >= 1);
        var bigCluster = clusters.OrderByDescending(c => c.Count).First();
        Assert.True(bigCluster.Count >= 2);
        Assert.Contains("timeout", bigCluster.RepresentativeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CosineSimilarity_HighForSimilarText()
    {
        var analyzer = new TfIdfLogAnalyzer();
        var logs = new List<LogEntry>
        {
            MakeLog("1", "Connection timeout to DB"),
            MakeLog("2", "Connection timeout to DB"),
        };
        var clusters = analyzer.ClusterMessages(logs, 0.5);
        Assert.Single(clusters);
        Assert.Equal(2, clusters[0].Count);
    }

    private static LogEntry MakeLog(string id, string message) => new()
    {
        Id = id,
        Timestamp = DateTime.UtcNow,
        Service = "test",
        Level = "error",
        Message = message,
        Source = LogSource.Api
    };
}
