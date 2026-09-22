using System.Security.Cryptography;
using System.Text;
using IncidentBrain.Core.Analysis;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;

namespace IncidentBrain.Api;

public static class IngestEndpoints
{
    public const string KeyHeader = "X-Ingest-Key";

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/ingest/logs", HandleAsync);
    }

    private static async Task<IResult> HandleAsync(
        HttpContext ctx,
        IngestBatchRequest? body,
        IIncidentStore store,
        IConfiguration config,
        CancellationToken ct)
    {
        var configured = config["Ingest:ApiKey"];
        if (string.IsNullOrWhiteSpace(configured))
            return Results.Json(new { error = "Ingest is disabled" }, statusCode: StatusCodes.Status503ServiceUnavailable);

        if (!ctx.Request.Headers.TryGetValue(KeyHeader, out var provided) || !KeysEqual(configured, provided.ToString()))
            return Results.Unauthorized();

        if (body?.Logs is null || body.Logs.Count == 0)
            return Results.BadRequest(new { error = "logs required" });

        var allowed = config.GetSection("Ingest:AllowedServices").Get<string[]>() ?? IngestPolicy.DefaultServices;
        var accepted = new List<LogEntry>();
        foreach (var item in body.Logs.Take(IngestPolicy.MaxBatch))
        {
            var service = RequestValidation.Sanitize(item.Service, RequestValidation.MaxServiceLength);
            if (!IngestPolicy.IsAllowedService(service, allowed))
                continue;
            var level = RequestValidation.Sanitize(item.Level, RequestValidation.MaxLevelLength);
            if (string.IsNullOrEmpty(level)) level = "info";
            var message = RequestValidation.Sanitize(item.Message, RequestValidation.MaxMessageLength);
            if (string.IsNullOrEmpty(message))
                continue;
            accepted.Add(new LogEntry
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = item.Timestamp ?? DateTime.UtcNow,
                Service = service,
                Level = level,
                Message = message,
                Source = LogSource.Ingest
            });
        }

        if (accepted.Count == 0)
            return Results.BadRequest(new { error = "no allowed services in batch" });

        await store.AddLogsAsync(accepted, ct);
        return Results.Json(new { accepted = accepted.Count }, statusCode: StatusCodes.Status202Accepted);
    }

    private static bool KeysEqual(string configured, string provided)
    {
        var left = Encoding.UTF8.GetBytes(configured);
        var right = Encoding.UTF8.GetBytes(provided);
        if (left.Length != right.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public sealed class IngestBatchRequest
{
    public List<IngestLogItem>? Logs { get; set; }
}

public sealed class IngestLogItem
{
    public DateTime? Timestamp { get; set; }
    public string? Service { get; set; }
    public string? Level { get; set; }
    public string? Message { get; set; }
}
