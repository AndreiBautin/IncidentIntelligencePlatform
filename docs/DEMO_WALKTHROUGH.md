# Demo walkthrough

Ten minutes. Docker only. No API keys.

## Start

```bash
docker compose up --build
```

API: `http://localhost:8080`  
Web: `http://localhost:3000`

Open the dashboard. The log stream starts when the first SSE client connects. Wait for a handful of log lines and at least one incident card.

## What you should see

1. Live log volume and a spike or cluster turning into an incident.
2. An incident detail with a summary and investigation steps. In this compose file that text comes from the Mock AI provider unless you pointed the container at a local Ollama.
3. The dashboard updating without a refresh. That is the SSE stream.

If the page is empty for more than a minute, check the API container logs and `GET http://localhost:8080/api/health`.

## What to ask an interviewer about

- How similar errors become one incident (TF-IDF clustering in Infrastructure, policy in Core).
- Why production is read-only and Mock-only (bounded public demo, no paid model).
- Where retention caps live and what gets purged first.
- How you would swap SQLite for Postgres without teaching the domain about a connection string.

## Stop

```bash
docker compose down
```

SQLite state lives in the API container unless you mapped a volume. A fresh `up` is a clean demo.

## Production compose

```bash
docker compose -f docker-compose.prod.yml up --build
```

Read-only UI. Mock AI only. Control routes are not registered. Use this when you want to show the public surface, not the operator surface.
