# Interview guide

Written to be said out loud. Nothing here claims a feature or technology that
isn't actually in the repository — if a claim in this file turns out to be
false, it's a documentation bug; fix the doc or fix the code.

## 30-second explanation

"It's an incident-intelligence dashboard — the kind of thing an SRE team
would run to turn a flood of raw application logs into a short list of
things that actually need attention. Logs stream in, get clustered by
similarity with TF-IDF and cosine similarity, and get checked for sudden
error-rate spikes. When either of those crosses a threshold, it opens an
incident with an AI-generated summary and suggested next steps, and pushes
it to the dashboard over Server-Sent Events in real time. The backend is a
.NET Minimal API, the frontend's Next.js, and everything runs on free-tier
hosting with a local SQLite file and a mock AI provider in production — no
paid APIs, no credit card, nothing that costs money to keep online."

## How to explain the architecture

Three points to lead with:

1. **Three-project .NET solution, dependency direction matters.** `Core` is
   the domain (`Incident`, `LogEntry`, the `IIncidentStore` / `IAIService`
   interfaces) and depends on nothing external. `Infrastructure` implements
   those interfaces — SQLite via EF Core, the TF-IDF clustering engine, spike
   detection, Mock and Ollama AI. `API` wires it together and exposes it.
   Dependencies point inward: API → Infrastructure → Core, never the other
   way. That's what makes the clustering and spike-detection logic unit
   -testable with no database or web server involved (see `TfIdfTests.cs`,
   `SpikeDetectionTests.cs`).
2. **Two background hosted services do the real work, not the request
   handlers.** `LogProcessorHostedService` consumes the (simulated) log
   stream, runs clustering and spike detection, creates incidents, and
   broadcasts them over SSE. `RetentionEnforcementHostedService` runs
   auto-resolution and the retention caps. The HTTP endpoints are mostly
   thin reads plus SSE — the interesting logic runs on a timer, not on a
   request.
3. **Production is deliberately a read-only public dashboard.** A whole
   category of endpoints (log ingestion, simulation control, AI provider
   switching, admin clear, incident mutation) is simply **not registered**
   when `ASPNETCORE_ENVIRONMENT=Production` — not hidden behind auth, not
   feature-flagged off, structurally absent from the route table. See
   `docs/READONLY_PRODUCTION.md`.

## Request lifecycle, end to end

Picking the SSE dashboard load, since it's the one that touches everything:

1. Browser opens `/dashboard` (`web/incidentbrain-web/app/dashboard/`), which
   connects to `GET /api/stream/incidents` via `EventSource`.
2. `Program.cs` maps that route to the SSE endpoint backed by
   `IncidentStreamBroadcaster`. In production, this connection is what
   **starts** the log stream — `LogProcessorHostedService` checks for at
   least one connected SSE client before running (see
   `docs/LOG_STREAMING_LIFECYCLE.md`); with zero clients, nothing runs.
3. `SimulatedStreamSource` produces log lines at a configured rate
   (`Simulation:LogsPerSecond`), including a deliberate injected spike after
   `Simulation:SpikeDelaySeconds`.
4. Logs are buffered and written via `IIncidentStore.AddLogsAsync` (SQLite,
   `IncidentBrain.Infrastructure`).
5. `LogProcessorHostedService` periodically runs `TfIdfLogAnalyzer` (cosine
   similarity clustering, `Infrastructure/Analysis`) and
   `SpikeDetectionEngine` over the recent window. A cluster over
   `Analysis:ClusterSizeThreshold`, or a spike over the P1/P2/P3 thresholds,
   creates an `Incident`.
6. The incident is enriched by `IAIService` — in production this is always
   `MockAIProvider`, producing a deterministic summary and suggested steps
   with no external call.
7. The incident is saved (subject to the retention caps in
   `RetentionEnforcementHostedService` / `docs/SQLITE_RETENTION.md`) and
   broadcast to every connected SSE client via `IncidentStreamBroadcaster`.
8. The browser's `EventSource` handler appends it to the dashboard's state;
   React re-renders the incident list and the charts (Recharts).

## Engineering decisions

