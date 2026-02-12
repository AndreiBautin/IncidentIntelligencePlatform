# Log streaming lifecycle

## When the stream runs

- **Production / read-only**: The log stream is tied to SSE subscribers. When the **first** client connects to `/api/stream/incidents`, the simulator starts automatically (using config from `Simulation`). When the **last** client disconnects, the simulator stops. No manual “Start stream” in the UI; opening the dashboard is enough to get live data.
- **Development**: You can still start/stop the stream manually via the dashboard or `POST /api/simulation/start` and `POST /api/simulation/stop`.

## Frequency cap

- Log rate is controlled by `Simulation:LogsPerSecond` and `Simulation:LogIntervalMs`. The simulator emits logs in batches with delays to avoid CPU spikes.
- The processor flushes logs to the store at most every second and runs analysis at a fixed interval (e.g. 10 seconds).

## Max concurrent SSE connections

- The number of concurrent connections to `/api/stream/incidents` is limited by `Stream:MaxConcurrentSSE` (default 50). When the limit is reached, new connections receive **503 Too Many Requests**.

## Summary

| Aspect              | Behavior                                                                 |
|---------------------|--------------------------------------------------------------------------|
| Start               | First SSE subscriber (prod) or manual start (dev)                        |
| Stop                | Last SSE disconnect (prod) or manual stop (dev)                          |
| Rate                | Capped by Simulation config (logs/sec, interval)                         |
| Connection limit    | `Stream:MaxConcurrentSSE` (default 50)                                   |
