import { useNavigate } from "react-router-dom";
import { appRoutes } from "../../routes";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { formatToday, getRiskLabel, getRiskLevel, priorityLabel, topPriorityActions } from "./format";
import { RiskTrendChart } from "./RiskTrendChart";
import {
  useComplianceRisk, useDashboardMetrics, useEmployeeVoice, useManagerLoad, useOpenActions,
} from "./queries";

const pathForRouteKey = (key?: string | null): string =>
  appRoutes.find((r) => r.key === key)?.path ?? appPath("/actions");

export function RiskCenterPage() {
  const navigate = useNavigate();
  const metricsQ = useDashboardMetrics();
  const loadQ = useManagerLoad();
  const voiceQ = useEmployeeVoice();
  const complianceQ = useComplianceRisk();
  const actionsQ = useOpenActions();

  const queries = [metricsQ, loadQ, voiceQ, complianceQ, actionsQ];
  if (queries.some((q) => q.isPending)) return <PageLoading />;
  const failed = queries.find((q) => q.isError);
  if (failed) return <PageError error={failed.error} />;

  const metrics = metricsQ.data!;
  const load = loadQ.data!;
  const voice = voiceQ.data!;
  const compliance = complianceQ.data!;
  const actions = actionsQ.data!;

  const riskScore = metrics.riskScore ?? 0;
  const trend = metrics.riskTrend ?? [];
  const delta = trend.length >= 2 ? trend[trend.length - 1] - trend[0] : 0;
  const todayCount = actions.filter((a) => a.priority === "high").length;
  const weekCount = actions.filter((a) => a.priority === "medium").length;
  const watchCount = actions.filter((a) => a.priority === "low").length;

  return (
    <div className="dashboard-wrapper">
      <div className="welcome-header">
        <div>
          <h2>Risk & Aksiyon Merkezi</h2>
          <p className="text-muted">Bugünün odak sorusu: hangi risk büyüyor, neden büyüyor ve hangi aksiyon alınmalı?</p>
        </div>
        <div className="date-widget">
          <i aria-hidden="true" className="fa-regular fa-calendar" />
          <span>{formatToday()}</span>
        </div>
      </div>

      <div className="kpi-grid intelligence-kpis">
        <button className={`kpi-card risk-kpi ${getRiskLevel(riskScore)} metric-card-button`} onClick={() => navigate(appPath("/actions"))}>
          <div className="kpi-icon bg-red-light"><i aria-hidden="true" className="fa-solid fa-shield-heart" /></div>
          <div className="kpi-content">
            <span className="kpi-label">İK Risk Skoru</span>
            <h3 className="kpi-value">{riskScore}<small>/100</small></h3>
            <span className="kpi-sub">{getRiskLabel(riskScore)} · 3 ana risk sürücüsü</span>
          </div>
        </button>
        <button className="kpi-card risk-kpi medium metric-card-button" onClick={() => navigate(appPath("/risk/manager-load"))}>
          <div className="kpi-icon bg-orange-light"><i aria-hidden="true" className="fa-solid fa-user-group" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Yönetici Yük Endeksi</span>
            <h3 className="kpi-value">{metrics.managerLoadIndex ?? 0}<small>/100</small></h3>
            <span className="kpi-sub">{load.pendingApprovals ?? 0} onay, {load.openActions ?? 0} açık aksiyon, yoğun ekip</span>
          </div>
        </button>
        <button className="kpi-card risk-kpi high metric-card-button" onClick={() => navigate(appPath("/risk/attrition"))}>
          <div className="kpi-icon bg-purple-light"><i aria-hidden="true" className="fa-solid fa-person-walking-arrow-right" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Ayrılma Riski Radarı</span>
            <h3 className="kpi-value">{metrics.attritionHigh ?? 0}</h3>
            <span className="kpi-sub">Yüksek riskli çalışan segmenti</span>
          </div>
        </button>
        <button className="kpi-card risk-kpi medium metric-card-button" onClick={() => navigate(appPath("/actions"))}>
          <div className="kpi-icon bg-blue-light"><i aria-hidden="true" className="fa-solid fa-bolt" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Bugünkü Kritik Aksiyon</span>
            <h3 className="kpi-value">{metrics.criticalActions ?? 0}</h3>
            <span className="kpi-sub">{todayCount} bugün, {weekCount} bu hafta, {watchCount} izlemede</span>
          </div>
        </button>
      </div>

      <div className="risk-dashboard-grid">
        <div className="risk-main">
          <div className="risk-decision-grid">
            <div className="card chart-card">
              <div className="card-header-clean">
                <div>
                  <h4>90 Günlük Risk Trendi</h4>
                  <p className="text-muted">Risk skoru son {trend.length} hafta içinde {delta >= 0 ? "yukarı" : "aşağı"} yönlü seyrediyor.</p>
                </div>
                <span className="status-pill pending">{delta >= 0 ? "+" : ""}{delta} puan</span>
              </div>
              <RiskTrendChart trend={trend} />
            </div>

            <div className="card heatmap-card">
              <div className="card-header-clean">
                <div>
                  <h4>Departman Bazlı Risk Isı Haritası</h4>
                  <p className="text-muted">Risk seviyesi ve öne çıkan nedenler.</p>
                </div>
              </div>
              <div className="risk-heatmap">
                {(metrics.departmentRisk ?? []).map((dept) => (
                  <div key={dept.departmentId} className={`heatmap-line ${getRiskLevel(dept.risk ?? 0)}`}>
                    <div className="heatmap-score">{dept.risk ?? 0}</div>
                    <div className="heatmap-body">
                      <strong>{dept.dept}</strong>
                      <span>{dept.employeeCount ?? 0} çalışan · {dept.highAttritionCount ?? 0} yüksek ayrılma · {dept.highBurnoutCount ?? 0} tükenmişlik sinyali</span>
                    </div>
                    <div className="heatmap-bar"><span style={{ width: `${dept.risk ?? 0}%` }} /></div>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="card capacity-card">
            <div className="card-header-clean">
              <div>
                <h4>Yetenek ve Kapasite</h4>
                <p className="text-muted">İşe alım, beceri, kritik rol ve kültür sinyalleri.</p>
              </div>
              <button className="btn btn-secondary btn-sm" onClick={() => navigate(appPath("/recruitment"))}>
                <i aria-hidden="true" className="fa-solid fa-arrow-up-right-from-square" /> İşe alıma git
              </button>
            </div>
            <div className="capacity-grid">
              {(metrics.talentCapacity ?? []).map((item) => (
                <div key={item.label} className={`capacity-item ${item.tone ?? ""}`}>
                  <div className="capacity-top">
                    <span>{item.label}</span>
                    <strong>{item.value}{item.label === "Kritik Rol Riski" ? "" : "%"}</strong>
                  </div>
                  <div className="progress-bar">
                    <div className="fill" style={{ width: `${item.label === "Kritik Rol Riski" ? (item.value ?? 0) * 22 : item.value ?? 0}%` }} />
                  </div>
                  <small>{item.meta}</small>
                </div>
              ))}
            </div>
          </div>

          <div className="card signal-card">
            <div className="card-header-clean">
              <div>
                <h4>Kurumsal Sinyaller</h4>
                <p className="text-muted">Çalışan nabzı, sessiz memnuniyetsizlik ve evrak uyum risklerini birlikte izleyin.</p>
              </div>
              <div className="toolbar-actions">
                <button className="btn btn-secondary btn-sm" onClick={() => navigate(appPath("/risk/employee-voice"))}>
                  <i aria-hidden="true" className="fa-solid fa-wave-square" /> Nabız detayı
                </button>
                <button className="btn btn-secondary btn-sm" onClick={() => navigate(appPath("/risk/compliance"))}>
                  <i aria-hidden="true" className="fa-solid fa-file-shield" /> Uyum detayı
                </button>
              </div>
            </div>
            <div className="signal-grid">
              <button className="signal-item high metric-card-button" onClick={() => navigate(appPath("/risk/employee-voice"))}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-heart-pulse" /></div>
                <div>
                  <span>Çalışan Nabız Skoru</span>
                  <strong>{voice.pulseScore ?? 0}<small>/100</small></strong>
                  <p>{voice.sentimentTrend}</p>
                </div>
              </button>
              <button className="signal-item medium metric-card-button" onClick={() => navigate(appPath("/risk/employee-voice"))}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-comments" /></div>
                <div>
                  <span>eNPS / Bağlılık</span>
                  <strong>{(voice.eNps ?? 0) >= 0 ? "+" : ""}{voice.eNps ?? 0}</strong>
                  <p>{voice.decliningTeams ?? 0} ekipte sessiz memnuniyetsizlik sinyali var.</p>
                </div>
              </button>
              <button className="signal-item medium metric-card-button" onClick={() => navigate(appPath("/risk/compliance"))}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-folder-check" /></div>
                <div>
                  <span>Evrak Uyum Skoru</span>
                  <strong>{compliance.documentComplianceScore ?? 0}<small>/100</small></strong>
                  <p>{compliance.missingDocuments ?? 0} eksik evrak, {compliance.upcomingDocuments ?? 0} yaklaşan son tarih.</p>
                </div>
              </button>
              <button className="signal-item medium metric-card-button" onClick={() => navigate(appPath("/risk/compliance"))}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-clipboard-check" /></div>
                <div>
                  <span>Denetime Hazırlık</span>
                  <strong>{compliance.auditReadinessRisk}</strong>
                  <p>Hazırlık skoru {compliance.auditReadinessScore ?? 0}/100; kritik evrak aksiyonları izleniyor.</p>
                </div>
              </button>
            </div>
          </div>
        </div>

        <aside className="card action-center">
          <div className="card-header-clean">
            <div>
              <h4>En Acil Aksiyonlar</h4>
              <p className="text-muted">Önceliği en yüksek 3 müdahale.</p>
            </div>
            <button className="btn btn-secondary btn-sm" onClick={() => navigate(appPath("/actions"))}>
              Tümünü aç ({actions.length})
            </button>
          </div>
          <div className="action-list">
            {topPriorityActions(actions, 3).map((action) => (
              <button key={action.id} className={`risk-action ${action.priority ?? ""}`} onClick={() => navigate(pathForRouteKey(action.sourceRoute))}>
                <div className="action-priority">{priorityLabel(action.priority)}</div>
                <strong>{action.title}</strong>
                <p>{action.source} · {action.owner}</p>
                {action.action && (
                  <div className="recommended-action">
                    <i aria-hidden="true" className="fa-solid fa-lightbulb" />
                    <span>{action.action}</span>
                  </div>
                )}
              </button>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}
