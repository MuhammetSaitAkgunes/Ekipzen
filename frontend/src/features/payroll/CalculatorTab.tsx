import { useEffect, useState } from "react";
import { ApiError } from "../../api/client";
import { PageError, PageLoading } from "../shared/PageState";
import { formatPayrollMoney, formatPayrollNumber } from "./format";
import {
  usePayrollPeriod, usePayrollPeriods, usePayrollPreview, usePayrollSettings,
} from "./queries";

const parseNumber = (value: string, fallback = 0): number => {
  const parsed = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : fallback;
};

type ScenarioForm = {
  gross: string;
  days: string;
  overtimeHours: string;
  overtimeMultiplier: string;
  premium: string;
  road: string;
  meal: string;
  mealDays: string;
  mealTreatment: "unspecified" | "cashExemptWhereNoMealProvided" | "fullyTaxable";
  benefit: string;
  benefitTaxableConfirmed: boolean;
  applyMinimumWageTaxExemption: boolean;
  employerSgkDiscount: "none" | "otherSectorTwoPoints" | "manufacturingFivePoints";
  disabilityReliefType: "unspecified" | "none" | "firstDegree" | "secondDegree" | "thirdDegree";
  deductions: string;
  taxBase: string;
};

const periodAsOf = (year?: number, month?: number) => {
  if (year && month) return `${year}-${String(month).padStart(2, "0")}-01`;
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-01`;
};

export function CalculatorTab() {
  const settingsQ = usePayrollSettings(true);
  const periodsQ = usePayrollPeriods(true);
  const latestPeriod = periodsQ.data?.[0];
  const latestPeriodId = latestPeriod?.id ?? null;
  const detailQ = usePayrollPeriod(latestPeriodId);
  const preview = usePayrollPreview();

  const [employeeRowId, setEmployeeRowId] = useState("");
  const [form, setForm] = useState<ScenarioForm | null>(null);
  const settings = settingsQ.data;

  useEffect(() => {
    if (form === null && settings) {
      setForm({
        gross: String(settings.minWageGross ?? 0),
        days: String(settings.defaultWorkedDays ?? 30),
        overtimeHours: "0",
        overtimeMultiplier: String(settings.overtimeMultiplier ?? 1.5),
        premium: "0",
        road: "0",
        meal: "0",
        mealDays: "0",
        mealTreatment: "unspecified",
        benefit: "0",
        benefitTaxableConfirmed: false,
        applyMinimumWageTaxExemption: true,
        employerSgkDiscount: "none",
        disabilityReliefType: "none",
        deductions: "0",
        taxBase: "0",
      });
    }
  }, [form, settings]);

  useEffect(() => {
    if (!form || parseNumber(form.gross) <= 0) return;
    const timer = setTimeout(() => {
      preview.mutate({
        grossSalary: parseNumber(form.gross),
        statutoryProfile: "domesticPrivateSectorStandardFourA",
        workedDays: Math.round(parseNumber(form.days, 30)),
        overtimeHours: parseNumber(form.overtimeHours),
        overtimeMultiplier: parseNumber(form.overtimeMultiplier, 1.5),
        premiumPay: parseNumber(form.premium),
        roadAllowance: parseNumber(form.road),
        mealAllowance: parseNumber(form.meal),
        mealDays: Math.round(parseNumber(form.mealDays)),
        mealTreatment: form.mealTreatment,
        benefitPay: parseNumber(form.benefit),
        benefitPayFullyTaxableConfirmed: form.benefitTaxableConfirmed,
        applyMinimumWageTaxExemption: form.applyMinimumWageTaxExemption,
        employerSgkDiscount: form.employerSgkDiscount,
        disabilityReliefType: form.disabilityReliefType,
        specialDeductions: parseNumber(form.deductions),
        previousTaxBase: parseNumber(form.taxBase),
        asOf: periodAsOf(latestPeriod?.year, latestPeriod?.month),
      });
    }, 300);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  if (settingsQ.isPending || periodsQ.isPending || form === null) return <PageLoading />;
  if (settingsQ.isError) return <PageError error={settingsQ.error} />;
  if (periodsQ.isError) return <PageError error={periodsQ.error} />;

  const rows = detailQ.data?.rows ?? [];
  const selectedRow = rows.find((row) => String(row.id) === employeeRowId);
  const result = preview.data;

  const fillFromRow = (rowId: string) => {
    setEmployeeRowId(rowId);
    const row = rows.find((item) => String(item.id) === rowId);
    if (!row) return;
    setForm({
      gross: String(row.grossSalary ?? 0),
      days: String(row.workedDays ?? settings!.defaultWorkedDays ?? 30),
      overtimeHours: String(row.overtimeHours ?? 0),
      overtimeMultiplier: String(settings!.overtimeMultiplier ?? 1.5),
      premium: String(row.premiumPay ?? 0),
      road: String(row.roadAllowance ?? 0),
      meal: String(row.mealAllowance ?? 0),
      mealDays: String(row.mealDays ?? 0),
      mealTreatment: (row.mealTreatment as ScenarioForm["mealTreatment"]) ?? "unspecified",
      benefit: String(row.benefitPay ?? 0),
      benefitTaxableConfirmed: row.benefitPayFullyTaxableConfirmed ?? false,
      applyMinimumWageTaxExemption: row.applyMinimumWageTaxExemption ?? true,
      employerSgkDiscount: (row.employerSgkDiscount as ScenarioForm["employerSgkDiscount"]) ?? "none",
      disabilityReliefType: (row.disabilityReliefType as ScenarioForm["disabilityReliefType"]) === "unspecified"
        ? "none"
        : (row.disabilityReliefType as ScenarioForm["disabilityReliefType"]) ?? "none",
      deductions: String(row.specialDeductions ?? 0),
      taxBase: String(row.previousTaxBase ?? 0),
    });
  };

  const setField = (key: keyof ScenarioForm) => (event: React.ChangeEvent<HTMLInputElement>) =>
    setForm((current) => ({ ...current!, [key]: event.target.value }));

  const fields: [keyof ScenarioForm, string, string][] = [
    ["gross", "Aylık brüt ücret", "1"],
    ["days", "SGK prim günü", "1"],
    ["overtimeHours", "Normal fazla çalışma saati", "1"],
    ["overtimeMultiplier", "Fazla çalışma çarpanı", "0.1"],
    ["premium", "Prime tabi prim", "1"],
    ["road", "Nakit yol yardımı (tam tabi)", "1"],
    ["meal", "Nakit yemek yardımı", "1"],
    ["mealDays", "Yemek fiili gün sayısı", "1"],
    ["benefit", "Diğer ödeme (tam tabi)", "1"],
    ["deductions", "Netten özel kesinti", "1"],
    ["taxBase", "Bu işverendeki önceki GV matrahı", "1"],
  ];

  return (
    <div className="payroll-calculator-grid">
      <section className="card payroll-calculator-form">
        <div className="card-header-clean">
          <div>
            <h4>Kanuni Tekil Hesaplama</h4>
            <p className="text-muted">Yalnız standart yurt içi özel sektör 4/a çalışanı için ön hesap üretir.</p>
          </div>
          <span className="status-pill info">{periodAsOf(latestPeriod?.year, latestPeriod?.month)}</span>
        </div>
        <div className="form-grid">
          <div className="input-group col-6">
            <label className="input-label" htmlFor="scenario-employee">Personel</label>
            <select id="scenario-employee" className="input-control" value={employeeRowId} onChange={(e) => fillFromRow(e.target.value)}>
              <option value="">Serbest giriş</option>
              {rows.map((row) => <option key={row.id} value={row.id}>{row.name} - {row.title}</option>)}
            </select>
          </div>
          {fields.map(([key, label, step]) => (
            <div key={key} className="input-group col-3">
              <label className="input-label" htmlFor={`scenario-${key}`}>{label}</label>
              <input id={`scenario-${key}`} className="input-control" type="number" step={step} value={String(form[key])} onChange={setField(key)} />
            </div>
          ))}
          <div className="input-group col-3">
            <label className="input-label" htmlFor="scenario-mealTreatment">Yemek vergi/SGK sınıfı</label>
            <select
              id="scenario-mealTreatment"
              className="input-control"
              value={form.mealTreatment}
              onChange={(e) => setForm((current) => ({ ...current!, mealTreatment: e.target.value as ScenarioForm["mealTreatment"] }))}
            >
              <option value="unspecified">Yemek yok / seçilmedi</option>
              <option value="cashExemptWhereNoMealProvided">İşyerinde yemek yok, nakit istisna</option>
              <option value="fullyTaxable">Tam prime ve vergiye tabi</option>
            </select>
          </div>
          <div className="input-group col-3">
            <label className="input-label" htmlFor="scenario-disabilityReliefType">Engellilik indirimi</label>
            <select
              id="scenario-disabilityReliefType"
              className="input-control"
              value={form.disabilityReliefType}
              onChange={(e) => setForm((current) => ({ ...current!, disabilityReliefType: e.target.value as ScenarioForm["disabilityReliefType"] }))}
            >
              <option value="none">Yok</option>
              <option value="firstDegree">1. derece · aylık {formatPayrollMoney(settings!.firstDegreeDisabilityDeduction)}</option>
              <option value="secondDegree">2. derece · aylık {formatPayrollMoney(settings!.secondDegreeDisabilityDeduction)}</option>
              <option value="thirdDegree">3. derece · aylık {formatPayrollMoney(settings!.thirdDegreeDisabilityDeduction)}</option>
            </select>
          </div>
          <div className="input-group col-3">
            <label className="input-label" htmlFor="scenario-employerSgkDiscount">SGK işveren indirimi</label>
            <select
              id="scenario-employerSgkDiscount"
              className="input-control"
              value={form.employerSgkDiscount}
              onChange={(e) => setForm((current) => ({ ...current!, employerSgkDiscount: e.target.value as ScenarioForm["employerSgkDiscount"] }))}
            >
              <option value="none">İndirimsiz</option>
              <option value="otherSectorTwoPoints">Diğer sektör 2 puan</option>
              <option value="manufacturingFivePoints">İmalat 5 puan</option>
            </select>
          </div>
        </div>
        <div className="payroll-rule-checks">
          <label className="leg-item">
            <input type="checkbox" checked={form.benefitTaxableConfirmed} onChange={(e) => setForm((current) => ({ ...current!, benefitTaxableConfirmed: e.target.checked }))} />
            Diğer ödemenin tamamının prime ve vergiye tabi olduğunu doğruladım
          </label>
          <label className="leg-item">
            <input type="checkbox" checked={form.applyMinimumWageTaxExemption} onChange={(e) => setForm((current) => ({ ...current!, applyMinimumWageTaxExemption: e.target.checked }))} />
            Asgari ücret gelir ve damga vergisi istisnası bu işverende (tek/en yüksek ücretli işveren) uygulanır
          </label>
        </div>
        <div className="payroll-note">
          <i aria-hidden="true" className="fa-solid fa-circle-info" />
          <span>Fazla çalışma, aylık ücret / {formatPayrollNumber(settings!.monthlyWorkingHours)} saat ve seçilen çarpanla hesaplanır. Özel statü veya farklı ödeme türü bu kapsamda hesaplanmaz.</span>
        </div>
        {preview.isError && (
          <p className="form-error" role="alert">
            {preview.error instanceof ApiError ? preview.error.message : "Kanuni ön hesap üretilemedi."}
          </p>
        )}
      </section>

      <aside className="card payroll-scenario-result" id="payroll-scenario-result">
        {result ? (
          <>
            <div className="scenario-result-head">
              <div><span className="status-pill info">Ön hesap</span><h4>{selectedRow?.name ?? "Tekil Hesaplama"}</h4><p>Bu hesap dönem bordrosuna işlenmedi.</p></div>
              <strong>{formatPayrollMoney(result.netPay)}</strong>
            </div>
            <div className="scenario-highlight-grid">
              <div><span>Saatlik ücret</span><strong>{formatPayrollMoney(result.hourlyRate)}</strong></div>
              <div><span>Fazla çalışma</span><strong>{formatPayrollMoney(result.overtimePay)}</strong><small>{form.overtimeHours} saat × {formatPayrollNumber(parseNumber(form.overtimeMultiplier, 1.5))}</small></div>
              <div><span>Toplam brüt</span><strong>{formatPayrollMoney(result.grossEarnings)}</strong></div>
              <div><span>İşveren maliyeti</span><strong>{formatPayrollMoney(result.employerCost)}</strong></div>
            </div>
            <div className="scenario-breakdown-grid">
              <section>
                <h5>Kazançlar</h5>
                <div className="payroll-line"><span>Brüt ücret</span><strong>{formatPayrollMoney(result.baseGross)}</strong></div>
                <div className="payroll-line"><span>Fazla çalışma</span><strong>{formatPayrollMoney(result.overtimePay)}</strong></div>
                <div className="payroll-line"><span>Prime tabi prim</span><strong>{formatPayrollMoney(parseNumber(form.premium))}</strong></div>
                <div className="payroll-line"><span>Nakit yol (tam tabi)</span><strong>{formatPayrollMoney(parseNumber(form.road))}</strong></div>
                <div className="payroll-line"><span>Nakit yemek</span><strong>{formatPayrollMoney(parseNumber(form.meal))}</strong></div>
                <div className="payroll-line"><span>Diğer ödeme (tam tabi)</span><strong>{formatPayrollMoney(parseNumber(form.benefit))}</strong></div>
              </section>
              <section>
                <h5>Kesintiler</h5>
                <div className="payroll-line deduction"><span>SGK işçi payı</span><strong>{formatPayrollMoney(result.sgkEmployee)}</strong></div>
                <div className="payroll-line deduction"><span>İşsizlik işçi payı</span><strong>{formatPayrollMoney(result.unemploymentEmployee)}</strong></div>
                <div className="payroll-line deduction"><span>Gelir vergisi</span><strong>{formatPayrollMoney(result.incomeTax)}</strong></div>
                <div className="payroll-line deduction"><span>Damga vergisi</span><strong>{formatPayrollMoney(result.stampTax)}</strong></div>
                <div className="payroll-line deduction"><span>Netten kesinti</span><strong>{formatPayrollMoney(parseNumber(form.deductions))}</strong></div>
              </section>
              <section>
                <h5>Matrah ve istisnalar</h5>
                <div className="payroll-line"><span>SGK matrahı</span><strong>{formatPayrollMoney(result.sgkBase)}</strong></div>
                <div className="payroll-line"><span>Gelir vergisi matrahı</span><strong>{formatPayrollMoney(result.incomeTaxBase)}</strong></div>
                <div className="payroll-line"><span>Engellilik matrah indirimi</span><strong>{formatPayrollMoney(result.disabilityIncomeTaxBaseDeduction)}</strong></div>
                <div className="payroll-line"><span>Yemek SGK istisnası</span><strong>{formatPayrollMoney(result.sgkExemptMealAllowance)}</strong></div>
                <div className="payroll-line"><span>Yemek GV istisnası</span><strong>{formatPayrollMoney(result.incomeTaxExemptMealAllowance)}</strong></div>
                <div className="payroll-line"><span>Asgari ücret GV istisnası</span><strong>{formatPayrollMoney(result.minimumWageIncomeTaxExemption)}</strong></div>
                <div className="payroll-line"><span>Uygulanan SGK işveren oranı</span><strong>%{formatPayrollNumber(result.appliedEmployerSgkRate)}</strong></div>
              </section>
            </div>
            {(result.warnings ?? []).length > 0 && (
              <div className="payroll-warning-stack">{result.warnings?.map((warning) => <span key={warning} className="status-pill pending">{warning}</span>)}</div>
            )}
          </>
        ) : <p className="pending-desc">Hesaplanıyor...</p>}
      </aside>
    </div>
  );
}
