import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import {
  useCreatePayrollPeriod,
  type PayrollPeriodDetailDto,
  type PayrollPeriodListItemDto,
} from "./queries";

export function CreatePeriodModal({ latest, onClose, onCreated }: {
  latest: PayrollPeriodListItemDto | null;
  onClose: () => void;
  onCreated: (id: number) => void;
}) {
  const { showToast } = useToast();
  const createPeriod = useCreatePayrollPeriod();
  const now = new Date();
  const next = latest ? new Date(latest.year ?? now.getFullYear(), latest.month ?? now.getMonth() + 1, 1) : now;
  const [year, setYear] = useState(String(next.getFullYear()));
  const [month, setMonth] = useState(String(next.getMonth() + 1));
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const validDate = Number.isInteger(Number(year)) && Number(year) >= 2000 && Number(year) <= 2100;
  const laterThanLatest = !latest || Number(year) * 12 + Number(month) > (latest.year ?? 0) * 12 + (latest.month ?? 0);
  const latestReady = !latest || latest.status === "approved" || latest.status === "closed";
  const canCreate = validDate && laterThanLatest && latestReady;

  const submit = async () => {
    if (!canCreate || !confirmed) return;
    setError(null);
    try {
      const period = await createPeriod.mutateAsync({ year: Number(year), month: Number(month) });
      showToast((period.name ?? "Yeni") + " bordro dönemi oluşturuldu.", "success");
      if (period.id !== undefined) onCreated(period.id);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Dönem oluşturulamadı.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in" role="dialog" aria-modal="true" aria-label="Yeni bordro dönemi">
        <div className="modal-head">
          <div>
            <h3>Yeni Bordro Dönemi</h3>
            <p>Seçilen ayın bordro girdileri gerçek personel ve puantaj kayıtlarından başlatılır.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Dönem penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="pp-year">Yıl</label>
              <input id="pp-year" type="number" min={2000} max={2100} className="input-control" value={year} onChange={(e) => { setYear(e.target.value); setConfirmed(false); }} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="pp-month">Ay</label>
              <select id="pp-month" className="input-control" value={month} onChange={(e) => { setMonth(e.target.value); setConfirmed(false); }}>
                {Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{i + 1}</option>)}
              </select>
            </div>
          </div>
          <div className="ez-payroll-review">
            <h4>Oluşturmadan önce</h4>
            <p><strong>Ne olacak?</strong> Dönem taslak açılır; ay başında geçerli ücret paketi ve onaylı hesap parametreleri başlangıç girdisine alınır. Puantaj tamlığı ayrıca kontrol edilir.</p>
            <p><strong>Neden kontrol ediyoruz?</strong> Son dönem onaylanmadan veya kapatılmadan yeni dönem açılamaz. Bu ekran kesin satır sayısını veya bordro sonucunu önceden hesaplamaz.</p>
            {latest && <p><strong>Son dönem:</strong> {latest.name} · {latest.status === "approved" ? "Onaylandı" : latest.status === "closed" ? "Kapandı" : latest.status === "control" ? "Kontrol" : "Taslak"}</p>}
            {!latestReady && <p role="alert">Önce son dönemin satırlarını ve dönem onayını tamamlayın.</p>}
            {!laterThanLatest && <p role="alert">Yeni dönem son dönemden sonraki bir ay olmalı.</p>}
            {!validDate && <p role="alert">2000 ile 2100 arasında geçerli bir yıl girin.</p>}
            <label><input type="checkbox" checked={confirmed} disabled={!canCreate} onChange={(e) => setConfirmed(e.target.checked)} /> Ayı ve son dönem durumunu kontrol ettim.</label>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={!canCreate || !confirmed || createPeriod.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> Oluştur
          </button>
        </div>
      </div>
    </div>
  );
}

export function SubmitPeriodModal({ detail, pending, onClose, onSubmit }: {
  detail: PayrollPeriodDetailDto;
  pending: boolean;
  onClose: () => void;
  onSubmit: () => void;
}) {
  const [confirmed, setConfirmed] = useState(false);
  const rows = detail.rows ?? [];
  const approved = rows.filter((row) => row.approvalStatus === "Onaylandı").length;
  const allApproved = approved === rows.length;
  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in" role="dialog" aria-modal="true" aria-label="Dönem onayı ön kontrolü">
        <div className="modal-head">
          <div>
            <h3>Dönem onayı ön kontrolü</h3>
            <p>{detail.name} döneminin sunucudaki satır durumunu inceleyin.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Dönem onayı penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          <div className="ez-payroll-review">
            <h4>Onaydan önce</h4>
            <p><strong>Gerçek kayıtlar:</strong> {rows.length} bordro satırının {approved} tanesi onaylı.</p>
            <p><strong>Ne olacak?</strong> Dönem Onaylandı durumuna geçer. Onaylanan dönem ve satırlar bu ekranda düzenlenemez.</p>
            <p><strong>Neden?</strong> Önce personel, puantaj ve hesap uyarılarını satır detaylarında gözden geçirin; eksikleri düzeltip kontrolü yeniden çalıştırın.</p>
            <p>Bu işlem dönemi Kapandı durumuna geçirmez; ödeme veya dış kuruma bildirim yapmaz.</p>
            {rows.length === 0 && <p role="alert">Bu dönemde bordro satırı yok. Boş dönemi onaylama kararını ayrıca doğrulayın.</p>}
            {!allApproved && <p role="alert">Önce {rows.length - approved} bekleyen satırı kontrol edip onaylayın.</p>}
            <label><input type="checkbox" checked={confirmed} disabled={!allApproved} onChange={(e) => setConfirmed(e.target.checked)} /> Satırları ve dönem sonucunu kontrol ettim.</label>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Satırlara dön</button>
          <button className="btn btn-primary" onClick={onSubmit} disabled={!allApproved || !confirmed || pending}>Dönemi onayla</button>
        </div>
      </div>
    </div>
  );
}
