import { useEffect, useRef, useState, type FocusEvent, type KeyboardEvent } from "react";
import { journeySteps } from "./content";
import { usePrefersReducedMotion } from "./motion";

/**
 * Çalışan yolculuğu: adımlar kendi ritminde ilerler (kaydırmaya bağlı değil).
 * İmleç üzerindeyken, odaklanıldığında ya da kullanıcı bir adım seçtiğinde durur.
 */
export function EmployeeJourney({ intervalMs = 5000 }: { intervalMs?: number }) {
  const [active, setActive] = useState(0);
  const [paused, setPaused] = useState(false);
  const [userSelected, setUserSelected] = useState(false);
  const reduced = usePrefersReducedMotion();
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const running = !reduced && !paused && !userSelected;

  useEffect(() => {
    if (!running) return;
    const timer = window.setInterval(() => setActive((i) => (i + 1) % journeySteps.length), intervalMs);
    return () => window.clearInterval(timer);
  }, [running, intervalMs]);

  const select = (index: number, moveFocus = false) => {
    setActive(index);
    setUserSelected(true);
    if (moveFocus) tabRefs.current[index]?.focus();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const last = journeySteps.length - 1;
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

  const step = journeySteps[active];
  const progress = (active + 1) / journeySteps.length;

  return (
    <div
      className="ez-journey"
      onPointerEnter={() => setPaused(true)}
      onPointerLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={(event: FocusEvent<HTMLDivElement>) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setPaused(false);
      }}
    >
      <div className="ez-journey__rail">
        <span className="ez-journey__track" aria-hidden="true">
          <span className="ez-journey__fill" style={{ transform: `scaleX(${progress})` }} />
        </span>
        <div role="tablist" aria-label="Çalışan yolculuğu adımları" className="ez-journey__tabs" onKeyDown={onKeyDown}>
          {journeySteps.map((s, i) => (
            <button
              key={s.id}
              ref={(el) => { tabRefs.current[i] = el; }}
              type="button"
              role="tab"
              id={`journey-tab-${s.id}`}
              aria-selected={i === active}
              aria-controls="journey-panel"
              tabIndex={i === active ? 0 : -1}
              className={`ez-journey__tab${i === active ? " is-active" : ""}${i < active ? " is-done" : ""}`}
              onClick={() => select(i)}
            >
              <span className="ez-journey__num" aria-hidden="true">{i + 1}</span>
              <span>{s.title}</span>
            </button>
          ))}
        </div>
      </div>

      <div id="journey-panel" role="tabpanel" aria-labelledby={`journey-tab-${step.id}`} tabIndex={0} className="ez-journey__panel">
        <figure className="ez-journey__card" aria-label="Örnek personel kaydı">
          <div className="ez-journey__person">
            <span className="ez-app__avatar ez-app__avatar--0" aria-hidden="true">AY</span>
            <div><strong>Ahmet Yılmaz</strong><span>Yazılım · örnek kayıt</span></div>
          </div>
          <dl className="ez-journey__fields">
            {journeySteps.map((s, i) => (
              <div key={s.id} className={i <= active ? "is-filled" : undefined}>
                <dt>{s.field.label}</dt>
                <dd>{i <= active ? s.field.value : "—"}</dd>
              </div>
            ))}
          </dl>
        </figure>

        <div className="ez-journey__detail">
          <p className="ez-journey__stage">Adım {active + 1} / {journeySteps.length}</p>
          <h3>{step.title}</h3>
          <p className="ez-lead">{step.text}</p>
          <p className="ez-journey__field-note">
            <span>{step.field.label}</span>
            <strong>{step.field.value}</strong>
          </p>
        </div>
      </div>
    </div>
  );
}
