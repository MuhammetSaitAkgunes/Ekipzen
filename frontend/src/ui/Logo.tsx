export function Logo({ variant = "full", tone = "dark", className }: { variant?: "full" | "mark"; tone?: "dark" | "light"; className?: string }) {
  return (
    <span role="img" aria-label="Ekipzen" className={["ez-logo", `ez-logo--${tone}`, className].filter(Boolean).join(" ")}>
      <svg className="ez-logo__mark" viewBox="0 0 32 32" width="32" height="32" aria-hidden="true">
        <rect className="ez-logo__bg" width="32" height="32" rx="9" />
        <path className="ez-logo__bars" d="M10 9.5h12M10 16h8M10 22.5h12" strokeWidth="3" strokeLinecap="round" fill="none" />
        <circle className="ez-logo__dot" cx="23" cy="16" r="2.2" />
      </svg>
      {variant === "full" && <span className="ez-logo__word" aria-hidden="true">Ekipzen</span>}
    </span>
  );
}
