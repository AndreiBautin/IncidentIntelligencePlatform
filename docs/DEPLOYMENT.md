# Deployment

How this app gets from a push on `main` to a live URL, on a genuinely free tier.

## Provider: Render

**Chosen because**: the app is two long-running Docker containers (a .NET API,
a Next.js server), not a static site — so GitHub Pages/Cloudflare Pages/Netlify
don't fit (see [ZERO_COST_DEPLOYMENT.md](ZERO_COST_DEPLOYMENT.md) for why the app
is shaped this way: SQLite, Mock AI, no external services to provision). Among
free container hosts:

| Option | Rejected because |
|---|---|
| **Fly.io** | No longer has a real free tier — new orgs get a short trial (2 VM-hours / 7 days) and then must add a credit card before deploying anything. Fails the "no credit card, ever" bar outright. Verified via web search, September 2026. |
| **Railway** | Free trial requires a credit card as of 2023 and stays that way; the ongoing free plan is $1/mo minimum. Same failure mode as Fly.io. |
| **Render** | Free web services (Docker, from a connected GitHub repo) require **no credit card**. This is what's used. |

**Trade-offs accepted, not hidden**:
- Free web services **sleep after 15 minutes** with no traffic and cold-start on
  the next request (up to ~60s). Fine for a portfolio link someone clicks
  occasionally; not fine for an always-on production service. Noted in the
  README so a reviewer isn't surprised by a slow first load.
- Free plan has **no persistent disk**. The API's SQLite file resets on every
  redeploy/restart. This is not a workaround — it matches the app's own design:
  production is a read-only public demo that seeds itself live from the
  simulated log stream (see [READONLY_PRODUCTION.md](READONLY_PRODUCTION.md)),
  never a place real data lives. A disk resetting on restart is the "seed only
  into empty storage" guarantee for free, at the cost of history not surviving
  a redeploy — acceptable for a demo, would not be for a real deployment.

## Architecture

Two Render **Web Services**, both built from the repo's existing Dockerfiles,
provisioned together from **[`render.yaml`](../render.yaml)** (a Render
"Blueprint" — infrastructure as code, committed to the repo):

- `incidentbrain-api` — builds `./Dockerfile` (the existing multi-stage .NET
  build), `ASPNETCORE_ENVIRONMENT=Production`, so mutation/control endpoints
  are unregistered and Mock AI is enforced (see
  [READONLY_PRODUCTION.md](READONLY_PRODUCTION.md)).
- `incidentbrain-web` — builds `./web/incidentbrain-web/Dockerfile`, with
  `NEXT_PUBLIC_READ_ONLY=true` and `NEXT_PUBLIC_API_URL` pointed at the API
  service, both baked in at build time exactly as `docker-compose.prod.yml`
  already does locally (Render injects a Docker service's `envVars` as build
  `ARG`s, matching the `ARG NEXT_PUBLIC_API_URL` / `ARG NEXT_PUBLIC_READ_ONLY`
  lines already in that Dockerfile).

Nothing about the app's Dockerfiles or Docker Compose files changed to support
this — `render.yaml` points at the same images `docker-compose.prod.yml` builds.

## CI/CD

- **`.github/workflows/build.yml`** ("Build") — runs on every push/PR to
  `main`: restores and builds the whole solution, runs the test suite
  (`dotnet test`), gates on `.NET` and `npm` dependency audits (High/Critical,
  with a documented, currently-unfixable exception — see
  [SECURITY.md](SECURITY.md)), builds the frontend under **both** configurations
  that ship (default and `NEXT_PUBLIC_READ_ONLY=true`), and scans full git
  history for secrets (`gitleaks`). This is also a **reusable workflow**
  (`workflow_call`), so...
