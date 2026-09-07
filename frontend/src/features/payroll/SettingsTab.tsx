import { useEffect, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import {
  useApprovePayrollSettings,
  usePayrollSettings,
  usePayrollSettingsDrafts,
  useUpdatePayrollSettings,
  type PayrollSettingsDto,
} from "./queries";
import { TerminationRulesPanel } from "./TerminationRulesPanel";

const FIELDS = [
  ["overtimeMultiplier", "Fazla mesai çarpanı", "0.1"],
  ["monthlyWorkingHours", "Aylık çalışma saati", "1"],
  ["defaultWorkedDays", "Aylık SGK prim günü", "1"],
  ["sgkEmployeeRate", "SGK işçi oranı (%)", "0.01"],
  ["unemploymentEmployeeRate", "İşsizlik işçi oranı (%)", "0.01"],
  ["sgkEmployerRate", "Teşviksiz SGK işveren oranı (%)", "0.01"],
  ["unemploymentEmployerRate", "İşsizlik işveren oranı (%)", "0.01"],
  ["stampTaxRate", "Damga vergisi oranı (%)", "0.001"],
  ["minWageGross", "Brüt asgari ücret / SGK alt sınırı", "1"],
  ["sgkBaseMax", "SGK PEK üst sınırı", "1"],
  ["mealSgkDailyExemption", "Nakit yemek SGK günlük istisnası", "0.01"],
  ["mealIncomeTaxDailyExemption", "Nakit yemek GV günlük istisnası", "0.01"],
  ["otherSectorEmployerDiscountPoints", "Diğer sektör işveren indirimi (puan)", "0.01"],
  ["manufacturingEmployerDiscountPoints", "İmalat işveren indirimi (puan)", "0.01"],
  ["firstDegreeDisabilityDeduction", "1. derece engellilik indirimi (aylık)", "1"],
  ["secondDegreeDisabilityDeduction", "2. derece engellilik indirimi (aylık)", "1"],
  ["thirdDegreeDisabilityDeduction", "3. derece engellilik indirimi (aylık)", "1"],
] as const;

type SettingsField = (typeof FIELDS)[number][0];
type SettingsForm = Record<SettingsField | "effectiveFrom" | "sourceReference" | "reviewNotes", string>;
type TaxBracket = NonNullable<PayrollSettingsDto["taxBrackets"]>[number];

const nextMonthStart = () => {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1))
    .toISOString()
    .slice(0, 10);
};

const toForm = (data: PayrollSettingsDto, preserveIdentity = false): SettingsForm => ({
  ...Object.fromEntries(FIELDS.map(([key]) => [key, String(data[key] ?? 0)])),
  effectiveFrom: preserveIdentity ? data.effectiveFrom ?? nextMonthStart() : nextMonthStart(),
  sourceReference: preserveIdentity ? data.sourceReference ?? "" : "",
  reviewNotes: preserveIdentity ? data.reviewNotes ?? "" : "",
}) as SettingsForm;

const parseNumber = (value: string): number => {
  const parsed = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : 0;
};

const taxKey = (index: number, field: "limit" | "base" | "baseTax" | "rate") =>
  `tax-${index}-${field}`;

const taxForm = (brackets: TaxBracket[]) => Object.fromEntries(
  brackets.flatMap((bracket, index) => [
    [taxKey(index, "limit"), bracket.limit == null ? "" : String(bracket.limit)],
    [taxKey(index, "base"), String(bracket.base ?? 0)],
    [taxKey(index, "baseTax"), String(bracket.baseTax ?? 0)],
    [taxKey(index, "rate"), String(bracket.rate ?? 0)],
  ]),
);

