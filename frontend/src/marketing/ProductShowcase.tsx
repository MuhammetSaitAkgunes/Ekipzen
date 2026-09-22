import { Link } from "react-router-dom";
import { useEffect, useRef, useState, type FocusEvent, type KeyboardEvent } from "react";
import { SHOWCASE_ORDER, modules } from "./content";
import { StatusBadge } from "./StatusBadge";
import { ModuleVisual } from "./visuals";
import { usePrefersReducedMotion } from "./motion";

const items = SHOWCASE_ORDER.map((id) => {
  const found = modules.find((m) => m.id === id);
  if (!found) throw new Error(`Vitrin modülü bulunamadı: ${id}`);
  return found;
});

export function ProductShowcase({ intervalMs = 6000 }: { intervalMs?: number }) {
  const [active, setActive] = useState(0);
  const [hovered, setHovered] = useState(false);
  const [userSelected, setUserSelected] = useState(false);
  const reduced = usePrefersReducedMotion();
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const running = !reduced && !hovered && !userSelected;

  useEffect(() => {
    if (!running) return;
    const timer = window.setInterval(() => setActive((i) => (i + 1) % items.length), intervalMs);
    return () => window.clearInterval(timer);
  }, [running, intervalMs]);

  const select = (index: number, moveFocus = false) => {
    setActive(index);
    setUserSelected(true);
    if (moveFocus) tabRefs.current[index]?.focus();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const last = items.length - 1;
    const next =
      event.key === "ArrowRight" ? (active === last ? 0 : active + 1)
      : event.key === "ArrowLeft" ? (active === 0 ? last : active - 1)
      : event.key === "Home" ? 0
      : event.key === "End" ? last
      : null;
    if (next === null) return;
    event.preventDefault();
    select(next, true);
  };

  const onBlur = (event: FocusEvent<HTMLDivElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setHovered(false);
  };

  const current = items[active];
  return (
    <div className="ez-showcase" onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)} onFocus={() => setHovered(true)} onBlur={onBlur}>
      <div role="tablist" aria-label="Modüller" className="ez-showcase__tabs" onKeyDown={onKeyDown}>
        {items.map((m, i) => (
          <button
            key={m.id}
            ref={(el) => { tabRefs.current[i] = el; }}
            type="button"
            role="tab"
            id={`showcase-tab-${m.id}`}
            aria-selected={i === active}
            aria-controls="showcase-panel"
            tabIndex={i === active ? 0 : -1}
            className="ez-showcase__tab"
            onClick={() => select(i)}
          >
            <i aria-hidden="true" className={`fa-solid ${m.icon}`} />
            <span>{m.title}</span>
            {i === active && running && <span className="ez-showcase__progress" style={{ animationDuration: `${intervalMs}ms` }} aria-hidden="true" />}
          </button>
        ))}
      </div>
      <div role="tabpanel" id="showcase-panel" aria-labelledby={`showcase-tab-${current.id}`} tabIndex={0} className="ez-showcase__panel ez-spotlight">
        <div key={current.id} className="ez-showcase__content">
          <div className="ez-showcase__copy">
            <StatusBadge status={current.status} />
            <h3>{current.title}</h3>
            <p>{current.summary}</p>
            <ul className="ez-check-list">{current.points.map((p) => <li key={p}>{p}</li>)}</ul>
            <Link className="ez-link" to={`/ozellikler#${current.id}`}>{current.title} detayları</Link>
          </div>
          <div className="ez-showcase__screen"><ModuleVisual id={current.id} /></div>
        </div>
      </div>
    </div>
  );
}
