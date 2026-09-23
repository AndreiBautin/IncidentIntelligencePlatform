"use client";

import { useMemo, useEffect, useState, useRef, type ReactNode } from "react";
import { toast } from "sonner";
import { getIncidents, getIncident, reanalyzeIncident, updateIncidentStatus, getStats, getRecentLogs, startSimulation, getSimulationStatus, clearAll, READ_ONLY_MODE, type Incident } from "@/lib/api";
import { useIncidentStream } from "@/hooks/useIncidentStream";
import {
  AreaChart,
  Area,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  ResponsiveContainer,
  Tooltip,
  Cell,
  PieChart,
  Pie,
} from "recharts";

const severityColors: Record<string, string> = {
  P1: "bg-red-500/20 text-red-400 border-red-500/50",
  P2: "bg-amber-500/20 text-amber-400 border-amber-500/50",
  P3: "bg-zinc-500/20 text-zinc-400 border-zinc-500/50",
};

const statusColors: Record<string, string> = {
  Open: "bg-rose-500/20 text-rose-400",
  Investigating: "bg-blue-500/20 text-blue-400",
  Resolved: "bg-emerald-500/20 text-emerald-400",
};

function isSuccessLikeMessage(text: string): boolean {
  const t = text.toLowerCase();
  return /deployment\s+(completed|successful)|successful|completed\s+successfully/.test(t);
}

function severityBadgeClass(incident: Incident): string {
  const base = severityColors[incident.severity] ?? "";
  if (isSuccessLikeMessage(incident.topErrorPattern) || isSuccessLikeMessage(incident.summary))
    return "bg-emerald-500/20 text-emerald-400 border-emerald-500/50";
  return base;
}

const SEVERITY_BAR_COLORS: Record<string, string> = {
  P1: "#ef4444",
  P2: "#f59e0b",
  P3: "#71717a",
};

const severityBorderClass: Record<string, string> = {
  P1: "border-l-red-500",
  P2: "border-l-amber-500",
  P3: "border-l-zinc-600",
};

function IncidentSparkline({ errorCount, maxCount }: { errorCount: number; maxCount: number }) {
  const pct = maxCount > 0 ? Math.min(100, (errorCount / maxCount) * 100) : 0;
  return (
    <div className="flex-shrink-0 flex items-center gap-2 w-28">
      <div className="flex-1 h-2 bg-zinc-800 rounded-full overflow-hidden">
        <div
          className="h-full rounded-full bg-[hsl(var(--accent))] transition-all duration-300"
          style={{ width: `${pct}%` }}
        />
      </div>
      <span className="text-xs text-zinc-500 tabular-nums">{errorCount}</span>
    </div>
  );
}

function DashboardRoot({ children }: { children: ReactNode }) {
  return (
    <div role="main" className="flex flex-col min-h-screen p-4 px-6 w-full max-w-full">
      {children}
    </div>
  );
}

