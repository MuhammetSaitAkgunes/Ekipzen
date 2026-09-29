import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import {
  usePayrollRevisionExternalReconciliation,
  useRecordPayrollRevisionExternalAction,
  type PayrollRevisionExternalAction,
  type PayrollRevisionExternalArgs,
} from "./queries";

const money = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });

export function PayrollRevisionExternalReconciliationPanel(args: PayrollRevisionExternalArgs) {
  const query = usePayrollRevisionExternalReconciliation(args);
  return <section className="ez-compensation-review" aria-label="Bordro dış ödeme ve bildirim uzlaştırması">
    <h5>Dış ödeme ve bildirim kanıtları</h5>
    <p>Bordrodaki parasal sürüm ödeme veya resmî bildirim yapmaz. Her iki işlemi ayrı sorumlu ve belgeyle izleyin.</p>
    {query.isPending && <p>Dış işlem kanıtları yükleniyor…</p>}
    {query.isError && <p className="form-error" role="alert">Dış işlem kanıtları yüklenemedi.</p>}
    {query.data && <>
      <p>Önceki net hesap <strong>{money.format(query.data.originalNetPay)}</strong> → geçerli net hesap <strong>{money.format(query.data.revisedNetPay)}</strong>. Hesaplanan net fark <strong>{money.format(query.data.calculatedNetDifference)}</strong>.</p>
      <p>Net fark gerçek ödenen tutar veya bildirim yükümlülüğü değildir. Önceki ödeme ve dış kayıtlar ayrıca incelenmelidir.</p>
      {!query.data.isCurrent && <p role="status">Bu sürüm geçmişte kaldı; yeni kanıt yalnız geçerli sürüme eklenir.</p>}
      <ActionForm key={`payment-${query.data.payment?.id ?? 0}`} args={args} kind="payment"
        current={query.data.payment} canRecord={query.data.isCurrent} />
      <ActionForm key={`notification-${query.data.notification?.id ?? 0}`} args={args}
        kind="notification" current={query.data.notification} canRecord={query.data.isCurrent} />
      <details><summary>Kanıt geçmişi ({query.data.history.length})</summary>
        {query.data.history.length === 0 && <p>Henüz kanıt kaydedilmedi.</p>}
        <ul>{query.data.history.map((item) => <li key={item.id}>
          {item.kind === "payment" ? "Ödeme" : "Bildirim"} · {item.outcome === "completed"
            ? "tamamlandı" : item.outcome === "pending" ? "bekliyor" : "gerekmiyor"} ·
          {item.evidenceReference || "kanıt bekleniyor"} · {item.responsibleName} ·
          {item.actionDate ?? "işlem tarihi yok"} · kaydeden {item.recordedByUserId}
          {item.previousRecordId !== null && ` · önceki kayıt #${item.previousRecordId}`}
        </li>)}</ul>
      </details>
    </>}
  </section>;
}

