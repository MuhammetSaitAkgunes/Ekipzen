import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { OffboardingExternalReconciliationPanel } from "./OffboardingExternalReconciliationPanel";
import {
  useApproveOffboardingSettlementRevision,
  useOffboardingSettlementRevisions,
  useOpenOffboardingSettlementRevision,
  useSaveOffboardingSettlementRevision,
  type OffboardingSettlementRevisionContext,
} from "./queries";

const money = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });
type Args = { employeeId: number; packageId: number; remediationCaseId: number;
  offboardingCaseId: number };

export function OffboardingSettlementRevisionPanel(args: Args) {
  const contextQ = useOffboardingSettlementRevisions(args);
  return <section className="ez-compensation-review" aria-label={`Ayrılış hesabı ${args.offboardingCaseId} parasal düzeltmesi`}>
    <h4>Ayrılışın parasal hesabını düzelt</h4>
    <p>İlk tamamlanmış ayrılış kaydı korunur. Onaylanan düzeltme yeni geçerli parasal sürüm olur; personel pasif, ayrılış tarihi ve çıkış bilgileri sabit kalır.</p>
    {contextQ.isPending && <p>Parasal hesap sürümleri yükleniyor…</p>}
    {contextQ.isError && <p className="form-error" role="alert">Parasal hesap sürümleri yüklenemedi.</p>}
    {contextQ.data && <SettlementRevisionContent
      key={`${contextQ.data.draft?.id ?? "none"}-${contextQ.data.draft?.version ?? 0}`}
      args={args} context={contextQ.data} />}
  </section>;
}

