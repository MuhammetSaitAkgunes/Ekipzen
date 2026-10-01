import { useEffect, useRef, useState } from "react";
import { GuidanceSourceLink } from "../guidance/GuidanceSourceLink";
import { GuidanceStepLessons } from "../guidance/GuidanceStepLessons";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import {
  useCompleteOffboarding,
  useEmployeeOffboardingContext,
  useGenerateOffboardingCalculationDraft,
  useReverseOffboarding,
  useSaveOffboardingDraft,
  useTerminationCalculationRules,
  type EmployeeOffboardingCaseDto,
  type SaveOffboardingDraftBody,
} from "./queries";

const todayInputValue = (): string => {
  const today = new Date();
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
};

const money = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  minimumFractionDigits: 2,
});


const terminationReasonLabel: Record<string, string> = {
  "employee-resignation": "Çalışan istifası",
  "employer-termination": "İşveren feshi",
  "employer-termination-for-cause": "İşveren haklı feshi",
  "mutual-agreement": "İkale",
  "fixed-term-expiry": "Belirli süre bitimi",
  retirement: "Emeklilik",
  "military-service": "Askerlik",
  marriage: "Evlilik",
  death: "Vefat",
  other: "Diğer",
};
const noticeHandlingLabel: Record<string, string> = {
  "not-applicable": "Uygulanmaz",
  worked: "Süre çalışıldı",
  "payable-to-employee": "Çalışana ödeme",
  "payable-to-employer": "İşverene ödeme",
  waived: "Feragat / mahsup",
};

