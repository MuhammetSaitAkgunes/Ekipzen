import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { ManagerPage } from "../manager/ManagerPage";
import { useRef, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { LeaveRequestModal } from "./LeaveRequestModal";
import { LEAVE_STATUS_TEXT, awayDateLabel, formatLeaveDate } from "./format";
import { useCancelLeave, useLeaveBalance, useLeaveLedger, useMyLeaves, useTeamLeaves, usePreviewLeaveCancellation, type LeaveCancellationPreview } from "./queries";
import { GuidanceOperationSource } from "../guidance/GuidanceOperationSource";
import { LeaveLearningJourney } from "./LeaveLearningJourney";

const LEDGER_TYPE_TEXT: Record<string, string> = {
  "legacy-opening": "Geçiş bakiyesi",
  "service-anniversary": "Hizmet yılı hak edişi",
  usage: "İzin kullanımı",
  "usage-reversal": "İzin iadesi",
  adjustment: "Manuel düzeltme",
};

function PersonalLeaves() {
  const { showToast } = useToast();
  const [params, setParams] = useSearchParams();
  const [modalOpen, setModalOpen] = useState(params.get("new") === "1");
  const [cancelReviewId, setCancelReviewId] = useState<number | null>(null);
  const [cancelError, setCancelError] = useState<string | null>(null);
  const [cancelPreview, setCancelPreview] = useState<LeaveCancellationPreview | null>(null);
  const [cancelSource, setCancelSource] = useState<number | null>(null);
  const cancelGeneration = useRef(0);
  const previewCancellation = usePreviewLeaveCancellation();
  const closeRequest = () => { setModalOpen(false); const next = new URLSearchParams(params); next.delete("new"); setParams(next, { replace: true }); };
  const balanceQ = useLeaveBalance();
  const ledgerQ = useLeaveLedger();
  const myQ = useMyLeaves();

  const cancelLeave = useCancelLeave();

  const queries = [balanceQ, ledgerQ, myQ];
  if (queries.some((q) => q.isPending)) return <PageLoading />;
  const failed = queries.find((q) => q.isError);
  if (failed) return <PageError error={failed.error} />;

  const balance = balanceQ.data!;
  const myLeaves = myQ.data!;
  const ledger = ledgerQ.data!;


  const entitlement = (balance.entitledDays ?? 0) + (balance.carriedOverDays ?? 0);
  const remaining = balance.remainingDays ?? 0;
  const progress = entitlement > 0 ? Math.round((remaining / entitlement) * 100) : 0;
  const pendingLeaves = myLeaves.filter((l) => l.status === "pending");
  const firstPending = pendingLeaves[0];
  const cancelTarget = myLeaves.find((leave) => leave.id === cancelReviewId
    && (leave.status === "pending" || leave.status === "approved"));

  const prepareCancel = async (id: number | undefined, retainSource = false) => {
    if (id === undefined || cancelLeave.isPending || previewCancellation.isPending) return;
    const generation = ++cancelGeneration.current;
    setCancelError(null);
    setCancelReviewId(id);
    setCancelPreview(null);
    if (!retainSource) setCancelSource(null);
    try {
      const preview = await previewCancellation.mutateAsync(id);
      if (generation === cancelGeneration.current) setCancelPreview(preview);
    } catch (e) {
      if (generation === cancelGeneration.current) setCancelError(e instanceof ApiError ? e.message : "İptal etkisi okunamadı; yeniden kontrol edin.");
    }
  };

  const handleCancel = async () => {
    if (!cancelTarget?.id || !cancelPreview?.canCancel || cancelPreview.id !== cancelTarget.id || cancelLeave.isPending) return;
    setCancelError(null);
    try {
      await cancelLeave.mutateAsync({ id: cancelTarget.id, expectedRecordToken: cancelPreview.recordToken,
        ...(cancelSource !== null ? { guidanceContentRevisionId: cancelSource } : {}) });
      setCancelReviewId(null);
      showToast("İzin talebi iptal edildi. Güncel durumu ve bakiyeyi kontrol edin.", "info");
    } catch (e) {
      setCancelPreview(null);
      setCancelError(e instanceof ApiError ? e.message : "Talep iptal edilemedi. Güncel durumunu kontrol edip tekrar deneyin.");
    }
  };

  return (
    <div id="leaves-screen">
      <div className="page-header">
        <div>
          <h2>İzinlerim</h2>
          <p>Bakiye, geçmiş talepler ve ekip yokluk takibini tek alanda görüntüleyin.</p>
        </div>
        <button className="btn btn-primary" onClick={() => setModalOpen(true)}>
          <i aria-hidden="true" className="fa-solid fa-plus" /> İzin Talebi Oluştur
        </button>
      </div>

      <section className="ez-leave-overview" aria-label="İzin bakiyesi rehberi">
        <strong>Bu ekranı nasıl okuyacağım?</strong>
        <p>Kalan bakiye izin defterindeki hak ediş, kullanım ve iade hareketlerinden gelir. Bekleyen taleplerin rezervasyonu ayrıca gösterilir; yeni bir talebin uygunluğu sunucuda yeniden kontrol edilir.</p>
        <p>Örneğin onaylı yıllık izni yanlış tarihle oluşturduysanız talebi iptal edip doğru tarihlerle yeni talep açın. İptal eski kaydı silmez; bakiyeden düşen kullanım için deftere iade hareketi eklenir.</p>
      </section>
      <LeaveLearningJourney />
      <div className="balance-grid">
        <div className="bal-card primary">
          <div className="bal-header">
            <div className="bal-icon"><i aria-hidden="true" className="fa-solid fa-umbrella-beach" /></div>
            <span className="status-pill info">Aktif bakiye</span>
          </div>
          <div className="bal-info">
            <span>Kalan Yıllık İzin</span>
            <strong>{remaining} <small>gün</small></strong>
          </div>
          <div className="bal-progress"><div className="prog-bar" style={{ width: `${progress}%` }} /></div>
          <span className="bal-sub">Hak ediş: {entitlement} gün</span>
          <span className="bal-sub">Bekleyen rezervasyon: {balance.pendingDays ?? 0} gün</span>
          {balance.nextAccrualDate && (
            <span className="bal-sub">Sıradaki hak ediş: {formatLeaveDate(balance.nextAccrualDate)}</span>
          )}
        </div>
        <div className="bal-card">
          <div className="bal-header"><div className="bal-icon"><i aria-hidden="true" className="fa-solid fa-clock-rotate-left" /></div></div>
          <div className="bal-info">
            <span>Kullanılan Toplam</span>
            <strong>{balance.usedDays ?? 0} <small>gün</small></strong>
          </div>
          <div className="bal-stats">
            <div className="stat-pill"><span className="dot approved" /> {balance.usedDays ?? 0} gün kullanıldı</div>
          </div>
        </div>
        <div className="bal-card">
          <div className="bal-header"><div className="bal-icon"><i aria-hidden="true" className="fa-solid fa-hourglass-half" /></div></div>
          <div className="bal-info">
            <span>Onay Bekleyen</span>
            <strong>{pendingLeaves.length} <small>talep</small></strong>
          </div>
          <p className="pending-desc">
            {firstPending
              ? `${formatLeaveDate(firstPending.startDate)} tarihli ${(firstPending.leaveTypeName ?? "izin").toLocaleLowerCase("tr-TR")} talebiniz yönetici onayı bekliyor.`
              : "Bekleyen talebiniz yok."}
          </p>
        </div>
      </div>

      <div className="leaves-layout">
        <div className="leaves-list-section">
          <div className="section-header"><h3>İzin Hareketleri</h3></div>
          <div className="table-scroll">
            <table className="leaf-table">
              <thead>
                <tr>
                  <th>Tür</th>
                  <th>Tarih Aralığı</th>
                  <th>Süre</th>
                  <th>Durum</th>
                  <th><span className="sr-only">İşlemler</span></th>
                </tr>
              </thead>
              <tbody>
                {myLeaves.map((leave) => (
                  <tr key={leave.id}>
                    <td>
                      <div className="l-type">
                        <span className={`dot ${(leave.leaveTypeName ?? "").includes("Rapor") ? "sick" : "annual"}`} />
                        <strong>{leave.leaveTypeName}</strong>
                      </div>
                    </td>
                    <td data-label="Tarihler">{formatLeaveDate(leave.startDate)} - {formatLeaveDate(leave.endDate)}</td>
                    <td data-label="Süre"><span className="days-badge">{leave.days} gün</span></td>
                    <td>
                      <span className={`status-pill ${leave.status ?? ""}`}>
                        {LEAVE_STATUS_TEXT[leave.status ?? ""] ?? leave.status}
                      </span>
                    </td>
                    <td className="text-right">
                      {(leave.status === "pending" || leave.status === "approved") && (
                        <button
                          className="btn-icon-sm"
                          title="Talebi iptal et"
                          aria-label={`${leave.leaveTypeName} talebini iptal et`}
                          onClick={() => prepareCancel(leave.id)}
                          disabled={cancelLeave.isPending || previewCancellation.isPending}
                          aria-expanded={cancelReviewId === leave.id}
                        >
                          <i aria-hidden="true" className="fa-solid fa-trash" />
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {cancelReviewId !== null && !cancelTarget && (
            <div className="ez-leave-cancel-review" role="status">
              <p>Seçilen talep artık iptal edilebilir durumda görünmüyor. Güncel listeyi kontrol edin.</p>
              <button className="btn btn-ghost" onClick={() => setCancelReviewId(null)}>Kapat</button>
            </div>
          )}
          {cancelTarget && (
            <section className="ez-leave-cancel-review" role="region" aria-labelledby="leave-cancel-review-title">
              <h4 id="leave-cancel-review-title">Talebi iptal etmeden önce</h4>
              <p><strong>Talep:</strong> {cancelTarget.leaveTypeName ?? "İzin"} · {formatLeaveDate(cancelTarget.startDate)} – {formatLeaveDate(cancelTarget.endDate)} · {cancelTarget.days} iş günü</p>
              <p><strong>Şu anki durum:</strong> {LEAVE_STATUS_TEXT[cancelPreview?.status ?? cancelTarget.status ?? ""] ?? cancelTarget.status}</p>
              {(cancelPreview?.status ?? cancelTarget.status) === "pending"
                ? <p>Bekleyen talep iptal edilir. Yıllık bakiyeden rezervasyon yapıldıysa bu rezervasyon kalkar.</p>
                : <p>Onaylı talep iptal edilir. Sunucu silinmeyen gerçek kullanım kaydına karşılık iade hareketi ekler.</p>}
              {previewCancellation.isPending && <p role="status">İptal etkisi sunucuda kontrol ediliyor.</p>}
              {cancelPreview && <p>Kalkacak yıllık rezervasyon: <strong>{cancelPreview.releasedReservationDays} gün</strong> · Deftere iade: <strong>{cancelPreview.refundedDays} gün</strong>. Önizleme kayıt değiştirmez; son onayda yeniden doğrulanır.</p>}
              {cancelPreview && !cancelPreview.canCancel && <p role="status">Bu talep artık iptal edilemez; güncel listeyi kontrol edin.</p>}
              <GuidanceOperationSource process="leave" step="request-lifecycle" value={cancelSource}
                onChange={id => { setCancelSource(id); setCancelPreview(null); }} disabled={cancelLeave.isPending || previewCancellation.isPending}
                label="İptalde kullandığım rehber sürümü" />
              <p>İptal, talebin tarihini veya türünü düzenlemez. Düzeltme gerekiyorsa doğru bilgilerle yeni bir talep oluşturun; yeni talep yeniden kontrol ve onay sürecine girer.</p>
              {cancelError && <p className="form-error" role="alert">{cancelError}</p>}
              <div className="ez-leave-cancel-actions">
                <button className="btn btn-ghost" onClick={() => { cancelGeneration.current++; setCancelReviewId(null); setCancelError(null); setCancelPreview(null); }} disabled={cancelLeave.isPending}>İptalden vazgeç</button>
                <button className="btn btn-secondary" onClick={() => void prepareCancel(cancelTarget.id, true)} disabled={cancelLeave.isPending || previewCancellation.isPending}>İptal etkisini yeniden kontrol et</button>
                <button className="btn btn-primary" onClick={handleCancel} disabled={cancelLeave.isPending || !cancelPreview?.canCancel}>Talebi iptal etmeyi onayla</button>
              </div>
            </section>
          )}
        </div>


      </div>

      <div className="leaves-list-section" style={{ marginTop: 24 }}>
        <div className="section-header">
          <div>
            <h3>İzin Bakiye Defteri</h3>
            <p className="pending-desc">Her hak ediş, kullanım ve iade değiştirilemez bir hareket olarak izlenir.</p>
          </div>
        </div>
        <div className="table-scroll">
          <table className="leaf-table">
            <thead>
              <tr><th>Tarih</th><th>Hareket</th><th>Hizmet yılı</th><th>Gün</th><th>Referans</th></tr>
            </thead>
            <tbody>
              {ledger.map((entry) => (
                <tr key={entry.id}>
                  <td>{formatLeaveDate(entry.occurredOn)}</td>
                  <td>{LEDGER_TYPE_TEXT[entry.type ?? ""] ?? entry.type}</td>
                  <td>{entry.serviceYear ?? "—"}</td>
                  <td><span className="days-badge">{(entry.days ?? 0) > 0 ? "+" : ""}{entry.days ?? 0} gün</span></td>
                  <td>{entry.sourceReference ?? "—"}</td>
                </tr>
              ))}
              {ledger.length === 0 && (
                <tr><td colSpan={5}>Bu dönem için henüz izin defteri hareketi yok.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {modalOpen && <LeaveRequestModal onClose={closeRequest} />}
    </div>
  );
}

function TeamLeaves() {
  const query = useTeamLeaves();
  if (query.isPending) return <PageLoading />;
  if (query.isError) return <PageError error={query.error} />;
  return <section className="ez-panel"><div className="ez-panel-heading"><div><h2>Ekip izinleri</h2><p>Ekibinizin planlanmış izinlerini birlikte görün.</p></div></div>
    <div className="ez-team-grid">{query.data.map((member) => <article className="ez-away-person" key={member.employeeId}><span className="ez-person-avatar">{member.initials}</span><div><strong>{member.employeeName}</strong><small>{awayDateLabel(member.startDate, member.endDate)}</small><small>{member.leaveTypeName}</small></div></article>)}</div>
    {query.data.length === 0 && <div className="ez-empty"><h3>Planlanmış ekip izni yok</h3><p>Kaydedilen izinler burada görünecek.</p></div>}
  </section>;
}
export function LeavesPage() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const management = user?.role === "hr-admin" || user?.role === "manager";
  const personalAvailable = !management || user?.employeeId != null;
  const requested = params.get("tab");
  const tab = requested === "team" ? "team" : requested === "analysis" && management ? "analysis" : personalAvailable ? "personal" : "team";
  const tabs = [...(personalAvailable ? [{ key: "personal", label: "İzinlerim" }] : []), { key: "team", label: "Ekip izinleri" }, ...(management ? [{ key: "analysis", label: "İzin analizleri" }] : [])];
  return <div className="ez-leaves">
    {!personalAvailable && <p>Hesabınıza bağlı personel kartı olmadığı için kişisel izin talebi ve bakiye alanı açılmaz. Ekip izinlerini ve bekleyen onayları yönetebilirsiniz.</p>}
    <div className="ez-tabs" role="tablist" aria-label="İzin bölümleri">{tabs.map((item) => <button key={item.key} id={`leave-tab-${item.key}`} role="tab" aria-selected={tab === item.key} aria-controls="leave-tab-content" onClick={() => setParams({ tab: item.key })}>{item.label}</button>)}
    {management && <Link className="ez-text-link" to={appPath("/inbox?tab=approvals")}>Bekleyen onaylar →</Link>}</div>
    <div role="tabpanel" id="leave-tab-content" aria-labelledby={`leave-tab-${tab}`}>{tab === "personal" ? <PersonalLeaves /> : tab === "team" ? <TeamLeaves /> : <ManagerPage />}</div>
  </div>;
}
