"use client";

import { useState, useEffect } from "react";
import { startSimulation, stopSimulation, getAIProvider, setAIProvider, type AIProvider } from "@/lib/api";

export default function SettingsPage() {
  const [running, setRunning] = useState(false);
  const [seed, setSeed] = useState("42");
  const [logsPerSecond, setLogsPerSecond] = useState("5");
  const [logIntervalMs, setLogIntervalMs] = useState("600");
  const [spikeDelaySeconds, setSpikeDelaySeconds] = useState("10");
  const [spikeDurationSeconds, setSpikeDurationSeconds] = useState("60");
  const [incidentSensitivity, setIncidentSensitivity] = useState<string>("medium");
  const [aiProvider, setAiProviderState] = useState<AIProvider>("Ollama");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAIProvider().then((res) => setAiProviderState(res.provider)).catch(() => {});
  }, []);

  const handleStart = async () => {
    setLoading(true);
    setError(null);
    try {
      await startSimulation({
        seed: seed ? parseInt(seed, 10) : undefined,
        logsPerSecond: logsPerSecond ? parseInt(logsPerSecond, 10) : undefined,
        logIntervalMs: logIntervalMs ? parseInt(logIntervalMs, 10) : undefined,
        spikeDelaySeconds: spikeDelaySeconds ? parseInt(spikeDelaySeconds, 10) : undefined,
        spikeDurationSeconds: spikeDurationSeconds ? parseInt(spikeDurationSeconds, 10) : undefined,
        incidentSensitivity: incidentSensitivity !== "medium" ? incidentSensitivity : undefined,
      });
      setRunning(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to start");
    } finally {
      setLoading(false);
    }
  };

  const handleStop = async () => {
    setLoading(true);
    setError(null);
    try {
      await stopSimulation();
      setRunning(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to stop");
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="p-6 max-w-2xl mx-auto">
      <h1 className="text-2xl font-semibold mb-6">Settings</h1>

      <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-6 space-y-4">
        <h2 className="text-sm font-medium">Simulation</h2>
        <p className="text-sm text-zinc-500">
          Simulated log stream: normal logs first, then an error spike. Incidents are created when the backend detects
          the spike (or clusters of similar errors). Tune delay, log speed, and incident sensitivity for a good demo.
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <label className="block">
            <span className="text-xs text-zinc-500 block mb-1">Seed (optional, for reproducible runs)</span>
            <input
              type="text"
              value={seed}
              onChange={(e) => setSeed(e.target.value)}
              className="w-full rounded bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm"
            />
          </label>
          <label className="block">
            <span className="text-xs text-zinc-500 block mb-1">Log interval (ms). 0 = use logs/sec</span>
            <input
              type="number"
              min="0"
              value={logIntervalMs}
              onChange={(e) => setLogIntervalMs(e.target.value)}
              className="w-full rounded bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm"
            />
          </label>
          <label className="block">
            <span className="text-xs text-zinc-500 block mb-1">Logs per second (used when interval = 0)</span>
            <input
              type="text"
              value={logsPerSecond}
              onChange={(e) => setLogsPerSecond(e.target.value)}
              className="w-full rounded bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm"
            />
          </label>
          <label className="block">
            <span className="text-xs text-zinc-500 block mb-1">Spike delay (s) — wait before error spike starts</span>
            <input
              type="number"
              min="0"
              value={spikeDelaySeconds}
              onChange={(e) => setSpikeDelaySeconds(e.target.value)}
              className="w-full rounded bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm"
            />
          </label>
          <label className="block">
            <span className="text-xs text-zinc-500 block mb-1">Spike duration (s)</span>
            <input
              type="number"
              min="1"
              value={spikeDurationSeconds}
              onChange={(e) => setSpikeDurationSeconds(e.target.value)}
              className="w-full rounded bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm"
            />
          </label>
          <label className="block">
            <span className="text-xs text-zinc-500 block mb-1">Incident sensitivity</span>
            <select
              value={incidentSensitivity}
              onChange={(e) => setIncidentSensitivity(e.target.value)}
              className="w-full rounded bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm"
            >
              <option value="low">Low — fewer incidents (higher threshold)</option>
              <option value="medium">Medium — use API defaults</option>
              <option value="high">High — more incidents (lower threshold)</option>
            </select>
          </label>
        </div>
        <div className="flex flex-wrap gap-4 items-end pt-2">
          {!running ? (
            <button
              onClick={handleStart}
              disabled={loading}
              className="px-4 py-2 rounded bg-emerald-600 hover:bg-emerald-500 text-sm disabled:opacity-50"
            >
              {loading ? "Starting..." : "Start simulation"}
            </button>
          ) : (
            <button
              onClick={handleStop}
              disabled={loading}
              className="px-4 py-2 rounded bg-rose-600 hover:bg-rose-500 text-sm disabled:opacity-50"
            >
              {loading ? "Stopping..." : "Stop simulation"}
            </button>
          )}
        </div>
        {running && <p className="text-sm text-emerald-400">Simulation is running. Open Dashboard to see live incidents.</p>}
      </div>

      <div className="mt-6 rounded-lg border border-zinc-800 bg-zinc-900/50 p-6">
        <h2 className="text-sm font-medium mb-2">AI mode</h2>
        <p className="text-sm text-zinc-500 mb-2">
          Default is Mock (works with no install). For real AI summaries, install Ollama on your machine, then switch to Ollama here. No restart required.
        </p>
        <select
          value={aiProvider}
          onChange={async (e) => {
            const next = e.target.value as AIProvider;
            try {
              await setAIProvider(next);
              setAiProviderState(next);
            } catch {
              setError("Failed to update AI provider");
            }
          }}
          className="w-full rounded bg-zinc-800 border border-zinc-700 px-3 py-2 text-sm"
        >
          <option value="Mock">Mock (offline)</option>
          <option value="Ollama">Ollama (AI)</option>
        </select>
      </div>

      {error && (
        <div className="mt-4 p-4 rounded-lg bg-red-500/10 text-red-400 text-sm">
          {error}
        </div>
      )}
    </main>
  );
}
