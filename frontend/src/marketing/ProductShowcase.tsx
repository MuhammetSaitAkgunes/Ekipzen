import { Link } from "react-router-dom";
import { useEffect, useRef, useState, type KeyboardEvent } from "react";
import { SHOWCASE_ORDER, modules } from "./content";
import { StatusBadge } from "./StatusBadge";
import { ModuleVisual } from "./visuals";
import { useAutoAdvance } from "./useAutoAdvance";
import { AutoAdvanceControl } from "./AutoAdvanceControl";

const items = SHOWCASE_ORDER.map((id) => {
  const found = modules.find((m) => m.id === id);
  if (!found) throw new Error(`Vitrin modülü bulunamadı: ${id}`);
  return found;
});

export function ProductShowcase({ intervalMs = 6000 }: { intervalMs?: number }) {
  const playback = useAutoAdvance(items.length, intervalMs);
  const { active, running } = playback;
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const tabsRef = useRef<HTMLDivElement>(null);
  const [overflow, setOverflow] = useState(false);

  useEffect(() => {
    const tabs = tabsRef.current;
    if (!tabs) return;
    let disposed = false;
    const update = () => { if (!disposed) setOverflow(tabs.scrollWidth > tabs.clientWidth + 1); };
    update();
    const observer = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(update);
    observer?.observe(tabs);
    window.addEventListener("resize", update);
    document.fonts?.ready.then(update);
    return () => { disposed = true; observer?.disconnect(); window.removeEventListener("resize", update); };
  }, []);

  useEffect(() => {
    const tabs = tabsRef.current;
    const selected = tabRefs.current[active];
    if (!tabs || !selected) return;
    const container = tabs.getBoundingClientRect();
    const button = selected.getBoundingClientRect();
    if (button.left < container.left) tabs.scrollLeft += button.left - container.left;
    else if (button.right > container.right) tabs.scrollLeft += button.right - container.right;
  }, [active, overflow]);

  const select = (index: number, moveFocus = false) => {
    playback.select(index);
    if (moveFocus) tabRefs.current[index]?.focus({ preventScroll: true });
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

  const current = items[active];
  return (
    <div className="ez-showcase" {...playback.interactionProps}>
      <div ref={tabsRef} role="tablist" aria-label="Modüller" className="ez-showcase__tabs" onKeyDown={onKeyDown}>
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
      <div className="ez-showcase__controls">
        {overflow && <div className="ez-showcase__arrows" aria-label="Modül seçenekleri">
          <button type="button" aria-label="Önceki modül" disabled={active === 0} onClick={() => select(active - 1)}><i aria-hidden="true" className="fa-solid fa-arrow-left" /></button>
          <button type="button" aria-label="Sonraki modül" disabled={active === items.length - 1} onClick={() => select(active + 1)}><i aria-hidden="true" className="fa-solid fa-arrow-right" /></button>
        </div>}
        <AutoAdvanceControl enabled={playback.enabled} reduced={playback.reduced} onToggle={playback.toggle} />
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