export function SettingsTab() {
  const { showToast } = useToast();
  const settingsQ = usePayrollSettings(true);
  const draftsQ = usePayrollSettingsDrafts(true);
  const updateSettings = useUpdatePayrollSettings();
  const approveSettings = useApprovePayrollSettings();
  const [form, setForm] = useState<SettingsForm | null>(null);
  const [bracketForm, setBracketForm] = useState<Record<string, string>>({});
  const [draft, setDraft] = useState<PayrollSettingsDto | null>(null);
  const [feedback, setFeedback] = useState("");

  useEffect(() => {
    if (form === null && settingsQ.data) {
      setForm(toForm(settingsQ.data));
      setBracketForm(taxForm(settingsQ.data.taxBrackets ?? []));
    }
  }, [form, settingsQ.data]);

  if (settingsQ.isPending || form === null) return <PageLoading />;
  if (settingsQ.isError) return <PageError error={settingsQ.error} />;

  const data = settingsQ.data;
  const approvedBrackets = data.taxBrackets ?? [];
  const drafts = Array.isArray(draftsQ.data) ? draftsQ.data : [];
  const brackets = draft?.taxBrackets ?? approvedBrackets;

  const save = async () => {
    try {
      const saved = await updateSettings.mutateAsync({
        effectiveFrom: form.effectiveFrom,
        overtimeMultiplier: parseNumber(form.overtimeMultiplier),
        monthlyWorkingHours: parseNumber(form.monthlyWorkingHours),
        defaultWorkedDays: Math.round(parseNumber(form.defaultWorkedDays)),
        sgkEmployeeRate: parseNumber(form.sgkEmployeeRate),
        unemploymentEmployeeRate: parseNumber(form.unemploymentEmployeeRate),
        sgkEmployerRate: parseNumber(form.sgkEmployerRate),
        unemploymentEmployerRate: parseNumber(form.unemploymentEmployerRate),
        stampTaxRate: parseNumber(form.stampTaxRate),
        sgkBaseMin: parseNumber(form.minWageGross),
        sgkBaseMax: parseNumber(form.sgkBaseMax),
        monthlyMinWageIncomeTaxExemption: data.monthlyMinWageIncomeTaxExemption ?? 0,
        monthlyMinWageStampTaxExemption: data.monthlyMinWageStampTaxExemption ?? 0,
        minWageGross: parseNumber(form.minWageGross),
        mealSgkDailyExemption: parseNumber(form.mealSgkDailyExemption),
        mealIncomeTaxDailyExemption: parseNumber(form.mealIncomeTaxDailyExemption),
        otherSectorEmployerDiscountPoints: parseNumber(form.otherSectorEmployerDiscountPoints),
        manufacturingEmployerDiscountPoints: parseNumber(form.manufacturingEmployerDiscountPoints),
        firstDegreeDisabilityDeduction: parseNumber(form.firstDegreeDisabilityDeduction),
        secondDegreeDisabilityDeduction: parseNumber(form.secondDegreeDisabilityDeduction),
        thirdDegreeDisabilityDeduction: parseNumber(form.thirdDegreeDisabilityDeduction),
        sourceReference: form.sourceReference.trim(),
        reviewNotes: form.reviewNotes.trim(),
        expectedVersion: draft?.effectiveFrom === form.effectiveFrom ? draft.version : null,
        taxBrackets: brackets.map((_, index) => ({
          order: index + 1,
          limit: index === brackets.length - 1
            ? null
            : parseNumber(bracketForm[taxKey(index, "limit")]),
          base: parseNumber(bracketForm[taxKey(index, "base")]),
          baseTax: parseNumber(bracketForm[taxKey(index, "baseTax")]),
          rate: parseNumber(bracketForm[taxKey(index, "rate")]),
        })),
      });
      setDraft(saved);
      setFeedback(`${saved.effectiveFrom} yürürlük tarihli v${saved.version} taslak kaydedildi; hesaplarda henüz kullanılmaz.`);
      showToast("Bordro mevzuat taslağı kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Mevzuat taslağı kaydedilemedi.", "error");
    }
  };

  const approve = async () => {
    if (!draft?.id || !draft.version) return;
    try {
      const approved = await approveSettings.mutateAsync({ id: draft.id, expectedVersion: draft.version });
      setDraft(null);
      setForm(toForm(approved));
      setBracketForm(taxForm(approved.taxBrackets ?? []));
      setFeedback(`${approved.effectiveFrom} yürürlük tarihli v${approved.version} mevzuat seti onaylandı ve değiştirilemez hale geldi.`);
      showToast("Bordro mevzuat seti onaylandı.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Mevzuat seti onaylanamadı.", "error");
    }
  };

  const loadDraft = (item: PayrollSettingsDto) => {
    setDraft(item);
    setForm(toForm(item, true));
    setBracketForm(taxForm(item.taxBrackets ?? []));
    setFeedback(`${item.effectiveFrom} yürürlük tarihli v${item.version} açık taslak düzenleniyor.`);
  };

  const reset = () => {
    setForm(toForm(data));
    setBracketForm(taxForm(approvedBrackets));
    setDraft(null);
    setFeedback("");
    showToast("Form son onaylı mevzuat setinden yeniden hazırlandı.", "info");
  };

  return (
    <>
      <section className="card payroll-settings-card">
        <div className="card-header-clean">
          <div>
            <h4>Bordro Mevzuat Seti</h4>
            <p className="text-muted">
              Son onaylı setten yeni, yürürlük tarihli bir taslak üretin. Taslak açıkça onaylanmadan hiçbir hesaba girmez;
              onaylanan sürüm yerinde değiştirilemez.
            </p>
          </div>
          <span className="status-pill approved">Onaylı v{data.version}</span>
        </div>

        <div className="payroll-settings-current">
          <strong>{data.effectiveFrom} tarihinden geçerli</strong>
          <span>{data.reviewNotes}</span>
          <small>{data.sourceReference}</small>
        </div>

        {drafts.length > 0 && (
          <div className="payroll-settings-current" aria-label="Açık mevzuat taslakları">
            <strong>Açık taslaklar</strong>
            <div className="toolbar-actions">
              {drafts.map((item) => (
                <button
                  className="btn btn-secondary"
                  key={item.id}
                  onClick={() => loadDraft(item)}
                >
                  {item.effectiveFrom} · v{item.version} taslağını aç
                </button>
              ))}
            </div>
          </div>
        )}

        <div className="payroll-settings-grid">
          <div className="input-group">
            <label className="input-label" htmlFor="setting-effectiveFrom">Yeni set yürürlük tarihi</label>
            <input
              id="setting-effectiveFrom"
              className="input-control"
              type="date"
              value={form.effectiveFrom}
              onChange={(e) => setForm((current) => ({ ...current!, effectiveFrom: e.target.value }))}
            />
            <small>Ayın ilk günü olmalıdır.</small>
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
                onChange={(e) => setForm((current) => ({ ...current!, [key]: e.target.value }))}
              />
            </div>
          ))}
        </div>

        <div className="card-header-clean payroll-settings-subhead">
          <div>
            <h5>Ücret gelir vergisi tarifesi</h5>
            <p className="text-muted">Limit, taban ve taban vergisi bir önceki dilimle matematiksel olarak kesintisiz olmalıdır.</p>
          </div>
        </div>
        <div className="payroll-tax-table-wrap">
          <table className="data-table payroll-tax-table">
            <thead><tr><th>Dilim</th><th>Üst sınır</th><th>Taban</th><th>Taban vergisi</th><th>Oran</th></tr></thead>
            <tbody>
              {brackets.map((bracket, index) => (
                <tr key={bracket.order ?? index}>
                  <td>{index + 1}</td>
                  {(["limit", "base", "baseTax", "rate"] as const).map((field) => (
                    <td key={field}>
                      <label className="sr-only" htmlFor={taxKey(index, field)}>
                        {index + 1}. dilim {field}
                      </label>
                      <input
                        id={taxKey(index, field)}
                        className="input-control"
                        type="number"
                        step={field === "rate" ? "0.01" : "1"}
                        value={bracketForm[taxKey(index, field)] ?? ""}
                        disabled={field === "limit" && index === brackets.length - 1}
                        placeholder={field === "limit" ? "Sınırsız" : undefined}
                        onChange={(e) => setBracketForm((current) => ({
                          ...current,
                          [taxKey(index, field)]: e.target.value,
                        }))}
                      />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="form-grid payroll-settings-review">
          <div className="input-group col-6">
            <label className="input-label" htmlFor="setting-sourceReference">Resmî HTTPS kaynakları</label>
            <textarea
              id="setting-sourceReference"
              className="input-control"
              value={form.sourceReference}
              onChange={(e) => setForm((current) => ({ ...current!, sourceReference: e.target.value }))}
              placeholder="https://...; https://..."
            />
          </div>
          <div className="input-group col-6">
            <label className="input-label" htmlFor="setting-reviewNotes">Uzman inceleme kaydı</label>
            <textarea
              id="setting-reviewNotes"
              className="input-control"
              value={form.reviewNotes}
              onChange={(e) => setForm((current) => ({ ...current!, reviewNotes: e.target.value }))}
              placeholder="İnceleyen uzman, tarih ve kontrol notu"
            />
          </div>
        </div>

        <div className="payroll-settings-feedback" aria-live="polite">{feedback}</div>
        <div className="toolbar-actions payroll-settings-actions">
          <button className="btn btn-secondary" onClick={reset}>
            <i aria-hidden="true" className="fa-solid fa-rotate-left" /> Son onaylı setten hazırla
          </button>
          <button className="btn btn-secondary" onClick={save} disabled={updateSettings.isPending}>
            <i aria-hidden="true" className="fa-solid fa-floppy-disk" /> Taslağı kaydet
          </button>
          <button className="btn btn-primary" onClick={approve} disabled={!draft || approveSettings.isPending}>
            <i aria-hidden="true" className="fa-solid fa-shield-halved" /> Mevzuat setini onayla
          </button>
        </div>
        <div className="payroll-note">
          <i aria-hidden="true" className="fa-solid fa-triangle-exclamation" />
          <span>
            Onay; resmî kaynakların, oranların, matrahların, engellilik indirimlerinin ve vergi dilimlerinin yetkin bordro uzmanı veya mali müşavir
            tarafından doğrulandığı anlamına gelir. Özel statüler bu standart 4/a motorunda hesaplanmaz.
          </span>
        </div>
      </section>
      <TerminationRulesPanel />
    </>
  );
}
