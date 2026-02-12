using IncidentBrain.Infrastructure.Ingestion;
using Xunit;

namespace IncidentBrain.Tests;

public class LogParsingTests
{
    private readonly FileUploadSource _parser = new();

    [Fact]
    public void ParseJsonl_ReturnsLogEntries()
    {
        var content = """
            {"timestamp": "2025-02-11T10:00:01Z", "service": "api-gateway", "level": "error", "message": "Connection timeout"}
            {"timestamp": "2025-02-11T10:00:02Z", "service": "auth-service", "level": "info", "message": "Request OK"}
            """;
        var entries = _parser.Parse(content);
        Assert.Equal(2, entries.Count);
        Assert.Equal("api-gateway", entries[0].Service);
        Assert.Equal("error", entries[0].Level);
        Assert.Equal("Connection timeout", entries[0].Message);
        Assert.Equal("auth-service", entries[1].Service);
    }

    [Fact]
    public void ParseJsonArray_ReturnsLogEntries()
    {
        var content = """
            [
                {"timestamp": "2025-02-11T10:00:01Z", "service": "svc1", "level": "warn", "message": "High latency"},
                {"timestamp": "2025-02-11T10:00:02Z", "service": "svc2", "level": "info", "message": "OK"}
            ]
            """;
        var entries = _parser.Parse(content);
        Assert.Equal(2, entries.Count);
        Assert.Equal("svc1", entries[0].Service);
        Assert.Equal("warn", entries[0].Level);
    }

    [Fact]
    public void ParsePlainText_WithRegex_ReturnsLogEntries()
    {
        var content = "2025-02-11T10:00:01Z api-gateway error Connection timeout to DB\n2025-02-11T10:00:02Z auth-service info Request processed";
        var entries = _parser.Parse(content);
        Assert.True(entries.Count >= 1);
    }
}
