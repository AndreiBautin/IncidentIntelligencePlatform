# Read-only production model

## Goal

In production, the public deployment is a **read-only dashboard**. Users can view incidents, stats, and the live SSE stream, but cannot change state or configuration.

## Disabled in production

- **Control endpoints** are not registered when `ASPNETCORE_ENVIRONMENT=Production`:
  - `POST /api/logs`
  - `POST /api/simulation/start`, `POST /api/simulation/stop`
  - `PATCH /api/settings/ai`
  - `POST /api/admin/clear`
  - `PATCH /api/incidents/{id}`
  - `POST /api/incidents/{id}/reanalyze`
- **Frontend**: When built with `NEXT_PUBLIC_READ_ONLY=true`, the UI hides:
  - Settings link
  - Clear all
  - Mark complete, Reopen, Reanalyze
  (In local dev, the stream also starts automatically when the dashboard is opened; dev UI shows Clear all and Settings.)

## Allowed in production

- `GET /api/health`, `GET /api/stats`, `GET /api/logs/recent`
- `GET /api/incidents`, `GET /api/incidents/{id}`
- `GET /api/stream/incidents` (SSE)
- `GET /api/simulation/status`, `GET /api/settings/ai` (read-only semantics)
- `POST /api/ingest/logs` when `Ingest:ApiKey` is set. Keyed. Not a browser path. See [INGEST.md](INGEST.md).

## Stream behavior

- Log generation starts automatically when the first SSE client connects and stops when the last disconnects. No manual start/stop in the UI in read-only mode.
- Ingested DJ logs sit in the same store. The hosted processor clusters them on the same ten-second tick as simulator logs.

## Summary

Public users get a **live but bounded** experience: they see real-time incidents and charts without any way to inject data, clear data, change thresholds, or switch AI provider. DJ Visualizer can still write failures through the keyed ingest route.
