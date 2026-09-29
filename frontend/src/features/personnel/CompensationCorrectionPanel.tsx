import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { CompensationRemediationPanel } from "./CompensationRemediationPanel";
import {
  useCompensationCorrectionContext,
  useCorrectCompensationPackage,
  type CompensationCorrectionContext,
  type CompensationPackageValues,
} from "./queries";

const currency = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });
const fields: Array<{ key: keyof Omit<CompensationPackageValues, "sourceReference">; label: string }> = [
  { key: "monthlyGrossSalary", label: "Aylık brüt maaş" },
  { key: "regularPremium", label: "Düzenli prim" },
  { key: "roadAllowance", label: "Yol yardımı" },
  { key: "mealAllowance", label: "Yemek yardımı" },
  { key: "otherRegularBenefits", label: "Diğer düzenli yan hak" },
];

export function CompensationCorrectionPanel({ employeeId, packageId, onClose }: {
  employeeId: number; packageId: number; onClose: () => void;
}) {
  const contextQ = useCompensationCorrectionContext(employeeId, packageId);
  return <section className="ez-compensation-review" aria-label="Ücret paketi düzeltme">
    <h4>Ücret paketi düzeltme</h4>
    {contextQ.isPending && <p>Ücret paketi kontrol ediliyor…</p>}
    {contextQ.isError && <p className="form-error">Düzeltme durumu yüklenemedi. Yeniden deneyin.</p>}
    {contextQ.data && <CorrectionForm context={contextQ.data} employeeId={employeeId} packageId={packageId} onClose={onClose} />}
    <button type="button" className="btn btn-ghost" onClick={onClose}>Düzeltmeyi kapat</button>
  </section>;
}

