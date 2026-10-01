import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useRef, useState } from "react";
import { ApiError, apiFetch } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import {
  useCreatePayrollPeriod,
  type PayrollPeriodDetailDto,
  type PayrollPeriodListItemDto,
} from "./queries";

type PeriodCreationPreview = {
  year: number; month: number; name: string; token: string;
  settings: { version: number; effectiveFrom: string; sourceReference: string };
  minimumWageIncomeTaxBaseBefore: number;
  rows: Array<{ employeeId: number; name: string; employmentPeriodId: number | null; compensationPackageId: number | null;
    compensationVersion: number | null; sourceReference: string | null; hireDate?: string; terminationDate?: string | null; grossSalary: number; premiumPay: number;
    roadAllowance: number; mealAllowance: number; benefitPay: number; workedDays: number; overtimeHours: number;
    previousTaxBase: number; ibanComplete: boolean; timesheetComplete: boolean }>;
};
const money = (value: number) => new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" }).format(value);

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
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [preview, setPreview] = useState<PeriodCreationPreview | null>(null);
  const [previewPending, setPreviewPending] = useState(false);
  const requestNumber = useRef(0);
  const invalidatePreview = () => { requestNumber.current++; setPreview(null); setConfirmed(false); setPreviewPending(false); };
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const validDate = Number.isInteger(Number(year)) && Number(year) >= 2000 && Number(year) <= 2100;
  const laterThanLatest = !latest || Number(year) * 12 + Number(month) > (latest.year ?? 0) * 12 + (latest.month ?? 0);
  const latestReady = !latest || latest.status === "approved" || latest.status === "closed";
  const canCreate = validDate && laterThanLatest && latestReady;

  const loadPreview = async () => {
    if (!canCreate || previewPending || createPeriod.isPending) return;
    const attempt = ++requestNumber.current;
    setConfirmed(false); setPreview(null); setError(null); setPreviewPending(true);
    try {
      const result = await apiFetch<PeriodCreationPreview>("/payroll/periods/creation-preview?year=" + year + "&month=" + month);
      if (!result?.token || result.year !== Number(year) || result.month !== Number(month)
        || !Array.isArray(result.rows) || !result.settings?.version
        || !result.rows.every(row => row && Number.isInteger(row.employeeId) && typeof row.name === "string"
          && [row.grossSalary, row.premiumPay, row.roadAllowance, row.mealAllowance, row.benefitPay,
            row.workedDays, row.overtimeHours, row.previousTaxBase].every(Number.isFinite)
          && typeof row.ibanComplete === "boolean" && typeof row.timesheetComplete === "boolean")) throw new Error("Önizleme yanıtı eksik; tekrar yükleyin.");
      if (attempt === requestNumber.current) setPreview(result);
    } catch (cause) {
      if (attempt === requestNumber.current) setError(cause instanceof Error ? cause.message : "Önizleme yüklenemedi.");
    } finally { if (attempt === requestNumber.current) setPreviewPending(false); }
  };

  const submit = async () => {
    if (!canCreate || !confirmed || !preview || previewPending || createPeriod.isPending) return;
    setError(null);
    try {
      const period = await createPeriod.mutateAsync({ year: Number(year), month: Number(month), expectedPreviewToken: preview.token, ...(guidanceVersion === null ? {} : { guidanceContentRevisionId: guidanceVersion }) });
      showToast((period.name ?? "Yeni") + " bordro dönemi oluşturuldu.", "success");
      if (period.id !== undefined) onCreated(period.id);
      onClose();
    } catch (e) {
      invalidatePreview();
      setError(e instanceof ApiError ? e.message : "Dönem oluşturulamadı; kayıt listesini kontrol edip önizlemeyi yeniden yükleyin.");
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
          <GuidanceOperationSource process="payroll" step="period-create" value={guidanceVersion} onChange={(id) => { setGuidanceVersion(id); setConfirmed(false); }} disabled={createPeriod.isPending} />
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="pp-year">Yıl</label>
              <input id="pp-year" disabled={createPeriod.isPending} type="number" min={2000} max={2100} className="input-control" value={year} onChange={(e) => { setYear(e.target.value); invalidatePreview(); }} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="pp-month">Ay</label>
              <select id="pp-month" disabled={createPeriod.isPending} className="input-control" value={month} onChange={(e) => { setMonth(e.target.value); invalidatePreview(); }}>
                {Array.from({ length: 12 }, (_, i) => <option key={i + 1} value={i + 1}>{i + 1}</option>)}
              </select>
            </div>
          </div>
          <div className="ez-payroll-review">
            <h4>Oluşturmadan önce</h4>
            <p><strong>Ne olacak?</strong> Dönem taslak açılır; ay başında geçerli ücret paketi ve onaylı hesap parametreleri başlangıç girdisine alınır. Puantaj tamlığı ayrıca kontrol edilir.</p>
            <p><strong>Neden kontrol ediyoruz?</strong> Son dönem onaylanmadan veya kapatılmadan yeni dönem açılamaz. Önizleme gerçek başlangıç girdilerini gösterir; kesin ödeme ve satır onayı ayrıca kontrol edilir.</p>
            {latest && <p><strong>Son dönem:</strong> {latest.name} · {latest.status === "approved" ? "Onaylandı" : latest.status === "closed" ? "Kapandı" : latest.status === "control" ? "Kontrol" : "Taslak"}</p>}
            {!latestReady && <p role="alert">Önce son dönemin satırlarını ve dönem onayını tamamlayın.</p>}
            {!laterThanLatest && <p role="alert">Yeni dönem son dönemden sonraki bir ay olmalı.</p>}
            {!validDate && <p role="alert">2000 ile 2100 arasında geçerli bir yıl girin.</p>}
            <button className="btn btn-secondary" onClick={loadPreview} disabled={!canCreate || previewPending || createPeriod.isPending}>
              {previewPending ? "Başlangıç girdileri yükleniyor…" : "Gerçek başlangıç girdilerini önizle"}
            </button>
            {preview && <section aria-label="Bordro başlangıç etki önizlemesi">
              <p><strong>{preview.name}:</strong> {preview.rows.length} personel satırı taslak oluşturulacak.</p>
              <p><strong>Onaylı parametre sürümü:</strong> #{preview.settings.version} · {preview.settings.effectiveFrom} · {preview.settings.sourceReference}</p>
              <p><strong>Önceki asgari ücret vergi matrahı:</strong> {money(preview.minimumWageIncomeTaxBaseBefore)}</p>
              {preview.rows.length === 0 && <p>Bu ay için uygun personel bulunamadı; boş dönem oluşturulacak.</p>}
              <ul>{preview.rows.map(row => <li key={row.employeeId}>
                <strong>{row.name}</strong> · {row.compensationPackageId === null ? "Ay başında ücret paketi yok; ilk ücret girdileri sıfır." : "Ücret paketi #" + row.compensationPackageId + " / sürüm " + row.compensationVersion + " · " + row.sourceReference}
                <p>Brüt: {money(row.grossSalary)} · Prim: {money(row.premiumPay)} · Yol: {money(row.roadAllowance)} · Yemek: {money(row.mealAllowance)} · Diğer: {money(row.benefitPay)}</p>
                {row.hireDate && <p>İşe giriş: {row.hireDate} · Ayrılış: {row.terminationDate ?? "Kayıt yok"}. Ay içinde giriş/ayrılış varsa başlangıç gün sayısını gerçek çalışma süresine göre düzeltin.</p>}
                <p>Başlangıç gün sayısı: {row.workedDays} (ayrıca doğrulayın) · Fazla mesai: {row.overtimeHours} saat · Önceki matrah: {money(row.previousTaxBase)}</p>
                <p>IBAN: {row.ibanComplete ? "Kayıtlı" : "Eksik"} · Puantaj: {row.timesheetComplete ? "Tam" : "Eksik"}. Kanuni sınıflandırmalar ve gün/istisna kabulleri satırda ayrıca doğrulanır.</p>
              </li>)}</ul>
            </section>}
            <label><input type="checkbox" checked={confirmed} disabled={!canCreate || !preview || previewPending || createPeriod.isPending} onChange={(e) => setConfirmed(e.target.checked)} /> Ayı, personel satırlarını, kaynakları ve eksikleri kontrol ettim.</label>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={!canCreate || !confirmed || !preview || previewPending || createPeriod.isPending}>
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
  onSubmit: (guidanceContentRevisionId?: number) => void;
}) {
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
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
          <GuidanceOperationSource process="payroll" step="approval" value={guidanceVersion} onChange={(id) => { setGuidanceVersion(id); setConfirmed(false); }} disabled={pending} />
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
          <button className="btn btn-primary" onClick={() => onSubmit(guidanceVersion ?? undefined)} disabled={!allApproved || !confirmed || pending}>Dönemi onayla</button>
        </div>
      </div>
    </div>
  );
}
