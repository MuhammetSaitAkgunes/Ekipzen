import type { KeyboardEvent } from "react";

export function navigateStepTab(event: KeyboardEvent<HTMLButtonElement>, current: number, count: number, select: (index: number) => void) {
  const index = event.key === "ArrowRight" ? (current + 1) % count : event.key === "ArrowLeft" ? (current - 1 + count) % count
    : event.key === "Home" ? 0 : event.key === "End" ? count - 1 : null;
  if (index === null) return;
  event.preventDefault(); select(index);
  event.currentTarget.parentElement?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[index]?.focus();
}
