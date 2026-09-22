using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IncidentBrain.Tests;

public class ProductionApiTests : IClassFixture<ProductionApiTests.ProductionFactory>
{
    private readonly ProductionFactory _factory;

    public ProductionApiTests(ProductionFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_and_incident_reads_are_registered()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/incidents")).StatusCode);
    }

    [Fact]
    public async Task Control_routes_are_not_registered()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/admin/clear", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/simulation/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/logs", null)).StatusCode);
    }

    public sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            var db = Path.Combine(Path.GetTempPath(), $"incident-test-{Guid.NewGuid():N}.db");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={db}");
        }
    }
}
