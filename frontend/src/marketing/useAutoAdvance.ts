import { useEffect, useState, type FocusEvent } from "react";
import { usePrefersReducedMotion } from "./motion";

/** Explicit pause and temporary pointer/focus pauses are independent. */
export function useAutoAdvance(count: number, intervalMs: number) {
  const [active, setActive] = useState(0);
  const [enabled, setEnabled] = useState(true);
  const [hovered, setHovered] = useState(false);
  const [focused, setFocused] = useState(false);
  const reduced = usePrefersReducedMotion();
  const running = enabled && !hovered && !focused && !reduced;

  useEffect(() => {
    if (!running) return;
    const timer = window.setInterval(() => setActive((index) => (index + 1) % count), intervalMs);
    return () => window.clearInterval(timer);
  }, [count, intervalMs, running]);

  return {
    active, enabled, reduced, running,
    select(index: number) { setActive(index); setEnabled(false); },
    toggle() { setEnabled((value) => !value); },
    interactionProps: {
      onMouseEnter: () => setHovered(true),
      onMouseLeave: () => setHovered(false),
      onFocus: () => setFocused(true),
      onBlur: (event: FocusEvent<HTMLDivElement>) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setFocused(false);
      },
    },
  };
}
