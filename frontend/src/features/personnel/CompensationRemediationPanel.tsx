import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import {
  useApproveCompensationRemediationCase,
  useCompensationRemediationContext,
  useSaveCompensationRemediationCase,
  type CompensationRemediationContext,
  type CompensationPackageValues,
} from "./queries";

const currency = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });
const amountFields: Array<{ key: keyof Omit<CompensationPackageValues, "sourceReference">; label: string }> = [
  { key: "monthlyGrossSalary", label: "Önerilen aylık brüt maaş" },
  { key: "regularPremium", label: "Önerilen düzenli prim" },
  { key: "roadAllowance", label: "Önerilen yol yardımı" },
  { key: "mealAllowance", label: "Önerilen yemek yardımı" },
  { key: "otherRegularBenefits", label: "Önerilen diğer düzenli yan hak" },
];

export function CompensationRemediationPanel({ employeeId, packageId }: {
  employeeId: number; packageId: number;
}) {
  const contextQ = useCompensationRemediationContext(employeeId, packageId);
  return <section aria-label="Ücret telafi incelemesi">
    <h4>Telafi inceleme dosyası</h4>
    <p>Bu dosya öneriyi ve etkilenmiş olabilecek kayıtları uzman incelemesine hazırlar. Onay, bordroyu, ayrılış hesabını veya ücret paketini değiştirmez; etkili düzeltme ayrı işlemdir.</p>
    {contextQ.isPending && <p>Etkiler yükleniyor…</p>}
    {contextQ.isError && <p className="form-error">Telafi durumu yüklenemedi.</p>}
    {contextQ.data && <RemediationContent key={`${contextQ.data.case?.id ?? "new"}-${contextQ.data.case?.version ?? 0}`}
      context={contextQ.data} employeeId={employeeId} packageId={packageId} />}
  </section>;
}

