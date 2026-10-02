import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useDepartments } from "../personnel/queries";
import { useCorrectPosition, useCreatePosition, usePositionCorrections,
  type PositionDto } from "./queries";

type PositionReview = {
  title: string; departmentId: number | null; departmentName: string;
  openCount: number; isOpen: boolean; reason: string;
};

export function PositionModal({ onClose, position }: { onClose: () => void; position?: PositionDto }) {
  const { showToast } = useToast();
  const departmentsQ = useDepartments();
  const createPosition = useCreatePosition();
  const correctPosition = useCorrectPosition();
  const historyQ = usePositionCorrections(position?.id ?? null);
  const [source, setSource] = useState<number | null>(null);
  const [title, setTitle] = useState(position?.title ?? "");
  const [departmentId, setDepartmentId] = useState(position?.departmentId ? String(position.departmentId) : "");
  const [openCount, setOpenCount] = useState(String(position?.openCount ?? 1));
  const [isOpen, setIsOpen] = useState(position?.isOpen ?? true);
  const [reason, setReason] = useState("");
  const [review, setReview] = useState<PositionReview | null>(null);
  const [error, setError] = useState<string | null>(null);

  const prepareReview = () => {
    setError(null);
    const cleanTitle = title.trim();
    if (!cleanTitle || cleanTitle.length > 128) {
      setError("Pozisyon adını 1-128 karakter arasında girin.");
      return;
    }
    const count = Number(openCount);
    if (!/^\d{1,3}$/.test(openCount) || count > 100 || (isOpen && count < 1)
      || (!position && count < 1)) {
      setError(isOpen || !position
        ? "Açık kadro sayısı 1-100 arasında tam sayı olmalıdır."
        : "Kapalı pozisyonda kalan açık kadro sayısı 0-100 arasında olmalıdır.");
      return;
    }
    if (position && (!reason.trim() || reason.trim().length > 2000)) {
      setError("Düzeltme gerekçesini 1-2000 karakter arasında girin.");
      return;
    }
    if (!departmentsQ.data) {
      setError("Departman listesi yüklenemedi. Pozisyonu oluşturmadan önce yeniden deneyin.");
      return;
    }
    const department = departmentId
      ? departmentsQ.data.find((item) => String(item.id) === departmentId)
      : null;
    if (departmentId && !department) {
      setError("Seçilen departman artık listede yok. Yeniden seçin.");
      return;
    }
    setReview({
      title: cleanTitle,
      departmentId: department?.id ?? null,
      departmentName: department?.name ?? "Departman seçilmedi",
      openCount: count, isOpen, reason: reason.trim(),
    });
  };

  const submit = async () => {
    if (!review || createPosition.isPending || correctPosition.isPending) return;
    setError(null);
    try {
      if (position) {
        await correctPosition.mutateAsync({ id: position.id, expectedVersion: position.version,
          title: review.title, departmentId: review.departmentId,
          openCount: review.openCount, isOpen: review.isOpen, reason: review.reason });
        showToast("Pozisyon düzeltildi; önceki değerler geçmişte saklandı.", "success");
      } else {
        const created = await createPosition.mutateAsync({ title: review.title,
          departmentId: review.departmentId, openCount: review.openCount,
          ...(source != null ? { guidanceContentRevisionId: source } : {}) });
        showToast((created.title || review.title) + " pozisyonu açıldı.", "success");
      }
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Pozisyon kaydedilemedi.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>{position ? "Pozisyonu Düzelt" : "Yeni Pozisyon"}</h3>
            <p>{position ? `Sürüm ${position.version} · düzeltme geçmişi korunur.` : "İşe alım için açık kadro oluşturun."}</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Pozisyon penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <section className="ez-position-guide" aria-label="Pozisyon oluşturma rehberi">
            <p><strong>Ne yapıyoruz?</strong> {position
              ? "Pozisyonun adını, departmanını, kalan kadrosunu veya açık durumunu gerekçeyle düzeltiyoruz. Önceki değerler geçmişte kalır."
              : "Başvuruları bağlayabileceğiniz açık pozisyonu kaydediyoruz. Bu işlem aday veya çalışan oluşturmaz, ilan yayımlamaz."}</p>
            <p><strong>Neden kadro sayısını seçiyoruz?</strong> Açık kadro sayısı, bu pozisyona bağlı bir aday işe alındığında azalır. Örneğin iki kişi alınacaksa sayı 2 olmalıdır; başvuru sayısını yazmayın.</p>
            <p>Kapalı pozisyonun bağlı adayları ve başvuru geçmişi korunur. Kapalı veya kadrosuz pozisyona yeni aday bağlanamaz; bağlı adayın işe alınması için pozisyonun yeniden açılması gerekir.</p>
          </section>
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="position-title">Pozisyon adı</label>
              <input id="position-title" className="input-control" value={title}
                onChange={(e) => { setTitle(e.target.value); setReview(null); setError(null); }} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="position-department">Departman</label>
              <select id="position-department" className="input-control" value={departmentId}
                onChange={(e) => { setDepartmentId(e.target.value); setReview(null); setError(null); }}
                disabled={!departmentsQ.data}>
                <option value="">Departman seçilmedi</option>
                {(departmentsQ.data ?? []).map((department) => (
                  <option key={department.id} value={department.id}>{department.name}</option>
                ))}
              </select>
              {departmentsQ.isError && <small role="alert">Departman listesi yüklenemedi.</small>}
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="position-count">Açık kadro sayısı</label>
              <input id="position-count" type="number" min={position && !isOpen ? "0" : "1"}
                max="100" step="1" className="input-control"
                value={openCount} onChange={(e) => { setOpenCount(e.target.value); setReview(null); setError(null); }} />
            </div>
            {position && <><label><input type="checkbox" checked={isOpen}
              onChange={(e) => { setIsOpen(e.target.checked); setReview(null); setError(null); }} /> Pozisyon açık</label>
              <div className="input-group"><label className="input-label" htmlFor="position-reason">Düzeltme gerekçesi</label>
                <textarea id="position-reason" className="input-control" value={reason}
                  onChange={(e) => { setReason(e.target.value); setReview(null); setError(null); }} /></div></>}
          </div>
          {position && <details><summary>Düzeltme geçmişi</summary>
            <p>Bu liste gerekçeli manuel düzeltmeleri gösterir. İşe alma da kalan kadroyu ve pozisyon sürümünü değiştirir.</p>
            {historyQ.isPending && <p>Geçmiş yükleniyor.</p>}
            {historyQ.isError && <p role="alert">Düzeltme geçmişi yüklenemedi.</p>}
            {historyQ.data?.length === 0 && <p>Henüz düzeltme yok.</p>}
            {historyQ.data?.map((item) => <p key={item.id}>
              Sürüm {item.fromVersion} → {item.toVersion} · {item.reason} · {item.changedAtUtc}<br />
              {item.before.title} / {item.before.openCount} kadro / {item.before.isOpen ? "açık" : "kapalı"}
              {" → "}{item.after.title} / {item.after.openCount} kadro / {item.after.isOpen ? "açık" : "kapalı"}
            </p>)}
          </details>}
          {!position && <GuidanceOperationSource process="recruitment" step="candidate-create" value={source} onChange={setSource} disabled={createPosition.isPending} label="Pozisyon açarken kullandığım rehber sürümü" />}
          {review && <section className="ez-position-review" role="region" aria-label="Pozisyon ön kontrolü" aria-live="polite">
            <h4>{position ? "Düzeltmeyi kaydetmeden önce" : "Pozisyonu açmadan önce"}</h4>
            <p><strong>Ad:</strong> {review.title} · <strong>Departman:</strong> {review.departmentName}</p>
            <p><strong>Açık kadro:</strong> {review.openCount} · {review.isOpen ? "Açık" : "Kapalı"}</p>
            {position ? <p><strong>Gerekçe:</strong> {review.reason}. Bağlı {position.candidateCount} adayın bağlantısı korunur.</p>
              : <p>Bu kayıt açık pozisyon olarak oluşacak. Aday eklerken bu pozisyonu ayrıca seçmeniz gerekir.</p>}
          </section>}
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          {review && <button className="btn btn-ghost" onClick={() => setReview(null)}>Bilgileri düzelt</button>}
          <button className="btn btn-primary" onClick={review ? submit : prepareReview}
            disabled={createPosition.isPending || correctPosition.isPending || departmentsQ.isPending}>
            {review ? (position ? "Düzeltmeyi kaydet" : "Pozisyonu oluştur") : "Bilgileri kontrol et"}
          </button>
        </div>
      </div>
    </div>
  );
}
