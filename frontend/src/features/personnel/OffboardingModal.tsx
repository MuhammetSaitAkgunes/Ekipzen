import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useEmployeeOffboardingContext, useOffboardEmployee } from "./queries";

const todayInputValue = (): string => {
  const today = new Date();
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-${String(today.getDate()).padStart(2, "0")}`;
};

export function OffboardingModal({ employeeId, onClose }: {
  employeeId: number;
  onClose: () => void;
}) {
  const { showToast } = useToast();
  const contextQ = useEmployeeOffboardingContext(employeeId);
  const offboard = useOffboardEmployee();
  const [terminationDate, setTerminationDate] = useState(todayInputValue);
  const [successorId, setSuccessorId] = useState("");
  const [error, setError] = useState<string | null>(null);

  const context = contextQ.data;
  const directReportCount = context?.directReportCount ?? 0;
  const pendingLeaveCount = context?.pendingLeaveRequestCount ?? 0;
  const successors = context?.successors ?? [];
  const requiresSuccessor = directReportCount > 0;
  const hasEligibleSuccessor = !requiresSuccessor || successors.length > 0;

  const submit = async () => {
    setError(null);
    if (!terminationDate) {
      setError("İşten ayrılma tarihi zorunludur.");
      return;
    }
    if (requiresSuccessor && !successorId) {
      setError("Ekibi olan yönetici için halef seçilmelidir.");
      return;
    }

    try {
      await offboard.mutateAsync({
        employeeId,
        terminationDate,
        successorManagerId: successorId ? Number(successorId) : null,
      });
      showToast("İşten çıkış ve yetki devri tamamlandı.", "success");
      onClose();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : "İşten çıkış tamamlanamadı.");
    }
  };

  const contextError = contextQ.error instanceof ApiError
    ? contextQ.error.message
    : contextQ.isError ? "Devir bilgileri yüklenemedi." : null;

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in" role="dialog" aria-modal="true" aria-labelledby="offboarding-title">
        <div className="modal-head">
          <div>
            <h3 id="offboarding-title">İşten Çıkış ve Halef Devri</h3>
            <p>{context?.employeeName ?? "Personel"} için organizasyon ve erişim kapanışını tamamlayın.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="İşten çıkış penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>

        <div className="modal-body-scroll">
          {contextQ.isPending && <p>Devir bilgileri yükleniyor…</p>}
          {contextError && <p className="form-error" role="alert">{contextError}</p>}
          {error && <p className="form-error" role="alert">{error}</p>}

          {context && (
            <>
              <div className="notice-card">
                <strong>Bu işlem geri dönüş için yeniden aktifleştirme gerektirir.</strong>
                <p>
                  Hesap erişimi kapatılır. {directReportCount} doğrudan ekip üyesi ve {pendingLeaveCount} bekleyen izin talebi
                  {requiresSuccessor ? " seçilen halefin kapsamına aktarılır." : " bulunuyor."}
                </p>
              </div>

              <div className="form-grid-2" style={{ marginTop: 16 }}>
                <div className="input-group">
                  <label htmlFor="offboarding-date">İşten ayrılma tarihi</label>
                  <input
                    id="offboarding-date"
                    className="input-control"
                    type="date"
                    max={todayInputValue()}
                    value={terminationDate}
                    onChange={(event) => setTerminationDate(event.target.value)}
                  />
                </div>

                {requiresSuccessor && (
                  <div className="input-group">
                    <label htmlFor="offboarding-successor">Halef yönetici</label>
                    <select
                      id="offboarding-successor"
                      className="input-control"
                      value={successorId}
                      onChange={(event) => setSuccessorId(event.target.value)}
                    >
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
              </div>

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
          <button
            className="btn btn-danger"
            onClick={submit}
            disabled={!context || !hasEligibleSuccessor || offboard.isPending}
          >
            <i aria-hidden="true" className="fa-solid fa-user-slash" /> İşten çıkışı tamamla
          </button>
        </div>
      </div>
    </div>
  );
}
