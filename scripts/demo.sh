#!/usr/bin/env bash
# Incident Intelligence Platform - Quick demo script (Bash)
# 1) Start backend (assumed already running or start in another terminal)
# 2) Wait for healthy
# 3) Start simulation
# 4) Wait for spike / incidents
# 5) Fetch incidents and print summary

API_BASE="${API_URL:-http://localhost:5000}"

echo "Using API: $API_BASE"
echo "Waiting for health..."
for i in $(seq 1 30); do
  if curl -sf "$API_BASE/api/health" > /dev/null; then break; fi
  if [ "$i" -eq 30 ]; then
    echo "API not ready. Start the backend first: dotnet run --project src/IncidentBrain.API"
    exit 1
  fi
  sleep 1
done
echo "API is healthy."

echo "Stopping any existing simulation..."
curl -s -X POST "$API_BASE/api/simulation/stop" > /dev/null
sleep 1
echo "Starting simulation (random data each run)..."
curl -s -X POST "$API_BASE/api/simulation/start" -H "Content-Type: application/json" -d '{}' > /dev/null

echo "Waiting 45s for spike and incident creation..."
sleep 45

echo "Fetching incidents..."
INCIDENTS=$(curl -s "$API_BASE/api/incidents")
COUNT=$(echo "$INCIDENTS" | jq 'length' 2>/dev/null || echo "0")
if [ "$COUNT" = "0" ] || [ -z "$COUNT" ]; then
  echo "No incidents yet. Wait longer or check simulation."
  exit 0
fi
echo "Found $COUNT incident(s)."
echo ""
echo "First incident:"
echo "$INCIDENTS" | jq -r '.[0] | "  ID: \(.id)\n  Service: \(.affectedService)\n  Severity: \(.severity)  Status: \(.status)\n  Summary: \(.summary)\n  Steps:\n\(.suggestedSteps[] | "    - " + .)"'
echo ""
echo "Stopping simulation."
curl -s -X POST "$API_BASE/api/simulation/stop" > /dev/null
echo "Demo complete. Run the script again for a fresh simulation with different data."
