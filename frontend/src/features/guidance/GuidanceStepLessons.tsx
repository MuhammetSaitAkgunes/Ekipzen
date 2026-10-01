import { useState } from "react";
import { useAuth } from "../../auth/AuthContext";
import { processTitles } from "./catalogQueries";
import { guidanceStepAudience, guidanceStepTitles } from "./stepDefinitions";
import { PublishedGuidancePractice } from "./PublishedGuidancePractice";

export function GuidanceStepLessons({ process }: { process: string }) {
  const { user } = useAuth();
  const [open, setOpen] = useState(false);
  const steps = Object.entries(guidanceStepTitles[process] ?? {}).filter(([step]) =>
    guidanceStepAudience(process, step).some(role => role === user?.role));
  if (!steps.length) return null;
  return <section className="ez-panel" aria-label={`${processTitles[process]} adım rehberi`}>
    <details onToggle={event => setOpen(event.currentTarget.open)}>
      <summary>{processTitles[process]} · Adım adım öğren</summary>
      <p>Önce ilgili adımın açıklamasını ve örneğini inceleyin, sonra karar alıştırmasını yanıtlayın. Gerçek işlemi ilgili ekranda ayrıca yapın ve sonucunu doğrulayın.</p>
      {open && <ol>{steps.map(([step, title]) => <li key={step}>
        <h4>{title}</h4><PublishedGuidancePractice process={process} step={step} />
      </li>)}</ol>}
    </details>
  </section>;
}
