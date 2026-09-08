# Security decisions

## API

- **HTTPS**: In production, HTTPS redirection and HSTS are enabled.
- **Errors**: Detailed exception pages are disabled in production; a global exception middleware returns a generic message and never includes stack traces in responses.
- **Validation**: Request inputs are validated and sanitized (trim, max length) for logs and filter parameters.
- **Rate limiting**: Per-IP (or X-Forwarded-For) limits: general requests per second and stricter limits for mutation endpoints per minute.
- **CORS**: Allowed origins are configured (e.g. in `Cors:AllowedOrigins`); production should list only the frontend origin(s).
- **SSE**: The `/api/stream/incidents` endpoint only exposes incident events; no internal state or stack traces. Connection count is limited.

## Access control

- In production, **mutation/control endpoints are not registered**. Only read-only and SSE endpoints are available to the public.
- No authentication is required for the read-only API; the app is designed for a single-tenant or low-risk public dashboard. For sensitive deployments, add auth (e.g. API keys or OAuth) and keep control endpoints disabled or protected.

## Docker

- **API**: Multi-stage build; container runs as non-root user; `/data` is used for SQLite.
- **Frontend**: Multi-stage build; runs as non-root `nextjs` user.
- **Secrets**: No secrets baked into images; use environment variables or mounts at runtime. Use GitHub Secrets (or equivalent) for deployment tokens.

## Dependencies

- NuGet and npm dependencies are updated; high/critical vulnerabilities are addressed. `dotnet list package --vulnerable` and `npm audit` now run **automatically on every push and PR** (`.github/workflows/build.yml`), gated at High/Critical — a new high-severity dependency fails the build rather than waiting to be noticed on the next manual run.
- **Accepted exceptions** (checked at every CI run, not silently ignored): the audit gates fail on any High/Critical finding except the two below. Both are re-evaluated whenever the gate script (`scripts/ci/npm-audit-gate.mjs`) or the .NET step in `build.yml` is touched.
  - `GHSA-2m69-gcr7-jv3q` (CVE-2025-6965, `SQLitePCLRaw.lib.e_sqlite3`, transitive via `Microsoft.EntityFrameworkCore.Sqlite`): a memory-corruption issue in SQLite's own aggregate-query handling. No patched version of the NuGet package exists yet. **Impact here**: low — every query goes through EF Core LINQ with no user-composed SQL or user-controlled aggregate structure (see "SQLite" below), so triggering it would require an attacker who can already write arbitrary queries against this database, which nothing in this API exposes.
  - `GHSA-qx2v-qp2m-jg93`, `GHSA-6g55-p6wh-862q`, `GHSA-fxqj-rqcc-2cmp`, `GHSA-r28c-9q8g-f849` (`postcss`, bundled inside `next`'s own build tooling, not the app's direct `postcss` dependency): XSS-in-stringify and source-map path-traversal issues in PostCSS's CSS compiler. **Impact here**: none at runtime — this is a build-time tool compiling this repository's own trusted Tailwind CSS, never fed attacker-controlled input or reachable by a request to the running app. Fixing it requires a Next.js 16 major upgrade, out of scope for this pass.
- **Secret scanning**: `gitleaks` scans full git history on every push/PR (`build.yml`, `secret-scan` job) — not just the working tree.
- **Dependency freshness**: `.github/dependabot.yml` files weekly PRs for NuGet, npm, and the GitHub Actions used in CI — minor/patch bumps grouped into one PR per ecosystem, majors and security fixes left separate so they don't get silently absorbed into a routine bump. Every such PR is proved by the Build workflow before merge, same as any other PR.

## SQLite

- All queries go through Entity Framework Core (parameterized). No raw SQL with string interpolation.