- **`.github/workflows/deploy.yml`** ("Deploy") — runs on push to `main`. Its
  `verify` job *calls* the Build workflow rather than re-implementing it, so
  there is exactly one place the checks live; `deploy` `needs: verify`, so a
  commit that fails a test or an audit gate never reaches Render. `deploy`
  POSTs to two Render **deploy hook** URLs (one per service; see setup below).
  `smoke-test` `needs: deploy`, polls the live `/api/health` endpoint until it
  reports healthy (Render's free-plan cold start can take up to ~60s) and then
  fetches the web app's root and asserts the response is an HTML document — a
  green deploy step only means an upload succeeded, this is what proves the
  site actually answered.
- Until the manual setup below is done, `deploy` and `smoke-test` **skip
  themselves with a `::warning::`** rather than failing — every push to `main`
  is still fully verified, it just doesn't yet have anywhere to publish to.

**Deliberately not gated further**: Build and Deploy are two separate workflow
*files* rather than one, so a PR's required status check ("Build") doesn't
depend on anything deploy-shaped, which is what keeps branch protection from
deadlocking (see [CONTRIBUTING.md](CONTRIBUTING.md) — do not add "Deploy" or
any of its jobs as a required check).

## One-time manual setup (required — this is the blocker only you can clear)

Nothing above can run until a Render account exists and two services are
provisioned from it. This sandbox has no path to create that account or reach
Render's API, so these steps are yours:

1. **Create a free Render account** at render.com (email or GitHub sign-in — no
   card). If prompted to connect GitHub, grant access to this repository. The
   repo is currently **private**; Render's GitHub integration works with
   private repos it's been granted access to, so this does **not** require
   making the repository public. (If you'd rather use Render's generic
   "public Git repo" import instead of the GitHub App integration, that path
   *does* need the repo public — the GitHub App path avoids that.)
2. **New → Blueprint**, pick this repository. Render reads `render.yaml` at
   the repo root and proposes both services. Confirm — both are free plan by
   the file, nothing to change. First build takes a few minutes (same Docker
   build the Dockerfiles already do locally).
3. Once both services exist, open each one's **Settings → Deploy Hook**, copy
   the URL, and add it as a **GitHub Actions repository secret**
   (Settings → Secrets and variables → Actions → New repository secret):
   - `RENDER_DEPLOY_HOOK_API` — the API service's deploy hook URL
   - `RENDER_DEPLOY_HOOK_WEB` — the web service's deploy hook URL
4. Copy each service's public URL (shown on its Render dashboard page, shape
   `https://<service-name>.onrender.com`) and add as **repository
   variables** (same Settings page, "Variables" tab — these are URLs, not
   secrets):
   - `RENDER_API_URL`
   - `RENDER_WEB_URL`
5. If the two URLs Render actually assigns differ from the
   `incidentbrain-api.onrender.com` / `incidentbrain-web.onrender.com` names
   `render.yaml` assumes (Render appends a suffix if the name is taken),
   update `Cors__AllowedOrigins__0` on the API service and
   `NEXT_PUBLIC_API_URL` on the web service in the Render dashboard to match,
   then redeploy both from the dashboard once.
6. Push to `main`, or run the "Deploy" workflow manually
   (`workflow_dispatch`) from the Actions tab. It will now actually deploy
   and smoke-test.

Nothing else is needed — no database to provision (SQLite lives in each
container), no other secrets, no other accounts.

## Updating a live deployment

Push to `main`. The Deploy workflow verifies, triggers both Render deploy
hooks, and confirms the new build is answering before finishing. Render's own
dashboard also shows build/deploy logs per service if something needs
debugging.

## Resetting demo data

Nothing to do — there is no seed script to re-run. Production's SQLite file
lives on the container's ephemeral disk, so it resets automatically on the
next redeploy or whenever Render restarts the free-plan instance (e.g. after
an idle sleep). The dashboard repopulates itself from the simulated log
stream the moment a viewer connects (see
[LOG_STREAMING_LIFECYCLE.md](LOG_STREAMING_LIFECYCLE.md)).

## Free-tier headroom

- **Compute**: 750 free instance-hours/month per Render account, shared across
  services. Two services that sleep when idle comfortably fit — a portfolio
  link gets occasional traffic, not sustained load.
- **Bandwidth**: 100GB/month free — far beyond what a dashboard SPA and a JSON
  API serving a bounded, capped dataset will use.
- **Build minutes**: 500/month free; each deploy here is two small Docker
  builds, a few minutes each.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `deploy` job logs `::warning:: RENDER_DEPLOY_HOOK_API secret not set` | One-time setup (above) not done yet | Follow steps 1–4 above |
| `smoke-test` times out waiting for `/api/health` | Free-plan cold start took longer than the poll budget, or the API build failed | Check the API service's logs in the Render dashboard; re-run the workflow (`workflow_dispatch`) once it's confirmed live |
| Web app loads but every request fails / CORS error in the browser console | `Cors__AllowedOrigins__0` on the API doesn't match the web service's actual URL (see step 5) | Update the env var on the API service in the Render dashboard, redeploy |
| Web app shows the wrong API URL (e.g. still `localhost`) | `NEXT_PUBLIC_API_URL` is a **build-time** arg — changing the env var alone doesn't affect an already-built image | Update the env var, then trigger a fresh deploy (env var changes on Render Docker services do trigger a rebuild, but confirm in the deploy log that the build step re-ran) |
| First load after a while is slow (~30–60s) | Free-plan services sleep after 15 minutes idle | Expected; documented above, not a bug |
