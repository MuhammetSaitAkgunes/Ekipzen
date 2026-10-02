import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useState } from "react";
import { ApiError } from "../../api/client";
import { formatCandidateScore } from "./format";
import { useToast } from "../../layout/ToastProvider";
import { useCandidateCorrections, useCorrectCandidate, usePositions,
  type CandidateCorrectionInput, type CandidateDetailDto, type CandidateState } from "./queries";

function Comparison({ before, after }: { before: CandidateState; after: CandidateState }) {
  const fields: [string, keyof CandidateState][] = [
    ["Ad Soyad", "name"], ["Başvurulan rol", "appliedRole"], ["Bağlı pozisyon", "positionTitle"],
    ["Elle verilen puan", "score"], ["Lokasyon", "location"], ["Deneyim (yıl)", "experienceYears"],
    ["Başvuru özeti", "summary"], ["Aşama", "status"],
  ];
  return <table className="data-table"><thead><tr><th>Alan</th><th>Önce</th><th>Sonra</th></tr></thead>
    <tbody>{fields.map(([label, key]) => <tr key={key}><th scope="row">{label}</th>
      <td>{key === "score" ? formatCandidateScore(before.score) : String(before[key] ?? "Belirtilmedi")}</td><td>{key === "score" ? formatCandidateScore(after.score) : String(after[key] ?? "Belirtilmedi")}</td>
    </tr>)}</tbody></table>;
}

