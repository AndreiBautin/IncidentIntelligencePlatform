# Incident Intelligence Platform - Quick demo script (PowerShell)
# 1) Start backend (assumed already running or start in another terminal)
# 2) Wait for healthy
# 3) Start simulation
# 4) Wait for spike / incidents
# 5) Fetch incidents and print summary

$ApiBase = if ($env:API_URL) { $env:API_URL } else { "http://localhost:5000" }

Write-Host "Using API: $ApiBase"
Write-Host "Waiting for health..."
$max = 30
$n = 0
while ($n -lt $max) {
    try {
        $r = Invoke-WebRequest -Uri "$ApiBase/api/health" -UseBasicParsing -TimeoutSec 2
        if ($r.StatusCode -eq 200) { break }
    } catch {}
    $n++
    Start-Sleep -Seconds 1
}
if ($n -ge $max) {
    Write-Host "API not ready. Start the backend first: dotnet run --project src/IncidentBrain.API"
    exit 1
}
Write-Host "API is healthy."

Write-Host "Stopping any existing simulation..."
Invoke-RestMethod -Uri "$ApiBase/api/simulation/stop" -Method POST -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Host "Starting simulation (random data each run)..."
Invoke-RestMethod -Uri "$ApiBase/api/simulation/start" -Method POST -ContentType "application/json" -Body '{}'

Write-Host "Waiting 45s for spike and incident creation..."
Start-Sleep -Seconds 45

Write-Host "Fetching incidents..."
$incidents = Invoke-RestMethod -Uri "$ApiBase/api/incidents" -Method GET
if ($incidents.Count -eq 0) {
    Write-Host "No incidents yet. Wait longer or check simulation."
    exit 0
}
Write-Host "Found $($incidents.Count) incident(s)."
$first = $incidents[0]
Write-Host ""
Write-Host "First incident:"
Write-Host "  ID: $($first.id)"
Write-Host "  Service: $($first.affectedService)"
Write-Host "  Severity: $($first.severity)  Status: $($first.status)"
Write-Host "  Summary: $($first.summary)"
Write-Host "  Steps:"
$first.suggestedSteps | ForEach-Object { Write-Host "    - $_" }
Write-Host ""
Write-Host "Stopping simulation."
Invoke-RestMethod -Uri "$ApiBase/api/simulation/stop" -Method POST -ErrorAction SilentlyContinue
Write-Host "Demo complete. Run the script again for a fresh simulation with different data."