function SettlementRevisionContent({ args, context }: {
  args: Args; context: OffboardingSettlementRevisionContext;
}) {
  const { showToast } = useToast();
  const open = useOpenOffboardingSettlementRevision();
  const save = useSaveOffboardingSettlementRevision();
  const approve = useApproveOffboardingSettlementRevision();
  const draft = context.draft;
  const [unusedLeaveDays, setUnusedLeaveDays] = useState(String(draft?.values.unusedLeaveDays ?? 0));
  const [unusedLeavePayment, setUnusedLeavePayment] = useState(String(draft?.values.unusedLeavePayment ?? 0));
  const [severancePayment, setSeverancePayment] = useState(String(draft?.values.severancePayment ?? 0));
  const [noticePayment, setNoticePayment] = useState(String(draft?.values.noticePayment ?? 0));
  const [reason, setReason] = useState(draft?.reason ?? "");
  const [reference, setReference] = useState(draft?.values.settlementReference ?? "");
  const [notes, setNotes] = useState(draft?.values.settlementNotes ?? "");
  const [expertReference, setExpertReference] = useState("");
  const [reviewNotes, setReviewNotes] = useState("");
  const [financialConfirmed, setFinancialConfirmed] = useState(false);
  const [externalConfirmed, setExternalConfirmed] = useState(false);
  const [selectedRevisionId, setSelectedRevisionId] = useState<number | null>(
    context.history[0]?.id ?? null);
  const [error, setError] = useState<string | null>(null);
  const busy = open.isPending || save.isPending || approve.isPending;
  const amounts = [unusedLeaveDays, unusedLeavePayment, severancePayment, noticePayment]
    .map(Number);
  const dirty = draft !== null && (amounts[0] !== draft.values.unusedLeaveDays
    || amounts[1] !== draft.values.unusedLeavePayment
    || amounts[2] !== draft.values.severancePayment
    || amounts[3] !== draft.values.noticePayment || reason !== draft.reason
    || reference !== draft.values.settlementReference
    || notes !== (draft.values.settlementNotes ?? ""));
  const valid = [unusedLeaveDays, unusedLeavePayment, severancePayment, noticePayment]
    .every((value) => value.trim().length > 0)
    && amounts.every((value) => Number.isFinite(value) && value >= 0)
    && reason.trim().length > 0 && reason.trim().length <= 1000
    && reference.trim().length > 0 && reference.trim().length <= 256
    && notes.trim().length <= 2000;

  const openDraft = async () => {
    setError(null);
    try {
      await open.mutateAsync({ ...args,
        expectedOffboardingVersion: context.offboardingCaseVersion });
      showToast("Yalnız parasal hesap taslağı açıldı; tamamlanmış ayrılış değişmedi.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Parasal hesap taslağı açılamadı.");
    }
  };
  const saveDraft = async () => {
    if (!draft) return;
    setError(null);
    if (!valid) {
      setError("Tutarları, düzeltme gerekçesini ve uzman hesap referansını doğrulayın.");
      return;
    }
    try {
      await save.mutateAsync({ ...args, revisionId: draft.id,
        expectedRevisionVersion: draft.version, unusedLeaveDays: amounts[0],
        unusedLeavePayment: amounts[1], severancePayment: amounts[2],
        noticePayment: amounts[3], reason: reason.trim(),
        settlementReference: reference.trim(), settlementNotes: notes.trim() || null });
      showToast("Parasal hesap taslağı kaydedildi; geçerli hesap henüz değişmedi.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Parasal hesap taslağı kaydedilemedi.");
    }
  };
  const approveDraft = async () => {
    if (!draft) return;
    setError(null);
    if (dirty) {
      setError("Değişen tutarları önce taslağa kaydedin.");
      return;
    }
    if (!expertReference.trim() || expertReference.trim().length > 256
      || !reviewNotes.trim() || reviewNotes.trim().length > 2000
      || !financialConfirmed || !externalConfirmed) {
      setError("Uzman inceleme referansı, notu ve iki açık teyit gereklidir.");
      return;
    }
    try {
      await approve.mutateAsync({ ...args, revisionId: draft.id,
        expectedRevisionVersion: draft.version,
        expertReviewReference: expertReference.trim(), reviewNotes: reviewNotes.trim() });
      showToast("Parasal düzeltme geçerli hesap sürümü oldu; dış ödeme ve bildirimleri ayrıca uzlaştırın.", "success");
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "Parasal hesap onaylanamadı.");
    }
  };

  return <>
    <p><strong>İlk hesap:</strong> brüt {money.format(context.original.monthlyGrossSalarySnapshot)} · toplam {money.format(context.original.totalPayment)}</p>
    <p><strong>Geçerli hesap:</strong> brüt {money.format(context.effective.monthlyGrossSalarySnapshot)} · toplam {money.format(context.effective.totalPayment)}</p>
    {context.history.length > 0 && <ul>{context.history.map((item) =>
      <li key={item.id}>Parasal sürüm #{item.revisionNumber} · {money.format(item.values.totalPayment)} · {item.values.settlementReference} · onay {item.approvedAtUtc}</li>)}</ul>}
    {context.history.length > 0 && <>
      <div className="input-group"><label htmlFor={`settlement-${args.offboardingCaseId}-external-version`}>Dış işlem kanıtı için parasal sürüm</label>
        <select id={`settlement-${args.offboardingCaseId}-external-version`}
          className="input-control" value={selectedRevisionId ?? context.history[0].id}
          onChange={(event) => setSelectedRevisionId(Number(event.target.value))}>
          {context.history.map((item) => <option key={item.id} value={item.id}>
            Sürüm #{item.revisionNumber}{item.id === context.history[0].id ? " · geçerli" : " · geçmiş"}
          </option>)}
        </select>
      </div>
      <OffboardingExternalReconciliationPanel key={selectedRevisionId ?? context.history[0].id}
        {...args} revisionId={selectedRevisionId ?? context.history[0].id} />
    </>}
    {context.blockingReasons.length > 0 && !draft && <ul>{context.blockingReasons.map((item) =>
      <li key={item}>{item}</li>)}</ul>}
    {context.canOpen && <button type="button" className="btn btn-secondary" onClick={openDraft} disabled={busy}>
      Parasal düzeltme taslağını aç
    </button>}
    {draft && <section aria-label="Parasal düzeltme taslağı">
      <p>Önerilen ücret görüntüsü: <strong>{money.format(draft.values.monthlyGrossSalarySnapshot)}</strong> brüt. Ücret alanları onaylı inceleme dosyasından gelir; burada yalnız izin günü ve ödeme tutarları uzman hesabıyla düzeltilir.</p>
      <div className="form-grid-2">
        {([
          ["Kullanılmamış izin günü", "unusedLeaveDays", unusedLeaveDays, setUnusedLeaveDays],
          ["İzin ödemesi (₺)", "unusedLeavePayment", unusedLeavePayment, setUnusedLeavePayment],
          ["Kıdem ödemesi (₺)", "severancePayment", severancePayment, setSeverancePayment],
          ["İhbar ödemesi (₺)", "noticePayment", noticePayment, setNoticePayment],
        ] as const).map(([label, key, value, setter]) => <div className="input-group" key={key}>
          <label htmlFor={`settlement-${draft.id}-${key}`}>{label}</label>
          <input id={`settlement-${draft.id}-${key}`} className="input-control" type="number"
            min="0" step={key === "unusedLeaveDays" ? "0.5" : "0.01"}
            value={value} onChange={(event) => setter(event.target.value)} />
        </div>)}
        <div className="input-group form-span-2"><label htmlFor={`settlement-${draft.id}-reason`}>Düzeltme gerekçesi</label>
          <textarea id={`settlement-${draft.id}-reason`} className="input-control" value={reason}
            onChange={(event) => setReason(event.target.value)} /></div>
        <div className="input-group form-span-2"><label htmlFor={`settlement-${draft.id}-reference`}>Uzman hesap belgesi / referansı</label>
          <input id={`settlement-${draft.id}-reference`} className="input-control" value={reference}
            onChange={(event) => setReference(event.target.value)} /></div>
        <div className="input-group form-span-2"><label htmlFor={`settlement-${draft.id}-notes`}>Hesap ve dış ödeme notları</label>
          <textarea id={`settlement-${draft.id}-notes`} className="input-control" value={notes}
            onChange={(event) => setNotes(event.target.value)} /></div>
      </div>
      <p>Önerilen yeni toplam: <strong>{money.format(amounts.slice(1).reduce((sum, value) => sum + (Number.isFinite(value) ? value : 0), 0))}</strong>. Bu rakam ödeme yapıldığını veya bildirim düzeltildiğini göstermez.</p>
      <button type="button" className="btn btn-secondary" onClick={saveDraft} disabled={busy || !dirty}>
        Parasal taslağı kaydet
      </button>
      <div className="ez-compensation-review" aria-label="Parasal düzeltmeyi onayla">
        <h4>Uzman incelemesi ve yeni geçerli sürüm</h4>
        <p>Kıdem, ihbar ve kullanılmamış izin değerlendirmesinde <a href="https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf" target="_blank" rel="noopener noreferrer">4857 sayılı İş Kanunu md. 17 ve 59</a> ile <a href="https://www.csgb.gov.tr/Media/fqed1ewm/1475-say%C4%B1l%C4%B1-i%C5%9F-kanunu-k%C4%B1dem-tazminat%C4%B1na-ili%C5%9Fkin-yasal-d%C3%BCzenleme.pdf" target="_blank" rel="noopener noreferrer">1475 sayılı Kanun md. 14</a> kaynak alınır. Hak ediş ve ödeme farkı bu vaka için uzman tarafından doğrulanmalıdır.</p>
        <div className="input-group"><label htmlFor={`settlement-${draft.id}-expert`}>Uzman son inceleme referansı</label>
          <input id={`settlement-${draft.id}-expert`} className="input-control" value={expertReference}
            onChange={(event) => setExpertReference(event.target.value)} /></div>
        <div className="input-group"><label htmlFor={`settlement-${draft.id}-review-notes`}>Uzman inceleme notu</label>
          <textarea id={`settlement-${draft.id}-review-notes`} className="input-control" value={reviewNotes}
            onChange={(event) => setReviewNotes(event.target.value)} /></div>
        <label><input type="checkbox" checked={financialConfirmed}
          onChange={(event) => setFinancialConfirmed(event.target.checked)} /> Tutarları, hak edişi ve uzman hesap belgesini karşılaştırdım.</label>
        <label><input type="checkbox" checked={externalConfirmed}
          onChange={(event) => setExternalConfirmed(event.target.checked)} /> Dış ödeme ve bildirim farkını ayrıca uzlaştırmam gerektiğini anlıyorum.</label>
        {dirty && <p>Onaydan önce değişen taslağı kaydedin.</p>}
        <button type="button" className="btn btn-primary" onClick={approveDraft}
          disabled={busy || dirty || !valid || !financialConfirmed || !externalConfirmed}>
          Yeni geçerli parasal sürümü onayla
        </button>
      </div>
    </section>}
    {error && <p className="form-error" role="alert">{error}</p>}
  </>;
}
