# Log streaming lifecycle

## When the stream runs

- **Production / read-only**: The log stream is tied to SSE subscribers. When the **first** client connects to `/api/stream/incidents`, the simulator starts automatically (using config from `Simulation`). When the **last** client disconnects, the simulator stops. No Start/Stop button in the UI; opening the dashboard is enough to get live data.
- **Development**: Opening the dashboard starts the stream automatically (the frontend calls `POST /api/simulation/start` on load). Same retention bounds as production. **Clear all** is available to reset; no Start/Stop button.

## Frequency cap

- Log rate is controlled by `Simulation:LogsPerSecond` and `Simulation:LogIntervalMs`. The simulator emits logs in batches with delays to avoid CPU spikes.
- The processor flushes logs to the store at most every second and runs analysis at a fixed interval (e.g. 10 seconds).

## Max concurrent SSE connections

- The number of concurrent connections to `/api/stream/incidents` is limited by `Stream:MaxConcurrentSSE` (default 50). When the limit is reached, new connections receive **503 Too Many Requests**.

## Summary

| Aspect              | Behavior                                                                 |
|---------------------|--------------------------------------------------------------------------|
| Start               | First SSE subscriber (prod) or auto on dashboard load (dev)              |
| Stop                | Last SSE disconnect (prod); dev has no Stop button, use Clear all to reset |
| Rate                | Capped by Simulation config (logs/sec, interval)                         |
| Connection limit    | `Stream:MaxConcurrentSSE` (default 50)                                   |
