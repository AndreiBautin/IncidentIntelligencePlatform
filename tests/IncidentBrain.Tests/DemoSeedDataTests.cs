using IncidentBrain.Core.Analysis;
using IncidentBrain.Core.Domain;
using Xunit;

namespace IncidentBrain.Tests;

public class DemoSeedDataTests
{
    /// <summary>
    /// The dashboard's default filter is Status=Open, so two Resolved incidents would still show
    /// an empty list on the view most visitors land on first. Exactly one of each is what makes
    /// the default view non-empty while also giving a broadened filter some history to show.
    /// </summary>
    [Fact]
    public void BuildExampleIncidents_Returns_One_Open_And_One_Resolved()
    {
        var now = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        var incidents = DemoSeedData.BuildExampleIncidents(now);

        Assert.Equal(2, incidents.Count);
        Assert.Contains(incidents, i => i.Status == IncidentStatus.Open && i.EndTime == null);
        Assert.Contains(incidents, i => i.Status == IncidentStatus.Resolved && i.EndTime != null && i.EndTime <= now);
    }

    [Fact]
    public void BuildExampleIncidents_Dates_Stay_Relative_To_Now()
    {
        var earlier = DemoSeedData.BuildExampleIncidents(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var later = DemoSeedData.BuildExampleIncidents(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.NotEqual(earlier[0].StartTime, later[0].StartTime);
    }
}
