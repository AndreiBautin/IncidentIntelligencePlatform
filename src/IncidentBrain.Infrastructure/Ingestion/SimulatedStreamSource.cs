using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using IncidentBrain.Core.Domain;
using IncidentBrain.Core.Interfaces;

namespace IncidentBrain.Infrastructure.Ingestion;

public class SimulatedStreamSource : ILogStreamSimulator
{
    private readonly ConcurrentQueue<SimulatorEvent> _queue = new();
    private volatile bool _running;
    private CancellationTokenSource? _cts;
    private Task? _producerTask;
    private static readonly string[] Services =
    {
        "api-gateway", "auth-service", "payment-service",
        "order-service", "inventory-service", "notifications", "search-index", "cdn-edge"
    };
    private static readonly string[] ErrorFirstLines =
    {
        "System.Data.SqlClient.SqlException: Connection timeout to DB",
        "System.Data.SqlClient.SqlException: Connection timeout to database",
        "System.InvalidOperationException: Database connection failed",
        "System.Net.Http.HttpRequestException: Request failed with status 500",
        "System.OutOfMemoryException: Out of memory exception",
        "System.TimeoutException: The operation has timed out",
        "System.IO.IOException: Unable to read data from the transport connection"
    };
    private static readonly string[] StackMethods =
    {
        "MyApp.Data.DbConnection.Open()",
        "MyApp.Data.OrderRepository.GetByIdAsync(Int32 id)",
        "MyApp.Services.OrderService.ProcessOrderAsync(Order order)",
        "MyApp.Api.Controllers.OrdersController.Get(Int32 id)",
        "MyApp.Infrastructure.Cache.GetOrAddAsync(String key, Func`1 factory)",
        "MyApp.Messaging.Publisher.PublishAsync(Message msg)",
        "ExternalLib.HttpClient.SendAsync(HttpRequestMessage request)"
    };
    private static readonly string[] StackFiles =
    {
        "C:\\src\\Data\\DbConnection.cs",
        "C:\\src\\Services\\OrderService.cs",
        "D:\\repos\\Api\\Controllers\\OrdersController.cs"
    };
    private static readonly string[] InfoMessages =
    {
        "Request GET /api/orders completed in 12ms",
        "Request POST /api/payments completed in 45ms",
        "Health check passed",
        "Cache hit for key orders:123",
        "User session created",
        "Request processed",
        "GET /api/products/42 200 8ms",
        "Connection pool checkout",
        "Deployment health check OK",
        "Metric batch sent (24 points)"
    };
    private static readonly string[] WarnMessages =
    {
        "Slow query: 2.3s for SELECT * FROM orders",
        "Retry attempt 2/3 for payment-service",
        "Circuit breaker open for auth-service",
        "Cache miss for key user:456",
        "Request GET /api/search took 1.2s (threshold 500ms)",
        "Deprecated API used: /v1/legacy"
    };

    public bool IsRunning => _running;

    public void Start(SimulatorConfig config)
    {
        if (_running) return;
        _cts = new CancellationTokenSource();
        _running = true;
        _producerTask = Task.Run(() => ProduceAsync(config, _cts.Token));
    }

    public void Stop()
    {
        _running = false;
        _cts?.Cancel();
        _producerTask = null;
    }

    public async IAsyncEnumerable<SimulatorEvent> GetEventStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (_running || _queue.Count > 0)
        {
            if (_queue.TryDequeue(out var evt))
                yield return evt;
            else
                await Task.Delay(50, cancellationToken);
        }
    }

    private async Task ProduceAsync(SimulatorConfig config, CancellationToken ct)
    {
        var random = config.Seed.HasValue ? new Random(config.Seed.Value) : new Random();
        var startTime = DateTime.UtcNow;
        var spikeStart = startTime.AddSeconds(config.SpikeDelaySeconds);
        var spikeEnd = spikeStart.AddSeconds(config.SpikeDurationSeconds);
        var deploymentInjected = false;
        var intervalMs = config.LogIntervalMs > 0 ? config.LogIntervalMs : 0;

        while (!ct.IsCancellationRequested && _running)
        {
            var now = DateTime.UtcNow;

            if (intervalMs > 0)
            {
                var inSpike = now >= spikeStart && now < spikeEnd;
                var log = GenerateLog(random, now, inSpike);
                _queue.Enqueue(new LogEmittedEvent { Log = log });
                if (config.DeploymentEventInjection && !deploymentInjected && now > spikeStart.AddSeconds(-10))
                {
                    var svc = Services[random.Next(Services.Length)];
                    _queue.Enqueue(new DeploymentEmittedEvent
                    {
                        Deployment = new DeploymentEvent
                        {
                            Id = Guid.NewGuid().ToString(),
                            Service = svc,
                            DeployedAt = now,
                            Version = "v2." + random.Next(1, 5)
                        }
                    });
                    deploymentInjected = true;
                }
                await Task.Delay(intervalMs, ct);
                continue;
            }

            var rate = now >= spikeStart && now < spikeEnd
                ? (int)(config.LogsPerSecond * config.SpikeMultiplier)
                : config.LogsPerSecond;
            rate = Math.Max(1, rate);

            for (var i = 0; i < rate && !ct.IsCancellationRequested; i++)
            {
                var inSpike = now >= spikeStart && now < spikeEnd;
                var log = GenerateLog(random, now, inSpike);
                _queue.Enqueue(new LogEmittedEvent { Log = log });
            }

            if (config.DeploymentEventInjection && !deploymentInjected && now > spikeStart.AddSeconds(-10))
            {
                var svc = Services[random.Next(Services.Length)];
                _queue.Enqueue(new DeploymentEmittedEvent
                {
                    Deployment = new DeploymentEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        Service = svc,
                        DeployedAt = now,
                        Version = "v2." + random.Next(1, 5)
                    }
                });
                deploymentInjected = true;
            }

            await Task.Delay(1000 / Math.Max(1, rate), ct);
        }
    }

    private static string GenerateErrorMessage(Random random)
    {
        var first = ErrorFirstLines[random.Next(ErrorFirstLines.Length)];
        var sb = new StringBuilder(first);
        var frameCount = 2 + random.Next(4);
        for (var i = 0; i < frameCount; i++)
        {
            var method = StackMethods[random.Next(StackMethods.Length)];
            var file = StackFiles[random.Next(StackFiles.Length)];
            var line = random.Next(1, 120);
            sb.Append("\n   at ").Append(method).Append(" in ").Append(file).Append(":line ").Append(line);
        }
        return sb.ToString();
    }

    private static LogEntry GenerateLog(Random random, DateTime now, bool inSpike)
    {
        var service = Services[random.Next(Services.Length)];
        string level;
        string message;
        if (inSpike)
        {
            var roll = random.Next(100);
            level = roll < 80 ? "error" : "warn";
            message = level == "error" ? GenerateErrorMessage(random) : WarnMessages[random.Next(WarnMessages.Length)];
        }
        else
        {
            var roll = random.Next(100);
            if (roll < 70) { level = "info"; message = InfoMessages[random.Next(InfoMessages.Length)]; }
            else if (roll < 90) { level = "warn"; message = WarnMessages[random.Next(WarnMessages.Length)]; }
            else { level = "error"; message = GenerateErrorMessage(random); }
        }
        return new LogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = now,
            Service = service,
            Level = level,
            Message = message,
            Source = LogSource.Simulated
        };
    }
}
