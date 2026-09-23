using IncidentBrain.Core.Analysis;
using IncidentBrain.Core.Domain;
using Xunit;

namespace IncidentBrain.Tests;

public class DemoSeedDataTests
{
    [Fact]
    public void BuildExampleIncidents_Returns_Two_Already_Resolved_Incidents()
    {
        var now = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        var incidents = DemoSeedData.BuildExampleIncidents(now);

        Assert.Equal(2, incidents.Count);
        Assert.All(incidents, i => Assert.Equal(IncidentStatus.Resolved, i.Status));
        Assert.All(incidents, i => Assert.NotNull(i.EndTime));
        Assert.All(incidents, i => Assert.True(i.EndTime <= now));
    }

    [Fact]
    public void BuildExampleIncidents_Dates_Stay_Relative_To_Now()
    {
        var earlier = DemoSeedData.BuildExampleIncidents(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var later = DemoSeedData.BuildExampleIncidents(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.NotEqual(earlier[0].StartTime, later[0].StartTime);
    }
}
