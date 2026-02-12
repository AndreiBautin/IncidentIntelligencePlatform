export const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

/** When true, hide admin/mutation controls (production read-only dashboard). */
export const READ_ONLY_MODE = process.env.NEXT_PUBLIC_READ_ONLY === "true";

export type Incident = {
  id: string;
  affectedService: string;
  startTime: string;
  endTime: string | null;
  errorCount: number;
  topErrorPattern: string;
  severity: "P1" | "P2" | "P3";
  status: "Open" | "Investigating" | "Resolved";
  summary: string;
  suggestedSteps: string[];
  clusterId: string | null;
  createdAt: string;
  updatedAt: string;
};

export async function getIncidents(params?: { status?: number; severity?: number; from?: string; to?: string; search?: string; service?: string }): Promise<Incident[]> {
  const sp = new URLSearchParams();
  if (params?.status != null) sp.set("status", String(params.status));
  if (params?.severity != null) sp.set("severity", String(params.severity));
  if (params?.from) sp.set("from", params.from);
  if (params?.to) sp.set("to", params.to);
  if (params?.search?.trim()) sp.set("search", params.search.trim());
  if (params?.service?.trim()) sp.set("service", params.service.trim());
  const url = `${API_URL}/api/incidents${sp.toString() ? `?${sp}` : ""}`;
  const r = await fetch(url);
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function getIncident(id: string): Promise<Incident | null> {
  const r = await fetch(`${API_URL}/api/incidents/${id}`);
  if (r.status === 404) return null;
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function reanalyzeIncident(id: string): Promise<Incident> {
  const r = await fetch(`${API_URL}/api/incidents/${id}/reanalyze`, { method: "POST" });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function updateIncidentStatus(id: string, status: number): Promise<Incident> {
  const r = await fetch(`${API_URL}/api/incidents/${id}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ status }),
  });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function startSimulation(body?: {
  seed?: number;
  logsPerSecond?: number;
  logIntervalMs?: number;
  spikeDelaySeconds?: number;
  spikeDurationSeconds?: number;
  incidentSensitivity?: string;
}) {
  const r = await fetch(`${API_URL}/api/simulation/start`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function stopSimulation() {
  const r = await fetch(`${API_URL}/api/simulation/stop`, { method: "POST" });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function getSimulationStatus(): Promise<{ running: boolean }> {
  const r = await fetch(`${API_URL}/api/simulation/status`);
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export type AIProvider = "Mock" | "Ollama";

export async function getAIProvider(): Promise<{ provider: AIProvider }> {
  const r = await fetch(`${API_URL}/api/settings/ai`);
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function setAIProvider(provider: AIProvider): Promise<{ provider: AIProvider }> {
  const r = await fetch(`${API_URL}/api/settings/ai`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ provider }),
  });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export async function clearAll() {
  const r = await fetch(`${API_URL}/api/admin/clear`, { method: "POST" });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export function getIncidentStreamUrl(): string {
  return `${API_URL}/api/stream/incidents`;
}

export type LogStats = { totalLogs: number; totalErrors: number; totalRequests: number };

export async function getStats(params?: { since?: string }): Promise<LogStats> {
  const sp = new URLSearchParams();
  if (params?.since) sp.set("since", params.since);
  const url = `${API_URL}/api/stats${sp.toString() ? `?${sp}` : ""}`;
  const r = await fetch(url);
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}

export type LogLine = { id: string; timestamp: string; service: string; level: string; message: string };

export async function getRecentLogs(params?: { count?: number; since?: string }): Promise<LogLine[]> {
  const sp = new URLSearchParams();
  if (params?.count != null) sp.set("count", String(params.count));
  if (params?.since) sp.set("since", params.since);
  const url = `${API_URL}/api/logs/recent${sp.toString() ? `?${sp}` : ""}`;
  const r = await fetch(url);
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}
