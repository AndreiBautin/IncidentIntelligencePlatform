# Architecture

Three projects. Dependencies point inward. `Program.cs` is the only composition root.

```
web (Next.js) -> IncidentBrain.API -> IncidentBrain.Infrastructure -> IncidentBrain.Core
                                      IncidentBrain.API -----------> IncidentBrain.Core
```

`IncidentBrain.Core` has zero package references. That is enforced by `ArchitectureConstraintTests`.

## One request, real files

An interviewer opens the dashboard. The browser opens `GET /api/stream/incidents` and `GET /api/incidents`.

1. `web/incidentbrain-web` (SSE hook) connects to `/api/stream/incidents`.
2. `src/IncidentBrain.API/Program.cs` maps that route in every environment.
3. `src/IncidentBrain.API/Services/IncidentStreamBroadcaster.cs` `Subscribe` sees the first client and calls `ILogStreamSimulator.Start`.
4. `SimulatedStreamSource` (Infrastructure) emits log events.
5. `src/IncidentBrain.API/HostedServices/LogProcessorHostedService.cs` buffers them and writes through `IIncidentStore.AddLogsAsync`.
6. Every ten seconds the same hosted service loads recent logs, calls `TfIdfLogAnalyzer` (Infrastructure) and `SpikeDetectionEngine` (Core), then `IncidentCreationPolicy` (Core) to decide which spikes and clusters become incidents.
7. New `Incident` records are enriched by `IAIService` (`MockAIService` in Production) and saved through `IIncidentStore.AddIncidentAsync`.
8. The broadcaster writes an SSE event. The dashboard calls `GET /api/incidents` (`Program.cs`) and renders the card.

Production difference: mutation routes are not mapped. The stream still starts from the first SSE subscriber. See `docs/READONLY_PRODUCTION.md` and `ProductionApiTests`.

## DJ ingest (second source)

DJ Visualizer's render worker posts job failures to `POST /api/ingest/logs` (`IngestEndpoints.cs`) with `X-Ingest-Key`. Allowed services are `dj-api` and `dj-worker`. Those rows land in the same store with `LogSource.Ingest`. Step 6 above picks them up on the next tick. If the key is unset, the route returns 503 and DJ's sink is a no-op.

## Why the seams exist

- Swap SQLite for Postgres by implementing `IIncidentStore`. Core does not change.
- Swap the simulator for App Insights or a Service Bus pump by implementing `ILogIngestionSource` / `ILogStreamSimulator`.
- Swap Mock AI for a paid model by implementing `IAIService`. Production currently forces Mock in `Program.cs`.
- Change who becomes an incident without touching the host: edit `IncidentCreationPolicy`.

## Limits that are honest

SQLite and the in-memory subscriber list are single-process. Two API instances will not share incidents or SSE. That is the cost of a free-host demo. The ports are there so the next store and the next bus do not rewrite the domain.
