using System.Net;
using System.Net.Http.Json;
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

    /// <summary>
    /// A fresh store (first boot, or a redeploy on a host with no persistent disk) otherwise leaves
    /// a first-time visitor looking at an empty dashboard until the simulated stream and an analysis
    /// tick produce something real. This factory always starts from a brand-new temp SQLite file, so
    /// it exercises exactly that "never seen anything yet" path.
    /// </summary>
    [Fact]
    public async Task A_brand_new_store_is_seeded_with_example_incidents()
    {
        var client = _factory.CreateClient();
        var incidents = await client.GetFromJsonAsync<List<IncidentSummary>>("/api/incidents");
        Assert.NotNull(incidents);
        Assert.Equal(2, incidents!.Count);
        Assert.All(incidents, i => Assert.Equal("Resolved", i.Status));
    }

    private sealed record IncidentSummary(string Status);

    [Fact]
    public async Task Control_routes_are_not_registered()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/admin/clear", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/simulation/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/logs", null)).StatusCode);
    }

    [Fact]
    public async Task Ingest_without_key_is_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/ingest/logs", new { logs = new[] { new { service = "dj-api", level = "info", message = "job queued" } } });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Ingest_with_key_accepts_dj_service()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Key", "test-ingest-key");
        var res = await client.PostAsJsonAsync("/api/ingest/logs", new { logs = new[] { new { service = "dj-worker", level = "error", message = "job abc failed: rendering failed" } } });
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
    }

    [Fact]
    public async Task Ingest_rejects_services_outside_the_allowlist()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Key", "test-ingest-key");
        var res = await client.PostAsJsonAsync("/api/ingest/logs", new { logs = new[] { new { service = "api-gateway", level = "error", message = "nope" } } });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    public sealed class ProductionFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            var db = Path.Combine(Path.GetTempPath(), $"incident-test-{Guid.NewGuid():N}.db");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={db}");
            builder.UseSetting("Ingest:ApiKey", "test-ingest-key");
        }
    }
}

public class IngestDisabledTests : IClassFixture<IngestDisabledTests.DisabledFactory>
{
    private readonly DisabledFactory _factory;
    public IngestDisabledTests(DisabledFactory factory) => _factory = factory;

    [Fact]
    public async Task Ingest_is_disabled_when_no_key_is_configured()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ingest-Key", "anything");
        var res = await client.PostAsJsonAsync("/api/ingest/logs", new { logs = new[] { new { service = "dj-api", level = "info", message = "x" } } });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    public sealed class DisabledFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            var db = Path.Combine(Path.GetTempPath(), $"incident-test-{Guid.NewGuid():N}.db");
            builder.UseSetting("ConnectionStrings:Default", $"Data Source={db}");
            builder.UseSetting("Ingest:ApiKey", "");
        }
    }
}
