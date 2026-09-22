# Interview guide

Walk an interviewer through this repo in about fifteen minutes.

## What the system does

Simulated logs arrive. Similar error messages cluster with TF-IDF and cosine similarity. Spikes are compared to a baseline. Those two signals create incidents. A Mock AI (production) or local Ollama (dev) writes a summary and investigation steps. The dashboard listens over SSE.

## Files to open, in order

1. `src/IncidentBrain.Core/Domain/Incident.cs` and `IIncidentStore.cs`. Domain and ports. No ASP.NET, no EF.
2. `src/IncidentBrain.Core/Analysis/SpikeDetectionEngine.cs`. Pure policy.
3. `src/IncidentBrain.Infrastructure/Analysis` and the SQLite store. Adapters.
4. `src/IncidentBrain.API/Program.cs`. Composition root and the production read-only split.
5. `src/IncidentBrain.API/HostedServices/LogProcessorHostedService.cs`. Batch, cluster, cap, persist, broadcast.
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
`tests/IncidentBrain.Tests`: TF-IDF, spike detection, log parsing, simulation reproducibility, plus `Integration/SimulationToIncidentTests.cs`.

## What this is not

It is a bounded demo, sized so a free host can run it. Retention caps, Mock AI, and read-only production are the constraints that keep it cheap and safe to leave public.
