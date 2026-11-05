import { useState } from "react";
import { GuidanceOperationDrawer } from "../guidance/GuidanceOperationDrawer";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import type { SaveTerminationCalculationRuleBody, TerminationCalculationRuleDto } from "../personnel/queries";
import {
  useApproveTerminationCalculationRule,
  useSaveTerminationCalculationRule,
  useTerminationCalculationRules,
} from "./queries";

const today = () => new Date().toISOString().slice(0, 10);

const emptyRule = (): SaveTerminationCalculationRuleBody => ({
  effectiveFrom: today(),
  dailyWageDivisor: 30,
  severanceDaysPerYear: 30,
  minimumSeveranceServiceDays: 365,
  severanceCeiling: 0,
  noticeDaysUnderSixMonths: 14,
  noticeDaysSixToEighteenMonths: 28,
  noticeDaysEighteenToThirtySixMonths: 42,
  noticeDaysOverThirtySixMonths: 56,
  noticeFirstThresholdMonths: 6,
  noticeSecondThresholdMonths: 18,
  noticeThirdThresholdMonths: 36,
  includeRegularPremiumInDressedWage: true,
  includeRoadAllowanceInDressedWage: true,
  includeMealAllowanceInDressedWage: true,
  includeOtherRegularBenefitsInDressedWage: true,
  sourceReference: "",
  reviewNotes: "",
  expectedVersion: null,
});

const editableRule = (rule: TerminationCalculationRuleDto): SaveTerminationCalculationRuleBody => ({
  ...rule,
  expectedVersion: rule.version,
});

const NUMBER_FIELDS: Array<[keyof SaveTerminationCalculationRuleBody, string, string]> = [
  ["dailyWageDivisor", "Günlük ücret böleni", "0.01"],
  ["severanceDaysPerYear", "Yıl başına kıdem günü", "0.01"],
  ["minimumSeveranceServiceDays", "Asgari kıdem hizmet günü", "1"],
  ["severanceCeiling", "Kıdem tavanı (₺)", "0.01"],
  ["noticeDaysUnderSixMonths", "İhbar: ilk kademe (gün)", "1"],
  ["noticeDaysSixToEighteenMonths", "İhbar: ikinci kademe (gün)", "1"],
  ["noticeDaysEighteenToThirtySixMonths", "İhbar: üçüncü kademe (gün)", "1"],
  ["noticeDaysOverThirtySixMonths", "İhbar: dördüncü kademe (gün)", "1"],
  ["noticeFirstThresholdMonths", "İlk eşik (takvim ayı)", "1"],
  ["noticeSecondThresholdMonths", "İkinci eşik (takvim ayı)", "1"],
  ["noticeThirdThresholdMonths", "Üçüncü eşik (takvim ayı)", "1"],
];

