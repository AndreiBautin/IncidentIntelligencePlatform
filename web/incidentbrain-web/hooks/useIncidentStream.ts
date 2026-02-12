"use client";

import { useEffect, useRef, useState } from "react";
import { getIncidentStreamUrl } from "@/lib/api";

export function useIncidentStream(onEvent?: (data: { type?: string; id?: string }) => void) {
  const [connected, setConnected] = useState(false);
  const eventSourceRef = useRef<EventSource | null>(null);
  const onEventRef = useRef(onEvent);
  onEventRef.current = onEvent;

  useEffect(() => {
    const url = getIncidentStreamUrl();
    const es = new EventSource(url);
    eventSourceRef.current = es;
    es.onopen = () => setConnected(true);
    es.onerror = () => setConnected(false);
    es.onmessage = (e) => {
      try {
        const data = JSON.parse(e.data || "{}");
        onEventRef.current?.(data);
      } catch {}
    };
    return () => {
      es.close();
      eventSourceRef.current = null;
      setConnected(false);
    };
  }, []);

  return { connected };
}
