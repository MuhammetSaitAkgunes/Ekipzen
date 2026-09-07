import { useEffect, useState } from "react";
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

const nonNegativeNumber = (value: string): number => {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
};

export function OffboardingModal({ employeeId, onClose }: {
  employeeId: number;
  onClose: () => void;
}) {
  const { showToast } = useToast();
  const contextQ = useEmployeeOffboardingContext(employeeId);
  const rulesQ = useTerminationCalculationRules();
  const saveDraft = useSaveOffboardingDraft();
  const generateCalculation = useGenerateOffboardingCalculationDraft();
  const complete = useCompleteOffboarding();
  const reverse = useReverseOffboarding();

  const [terminationDate, setTerminationDate] = useState(todayInputValue);
  const [exitCode, setExitCode] = useState("");
  const [exitReason, setExitReason] = useState("");
  const [terminationReason, setTerminationReason] = useState("other");
  const [noticeHandling, setNoticeHandling] = useState("not-applicable");
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

  const context = contextQ.data;
  const currentCase = context?.currentCase as EmployeeOffboardingCaseDto | null | undefined;
  const completedCase = currentCase?.status === "completed" ? currentCase : null;
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
    setTerminationDate(currentCase.terminationDate ?? todayInputValue());
    setExitCode(currentCase.exitCode ?? "");
    setExitReason(currentCase.exitReason ?? "");
    setTerminationReason(currentCase.terminationReason ?? "other");
    setNoticeHandling(currentCase.noticeHandling ?? "not-applicable");
    setSuccessorId(currentCase.successorManagerId ? String(currentCase.successorManagerId) : "");
    setUnusedLeaveDays(String(currentCase.unusedLeaveDays ?? 0));
    setUnusedLeavePayment(String(currentCase.unusedLeavePayment ?? 0));
    setSeverancePayment(String(currentCase.severancePayment ?? 0));
    setNoticePayment(String(currentCase.noticePayment ?? 0));
    setSettlementReference(currentCase.settlementReference ?? "");
    setSettlementNotes(currentCase.settlementNotes ?? "");
  }, [currentCase]);

  const validateDraft = (): string | null => {
    if (!terminationDate) return "İşten ayrılma tarihi zorunludur.";
    if (!exitCode.trim()) return "Çıkış kodu zorunludur.";
    if (!exitReason.trim()) return "Çıkış gerekçesi zorunludur.";
    if (!settlementReference.trim()) return "Hesap ve uzman inceleme referansı zorunludur.";
    if (requiresSuccessor && !successorId) return "Ekibi olan yönetici için halef seçilmelidir.";
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
    unusedLeaveDays: nonNegativeNumber(unusedLeaveDays),
    unusedLeavePayment: nonNegativeNumber(unusedLeavePayment),
    severancePayment: nonNegativeNumber(severancePayment),
    noticePayment: nonNegativeNumber(noticePayment),
    settlementReference: settlementReference.trim(),
    settlementNotes: settlementNotes.trim() || null,
  });

  const save = async (finish: boolean) => {
    setError(null);
    const validationError = validateDraft();
    if (validationError) {
      setError(validationError);
      return;
    }

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
        showToast("İşten çıkış taslağı kaydedildi.", "success");
      }
      onClose();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "İşten çıkış dosyası kaydedilemedi.");
    }
  };


  const calculateDraft = async () => {
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

    try {
      const saved = await saveDraft.mutateAsync({ employeeId, body: draftBody() });
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
      setError(cause instanceof ApiError ? cause.message : "Hesap taslağı üretilemedi.");
    }
  };
  const reverseCompleted = async () => {
    if (!completedCase) return;
    setError(null);
    if (!reversalReason.trim()) {
      setError("Geri alma gerekçesi zorunludur.");
      return;
    }

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
      showToast("İşten çıkış kontrollü biçimde geri alındı.", "success");
      onClose();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "İşten çıkış geri alınamadı.");
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
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="İşten çıkış penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>

        <div className="modal-body-scroll">
          {contextQ.isPending && <p>İşten çıkış bilgileri yükleniyor…</p>}
          {contextError && <p className="form-error" role="alert">{contextError}</p>}
          {error && <p className="form-error" role="alert">{error}</p>}

          {context && completedCase && (
            <>
              <div className="notice-card">
                <strong>Tamamlanmış ve geri alınabilir süreç kaydı</strong>
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
              <p><strong>Çıkış gerekçesi:</strong> {completedCase.exitReason}</p>
              {completedCase.identityProvisioningPending && (
                <p className="form-warning" role="status">
                  Identity erişim/rol işlemlerinin bir bölümü yeniden deneme kuyruğunda.
                  {completedCase.identityProvisioningLastError
                    ? ` Son hata: ${completedCase.identityProvisioningLastError}`
                    : ""}
                </p>
              )}
              <div className="input-group">
                <label htmlFor="offboarding-reversal-reason">Geri alma gerekçesi</label>
                <textarea
                  id="offboarding-reversal-reason"
                  className="input-control"
                  rows={3}
                  value={reversalReason}
                  onChange={(event) => setReversalReason(event.target.value)}
                  placeholder="Hatalı tarih, yanlış personel veya onay düzeltmesi"
                />
              </div>
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
                  <button className="btn btn-secondary" onClick={() => setManualOverride(true)}>
                    Harici uzman girişine dön
                  </button>
                )}
              </div>

              {!calculationLocked && !activeRule && !rulesQ.isPending && (
                <p className="form-warning" role="status">
                  Bu tarih için onaylı hesap kuralı yok; harici uzman girişi kullanılabilir.
                </p>
              )}

              <fieldset disabled={calculationLocked} className="offboarding-calculation-fieldset">
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

              {requiresSuccessor && !hasEligibleSuccessor && (
                <p className="form-error" role="alert">
                  Aktif ve giriş hesabı bağlı uygun halef bulunamadı. Önce bir yönetici hesabı hazırlayın.
                </p>
              )}
            </>
          )}
        </div>

        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          {completedCase ? (
            <button className="btn btn-danger" onClick={reverseCompleted} disabled={isBusy}>
              <i aria-hidden="true" className="fa-solid fa-rotate-left" /> İşten çıkışı geri al
            </button>
          ) : (
            <>
              {!calculationLocked && activeRule && (
                <button className="btn btn-secondary" onClick={calculateDraft}
                  disabled={!context || !hasEligibleSuccessor || isBusy}>
                  Onaylı kuralla taslak hesapla
                </button>
              )}
              <button className="btn btn-secondary" onClick={() => save(false)}
                disabled={!context || !hasEligibleSuccessor || isBusy}>
                Taslağı kaydet
              </button>
              <button className="btn btn-danger" onClick={() => save(true)}
                disabled={!context || !hasEligibleSuccessor || isBusy}>
                <i aria-hidden="true" className="fa-solid fa-user-slash" /> İşten çıkışı tamamla
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}