function ActionForm({ args, kind, current, canRecord }: {
  args: PayrollRevisionExternalArgs; kind: "payment" | "notification";
  current: PayrollRevisionExternalAction | null; canRecord: boolean;
}) {
  const title = kind === "payment" ? "Dış ödeme" : "Dış bildirim";
  const { showToast } = useToast();
  const mutation = useRecordPayrollRevisionExternalAction();
  const [outcome, setOutcome] = useState<"pending" | "completed" | "not-required">(
    current?.outcome ?? "pending");
  const [responsibleName, setResponsibleName] = useState(current?.responsibleName ?? "");
  const [evidenceReference, setEvidenceReference] = useState(current?.evidenceReference ?? "");
  const [actionDate, setActionDate] = useState(current?.actionDate ?? "");
  const [recordedAmount, setRecordedAmount] = useState(current?.recordedAmount?.toString() ?? "");
  const [notes, setNotes] = useState(current?.notes ?? "");
  const [confirmed, setConfirmed] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const valid = responsibleName.trim().length > 0 && responsibleName.trim().length <= 256
    && evidenceReference.trim().length <= 256 && notes.trim().length <= 2000
    && (outcome === "pending" ? evidenceReference.trim().length === 0
      : outcome === "not-required" ? evidenceReference.trim().length > 0 && notes.trim().length > 0
      : evidenceReference.trim().length > 0 && actionDate.length > 0
        && (kind !== "payment" || recordedAmount.trim().length > 0
          && Number.isFinite(Number(recordedAmount)) && Number(recordedAmount) >= 0));

  const save = async () => {
    if (!valid || !confirmed) return;
    setError(null);
    try {
      await mutation.mutateAsync({ ...args, kind, outcome,
        responsibleName: responsibleName.trim(), evidenceReference: evidenceReference.trim(),
        actionDate: outcome === "completed" ? actionDate : null,
        recordedAmount: outcome === "completed" && kind === "payment"
          ? Number(recordedAmount) : null,
        notes: notes.trim(), expectedLatestRecordId: current?.id ?? null });
      showToast(`${title} durumu kaydedildi; önceki kanıt geçmişte korundu.`, "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Kanıt kaydedilemedi; durumu yeniden açın.");
    }
  };

  return <section className="ez-compensation-review" aria-label={`${title} kanıtı`}>
    <h5>{title}: {current ? current.outcome === "completed" ? "kanıtlı tamamlandı"
      : current.outcome === "pending" ? "bekliyor" : "işlem gerekmiyor" : "kanıt bekliyor"}</h5>
    {current && <p>Son kayıt: {current.evidenceReference || "kanıt yok"} · sorumlu {current.responsibleName}. Hata varsa yeni kayıt ekleyin.</p>}
    {canRecord && <>
      <div className="form-grid-2">
        <div className="input-group"><label htmlFor={`payroll-external-${kind}-outcome`}>Sonuç</label>
          <select id={`payroll-external-${kind}-outcome`} className="input-control" value={outcome}
            onChange={(event) => {
              const next = event.target.value as typeof outcome;
              setOutcome(next); setConfirmed(false);
              if (next !== "completed") { setActionDate(""); setRecordedAmount(""); }
              if (next === "pending") setEvidenceReference("");
            }}>
            <option value="pending">Sorumlu atandı, işlem bekliyor</option>
            <option value="completed">Uygulama dışında tamamlandı</option>
            <option value="not-required">İşlem gerekmiyor</option>
          </select></div>
        <div className="input-group"><label htmlFor={`payroll-external-${kind}-owner`}>Sorumlu kişi</label>
          <input id={`payroll-external-${kind}-owner`} className="input-control"
            value={responsibleName} onChange={(event) => setResponsibleName(event.target.value)} /></div>
        {outcome !== "pending" && <div className="input-group">
          <label htmlFor={`payroll-external-${kind}-reference`}>Belge / işlem referansı</label>
          <input id={`payroll-external-${kind}-reference`} className="input-control"
            value={evidenceReference} onChange={(event) => setEvidenceReference(event.target.value)} /></div>}
        {outcome === "completed" && <div className="input-group">
          <label htmlFor={`payroll-external-${kind}-date`}>Dış işlem tarihi</label>
          <input id={`payroll-external-${kind}-date`} type="date" className="input-control"
            value={actionDate} onChange={(event) => setActionDate(event.target.value)} /></div>}
        {outcome === "completed" && kind === "payment" && <div className="input-group">
          <label htmlFor={`payroll-external-${kind}-amount`}>Belgelenen gerçek ödeme tutarı</label>
          <input id={`payroll-external-${kind}-amount`} type="number" min="0" step="0.01"
            className="input-control" value={recordedAmount}
            onChange={(event) => setRecordedAmount(event.target.value)} /></div>}
        <div className="input-group form-span-2"><label htmlFor={`payroll-external-${kind}-notes`}>İnceleme / gerekçe notu</label>
          <textarea id={`payroll-external-${kind}-notes`} className="input-control"
            value={notes} onChange={(event) => setNotes(event.target.value)} /></div>
      </div>
      {outcome === "not-required" && <p>İşlem gerekmiyor kararının dayanağını referans ve notla belirtin.</p>}
      <label><input type="checkbox" checked={confirmed}
        onChange={(event) => setConfirmed(event.target.checked)} /> {outcome === "pending"
          ? "Sorumluyu atadım; işlemin henüz tamamlanmadığını anlıyorum."
          : "Gerçek dış işlem sonucunu ve dayanak belgeyi doğruladım."}</label>
      <button type="button" className="btn btn-secondary" disabled={!valid || !confirmed || mutation.isPending}
        onClick={save}>{outcome === "pending" ? "Sorumluyu kaydet" : "Yeni kanıt kaydı ekle"}</button>
      {error && <p className="form-error" role="alert">{error}</p>}
    </>}
  </section>;
}
