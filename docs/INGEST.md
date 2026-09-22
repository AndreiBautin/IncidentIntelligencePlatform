# Keyed ingest (DJ Visualizer)

Production is read-only for browsers. The one write that stays registered is `POST /api/ingest/logs`.

DJ Visualizer posts job failures here. The hosted processor clusters them the same way it clusters simulator logs, so a burst of failed renders becomes an incident on the dashboard.

## Contract

```
POST /api/ingest/logs
X-Ingest-Key: <shared secret>
Content-Type: application/json

{
  "logs": [
    {
      "timestamp": "2026-09-22T04:00:00Z",
      "service": "dj-worker",
      "level": "error",
      "message": "job 9f2c... failed: Rendering failed. The uploaded audio or artwork may be corrupt or in an unsupported format."
    }
  ]
}
```

| Result | When |
|--------|------|
| 202 | At least one allowlisted log was stored |
| 400 | Empty body, or every service was outside the allowlist |
| 401 | Missing or wrong `X-Ingest-Key` |
| 503 | `Ingest:ApiKey` is empty (ingest off) |

Key compare is constant-time. Batch cap is 50 (`IngestPolicy.MaxBatch`). Default allowlist is `dj-api`, `dj-worker`.

Anonymous `POST /api/logs` is still not registered in production.

## Wire it

1. Set `Ingest__ApiKey` on `incident-api` (Render env, or local).
2. Set the same value on DJ as `Ops__IngestKey`, plus `Ops__IncidentBrainUrl` to this API's public origin.
3. Fail a DJ render. Within ~10 seconds the processor should open an incident for `dj-worker` if the failure repeats enough to trip spike/cluster thresholds.

With an empty key, DJ's sink is a no-op and this API returns 503. Neither side depends on the other being up.
