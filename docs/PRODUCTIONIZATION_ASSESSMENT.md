# Productionization assessment

Assessed 2026-09-05 against the default branch at the start of the portfolio frame slice. Gaps table revisited 2026-09-08 after closing the deploy pipeline and interview guide (see the table below and `docs/DEPLOYMENT.md`, `docs/INTERVIEW_GUIDE.md`).

## Verdict up front

The application is a working offline-first incident intelligence demo: log ingestion, TF-IDF clustering, spike detection, incident lifecycle, SSE dashboard, Mock AI in production. Frame work is LICENSE and this assessment only. No behaviour change in this slice.

## What exists today

- **Backend:** .NET 9 Minimal APIs, Core (domain and interfaces), Infrastructure (SQLite, clustering, Mock/Ollama AI), hosted services for retention and streaming.
- **Frontend:** Next.js App Router, dark UI, Recharts, SSE-driven dashboard, read-only mode in production.
- **Orchestration:** Docker Compose (dev and prod variants). SQLite.
- **Docs:** Architecture, security, lifecycle, clustering, retention, zero-cost deployment, manual verification.
- **Tests:** Unit and integration under tests/IncidentBrain.Tests.
- **CI:** Build workflow on push/PR.
- **README:** Problem, stack, how to run (local and Docker), production bounds, security posture, API summary. Stranger can follow in a few minutes.
- **.env.example:** Present.

## Gaps for portfolio shape

| Slice | Status | Notes |
|-------|--------|-------|
| 1 Frame | Done | LICENSE MIT and this assessment. README already usable. |
| 2 Seams | Pending | Domain (`Core`) confirmed to have no I/O by inspection; no incremental port/policy move made this pass — out of scope for the deploy-gap slice below. |
| 3 Hygiene | Partial | .env.example exists; no secrets found in the tree. Full git-history secret scanning is now automated (`gitleaks`, `build.yml`) rather than a one-time manual check. No demo-data fixture exists to check — see note below. |
| 4 Domain tests | Partial | 9 automated tests (`dotnet test`, all passing as of this pass) cover TF-IDF clustering, spike detection, log parsing, and simulation reproducibility, plus one integration test. No formal coverage report. |
| 5 Use-case tests | Pending | No HTTP-level (`WebApplicationFactory`) endpoint tests exist. Named as a deliberate, documented gap in `docs/INTERVIEW_GUIDE.md` rather than left unaddressed silently. |
| 6 CI | Done | `build.yml` now runs `dotnet test`, gates .NET and npm dependency audits at High/Critical (two exceptions documented in `docs/SECURITY.md`), scans full git history for secrets, and builds the frontend under both shipping configurations. `.github/dependabot.yml` added. |
| 7 Demo | Done (deploy pipeline) / structurally different (data) | Render free-tier deploy pipeline built and gated on CI — see `docs/DEPLOYMENT.md`; **live URL requires a one-time manual Render signup this environment cannot perform**, named there as the remaining blocker. There is no separate "demo data fixture" to seed: production already runs read-only, seeded live by the existing simulated log stream (`docs/READONLY_PRODUCTION.md`), so Phase 3 of the productionization skill (generate-a-fixture) doesn't apply the way it would to an app with real user records — there was never personal data to keep out of a demo build in the first place. |
| 8 Explain | Done | Architecture and security already existed; `docs/INTERVIEW_GUIDE.md` added this pass. |

## Visibility and archive

The repository is private and not archived. Making it public is a Settings action this environment cannot perform (repository-settings writes are blocked here) — and turns out not to matter for the chosen deploy path: Render's GitHub App integration deploys from a private repo it's been granted access to, so no visibility change is required to get a live URL. It would still be worth making public before sharing the portfolio link, so an employer can read the source, not to make deployment possible. Settings: https://github.com/AndreiBautin/IncidentIntelligencePlatform/settings

## What this PR does not touch

Application code, tests, Docker, CI workflows, frontend or backend behaviour.

## Next slice after merge

Seams (one incremental clean-architecture change) or hygiene confirmation.
