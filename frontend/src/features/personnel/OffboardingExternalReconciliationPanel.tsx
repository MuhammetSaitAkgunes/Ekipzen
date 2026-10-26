import { useState } from "react";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import {
  useOffboardingExternalReconciliation,
  useRecordOffboardingExternalAction,
  type OffboardingExternalAction,
  type OffboardingExternalReconciliation,
} from "./queries";

const money = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });
type Args = { employeeId: number; packageId: number; remediationCaseId: number;
  offboardingCaseId: number; revisionId: number };

export function OffboardingExternalReconciliationPanel(args: Args) {
  const query = useOffboardingExternalReconciliation(args);
  return <section className="ez-compensation-review" aria-label="Dış ödeme ve bildirim uzlaştırması">
    <h4>Dış ödeme ve bildirim kanıtı</h4>
    <p>Bu kayıtlar uygulama dışında yapılan işlemleri belgelendirir. Referans girilmesi banka ödemesi veya resmî bildirimi uygulamanın yaptığı anlamına gelmez.</p>
    {query.isPending && <p>Dış işlem durumu yükleniyor…</p>}
    {query.isError && <p className="form-error" role="alert">Dış işlem durumu yüklenemedi.</p>}
    {query.data && <ReconciliationContent args={args} context={query.data} />}
  </section>;
}

function ReconciliationContent({ args, context }: {
  args: Args; context: OffboardingExternalReconciliation;
}) {
  return <>
    <p>İlk tamamlanmış hesap <strong>{money.format(context.originalTotal)}</strong>. Önceki geçerli parasal hesap <strong>{money.format(context.previousEffectiveTotal)}</strong> → bu sürüm <strong>{money.format(context.revisedTotal)}</strong>. Bu sürümün hesap farkı <strong>{money.format(context.calculationDifference)}</strong>.</p>
    <p>Bu fark ödenecek net tutar değildir; kesinti, önceki gerçek ödeme ve bildirim yükümlülüğü ayrı incelenmelidir.</p>
    {!context.isCurrent && <p role="status">Bu parasal sürüm geçmişte kaldı. Kanıtları görüntüleyebilirsiniz; yeni kayıt yalnız geçerli sürüme eklenir.</p>}
    <ExternalActionForm key={`payment-${context.payment?.id ?? 0}`} args={args}
      kind="payment" title="Dış ödeme" current={context.payment} canRecord={context.isCurrent} />
    <ExternalActionForm key={`notification-${context.notification?.id ?? 0}`} args={args}
      kind="notification" title="Dış bildirim" current={context.notification} canRecord={context.isCurrent} />
    <details><summary>Kanıt geçmişi ({context.history.length})</summary>
      {context.history.length === 0 && <p>Henüz dış işlem kanıtı kaydedilmedi.</p>}
      <ul>{context.history.map((item) => <li key={item.id}>
        {item.kind === "payment" ? "Ödeme" : "Bildirim"} ·
        {item.outcome === "completed" ? " tamamlandı" : item.outcome === "pending" ? " bekliyor" : " işlem gerekmiyor"} ·
        {item.evidenceReference || "kanıt bekleniyor"} · {item.responsibleName} ·
        {item.actionDate ?? "işlem tarihi yok"} · kaydeden {item.recordedByUserId}
        {item.previousRecordId !== null && ` · önceki kayıt #${item.previousRecordId}`}
      </li>)}</ul>
    </details>
  </>;
}

export type ExternalActionInput = { kind: "payment" | "notification"; outcome: "pending" | "completed" | "not-required";
  responsibleName: string; evidenceReference: string; actionDate: string | null; recordedAmount: number | null;
  notes: string; expectedLatestRecordId: number | null; guidanceContentRevisionId?: number };

