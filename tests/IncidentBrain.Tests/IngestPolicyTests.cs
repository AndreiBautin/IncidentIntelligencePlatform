using IncidentBrain.Core.Analysis;
using Xunit;

namespace IncidentBrain.Tests;

public class IngestPolicyTests
{
    [Fact]
    public void Default_services_are_the_DJ_process_names()
    {
        Assert.Contains("dj-api", IngestPolicy.DefaultServices);
        Assert.Contains("dj-worker", IngestPolicy.DefaultServices);
    }

    [Theory]
    [InlineData("dj-api", true)]
    [InlineData("DJ-WORKER", true)]
    [InlineData("api-gateway", false)]
    [InlineData("", false)]
    public void Allowlist_is_case_insensitive(string service, bool allowed)
    {
        Assert.Equal(allowed, IngestPolicy.IsAllowedService(service, IngestPolicy.DefaultServices));
    }
}
