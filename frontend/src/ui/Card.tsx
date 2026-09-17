import type { ElementType, ReactNode } from "react";

export function Card({ as: Tag = "div", className, children }: { as?: ElementType; className?: string; children: ReactNode }) {
  return <Tag className={["ez-card", className].filter(Boolean).join(" ")}>{children}</Tag>;
}

export function Badge({ tone = "neutral", children }: { tone?: "neutral" | "primary" | "success" | "warning" | "danger"; children: ReactNode }) {
  return <span className={`ez-badge ez-badge--${tone}`}>{children}</span>;
}