function CorrectionForm({ context, employeeId, packageId, onClose }: {
  context: CompensationCorrectionContext; employeeId: number; packageId: number; onClose: () => void;
}) {
  const { showToast } = useToast();
  const correct = useCorrectCompensationPackage();
  const [values, setValues] = useState<CompensationPackageValues>(() => ({
    monthlyGrossSalary: context.package.monthlyGrossSalary,
    regularPremium: context.package.regularPremium,
    roadAllowance: context.package.roadAllowance,
    mealAllowance: context.package.mealAllowance,
    otherRegularBenefits: context.package.otherRegularBenefits,
    sourceReference: context.package.sourceReference,
  }));
  const [reason, setReason] = useState("");
  const [review, setReview] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const changed = fields.some(({ key }) => values[key] !== context.package[key])
    || values.sourceReference.trim() !== context.package.sourceReference;

  const prepare = () => {
    setError(null);
    if (!Number.isFinite(values.monthlyGrossSalary) || values.monthlyGrossSalary <= 0
      || fields.slice(1).some(({ key }) => !Number.isFinite(values[key]) || values[key] < 0)) {
      setError("Brüt maaş pozitif, diğer tutarlar sıfır veya pozitif olmalıdır.");
    } else if (!values.sourceReference.trim() || values.sourceReference.trim().length > 256) {
      setError("Kaynak referansı zorunludur ve 256 karakteri geçemez.");
    } else if (!reason.trim() || reason.trim().length > 500) {
      setError("Düzeltme gerekçesi zorunludur ve 500 karakteri geçemez.");
    } else if (!changed) {
      setError("Kaydetmek için en az bir tutar veya kaynak referansı değişmelidir.");
    } else {
      setReview(true);
    }
  };

  const save = async () => {
    setError(null);
    try {
      await correct.mutateAsync({ employeeId, packageId, body: {
        ...values, sourceReference: values.sourceReference.trim(),
        expectedVersion: context.package.version, reason: reason.trim(),
      } });
      showToast("Ücret paketi düzeltildi; önceki değerler geçmişte korundu.", "success");
      onClose();
    } catch (cause) {
      setReview(false);
      setError(cause instanceof ApiError ? cause.message : "Ücret paketi düzeltilemedi; güncel durumu yeniden açın.");
    }
  };

  return <>
    <p><strong>Yürürlük:</strong> {context.package.effectiveFrom}{context.package.effectiveTo ? ` – ${context.package.effectiveTo}` : " – güncel"} · <strong>Sürüm:</strong> {context.package.version}</p>
    <p>Bu işlem yalnızca tutarları ve kaynak referansını düzeltir. Yürürlük tarihi ayrı bir işlemdir. Bordro veya ayrılışta kullanılan değerler kayıt geçmişi olduğu için doğrudan değiştirilemez; böyle bir durumda telafi akışı gerekir.</p>
    {!context.canCorrect && <div role="alert"><strong>Doğrudan düzeltme kilitli.</strong><ul>{context.blockingReasons.map((item) => <li key={item}>{item}</li>)}</ul></div>}
    {!context.canCorrect && <CompensationRemediationPanel employeeId={employeeId} packageId={packageId} />}
    {context.canCorrect && <>
      <div className="form-grid-2">
        {fields.map(({ key, label }) => <div className="input-group" key={key}>
          <label className="input-label" htmlFor={`pm-correct-${key}`}>{label}</label>
          <input id={`pm-correct-${key}`} className="input-control" type="number" min="0" step="0.01"
            value={values[key]} onChange={(event) => { setValues({ ...values, [key]: Number(event.target.value) }); setReview(false); }} />
        </div>)}
        <div className="input-group form-span-2">
          <label className="input-label" htmlFor="pm-correct-source">Düzeltilmiş kaynak / karar referansı</label>
          <input id="pm-correct-source" className="input-control" value={values.sourceReference}
            onChange={(event) => { setValues({ ...values, sourceReference: event.target.value }); setReview(false); }} />
        </div>
        <div className="input-group form-span-2">
          <label className="input-label" htmlFor="pm-correct-reason">Düzeltme gerekçesi</label>
          <textarea id="pm-correct-reason" className="input-control" value={reason}
            onChange={(event) => { setReason(event.target.value); setReview(false); }} placeholder="Hangi kaynakta ne hatalıydı?" />
        </div>
      </div>
      {error && <p className="form-error" role="alert">{error}</p>}
      <button type="button" className="btn btn-secondary" onClick={prepare} disabled={correct.isPending}>Düzeltmeyi kontrol et</button>
      {review && <section aria-label="Düzeltme ön kontrolü" className="ez-compensation-review">
        <h4>Düzeltme ön kontrolü</h4>
        <p><strong>Brüt maaş:</strong> {currency.format(context.package.monthlyGrossSalary)} → {currency.format(values.monthlyGrossSalary)}</p>
        <p><strong>Kaynak:</strong> {context.package.sourceReference} → {values.sourceReference.trim()}</p>
        <p><strong>Gerekçe:</strong> {reason.trim()}</p>
        <p>Kaydettiğinizde eski ve yeni tutarların tamamı, gerekçe ve işlemi yapan kişi denetim geçmişine eklenir.</p>
        <button type="button" className="btn btn-primary" onClick={save} disabled={correct.isPending}>Düzeltmeyi onayla ve kaydet</button>
      </section>}
    </>}
    <details><summary>Düzeltme geçmişi ({context.history.length})</summary>
      {context.history.length === 0 && <p>Bu pakette daha önce düzeltme yapılmadı.</p>}
      {context.history.map((item) => <p key={item.toVersion}>
        <strong>Sürüm {item.fromVersion} → {item.toVersion}</strong> · {new Date(item.correctedAtUtc).toLocaleString("tr-TR")}
        <br />{item.reason} · İşlemi yapan: {item.actorUserId}
        <br />Brüt maaş: {currency.format(item.before.monthlyGrossSalary)} → {currency.format(item.after.monthlyGrossSalary)}
        <br />Kaynak: {item.before.sourceReference} → {item.after.sourceReference}
      </p>)}
    </details>
  </>;
}
