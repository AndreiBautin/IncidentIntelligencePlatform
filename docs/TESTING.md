# Testing

Run from the repo root:

```bash
dotnet test tests/IncidentBrain.Tests/IncidentBrain.Tests.csproj -c Release
node --test web/incidentbrain-web/lib/readOnlyMode.test.mjs
```

CI runs both, plus coverlet, NuGet vulnerability listing, `npm audit --audit-level=high`, Next lint and build, and gitleaks. Coverage XML is uploaded as the `coverage` artifact. Open the latest Build run and read the Coverage summary step for line-rate by package.

## What is covered

| Area | File | Why it matters |
|---|---|---|
| Incident policy | `IncidentCreationPolicyTests.cs` | Sensitivity, spike filter, cluster severity |
| Spike policy | `SpikeDetectionTests.cs` | Empty input, single bucket, no cross-service bleed, P1 ratio |
| Clustering | `TfIdfTests.cs` | Similar errors become one cluster |
| Log ingest parse | `LogParsingTests.cs` | JSONL, JSON array, plain text |
| Mock AI | `MockAIServiceTests.cs` | Production summaries stay deterministic |
| Request edge | `RequestValidationTests.cs` | Trim and truncate at the HTTP boundary |
| Store retention | `SqliteStoreRetentionTests.cs` | Oldest logs and resolved incidents go first |
| Simulation to incident | `Integration/SimulationToIncidentTests.cs` | Logs plus spike plus Mock AI persist |
| Production surface | `ProductionApiTests.cs` | Reads exist, control routes 404 |
| Layer direction | `ArchitectureConstraintTests.cs` | Core has no packages and no API reference |
| Reproducible sim | `SimulationReproducibilityTests.cs` | Same seed, same first messages |
| Read-only UI flag | `web/incidentbrain-web/lib/readOnlyMode.test.mjs` | Only `NEXT_PUBLIC_READ_ONLY=true` hides controls |

## What is left out on purpose

- Next.js page chrome and chart layout
- Ollama live calls (dev only, requires a local model)
- Multi-instance SQLite (the demo is single-process)

If you add a domain rule, add a test next to these before adding UI.
