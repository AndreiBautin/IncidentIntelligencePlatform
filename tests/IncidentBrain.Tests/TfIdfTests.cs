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

    /// <summary>
    /// A real error message is usually a fixed template plus a per-occurrence id (job id, request id,
    /// trace id). Reproduces a real production case: ten DJ Visualizer worker failures, each embedding
    /// a distinct job GUID, measured at cosine similarity 0.4941 against the default 0.5 threshold
    /// before the id-normalization fix - just under, so they never clustered into one incident despite
    /// being the same failure. They must cluster together.
    /// </summary>
    [Fact]
    public void ClusterMessages_SameTemplateWithDifferentEmbeddedGuids_Groups()
    {
        var analyzer = new TfIdfLogAnalyzer();
        var jobIds = new[]
        {
            "15a751c7-60fb-4412-b7bd-3cf0bc0b21bb",
            "e88ffce4-03dc-4b1f-9c6b-9ece3f66dab7",
            "6c066861-9665-4053-bde5-433d975874f6",
            "67bf3873-d6bd-4645-977f-422bfada4824",
            "84937562-fc5d-4267-9e7c-8073a42ee9ff",
        };
        var logs = jobIds
            .Select((id, i) => MakeLog(i.ToString(), $"job {id} failed: Rendering failed. The uploaded audio or artwork may be corrupt or in an unsupported format."))
            .ToList();

        var clusters = analyzer.ClusterMessages(logs, 0.5);

        Assert.Single(clusters);
        Assert.Equal(jobIds.Length, clusters[0].Count);
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
