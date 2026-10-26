import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { NOTE_TYPES } from "./format";
import { useCandidateEvidenceHistory, useReviseCandidateEvidence,
  type CandidateDetailDto, type CandidateEvaluationDto, type CandidateEvidenceInput,
  type CandidateEvidenceState, type InterviewNoteDto } from "./queries";

export type EvidenceSelection = { kind: "notes"; record: InterviewNoteDto }
  | { kind: "evaluations"; record: CandidateEvaluationDto };

function StateView({ state }: { state: CandidateEvidenceState }) {
  return state.text !== null ? <><p><strong>Görüşme türü:</strong> {state.noteType}</p><p>{state.text}</p></>
    : <p><strong>{state.criterion}:</strong> {state.score}/{state.maxScore}</p>;
}

export function CandidateEvidenceModal({ candidate, selection, onClose }: {
  candidate: CandidateDetailDto; selection: EvidenceSelection; onClose: () => void;
}) {
  const { showToast } = useToast();
  const [source] = useState({ candidate, selection });
  const isNote = source.selection.kind === "notes";
  const recordId = source.selection.record.id!;
  const label = isNote ? "Mülakat notu" : "Değerlendirme";
  const history = useCandidateEvidenceHistory(source.candidate.id, source.selection.kind, recordId);
  const revise = useReviseCandidateEvidence();
  const [form, setForm] = useState(() => source.selection.kind === "notes"
    ? { noteType: source.selection.record.noteType ?? NOTE_TYPES[0], text: source.selection.record.text ?? "",
      criterion: "", score: "", maxScore: "5", reason: "" }
    : { noteType: NOTE_TYPES[0], text: "", criterion: source.selection.record.criterion ?? "",
      score: String(source.selection.record.score ?? 0), maxScore: String(source.selection.record.maxScore ?? 5), reason: "" });
  const [review, setReview] = useState<CandidateEvidenceInput | null>(null);
  const [guidanceSource, setGuidanceSource] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const readOnly = candidate.status === "İşe Alındı";
  const current: CandidateEvidenceState = source.selection.kind === "notes"
    ? { noteType: source.selection.record.noteType ?? null, text: source.selection.record.text ?? "",
      criterion: null, score: null, maxScore: null }
    : { noteType: null, text: null, criterion: source.selection.record.criterion ?? null,
      score: source.selection.record.score ?? 0, maxScore: source.selection.record.maxScore ?? 5 };
  const set = (key: keyof typeof form, value: string) => {
    setForm(previous => ({ ...previous, [key]: value })); setReview(null); setError(null);
  };
  const prepare = () => {
    setError(null);
    if (!form.reason.trim() || form.reason.trim().length > 2000) {
      setError("Düzeltme gerekçesini ve dayanağını 1-2000 karakter arasında yazın."); return;
    }
    if (isNote && (!form.text.trim() || form.text.trim().length > 4000)) {
      setError("Mülakat notu 1-4000 karakter olmalıdır."); return;
    }
    if (!isNote && (!form.criterion.trim() || form.criterion.trim().length > 128
      || !/^(?:[1-9]|10)$/.test(form.maxScore) || !/^(0|[1-9]|10)$/.test(form.score)
      || Number(form.score) > Number(form.maxScore))) {
      setError("Ölçüt 1-128 karakter, azami puan 1-10 ve puan 0-azami puan arasında tam sayı olmalıdır."); return;
    }
    setReview({ candidateId: source.candidate.id, recordId, kind: source.selection.kind,
      expectedCandidateVersion: source.candidate.version, expectedRecordVersion: source.selection.record.version,
      reason: form.reason.trim(), ...(isNote ? { noteType: form.noteType, text: form.text.trim() }
        : { criterion: form.criterion.trim(), score: Number(form.score), maxScore: Number(form.maxScore) }) });
  };
  const submit = async () => {
    if (!review || readOnly || revise.isPending) return;
    setError(null);
    try {
      await revise.mutateAsync({ ...review, ...(guidanceSource !== null ? { guidanceContentRevisionId: guidanceSource } : {}) });
      showToast(`${label} yeni sürümle düzeltildi; ilk kayıt ve geçmiş korundu.`, "success"); onClose();
    } catch (cause) {
      setReview(null); setError(cause instanceof ApiError ? cause.message : "Düzeltme kaydedilemedi.");
    }
  };
  const after: CandidateEvidenceState | null = review ? { noteType: review.noteType ?? null,
    text: review.text ?? null, criterion: review.criterion ?? null,
    score: review.score ?? null, maxScore: review.maxScore ?? null } : null;

  return <div className="modal-overlay" style={{ display: "flex" }}>
    <div className="modal-card scale-in" role="dialog" aria-modal="true" aria-labelledby="evidence-title">
      <div className="modal-head"><div><h3 id="evidence-title">{label}: {readOnly ? "sürüm geçmişi" : "gerekçeli düzeltme"}</h3>
        <p>{source.candidate.name} · incelenen kayıt sürümü {source.selection.record.version}</p></div>
        <button className="btn-icon-sm" aria-label="Kanıt düzeltme penceresini kapat" onClick={onClose} disabled={revise.isPending}>
          <i className="fa-solid fa-xmark" aria-hidden="true" /></button></div>
      <div className="modal-body-scroll ez-workbench">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}>{!readOnly && <GuidanceOperationSource process="recruitment" step="first-review" value={guidanceSource} onChange={setGuidanceSource} disabled={revise.isPending} label={isNote ? "Not düzeltirken kullandığım rehber sürümü" : "Değerlendirme düzeltirken kullandığım rehber sürümü"} />}</div>
          <div className="ez-workbench-main">

        {readOnly ? <p>İşe alınmış adayın başvuru geçmişi kilitli; not ve değerlendirme bu ekrandan düzeltilemez.</p>
          : <><section aria-label="Kanıt düzeltme rehberi">
            <p><strong>Ne yapıyoruz?</strong> Hatalı {isNote ? "görüşme türünü veya not metnini" : "ölçütü, puanı veya azami puanı"} gerekçeli yeni sürümle düzeltiyoruz. İlk kayıt değişmez; yeni sürüm güncel görünür.</p>
            <p><strong>Neden?</strong> Sonraki kararı doğru gözleme dayandırırken önceki bilgiye ve düzeltmenin nedenine ulaşabilmek için.</p>
            <p><strong>Örnek:</strong> {isNote ? "Teknik görüşmede yazılan not yanlışlıkla İK Görüşmesi seçildiyse türü düzeltip görüşme kaynağını gerekçede belirtin."
              : "Değerlendirme belgesindeki 4/10 puan 4/5 girildiyse hem puanı hem azami puanı karşılaştırın; anlamı değişir."}</p>
            <p>Adayın aşaması ve genel puanı değişmez. Yanlış adaya ait kaydı taşımak veya iptal etmek bu düzeltmenin kapsamına girmez.</p>
          </section><div className="form-grid-2">
            {isNote ? <><div className="input-group"><label htmlFor="revise-type">Görüşme türü</label>
              <select id="revise-type" className="input-control" value={form.noteType} onChange={e => set("noteType", e.target.value)}>
                {NOTE_TYPES.map(type => <option key={type}>{type}</option>)}</select></div>
              <div className="input-group"><label htmlFor="revise-text">Güncel not metni</label>
                <textarea id="revise-text" className="input-control" value={form.text} onChange={e => set("text", e.target.value)} /></div></>
              : <><div className="input-group"><label htmlFor="revise-criterion">Güncel ölçüt</label>
                <input id="revise-criterion" className="input-control" value={form.criterion} onChange={e => set("criterion", e.target.value)} /></div>
                <div className="input-group"><label htmlFor="revise-score">Güncel puan</label>
                  <input id="revise-score" type="number" min="0" max="10" step="1" className="input-control" value={form.score} onChange={e => set("score", e.target.value)} /></div>
                <div className="input-group"><label htmlFor="revise-max">Güncel azami puan</label>
                  <input id="revise-max" type="number" min="1" max="10" step="1" className="input-control" value={form.maxScore} onChange={e => set("maxScore", e.target.value)} /></div></>}
            <div className="input-group"><label htmlFor="revise-reason">Düzeltme gerekçesi ve dayanak</label>
              <textarea id="revise-reason" className="input-control" value={form.reason} onChange={e => set("reason", e.target.value)} /></div>
          </div></>}
        {error && <p className="form-error" role="alert">{error} Güncel kayda dönüp yeniden açabilirsiniz.</p>}

        {after && <section aria-label="Kanıt düzeltmesi ön kontrolü" aria-live="polite">
          <h4>Önce</h4><StateView state={current} /><h4>Yeni geçerli sürüm</h4><StateView state={after} />
          <p><strong>Gerekçe:</strong> {review?.reason}</p><p>İlk kayıt korunur. Bu işlem adayın aşamasını veya genel puanını değiştirmez.</p>
        </section>}
        <details><summary>İlk kayıt ve düzeltme geçmişi</summary>
          {history.isPending && <p>Geçmiş yükleniyor.</p>}
          {history.isError && <p role="alert">Sürüm geçmişi yüklenemedi.</p>}
          {history.data && <><h4>İlk kayıt · sürüm 1</h4><StateView state={history.data.original} />
            {history.data.revisions.length === 0 && <p>Henüz düzeltme yok.</p>}
            {history.data.revisions.map(revision => <details key={revision.id}>
              <summary>Sürüm {revision.version} · {revision.reason}</summary>
              <p>{revision.changedByName} · {revision.changedAtUtc}</p>
              <h4>Önce</h4><StateView state={revision.before} /><h4>Sonra</h4><StateView state={revision.after} />
            </details>)}</>}
        </details>
      </div></div></div><div className="modal-footer">
        <button className="btn btn-ghost" onClick={onClose} disabled={revise.isPending}>Güncel kayda dön</button>
        {!readOnly && <button className="btn btn-primary" onClick={review ? submit : prepare}
          disabled={revise.isPending || !Number.isInteger(source.candidate.version) || source.candidate.version < 1
            || !Number.isInteger(source.selection.record.version) || source.selection.record.version < 1}>
          {review ? "Yeni sürümü kaydet" : "Bilgileri kontrol et"}</button>}
      </div>
    </div>
  </div>;
}