| Decision | Alternatives considered | Why this one | Trade-off |
|---|---|---|---|
| SQLite, single file, no managed DB | Postgres (Neon/Supabase free tier), a hosted document store | Zero cost, zero extra account, matches a genuinely bounded dataset (retention caps keep it small) | Doesn't survive a redeploy on Render's free plan (no persistent disk); wouldn't scale past one instance |
| Mock AI in production, Ollama in dev only | A paid LLM API (OpenAI/Anthropic/etc.) | No credit card anywhere in the deployed path; summaries stay deterministic, which also makes the whole pipeline testable without network calls | Production summaries are templated, not genuinely reasoned — explicitly not claiming otherwise anywhere in the UI or docs |
| Read-only production (control endpoints unregistered, not just hidden) | Auth-gate the mutation endpoints | A public demo with no auth is safer if the dangerous routes structurally don't exist than if they exist behind a check that could be misconfigured | No way to demo the "create/mutate" flows on the live deployment — only locally / in the read-write Docker Compose profile |
| TF-IDF + cosine similarity for clustering, not an embedding model | A real embeddings API, a local embedding model | Fast, deterministic, no dependency, explainable in one sentence, and — same reasoning as the AI choice — no network call or GPU needed | Weaker semantic grouping than embeddings would give; two log lines that mean the same thing in different words won't cluster |
| Deploy target: Render free web services (Docker) | Fly.io, Railway | Both now require a credit card even for their "free" trial (verified via web search, Sept 2026); Render's Docker free plan genuinely doesn't | Free services sleep after 15 minutes idle and cold-start (~60s); no persistent disk |
| Minimal APIs, not MVC controllers | ASP.NET Core MVC with controllers | Smaller surface for a mostly-read API with a handful of routes; matches the app's actual size | Less structure if the route count grows a lot — would likely want controllers or a proper endpoint-grouping convention past a certain size |

## Security talking points

Lead with the threat model, not a checklist: **this is a public, read-only
demo with no authentication and no user data**, so a large share of the
standard web-app checklist (session security, password handling, IDOR
across tenants, authz boundaries between users) is **structurally absent**
rather than "handled" — there's nothing to protect because there's no
per-user data and, in production, no state-changing endpoint at all.

What's real and actually done:

- **Control endpoints don't exist in production** (see above) — the
  strongest form of "disabled," since there's no code path to misconfigure.
- **Global exception middleware** returns a generic error and never a stack
  trace in production; detailed errors are dev-only.
- **Rate limiting** per IP (general requests/sec, stricter for mutation
  endpoints where they exist), **CORS** restricted to the configured web
  origin, **HSTS + HTTPS redirection** in production.
- **All queries go through EF Core** — parameterized, no string-built SQL,
  so there's no SQL-injection surface to begin with.
- **Both containers run as non-root users** in their Dockerfiles.
- **CI gates on dependency vulnerabilities (High/Critical) and scans full
  git history for secrets** (`gitleaks`) on every push and PR — not a
  one-time manual check.
- **Two known, currently-unfixable High-severity transitive dependency
  advisories are explicitly accepted and documented**, not swept under an
  ignored warning — see `docs/SECURITY.md`. Both are build-time or
  low-exploitability given how the app actually uses the vulnerable code
  path. This is worth bringing up unprompted: it shows the audit gate is
  real (it currently has something to accept) rather than trivially green.

## Database

- **SQLite**, one file, accessed exclusively through EF Core
  (`AppDbContext` in `Infrastructure`). Two tables of consequence:
  incidents and logs (see `IncidentBrain.Core.Domain` for the shapes).
- **No migrations directory** — the API calls `EnsureCreatedAsync` at
  startup, with a self-healing check (drops and recreates if the schema
  looks stale/mismatched — see `Program.cs`). That's a reasonable choice
  for a single-file bounded dataset with no real user data to preserve
  across a schema change; it would **not** be the right choice the moment
  this held anything worth migrating rather than regenerating — EF Core
  migrations would replace it then.
- **Access pattern**: everything through `IIncidentStore`, no raw SQL
  anywhere, so there's one seam to reason about for correctness and
  security both.
- **What breaks at scale**: a single SQLite file has no concurrent-writer
  story past one process, and Render's free plan doesn't even persist it
  across restarts. Neither matters for a bounded, single-instance,
  reset-on-restart demo — both would be the first thing to replace (with
  Postgres) for anything real.

## Deployment

- **Render**, two free Docker web services (API, web), provisioned from a
  committed `render.yaml` Blueprint. No credit card, no managed database.
- **CI/CD**: `build.yml` (push/PR to `main`) restores, builds, tests, audits
  dependencies at High+, scans history for secrets, and builds the frontend
  under both configurations that actually ship. `deploy.yml` (push to
  `main`) calls that same workflow as a job dependency — a red build never
  reaches Render — then triggers Render's deploy hooks and polls the live
  `/api/health` endpoint until it answers before calling the deploy done.
- **Config**: environment-driven throughout (`ASPNETCORE_ENVIRONMENT`,
  `NEXT_PUBLIC_*` build args, `ConnectionStrings__Default`, `Cors__*`) —
  nothing environment-specific is hardcoded in source.
- **Secrets**: two deploy-hook URLs live in GitHub Actions repository
  secrets; nothing else needs a secret because there's no external service
  to authenticate to (Mock AI, no managed DB).
