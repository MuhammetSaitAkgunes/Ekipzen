import { Link } from "react-router-dom";
import { ApprovalsList } from "../inbox/ApprovalsList";
import { appPath } from "../../paths";
import { useAuth } from "../../auth/AuthContext";

import { formatToday } from "../dashboard/format";


import { PageError, PageLoading } from "../shared/PageState";
import { DeptDistributionChart, RecruitmentFunnelChart } from "./OverviewCharts";
import { useOverview } from "./queries";

export function OverviewPage() {
  const overview = useOverview();
  const { user } = useAuth();

  const isManagement = user?.role === "hr-admin" || user?.role === "manager";
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
            <Link className="kpi-link" to={appPath("/inbox")}>Talepleri incele</Link>
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
            </div>
          </div>
          <DeptDistributionChart distribution={distribution} />
        </div>

        <div className="card chart-card">
          <div className="card-header-clean">
            <div>
              <h4>İşe Alım Hunisi</h4>
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

      {isManagement && <section className="ez-panel"><div className="ez-panel-heading"><h2>Bekleyen onaylar</h2><Link to={appPath("/inbox")} className="ez-text-link">İş kutusuna git →</Link></div><ApprovalsList limit={3} /></section>}
    </div>
  );
}
