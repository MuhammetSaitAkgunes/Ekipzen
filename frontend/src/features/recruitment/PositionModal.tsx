import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useDepartments } from "../personnel/queries";
import { useCreatePosition, type CreatePositionCommand } from "./queries";

type PositionReview = { command: CreatePositionCommand; departmentName: string };

export function PositionModal({ onClose }: { onClose: () => void }) {
  const { showToast } = useToast();
  const departmentsQ = useDepartments();
  const createPosition = useCreatePosition();
  const [title, setTitle] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [openCount, setOpenCount] = useState("1");
  const [review, setReview] = useState<PositionReview | null>(null);
  const [error, setError] = useState<string | null>(null);

  const prepareReview = () => {
    setError(null);
    const cleanTitle = title.trim();
    if (!cleanTitle || cleanTitle.length > 128) {
      setError("Pozisyon adını 1-128 karakter arasında girin.");
      return;
    }
    if (!/^(?:[1-9]|[1-9][0-9]|100)$/.test(openCount)) {
      setError("Açık kadro sayısı 1-100 arasında tam sayı olmalıdır.");
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
      command: {
        title: cleanTitle,
        departmentId: department?.id ?? null,
        openCount: Number(openCount),
      },
      departmentName: department?.name ?? "Departman seçilmedi",
    });
  };

  const submit = async () => {
    if (!review || createPosition.isPending) return;
    setError(null);
    try {
      const position = await createPosition.mutateAsync(review.command);
      showToast((position.title || review.command.title) + " pozisyonu açıldı.", "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Pozisyon oluşturulamadı.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>Yeni Pozisyon</h3>
            <p>İşe alım için açık kadro oluşturun.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Pozisyon penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <section className="ez-position-guide" aria-label="Pozisyon oluşturma rehberi">
            <p><strong>Ne yapıyoruz?</strong> Başvuruları bağlayabileceğiniz açık pozisyonu kaydediyoruz. Bu işlem aday veya çalışan oluşturmaz, ilan yayımlamaz.</p>
            <p><strong>Neden kadro sayısını seçiyoruz?</strong> Açık kadro sayısı, bu pozisyona bağlı bir aday işe alındığında azalır. Örneğin iki kişi alınacaksa sayı 2 olmalıdır; başvuru sayısını yazmayın.</p>
            <p>Departman seçimi isteğe bağlıdır. Mevcut API pozisyonu düzenleme veya kapatma sunmuyor; ad, departman ve sayıyı kaydetmeden önce doğrulayın.</p>
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
              <input id="position-count" type="number" min="1" max="100" step="1" className="input-control"
                value={openCount} onChange={(e) => { setOpenCount(e.target.value); setReview(null); setError(null); }} />
            </div>
          </div>
          {review && <section className="ez-position-review" role="region" aria-label="Pozisyon ön kontrolü" aria-live="polite">
            <h4>Pozisyonu açmadan önce</h4>
            <p><strong>Ad:</strong> {review.command.title} · <strong>Departman:</strong> {review.departmentName}</p>
            <p><strong>Açık kadro:</strong> {review.command.openCount}</p>
            <p>Bu kayıt açık pozisyon olarak oluşacak. Aday eklerken bu pozisyonu ayrıca seçmeniz gerekir.</p>
          </section>}
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          {review && <button className="btn btn-ghost" onClick={() => setReview(null)}>Bilgileri düzelt</button>}
          <button className="btn btn-primary" onClick={review ? submit : prepareReview} disabled={createPosition.isPending || departmentsQ.isPending}>
            {review ? "Pozisyonu oluştur" : "Bilgileri kontrol et"}
          </button>
        </div>
      </div>
    </div>
  );
}
