"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { getIncident, reanalyzeIncident, type Incident } from "@/lib/api";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  ResponsiveContainer,
  Tooltip,
} from "recharts";

export default function IncidentDetailPage() {
  const params = useParams();
  const id = params.id as string;
  const [incident, setIncident] = useState<Incident | null>(null);
  const [loading, setLoading] = useState(true);
  const [reanalyzing, setReanalyzing] = useState(false);

  const load = () => getIncident(id).then(setIncident).finally(() => setLoading(false));

  useEffect(() => {
    load();
  }, [id]);

  const handleReanalyze = async () => {
    setReanalyzing(true);
    try {
      const updated = await reanalyzeIncident(id);
      setIncident(updated);
    } finally {
      setReanalyzing(false);
    }
  };

  if (loading || !incident) {
    return (
      <main className="p-6 max-w-4xl mx-auto">
        {loading ? <div className="text-zinc-500">Loading...</div> : <div className="text-zinc-500">Incident not found.</div>}
      </main>
    );
  }

  const chartData = [{ time: incident.startTime, count: incident.errorCount }];

  return (
    <main className="p-6 max-w-4xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Incident</h1>
        <button
          onClick={handleReanalyze}
          disabled={reanalyzing}
          className="px-4 py-2 rounded bg-zinc-700 hover:bg-zinc-600 text-sm disabled:opacity-50"
        >
          {reanalyzing ? "Reanalyzing..." : "Reanalyze"}
        </button>
      </div>

      <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-4">
        <div className="flex gap-2 mb-4">
          <span className="text-xs font-medium px-2 py-1 rounded bg-red-500/20 text-red-400 border border-red-500/50">{incident.severity}</span>
          <span className="text-xs px-2 py-1 rounded bg-zinc-500/20 text-zinc-400">{incident.status}</span>
          <span className="text-sm text-zinc-500">{incident.affectedService}</span>
        </div>
        <p className="text-sm text-zinc-400 mb-2">Top pattern: {incident.topErrorPattern}</p>
        <p className="text-xs text-zinc-500">{incident.errorCount} errors · Started {new Date(incident.startTime).toLocaleString()}</p>
      </div>

      <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-4">
        <h2 className="text-sm font-medium mb-2">Error timeline</h2>
        <div className="h-40">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={chartData}>
              <XAxis dataKey="time" tick={{ fontSize: 10 }} stroke="#71717a" />
              <YAxis tick={{ fontSize: 10 }} stroke="#71717a" />
              <Tooltip contentStyle={{ background: "#18181b", border: "1px solid #27272a" }} />
              <Area type="monotone" dataKey="count" stroke="hsl(var(--accent))" fill="hsl(var(--accent))" fillOpacity={0.3} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-4">
        <h2 className="text-sm font-medium mb-2">AI Summary</h2>
        <p className="text-sm text-zinc-300">{incident.summary}</p>
      </div>

      <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-4">
        <h2 className="text-sm font-medium mb-2">Suggested investigation steps</h2>
        <ul className="list-disc list-inside space-y-1 text-sm text-zinc-300">
          {incident.suggestedSteps?.map((step, i) => (
            <li key={i}>{step}</li>
          ))}
        </ul>
      </div>
    </main>
  );
}
