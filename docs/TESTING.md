# Testing

Run from the repo root:

```bash
dotnet test tests/IncidentBrain.Tests/IncidentBrain.Tests.csproj -c Release
```

CI runs the same command plus a gitleaks scan. Coverage is collected with coverlet (`--collect:"XPlat Code Coverage"`).

## What is covered

| Area | File | Why it matters |
|---|---|---|
| Spike policy | `SpikeDetectionTests.cs` | Core rule: ratio vs baseline becomes a spike |
| Clustering | `TfIdfTests.cs` | Similar errors become one cluster |
| Log ingest parse | `LogParsingTests.cs` | JSONL, JSON array, plain text |
| Mock AI | `MockAIServiceTests.cs` | Production summaries stay deterministic |
| Request edge | `RequestValidationTests.cs` | Trim and truncate at the HTTP boundary |
| Store retention | `SqliteStoreRetentionTests.cs` | Oldest logs and resolved incidents go first |
| Simulation to incident | `Integration/SimulationToIncidentTests.cs` | Logs plus spike plus Mock AI persist |
| Production surface | `ProductionApiTests.cs` | Reads exist, control routes 404 |
| Layer direction | `ArchitectureConstraintTests.cs` | Core has no packages and no API reference |
| Reproducible sim | `SimulationReproducibilityTests.cs` | Same seed, same stream |

## What is deliberately untested

- Next.js page chrome and chart layout
- Ollama live calls (dev only, requires a local model)
- Multi-instance SQLite (the demo is single-process on purpose)

If you add a domain rule, add a test next to these before adding UI.
