import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { LeaveRequestModal } from "./LeaveRequestModal";
import { LEAVE_STATUS_TEXT, awayDateLabel, formatLeaveDate } from "./format";
import { useCancelLeave, useLeaveBalance, useLeaveLedger, useMyLeaves, useTeamLeaves } from "./queries";

const LEDGER_TYPE_TEXT: Record<string, string> = {
  "legacy-opening": "Geçiş bakiyesi",
  "service-anniversary": "Hizmet yılı hak edişi",
  usage: "İzin kullanımı",
  "usage-reversal": "İzin iadesi",
  adjustment: "Manuel düzeltme",
};

export function LeavesPage() {
  const { showToast } = useToast();
  const [modalOpen, setModalOpen] = useState(false);
  const balanceQ = useLeaveBalance();
  const ledgerQ = useLeaveLedger();
  const myQ = useMyLeaves();
  const teamQ = useTeamLeaves();
  const cancelLeave = useCancelLeave();

  const queries = [balanceQ, ledgerQ, myQ, teamQ];
  if (queries.some((q) => q.isPending)) return <PageLoading />;
  const failed = queries.find((q) => q.isError);
  if (failed) return <PageError error={failed.error} />;

  const balance = balanceQ.data!;
  const myLeaves = myQ.data!;
  const ledger = ledgerQ.data!;
  const team = teamQ.data!;

  const entitlement = (balance.entitledDays ?? 0) + (balance.carriedOverDays ?? 0);
  const remaining = balance.remainingDays ?? 0;
  const progress = entitlement > 0 ? Math.round((remaining / entitlement) * 100) : 0;
  const pendingLeaves = myLeaves.filter((l) => l.status === "pending");
  const firstPending = pendingLeaves[0];

  const handleCancel = async (id: number | undefined, type: string) => {
    if (id === undefined) return;
    try {
      await cancelLeave.mutateAsync(id);
      showToast(`${type} talebi iptal edildi.`, "info");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Talep iptal edilemedi.", "error");
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
                  <th></th>
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
                    <td>{formatLeaveDate(leave.startDate)} - {formatLeaveDate(leave.endDate)}</td>
                    <td><span className="days-badge">{leave.days} gün</span></td>
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
                          onClick={() => handleCancel(leave.id, leave.leaveTypeName ?? "İzin")}
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
        </div>

        <div className="sidebar-col">
          <div className="team-calendar-widget card">
            <div className="widget-header"><h3>Ofiste Kimler Yok?</h3></div>
            <div className="away-list">
              {team.map((member) => (
                <div key={member.employeeId} className="away-item">
                  <div className="away-avatar">{member.initials}</div>
                  <div className="away-info">
                    <strong>{member.employeeName}</strong>
                    <span>{awayDateLabel(member.startDate, member.endDate)} • {member.leaveTypeName}</span>
                  </div>
                </div>
              ))}
              {team.length === 0 && <p className="pending-desc">Bu hafta ekipten izinli kimse yok.</p>}
            </div>
          </div>
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

      {modalOpen && <LeaveRequestModal onClose={() => setModalOpen(false)} />}
    </div>
  );
}
