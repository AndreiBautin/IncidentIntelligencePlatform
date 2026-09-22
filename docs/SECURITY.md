# Security

Production posture for the public demo.

- HTTPS and HSTS when the process actually has an HTTPS port. Render terminates TLS at the edge, so the container skips redirect.
- Global exception middleware. No stack traces in responses.
- Input sanitizer on search, service, level, and message.
- Rate limits on general and mutation routes.
- CORS from configured origins.
- API and frontend containers run as non-root.
- Control routes are not registered in Production. See `ProductionApiTests`.
- Mock AI only in Production. No API keys in the image.
- `.env.example` is the only env template. No secrets in git.
- CI runs gitleaks and a NuGet vulnerability listing.

## SQLite native override

`Microsoft.EntityFrameworkCore.Sqlite` 9.0.0 still pulls `SQLitePCLRaw.lib.e_sqlite3` 2.1.x, which NuGet flags as GHSA-2m69-gcr7-jv3q (SQLite before 3.50.2). Both Infrastructure and the test project pin `SQLitePCLRaw.bundle_e_sqlite3` 3.0.3, which ships SourceGear.sqlite3 3.50.4 and drops the flagged package. EF Core has not moved this transitive pin yet.
