import { useState } from "react";
import { ApiError, apiDownload } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { formatPayrollMoney, payrollStatusClass } from "./format";
import { useApprovePayrollRow, useUpdatePayrollRow, type PayrollRowDto } from "./queries";

const Line = ({ label, value, tone = "" }: { label: string; value?: number; tone?: string }) => (
  <div className={`payroll-line ${tone}`}>
    <span>{label}</span>
    <strong>{formatPayrollMoney(value)}</strong>
  </div>
);

export function PayrollDetailPanel({ periodId, periodName, row, onClose }: {
  periodId: number;
  periodName: string;
  row: PayrollRowDto;
  onClose: () => void;
}) {
  const { showToast } = useToast();
  const updateRow = useUpdatePayrollRow();
  const approveRow = useApprovePayrollRow();
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState({
    grossSalary: String(row.grossSalary ?? 0),
    workedDays: String(row.workedDays ?? 30),
    overtimeHours: String(row.overtimeHours ?? 0),
    premiumPay: String(row.premiumPay ?? 0),
    roadAllowance: String(row.roadAllowance ?? 0),
    mealAllowance: String(row.mealAllowance ?? 0),
    benefitPay: String(row.benefitPay ?? 0),
    specialDeductions: String(row.specialDeductions ?? 0),
    ibanComplete: row.ibanComplete ?? true,
    notes: row.notes ?? "",
  });

  const rowId = row.id!;
  const isApproved = row.approvalStatus === "Onaylandı";
  const parse = (value: string) => {
    const parsed = Number(String(value).replace(",", "."));
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({
      ...f,
      [key]: e.target.type === "checkbox" ? e.target.checked : e.target.value,
    }));

  const save = async () => {
    setError(null);
    try {
      await updateRow.mutateAsync({
        periodId,
        rowId,
        model: {
          grossSalary: parse(form.grossSalary),
          workedDays: Math.round(parse(form.workedDays)),
          overtimeHours: Math.round(parse(form.overtimeHours)),
          premiumPay: parse(form.premiumPay),
          roadAllowance: parse(form.roadAllowance),
          mealAllowance: parse(form.mealAllowance),
          benefitPay: parse(form.benefitPay),
          specialDeductions: parse(form.specialDeductions),
          ibanComplete: form.ibanComplete,
          notes: form.notes || null,
        },
      });
      showToast(`${row.name} bordro girdileri kaydedildi.`, "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Girdiler kaydedilemedi.");
    }
  };

  const approve = async () => {
    setError(null);
    try {
      await approveRow.mutateAsync({ periodId, rowId });
      showToast(`${row.name} bordrosu onaylandı.`, "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Satır onaylanamadı.");
    }
  };

  const downloadSlip = async () => {
    try {
      const { blob, fileName } = await apiDownload(`/payroll/periods/${periodId}/rows/${rowId}/payslip`);
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = fileName ?? `bordro-${periodName}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
      showToast("Bordro pusulası indirildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Pusula indirilemedi.", "error");
    }
  };

  const inputFields: [keyof typeof form, string][] = [
    ["grossSalary", "Brüt ücret"],
    ["workedDays", "Çalışılan gün"],
    ["overtimeHours", "Fazla mesai saati"],
    ["premiumPay", "Prim"],
    ["roadAllowance", "Yol yardımı"],
    ["mealAllowance", "Yemek yardımı"],
    ["benefitPay", "Yan hak / ek ödeme"],
    ["specialDeductions", "Özel kesinti"],
  ];

  return (
    <div className="payroll-detail-panel">
      <div className="payroll-detail-head">
        <div>
          <span className={`status-pill ${payrollStatusClass(row.approvalStatus)}`}>{row.approvalStatus}</span>
          <h3>{row.name}</h3>
          <p>{row.title} · {row.department} · {periodName}</p>
        </div>
        <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Bordro detayını kapat">
          <i aria-hidden="true" className="fa-solid fa-xmark" />
        </button>
      </div>

      {error && <p className="form-error" role="alert">{error}</p>}

      <div className="payroll-detail-summary">
        <div><span>Brüt Kazanç</span><strong>{formatPayrollMoney(row.grossEarnings)}</strong></div>
        <div><span>Toplam Kesinti</span><strong>{formatPayrollMoney(row.totalDeductions)}</strong></div>
        <div><span>Net Ücret</span><strong>{formatPayrollMoney(row.netPay)}</strong></div>
        <div><span>İşveren Maliyeti</span><strong>{formatPayrollMoney(row.employerCost)}</strong></div>
      </div>

      {!isApproved && (
        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Girdiler</h4></div>
          <div className="form-grid">
            {inputFields.map(([key, label]) => (
              <div key={key} className="input-group col-3">
                <label className="input-label" htmlFor={`pd-${key}`}>{label}</label>
                <input
                  id={`pd-${key}`}
                  type="number"
                  className="input-control"
                  value={String(form[key])}
                  onChange={set(key)}
                />
              </div>
            ))}
            <div className="input-group col-3">
              <label className="input-label" htmlFor="pd-notes">Not</label>
              <input
                id="pd-notes"
                type="text"
                className="input-control"
                value={form.notes}
                onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
              />
            </div>
          </div>
          <div className="toolbar-actions">
            <label className="leg-item">
              <input type="checkbox" checked={form.ibanComplete} onChange={set("ibanComplete")} /> IBAN tamam
            </label>
            <span className={`status-pill ${row.timesheetComplete ? "approved" : "pending"}`}>
              {row.timesheetComplete
                ? "Puantaj sistem tarafından tamamlandı"
                : "Puantaj sistem kontrolünde eksik"}
            </span>
          </div>
        </section>
      )}

      <div className="payroll-detail-grid">
        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Kazançlar</h4></div>
          <Line label="Brüt ücret" value={row.baseGross} />
          <Line label="Fazla mesai" value={row.overtimePay} />
          <Line label="Prim" value={row.premiumPay} />
          <Line label="Yol yardımı" value={row.roadAllowance} />
          <Line label="Yemek yardımı" value={row.mealAllowance} />
          <Line label="Yan hak / ek ödeme" value={row.benefitPay} />
          <div className="payroll-note compact">
            <i aria-hidden="true" className="fa-solid fa-circle-info" />
            <span>{row.notes || "Not yok."}</span>
          </div>
        </section>

        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Kesintiler</h4></div>
          <Line label="SGK işçi payı" value={row.sgkEmployee} tone="deduction" />
          <Line label="İşsizlik işçi payı" value={row.unemploymentEmployee} tone="deduction" />
          <Line label="Gelir vergisi" value={row.incomeTax} tone="deduction" />
          <Line label="Damga vergisi" value={row.stampTax} tone="deduction" />
          <Line label="Özel kesintiler" value={row.specialDeductions} tone="deduction" />
        </section>

        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Vergi / SGK Matrahları</h4></div>
          <Line label="Saatlik ücret" value={row.hourlyRate} />
          <Line label="SGK matrahı" value={row.sgkBase} />
          <Line label="Gelir vergisi matrahı" value={row.incomeTaxBase} />
          <Line label="Önceki kümülatif matrah" value={row.previousTaxBase} />
          <Line label="Dönem sonu kümülatif" value={(row.previousTaxBase ?? 0) + (row.incomeTaxBase ?? 0)} />
          <div className="payroll-warning-stack">
            {(row.warnings ?? []).length > 0
              ? (row.warnings ?? []).map((warning) => (
                  <span key={warning} className="status-pill pending">{warning}</span>
                ))
              : <span className="status-pill approved">Uyarı yok</span>}
          </div>
        </section>

        <section className="card payroll-slip-preview">
          <div className="card-header-clean">
            <h4>Bordro Pusulası Önizleme</h4>
            <span className="status-pill info">Demo</span>
          </div>
          <div className="slip-paper">
            <div className="slip-brand"><strong>İK Pro</strong><span>{periodName} Bordro Pusulası</span></div>
            <div className="slip-row"><span>Personel</span><strong>{row.name}</strong></div>
            <div className="slip-row"><span>Çalışılan gün</span><strong>{row.workedDays}</strong></div>
            <div className="slip-row"><span>Fazla mesai</span><strong>{row.overtimeHours ?? 0} saat · {formatPayrollMoney(row.overtimePay)}</strong></div>
            <div className="slip-row"><span>Brüt kazanç</span><strong>{formatPayrollMoney(row.grossEarnings)}</strong></div>
            <div className="slip-row"><span>Yasal kesintiler</span><strong>{formatPayrollMoney((row.totalDeductions ?? 0) - (row.specialDeductions ?? 0))}</strong></div>
            <div className="slip-row"><span>Özel kesintiler</span><strong>{formatPayrollMoney(row.specialDeductions)}</strong></div>
            <div className="slip-row total"><span>Net ödenecek</span><strong>{formatPayrollMoney(row.netPay)}</strong></div>
          </div>
        </section>
      </div>

      <div className="payroll-detail-footer">
        <button className="btn btn-secondary" onClick={onClose}>Kapat</button>
        {isApproved ? (
          <button className="btn btn-primary" onClick={downloadSlip}>
            <i aria-hidden="true" className="fa-solid fa-file-pdf" /> Pusula İndir
          </button>
        ) : (
          <>
            <button className="btn btn-secondary" onClick={save} disabled={updateRow.isPending}>
              <i aria-hidden="true" className="fa-solid fa-floppy-disk" /> Kaydet
            </button>
            <button className="btn btn-primary" onClick={approve} disabled={approveRow.isPending}>
              <i aria-hidden="true" className="fa-solid fa-paper-plane" /> Onaya gönder
            </button>
          </>
        )}
      </div>
    </div>
  );
}
