import { Link } from "react-router-dom";
import { ApiError } from "../../api/client";
import { appPath } from "../../paths";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { formatToday } from "../dashboard/format";
import { formatLeaveDate } from "../leaves/format";
import { useDecideLeave, usePendingLeaves } from "../leaves/queries";
import { PageError, PageLoading } from "../shared/PageState";
import { DeptDistributionChart, RecruitmentFunnelChart } from "./OverviewCharts";
import { useOverview } from "./queries";

export function OverviewPage() {
  const overview = useOverview();
  const { user } = useAuth();
  const { showToast } = useToast();
  const isManagement = user?.role === "hr-admin" || user?.role === "manager";
  const pendingQ = usePendingLeaves(isManagement);
  const decide = useDecideLeave();

  const handleDecision = async (id: number | undefined, name: string, approve: boolean) => {
    if (id === undefined) return;
    try {
      await decide.mutateAsync({ id, approve });
      showToast(`${name} izin talebi ${approve ? "onaylandı" : "reddedildi"}.`, approve ? "success" : "info");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "İşlem tamamlanamadı.", "error");
    }
  };

  if (overview.isPending) return <PageLoading />;
  if (overview.isError) return <PageError error={overview.error} />;

  const data = overview.data;
  const active = data.activeEmployees ?? 0;
  const inOffice = data.inOfficeToday ?? 0;
  const onLeave = data.onLeaveToday ?? 0;
  const unknown = Math.max(0, active - inOffice - onLeave);
  const pulse = data.pulseScore ?? 0;
  const distribution = data.departmentDistribution ?? [];

  return (
    <div className="dashboard-wrapper">
      <div className="welcome-header">
        <div>
          <h2>Genel Durum</h2>
          <p className="text-muted">Şirkette bugün ne oluyor? Operasyonel İK görünümünü hızlıca tarayın.</p>
        </div>
        <div className="date-widget">
          <i aria-hidden="true" className="fa-regular fa-calendar" />
          <span>{formatToday()}</span>
        </div>
      </div>

      <div className="kpi-grid">
        <div className="kpi-card">
          <div className="kpi-icon bg-blue-light"><i aria-hidden="true" className="fa-solid fa-users" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Aktif Personel</span>
            <h3 className="kpi-value">{active}</h3>
            <span className="kpi-trend">
              <i aria-hidden="true" className="fa-solid fa-arrow-trend-up" /> {distribution.length} departmanda aktif kadro
            </span>
          </div>
        </div>
        <div className="kpi-card">
          <div className="kpi-icon bg-orange-light"><i aria-hidden="true" className="fa-solid fa-file-signature" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Onay Bekleyen</span>
            <h3 className="kpi-value">{data.pendingApprovals ?? 0}</h3>
            <Link className="kpi-link" to={appPath("/manager")}>Talepleri incele</Link>
          </div>
        </div>
        <div className="kpi-card">
          <div className="kpi-icon bg-purple-light"><i aria-hidden="true" className="fa-solid fa-briefcase" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Açık Pozisyon</span>
            <h3 className="kpi-value">{data.openPositions ?? 0}</h3>
            <span className="kpi-sub">{data.newApplications ?? 0} yeni başvuru</span>
          </div>
        </div>
        <div className="kpi-card">
          <div className="kpi-icon bg-red-light"><i aria-hidden="true" className="fa-solid fa-clock-rotate-left" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Bugün İzinli</span>
            <h3 className="kpi-value">{onLeave}</h3>
            <span className="kpi-sub">İzinli / raporlu çalışan</span>
          </div>
        </div>
      </div>

      <div className="charts-grid">
        <div className="card chart-card">
          <div className="card-header-clean">
            <div>
              <h4>Departman Dağılımı</h4>
              <p className="text-muted">Aktif çalışan kırılımı</p>
            </div>
          </div>
          <DeptDistributionChart distribution={distribution} />
        </div>

        <div className="card chart-card">
          <div className="card-header-clean">
            <div>
              <h4>İşe Alım Hunisi</h4>
              <p className="text-muted">Aday ilerleyişi</p>
            </div>
          </div>
          <RecruitmentFunnelChart funnel={data.recruitmentFunnel ?? {}} />
        </div>

        <div className="card status-widget">
          <div className="card-header-clean">
            <h4>Anlık Çalışma Durumu</h4>
          </div>
          <div className="status-list">
            <div className="status-item">
              <div className="status-info"><span className="dot dot-green" /><span>Ofiste</span></div>
              <strong>{inOffice}</strong>
            </div>
            <div className="status-item">
              <div className="status-info"><span className="dot dot-orange" /><span>İzinli / Raporlu</span></div>
              <strong>{onLeave}</strong>
            </div>
            <div className="status-item">
              <div className="status-info"><span className="dot dot-blue" /><span>Kayıt Bekleyen</span></div>
              <strong>{unknown}</strong>
            </div>
          </div>
          <div className="pulse-check">
            <small>Çalışan memnuniyeti</small>
            <div className="progress-bar"><div className="fill" style={{ width: `${pulse}%` }} /></div>
            <small className="text-right">%{pulse} pozitif</small>
          </div>
        </div>
      </div>

      {isManagement && (
        <div className="bottom-grid">
          <div className="card task-list">
            <div className="card-header-clean">
              <div>
                <h4>Bekleyen Aksiyonlar</h4>
                <p className="text-muted">Öncelik sırasına göre</p>
              </div>
              <span className="badge-count">{(pendingQ.data ?? []).length}</span>
            </div>
            <div className="task-stack">
              {(pendingQ.data ?? []).map((request) => (
                <div key={request.id} className="task-item">
                  <div className="task-icon warning"><i aria-hidden="true" className="fa-solid fa-plane-departure" /></div>
                  <div className="task-desc">
                    <strong>{request.employeeName} - {request.leaveTypeName}</strong>
                    <small>{formatLeaveDate(request.startDate)} - {formatLeaveDate(request.endDate)}, {request.days} gün</small>
                  </div>
                  <div className="toolbar-actions">
                    <button className="btn-icon-sm" aria-label={`${request.employeeName} izin talebini onayla`} title="Onayla" onClick={() => handleDecision(request.id, request.employeeName ?? "", true)}>
                      <i aria-hidden="true" className="fa-solid fa-check" />
                    </button>
                    <button className="btn-icon-sm" aria-label={`${request.employeeName} izin talebini reddet`} title="Reddet" onClick={() => handleDecision(request.id, request.employeeName ?? "", false)}>
                      <i aria-hidden="true" className="fa-solid fa-xmark" />
                    </button>
                  </div>
                </div>
              ))}
              {(pendingQ.data ?? []).length === 0 && <p className="pending-desc">Bekleyen talep yok.</p>}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
