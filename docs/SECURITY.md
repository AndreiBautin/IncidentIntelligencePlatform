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

- NuGet and npm dependencies are updated; high/critical vulnerabilities are addressed. Run `dotnet list package --vulnerable` and `npm audit` periodically.

## SQLite

- All queries go through Entity Framework Core (parameterized). No raw SQL with string interpolation.
