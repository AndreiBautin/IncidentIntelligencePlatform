# Deployment

## Host

Render free web services. Same host as DJ Visualizer. No new cloud account if that Render login still works.

Blueprint: `render.yaml` at the repo root. `plan: free` is the only plan named.

## Apply

1. Merge this branch to `main`.
2. Open https://dashboard.render.com/blueprints
3. New Blueprint Instance, point it at `AndreiBautin/IncidentIntelligencePlatform`, branch `main`.
4. Apply. Two services come up: `incident-api` and `incident-web`.
5. First request after idle takes about a minute (free tier sleep).
6. Paste the `incident-web` URL into the README Live demo line.

Render can build a private GitHub repo if the GitHub account is already connected. Making the repo public is still required before you send the GitHub link to an interviewer.

## What the blueprint sets

- API: Production, Mock AI, SQLite at `/data/incidentbrain.db`.
- Web: `NEXT_PUBLIC_READ_ONLY=true`, `NEXT_PUBLIC_API_URL` taken from the API service public URL at build time.
- `Ingest__ApiKey` starts empty, so keyed ingest is off until you set it. **Left empty on this deployment on purpose** — the public demo runs on the simulated stream only, never another app's real production data on an unauthenticated dashboard. See "System overview" in the root README.

Free instances have no persistent disk. SQLite resets when the API box sleeps. That is acceptable for a public demo. Do not treat the hosted data as durable.

## DJ ingest, for a private instance only

The steps below wire real DJ Visualizer failures into a running instance. Do this on a separate, authenticated deployment - not the public one above - or you are back to piping one app's production telemetry into an unauthenticated dashboard. The `dj-ingest-live-instance` branch (both repos) already has it wired; these are the steps if you're standing up your own.

1. Generate a long random string.
2. On `incident-api`, set `Ingest__ApiKey` to that string.
3. On `dj-visualizer`, set `Ops__IngestKey` to the same string and `Ops__IncidentBrainUrl` to the `incident-api` public origin (no trailing path).
4. Fail a DJ render. The dashboard should show a `dj-worker` incident once clustering thresholds trip.

Details: [INGEST.md](INGEST.md).

## Local production check before you apply

```bash
docker compose -f docker-compose.prod.yml up --build
```

Open `http://localhost:3000`. You should see incidents. You should not see Clear all or Settings. `GET /api/incidents` must return 200. If that route 404s, production still has the old Program.cs that hid reads.
