using System.Collections.Concurrent;
using System.Net;

namespace IncidentBrain.Api.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _generalPerSecond;
    private readonly int _mutationPerMinute;
    private static readonly ConcurrentDictionary<string, RateLimitState> GeneralBuckets = new();
    private static readonly ConcurrentDictionary<string, RateLimitState> MutationBuckets = new();
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
    private static DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly object CleanupLock = new();

    public RateLimitMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _generalPerSecond = config.GetValue("RateLimit:GeneralPerSecond", 100);
        _mutationPerMinute = config.GetValue("RateLimit:MutationPerMinute", 30);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var key = GetClientKey(context);
        if (string.IsNullOrEmpty(key))
        {
            await _next(context);
            return;
        }

        var isMutation = IsMutationPath(context);
        var bucket = isMutation ? MutationBuckets : GeneralBuckets;
        var limit = isMutation ? _mutationPerMinute : _generalPerSecond;
        var window = isMutation ? TimeSpan.FromMinutes(1) : TimeSpan.FromSeconds(1);

        var now = DateTime.UtcNow;
        MaybeCleanup(bucket, now);

        var state = bucket.AddOrUpdate(key,
            _ => NewState(now, limit, window),
            (_, s) => UpdateState(s, now, limit, window));

        if (!state.Allowed)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"Too many requests"}""");
            return;
        }

        await _next(context);
    }

    private static RateLimitState NewState(DateTime now, int limit, TimeSpan window)
    {
        return new RateLimitState(now, 1, limit, window, true);
    }

    private static RateLimitState UpdateState(RateLimitState s, DateTime now, int limit, TimeSpan window)
    {
        if (now - s.WindowStart > window)
            return new RateLimitState(now, 1, limit, window, true);
        var count = s.Count + 1;
        return new RateLimitState(s.WindowStart, count, limit, window, count <= limit);
    }

    private static string? GetClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
        {
            var first = forwarded.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }
        return context.Connection.RemoteIpAddress?.ToString();
    }

    private static bool IsMutationPath(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        if (path.StartsWith("/api/logs", StringComparison.OrdinalIgnoreCase) && path.Length > 8 && method == "POST") return true;
        if (path.StartsWith("/api/simulation/start", StringComparison.OrdinalIgnoreCase) && method == "POST") return true;
        if (path.StartsWith("/api/simulation/stop", StringComparison.OrdinalIgnoreCase) && method == "POST") return true;
        if (path.StartsWith("/api/settings/ai", StringComparison.OrdinalIgnoreCase) && method == "PATCH") return true;
        if (path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase)) return true;
        if (path.StartsWith("/api/incidents/", StringComparison.OrdinalIgnoreCase) && (path.EndsWith("/reanalyze", StringComparison.OrdinalIgnoreCase) && method == "POST" || method == "PATCH")) return true;
        return false;
    }

    private static void MaybeCleanup(ConcurrentDictionary<string, RateLimitState> bucket, DateTime now)
    {
        if (now - _lastCleanup < CleanupInterval) return;
        lock (CleanupLock)
        {
            if (now - _lastCleanup < CleanupInterval) return;
            _lastCleanup = now;
            var expired = bucket.Where(kv => now - kv.Value.WindowStart > TimeSpan.FromMinutes(5)).Select(kv => kv.Key).ToList();
            foreach (var k in expired)
                bucket.TryRemove(k, out _);
        }
    }

    private record RateLimitState(DateTime WindowStart, int Count, int Limit, TimeSpan Window, bool Allowed);
}
