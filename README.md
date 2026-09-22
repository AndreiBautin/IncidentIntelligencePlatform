# Incident Intelligence Platform

A production-grade starter for a mini observability plus AI reasoning layer. Runs fully offline in production: no API keys, no paid services, no hosted LLMs. Optional local Ollama for development only.

## Five minutes

```bash
docker compose up --build
```

Open `http://localhost:3000`. The dashboard starts the log stream when it connects. Wait for an incident card, open it, read the summary. Tests: `dotnet test tests/IncidentBrain.Tests/IncidentBrain.Tests.csproj`.

Full click path and talking points: [docs/DEMO_WALKTHROUGH.md](docs/DEMO_WALKTHROUGH.md).

This repo is still private. Public toggle: [Settings](https://github.com/AndreiBautin/IncidentIntelligencePlatform/settings).

## System overview

The platform ingests log streams, clusters similar errors (TF-IDF + cosine similarity), detects spikes, and creates incidents with AI-generated summaries and investigation steps. The dashboard shows incidents in real time via Server-Sent Events (SSE). Production runs in read-only mode: public users see a live, bounded dashboard without control over the stream or data.

## Architecture summary

- Backend: .NET 9 Web API (Minimal APIs), SQLite, background hosted services for log processing and retention.
- Frontend: Next.js (App Router), TypeScript, Tailwind, Recharts, dark-mode UI.
- Streaming: Log generation starts when the first SSE client connects and stops when the last disconnects (production). Incidents and retention are bounded by configurable caps.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for diagrams and data flow.

## Technology stack

- API: .NET 9, ASP.NET Core Minimal APIs, Serilog, Entity Framework Core, SQLite
- Frontend: Next.js 15, React, TypeScript, Tailwind CSS, Recharts, Sonner
- Analysis: TF-IDF clustering, spike detection, configurable thresholds
- AI: Mock (default, production-only) and Ollama (local dev optional)

## Local setup

1. Backend (from repo root):

   ```bash
   dotnet run --project src/IncidentBrain.API
   ```

   API runs at `http://localhost:5000` (or `ASPNETCORE_URLS`).

2. Frontend:

   ```bash
   cd web/incidentbrain-web && npm install && npm run dev
   ```

   Open `http://localhost:3000`. The Dashboard starts the stream automatically; incidents appear live via SSE. Use Clear all (dev only) to reset.

3. Demo script (second terminal): `./scripts/demo.ps1` (PowerShell) or `./scripts/demo.sh` (Bash).

4. AI (dev): Local dev defaults to Ollama for summaries; run Ollama locally. If unavailable, the app falls back to Mock. Use Settings to switch provider.

## Docker

- Local (dev): `docker compose up --build`. API at `http://localhost:8080`, Web at `http://localhost:3000`. Open the dashboard; the stream starts automatically. Same retention bounds as production. AI defaults to Ollama (override points at `host.docker.internal:11434` when Ollama runs on the host). Use Clear all and Settings in the UI.
- Production: `docker compose -f docker-compose.prod.yml up --build`. Enforces `ASPNETCORE_ENVIRONMENT=Production`, Mock AI only, read-only frontend (`NEXT_PUBLIC_READ_ONLY=true`). No Ollama.

See [docs/DOCKER_SETUP.md](docs/DOCKER_SETUP.md) for Docker install and first-run.

## AI mode

- Mock is the default and only provider in production. No paid APIs, no hosted LLMs.
- Ollama is the default in Development for local experimentation. If Ollama is unavailable, the system falls back to Mock.
- Production UI does not expose AI toggles; control endpoints (for example PATCH `/api/settings/ai`) are disabled in production.

See [docs/AI_ABSTRACTION.md](docs/AI_ABSTRACTION.md) and [docs/ZERO_COST_DEPLOYMENT.md](docs/ZERO_COST_DEPLOYMENT.md).

## Live but bounded production

In production, the system behaves like a live dashboard while staying deterministic and bounded:

- Stream: Starts when the first SSE client connects, pauses when none. Log rate and SSE connection count are capped.
- Incidents: Auto-resolution after idle timeout; caps on active and total incidents; oldest resolved are purged when over cap.
- Logs: Total log rows capped (for example 5,000); oldest purged when over.
- UI: Read-only. No Start/Stop stream, Clear all, or Mark complete. See [docs/READONLY_PRODUCTION.md](docs/READONLY_PRODUCTION.md) and [docs/INCIDENT_LIFECYCLE.md](docs/INCIDENT_LIFECYCLE.md).

## Security posture

- HTTPS redirection and HSTS in production; global exception handling (no stack traces in responses); input validation and rate limiting; CORS restricted to configured origins; API and frontend containers run as non-root.
- Mutation/control endpoints are not registered in production; no anonymous writes.

See [docs/SECURITY.md](docs/SECURITY.md).

## CI/CD

- Build: GitHub Actions workflow (`.github/workflows/build.yml`) runs on push/PR to `main`. Restores and builds the API and frontend. Use as required status check for branch protection.
- Deploy: Optional deploy workflow (`.github/workflows/deploy.yml`) placeholder. Add steps for your free-tier host (Fly.io, Render). Store secrets in GitHub Secrets only.

See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for branch protection and workflow (feature branches, conventional commits, PRs).

## Design tradeoffs

- SQLite: Chosen for simplicity and zero cost. Single-file, no separate DB server. For scale or multi-instance, replace with a shared store (Cosmos DB, PostgreSQL).
- Mock AI in production: Enables zero-cost deployment. Summaries and steps are deterministic. For richer AI in private deployments, add an optional paid provider behind the same `IAIService` abstraction.
- Read-only production: Prevents abuse and keeps the public surface minimal. Admin control stays out of the public API and UI.

## Future evolution

- Replace SQLite with a scalable store for multi-instance or high volume.
- Add authentication (API keys, OAuth) for protected deployments.
- Optional paid AI provider for private instances. Keep Mock-only for public zero-cost deploy.
- Real log ingestion (file tail, webhook) alongside or instead of simulation.

## Project structure

```
/src
  IncidentBrain.API        # Minimal APIs, SSE, hosted services, middleware
  IncidentBrain.Core       # Domain, interfaces
  IncidentBrain.Infrastructure  # SQLite, TF-IDF, spike detection, Mock/Ollama AI
/tests
  IncidentBrain.Tests      # Unit + integration tests
/web
  incidentbrain-web        # Next.js App Router app
/docs                      # Architecture, lifecycle, security, deployment
```

## API endpoints (summary)

| Method | Path | Description | Production |
|--------|------|-------------|------------|
| GET | `/api/health` | Health check | Yes |
| GET | `/api/stats` | Log stats | Yes |
| GET | `/api/logs/recent` | Recent logs | Yes |
| GET | `/api/incidents` | List incidents | Yes |
| GET | `/api/incidents/{id}` | Get incident | Yes |
| GET | `/api/stream/incidents` | SSE stream | Yes |
| GET | `/api/simulation/status` | Stream running | Yes |
| GET | `/api/settings/ai` | Current AI provider | Yes (read-only) |
| POST | `/api/logs` | Ingest log | No (control) |
| POST | `/api/simulation/start` | Start stream (dashboard auto-calls in dev) | No (control; not registered in prod) |
| POST | `/api/simulation/stop` | Stop stream | No (control; not registered in prod) |
| PATCH | `/api/settings/ai` | Set AI provider | No (control) |
| POST | `/api/admin/clear` | Clear all data | No (control) |
| PATCH | `/api/incidents/{id}` | Update status | No (control) |
| POST | `/api/incidents/{id}/reanalyze` | Re-run AI | No (control) |

## Tests

```bash
dotnet test tests/IncidentBrain.Tests/IncidentBrain.Tests.csproj
```

## Documentation

- [DEMO_WALKTHROUGH](docs/DEMO_WALKTHROUGH.md): Docker click path and talking points
- [ARCHITECTURE](docs/ARCHITECTURE.md): Components and data flow
- [LOG_STREAMING_LIFECYCLE](docs/LOG_STREAMING_LIFECYCLE.md): When stream starts and stops, caps
- [CLUSTERING_ENGINE](docs/CLUSTERING_ENGINE.md): TF-IDF, thresholds
- [SQLITE_RETENTION](docs/SQLITE_RETENTION.md): Retention caps and purge order
- [INCIDENT_LIFECYCLE](docs/INCIDENT_LIFECYCLE.md): Auto-resolution, caps
- [AI_ABSTRACTION](docs/AI_ABSTRACTION.md): Mock vs Ollama, production rules
- [READONLY_PRODUCTION](docs/READONLY_PRODUCTION.md): What is disabled in production
- [ZERO_COST_DEPLOYMENT](docs/ZERO_COST_DEPLOYMENT.md): No paid services, bounded design
- [SECURITY](docs/SECURITY.md): HTTPS, CORS, rate limiting, Docker
- [CONTRIBUTING](docs/CONTRIBUTING.md): Workflow, branch protection
- [DOCKER_SETUP](docs/DOCKER_SETUP.md): Docker install and run
- [MANUAL_VERIFICATION_GUIDE](docs/MANUAL_VERIFICATION_GUIDE.md): Verification checklists
