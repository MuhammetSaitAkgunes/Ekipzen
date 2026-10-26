import { useId, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { usePublishedPractice, usePublishedStep, useSubmitPublishedPractice } from "./publishedPracticeQueries";
import { GuidanceStepExplanation } from "./GuidanceStepExplanation";

function PinnedPractice({ id }: { id: number }) {
  const { user } = useAuth();
  const name = useId();
  const query = usePublishedPractice(id, user?.id);
  const submit = useSubmitPublishedPractice(user?.id);
  // İstek yeniden gönderilirse aynı kimlik ve beklenen sayaç kullanılır; mükerrer deneme üretilmez.
  const [answer, setAnswer] = useState<{ selectedChoice: number; expectedAttemptCount: number; attemptId: string } | null>(null);
  if (query.isPending) return <p>Seçilen alıştırma sürümü yükleniyor.</p>;
  if (query.isError) return <p role="alert">{query.error.message} <button type="button" onClick={() => void query.refetch()}>Alıştırmayı yeniden yükle</button></p>;
  const shown = submit.data ?? query.data;
  return <section aria-label="Seçilen sürüm alıştırması">
    <p>{shown.title} · Sürüm {shown.contentVersion}</p>
    <Link target="_blank" rel="noopener noreferrer" to={appPath(`/guidance/catalog?version=${id}`)}>Bu sürümün açıklama ve dayanağını aç</Link>
    <GuidanceStepExplanation id={id} />
    {!query.data.isCurrent && <p role="status">Bu sürüm güncel değil. Yeni deneme kaydedilemez; güncel adımı yeniden yükleyin.</p>}
    <fieldset disabled={submit.isPending || !query.data.isCurrent}>
      <legend>{shown.question}</legend>
      {shown.choices.map((choice, index) => <label key={index} className="ez-guidance-inline">
        <input type="radio" name={name} checked={(answer?.selectedChoice ?? shown.lastSelectedChoice) === index}
          onChange={() => { submit.reset(); setAnswer({ selectedChoice: index, expectedAttemptCount: query.data.attemptCount, attemptId: crypto.randomUUID() }); }} />{choice}
      </label>)}
    </fieldset>
    <button type="button" className="btn btn-secondary" disabled={!answer || submit.isPending || !query.data.isCurrent}
      onClick={async () => {
        if (!answer) return;
        try { await submit.mutateAsync({ id, ...answer }); setAnswer(null); } catch { /* Yanıt ve deneme kimliği korunur. */ }
      }}>Sürümlü yanıtı kaydet</button>
    {submit.isError && <p role="alert">{submit.error.message} <button type="button" onClick={() => void query.refetch()}>Kaydı yeniden yükle</button></p>}
    {shown.lastCorrect !== null && <p role="status">{shown.lastCorrect ? "Doğru karar." : "Bir daha düşünün."} {shown.explanation}</p>}
    <p>{shown.attemptCount} kayıtlı deneme. Yanıtınız bu içerik sürümüne aittir; gerçek işlem veya yeterlilik onayı değildir.</p>
  </section>;
}

export function PublishedGuidancePractice({ process, step, initiallyOpen = false }: { process: string; step: string; initiallyOpen?: boolean }) {
  const { user } = useAuth();
  const [open, setOpen] = useState(initiallyOpen);
  const [pinned, setPinned] = useState<number | null>(null);
  const [sequence, setSequence] = useState(0);
  const current = usePublishedStep(process, step, user?.id, open);
  return <details open={open} className="ez-personnel-decision-check" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>Kaynaklı adım ve sürümlü alıştırma</summary>
    {open && <>
      {current.isPending ? <p>Güncel kaynaklı adım yükleniyor.</p> : current.isError ? <p role="alert">{current.error.message}</p>
        : !current.data ? <p>Bu alt adım için incelenmiş alıştırma henüz yayımlanmadı. Süreçteki gerçek görevinizi ilgili ekrandan sürdürün.</p>
          : <><p>Güncel içerik: {current.data.title} · Sürüm {current.data.contentVersion}</p>
            <button type="button" className="btn btn-secondary" onClick={() => { setPinned(current.data!.contentRevisionId); setSequence(x => x + 1); }}>
              {pinned ? "Güncel sürümü açıkça seç" : "Bu sürümle alıştırmayı aç"}</button></>}
      <button type="button" className="btn btn-secondary" onClick={() => void current.refetch()}>Güncel adımı yeniden yükle</button>
      {pinned !== null && <PinnedPractice key={`${user?.id}:${pinned}:${sequence}`} id={pinned} />}
    </>}
  </details>;
}
