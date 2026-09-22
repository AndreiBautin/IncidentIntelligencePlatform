# Security decisions

## API

- HTTPS and HSTS in production when the process has an HTTPS port. Render terminates TLS at the edge, so the container skips redirect.
- Detailed exception pages are off in production. Global exception middleware returns a generic message and never includes stack traces.
- Request inputs are trimmed and length-capped for logs and filters.
- Per-IP rate limits: general requests per second, stricter on mutation routes per minute.
- CORS origins come from `Cors:AllowedOrigins`. Production should list only the frontend origin.
- `/api/stream/incidents` only emits incident events. Connection count is capped.

## Access control

- Mutation and control endpoints are not registered in production. Only reads, SSE, and keyed ingest are public-facing.
- `POST /api/ingest/logs` requires `X-Ingest-Key`. Compare is constant-time. Empty `Ingest:ApiKey` disables the route (503). Service names must be on the allowlist (`dj-api`, `dj-worker` by default).
- The public demo has no user auth. Add API keys or OAuth for a private deploy.

## Docker

- Multi-stage builds. API and frontend run as non-root.
- No secrets in images. Use environment variables at runtime. Do not put a real ingest key in `render.yaml` or `.env.example`.

## Dependencies

CI runs `dotnet list package --vulnerable --include-transitive` and fails the job if NuGet reports any. `npm audit --audit-level=high` runs and is allowed to warn: Next 15.5.12 still has published advisories. Bump to a patched 15.5.x after this PR if you want that gate hard-fail.

`Microsoft.EntityFrameworkCore.Sqlite` 9.0.0 still pulls `SQLitePCLRaw.lib.e_sqlite3` 2.1.x (GHSA-2m69-gcr7-jv3q). Infrastructure and tests pin `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3, which ships SQLite 3.50.4 and drops the flagged package.

## SQLite

All queries go through EF Core (parameterized). No raw SQL with string interpolation.