export function TerminationRulesPanel() {
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const { showToast } = useToast();
  const rulesQ = useTerminationCalculationRules();
  const saveRule = useSaveTerminationCalculationRule();
  const approveRule = useApproveTerminationCalculationRule();
  const [editingId, setEditingId] = useState<number | undefined>();
  const [form, setForm] = useState<SaveTerminationCalculationRuleBody>(emptyRule);

  const setNumber = (key: keyof SaveTerminationCalculationRuleBody, raw: string) => {
    const value = Number(raw.replace(",", "."));
    setForm((current) => ({ ...current, [key]: Number.isFinite(value) ? value : 0 }));
  };

  const save = async () => {
    if (!form.sourceReference.trim()) {
      showToast("Resmî kaynak ve uzman inceleme referansı zorunludur.", "error");
      return;
    }

    try {
      await saveRule.mutateAsync({
        id: editingId,
        body: { ...form, guidanceContentRevisionId: guidanceVersion ?? undefined, sourceReference: form.sourceReference.trim(), reviewNotes: form.reviewNotes?.trim() || null },
      });
      setEditingId(undefined);
      setForm(emptyRule());
      showToast("İşten çıkış hesap kuralı taslak olarak kaydedildi.", "success");
    } catch (cause) {
      showToast(cause instanceof ApiError ? cause.message : "Hesap kuralı kaydedilemedi.", "error");
    }
  };

  const approve = async (rule: TerminationCalculationRuleDto) => {
    if (!window.confirm(
      `${rule.effectiveFrom} sürümünü onaylıyor musunuz? Onaydan sonra bu sürüm değiştirilemez.`,
    )) return;
    try {
      await approveRule.mutateAsync({ id: rule.id, expectedVersion: rule.version, guidanceContentRevisionId: guidanceVersion ?? undefined });
      showToast("Kural sürümü onaylandı ve değişmez hale getirildi.", "success");
    } catch (cause) {
      showToast(cause instanceof ApiError ? cause.message : "Kural onaylanamadı.", "error");
    }
  };

  return (
    <section className="card payroll-settings-card ez-workbench">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationDrawer process="payroll" step="settings-review" value={guidanceVersion} onChange={setGuidanceVersion} disabled={saveRule.isPending || approveRule.isPending} /></div>
          <div className="ez-workbench-main">


      <div className="card-header-clean">
        <div>
          <h4>İşten Çıkış Hesap Kuralları</h4>
          <p className="text-muted">
            Değerleri resmî kaynak ve yetkili uzman incelemesiyle taslaklaştırın. Onaylanan sürüm
            değiştirilemez; ayrılış tarihindeki son onaylı sürüm yalnız hesap taslağı üretir.
          </p>
        </div>
        <button className="btn btn-primary" onClick={save} disabled={saveRule.isPending}>
          <i aria-hidden="true" className="fa-solid fa-floppy-disk" /> {editingId ? "Taslağı güncelle" : "Taslak oluştur"}
        </button>
      </div>

      <div className="payroll-settings-grid">
        <div className="input-group">
          <label className="input-label" htmlFor="termination-rule-date">Yürürlük tarihi</label>
          <input id="termination-rule-date" className="input-control" type="date"
            value={form.effectiveFrom}
            onChange={(event) => setForm((current) => ({ ...current, effectiveFrom: event.target.value }))} />
        </div>
        {NUMBER_FIELDS.map(([key, label, step]) => (
          <div className="input-group" key={key}>
            <label className="input-label" htmlFor={`termination-rule-${key}`}>{label}</label>
            <input id={`termination-rule-${key}`} className="input-control" type="number" min="0" step={step}
              value={String(form[key] ?? 0)} onChange={(event) => setNumber(key, event.target.value)} />
          </div>
        ))}
        <div className="input-group form-span-2">
          <label className="input-label" htmlFor="termination-rule-source">Resmî kaynak + uzman inceleme referansı</label>
          <input id="termination-rule-source" className="input-control" value={form.sourceReference}
            onChange={(event) => setForm((current) => ({ ...current, sourceReference: event.target.value }))}
            placeholder="Kaynak URL/belge sürümü ve uzman onay numarası" />
        </div>
        <div className="input-group form-span-2">
          <label className="input-label" htmlFor="termination-rule-notes">İnceleme notu</label>
          <textarea id="termination-rule-notes" className="input-control" rows={2}
            value={form.reviewNotes ?? ""}
            onChange={(event) => setForm((current) => ({ ...current, reviewNotes: event.target.value }))} />
        </div>
      </div>

      <div className="payroll-rule-checks">
        {([
          ["includeRegularPremiumInDressedWage", "Düzenli prim"],
          ["includeRoadAllowanceInDressedWage", "Yol yardımı"],
          ["includeMealAllowanceInDressedWage", "Yemek yardımı"],
          ["includeOtherRegularBenefitsInDressedWage", "Diğer düzenli yan haklar"],
        ] as Array<[keyof SaveTerminationCalculationRuleBody, string]>).map(([key, label]) => (
          <label key={key} className="checkbox-line">
            <input type="checkbox" checked={Boolean(form[key])}
              onChange={(event) => setForm((current) => ({ ...current, [key]: event.target.checked }))} />
            {label} giydirilmiş ücrete dahil
          </label>
        ))}
      </div>

      <div className="payroll-rule-list">
        {rulesQ.isPending && <p className="text-muted">Kural sürümleri yükleniyor…</p>}
        {rulesQ.isError && <p className="form-error">Kural sürümleri yüklenemedi.</p>}
        {rulesQ.data?.map((rule) => (
          <div className="payroll-rule-row" key={rule.id}>
            <div>
              <strong>{rule.effectiveFrom}</strong> — {rule.status === "approved" ? "Onaylı / değişmez" : "Taslak"}
              <p className="text-muted">Tavan: {rule.severanceCeiling.toLocaleString("tr-TR")} ₺ · Kaynak: {rule.sourceReference}</p>
            </div>
            {rule.status === "draft" && (
              <div className="toolbar-actions">
                <button className="btn btn-secondary" onClick={() => { setEditingId(rule.id); setForm(editableRule(rule)); }}>
                  Düzenle
                </button>
                <button className="btn btn-primary" onClick={() => approve(rule)} disabled={approveRule.isPending}>
                  Uzman onayını kilitle
                </button>
              </div>
            )}
          </div>
        ))}
      </div>
      <div className="payroll-note">
        <i aria-hidden="true" className="fa-solid fa-shield-halved" />
        <span>Bu panel hukuki uygunluk kararı vermez. Kural onayı ve vaka bazlı hak ediş teyidi iki ayrı insan kontrolüdür.</span>
      </div>
    </div></div></section>
  );
}