- **Lifecycle**: push to `main` → verify → deploy → smoke-test, all in one
  workflow, all visible in the Actions tab.

## Testing

- **What's tested**: the actual business logic — TF-IDF clustering
  (`TfIdfTests.cs`), spike detection (`SpikeDetectionTests.cs`), log line
  parsing (`LogParsingTests.cs`), and that the simulated log stream is
  reproducible given a fixed seed (`SimulationReproducibilityTests.cs`) —
  plus one integration test tying the whole pipeline together, simulated
  logs in to an incident out (`Integration/SimulationToIncidentTests.cs`).
- **Why those**: this is where the app's actual value is — if clustering or
  spike detection is wrong, the whole product is wrong, silently, and
  nothing in manual testing would necessarily catch a subtle threshold bug.
- **What's deliberately not tested, and why**: there's no HTTP-level test
  hitting the Minimal API endpoints directly (no `WebApplicationFactory`
  integration test), and no frontend test suite. Both are honest gaps
  rather than "not needed" — the pipeline logic is the part that would
  actually break silently; the endpoints are thin enough that a manual
  pass (`docs/MANUAL_VERIFICATION_GUIDE.md`) and the CI build/typecheck
  catch most of what would go wrong with them. If this app grew real
  auth or more endpoints, endpoint-level tests would be the next thing
  added, not an afterthought.
- **Numbers**: 9 automated tests, all passing (`dotnet test`), verified
  during this pass.

## Deliberate simplifications

| Simplified | What a "real" version would have | Why it's fine here |
|---|---|---|
| No authentication anywhere | API keys or OAuth in front of mutation endpoints | Production has no mutation endpoints to protect; local/dev mode is explicitly not the public surface |
| SQLite instead of Postgres | A managed, horizontally-scalable database | Bounded dataset by design (retention caps); zero cost, zero extra account |
| Simulated log stream, not real ingestion | A log shipper / webhook / file-tail source | The point of the demo is the clustering and spike-detection pipeline, not building a log collector; the simulator produces the same shape of data |
| Mock AI, not a real LLM | An actual reasoning model summarizing incidents | Zero cost in production; the summaries are honestly templated, not dressed up as more than they are |
| No database migrations | EF Core Migrations with a version history | Nothing in this dataset is worth preserving across a schema change yet; would switch the moment that stops being true |
| Ephemeral storage on the free host | A persistent volume | Matches "read-only public demo," not a workaround being passed off as a feature — documented plainly as a Render free-plan constraint |

## Likely questions with concise answers

**"Isn't this over-engineered for a demo?"** — No; if anything it's
under-built in one direction (no auth, no real ingestion) and reasonably
scoped in the other (three-layer .NET solution because the domain logic
genuinely benefits from being testable in isolation, not because the app
"needed" clean architecture as a checkbox).

**"What's the weakest part?"** — The database story. SQLite with no
migrations and no persistence across a Render restart is exactly right for
a free public demo and exactly wrong the moment this needed to hold real
data or run more than one instance. I'd name that unprompted before anyone
asked.

**"What would you do differently?"** — Add authentication and real log
ingestion if this ever needed to be more than a demo; add
`WebApplicationFactory`-based endpoint tests before adding new endpoints;
move to Postgres before trusting it with anything that has to survive a
restart.

**"Why Render and not Fly.io/Railway/AWS?"** — Checked current terms
directly rather than going from memory: Fly.io and Railway both now
require a credit card, even for their nominally "free" trial. Render's
Docker free tier doesn't. For a portfolio project, "no card, ever" was a
hard requirement, not a preference.

**"How would this handle real production traffic?"** — It wouldn't, as
deployed — that's explicit, not a gap I'm hoping nobody asks about. The
free-tier host sleeps when idle, the database is a single ephemeral SQLite
file, and there's no horizontal scaling story. The clustering/spike logic
itself is stateless and would port to a real deployment; the storage and
hosting layer is what would need to change.

## Things not to say

- Don't call the AI "AI-powered summarization" without immediately
  clarifying it's a deterministic mock in production — the summaries are
  real text but not the output of a reasoning model, and overclaiming that
  invites a follow-up question with an uncomfortable answer.
- Don't say "production-ready" — say what's deployed, tested, and
  documented, and name what isn't (auth, real ingestion, a persistent
  database).
- Don't claim the live URL is always up — free-tier services sleep; say so
  before someone clicks the link mid-interview and waits 45 seconds.
- Don't claim "no known vulnerabilities" — two are explicitly accepted and
  documented (`docs/SECURITY.md`). Naming them unprompted is a stronger
  signal than a clean-looking `npm audit` that turns out not to reflect
  what's actually running.