export default function DashboardPage() {
  const [incidents, setIncidents] = useState<Incident[]>([]);
  const [stats, setStats] = useState<{ totalLogs: number; totalErrors: number; totalRequests: number } | null>(null);
  const [statusFilter, setStatusFilter] = useState<number | "">(0);
  const [severityFilter, setSeverityFilter] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState("");
  const [serviceFilter, setServiceFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [streamRunning, setStreamRunning] = useState(false);
  const [clearLoading, setClearLoading] = useState(false);
  const [logLines, setLogLines] = useState<{ id: string; timestamp: string; service: string; level: string; message: string }[]>([]);
  const logStreamRef = useRef<HTMLDivElement>(null);
  const lastLogTimestampRef = useRef<string | undefined>(undefined);
  const isAtBottomRef = useRef(true);
  const [fastPollPhase, setFastPollPhase] = useState(false);
  const [selectedIncidentId, setSelectedIncidentId] = useState<string | null>(null);
  const [selectedIncident, setSelectedIncident] = useState<Incident | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [reanalyzing, setReanalyzing] = useState(false);
  const [markingComplete, setMarkingComplete] = useState(false);
  const selectedCardRef = useRef<HTMLDivElement>(null);

  const refresh = () => {
    const severityNum = severityFilter === "P1" ? 2 : severityFilter === "P2" ? 1 : severityFilter === "P3" ? 0 : undefined;
    const incidentsPromise = getIncidents({
      status: statusFilter === "" ? undefined : statusFilter,
      severity: severityNum,
      search: searchQuery.trim() || undefined,
      service: serviceFilter.trim() || undefined,
    }).then(setIncidents).finally(() => setLoading(false));
    getStats().then(setStats).catch(() => setStats(null));
    return incidentsPromise;
  };

  useIncidentStream((data) => {
    if (data?.type === "IncidentCreated" && data.id) {
      toast.success("New incident created", { description: data.id });
      getIncident(data.id).then((inc) => {
        if (inc) setIncidents((prev) => [inc, ...prev.filter((i) => i.id !== inc.id)]);
      }).catch(() => refresh());
    } else if (data?.type === "IncidentUpdated" && data.id) {
      getIncident(data.id).then((inc) => {
        if (inc) setIncidents((prev) => prev.map((i) => (i.id === inc.id ? inc : i)));
      }).catch(() => refresh());
    }
  });

  useEffect(() => {
    refresh();
  }, [statusFilter, severityFilter, searchQuery, serviceFilter]);

  useEffect(() => {
    getSimulationStatus().then((s) => setStreamRunning(s.running)).catch(() => {});
  }, []);

  // Auto-start stream when dashboard loads (local dev only)
  useEffect(() => {
    if (READ_ONLY_MODE) return;
    let cancelled = false;
    startSimulation()
      .then(() => {
        if (!cancelled) setStreamRunning(true);
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (streamRunning) setFastPollPhase(true);
    else setFastPollPhase(false);
  }, [streamRunning]);

  useEffect(() => {
    if (!streamRunning) return;
    lastLogTimestampRef.current = undefined;
  }, [streamRunning]);

  useEffect(() => {
    if (!streamRunning) return;
    const intervalMs = fastPollPhase ? 800 : 1500;
    const interval = setInterval(() => {
      const since = lastLogTimestampRef.current;
      getRecentLogs({ count: 200, since })
        .then((logs) => {
          if (since === undefined) {
            setLogLines(logs.slice(0, 200));
            if (logs.length > 0) {
              const latest = logs.reduce((m, l) => (l.timestamp > m ? l.timestamp : m), logs[0].timestamp);
              lastLogTimestampRef.current = latest;
            }
          } else {
            setLogLines((prev) => {
              const seen = new Set(prev.map((x) => x.id));
              const added = logs.filter((x) => !seen.has(x.id));
              return [...prev, ...added].slice(-200);
            });
          }
          if (logs.length > 0) {
            const latest = logs.reduce((m, l) => (l.timestamp > m ? l.timestamp : m), logs[0].timestamp);
            lastLogTimestampRef.current = latest;
          }
        })
        .catch(() => {});
    }, intervalMs);
    return () => clearInterval(interval);
  }, [streamRunning, fastPollPhase]);

  useEffect(() => {
    if (!streamRunning || !fastPollPhase) return;
    const t = setTimeout(() => setFastPollPhase(false), 20000);
    return () => clearTimeout(t);
  }, [streamRunning, fastPollPhase]);

  useEffect(() => {
    if (isAtBottomRef.current && logStreamRef.current) {
      logStreamRef.current.scrollTop = logStreamRef.current.scrollHeight;
    }
  }, [logLines]);

  const handleReanalyze = async () => {
    if (!selectedIncidentId) return;
    setReanalyzing(true);
    try {
      const updated = await reanalyzeIncident(selectedIncidentId);
      setSelectedIncident(updated);
      setIncidents((prev) => prev.map((i) => (i.id === updated.id ? updated : i)));
    } finally {
      setReanalyzing(false);
    }
  };

  const handleStatusUpdate = async (scope: "selected" | "all", newStatus: 0 | 2) => {
    if (!selectedIncidentId || !selectedIncident) return;
    const grp = incidentGroups.find((g) => g.incidents.some((i) => i.id === selectedIncidentId));
    const toUpdate = scope === "all" && grp ? grp.incidents : [selectedIncident];
    setMarkingComplete(true);
    try {
      const updatedList = await Promise.all(toUpdate.map((i) => updateIncidentStatus(i.id, newStatus)));
      setIncidents((prev) => {
        const next = prev.map((i) => {
          const u = updatedList.find((u) => u.id === i.id);
          return u ?? i;
        });
        if (statusFilter === "") return next;
        const wantStatus = Number(statusFilter);
        return next.filter((i) => (i.status === "Open" ? 0 : i.status === "Resolved" ? 2 : 1) === wantStatus);
      });
      if (scope === "all") {
        setSelectedIncidentId(null);
        setSelectedIncident(null);
      } else {
        const updated = updatedList[0];
        const stillInList = statusFilter === "" || (updated.status === "Open" ? 0 : updated.status === "Resolved" ? 2 : 1) === Number(statusFilter);
        if (grp && grp.incidents.length === 1) {
          setSelectedIncidentId(null);
          setSelectedIncident(null);
        } else if (!stillInList && grp) {
          const other = grp.incidents.find((i) => i.id !== updated.id);
          if (other) {
            setSelectedIncidentId(other.id);
            setSelectedIncident(other);
          } else {
            setSelectedIncidentId(null);
            setSelectedIncident(null);
          }
        } else {
          setSelectedIncident(updated);
        }
      }
      const isResolve = newStatus === 2;
      const msg =
        scope === "all"
          ? isResolve
            ? toUpdate.length > 1
              ? "All incidents in group marked complete"
              : "Incident marked complete"
            : toUpdate.length > 1
              ? "All incidents in group reopened"
              : "Incident reopened"
          : isResolve
            ? "Incident marked complete"
            : "Incident reopened";
      toast.success(msg);
    } catch {
      toast.error("Failed to update incident(s)");
    } finally {
      setMarkingComplete(false);
    }
  };

  const handleClear = async () => {
    if (!confirm("Clear all incidents and logs? This cannot be undone.")) return;
    setClearLoading(true);
    try {
      await clearAll();
      setStats(null);
      setIncidents([]);
      setLoading(false);
    } finally {
      setClearLoading(false);
    }
  };

  const overview = useMemo(() => {
    const total = incidents.length;
    const p1 = incidents.filter((i) => i.severity === "P1").length;
    const p2 = incidents.filter((i) => i.severity === "P2").length;
    const p3 = incidents.filter((i) => i.severity === "P3").length;
    return { total, p1, p2, p3 };
  }, [incidents]);

  const severityBarData = useMemo(
    () => [
      { name: "P1", count: overview.p1, fill: SEVERITY_BAR_COLORS.P1 },
      { name: "P2", count: overview.p2, fill: SEVERITY_BAR_COLORS.P2 },
      { name: "P3", count: overview.p3, fill: SEVERITY_BAR_COLORS.P3 },
    ],
    [overview]
  );

  const maxErrorCount = useMemo(
    () => (incidents.length > 0 ? Math.max(...incidents.map((i) => i.errorCount), 1) : 1),
    [incidents]
  );

  const timelineData = useMemo(() => {
    const byHour: Record<string, number> = {};
    incidents.forEach((inc) => {
      const t = new Date(inc.startTime);
      const key = `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, "0")}-${String(t.getDate()).padStart(2, "0")} ${String(t.getHours()).padStart(2, "0")}:00`;
      byHour[key] = (byHour[key] ?? 0) + 1;
    });
    return Object.entries(byHour)
      .sort(([a], [b]) => a.localeCompare(b))
      .slice(-12)
      .map(([time, count]) => ({ time: time.slice(-5), count }));
  }, [incidents]);

  const byServiceData = useMemo(() => {
    const map: Record<string, number> = {};
    incidents.forEach((inc) => {
      map[inc.affectedService] = (map[inc.affectedService] ?? 0) + 1;
    });
    return Object.entries(map)
      .map(([service, count]) => ({ service, count }))
      .sort((a, b) => b.count - a.count)
      .slice(0, 8);
  }, [incidents]);

  const statusPieData = useMemo(() => {
    const open = incidents.filter((i) => i.status === "Open").length;
    const investigating = incidents.filter((i) => i.status === "Investigating").length;
    const resolved = incidents.filter((i) => i.status === "Resolved").length;
    return [
      { name: "Open", value: open, fill: "#f43f5e" },
      { name: "Investigating", value: investigating, fill: "#3b82f6" },
      { name: "Resolved", value: resolved, fill: "#10b981" },
    ].filter((d) => d.value > 0);
  }, [incidents]);

  const incidentGroups = useMemo(() => {
    const sorted = [...incidents].sort((a, b) => new Date(a.startTime).getTime() - new Date(b.startTime).getTime());
    const byKey = new Map<string, Incident[]>();
    for (const inc of sorted) {
      const key = (inc.topErrorPattern.split("\n")[0]?.trim() || inc.topErrorPattern).replace(/\s+/g, " ");
      if (!byKey.has(key)) byKey.set(key, []);
      byKey.get(key)!.push(inc);
    }
    return Array.from(byKey.entries()).map(([patternKey, groupIncidents]) => ({
      patternKey,
      patternDisplay: groupIncidents[0].topErrorPattern.split("\n")[0]?.trim() || groupIncidents[0].topErrorPattern,
      incidents: groupIncidents,
      totalErrors: groupIncidents.reduce((s, i) => s + i.errorCount, 0),
      services: [...new Set(groupIncidents.map((i) => i.affectedService))],
      worstSeverity: groupIncidents.some((i) => i.severity === "P1") ? "P1" : groupIncidents.some((i) => i.severity === "P2") ? "P2" : "P3",
      representative: groupIncidents.reduce<Incident>((best, i) => (i.errorCount >= best.errorCount ? i : best), groupIncidents[0]),
    }));
  }, [incidents]);

  const detailChartData = useMemo(() => {
    if (!selectedIncidentId || !selectedIncident) return [];
    const grp = incidentGroups.find((g) => g.incidents.some((i) => i.id === selectedIncidentId));
    const list = grp ? grp.incidents : [selectedIncident];
    return list
      .map((inc) => ({ id: inc.id, time: inc.startTime, count: inc.errorCount }))
      .sort((a, b) => new Date(a.time).getTime() - new Date(b.time).getTime())
      .map((row, i) => ({ ...row, incidentLabel: list.length > 1 ? `Incident ${i + 1}` : "Incident" }));
  }, [selectedIncidentId, selectedIncident, incidentGroups]);

  const selectedGroup = useMemo(
    () => incidentGroups.find((g) => g.incidents.some((i) => i.id === selectedIncidentId)),
    [incidentGroups, selectedIncidentId]
  );
  const isGroupWithMultiple = (selectedGroup?.incidents.length ?? 0) > 1;

  useEffect(() => {
    if (!selectedIncidentId) {
      setSelectedIncident(null);
      return;
    }
    const inGroup = incidentGroups.flatMap((g) => g.incidents).find((i) => i.id === selectedIncidentId);
    if (inGroup) {
      setSelectedIncident(inGroup);
      setDetailLoading(false);
      return;
    }
    setSelectedIncident(null);
    setDetailLoading(true);
    getIncident(selectedIncidentId)
      .then((inc) => {
        if (inc) setSelectedIncident(inc);
        else setSelectedIncidentId(null);
      })
      .finally(() => setDetailLoading(false));
  }, [selectedIncidentId, incidentGroups]);

  useEffect(() => {
    if (selectedIncidentId && incidentGroups.some((g) => g.incidents.some((i) => i.id === selectedIncidentId))) {
      selectedCardRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }
  }, [incidentGroups, selectedIncidentId]);

  // When there are no incidents, clear selection so we don't show "Incident not found."
  useEffect(() => {
    if (incidents.length === 0 && selectedIncidentId) setSelectedIncidentId(null);
  }, [incidents.length, selectedIncidentId]);

  const filterControls = (
    <div className="flex flex-col gap-3">
      <div>
        <label className="block text-xs text-zinc-500 uppercase tracking-wider mb-1">Status</label>
        <select
          className="w-full bg-zinc-800 border border-zinc-700 rounded px-3 py-1.5 text-sm"
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value === "" ? "" : Number(e.target.value))}
        >
          <option value="">All</option>
          <option value="0">Open</option>
          <option value="2">Resolved</option>
        </select>
      </div>
      <div>
        <label className="block text-xs text-zinc-500 uppercase tracking-wider mb-1">Severity</label>
        <select
          className="w-full bg-zinc-800 border border-zinc-700 rounded px-3 py-1.5 text-sm"
          value={severityFilter}
          onChange={(e) => setSeverityFilter(e.target.value)}
        >
          <option value="">All</option>
          <option value="P1">P1</option>
          <option value="P2">P2</option>
          <option value="P3">P3</option>
        </select>
      </div>
      <div>
        <label className="block text-xs text-zinc-500 uppercase tracking-wider mb-1">Search</label>
        <input
          type="text"
          placeholder="Message, service..."
          className="w-full bg-zinc-800 border border-zinc-700 rounded px-3 py-1.5 text-sm placeholder:text-zinc-500"
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
        />
      </div>
      <div>
        <label className="block text-xs text-zinc-500 uppercase tracking-wider mb-1">Service</label>
        <input
          type="text"
          placeholder="e.g. payment-service"
          className="w-full bg-zinc-800 border border-zinc-700 rounded px-3 py-1.5 text-sm placeholder:text-zinc-500"
          value={serviceFilter}
          onChange={(e) => setServiceFilter(e.target.value)}
        />
      </div>
      {!READ_ONLY_MODE && (
        <button
          type="button"
          onClick={handleClear}
          disabled={clearLoading}
          className="px-3 py-1.5 rounded text-sm font-medium bg-zinc-700 hover:bg-zinc-600 disabled:opacity-50"
        >
          {clearLoading ? "..." : "Clear all"}
        </button>
      )}
    </div>
  );

  const statCards = (
    <div className="flex flex-col gap-3">
      <div className="rounded-lg border border-[hsl(var(--accent))]/30 bg-[hsl(var(--accent))]/10 p-4">
        <p className="text-xs text-[hsl(var(--accent))]/90 uppercase tracking-wider">Total incidents</p>
        <p className="text-3xl font-semibold mt-1">{overview.total}</p>
      </div>
      <div className="grid grid-cols-3 gap-3">
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-3">
          <p className="text-xs text-red-400/80 uppercase tracking-wider">P1</p>
          <p className="text-xl font-semibold text-red-400 mt-0.5">{overview.p1}</p>
        </div>
        <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3">
          <p className="text-xs text-amber-400/80 uppercase tracking-wider">P2</p>
          <p className="text-xl font-semibold text-amber-400 mt-0.5">{overview.p2}</p>
        </div>
        <div className="rounded-lg border border-zinc-600 bg-zinc-800/50 p-3">
          <p className="text-xs text-zinc-400 uppercase tracking-wider">P3</p>
          <p className="text-xl font-semibold text-zinc-300 mt-0.5">{overview.p3}</p>
        </div>
      </div>
      {stats != null && (
        <div className="grid grid-cols-2 gap-3">
          <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-3">
            <p className="text-xs text-zinc-500 uppercase tracking-wider">Requests</p>
            <p className="text-lg font-semibold mt-0.5">{stats.totalRequests.toLocaleString()}</p>
          </div>
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-3">
            <p className="text-xs text-red-400/80 uppercase tracking-wider">Errors</p>
            <p className="text-lg font-semibold text-red-400 mt-0.5">{stats.totalErrors.toLocaleString()}</p>
          </div>
        </div>
      )}
    </div>
  );

  return (
    <DashboardRoot>
      <div className="flex-shrink-0 flex flex-wrap items-center justify-between gap-4 mb-6">
        <div className="flex items-center gap-3">
          <span className="inline-block h-2.5 w-2.5 rounded-full bg-[hsl(var(--accent))] shadow-[0_0_10px_hsl(var(--accent))]" />
          <h1 className="text-3xl font-semibold tracking-tight">Dashboard</h1>
        </div>
      </div>

      <div className="flex-1 flex flex-col lg:flex-row gap-6 min-h-0">
        <aside className="lg:w-72 shrink-0 flex flex-col gap-6">
          {statCards}
          <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-4">
            <h2 className="text-xs font-medium text-zinc-400 uppercase tracking-wider mb-3">Filters</h2>
            {filterControls}
          </div>
        </aside>

        <div className="flex-1 min-w-0 flex flex-col gap-4">

      {(streamRunning || logLines.length > 0) && (
        <div className="flex-shrink-0 rounded-lg border border-zinc-800 bg-zinc-900/50 p-4 mb-4">
          <h2 className="text-xs font-medium text-zinc-400 mb-1.5">{streamRunning ? "Live log stream" : "Log stream"}</h2>
          <div
            ref={logStreamRef}
            onScroll={() => {
              const el = logStreamRef.current;
              if (!el) return;
              const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 50;
              isAtBottomRef.current = atBottom;
            }}
            className="h-[120px] overflow-y-auto font-mono text-[11px] text-zinc-400 space-y-0.5"
          >
            {logLines.length === 0 && <div className="text-zinc-500">Waiting for logs...</div>}
            {logLines.map((log) => {
              const t = new Date(log.timestamp);
              const timeStr = t.toLocaleTimeString("en-US", { hour12: false, hour: "2-digit", minute: "2-digit", second: "2-digit" });
              const levelColor = log.level === "error" ? "text-red-400" : log.level === "warn" ? "text-amber-400" : "text-zinc-400";
              return (
                <div key={log.id} className="flex gap-2 items-start">
                  <span className="text-zinc-500 shrink-0">[{timeStr}]</span>
                  <span className={`shrink-0 w-10 ${levelColor}`}>{log.level}</span>
                  <span className="text-zinc-500 shrink-0">{log.service}</span>
                  <span className="min-w-0 whitespace-pre-wrap break-words text-[10px]">— {log.message}</span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {!loading && (
        <div className="flex-shrink-0 space-y-3 mt-4">
          {incidents.length > 0 && (
          <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-3">
            <h2 className="text-xs font-medium mb-2 text-zinc-400">Severity distribution</h2>
            <div className="h-7">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={severityBarData} layout="vertical" margin={{ top: 0, right: 8, bottom: 0, left: 0 }}>
                  <XAxis type="number" hide />
                  <YAxis type="category" dataKey="name" width={32} tick={{ fontSize: 10 }} stroke="#71717a" />
                  <Bar dataKey="count" radius={[0, 2, 2, 0]}>
                    {severityBarData.map((entry, index) => (
                      <Cell key={index} fill={entry.fill} />
                    ))}
                  </Bar>
                </BarChart>
              </ResponsiveContainer>
            </div>
          </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
            {incidents.length > 0 && timelineData.length > 0 && (
              <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-3">
                <h2 className="text-xs font-medium mb-2 text-zinc-400">Incidents over time</h2>
                <div className="h-24">
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={timelineData} margin={{ top: 2, right: 2, bottom: 2, left: 2 }}>
                      <XAxis dataKey="time" tick={{ fontSize: 9 }} stroke="#71717a" />
                      <YAxis tick={{ fontSize: 9 }} stroke="#71717a" width={20} />
                      <Tooltip contentStyle={{ background: "#18181b", border: "1px solid #27272a", fontSize: 11 }} />
                      <Area type="monotone" dataKey="count" stroke="hsl(var(--accent))" fill="hsl(var(--accent))" fillOpacity={0.3} strokeWidth={1.5} />
                    </AreaChart>
                  </ResponsiveContainer>
                </div>
              </div>
            )}
            {incidents.length > 0 && byServiceData.length > 0 && (
              <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-3">
                <h2 className="text-xs font-medium mb-2 text-zinc-400">By service</h2>
                <div className="h-24">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={byServiceData} layout="vertical" margin={{ top: 0, right: 4, bottom: 0, left: 0 }}>
                      <XAxis type="number" hide />
                      <YAxis type="category" dataKey="service" width={90} tick={{ fontSize: 9 }} stroke="#71717a" />
                      <Bar dataKey="count" radius={[0, 2, 2, 0]} fill="hsl(var(--accent))" />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </div>
            )}
            {incidents.length > 0 && statusPieData.length > 0 && (
              <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-3">
                <h2 className="text-xs font-medium mb-2 text-zinc-400">Status</h2>
                <div className="h-24">
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie data={statusPieData} dataKey="value" nameKey="name" cx="50%" cy="50%" innerRadius={20} outerRadius={32} paddingAngle={2}>
                        {statusPieData.map((entry, index) => (
                          <Cell key={index} fill={entry.fill} />
                        ))}
                      </Pie>
                      <Tooltip contentStyle={{ background: "#18181b", border: "1px solid #27272a", fontSize: 11 }} />
                    </PieChart>
                  </ResponsiveContainer>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      <div className="flex flex-col min-w-0">
        <div>
          <h2 className="text-xs font-medium text-zinc-400 mb-2 pr-2">Incidents</h2>

          {loading ? (
            <div className="text-zinc-500">Loading...</div>
          ) : incidents.length === 0 ? (
            <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-8 text-center text-zinc-500">
              {READ_ONLY_MODE ? "No incidents yet." : "No incidents yet. Stream runs automatically."}
            </div>
          ) : (
            <div className="space-y-2">
            {incidentGroups.map((grp) => {
              const isSelected = grp.incidents.some((i) => i.id === selectedIncidentId);
              const rep = grp.representative;
              return (
                <div
                  key={grp.patternKey}
                  ref={isSelected ? selectedCardRef : undefined}
                  className={`rounded-lg border border-l-4 transition-all duration-150 ${severityBorderClass[grp.worstSeverity] ?? "border-l-zinc-600"} ${isSelected ? "border-zinc-600 bg-zinc-800/50" : "border-zinc-800 bg-zinc-900/50 hover:border-zinc-600 hover:bg-zinc-800/70"}`}
                >
                  <button
                    type="button"
                    onClick={() => setSelectedIncidentId((prev) => (prev === rep.id ? null : rep.id))}
                    className="w-full text-left p-3 focus:outline-none focus:ring-0"
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="min-w-0 flex-1">
                        <div className="flex items-center gap-2 flex-wrap mb-1">
                          <span className={`text-xs font-medium px-2 py-0.5 rounded border ${severityBadgeClass(rep)}`}>
                            {grp.worstSeverity}
                          </span>
                          <span className={`text-xs px-2 py-0.5 rounded ${statusColors[rep.status] ?? ""}`}>{rep.status}</span>
                          <span className="text-sm text-zinc-500">{grp.services.join(", ")}</span>
                        </div>
                        <p className="text-sm text-zinc-300 truncate">{grp.patternDisplay}</p>
                        <p className="text-xs text-zinc-500 mt-1">{grp.totalErrors} errors{grp.incidents.length > 1 ? ` · ${grp.incidents.length} incidents` : ""}</p>
                      </div>
                      <IncidentSparkline errorCount={grp.totalErrors} maxCount={maxErrorCount} />
                    </div>
                  </button>
                  {isSelected && (
                    <div className="border-t border-zinc-700 px-3 pb-3 pt-2">
                      {detailLoading ? (
                        <div className="text-zinc-500 py-4">Loading...</div>
                      ) : selectedIncident ? (
                        <div className="space-y-3">
                          <div>
                            <h3 className="text-xs font-medium text-zinc-400 mb-1">Stack trace</h3>
                            <pre className="text-[10px] text-zinc-500 whitespace-pre-wrap font-mono overflow-x-auto max-h-32">{selectedIncident.topErrorPattern}</pre>
                          </div>
                          <div className="h-24">
                            <ResponsiveContainer width="100%" height="100%">
                              <AreaChart data={detailChartData}>
                                <XAxis
                                  dataKey={detailChartData.length > 1 ? "incidentLabel" : "time"}
                                  tick={{ fontSize: 9 }}
                                  stroke="#71717a"
                                  tickFormatter={detailChartData.length > 1 ? undefined : (t) => new Date(t).toLocaleTimeString()}
                                />
                                <YAxis tick={{ fontSize: 9 }} stroke="#71717a" />
                                <Tooltip
                                  contentStyle={{ background: "#18181b", border: "1px solid #27272a", fontSize: 11 }}
                                  labelFormatter={(_: string, payload: { payload?: { time?: string } }[]) =>
                                    payload?.[0]?.payload?.time ? new Date(payload[0].payload.time).toLocaleString() : undefined
                                  }
                                  formatter={(value: number) => [`${value} errors`, "Count"]}
                                />
                                <Area
                                  type="monotone"
                                  dataKey="count"
                                  stroke="hsl(var(--accent))"
                                  fill="hsl(var(--accent))"
                                  fillOpacity={0.3}
                                  dot={(props) => {
                                    const { cx, cy, payload } = props;
                                    if (payload?.id == null || typeof cx !== "number" || typeof cy !== "number")
                                      return <circle key="none" r={0} />;
                                    const dotSelected = payload.id === selectedIncidentId;
                                    return (
                                      <circle
                                        key={payload.id}
                                        cx={cx}
                                        cy={cy}
                                        r={dotSelected ? 5 : 4}
                                        fill="hsl(var(--accent))"
                                        stroke={dotSelected ? "#fff" : "transparent"}
                                        strokeWidth={dotSelected ? 2 : 0}
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          setSelectedIncidentId(payload.id);
                                          const inc = selectedGroup?.incidents.find((i) => i.id === payload.id);
                                          if (inc) setSelectedIncident(inc);
                                        }}
                                        className="cursor-pointer"
                                      />
                                    );
                                  }}
                                  activeDot={false}
                                />
                              </AreaChart>
                            </ResponsiveContainer>
                          </div>
                          {detailChartData.length > 0 && (
                            <p className="text-[10px] text-zinc-500 mt-0.5">
                              {detailChartData.length === 1
                                ? "One incident (point = error count)."
                                : `${detailChartData.length} incidents — click a point to select that incident (height = error count).`}
                            </p>
                          )}
                          <div>
                            <h3 className="text-xs font-medium text-zinc-400 mb-1">AI Summary</h3>
                            <p className="text-sm text-zinc-300">{selectedIncident.summary}</p>
                          </div>
                          <div>
                            <h3 className="text-xs font-medium text-zinc-400 mb-1">Suggested steps</h3>
                            <ul className="list-disc list-inside space-y-0.5 text-sm text-zinc-300">
                              {selectedIncident.suggestedSteps?.map((step, i) => (
                                <li key={i}>{step}</li>
                              ))}
                            </ul>
                          </div>
                          {!READ_ONLY_MODE && (
                            <div className="flex gap-2 flex-wrap">
                              <button
                                type="button"
                                onClick={handleReanalyze}
                                disabled={reanalyzing}
                                className="px-3 py-1.5 rounded text-sm bg-zinc-700 hover:bg-zinc-600 disabled:opacity-50"
                              >
                                {reanalyzing ? "Reanalyzing..." : "Reanalyze"}
                              </button>
                              {selectedIncident.status === "Resolved" ? (
                                <>
                                  <button
                                    type="button"
                                    onClick={() => handleStatusUpdate("selected", 0)}
                                    disabled={markingComplete}
                                    className="px-3 py-1.5 rounded text-sm bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50"
                                  >
                                    {markingComplete ? "..." : "Reopen"}
                                  </button>
                                  {isGroupWithMultiple && (
                                    <button
                                      type="button"
                                      onClick={() => handleStatusUpdate("all", 0)}
                                      disabled={markingComplete}
                                      className="px-3 py-1.5 rounded text-sm bg-zinc-600 hover:bg-zinc-500 disabled:opacity-50"
                                    >
                                      {markingComplete ? "..." : "Reopen all"}
                                    </button>
                                  )}
                                </>
                              ) : (
                                <>
                                  <button
                                    type="button"
                                    onClick={() => handleStatusUpdate("selected", 2)}
                                    disabled={markingComplete}
                                    className="px-3 py-1.5 rounded text-sm bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50"
                                  >
                                    {markingComplete ? "..." : "Mark complete"}
                                  </button>
                                  {isGroupWithMultiple && (
                                    <button
                                      type="button"
                                      onClick={() => handleStatusUpdate("all", 2)}
                                      disabled={markingComplete}
                                      className="px-3 py-1.5 rounded text-sm bg-zinc-600 hover:bg-zinc-500 disabled:opacity-50"
                                    >
                                      {markingComplete ? "..." : "Mark all complete"}
                                    </button>
                                  )}
                                </>
                              )}
                            </div>
                          )}
                        </div>
                      ) : (
                        <div className="text-zinc-500 py-4">Incident not found.</div>
                      )}
                    </div>
                  )}
                </div>
              );
            })}
            </div>
          )}
        </div>
        </div>
      </div>
      </div>
    </DashboardRoot>
  );
}
