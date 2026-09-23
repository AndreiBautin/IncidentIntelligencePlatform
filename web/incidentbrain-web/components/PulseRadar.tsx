/**
 * Concentric rings expanding outward on a loop, the same visual grammar as a radar sweep or a
 * heartbeat monitor - both readings of "live" that fit a tool whose whole job is watching a
 * stream for something happening right now. Purely decorative (aria-hidden): the actual state is
 * the number it sits behind, this only tells you the number is being watched, not read from it.
 */
export function PulseRadar() {
  return (
    <div
      aria-hidden="true"
      className="pointer-events-none absolute -right-6 -top-6 h-28 w-28 motion-reduce:hidden"
    >
      <span className="absolute inset-0 rounded-full border border-[hsl(var(--accent))]/40 motion-safe:animate-[radar-ping_2.6s_cubic-bezier(0,0,0.2,1)_infinite]" />
      <span className="absolute inset-0 rounded-full border border-[hsl(var(--accent))]/40 motion-safe:animate-[radar-ping_2.6s_cubic-bezier(0,0,0.2,1)_infinite] [animation-delay:0.9s]" />
      <span className="absolute inset-0 rounded-full border border-[hsl(var(--accent))]/40 motion-safe:animate-[radar-ping_2.6s_cubic-bezier(0,0,0.2,1)_infinite] [animation-delay:1.8s]" />
    </div>
  );
}
