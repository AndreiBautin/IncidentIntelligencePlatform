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

Free instances have no persistent disk. SQLite resets when the API box sleeps. That is acceptable for a public demo. Do not treat the hosted data as durable.

## Local production check before you apply

```bash
docker compose -f docker-compose.prod.yml up --build
```

Open `http://localhost:3000`. You should see incidents. You should not see Clear all or Settings. `GET /api/incidents` must return 200. If that route 404s, production still has the old Program.cs that hid reads.
