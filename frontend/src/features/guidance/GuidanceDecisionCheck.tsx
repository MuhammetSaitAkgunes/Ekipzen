import { useId, useState } from "react";
import { PublishedGuidancePractice } from "./PublishedGuidancePractice";
import {
  useGuidanceDecisionCheck, useSubmitGuidanceDecisionCheck,
  type GuidanceDecisionCheck as Check, type GuidanceDecisionProcess,
} from "./decisionCheckQueries";

export function GuidanceDecisionCheck({ process, step, operationName, eager = true }: {
  process: GuidanceDecisionProcess; step: string; operationName: string; eager?: boolean;
}) {
  const inputName = useId();
  const [open, setOpen] = useState(false);
  const [selected, setSelected] = useState<number | null>(null);
  const [submitted, setSubmitted] = useState<Check | null>(null);
  const query = useGuidanceDecisionCheck(process, step, eager || open);
  const submit = useSubmitGuidanceDecisionCheck();
  const shown = submitted ?? (selected === null ? query.data : null);

  return <><PublishedGuidancePractice process={process === "employee-entry" ? "personnel-entry" : process === "leave-approval" ? "leave" : process} step={step} />
  <details className="ez-personnel-decision-check"
    onToggle={(event) => setOpen(event.currentTarget.open)}>
    <summary>Kısa karar alıştırması</summary>
    <p>Bu bölümün yanıtları kaynaklı sürüm alıştırmalarından ayrı izlenir.</p>
    {query.isPending ? <p>Alıştırma yükleniyor.</p> : query.isError
      ? <p role="alert">Alıştırma yüklenemedi. Daha sonra yeniden deneyin.</p>
      : <>
        <fieldset>
          <legend>{query.data.question}</legend>
          {query.data.choices.map((choice, index) => <label key={choice}>
            <input type="radio" name={`guidance-check-${process}-${step}-${inputName}`}
              checked={(selected ?? query.data.lastSelectedChoice) === index}
              onChange={() => { setSelected(index); setSubmitted(null); }} />
            {choice}
          </label>)}
        </fieldset>
        <button type="button" className="btn btn-secondary btn-sm"
          disabled={selected === null || submit.isPending}
          onClick={async () => {
            if (selected === null) return;
            try {
              setSubmitted(await submit.mutateAsync({ process, step, selectedChoice: selected }));
              setSelected(null);
            } catch {
              // Hata mesajı mutation durumundan gösterilir; önceki kayıt korunur.
            }
          }}>Yanıtı kontrol et</button>
        {submit.isError && <p role="alert">Yanıt kaydedilemedi. Tekrar deneyin.</p>}
        {shown?.lastCorrect !== null && shown?.lastCorrect !== undefined &&
          <p role="status">
            <strong>{shown.lastCorrect ? "Doğru karar." : "Bir daha düşünün."}</strong>{" "}
            {shown.explanation}
          </p>}
        <small>
          {query.data.attemptCount > 0 ? `${query.data.attemptCount} kayıtlı deneme. ` : ""}
          Bu alıştırma öğrenme takibidir; gerçek {operationName} kanıtı veya yeterlilik onayı değildir.
        </small>
      </>}
  </details></>;
}
