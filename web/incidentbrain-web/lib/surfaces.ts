/**
 * A small, deliberate set of surface levels, used instead of every card reaching for its own
 * ad-hoc `bg-zinc-900/50 border-zinc-800` - so elevation reads as a designed system rather than
 * Tailwind's un-opinionated defaults repeated in twenty places.
 *
 * - `SURFACE_BASE`: an ordinary card sitting on the page background (the incident list, chart
 *   panels).
 * - `SURFACE_RAISED`: a card that should read as the page's focal point - a top highlight and a
 *   visible shadow, so it sits physically above the base surfaces around it (the hero stat card).
 */
export const SURFACE_BASE =
  "rounded-lg border border-zinc-800 bg-gradient-to-b from-zinc-900/70 to-zinc-900/40 shadow-sm shadow-black/20";

export const SURFACE_RAISED =
  "rounded-lg border border-[hsl(var(--accent))]/30 bg-gradient-to-b from-[hsl(var(--accent))]/15 to-[hsl(var(--accent))]/5 shadow-lg shadow-[hsl(var(--accent))]/10";
