import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { OffboardingSettlementRevisionPanel } from "./OffboardingSettlementRevisionPanel";
import { PayrollRevisionExternalReconciliationPanel } from "./PayrollRevisionExternalReconciliationPanel";
import {
  useApproveCompensationRemediationCase,
  useApprovedCompensationPayrollImpact,
  useApplyApprovedCompensationPayrollRevision,
  useApplyCompensationPayrollApplication,
  useCompensationRemediationContext,
  usePreviewCompensationPayrollApplication,
  useSaveCompensationRemediationCase,
  type CompensationRemediationContext,
  type ApprovedCompensationPayrollImpact,
  type CompensationPayrollApplication,
  type CompensationPayrollPreview,
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
    <p><strong>Neden ayrı işlem?</strong> Ücret farkı bordro hesabını ve yapılmış bildirimleri etkileyebilir. Bildirilmiş bir dönemde hata varsa düzeltme beyannamesi usulü ayrıca değerlendirilir (<a href="https://gib.gov.tr/mevzuat/kanun/433/teblig/6938" target="_blank" rel="noopener noreferrer">Muhtasar ve Prim Hizmet Beyannamesi Genel Tebliği, md. 12</a>). Açık, onaysız satırda önizlemeli uygulama yapılabilir. Onaylı dönemde etki dosyası incelenir; güvenli kapsamdaysa parasal sürüm ayrıca onaylanır.</p>
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
  const displayedImpact = current?.status === "approved" ? current.impactSnapshot : context.currentImpact;
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
      {displayedImpact.potentialPayrollRows.map((row) => <li key={`payroll-${row.rowId}`}>
        {row.year}/{String(row.month).padStart(2, "0")} bordro satırı #{row.rowId} · {row.periodStatus}/{row.rowStatus} · kayıtlı brüt {currency.format(row.recordedGrossSalary)}
        {current?.status === "approved" && <PayrollRemediationRow
          employeeId={employeeId} packageId={packageId} caseId={current.id}
          periodId={row.periodId} rowId={row.rowId} periodStatus={row.periodStatus} />}
      </li>)}
      {displayedImpact.offboardingCases.map((item) => <li key={`offboarding-${item.caseId}`}>
        {item.terminationDate} ayrılış vakası #{item.caseId} · {item.status} · ücret görüntüsü {currency.format(item.grossSalarySnapshot)}
        {current?.status === "approved" && <OffboardingSettlementRevisionPanel
          employeeId={employeeId} packageId={packageId} remediationCaseId={current.id}
          offboardingCaseId={item.caseId} />}
      </li>)}
    </ul>
    {!context.canOpen && !current && <p>Bu paket için bordro veya ayrılış etkisi bulunmadı. Doğrudan düzeltme durumunu kullanın.</p>}
    {current?.status === "approved" && <div role="status">
      <strong>Uzman incelemesi onaylandı.</strong> Referans: {current.expertReviewReference} · Onaylayan: {current.reviewedByUserId}
      <p>{current.reviewNotes}</p>
      <p>İnceleme onayı finansal kayıtları tek başına değiştirmez. Uygun açık bordro satırı aşağıda ayrıca önizlenip uygulanabilir.</p>
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

function PayrollRemediationRow({ employeeId, packageId, caseId, periodId, rowId, periodStatus }: {
  employeeId: number; packageId: number; caseId: number; periodId: number; rowId: number;
  periodStatus: string;
}) {
  const { showToast } = useToast();
  const impactMutation = useApprovedCompensationPayrollImpact();
  const approvedApply = useApplyApprovedCompensationPayrollRevision();
  const previewMutation = usePreviewCompensationPayrollApplication();
  const applyMutation = useApplyCompensationPayrollApplication();
  const [preview, setPreview] = useState<CompensationPayrollPreview | null>(null);
  const [impact, setImpact] = useState<ApprovedCompensationPayrollImpact | null>(null);
  const [expertReference, setExpertReference] = useState("");
  const [approvedNotes, setApprovedNotes] = useState("");
  const [sourceConfirmed, setSourceConfirmed] = useState(false);
  const [externalConfirmed, setExternalConfirmed] = useState(false);
  const [applied, setApplied] = useState<CompensationPayrollApplication | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const args = { employeeId, packageId, caseId, periodId, rowId };

  const loadPreview = async () => {
    setError(null);
    setConfirmed(false);
    setPreview(null);
    try {
      setPreview(await previewMutation.mutateAsync(args));
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Bordro telafisi önizlenemedi.");
    }
  };

  const apply = async () => {
    if (!preview?.canApply || !confirmed) return;
    setError(null);
    try {
      const result = await applyMutation.mutateAsync({ ...args,
        expectedPreviewToken: preview.previewToken });
      setApplied(result);
      setPreview(null);
      showToast("Açık bordro satırına telafi uygulandı; satır tekrar kontrol ve onay bekliyor.", "success");
    } catch (cause) {
      setPreview(null);
      setConfirmed(false);
      setError(cause instanceof ApiError ? cause.message : "Bordro değişti; yeniden önizleyin.");
    }
  };

  const application = applied ?? preview?.application;
  const applyApproved = async () => {
    if (!impact?.canApply || !sourceConfirmed || !externalConfirmed
      || !expertReference.trim() || !approvedNotes.trim()) return;
    setError(null);
    try {
      const revision = await approvedApply.mutateAsync({ ...args,
        expectedPreviewToken: impact.previewToken,
        expertReviewReference: expertReference.trim(), reviewNotes: approvedNotes.trim() });
      setImpact({ ...impact, revision, canApply: false,
        blockingReasons: ["Bu satır için parasal sürüm onaylandı."] });
      showToast("Yeni parasal bordro sürümü kaydedildi; dış ödeme ve bildirim ayrıca uzlaştırılmalı.", "success");
    } catch (cause) {
      setImpact(null);
      setSourceConfirmed(false);
      setExternalConfirmed(false);
      setError(cause instanceof ApiError ? cause.message : "Bordro değişti; etki dosyasını yeniden açın.");
    }
  };
  if (periodStatus === "approved" || periodStatus === "closed") return <div className="ez-compensation-review"
    aria-label={`Onaylı bordro satırı ${rowId} etki dosyası`}>
    <button type="button" className="btn btn-secondary"
      disabled={impactMutation.isPending}
      onClick={async () => {
        setError(null);
        try { setImpact(await impactMutation.mutateAsync(args)); }
        catch (cause) { setError(cause instanceof ApiError ? cause.message : "Etki dosyası açılamadı."); }
      }}>Onaylı satırın etki dosyasını aç</button>
    {impactMutation.isPending && <p>Onaylı sonuç ve sonraki dönemler inceleniyor…</p>}
    {impact && <>
      <p><strong>Uzman inceleme referansı:</strong> {impact.expertReviewReference} · {impact.reviewedAtUtc}</p>
      {impact.reviewWarnings.length > 0 && <ul>{impact.reviewWarnings.map((warning) =>
        <li key={warning}>{warning}</li>)}</ul>}
      {impact.blockingReasons?.length > 0 && <ul>{impact.blockingReasons.map((reason) =>
        <li key={reason}>{reason}</li>)}</ul>}
      {impact.hypothetical && <div className="ez-compensation-table-scroll"><table>
        <thead><tr><th>Alan</th><th>Onaylı kayıt</th><th>Öneriyle tek dönem hesabı</th><th>Fark</th></tr></thead>
        <tbody>{([
          ["Brüt kazanç", "grossEarnings"], ["GV matrahı", "incomeTaxBase"],
          ["Gelir vergisi", "incomeTax"], ["Net ödeme", "netPay"],
          ["İşveren maliyeti", "employerCost"],
        ] as const).map(([label, key]) => <tr key={key}><td>{label}</td>
          <td>{currency.format(impact.recorded[key])}</td>
          <td>{currency.format(impact.hypothetical![key])}</td>
          <td>{currency.format(impact.hypothetical![key] - impact.recorded[key])}</td></tr>)}</tbody>
      </table></div>}
      <p>Bu hesap onaylı incelemedeki öneriyi güncel bordro snapshot'ıyla karşılaştırır; tek başına yeni bir uzman onayı değildir. Yalnız bu dönemin olası farkını gösterir, ödeme veya bildirim tutarı değildir. Onaylı bordro sonucu değişmedi.</p>
      {impact.laterSameYearRows.length > 0 && <p>Aynı yıl içinde kümülatif GV matrahı açısından ayrıca incelenecek sonraki bordrolar: {impact.laterSameYearRows.map((row) =>
        `${row.year}/${String(row.month).padStart(2, "0")} (#${row.rowId}, ${row.status})`).join(", ")}.</p>}
      {impact.laterApprovedRows?.length > 0 && <p>Parasal uygulamayı kilitleyen sonraki onaylı satırlar: {impact.laterApprovedRows.map((row) =>
        `${row.year}/${String(row.month).padStart(2, "0")} (#${row.rowId})`).join(", ")}.</p>}
      <p>Sonraki onaylı satır varsa uygulama kilitlenir. Dış ödeme ve bildirim uygulama tarafından gerçekleştirilmez; farklar ayrıca uzlaştırılmalıdır.</p>
      {impact.revision && <p role="status">Geçerli parasal sürüm #{impact.revision.revisionNumber} · {impact.revision.approvedAtUtc} · önceki onaylı sonuç geçmişte korunuyor.</p>}
      {impact.revision && <PayrollRevisionExternalReconciliationPanel {...args}
        revisionId={impact.revision.id} />}
      {impact.canApply && <section aria-label="Onaylı bordro parasal revizyonu">
        <h5>Parasal sürümü onayla</h5>
        <p>Onaylı uzman dosyasındaki öneri yalnız bu satıra uygulanır. Eski hesap geçmişte kalır; yeni sonuç dönem ve pusulada geçerli olur.</p>
        <div className="input-group"><label className="input-label" htmlFor={`payroll-expert-${rowId}`}>Son uzman inceleme referansı</label>
          <input id={`payroll-expert-${rowId}`} className="input-control" value={expertReference}
            onChange={(event) => setExpertReference(event.target.value)} /></div>
        <div className="input-group"><label className="input-label" htmlFor={`payroll-notes-${rowId}`}>Uzman inceleme notu</label>
          <textarea id={`payroll-notes-${rowId}`} className="input-control" value={approvedNotes}
            onChange={(event) => setApprovedNotes(event.target.value)} /></div>
        <label><input type="checkbox" checked={sourceConfirmed}
          onChange={(event) => setSourceConfirmed(event.target.checked)} /> Kaynak ücret paketi ile bu onaylı bordro satırının ilişkisini doğruladım.</label>
        <label><input type="checkbox" checked={externalConfirmed}
          onChange={(event) => setExternalConfirmed(event.target.checked)} /> Dış ödeme ve bildirim farklarının ayrıca incelenip kanıtlanacağını anlıyorum.</label>
        <button type="button" className="btn btn-primary" onClick={applyApproved}
          disabled={approvedApply.isPending || !sourceConfirmed || !externalConfirmed
            || !expertReference.trim() || !approvedNotes.trim()}>Yeni parasal sürümü onayla</button>
      </section>}
    </>}
    {error && <p className="form-error" role="alert">{error}</p>}
  </div>;
  return <div className="ez-compensation-review" aria-label={`Bordro satırı ${rowId} telafi uygulaması`}>
    <button type="button" className="btn btn-secondary" onClick={loadPreview}
      disabled={previewMutation.isPending || applyMutation.isPending}>
      Satır #{rowId} için telafiyi önizle
    </button>
    {previewMutation.isPending && <p>Güncel bordro hesaplanıyor…</p>}
    {preview && <>
      {preview.blockingReasons.length > 0 && <ul>{preview.blockingReasons.map((reason) =>
        <li key={reason}>{reason}</li>)}</ul>}
      {preview.canApply && preview.after && <>
        <p>Bu satır kaynak ücret paketinin düzenli tutarlarıyla eşleşiyor. Yalnız bu beş tutar değişecek; diğer bordro girdileri korunacak.</p>
        <div className="ez-compensation-table-scroll"><table><thead><tr><th>Alan</th><th>Önce</th><th>Sonra</th></tr></thead><tbody>
          {([
            ["Brüt maaş", "grossSalary"], ["Düzenli prim", "premiumPay"],
            ["Yol yardımı", "roadAllowance"], ["Yemek yardımı", "mealAllowance"],
            ["Diğer düzenli yan hak", "benefitPay"], ["Brüt kazanç", "grossEarnings"],
            ["Net ödeme", "netPay"],
          ] as const).map(([label, key]) => <tr key={key}><td>{label}</td>
            <td>{currency.format(preview.before[key])}</td>
            <td>{currency.format(preview.after![key])}</td></tr>)}
        </tbody></table></div>
        <p>Uygulamadan sonra bordro satırı yeniden kontrol ve onay sürecinden geçer. Ücret paketinin kendisi ve onaylı geçmiş kayıtlar değişmez.</p>
        <label><input type="checkbox" checked={confirmed}
          onChange={(event) => setConfirmed(event.target.checked)} /> Kaynak belgeyle bu bordro satırının bu ücret paketinden üretildiğini doğruladım; önizlenen tutarları uyguluyorum.</label>
        <button type="button" className="btn btn-primary" onClick={apply}
          disabled={!confirmed || applyMutation.isPending}>Önizlenen telafiyi uygula</button>
      </>}
    </>}
    {application && <p role="status">Bu satıra telafi uygulandı: kayıt #{application.id} · {application.appliedAtUtc}</p>}
    {error && <p className="form-error" role="alert">{error}</p>}
  </div>;
}
