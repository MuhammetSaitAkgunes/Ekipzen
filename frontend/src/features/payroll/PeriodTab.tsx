import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import {
  CONTROL_META, PERIOD_STATUS_TEXT, derivePayrollSteps, formatPayrollMoney,
  formatPayrollNumber, payrollStatusClass,
} from "./format";
import { PayrollDetailPanel } from "./PayrollDetailPanel";
import { usePayrollPeriod, useRunPayrollCheck, type PayrollRowDto } from "./queries";

export function PeriodTab({ periodId }: { periodId: number | null }) {
  const { showToast } = useToast();
  const detailQ = usePayrollPeriod(periodId);
  const runCheck = useRunPayrollCheck();
  const [deptFilter, setDeptFilter] = useState("");
  const [selectedStep, setSelectedStep] = useState<string | null>(null);
  const [detailRow, setDetailRow] = useState<PayrollRowDto | null>(null);

  if (periodId === null) {
    return (
      <div className="card">
        <p className="pending-desc">Henüz bordro dönemi yok. "Yeni Dönem" ile ilk dönemi oluşturun.</p>
      </div>
    );
  }

  if (detailQ.isPending) return <PageLoading />;
  if (detailQ.isError) return <PageError error={detailQ.error} />;

  const detail = detailQ.data;
  const parameters = detail.settings;
  if (!parameters) return <PageError error={new Error("Dönemin mevzuat snapshot'ı bulunamadı.")} />;
  const rows = detail.rows ?? [];
  const controls = detail.controls ?? [];
  const totals = detail.totals ?? {};
  const approvedCount = rows.filter((r) => r.approvalStatus === "Onaylandı").length;
  const steps = derivePayrollSteps(detail.status ?? undefined, rows, controls);
  const departments = [...new Set(rows.map((r) => r.department ?? ""))].filter(Boolean);
  const visibleRows = deptFilter ? rows.filter((r) => r.department === deptFilter) : rows;

  const handleCheck = async () => {
    try {
      await runCheck.mutateAsync(periodId);
      showToast("Kontrol listesi işaretlendi; uyarılar denetim izine kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Kontrol çalıştırılamadı.", "error");
    }
  };

  return (
    <>
      <div className="payroll-kpi-grid">
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Dönem Durumu</span>
          <strong className="sb-val">{PERIOD_STATUS_TEXT[detail.status ?? ""] ?? detail.status}</strong>
          <small>{detail.name} bordrosu</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Çalışan</span>
          <strong className="sb-val">{rows.length}</strong>
          <small>{approvedCount} onaylandı</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Toplam Brüt</span>
          <strong className="sb-val">{formatPayrollMoney(totals.gross)}</strong>
          <small>Ek ödemeler dahil</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Toplam Net</span>
          <strong className="sb-val">{formatPayrollMoney(totals.net)}</strong>
          <small>Ödeme önizlemesi</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">İşveren Maliyeti</span>
          <strong className="sb-val">{formatPayrollMoney(totals.employerCost)}</strong>
          <small>Prim işveren payı dahil</small>
        </div>
      </div>

      <section className="card payroll-flow-card">
        <div className="card-header-clean">
          <div>
            <h4>Bordro Akışı</h4>
            <p className="text-muted">Dönem kapanışı için adım adım operasyon takibi.</p>
          </div>
        </div>
        <div className="payroll-flow">
          {steps.map((step) => (
            <button
              key={step.id}
              className={`payroll-step ${step.status} ${selectedStep === step.id ? "selected" : ""}`}
              onClick={() => setSelectedStep(step.id)}
            >
              <span>{step.label}</span>
              <strong>{step.status === "done" ? "Tamam" : step.status === "active" ? "Aktif" : "Bekliyor"}</strong>
              <small>{step.meta}</small>
            </button>
          ))}
        </div>
      </section>

      <div className="payroll-main-grid">
        <section className="card payroll-control-card" data-active-step={selectedStep ?? undefined}>
          <div className="card-header-clean">
            <div>
              <h4>Kontrol Merkezi</h4>
              <p className="text-muted">Eksik veri, matrah ve onay uyarıları.</p>
            </div>
            <button className="btn btn-secondary btn-sm" onClick={handleCheck} disabled={runCheck.isPending}>
              <i aria-hidden="true" className="fa-solid fa-check-double" /> Kontrol edildi
            </button>
          </div>
          <div className="payroll-control-grid">
            {controls.map((control) => {
              const meta = CONTROL_META[control.label ?? ""] ?? { detail: "", icon: "fa-circle-info" };
              return (
                <div key={control.label} className={`payroll-control ${control.level ?? ""}`}>
                  <div className="payroll-control-icon"><i aria-hidden="true" className={`fa-solid ${meta.icon}`} /></div>
                  <div>
                    <span>{control.label}</span>
                    <strong>{control.value}</strong>
                    <p>{meta.detail}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        <div className="card payroll-parameters">
          <div className="card-header-clean">
            <div>
              <h4>Bordro Parametreleri ({parameters.effectiveFrom} tarihinden geçerli)</h4>
              <p className="text-muted">Bu dönemin hesabında kilitlenen onaylı Türkiye 4/a mevzuat seti.</p>
            </div>
            <span className="status-pill approved">Onaylı v{parameters.version}</span>
          </div>
          <div className="parameter-grid">
            <div><span>Fazla çalışma çarpanı</span><strong>{formatPayrollNumber(parameters.overtimeMultiplier)}</strong></div>
            <div><span>Aylık çalışma saati</span><strong>{formatPayrollNumber(parameters.monthlyWorkingHours)}</strong></div>
            <div><span>Brüt asgari ücret / SGK alt sınır</span><strong>{formatPayrollMoney(parameters.minWageGross)}</strong></div>
            <div><span>SGK PEK üst sınır</span><strong>{formatPayrollMoney(parameters.sgkBaseMax)}</strong></div>
            <div><span>SGK işçi / işsizlik</span><strong>%{formatPayrollNumber(parameters.sgkEmployeeRate)} + %{formatPayrollNumber(parameters.unemploymentEmployeeRate)}</strong></div>
            <div><span>Teşviksiz SGK işveren / işsizlik</span><strong>%{formatPayrollNumber(parameters.sgkEmployerRate)} + %{formatPayrollNumber(parameters.unemploymentEmployerRate)}</strong></div>
            <div><span>Nakit yemek SGK / GV günlük istisna</span><strong>{formatPayrollMoney(parameters.mealSgkDailyExemption)} / {formatPayrollMoney(parameters.mealIncomeTaxDailyExemption)}</strong></div>
            <div><span>Diğer sektör / imalat indirimi</span><strong>{formatPayrollNumber(parameters.otherSectorEmployerDiscountPoints)} / {formatPayrollNumber(parameters.manufacturingEmployerDiscountPoints)} puan</strong></div>
            <div><span>Damga vergisi</span><strong>%{formatPayrollNumber(parameters.stampTaxRate)}</strong></div>
          </div>
          <div className="payroll-note">
            <i aria-hidden="true" className="fa-solid fa-circle-info" />
            <span>{parameters.reviewNotes} Kaynak: {parameters.sourceReference}</span>
          </div>
        </div>
      </div>

      <section className="table-container payroll-table-card">
        <div className="payroll-table-header">
          <div>
            <h4>Çalışan Bordro Tablosu</h4>
            <p className="text-muted">Satıra tıklayarak bordro pusulası ve hesap detayını açın.</p>
          </div>
          <div className="toolbar-actions">
            <label className="sr-only" htmlFor="payroll-dept-filter">Departman filtresi</label>
            <select
              id="payroll-dept-filter"
              className="small-select"
              value={deptFilter}
              onChange={(e) => setDeptFilter(e.target.value)}
            >
              <option value="">Tüm departmanlar</option>
              {departments.map((dept) => (
                <option key={dept} value={dept}>{dept}</option>
              ))}
            </select>
          </div>
        </div>
        <table className="data-table payroll-table">
          <thead>
            <tr>
              <th>Personel</th><th>Departman</th><th>Brüt</th><th>Fazla Mesai</th><th>Ek Ödeme</th><th>Kesinti</th><th>SGK Matrahı</th><th>GV Matrahı</th><th>Net</th><th>Durum</th><th></th>
            </tr>
          </thead>
          <tbody>
            {visibleRows.map((r) => (
              <tr key={r.id} data-dept={r.department ?? ""}>
                <td><strong>{r.name}</strong><small>{r.title}</small></td>
                <td>{r.department}</td>
                <td>{formatPayrollMoney(r.grossEarnings)}</td>
                <td>{formatPayrollMoney(r.overtimePay)}<small>{r.overtimeHours ?? 0} saat</small></td>
                <td>{formatPayrollMoney((r.premiumPay ?? 0) + (r.roadAllowance ?? 0) + (r.mealAllowance ?? 0) + (r.benefitPay ?? 0))}</td>
                <td>{formatPayrollMoney(r.totalDeductions)}</td>
                <td>{formatPayrollMoney(r.sgkBase)}</td>
                <td>{formatPayrollMoney(r.incomeTaxBase)}</td>
                <td><strong>{formatPayrollMoney(r.netPay)}</strong></td>
                <td><span className={`status-pill ${payrollStatusClass(r.approvalStatus)}`}>{r.approvalStatus}</span></td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => setDetailRow(r)}>Detay</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      <div id="payroll-detail-overlay" className={`payroll-detail-overlay ${detailRow ? "active" : ""}`}>
        {detailRow && (
          <PayrollDetailPanel
            periodId={periodId}
            periodName={detail.name ?? ""}
            row={detailRow}
            onClose={() => setDetailRow(null)}
          />
        )}
      </div>
    </>
  );
}