export function OffboardingModal({ employeeId, onClose }: {
  employeeId: number;
  onClose: () => void;
}) {
  const { showToast } = useToast();
  const actionInFlight = useRef(false);
  const contextQ = useEmployeeOffboardingContext(employeeId);
  const rulesQ = useTerminationCalculationRules();
  const saveDraft = useSaveOffboardingDraft();
  const generateCalculation = useGenerateOffboardingCalculationDraft();
  const complete = useCompleteOffboarding();
  const reverse = useReverseOffboarding();

  const [terminationDate, setTerminationDate] = useState("");
  const [exitCode, setExitCode] = useState("");
  const [exitReason, setExitReason] = useState("");
  const [terminationReason, setTerminationReason] = useState("");
  const [noticeHandling, setNoticeHandling] = useState("");
  const [successorId, setSuccessorId] = useState("");
  const [unusedLeaveDays, setUnusedLeaveDays] = useState("0");
  const [unusedLeavePayment, setUnusedLeavePayment] = useState("0");
  const [severancePayment, setSeverancePayment] = useState("0");
  const [noticePayment, setNoticePayment] = useState("0");
  const [settlementReference, setSettlementReference] = useState("");
  const [settlementNotes, setSettlementNotes] = useState("");
  const [confirmSeveranceEligibility, setConfirmSeveranceEligibility] = useState(false);
  const [confirmNoticeEligibility, setConfirmNoticeEligibility] = useState(false);
  const [manualOverride, setManualOverride] = useState(false);
  const [reversalReason, setReversalReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [completionReview, setCompletionReview] = useState(false);
  const [completionAcknowledged, setCompletionAcknowledged] = useState(false);
  const [reverseReview, setReverseReview] = useState(false);

  const context = contextQ.data;
  const currentCase = context?.currentCase as EmployeeOffboardingCaseDto | null | undefined;
  const completedCase = currentCase?.status === "completed" ? currentCase : null;
  const effectiveSettlementRevision = context?.effectiveSettlementRevision;
  const directReportCount = context?.directReportCount ?? 0;
  const pendingLeaveCount = context?.pendingLeaveRequestCount ?? 0;
  const successors = context?.successors ?? [];
  const requiresSuccessor = directReportCount > 0;
  const hasEligibleSuccessor = !requiresSuccessor || successors.length > 0;
  const calculationLocked = currentCase?.status === "draft"
    && currentCase.settlementSource === "approved-rule-draft" && !manualOverride;
  const activeRule = rulesQ.data
    ?.filter((rule) => rule.status === "approved" && rule.effectiveFrom <= terminationDate)
    .sort((left, right) => right.effectiveFrom.localeCompare(left.effectiveFrom))[0];
  const isBusy = saveDraft.isPending || generateCalculation.isPending
    || complete.isPending || reverse.isPending;

  useEffect(() => {
    if (currentCase?.status !== "draft") return;
    setTerminationDate(currentCase.terminationDate ?? "");
    setExitCode(currentCase.exitCode ?? "");
    setExitReason(currentCase.exitReason ?? "");
    setTerminationReason(currentCase.terminationReason ?? "");
    setNoticeHandling(currentCase.noticeHandling ?? "");
    setSuccessorId(currentCase.successorManagerId ? String(currentCase.successorManagerId) : "");
    setUnusedLeaveDays(String(currentCase.unusedLeaveDays ?? 0));
    setUnusedLeavePayment(String(currentCase.unusedLeavePayment ?? 0));
    setSeverancePayment(String(currentCase.severancePayment ?? 0));
    setNoticePayment(String(currentCase.noticePayment ?? 0));
    setSettlementReference(currentCase.settlementReference ?? "");
    setSettlementNotes(currentCase.settlementNotes ?? "");
  }, [currentCase]);

  useEffect(() => {
    setCompletionReview(false);
    setCompletionAcknowledged(false);
    setReverseReview(false);
  }, [currentCase?.id, currentCase?.version, currentCase?.status]);

  const validateDraft = (): string | null => {
    if (!terminationDate || terminationDate > todayInputValue()) return "Gerçek işten ayrılma tarihini seçin; gelecek tarih kullanılamaz.";
    if (!exitCode.trim() || exitCode.trim().length > 64) return "Çıkış kodunu doğrulayın (en çok 64 karakter).";
    if (!exitReason.trim() || exitReason.trim().length > 1000) return "Çıkış gerekçesini doğrulayın (en çok 1000 karakter).";
    if (!terminationReason) return "Fesih nedenini belgedeki duruma göre açıkça seçin.";
    if (!noticeHandling) return "İhbar uygulamasını uzman değerlendirmesine göre açıkça seçin.";
    if (!settlementReference.trim() || settlementReference.trim().length > 256) return "Uzman/harici hesap referansını doğrulayın (en çok 256 karakter).";
    if (settlementNotes.trim().length > 2000) return "Kapanış notu 2000 karakteri geçemez.";
    if (requiresSuccessor && !successorId) return "Ekibi olan yönetici için halef seçilmelidir.";
    const amounts = [
      ["Ödenecek kullanılmamış izin günü", unusedLeaveDays],
      ["İzin ödemesi", unusedLeavePayment],
      ["Kıdem ödemesi", severancePayment],
      ["İhbar ödemesi", noticePayment],
    ] as const;
    for (const [label, value] of amounts) {
      if (!value.trim() || !Number.isFinite(Number(value)) || Number(value) < 0) {
        return label + " boş, geçersiz veya negatif olamaz; tutarı doğrulayın.";
      }
    }
    return null;
  };

  const draftBody = (): SaveOffboardingDraftBody => ({
    expectedVersion: currentCase?.status === "draft" ? currentCase.version : null,
    terminationDate,
    exitCode: exitCode.trim(),
    exitReason: exitReason.trim(),
    terminationReason,
    noticeHandling,
    successorManagerId: successorId ? Number(successorId) : null,
    unusedLeaveDays: Number(unusedLeaveDays),
    unusedLeavePayment: Number(unusedLeavePayment),
    severancePayment: Number(severancePayment),
    noticePayment: Number(noticePayment),
    settlementReference: settlementReference.trim(),
    settlementNotes: settlementNotes.trim() || null,
  });

  const prepareCompletion = () => {
    setError(null);
    const validationError = validateDraft();
    if (validationError) {
      setError(validationError);
      return;
    }
    setCompletionAcknowledged(false);
    setCompletionReview(true);
  };

  const save = async (finish: boolean) => {
    if (isBusy || actionInFlight.current || (finish && (!completionReview || !completionAcknowledged))) return;
    setError(null);
    const validationError = validateDraft();
    if (validationError) {
      setError(validationError);
      return;
    }

    actionInFlight.current = true;
    let draftPersisted = false;
    try {
      if (finish && calculationLocked && currentCase?.status === "draft") {
        if (currentCase.id == null || currentCase.version == null) {
          throw new Error("Hesap taslağı kimlik veya sürüm bilgisi döndürmedi.");
        }
        await complete.mutateAsync({
          employeeId,
          caseId: currentCase.id,
          expectedVersion: currentCase.version,
        });
        showToast("İşten çıkış dosyası tamamlandı; erişim ve rol işleri güvenli kuyruğa alındı.", "success");
        onClose();
        return;
      }

      const saved = await saveDraft.mutateAsync({ employeeId, body: draftBody() });
      draftPersisted = true;
      if (finish) {
        if (saved.id == null || saved.version == null) {
          throw new Error("İşten çıkış taslağı kimlik veya sürüm bilgisi döndürmedi.");
        }
        await complete.mutateAsync({
          employeeId,
          caseId: saved.id,
          expectedVersion: saved.version,
        });
        showToast("İşten çıkış dosyası tamamlandı; erişim ve rol işleri güvenli kuyruğa alındı.", "success");
      } else {
        showToast("İşten çıkış taslağı kaydedildi; personel durumu değişmedi.", "success");
      }
      onClose();
    } catch (cause) {
      const message = cause instanceof ApiError ? cause.message : "İşten çıkış işlemi tamamlanamadı.";
      setError(draftPersisted
        ? "Taslak kaydedildi, ancak sonraki adım tamamlanamadı: " + message + " Dosyanın güncel durumunu kontrol edin."
        : message);
    } finally {
      actionInFlight.current = false;
    }
  };

  const calculateDraft = async () => {
    if (isBusy || actionInFlight.current) return;
    setError(null);
    const validationError = validateDraft();
    if (validationError) {
      setError(validationError);
      return;
    }
    if (!activeRule) {
      setError("Ayrılış tarihinde yürürlükte olan onaylı kural yok. Bordro Ayarları'nda uzman onaylı sürüm oluşturun veya harici uzman tutarı kullanın.");
      return;
    }

    actionInFlight.current = true;
    let draftPersisted = false;
    try {
      const saved = await saveDraft.mutateAsync({ employeeId, body: draftBody() });
      draftPersisted = true;
      if (saved.id == null || saved.version == null) {
        throw new Error("İşten çıkış taslağı kimlik veya sürüm bilgisi döndürmedi.");
      }
      await generateCalculation.mutateAsync({
        employeeId,
        caseId: saved.id,
        expectedVersion: saved.version,
        confirmSeveranceEligibility,
        confirmNoticeEligibility,
        expertReviewReference: settlementReference.trim(),
      });
      showToast("Onaylı kural sürümüyle hesap taslağı üretildi. Tamamlamadan önce yeniden inceleyin.", "success");
      onClose();
    } catch (cause) {
      const message = cause instanceof ApiError ? cause.message : "Hesap taslağı üretilemedi.";
      setError(draftPersisted
        ? "İşten çıkış taslağı kaydedildi, ancak hesap taslağı üretilemedi: " + message + " Dosyanın güncel durumunu kontrol edin."
        : message);
    } finally {
      actionInFlight.current = false;
    }
  };

  const prepareReverse = () => {
    setError(null);
    if (!reversalReason.trim() || reversalReason.trim().length > 1000) {
      setError("Geri alma gerekçesini doğrulayın (1–1000 karakter).");
      return;
    }
    setReverseReview(true);
  };

  const reverseCompleted = async () => {
    if (!completedCase || !reverseReview || isBusy || actionInFlight.current) return;
    setError(null);
    if (!reversalReason.trim() || reversalReason.trim().length > 1000) {
      setError("Geri alma gerekçesini doğrulayın (1–1000 karakter).");
      return;
    }

    actionInFlight.current = true;
    try {
      if (completedCase.id == null || completedCase.version == null) {
        throw new Error("İşten çıkış dosyası kimlik veya sürüm bilgisi içermiyor.");
      }
      await reverse.mutateAsync({
        employeeId,
        caseId: completedCase.id,
        expectedVersion: completedCase.version,
        reason: reversalReason.trim(),
      });
      showToast("İşten çıkış kontrollü biçimde geri alındı; erişim işlemlerinin durumunu kontrol edin.", "success");
      onClose();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "İşten çıkış geri alınamadı. Dosyanın güncel durumunu kontrol edin.");
    } finally {
      actionInFlight.current = false;
    }
  };

  const contextError = contextQ.error instanceof ApiError
    ? contextQ.error.message
    : contextQ.isError ? "İşten çıkış bilgileri yüklenemedi." : null;

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in offboarding-modal" role="dialog" aria-modal="true" aria-labelledby="offboarding-title">
        <div className="modal-head">
          <div>
            <h3 id="offboarding-title">
              {completedCase ? "İşten Çıkış Dosyası" : "İşten Çıkış ve Finansal Kapanış"}
            </h3>
            <p>{context?.employeeName ?? "Personel"} için denetlenebilir kapanış sürecini yönetin.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} disabled={isBusy} title="Kapat" aria-label="İşten çıkış penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>

        <div className="modal-body-scroll">
          {contextQ.isPending && <p>İşten çıkış bilgileri yükleniyor…</p>}
          {contextError && <p className="form-error" role="alert">{contextError}</p>}
          {error && <p className="form-error" role="alert">{error}</p>}
          {context && <section className="ez-offboarding-guide" aria-label="İşten ayrılış rehberi">
            <strong>Ne yapıyoruz?</strong>
            <p>Önce ayrılış dosyasını taslak olarak hazırlıyoruz. Taslak kaydı personeli pasife almaz. Tamamlama ise istihdam dönemini kapatır, varsa ekibi halefe devreder ve erişim/rol işlemlerini kuyruğa alır.</p>
            <p><strong>Neden kontrol ediyoruz?</strong> Tarih, çıkış kodu, fesih nedeni, ihbar uygulaması ve finansal tutarlar gerçek vaka ve uzman incelemesiyle uyuşmalıdır. Hesap taslağı tek başına hukuki hak kararı veya ödeme kanıtı değildir.</p>
            <GuidanceSourceLink processKey="offboarding" />
            <p>Kaynaklı metinler inceleme ve yayın onayından sonra katalogda görünür. Gerekli dış bildirim ve ödemeleri ayrıca takip edin; tamamlanan uygulama kaydı bunları doğrulamaz.</p>
          </section>}

          {context && <GuidanceStepLessons process="offboarding" />}
          {context && completedCase && (
            <>
              <div className="notice-card">
                <strong>İlk tamamlanan süreç ve hesap kaydı</strong>
                <p>
                  {completedCase.settlementSource === "approved-rule-draft"
                    ? "Tutarlar onaylı kural sürümüyle taslaklandı ve insan onayıyla tamamlandı."
                    : "Finansal tutarlar uzman/harici kaynaktan alınan snapshot olarak saklanır."}
                </p>
              </div>
              <dl className="offboarding-summary">
                <div><dt>Ayrılış tarihi</dt><dd>{completedCase.terminationDate}</dd></div>
                <div><dt>Çıkış kodu</dt><dd>{completedCase.exitCode}</dd></div>
                <div><dt>Fesih nedeni</dt><dd>{completedCase.terminationReason}</dd></div>
                <div><dt>İhbar uygulaması</dt><dd>{completedCase.noticeHandling}</dd></div>
                <div><dt>Sözleşme snapshot</dt><dd>{completedCase.contractTypeSnapshot}</dd></div>
                <div><dt>Brüt ücret snapshot</dt><dd>{money.format(completedCase.monthlyGrossSalarySnapshot ?? 0)}</dd></div>
                <div><dt>Kalan izin</dt><dd>{completedCase.unusedLeaveDays} gün</dd></div>
                <div><dt>İzin ödemesi</dt><dd>{money.format(completedCase.unusedLeavePayment ?? 0)}</dd></div>
                <div><dt>Kıdem</dt><dd>{money.format(completedCase.severancePayment ?? 0)}</dd></div>
                <div><dt>İhbar</dt><dd>{money.format(completedCase.noticePayment ?? 0)}</dd></div>
                <div><dt>Toplam kapanış</dt><dd>{money.format(completedCase.totalPayment ?? 0)}</dd></div>
                <div><dt>Hesap referansı</dt><dd>{completedCase.settlementReference}</dd></div>
                <div><dt>Hesap kaynağı</dt><dd>{completedCase.settlementSource}</dd></div>
              </dl>
              {effectiveSettlementRevision?.values && <section className="ez-offboarding-review" aria-label="Geçerli düzeltilmiş parasal hesap">
                <h4>Geçerli parasal hesap · sürüm #{effectiveSettlementRevision.revisionNumber}</h4>
                <p>İlk tamamlanan hesap yukarıda korunur. Bu onaylı sürüm yalnız parasal tutarlarda geçerlidir; ayrılış tarihi, çıkış kodu ve personelin pasif durumu değişmedi.</p>
                <dl className="offboarding-summary">
                  <div><dt>Düzeltilmiş brüt ücret</dt><dd>{money.format(effectiveSettlementRevision.values.monthlyGrossSalarySnapshot ?? 0)}</dd></div>
                  <div><dt>Kalan izin</dt><dd>{effectiveSettlementRevision.values.unusedLeaveDays} gün</dd></div>
                  <div><dt>İzin ödemesi</dt><dd>{money.format(effectiveSettlementRevision.values.unusedLeavePayment ?? 0)}</dd></div>
                  <div><dt>Kıdem</dt><dd>{money.format(effectiveSettlementRevision.values.severancePayment ?? 0)}</dd></div>
                  <div><dt>İhbar</dt><dd>{money.format(effectiveSettlementRevision.values.noticePayment ?? 0)}</dd></div>
                  <div><dt>Geçerli toplam</dt><dd>{money.format(effectiveSettlementRevision.values.totalPayment ?? 0)}</dd></div>
                  <div><dt>Uzman hesap referansı</dt><dd>{effectiveSettlementRevision.values.settlementReference}</dd></div>
                </dl>
                <p>Dış ödeme ve bildirim farkı bu uygulama kaydıyla otomatik uzlaştırılmaz.</p>
              </section>}
              <p><strong>Çıkış gerekçesi:</strong> {completedCase.exitReason}</p>
              {context.hasSettlementRevisions && <p className="form-warning" role="status">
                Parasal düzeltme geçmişi bulunduğu için otomatik işten çıkış geri alma kapalıdır.
              </p>}
              {completedCase.identityProvisioningPending && (
                <p className="form-warning" role="status">
                  Identity erişim/rol işlemlerinin bir bölümü yeniden deneme kuyruğunda.
                  {completedCase.identityProvisioningLastError
                    ? ` Son hata: ${completedCase.identityProvisioningLastError}`
                    : ""}
                </p>
              )}
              {!context.hasSettlementRevisions && <div className="input-group">
                <label htmlFor="offboarding-reversal-reason">Geri alma gerekçesi</label>
                <textarea
                  id="offboarding-reversal-reason"
                  className="input-control"
                  rows={3}
                  value={reversalReason}
                  onChange={(event) => { setReversalReason(event.target.value); setReverseReview(false); }}
                  placeholder="Hatalı tarih, yanlış personel veya onay düzeltmesi"
                />
              </div>}
              {!context.hasSettlementRevisions && reverseReview && <section className="ez-offboarding-review" role="region" aria-label="Geri almadan önce kontrol edin">
                <h4>Geri almadan önce kontrol edin</h4>
                <p><strong>Personel:</strong> {context.employeeName} · <strong>Dosya:</strong> #{completedCase.id} · <strong>Gerekçe:</strong> {reversalReason.trim()}</p>
                <p>Sunucu, personel durumunu ve kapanan istihdam/ücret dönemini geri açmayı, değişmeyen ekip bağlarını geri kurmayı ve erişim işlemlerini kuyruğa almayı dener. Arada değişmiş kayıt varsa güvenli geri almayı reddeder.</p>
                <p>Uygulama dışındaki bildirimler ve yapılmış ödemeler otomatik geri alınmaz; bunları ilgili uzmanla ayrıca değerlendirin.</p>
              </section>}
            </>
          )}

          {context && !completedCase && (
            <>
              <div className="notice-card">
                <strong>{calculationLocked ? "Onaylı kural sürümüyle üretilmiş hesap taslağı" : "İki güvenli kapanış yolu"}</strong>
                <p>
                  {calculationLocked
                    ? `Kural #${currentCase?.calculationRuleId}, sürüm ${currentCase?.calculationRuleVersion}. Alanlar hesap izi bozulmasın diye kilitli; ayrı tamamla eylemi insan onayıdır.`
                    : "Harici uzman tutarlarını girebilir veya ayrılış tarihindeki onaylı ve değişmez kural sürümüyle denetlenebilir taslak üretebilirsiniz."}
                </p>
                <p>
                  {directReportCount} doğrudan ekip üyesi ve {pendingLeaveCount} bekleyen izin talebi
                  {requiresSuccessor ? " seçilen halefin kapsamına aktarılacak." : " bulunuyor."}
                </p>
                {calculationLocked && (
                  <button className="btn btn-secondary" onClick={() => { setManualOverride(true); setCompletionReview(false); }}>
                    Harici uzman girişine dön
                  </button>
                )}
              </div>

              {!calculationLocked && !activeRule && !rulesQ.isPending && (
                <p className="form-warning" role="status">
                  Bu tarih için onaylı hesap kuralı yok; harici uzman girişi kullanılabilir.
                </p>
              )}

              <fieldset disabled={calculationLocked} className="offboarding-calculation-fieldset" onChange={() => { setCompletionReview(false); setCompletionAcknowledged(false); }}>
                <div className="form-grid-2 offboarding-form-grid">
                <div className="input-group">
                  <label htmlFor="offboarding-date">İşten ayrılma tarihi</label>
                  <input id="offboarding-date" className="input-control" type="date" max={todayInputValue()}
                    value={terminationDate} onChange={(event) => setTerminationDate(event.target.value)} />
                </div>
                <div className="input-group">
                  <label htmlFor="offboarding-exit-code">Çıkış kodu</label>
                  <input id="offboarding-exit-code" className="input-control" value={exitCode}
                    onChange={(event) => setExitCode(event.target.value)} placeholder="Örn. Kod-03" />
                </div>
                <div className="input-group">
                  <label htmlFor="offboarding-termination-reason">Fesih nedeni</label>
                  <select id="offboarding-termination-reason" className="input-control" value={terminationReason}
                    onChange={(event) => setTerminationReason(event.target.value)}>
                    <option value="">Fesih nedenini seçin</option>
                    <option value="employee-resignation">Çalışan istifası</option>
                    <option value="employer-termination">İşveren feshi</option>
                    <option value="employer-termination-for-cause">İşveren haklı feshi</option>
                    <option value="mutual-agreement">İkale</option>
                    <option value="fixed-term-expiry">Belirli süre bitimi</option>
                    <option value="retirement">Emeklilik</option>
                    <option value="military-service">Askerlik</option>
                    <option value="marriage">Evlilik</option>
                    <option value="death">Vefat</option>
                    <option value="other">Diğer</option>
                  </select>
                </div>
                <div className="input-group">
                  <label htmlFor="offboarding-notice-handling">İhbar uygulaması</label>
                  <select id="offboarding-notice-handling" className="input-control" value={noticeHandling}
                    onChange={(event) => setNoticeHandling(event.target.value)}>
                    <option value="">İhbar uygulamasını seçin</option>
                    <option value="not-applicable">Uygulanmaz</option>
                    <option value="worked">Süre çalışıldı</option>
                    <option value="payable-to-employee">Çalışana ödeme</option>
                    <option value="payable-to-employer">İşverene ödeme</option>
                    <option value="waived">Feragat / mahsup</option>
                  </select>
                </div>
                <div className="input-group form-span-2">
                  <label htmlFor="offboarding-exit-reason">Çıkış gerekçesi</label>
                  <textarea id="offboarding-exit-reason" className="input-control" rows={3}
                    value={exitReason} onChange={(event) => setExitReason(event.target.value)} />
                </div>
                {requiresSuccessor && (
                  <div className="input-group form-span-2">
                    <label htmlFor="offboarding-successor">Halef yönetici</label>
                    <select id="offboarding-successor" className="input-control" value={successorId}
                      onChange={(event) => setSuccessorId(event.target.value)}>
                      <option value="">Halef seçin</option>
                      {successors.map((successor) => (
                        <option key={successor.id} value={successor.id}>
                          {successor.name} — {successor.title}
                          {successor.isDirectReport ? " (ekip içi terfi)" : ""}
                        </option>
                      ))}
                    </select>
                  </div>
                )}
                <div className="input-group">
                  <label htmlFor="offboarding-leave-days">Ödenecek kullanılmamış izin (gün)</label>
                  <input id="offboarding-leave-days" className="input-control" type="number" min="0" step="0.5"
                    value={unusedLeaveDays} onChange={(event) => setUnusedLeaveDays(event.target.value)} />
                </div>
                <div className="input-group">
                  <label htmlFor="offboarding-leave-payment">İzin ödemesi (₺)</label>
                  <input id="offboarding-leave-payment" className="input-control" type="number" min="0" step="0.01"
                    value={unusedLeavePayment} onChange={(event) => setUnusedLeavePayment(event.target.value)} />
                </div>
                <div className="input-group">
                  <label htmlFor="offboarding-severance">Kıdem ödemesi (₺)</label>
                  <input id="offboarding-severance" className="input-control" type="number" min="0" step="0.01"
                    value={severancePayment} onChange={(event) => setSeverancePayment(event.target.value)} />
                </div>
                <div className="input-group">
                  <label htmlFor="offboarding-notice">İhbar ödemesi (₺)</label>
                  <input id="offboarding-notice" className="input-control" type="number" min="0" step="0.01"
                    value={noticePayment} onChange={(event) => setNoticePayment(event.target.value)} />
                </div>
                <div className="input-group form-span-2">
                  <label htmlFor="offboarding-reference">Uzman/harici hesap referansı</label>
                  <input id="offboarding-reference" className="input-control" value={settlementReference}
                    onChange={(event) => setSettlementReference(event.target.value)}
                    placeholder="Mali müşavir hesap özeti, bordro dosyası veya belge numarası" />
                </div>
                <div className="input-group form-span-2">
                  <label htmlFor="offboarding-notes">Kapanış notu</label>
                  <textarea id="offboarding-notes" className="input-control" rows={2}
                    value={settlementNotes} onChange={(event) => setSettlementNotes(event.target.value)} />
                </div>
                {!calculationLocked && activeRule && (
                  <div className="input-group form-span-2 offboarding-eligibility-checks">
                    <strong>Vaka bazlı uzman hak ediş teyidi</strong>
                    <label className="checkbox-line">
                      <input type="checkbox" checked={confirmSeveranceEligibility}
                        onChange={(event) => setConfirmSeveranceEligibility(event.target.checked)} />
                      Bu vaka için kıdem hak edişi uzman tarafından teyit edildi
                    </label>
                    <label className="checkbox-line">
                      <input type="checkbox" checked={confirmNoticeEligibility}
                        onChange={(event) => setConfirmNoticeEligibility(event.target.checked)} />
                      Bu vaka için çalışana ihbar ödemesi uzman tarafından teyit edildi
                    </label>
                  </div>
                )}
              </div>
              </fieldset>

              {completionReview && <section className="ez-offboarding-review" role="region" aria-label="Tamamlamadan önce kontrol edin">
                <h4>Tamamlamadan önce kontrol edin</h4>
                <p><strong>Personel:</strong> {context.employeeName} · <strong>Ayrılış:</strong> {terminationDate} · <strong>Çıkış kodu:</strong> {exitCode.trim()}</p>
                <p><strong>Fesih nedeni:</strong> {terminationReasonLabel[terminationReason]} · <strong>İhbar:</strong> {noticeHandlingLabel[noticeHandling]}</p>
                {requiresSuccessor && <p><strong>Halef:</strong> {successors.find((item) => String(item.id) === successorId)?.name ?? "Seçilmedi"} · {directReportCount} ekip üyesi · {pendingLeaveCount} bekleyen izin talebi</p>}
                <p><strong>Hesap kaynağı:</strong> {calculationLocked ? "Onaylı kural sürümlü taslak" : "Harici uzman girişi"} · <strong>Referans:</strong> {settlementReference.trim()}</p>
                <p><strong>Kalan izin:</strong> {unusedLeaveDays} gün · <strong>İzin ödemesi:</strong> {money.format(Number(unusedLeavePayment))} · <strong>Kıdem:</strong> {money.format(Number(severancePayment))} · <strong>İhbar:</strong> {money.format(Number(noticePayment))}</p>
                <p>Tamamlama personeli pasife alır, istihdam dönemini kapatır ve erişim/rol işlerini kuyruğa alır. Taslak ve tamamlanmış kayıt aynı şey değildir; dış ödeme veya bildirimin tamamlandığı sonucunu çıkarmayın.</p>
                <label className="checkbox-line"><input type="checkbox" checked={completionAcknowledged} onChange={(event) => setCompletionAcknowledged(event.target.checked)} /> Vaka bilgilerini, uzman hesap referansını ve dış adımları ayrıca kontrol ettiğimi onaylıyorum.</label>
              </section>}

              {requiresSuccessor && !hasEligibleSuccessor && (
                <p className="form-error" role="alert">
                  Aktif ve giriş hesabı bağlı uygun halef bulunamadı. Önce bir yönetici hesabı hazırlayın.
                </p>
              )}
            </>
          )}
        </div>

        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose} disabled={isBusy}>Vazgeç</button>
          {completedCase && !context?.hasSettlementRevisions ? (
            reverseReview ? <>
              <button className="btn btn-ghost" onClick={() => setReverseReview(false)} disabled={isBusy}>Bilgileri düzelt</button>
              <button className="btn btn-danger" onClick={reverseCompleted} disabled={isBusy}>
                <i aria-hidden="true" className="fa-solid fa-rotate-left" /> Geri almayı onayla
              </button>
            </> : <button className="btn btn-danger" onClick={prepareReverse} disabled={isBusy}>
              İşten çıkışı geri almayı gözden geçir
            </button>
          ) : completedCase ? null : (
            <>
              {!completionReview && !calculationLocked && activeRule && (
                <button className="btn btn-secondary" onClick={calculateDraft}
                  disabled={!context || !hasEligibleSuccessor || isBusy}>
                  Onaylı kuralla taslak hesapla
                </button>
              )}
              {!completionReview && <button className="btn btn-secondary" onClick={() => save(false)}
                disabled={!context || !hasEligibleSuccessor || isBusy}>
                Taslağı kaydet
              </button>}
              {completionReview ? <>
                <button className="btn btn-ghost" onClick={() => setCompletionReview(false)} disabled={isBusy}>Bilgileri düzelt</button>
                <button className="btn btn-danger" onClick={() => save(true)}
                  disabled={!completionAcknowledged || !context || !hasEligibleSuccessor || isBusy}>
                  <i aria-hidden="true" className="fa-solid fa-user-slash" /> İşten çıkışı onayla ve tamamla
                </button>
              </> : <button className="btn btn-danger" onClick={prepareCompletion}
                disabled={!context || !hasEligibleSuccessor || isBusy}>
                İşten çıkışı gözden geçir
              </button>}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
