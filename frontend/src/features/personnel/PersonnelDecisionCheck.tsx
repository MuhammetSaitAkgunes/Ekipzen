import { useState } from "react";
import type { PersonnelGuideStep } from "./PersonnelGuide";
import {
  usePersonnelDecisionCheck, useSubmitPersonnelDecisionCheck,
  type PersonnelDecisionCheck as Check,
} from "./guidanceQueries";

export function PersonnelDecisionCheck({ step }: { step: PersonnelGuideStep }) {
  const [selected, setSelected] = useState<number | null>(null);
  const [submitted, setSubmitted] = useState<Check | null>(null);
  const query = usePersonnelDecisionCheck(step);
  const submit = useSubmitPersonnelDecisionCheck();

  return <details className="ez-personnel-decision-check">
    <summary>Kısa karar alıştırması</summary>
    {query.isPending ? <p>Alıştırma yükleniyor.</p> : query.isError
      ? <p role="alert">Alıştırma yüklenemedi. Daha sonra yeniden deneyin.</p>
      : <>
        <fieldset>
          <legend>{query.data.question}</legend>
          {query.data.choices.map((choice, index) => <label key={choice}>
            <input type="radio" name={`personnel-check-${step}`}
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
              setSubmitted(await submit.mutateAsync({ step, selectedChoice: selected }));
              setSelected(null);
            } catch {
              // Hata mesajı mutation durumundan gösterilir; önceki kayıt korunur.
            }
          }}>Yanıtı kontrol et</button>
        {submit.isError && <p role="alert">Yanıt kaydedilemedi. Tekrar deneyin.</p>}
        {(submitted ?? (selected === null ? query.data : null))?.lastCorrect !== null &&
          (submitted ?? (selected === null ? query.data : null))?.lastCorrect !== undefined &&
          <p role="status">
            <strong>{(submitted ?? query.data).lastCorrect ? "Doğru karar." : "Bir daha düşünün."}</strong>{" "}
            {(submitted ?? query.data).explanation}
          </p>}
        <small>
          {query.data.attemptCount > 0 ? `${query.data.attemptCount} kayıtlı deneme. ` : ""}
          Bu alıştırma öğrenme takibidir; gerçek personel işlemi kanıtı veya yeterlilik onayı değildir.
        </small>
      </>}
  </details>;
}
