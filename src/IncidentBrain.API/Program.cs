using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;
using IncidentBrain.Infrastructure;
using IncidentBrain.Infrastructure.Persistence;
using IncidentBrain.Infrastructure.Analysis;
using IncidentBrain.Infrastructure.AI;
using IncidentBrain.Infrastructure.Cost;
using IncidentBrain.Infrastructure.Ingestion;
using IncidentBrain.Api;
using IncidentBrain.Api.Services;
using IncidentBrain.Api.HostedServices;
using IncidentBrain.Api.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.Configure<LogProcessorOptions>(builder.Configuration.GetSection(LogProcessorOptions.Section));
builder.Services.Configure<CostMonitorOptions>(builder.Configuration.GetSection("Analysis"));
builder.Services.Configure<SpikeDetectionOptions>(opt =>
{
    var section = builder.Configuration.GetSection(LogProcessorOptions.Section);
    opt.BaselineMultiplier = section.GetValue<double>("SpikeBaselineMultiplier");
    opt.P1Threshold = section.GetValue<double>("SpikeP1Threshold");
    opt.P2Threshold = section.GetValue<double>("SpikeP2Threshold");
    opt.P3Threshold = section.GetValue<double>("SpikeP3Threshold");
});
builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));
builder.Services.Configure<RetentionOptions>(builder.Configuration.GetSection(RetentionOptions.Section));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.Section));

builder.Services.AddHttpClient(OllamaAIService.HttpClientName, (sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
});

builder.Services.AddSingleton<MockAIService>();
builder.Services.AddSingleton<OllamaAIService>();
builder.Services.AddSingleton<IAIProviderState>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IHostEnvironment>();
    var p = config.GetValue<string>("AI:Provider") ?? "Mock";
    if (env.IsProduction())
        p = "Mock";
    else
    {
        if (!string.Equals(p, "Ollama", StringComparison.OrdinalIgnoreCase) && !string.Equals(p, "Mock", StringComparison.OrdinalIgnoreCase))
            p = "Mock";
    }
    return new AIProviderState { Provider = p };
});
builder.Services.AddSingleton<IAIService, SwitchableAIService>();

builder.Services.AddDbContext<AppDbContext>(o =>
{
    o.UseSqlite(builder.Configuration.GetConnectionString("Default"));
    o.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
builder.Services.AddScoped<IIncidentStore, SqliteIncidentStore>();
builder.Services.AddSingleton<ILogAnalyzer, TfIdfLogAnalyzer>();
builder.Services.AddSingleton<SpikeDetectionEngine>();
builder.Services.AddSingleton<ICostMonitor, CostMonitor>();
builder.Services.AddSingleton<BudgetGuard>();
builder.Services.AddSingleton<AnalysisBatchLimiter>();
builder.Services.AddSingleton<FileUploadSource>();
builder.Services.AddSingleton<ILogStreamSimulator, SimulatedStreamSource>();
builder.Services.AddSingleton<IncidentStreamBroadcaster>();
builder.Services.AddSingleton<ISimulationRuntimeState, SimulationRuntimeState>();
builder.Services.AddHostedService<LogProcessorHostedService>();
builder.Services.AddHostedService<RetentionEnforcementHostedService>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (corsOrigins.Length > 0)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader()));
else
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

var isProduction = app.Environment.IsProduction();
if (isProduction)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RateLimitMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Incidents';";
        var hasIncidentsTable = await cmd.ExecuteScalarAsync() is not null;
        if (!hasIncidentsTable)
        {
            await conn.CloseAsync();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }
    }
    await conn.CloseAsync();
}

app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", version = "1.0" }));

app.MapGet("/api/stats", async (DateTime? since, IIncidentStore store, CancellationToken ct) =>
{
    var stats = await store.GetLogStatsAsync(since, ct);
    return Results.Ok(new { totalLogs = stats.TotalLogs, totalErrors = stats.TotalErrors, totalRequests = stats.TotalRequests });
});

app.MapGet("/api/logs/recent", async (int? count, DateTime? since, IIncidentStore store, CancellationToken ct) =>
{
    var limit = Math.Clamp(count ?? 200, 1, 500);
    var logs = await store.GetRecentLogsAsync(null, limit, since, ct);
    return Results.Ok(logs.Select(l => new { l.Id, l.Timestamp, l.Service, l.Level, l.Message }));
});

