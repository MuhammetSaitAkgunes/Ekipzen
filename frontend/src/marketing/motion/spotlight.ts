import type { PointerEvent } from "react";

/** Kart ışığı: imleç konumunu --mx/--my değişkenlerine yazar (.ez-spotlight kullanır). */
export function onSpotlightMove(event: PointerEvent<HTMLElement>) {
  const el = event.currentTarget;
  const rect = el.getBoundingClientRect();
  el.style.setProperty("--mx", `${event.clientX - rect.left}px`);
  el.style.setProperty("--my", `${event.clientY - rect.top}px`);
}
