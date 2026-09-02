import { useEffect, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { usePayrollSettings, useUpdatePayrollSettings, type PayrollSettingsDto } from "./queries";
import { TerminationRulesPanel } from "./TerminationRulesPanel";

const FIELDS: [keyof PayrollSettingsDto & string, string, string][] = [
  ["overtimeMultiplier", "Fazla mesai çarpanı", "0.1"],
  ["monthlyWorkingHours", "Aylık çalışma saati", "1"],
  ["defaultWorkedDays", "Varsayılan çalışılan gün", "1"],
  ["sgkEmployeeRate", "SGK işçi oranı (%)", "0.01"],
  ["unemploymentEmployeeRate", "İşsizlik işçi oranı (%)", "0.01"],
  ["sgkEmployerRate", "SGK işveren oranı (%)", "0.01"],
  ["unemploymentEmployerRate", "İşsizlik işveren oranı (%)", "0.01"],
  ["stampTaxRate", "Damga vergisi oranı (%)", "0.001"],
  ["sgkBaseMin", "SGK PEK alt sınırı", "1"],
  ["sgkBaseMax", "SGK PEK üst sınırı", "1"],
  ["monthlyMinWageIncomeTaxExemption", "Asgari ücret GV istisnası", "0.01"],
  ["monthlyMinWageStampTaxExemption", "Asgari ücret damga istisnası", "0.01"],
];

const TODAY = () => new Date().toISOString().slice(0, 10);

// Yürürlük tarihi sayısal alanlardan ayrı tutulur: parametreler yıla değil bu
// tarihe bağlıdır ve yıl ortası değişimler (temmuz asgari ücret) böyle modellenir.
const toForm = (data: PayrollSettingsDto): Record<string, string> => ({
  ...Object.fromEntries(FIELDS.map(([key]) => [key, String(data[key] ?? 0)])),
  effectiveFrom: data.effectiveFrom ?? TODAY(),
});

const parseNumber = (value: string): number => {
  const parsed = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : 0;
};

export function SettingsTab() {
  const { showToast } = useToast();
  const settingsQ = usePayrollSettings(true);
  const updateSettings = useUpdatePayrollSettings();
  const [form, setForm] = useState<Record<string, string> | null>(null);
  const [feedback, setFeedback] = useState("");

  useEffect(() => {
    if (form === null && settingsQ.data) setForm(toForm(settingsQ.data));
  }, [form, settingsQ.data]);

  if (settingsQ.isPending || form === null) return <PageLoading />;
  if (settingsQ.isError) return <PageError error={settingsQ.error} />;

  const data = settingsQ.data;

  const save = async () => {
    try {
      await updateSettings.mutateAsync({
        effectiveFrom: form.effectiveFrom || TODAY(),
        overtimeMultiplier: parseNumber(form.overtimeMultiplier),
        monthlyWorkingHours: parseNumber(form.monthlyWorkingHours),
        defaultWorkedDays: Math.round(parseNumber(form.defaultWorkedDays)),
        sgkEmployeeRate: parseNumber(form.sgkEmployeeRate),
        unemploymentEmployeeRate: parseNumber(form.unemploymentEmployeeRate),
        sgkEmployerRate: parseNumber(form.sgkEmployerRate),
        unemploymentEmployerRate: parseNumber(form.unemploymentEmployerRate),
        stampTaxRate: parseNumber(form.stampTaxRate),
        sgkBaseMin: parseNumber(form.sgkBaseMin),
        sgkBaseMax: parseNumber(form.sgkBaseMax),
        monthlyMinWageIncomeTaxExemption: parseNumber(form.monthlyMinWageIncomeTaxExemption),
        monthlyMinWageStampTaxExemption: parseNumber(form.monthlyMinWageStampTaxExemption),
        minWageGross: parseNumber(form.sgkBaseMin),
      });
      setFeedback(`${form.effectiveFrom} tarihinden itibaren geçerli. Bu tarihten önceki dönemler etkilenmez.`);
      showToast("Bordro ayarları kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Ayarlar kaydedilemedi.", "error");
    }
  };

  const reset = () => {
    setForm(toForm(data));
    setFeedback("");
    showToast("Bordro ayarları varsayılana döndürüldü.", "info");
  };

  return (
    <>
      <section className="card payroll-settings-card">
      <div className="card-header-clean">
        <div>
          <h4>Bordro Ayarları</h4>
          <p className="text-muted">
            Şirket varsayılanlarını düzenleyin. Parametreler yürürlük tarihine göre saklanır;
            asgari ücret yıl ortasında değişirse yeni bir tarih girip kaydedin — önceki dönemler etkilenmez.
          </p>
        </div>
        <div className="toolbar-actions">
          <button className="btn btn-secondary" onClick={reset}>
            <i aria-hidden="true" className="fa-solid fa-rotate-left" /> Varsayılana dön
          </button>
          <button className="btn btn-primary" onClick={save} disabled={updateSettings.isPending}>
            <i aria-hidden="true" className="fa-solid fa-floppy-disk" /> Kaydet
          </button>
        </div>
      </div>
      <div className="payroll-settings-grid">
        <div className="input-group">
          <label className="input-label" htmlFor="setting-effectiveFrom">Yürürlük tarihi</label>
          <input
            id="setting-effectiveFrom"
            className="input-control"
            type="date"
            value={form.effectiveFrom}
            onChange={(e) => setForm((f) => ({ ...f!, effectiveFrom: e.target.value }))}
          />
        </div>
        {FIELDS.map(([key, label, step]) => (
          <div key={key} className="input-group">
            <label className="input-label" htmlFor={`setting-${key}`}>{label}</label>
            <input
              id={`setting-${key}`}
              className="input-control"
              type="number"
              step={step}
              value={form[key]}
              onChange={(e) => setForm((f) => ({ ...f!, [key]: e.target.value }))}
            />
          </div>
        ))}
      </div>
      <div className="payroll-settings-feedback" aria-live="polite">{feedback}</div>
      <div className="payroll-note">
        <i aria-hidden="true" className="fa-solid fa-circle-info" />
        <span>Bu değerler dönem bordrosu ve tekil hesaplama ön hesabında kullanılır. Üretim öncesi mevzuat ve müşavir doğrulaması gerekir.</span>
      </div>
      </section>
      <TerminationRulesPanel />
    </>
  );
}
