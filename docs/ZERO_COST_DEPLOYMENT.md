# Zero-cost deployment architecture

## Constraints

- **No paid AI APIs** (e.g. no OpenAI, Azure OpenAI, or other paid LLM services).
- **No hosted LLMs** or GPU infrastructure.
- **No paid compute** for this app; deployment targets are free-tier options (e.g. Fly.io, Render, static + serverless free tiers).

## How it works

- **AI**: Production uses **Mock** only. Summaries and steps are generated without external calls. Optional **Ollama** is for local development only.
- **Data**: **SQLite** for storage; no managed database cost. Retention caps keep size bounded.
- **Compute**: Single API process and optional static/Node frontend; sized for free-tier limits.
- **Streaming**: Log stream runs only when at least one SSE client is connected and is rate-limited; no unbounded background load.

## Bounded behavior

- **Incidents**: Capped by `MaxActiveIncidents` and `MaxTotalIncidents`; oldest resolved are purged.
- **Logs**: Capped by `MaxLogEntries`; oldest logs are purged.
- **Memory**: No unbounded in-memory collections; retention is enforced in SQLite.
- **Connections**: SSE connections limited by `Stream:MaxConcurrentSSE`.

The system is designed to run predictably for 48+ hours without growth in storage or memory beyond the configured caps.
