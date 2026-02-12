using IncidentBrain.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace IncidentBrain.Api.HostedServices;

public class RetentionEnforcementHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionEnforcementHostedService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);

    public RetentionEnforcementHostedService(IServiceScopeFactory scopeFactory, IOptions<RetentionOptions> options, ILogger<RetentionEnforcementHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.MaxTotalIncidents <= 0 && _options.AutoResolveIdleMinutes <= 0)
            return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IIncidentStore>();

                if (_options.AutoResolveIdleMinutes > 0)
                {
                    var resolved = await store.AutoResolveStaleAsync(TimeSpan.FromMinutes(_options.AutoResolveIdleMinutes), stoppingToken);
                    if (resolved > 0)
                        _logger.LogInformation("Auto-resolved {Count} stale incidents", resolved);
                }

                if (_options.MaxTotalIncidents > 0)
                {
                    var active = await store.CountActiveIncidentsAsync(stoppingToken);
                    var resolved = await store.CountResolvedIncidentsAsync(stoppingToken);
                    var total = active + resolved;
                    if (total > _options.MaxTotalIncidents)
                    {
                        var toRemove = total - _options.MaxTotalIncidents;
                        var ids = await store.GetOldestResolvedIncidentIdsAsync(toRemove, stoppingToken);
                        if (ids.Count > 0)
                        {
                            await store.DeleteIncidentsByIdsAsync(ids, stoppingToken);
                            _logger.LogInformation("Retention: removed {Count} oldest resolved incidents", ids.Count);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retention enforcement run failed");
            }
        }
    }
}
