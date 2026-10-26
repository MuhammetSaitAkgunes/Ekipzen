import { useState } from "react";
import { ApiError } from "../../api/client";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { useToast } from "../../layout/ToastProvider";
import {
  useCorrectCompensationDate,
  usePreviewCompensationDateCorrection,
  type CompensationCorrectionContext,
} from "./queries";

export function CompensationDateCorrectionPanel({ employeeId, packageId, currentPackage, onSaved }: {
  employeeId: number; packageId: number;
  currentPackage: CompensationCorrectionContext["package"];
  onSaved: () => void;
}) {
  const { showToast } = useToast();
  const preview = usePreviewCompensationDateCorrection();
  const correct = useCorrectCompensationDate();
  const [proposedDate, setProposedDate] = useState(currentPackage.effectiveFrom);
  const [reason, setReason] = useState("");
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);

  const check = async () => {
    setError(null);
    if (!proposedDate || !reason.trim() || reason.trim().length > 500) {
      setError("Yeni yürürlük tarihi ve en fazla 500 karakterlik düzeltme gerekçesi zorunludur.");
      return;
    }
    try {
      await preview.mutateAsync({ employeeId, packageId, proposedEffectiveFrom: proposedDate });
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Tarih etkisi önizlenemedi.");
    }
  };

  const save = async () => {
    const checked = preview.data;
    if (!checked?.canApply || checked.proposedEffectiveFrom !== proposedDate) return;
    setError(null);
    try {
      await correct.mutateAsync({ employeeId, packageId, proposedEffectiveFrom: proposedDate,
        expectedPackageVersion: checked.package.version,
        expectedPreviousPackageId: checked.previousPackage?.id ?? null,
        expectedPreviousPackageVersion: checked.previousPackage?.version ?? null,
        reason: reason.trim(), ...(guidanceVersion != null ? { guidanceContentRevisionId: guidanceVersion } : {}) });
      showToast("Yürürlük tarihi düzeltildi; eski tarih ve gerekçe geçmişte korundu.", "success");
      onSaved();
    } catch (cause) {
      preview.reset();
      setError(cause instanceof ApiError ? cause.message : "Tarih düzeltilemedi; önizlemeyi yenileyin.");
    }
  };

  return <section className="ez-compensation-review ez-workbench" aria-label="Yürürlük tarihi düzeltme">
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}><GuidanceOperationSource process="personnel-entry" step="tab-mali" label="Tarih düzeltmede kullandığım rehber sürümü"
      value={guidanceVersion} onChange={id => { setGuidanceVersion(id); preview.reset(); }} disabled={correct.isPending} /></div>
          <div className="ez-workbench-main">

    <h4>Yürürlük tarihini ayrı işlemle düzelt</h4>
    <p>Yalnız açık çalışma döneminin son paketi düzeltilebilir. Paket veya değişecek tarih aralığı bordro ya da ayrılışta kullanılmışsa kayıt korunur ve işlem kilitlenir.</p>

    <div className="form-grid-2">
      <div className="input-group">
        <label className="input-label" htmlFor="pm-date-correction-date">Önerilen yeni yürürlük tarihi</label>
        <input id="pm-date-correction-date" type="date" className="input-control" value={proposedDate}
          onChange={(event) => { setProposedDate(event.target.value); preview.reset(); }} />
      </div>
      <div className="input-group form-span-2">
        <label className="input-label" htmlFor="pm-date-correction-reason">Tarih düzeltme gerekçesi</label>
        <textarea id="pm-date-correction-reason" className="input-control" value={reason}
          onChange={(event) => { setReason(event.target.value); preview.reset(); }} />
      </div>
    </div>
    <button type="button" className="btn btn-secondary" onClick={check} disabled={preview.isPending || correct.isPending}>
      Tarih etkisini önizle
    </button>
    {preview.data && <section aria-label="Tarih düzeltme önizlemesi" className="ez-compensation-review">
      <p><strong>Son paket:</strong> {preview.data.package.effectiveFrom} → {preview.data.proposedEffectiveFrom}</p>
      {preview.data.previousPackage && <p><strong>Önceki paketin bitişi:</strong> {preview.data.previousPackage.effectiveTo ?? "açık"} → {preview.data.proposedPreviousEffectiveTo}</p>}
      {preview.data.canApply
        ? <><p>Sunucu kontrolü geçildi. Kaydetme anında paketler ve tarihsel etkiler yeniden kontrol edilir.</p>
          <p><strong>Gerekçe:</strong> {reason.trim()}</p>
          <button type="button" className="btn btn-primary" onClick={save} disabled={correct.isPending}>Tarih düzeltmesini onayla ve kaydet</button></>
        : <div role="alert"><strong>Doğrudan tarih düzeltmesi kilitli.</strong><ul>
          {preview.data.blockingReasons.map((item) => <li key={item}>{item}</li>)}
        </ul></div>}
      <details><summary>Tarih düzeltme geçmişi ({preview.data.history.length})</summary>
        {preview.data.history.length === 0 && <p>Bu pakette önceki tarih düzeltmesi yok.</p>}
        {preview.data.history.map((item) => <p key={item.toVersion}>
          {item.oldEffectiveFrom} → {item.newEffectiveFrom} · {item.reason} · {item.actorUserId}
        </p>)}
      </details>
    </section>}
    {error && <p className="form-error" role="alert">{error}</p>}
  </div></div></section>;
}