export function ExternalActionForm({ args, kind, title, current, canRecord, onRecord, pending = false }: {
  args?: Args; kind: "payment" | "notification"; title: string;
  current: OffboardingExternalAction | null; canRecord: boolean;
  onRecord?: (input: ExternalActionInput) => Promise<unknown>; pending?: boolean;
}) {
  const { showToast } = useToast();
  const record = useRecordOffboardingExternalAction();
  const [outcome, setOutcome] = useState<"pending" | "completed" | "not-required">(current?.outcome ?? "pending");
  const [responsibleName, setResponsibleName] = useState(current?.responsibleName ?? "");
  const [evidenceReference, setEvidenceReference] = useState(current?.evidenceReference ?? "");
  const [actionDate, setActionDate] = useState(current?.actionDate ?? "");
  const [recordedAmount, setRecordedAmount] = useState(current?.recordedAmount?.toString() ?? "");
  const [notes, setNotes] = useState(current?.notes ?? "");
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [guidanceVersion, setGuidanceVersion] = useState<number | null>(null);
  const valid = responsibleName.trim().length > 0 && responsibleName.trim().length <= 256
    && evidenceReference.trim().length <= 256
    && notes.trim().length <= 2000
    && (outcome === "pending" ? evidenceReference.trim().length === 0
      : outcome === "not-required" ? notes.trim().length > 0 && evidenceReference.trim().length > 0
      : evidenceReference.trim().length > 0
        && actionDate.length > 0 && (kind !== "payment" ||
          (recordedAmount.trim().length > 0 && Number.isFinite(Number(recordedAmount))
            && Number(recordedAmount) >= 0)));

  const save = async () => {
    setError(null);
    if (!valid || !confirmed) {
      setError("Sorumluyu, kanıt referansını, gerekli işlem bilgilerini ve teyidi tamamlayın.");
      return;
    }
    try {
      const input: ExternalActionInput = { kind, outcome, guidanceContentRevisionId: guidanceVersion ?? undefined,
        responsibleName: responsibleName.trim(), evidenceReference: evidenceReference.trim(),
        actionDate: outcome === "completed" ? actionDate : null,
        recordedAmount: outcome === "completed" && kind === "payment"
          ? Number(recordedAmount) : null,
        notes: notes.trim(), expectedLatestRecordId: current?.id ?? null };
      if (onRecord) await onRecord(input);
      else if (args) await record.mutateAsync({ ...args, ...input });
      else throw new Error("Dış işlem kaynağı bulunamadı.");
      showToast(`${title} durumu kaydedildi; önceki kayıt geçmişte korundu.`, "success");
    } catch (cause) {
      setConfirmed(false);
      setError(cause instanceof ApiError ? cause.message : "Kanıt kaydedilemedi; güncel durumu yeniden açın.");
    }
  };

  return <section className="ez-compensation-review ez-workbench" aria-label={`${title} kanıtı`}>
          <div className="ez-workbench-layout">
<div className="ez-workbench-guide" role="group" aria-label="İşlem açıklamaları" tabIndex={0}>{canRecord && <GuidanceOperationSource process="daily" step="external-followup" value={guidanceVersion}
      onChange={(id) => { setGuidanceVersion(id); setConfirmed(false); }} disabled={pending || record.isPending} />}</div>
          <div className="ez-workbench-main">


    <h5>{title}: {current ? current.outcome === "completed" ? "kanıtlı tamamlandı" : current.outcome === "pending" ? "sorumlu atandı, işlem bekliyor" : "işlem gerekmiyor kaydı" : "sorumlu ve kanıt bekliyor"}</h5>
    {current && <p>Son kayıt: {current.evidenceReference || "Kanıt henüz yok"} · sorumlu {current.responsibleName}. Hata varsa yeni kayıt ekleyin; eski kayıt korunur.</p>}
    {canRecord && <>
      <div className="form-grid-2">
        <div className="input-group"><label htmlFor={`external-${kind}-outcome`}>Sonuç</label>
          <select id={`external-${kind}-outcome`} className="input-control" value={outcome}
            onChange={(event) => {
              const next = event.target.value as typeof outcome;
              setOutcome(next);
              setConfirmed(false);
              if (next === "pending") {
                setEvidenceReference("");
                setActionDate("");
                setRecordedAmount("");
              }
            }}>
            <option value="pending">Sorumlu atandı, işlem bekliyor</option>
            <option value="completed">Uygulama dışında tamamlandı</option>
            <option value="not-required">İşlem gerekmiyor</option>
          </select></div>
        <div className="input-group"><label htmlFor={`external-${kind}-owner`}>Sorumlu kişi</label>
          <input id={`external-${kind}-owner`} className="input-control" value={responsibleName}
            onChange={(event) => setResponsibleName(event.target.value)} /></div>
        {outcome !== "pending" && <div className="input-group"><label htmlFor={`external-${kind}-reference`}>Belge / işlem referansı</label>
          <input id={`external-${kind}-reference`} className="input-control" value={evidenceReference}
            onChange={(event) => setEvidenceReference(event.target.value)} /></div>}
        {outcome === "completed" && <div className="input-group"><label htmlFor={`external-${kind}-date`}>Dış işlem tarihi</label>
          <input id={`external-${kind}-date`} type="date" className="input-control" value={actionDate}
            onChange={(event) => setActionDate(event.target.value)} /></div>}
        {outcome === "completed" && kind === "payment" && <div className="input-group">
          <label htmlFor="external-payment-amount">Belgelenen gerçek ödeme tutarı</label>
          <input id="external-payment-amount" type="number" min="0" step="0.01"
            className="input-control" value={recordedAmount}
            onChange={(event) => setRecordedAmount(event.target.value)} />
        </div>}
        <div className="input-group form-span-2"><label htmlFor={`external-${kind}-notes`}>İnceleme / gerekçe notu</label>
          <textarea id={`external-${kind}-notes`} className="input-control" value={notes}
            onChange={(event) => setNotes(event.target.value)} /></div>
      </div>
      {outcome === "not-required" && <p>İşlem gerekmiyor kararının dayanağını notta ve referansta açıkça belirtin.</p>}
      <label><input type="checkbox" checked={confirmed}
        onChange={(event) => setConfirmed(event.target.checked)} /> {outcome === "pending" ? "Sorumluyu atadım; dış işlemin henüz tamamlanmadığını anlıyorum." : "Gerçek dış işlem sonucunu ve dayanak belgeyi doğruladım."}</label>
      <button type="button" className="btn btn-secondary" disabled={!valid || !confirmed || pending || record.isPending}
        onClick={save}>{outcome === "pending" ? "Sorumluyu kaydet" : current ? "Yeni kanıt kaydı ekle" : "Kanıtı kaydet"}</button>
      {error && <p className="form-error" role="alert">{error}</p>}
    </>}
  </div></div></section>;
}
