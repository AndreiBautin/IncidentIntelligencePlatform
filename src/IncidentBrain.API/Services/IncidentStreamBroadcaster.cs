using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using IncidentBrain.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace IncidentBrain.Api.Services;

public class IncidentStreamBroadcaster
{
    private readonly ConcurrentDictionary<string, Stream> _subscribers = new();
    private readonly ILogStreamSimulator? _simulator;
    private readonly IConfiguration? _config;

    public IncidentStreamBroadcaster(IConfiguration config, ILogStreamSimulator simulator)
    {
        _config = config;
        _simulator = simulator;
    }

    public int SubscriberCount => _subscribers.Count;

    public string Subscribe(Stream responseStream)
    {
        var id = Guid.NewGuid().ToString();
        _subscribers[id] = responseStream;
        if (_subscribers.Count == 1 && _simulator != null && _config != null && !_simulator.IsRunning)
        {
            var opts = _config.GetSection("Simulation");
            var cfg = new SimulatorConfig
            {
                Seed = opts.GetValue<int?>("DefaultSeed"),
                LogsPerSecond = opts.GetValue<int>("LogsPerSecond"),
                LogIntervalMs = opts.GetValue<int>("LogIntervalMs"),
                SpikeDelaySeconds = opts.GetValue<int>("SpikeDelaySeconds"),
                SpikeMultiplier = opts.GetValue<double>("SpikeMultiplier"),
                SpikeDurationSeconds = opts.GetValue<int>("SpikeDurationSeconds"),
                DeploymentEventInjection = opts.GetValue<bool>("DeploymentEventInjection")
            };
            _simulator.Start(cfg);
        }
        return id;
    }

    public void Unsubscribe(string id)
    {
        _subscribers.TryRemove(id, out _);
        if (_subscribers.Count == 0 && _simulator != null && _simulator.IsRunning)
            _simulator.Stop();
    }

    public async Task BroadcastAsync(object evt, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(evt);
        var data = $"data: {json}\n\n";
        var bytes = Encoding.UTF8.GetBytes(data);
        var dead = new List<string>();
        foreach (var (sid, stream) in _subscribers)
        {
            try
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            catch
            {
                dead.Add(sid);
            }
        }
        foreach (var sid in dead)
            _subscribers.TryRemove(sid, out _);
        if (dead.Count > 0 && _subscribers.Count == 0 && _simulator != null && _simulator.IsRunning)
            _simulator.Stop();
    }
}
