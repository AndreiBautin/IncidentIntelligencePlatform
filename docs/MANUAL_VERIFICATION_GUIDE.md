# Incident Intelligence Platform — Manual Verification Guide

Use this guide to get the app running and see the demo. No manual endpoint testing—just run the backend and (optionally) the frontend. The stream starts automatically when you open the **Dashboard**; or run the demo script for a CLI-only flow.

---

## Prerequisites

- [X] .NET 9 SDK (`dotnet --version` shows 9.x)
- [X] Node.js 18+ and npm (only if you run the web UI)
- [X] **Before building:** Stop any running API (Ctrl+C in its terminal, or `Stop-Process -Name "IncidentBrain.API" -Force -ErrorAction SilentlyContinue`), or the build may fail with "file is being used by another process".

---

## Option A: Demo script only (fastest)

See the full flow from the command line: API starts, simulation runs, spike triggers, incidents are created, script prints a summary.

1. **Stop** any running API (Ctrl+C if it’s in another terminal).

2. **Start the API** (leave this terminal open):
   ```powershell
   cd C:\Code\IncidentBrain
   dotnet run --project src/IncidentBrain.API
   ```
   Wait until you see: `Now listening on: http://localhost:5000`.

3. **In a second terminal**, run the demo script:
   - **PowerShell**: `.\scripts\demo.ps1`
   - **Bash**: `./scripts/demo.sh` (or `bash scripts/demo.sh`)

4. The script will:
   - Wait for the API to be healthy
   - Start the simulation (with seed 42)
   - Wait ~45 seconds for a spike and incident creation
   - Fetch incidents and print the first incident’s summary and suggested steps
   - Print "Demo complete."

If you see "Found N incident(s)" and a summary with steps, the backend and demo are working.

**Tip:** With the dashboard open in the browser (and API running), run the demo script in a terminal. The script stops any existing simulation, starts a new one (with different data each time), and stops again at the end. You’ll see new incidents appear or refresh on the dashboard as they’re created. Each run uses a random seed so errors and timing vary.

---

## Option B: See it running in the browser

Run the API and the Next.js app; the stream starts automatically when you open the dashboard.

1. **Stop** any running API.

2. **Start the API** (leave this terminal open):
   ```powershell
   cd C:\Code\IncidentBrain
   dotnet run --project src/IncidentBrain.API
   ```
   Wait for: `Now listening on: http://localhost:5000`.

3. **Start the frontend** (new terminal):
   ```powershell
   cd C:\Code\IncidentBrain\web\incidentbrain-web
   npm install
   ```
   Create `web/incidentbrain-web/.env.local` with one line:
   ```
   NEXT_PUBLIC_API_URL=http://localhost:5000
   ```
   Then:
   ```powershell
   npm run dev
   ```

4. **In the browser**, open **http://localhost:3000**.

5. **Watch the demo in the UI:**
   - The **Dashboard** starts the stream automatically when you open it.
   - Leave the Dashboard open. After 45–60 seconds you should see one or more incidents (spike/cluster-driven). The list updates live via SSE.
   - Click an incident to see its **AI summary** and **suggested investigation steps**.
   - Use **Clear all** to reset and run the demo from a fresh slate.
   - Optionally click **Reanalyze** on an incident. Simulation options (seed, logs/sec) are in **Settings**.

If the dashboard shows incidents with summaries and steps, the end-to-end flow is working.

---

## Optional: Run tests

From the repo root:

```powershell
dotnet test tests/IncidentBrain.Tests/IncidentBrain.Tests.csproj
```

Expect: **Passed: 9, Failed: 0**.

---

## Optional: Docker

**Don’t have Docker?** See **[DOCKER_SETUP.md](DOCKER_SETUP.md)** for install (Windows).

From repo root:

```powershell
docker compose up --build
```

Then open **http://localhost:3000** (set `NEXT_PUBLIC_API_URL=http://localhost:8080` for the browser). The stream starts automatically on the Dashboard; incidents appear as they are created. Stop containers with Ctrl+C, then `docker compose down`.

---

## Quick reference

| Context       | API URL                 | Web UI        |
|---------------|-------------------------|---------------|
| Local run     | http://localhost:5000   | http://localhost:3000 |
| Docker        | http://localhost:8080   | http://localhost:3000 |

---

## Troubleshooting

**Build fails: "file is being used by another process"**  
The API is still running. Stop it: Ctrl+C in its terminal, or in PowerShell:
```powershell
Stop-Process -Id <PID_from_error> -Force -ErrorAction SilentlyContinue
# or
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force
```
Then run `dotnet build IncidentBrain.sln` again.

**"No such table: Incidents" (500 on /api/incidents)**  
The database file existed from an earlier run but had no tables. Stop the API, delete any `incidentbrain.db` in the repo (e.g. repo root, `src/IncidentBrain.API/`, `src/IncidentBrain.API/bin/Debug/net9.0/`), then start the API again. It will recreate the database with all tables.

**Demo script: "API not ready"**  
Start the API first (`dotnet run --project src/IncidentBrain.API`) and wait until you see "Now listening on: ..." before running the script.

**Dashboard shows no incidents**  
The stream starts automatically when you open the Dashboard. Wait at least 45–60 seconds for the built-in spike; incidents will appear via SSE. You can also run the demo script while the dashboard is open.