function RemediationContent({ context, employeeId, packageId }: {
  context: CompensationRemediationContext; employeeId: number; packageId: number;
}) {
  const { showToast } = useToast();
  const save = useSaveCompensationRemediationCase();
  const approve = useApproveCompensationRemediationCase();
  const current = context.case;
  const initial = current?.proposedValues ?? context.package;
  const [values, setValues] = useState<CompensationPackageValues>({
    monthlyGrossSalary: initial.monthlyGrossSalary, regularPremium: initial.regularPremium,
    roadAllowance: initial.roadAllowance, mealAllowance: initial.mealAllowance,
    otherRegularBenefits: initial.otherRegularBenefits, sourceReference: initial.sourceReference,
  });
  const [issue, setIssue] = useState(current?.issueDescription ?? "");
  const [effectiveFrom, setEffectiveFrom] = useState(current?.proposedEffectiveFrom ?? "");
  const [expertReference, setExpertReference] = useState("");
  const [reviewNotes, setReviewNotes] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const draftDirty = current?.status === "draft" && (
    issue !== current.issueDescription || effectiveFrom !== (current.proposedEffectiveFrom ?? "")
    || values.sourceReference !== current.proposedValues.sourceReference
    || amountFields.some(({ key }) => values[key] !== current.proposedValues[key]));

  const saveDraft = async () => {
    setError(null);
    if (!issue.trim() || issue.trim().length > 2000 || !values.sourceReference.trim()
      || values.sourceReference.trim().length > 256 || values.monthlyGrossSalary <= 0
      || amountFields.slice(1).some(({ key }) => values[key] < 0 || !Number.isFinite(values[key]))) {
      setError("Sorun açıklamasını, kaynak referansını ve geçerli önerilen tutarları tamamlayın.");
      return;
    }
    try {
      await save.mutateAsync({ employeeId, packageId, caseId: current?.id ?? null, body: {
        ...values, sourceReference: values.sourceReference.trim(),
        expectedPackageVersion: context.package.version,
        expectedCaseVersion: current?.version ?? null,
        issueDescription: issue.trim(), proposedEffectiveFrom: effectiveFrom || null,
      } });
      showToast("Telafi inceleme taslağı kaydedildi; etkili kayıtlar değişmedi.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "İnceleme taslağı kaydedilemedi.");
    }
  };

  const approveCase = async () => {
    if (!current) return;
    setError(null);
    if (draftDirty) {
      setError("Değişen öneriyi önce taslağa kaydedin; ardından onaylayın.");
      return;
    }
    if (!expertReference.trim() || expertReference.trim().length > 256
      || !reviewNotes.trim() || reviewNotes.trim().length > 2000 || !confirmed) {
      setError("Uzman inceleme referansı, notu ve açık teyit gereklidir.");
      return;
    }
    try {
      await approve.mutateAsync({ employeeId, packageId, caseId: current.id,
        expectedCaseVersion: current.version, expertReviewReference: expertReference.trim(),
        reviewNotes: reviewNotes.trim() });
      showToast("İnceleme dosyası onaylandı; finansal kayıtlar değişmedi.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "İnceleme onaylanamadı; etkileri yeniden kontrol edin.");
    }
  };

  return <>
    <p><strong>Mevcut paket:</strong> {context.package.effectiveFrom} · {currency.format(context.package.monthlyGrossSalary)} · sürüm {context.package.version}</p>
    <p>Bordro satırında kaynak paket kimliği tutulmadığı için aşağıdaki bordrolar tarih aralığına göre <strong>olası etki</strong> olarak gösterilir; ilişkilerini uzman doğrulamalıdır.</p>
    <ul>
      {context.currentImpact.potentialPayrollRows.map((row) => <li key={`payroll-${row.rowId}`}>
        {row.year}/{String(row.month).padStart(2, "0")} bordro satırı #{row.rowId} · {row.periodStatus}/{row.rowStatus} · kayıtlı brüt {currency.format(row.recordedGrossSalary)}
      </li>)}
      {context.currentImpact.offboardingCases.map((item) => <li key={`offboarding-${item.caseId}`}>
        {item.terminationDate} ayrılış vakası #{item.caseId} · {item.status} · ücret görüntüsü {currency.format(item.grossSalarySnapshot)}
      </li>)}
    </ul>
    {!context.canOpen && !current && <p>Bu paket için bordro veya ayrılış etkisi bulunmadı. Doğrudan düzeltme durumunu kullanın.</p>}
    {current?.status === "approved" && <div role="status">
      <strong>Uzman incelemesi onaylandı.</strong> Referans: {current.expertReviewReference} · Onaylayan: {current.reviewedByUserId}
      <p>{current.reviewNotes}</p>
      <p>Önerilen tutar ve tarih uygulanmadı; etkili düzeltme ayrı bir işlem olarak bekliyor.</p>
    </div>}
    {(context.canOpen || current?.status === "draft") && current?.status !== "approved" && <>
      <div className="form-grid-2">
        <div className="input-group form-span-2">
          <label className="input-label" htmlFor="pm-remediation-issue">Hatanın açıklaması ve kaynağı</label>
          <textarea id="pm-remediation-issue" className="input-control" value={issue}
            onChange={(event) => setIssue(event.target.value)} />
        </div>
        <div className="input-group">
          <label className="input-label" htmlFor="pm-remediation-date">Önerilen yürürlük başlangıcı (varsa)</label>
          <input id="pm-remediation-date" type="date" className="input-control" value={effectiveFrom}
            onChange={(event) => setEffectiveFrom(event.target.value)} />
        </div>
        {amountFields.map(({ key, label }) => <div className="input-group" key={key}>
          <label className="input-label" htmlFor={`pm-remediation-${key}`}>{label}</label>
          <input id={`pm-remediation-${key}`} type="number" min="0" step="0.01" className="input-control"
            value={values[key]} onChange={(event) => setValues({ ...values, [key]: Number(event.target.value) })} />
        </div>)}
        <div className="input-group form-span-2">
          <label className="input-label" htmlFor="pm-remediation-source">Önerilen kaynak / karar referansı</label>
          <input id="pm-remediation-source" className="input-control" value={values.sourceReference}
            onChange={(event) => setValues({ ...values, sourceReference: event.target.value })} />
        </div>
      </div>
      <button type="button" className="btn btn-secondary" onClick={saveDraft} disabled={save.isPending || approve.isPending}>
        {current ? "İnceleme taslağını güncelle" : "İnceleme dosyasını aç"}
      </button>
      {current && <section className="ez-compensation-review" aria-label="Uzman incelemesini onayla">
        <h4>Uzman incelemesi</h4>
        <p>Kaynak belgeleri, olası bordroları ve ayrılış görüntülerini karşılaştırın. Dosyayı açan İK yöneticisi de uzman incelemesi referansı ile onaylayabilir.</p>
        <div className="input-group"><label className="input-label" htmlFor="pm-remediation-expert">Uzman inceleme referansı</label>
          <input id="pm-remediation-expert" className="input-control" value={expertReference}
            onChange={(event) => setExpertReference(event.target.value)} /></div>
        <div className="input-group"><label className="input-label" htmlFor="pm-remediation-notes">Uzman inceleme notu</label>
          <textarea id="pm-remediation-notes" className="input-control" value={reviewNotes}
            onChange={(event) => setReviewNotes(event.target.value)} /></div>
        <label><input type="checkbox" checked={confirmed} onChange={(event) => setConfirmed(event.target.checked)} /> Kaynakları ve olası etkileri inceledim; bu onayın finansal kayıtları değiştirmediğini anlıyorum.</label>
        {draftDirty && <p>Onaydan önce değişen taslağı kaydedin.</p>}
        <button type="button" className="btn btn-primary" onClick={approveCase} disabled={approve.isPending || save.isPending || draftDirty}>
          İnceleme dosyasını onayla
        </button>
      </section>}
    </>}
    {error && <p className="form-error" role="alert">{error}</p>}
  </>;
}