if (!isProduction)
{
app.MapPost("/api/logs", async (LogEntryRequest body, IIncidentStore store) =>
{
    var service = RequestValidation.Sanitize(body.Service, RequestValidation.MaxServiceLength);
    if (string.IsNullOrEmpty(service)) service = "unknown";
    var level = RequestValidation.Sanitize(body.Level, RequestValidation.MaxLevelLength);
    if (string.IsNullOrEmpty(level)) level = "info";
    var message = RequestValidation.Sanitize(body.Message, RequestValidation.MaxMessageLength);
    var entry = new LogEntry
    {
        Id = Guid.NewGuid().ToString(),
        Timestamp = body.Timestamp ?? DateTime.UtcNow,
        Service = service,
        Level = level,
        Message = message,
        Source = LogSource.Api
    };
    await store.AddLogsAsync(new[] { entry });
    return Results.Created($"/api/logs/{entry.Id}", new { id = entry.Id });
});

app.MapPost("/api/simulation/start", (SimulationStartRequest? body, ILogStreamSimulator simulator, IConfiguration config, ISimulationRuntimeState runtimeState) =>
{
    if (simulator.IsRunning) return Results.Ok(new { status = "already_running" });
    var opts = config.GetSection("Simulation");
    var cfg = new SimulatorConfig
    {
        Seed = body?.Seed ?? opts.GetValue<int?>("DefaultSeed"),
        LogsPerSecond = body?.LogsPerSecond ?? opts.GetValue<int>("LogsPerSecond"),
        LogIntervalMs = body?.LogIntervalMs ?? opts.GetValue<int>("LogIntervalMs"),
        SpikeDelaySeconds = body?.SpikeDelaySeconds ?? opts.GetValue<int>("SpikeDelaySeconds"),
        SpikeMultiplier = body?.SpikeMultiplier ?? opts.GetValue<double>("SpikeMultiplier"),
        SpikeDurationSeconds = body?.SpikeDurationSeconds ?? opts.GetValue<int>("SpikeDurationSeconds"),
        DeploymentEventInjection = body?.DeploymentEventInjection ?? opts.GetValue<bool>("DeploymentEventInjection")
    };
    runtimeState.IncidentSensitivity = body?.IncidentSensitivity;
    simulator.Start(cfg);
    return Results.Ok(new { status = "started" });
});

app.MapPost("/api/simulation/stop", (ILogStreamSimulator simulator, ISimulationRuntimeState runtimeState) =>
{
    simulator.Stop();
    runtimeState.IncidentSensitivity = null;
    return Results.Ok(new { status = "stopped" });
});

app.MapGet("/api/simulation/status", (ILogStreamSimulator simulator) =>
    Results.Ok(new { running = simulator.IsRunning }));

app.MapGet("/api/settings/ai", (IAIProviderState aiState) =>
    Results.Ok(new { provider = aiState.Provider }));

app.MapPatch("/api/settings/ai", async (HttpContext ctx, IAIProviderState aiState) =>
{
    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var body = await ctx.Request.ReadFromJsonAsync<AISettingsUpdateRequest>(options, ctx.RequestAborted);
    if (body?.Provider is not { } p)
        return Results.BadRequest();
    var normalized = p.Trim();
    if (!string.Equals(normalized, "Ollama", StringComparison.OrdinalIgnoreCase) && !string.Equals(normalized, "Mock", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest();
    aiState.Provider = string.Equals(normalized, "Ollama", StringComparison.OrdinalIgnoreCase) ? "Ollama" : "Mock";
    return Results.Ok(new { provider = aiState.Provider });
});

app.MapPost("/api/admin/clear", async (IIncidentStore store, CancellationToken ct) =>
{
    await store.ClearAllAsync(ct);
    return Results.Ok(new { cleared = true });
});

app.MapGet("/api/incidents", async ([AsParameters] IncidentFilterQuery q, IIncidentStore store) =>
{
    var filter = new IncidentFilter();
    if (q.Status.HasValue) filter.Status = (IncidentStatus)q.Status.Value;
    if (q.Severity.HasValue) filter.Severity = (Severity)q.Severity.Value;
    if (q.From.HasValue) filter.From = q.From;
    if (q.To.HasValue) filter.To = q.To;
    var search = RequestValidation.Sanitize(q.Search, RequestValidation.MaxSearchLength);
    if (!string.IsNullOrEmpty(search)) filter.Search = search;
    var service = RequestValidation.Sanitize(q.Service, RequestValidation.MaxServiceLength);
    if (!string.IsNullOrEmpty(service)) filter.Service = service;
    var list = await store.ListAsync(filter);
    return Results.Ok(list);
});

app.MapGet("/api/incidents/{id}", async (string id, IIncidentStore store) =>
{
    var incident = await store.GetByIdAsync(id);
    return incident is null ? Results.NotFound() : Results.Ok(incident);
});

app.MapPatch("/api/incidents/{id}", async (string id, IncidentStatusUpdateRequest body, IIncidentStore store, IncidentStreamBroadcaster broadcaster, CancellationToken ct) =>
{
    var incident = await store.GetByIdAsync(id, ct);
    if (incident is null) return Results.NotFound();
    if (body.Status is < 0 or > 2) return Results.BadRequest();
    incident.Status = (IncidentStatus)body.Status!.Value;
    incident.UpdatedAt = DateTime.UtcNow;
    await store.UpdateIncidentAsync(incident, ct);
    await broadcaster.BroadcastAsync(new { type = "IncidentUpdated", id }, ct);
    return Results.Ok(incident);
});

app.MapPost("/api/incidents/{id}/reanalyze", async (string id, IIncidentStore store, IAIService ai, IConfiguration config, IncidentStreamBroadcaster broadcaster, CancellationToken ct) =>
{
    var incident = await store.GetByIdAsync(id, ct);
    if (incident is null) return Results.NotFound();
    var meta = await store.GetAnalysisMetadataForIncidentAsync(id, ct);
    var cooldownSec = config.GetValue<int>("Analysis:ReanalysisCooldownSeconds");
    if (meta?.CooldownUntil is { } until && until > DateTime.UtcNow)
        return Results.Json(new { error = "Reanalysis cooldown active" }, statusCode: 429);
    var recent = await store.GetRecentLogsAsync(incident.AffectedService, 100, null, ct);
    var patterns = recent.Select(l => l.Message).Distinct().Take(20).ToList();
    incident.Summary = await ai.GenerateIncidentSummaryAsync(incident, patterns, ct);
    incident.SuggestedSteps = await ai.GenerateInvestigationStepsAsync(incident, patterns, ct);
    incident.UpdatedAt = DateTime.UtcNow;
    await store.UpdateIncidentAsync(incident, ct);
    await store.SaveAnalysisMetadataAsync(new AnalysisMetadata
    {
        IncidentId = id,
        ProcessedAt = DateTime.UtcNow,
        CooldownUntil = DateTime.UtcNow.AddSeconds(cooldownSec)
    }, ct);
    await broadcaster.BroadcastAsync(new { type = "IncidentUpdated", id }, ct);
    return Results.Ok(incident);
});
}

app.MapGet("/api/stream/incidents", async (HttpContext ctx, IncidentStreamBroadcaster broadcaster, IConfiguration config) =>
{
    var maxConcurrent = config.GetValue("Stream:MaxConcurrentSSE", 50);
    if (broadcaster.SubscriberCount >= maxConcurrent)
    {
        ctx.Response.StatusCode = 503;
        await ctx.Response.WriteAsJsonAsync(new { error = "Too many SSE connections" });
        return;
    }
    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");
    var stream = ctx.Response.Body;
    var id = broadcaster.Subscribe(stream);
    try
    {
        await Task.Delay(Timeout.Infinite, ctx.RequestAborted);
    }
    catch (OperationCanceledException) { }
    finally
    {
        broadcaster.Unsubscribe(id);
    }
});

app.Run();

// ReSharper disable once ClassNeverInstantiated.Global
public record LogEntryRequest(DateTime? Timestamp, string? Service, string? Level, string? Message);

// ReSharper disable once ClassNeverInstantiated.Global
public record SimulationStartRequest(int? Seed, int? LogsPerSecond, int? LogIntervalMs, int? SpikeDelaySeconds, int? SpikeDurationSeconds, double? SpikeMultiplier, bool? DeploymentEventInjection, string? IncidentSensitivity);

// ReSharper disable once ClassNeverInstantiated.Global
public record IncidentFilterQuery(int? Status, int? Severity, DateTime? From, DateTime? To, string? Search, string? Service);

// ReSharper disable once ClassNeverInstantiated.Global
public record IncidentStatusUpdateRequest(int? Status);

// ReSharper disable once ClassNeverInstantiated.Global
public record AISettingsUpdateRequest(string? Provider);