export function CandidateCorrectionModal({ candidate, onClose }: {
  candidate: CandidateDetailDto; onClose: () => void;
}) {
  const { showToast } = useToast();
  // Form ve expectedVersion aynı ilk görüntüden alınır; arka plandaki yenileme eski taslağı güncellemez.
  const [source] = useState(candidate);
  const historyQ = useCandidateCorrections(source.id);
  const positionsQ = usePositions();
  const correction = useCorrectCandidate();
  const [form, setForm] = useState({ name: source.name ?? "", appliedRole: source.appliedRole ?? "",
    positionId: source.positionId ? String(source.positionId) : "", score: source.score == null ? "" : String(source.score),
    location: source.location ?? "", experienceYears: String(source.experienceYears ?? 0),
    summary: source.summary ?? "", reason: "" });
  const [review, setReview] = useState<CandidateCorrectionInput | null>(null);
  const [reviewPositionTitle, setReviewPositionTitle] = useState<string | null>(null);
  const [evidenceConfirmed, setEvidenceConfirmed] = useState(false);
  const [guidanceSource, setGuidanceSource] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const readOnly = candidate.status === "İşe Alındı";
  const roleChanged = form.appliedRole.trim() !== source.appliedRole
    || (form.positionId ? Number(form.positionId) : null) !== (source.positionId ?? null);
  const before: CandidateState = { name: source.name ?? "", appliedRole: source.appliedRole ?? "",
    positionId: source.positionId ?? null, positionTitle: source.positionTitle ?? null,
    score: source.score ?? null, location: source.location ?? null,
    experienceYears: source.experienceYears ?? 0, summary: source.summary ?? null,
    status: source.status ?? "Yeni" };

  const set = (key: keyof typeof form, value: string) => {
    setForm(current => ({ ...current, [key]: value }));
    setReview(null); setEvidenceConfirmed(false); setError(null);
  };

  const prepareReview = () => {
    setError(null);
    if (!form.name.trim() || form.name.trim().length > 200
      || !form.appliedRole.trim() || form.appliedRole.trim().length > 128) {
      setError("Ad Soyad 1-200, başvurulan rol 1-128 karakter olmalıdır."); return;
    }
    if ((form.score !== "" && (!/^(0|[1-9]\d{0,2})$/.test(form.score) || Number(form.score) > 100))
      || !/^(0|[1-9]\d?)$/.test(form.experienceYears) || Number(form.experienceYears) > 60) {
      setError("Puanı boş bırakın veya 0-100, deneyimi 0-60 arasında tam sayı olarak girin."); return;
    }
    if (form.location.trim().length > 128 || form.summary.trim().length > 2000
      || !form.reason.trim() || form.reason.trim().length > 2000) {
      setError("Lokasyon en fazla 128, özet en fazla 2000 karakter olabilir. Düzeltme gerekçesi 1-2000 karakter olmalıdır."); return;
    }
    const targetId = form.positionId ? Number(form.positionId) : null;
    const positionChanged = targetId !== (source.positionId ?? null);
    const target = positionsQ.data?.find(item => item.id === targetId);
    if (positionChanged && targetId !== null && (!target?.isOpen || (target.openCount ?? 0) < 1
      || !Number.isInteger(target.version) || target.version < 1)) {
      setError("Yeni pozisyonun güncel ve açık kaydını seçin; kalan kadrosu bulunmalı."); return;
    }
    if (roleChanged && !evidenceConfirmed) {
      setError("Mevcut not ve değerlendirmelerin yeni role uygunluğunu kontrol ettiğinizi teyit edin."); return;
    }
    setReviewPositionTitle(positionChanged ? target?.title ?? null : source.positionTitle ?? null);
    setReview({ id: source.id, expectedVersion: source.version, name: form.name.trim(),
      appliedRole: form.appliedRole.trim(), positionId: targetId,
      expectedPositionVersion: positionChanged && target ? target.version : null,
      score: form.score === "" ? null : Number(form.score), location: form.location.trim() || null,
      experienceYears: Number(form.experienceYears), summary: form.summary.trim() || null,
      reason: form.reason.trim(), confirmExistingEvidenceReview: roleChanged && evidenceConfirmed });
  };

  const submit = async () => {
    if (!review || readOnly || correction.isPending) return;
    setError(null);
    try {
      await correction.mutateAsync({ ...review, ...(guidanceSource !== null ? { guidanceContentRevisionId: guidanceSource } : {}) });
      showToast("Aday kaydı düzeltildi; önceki bilgiler ve gerekçe geçmişte korundu.", "success");
      onClose();
    } catch (cause) {
      setReview(null); setEvidenceConfirmed(false);
      setError(cause instanceof ApiError ? cause.message : "Aday düzeltmesi kaydedilemedi.");
    }
  };

  return <div className="modal-overlay" style={{ display: "flex" }}>
    <div className="modal-card scale-in" role="dialog" aria-modal="true" aria-labelledby="candidate-correction-title">
      <div className="modal-head"><div>
        <h3 id="candidate-correction-title">{readOnly ? "Aday düzeltme geçmişi" : "Aday kaydını düzelt"}</h3>
        <p>{source.name} · incelenen sürüm {source.version}</p>
      </div><button className="btn-icon-sm" onClick={onClose} disabled={correction.isPending} aria-label="Aday düzeltme penceresini kapat">
        <i className="fa-solid fa-xmark" aria-hidden="true" /></button></div>
      <div className="modal-body-scroll">
        {error && <p role="alert" className="form-error">{error} Güncel kayda dönüp düzeltmeyi yeniden açabilirsiniz.</p>}
        {readOnly ? <p>İşe alınmış adayın başvuru kaydı kilitlidir. Çalışan bilgilerindeki hatayı personel kartındaki ayrı düzeltme akışında inceleyin.</p>
          : <><section aria-label="Aday düzeltme rehberi">
            <p><strong>Ne yapıyoruz?</strong> Hatalı başvuru bilgisini gerekçeyle düzeltiyoruz. Örneğin yanlış pozisyona bağlanan başvuruyu doğru açık kadroya taşıyabilirsiniz.</p>
            <p><strong>Neden kontrol ediyoruz?</strong> Rol değiştiğinde eski mülakat ölçütleri yeni işe uygun olmayabilir. Aşama, notlar ve değerlendirmeler korunur; yeniden inceleme kararını siz verirsiniz.</p>
            <p>Puan elle kaydedilen 0-100 değeridir; boş alan “Henüz puan verilmedi” demektir. Yanlış girilmiş puanı gerekçeyle boşaltabilirsiniz; önceki değer geçmişte kalır. Puanı değiştiriyorsanız gerekçede değerlendirme kaynağını belirtin. Not ve ölçüt değerlendirmeleri genel puanı otomatik hesaplamaz.</p>
          </section><div className="form-grid-2">
            <div className="input-group"><label className="input-label" htmlFor="correction-name">Ad Soyad</label>
              <input id="correction-name" className="input-control" value={form.name} onChange={e => set("name", e.target.value)} /></div>
            <div className="input-group"><label className="input-label" htmlFor="correction-position">Bağlı pozisyon</label>
              <select id="correction-position" className="input-control" value={form.positionId}
                onChange={e => {
                  const position = positionsQ.data?.find(item => String(item.id) === e.target.value);
                  setForm(current => ({ ...current, positionId: e.target.value,
                    appliedRole: position?.id === source.positionId ? source.appliedRole ?? "" : position?.title ?? current.appliedRole }));
                  setReview(null); setEvidenceConfirmed(false); setError(null);
                }}><option value="">Pozisyona bağlama</option>
                {source.positionId && !positionsQ.data?.some(item => item.id === source.positionId)
                  && <option value={source.positionId}>{source.positionTitle ?? "Mevcut pozisyon"} (mevcut bağlantı)</option>}
                {(positionsQ.data ?? []).filter(item => item.id === source.positionId || (item.isOpen && (item.openCount ?? 0) > 0))
                  .map(item => <option key={item.id} value={item.id}>{item.title} · {item.isOpen ? `${item.openCount} kalan kadro` : "kapalı, mevcut bağlantı"}</option>)}
              </select>{positionsQ.isError && <small role="alert">Pozisyon listesi okunamadı; yeni bağlantı seçmeden önce yeniden açın.</small>}</div>
            <div className="input-group"><label className="input-label" htmlFor="correction-role">Başvurulan rol</label>
              <input id="correction-role" className="input-control" value={form.appliedRole} onChange={e => set("appliedRole", e.target.value)} /></div>
            <div className="input-group"><label className="input-label" htmlFor="correction-score">Elle verilen aday puanı (0-100)</label>
              <input id="correction-score" type="number" min="0" max="100" step="1" className="input-control" value={form.score} onChange={e => set("score", e.target.value)} /></div>
            <div className="input-group"><label className="input-label" htmlFor="correction-location">Lokasyon</label>
              <input id="correction-location" className="input-control" value={form.location} onChange={e => set("location", e.target.value)} /></div>
            <div className="input-group"><label className="input-label" htmlFor="correction-years">Deneyim (yıl)</label>
              <input id="correction-years" type="number" min="0" max="60" step="1" className="input-control" value={form.experienceYears} onChange={e => set("experienceYears", e.target.value)} /></div>
            <div className="input-group"><label className="input-label" htmlFor="correction-summary">Başvuru özeti</label>
              <textarea id="correction-summary" className="input-control" value={form.summary} onChange={e => set("summary", e.target.value)} /></div>
            <div className="input-group"><label className="input-label" htmlFor="correction-reason">Düzeltme gerekçesi ve dayanak</label>
              <textarea id="correction-reason" className="input-control" value={form.reason} onChange={e => set("reason", e.target.value)} /></div>
          </div>{roleChanged && <label><input type="checkbox" checked={evidenceConfirmed}
            onChange={e => { setEvidenceConfirmed(e.target.checked); setReview(null); setError(null); }} /> Mevcut not ve değerlendirmelerin yeni role uygunluğunu kontrol ettim; aşamanın ve geçmişin korunacağını biliyorum.</label>}
        {!readOnly && <GuidanceOperationSource process="recruitment" step="candidate-create" value={guidanceSource} onChange={setGuidanceSource} disabled={correction.isPending} label="Aday düzeltirken kullandığım rehber sürümü" />}
          {review && <section aria-label="Aday düzeltmesi ön kontrolü" aria-live="polite">
            <h4>Kaydetmeden önce karşılaştırın</h4>
            <Comparison before={before} after={{ ...review, positionTitle: reviewPositionTitle, status: before.status }} />
            <p><strong>Gerekçe:</strong> {review.reason}</p>
            <p>Bağlantı değişikliği kalan kadroyu tüketmez. Adayın aşaması {before.status} olarak kalır.</p>
          </section>}</>}
        <details><summary>Gerekçeli düzeltme geçmişi</summary>
          {historyQ.isPending && <p>Geçmiş yükleniyor.</p>}
          {historyQ.isError && <p role="alert">Düzeltme geçmişi yüklenemedi.</p>}
          {historyQ.data?.length === 0 && <p>Henüz düzeltme yok.</p>}
          {historyQ.data?.map(item => <details key={item.id}>
            <summary>Sürüm {item.fromVersion} → {item.toVersion} · {item.reason}</summary>
            <p>{item.changedByName} · {item.changedAtUtc}</p>
            <Comparison before={item.before} after={item.after} />
            {item.evidenceReviewConfirmed && <p>Rol değişiminde mevcut kanıtların uygunluğu kontrol edildi.</p>}
          </details>)}
        </details>
      </div><div className="modal-footer">
        <button className="btn btn-ghost" onClick={onClose} disabled={correction.isPending}>Güncel kayda dön</button>
        {!readOnly && <button className="btn btn-primary" onClick={review ? submit : prepareReview}
          disabled={correction.isPending || !Number.isInteger(source.version) || source.version < 1}>
          {review ? "Düzeltmeyi kaydet" : "Bilgileri kontrol et"}</button>}
      </div>
    </div>
  </div>;
}
