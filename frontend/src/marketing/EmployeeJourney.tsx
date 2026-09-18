import { useCallback, useEffect, useState } from "react";
import { journeySteps } from "./content";
import { useInView } from "./motion";

type Step = (typeof journeySteps)[number];

function JourneyStep({ step, index, active, onActive }: { step: Step; index: number; active: boolean; onActive: (i: number) => void }) {
  const { ref, inView } = useInView<HTMLLIElement>({ threshold: 0.6, rootMargin: "-35% 0px -35% 0px" });
  useEffect(() => { if (inView) onActive(index); }, [inView, index, onActive]);
  return (
    <li ref={ref} className={`ez-journey__step${active ? " is-active" : ""}`} aria-current={active ? "step" : undefined}>
      <span className="ez-journey__dot" aria-hidden="true">{index + 1}</span>
      <div>
        <h3>{step.title}</h3>
        <p>{step.text}</p>
      </div>
    </li>
  );
}

export function EmployeeJourney() {
  const [active, setActive] = useState(0);
  const onActive = useCallback((i: number) => setActive(i), []);
  return (
    <div className="ez-journey">
      <div className="ez-journey__sticky">
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
      </div>
      <ol className="ez-journey__steps">
        {journeySteps.map((s, i) => <JourneyStep key={s.id} step={s} index={i} active={i === active} onActive={onActive} />)}
      </ol>
    </div>
  );
}
