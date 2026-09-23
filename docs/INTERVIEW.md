# Interview guide

Walk an interviewer through this repo in about fifteen minutes.

## What the system does

Simulated logs arrive. Similar error messages cluster with TF-IDF and cosine similarity. Spikes are compared to a baseline. Those two signals create incidents. A Mock AI (production) or local Ollama (dev) writes a summary and investigation steps. The dashboard listens over SSE.

## Files to open, in order

1. `src/IncidentBrain.Core/Domain/Incident.cs` and `IIncidentStore.cs`. Domain and ports. No ASP.NET, no EF.
2. `src/IncidentBrain.Core/Analysis/SpikeDetectionEngine.cs` and `IncidentCreationPolicy.cs`. Pure policy.
3. `src/IncidentBrain.Infrastructure/Analysis` and the SQLite store. Adapters.
4. `src/IncidentBrain.API/Program.cs`. Composition root and the production read-only split.
5. `src/IncidentBrain.API/HostedServices/LogProcessorHostedService.cs`. Batch, call policy, cap, persist, broadcast.
6. `src/IncidentBrain.API/Services/IncidentStreamBroadcaster.cs`. First SSE subscriber starts the simulator. Last one stops it.

## Questions they will ask

How would you ingest real App Insights or Service Bus traffic?
Implement `ILogIngestionSource` and stop calling the simulator. Domain stays the same.

Why SQLite?
Zero-cost public demo. Swap the store implementation. Keep `IIncidentStore`.

Why Mock AI in production?
No key, no bill, deterministic summaries. `IAIService` is the seam for a paid provider later.

What happens when two instances run?
SQLite and the in-memory subscriber list will not share. That is the honest limit of this demo. Next step is a shared store and a real pub/sub for SSE.

Where are the tests?
`tests/IncidentBrain.Tests` plus `web/incidentbrain-web/lib/readOnlyMode.test.mjs`. CI prints coverlet line-rate. See `docs/TESTING.md`.

DJ Visualizer posts here - why doesn't the live dashboard show it?
It's implemented and verified end to end on both sides (see `docs/INGEST.md`), and deliberately not wired to this deployment. Piping one app's real production failures into an unauthenticated public dashboard is a bad habit even when the content is safe - `Ingest:ApiKey` is unset here so the route 503s, and the public demo stays on the simulated stream only. The `dj-ingest-live-instance` branch is where it's live, for a private, authenticated instance.

## Bounded demo

Retention caps, Mock AI, and read-only production keep a free host cheap and safe to leave public.
