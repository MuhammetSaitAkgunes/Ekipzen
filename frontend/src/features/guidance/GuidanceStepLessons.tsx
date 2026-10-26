import { useId, useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { processTitles } from "./catalogQueries";
import { guidanceStepAudience, guidanceStepTitles } from "./stepDefinitions";
import { PublishedGuidancePractice } from "./PublishedGuidancePractice";
import { navigateStepTab } from "./stepTabNavigation";

export function GuidanceStepLessons({ process }: { process: string }) {
  const { user } = useAuth(); const id = useId();
  const [selected, setSelected] = useState<string | null>(null);
  const steps = Object.entries(guidanceStepTitles[process] ?? {}).filter(([step]) => guidanceStepAudience(process, step).some(role => role === user?.role));
  if (!steps.length) return null;
  const active = steps.find(([step]) => step === selected) ?? steps[0];
  return <section className="ez-process-lessons ez-panel" aria-label={`${processTitles[process]} adım rehberi`}>
    <div className="ez-panel-heading"><div><span className="ez-overline">ADIM ADIM ÖĞREN</span><h2>{processTitles[process]}</h2><p>İlgili adımı seçin, açıklamayı inceleyin ve gerçek işlemin sonucunu kontrol edin.</p></div></div>
    <div className="ez-step-tabs" role="tablist" aria-label={`${processTitles[process]} adımları`}>{steps.map(([step, title], index) => <button type="button" role="tab" tabIndex={step === active[0] ? 0 : -1} onKeyDown={event => navigateStepTab(event, index, steps.length, next => setSelected(steps[next][0]))} id={`${id}-${step}`} aria-selected={step === active[0]} aria-controls={`${id}-panel`} key={step} onClick={() => setSelected(step)}><span>{index + 1}</span>{title}</button>)}</div>
    <div role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-${active[0]}`}><h3>{active[1]}</h3><PublishedGuidancePractice initiallyOpen key={`${process}/${active[0]}`} process={process} step={active[0]} /></div>
  </section>;
}
