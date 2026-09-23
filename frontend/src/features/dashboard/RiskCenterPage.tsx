import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { getRiskLabel, getRiskLevel, topPriorityActions } from "./format";
import { RiskTrendChart } from "./RiskTrendChart";
import {
  useComplianceRisk, useDashboardMetrics, useEmployeeVoice, useManagerLoad, useOpenActions,
} from "./queries";

const count = (value?: number | null) => value == null ? "—" : value.toLocaleString("tr-TR");
const width = (value?: number | null) => Math.max(0, Math.min(100, value ?? 0)) + "%";
const overviewPath = appPath("/dashboard");
const peoplePath = appPath("/dashboard?view=people");
const operationsPath = appPath("/dashboard?view=operations");

function Ring({ value, label, tone = "" }: { value?: number | null; label: string; tone?: string }) {
  const score = value == null ? null : Math.max(0, Math.min(100, value));
  return (
    <div className={"ez-risk-ring " + tone} role="img" aria-label={score == null ? label + ": veri yok" : label + ": 100 üzerinden " + count(value)}>
      <span className="ez-risk-ring-track" style={score == null ? undefined : { background: "conic-gradient(var(--risk-ring-color) " + score + "%, var(--line) 0)" }} aria-hidden="true" />
      <span className="ez-risk-ring-label" aria-hidden="true"><strong>{count(value)}</strong><small>/100</small></span>
    </div>
  );
}

function Bar({ value, tone = "" }: { value?: number | null; tone?: string }) {
  return <div className={"ez-risk-bar " + tone} aria-hidden="true"><span style={{ width: value == null ? "0%" : width(value) }} /></div>;
}

function MetricTile({ label, value, unit, progress, tone, to }: {
  label: string; value?: number | null; unit: string; progress?: number | null; tone?: string; to: string;
}) {
  return (
    <Link className="ez-risk-metric" to={to}>
      <span className="ez-risk-metric-top"><strong>{label}</strong><i className="fa-solid fa-arrow-up-right-from-square" aria-hidden="true" /></span>
      <span className="ez-risk-metric-value">{count(value)}<small>{unit}</small></span>
      {progress != null && <Bar value={progress} tone={tone} />}
    </Link>
  );
}

function Overview() {
  const metricsQ = useDashboardMetrics();
  const actionsQ = useOpenActions();
  if (metricsQ.isPending || actionsQ.isPending) return <PageLoading />;
  if (metricsQ.isError) return <PageError error={metricsQ.error} />;
  if (actionsQ.isError) return <PageError error={actionsQ.error} />;

  const metrics = metricsQ.data;
  const actions = actionsQ.data;
  const high = actions.filter((action) => action.priority === "high").length;
  const medium = actions.filter((action) => action.priority === "medium").length;
  const low = actions.length - high - medium;
  const riskScore = metrics.riskScore;

  return (
    <div className="ez-risk-view">
      <div className="ez-risk-overview-grid">
        <section className="ez-risk-surface ez-risk-overview-score" aria-labelledby="risk-status-title">
          <div className="ez-risk-section-heading"><span className="ez-risk-eyebrow">GENEL RİSK</span></div>
          <div className="ez-risk-overview-score-main">
            <Ring value={riskScore} label="Genel risk" tone={riskScore == null ? "" : getRiskLevel(riskScore)} />
            <div><h2 id="risk-status-title">{riskScore == null ? "Veri yok" : getRiskLabel(riskScore)}</h2><span>Çalışan risk ortalaması</span></div>
          </div>
          <div className="ez-risk-overview-mini">
            <Link to={peoplePath}><strong>{count(metrics.attritionHigh)}</strong><span>Ayrılma riski</span></Link>
            <Link to={peoplePath}><strong>{count(metrics.burnoutRisk)}</strong><span>Tükenmişlik</span></Link>
          </div>
        </section>

        <section className="ez-risk-surface ez-risk-overview-actions" aria-labelledby="risk-actions-title">
          <div className="ez-risk-section-heading"><div><span className="ez-risk-eyebrow">İŞ KUTUSU</span><h2 id="risk-actions-title">Açık işler <em>{count(actions.length)}</em></h2></div><Link to={appPath("/inbox?tab=actions")}>Tümünü aç <span aria-hidden="true">→</span></Link></div>
          {actions.length === 0 ? <p className="ez-risk-empty">Şu anda açık aksiyon yok.</p> : (
            <>
              <div className="ez-risk-priority-chart" role="img" aria-label={count(high) + " yüksek, " + count(medium) + " orta, " + count(low) + " düşük öncelikli açık iş"}>
                <span className="high" style={{ width: width(high / actions.length * 100) }} /><span className="medium" style={{ width: width(medium / actions.length * 100) }} /><span className="low" style={{ width: width(low / actions.length * 100) }} />
              </div>
              <div className="ez-risk-priority-legend" aria-hidden="true"><span><i className="high" />{count(high)} yüksek</span><span><i className="medium" />{count(medium)} orta</span><span><i className="low" />{count(low)} düşük</span></div>
              <div className="ez-risk-action-list">
                {topPriorityActions(actions, 3).map((action) => (
                  <Link className="ez-risk-action" key={action.id} to={appPath("/inbox?tab=actions")} aria-label={(action.title || "Aksiyon") + " işini İş Kutusunda aç"}>
                    <i className={"ez-risk-action-dot " + (action.priority || "low")} aria-hidden="true" />
                    <span>{action.title}</span>
                    <small>{action.due || ""}</small>
                    <i className="fa-solid fa-arrow-right" aria-hidden="true" />
                  </Link>
                ))}
              </div>
            </>
          )}
        </section>
      </div>
      <div className="ez-risk-destinations" aria-label="Ayrıntılı görünümler">
        <Link to={peoplePath}><span><strong>İnsan ve ekip</strong><small>Çalışan sinyalleri · departmanlar · eğilim</small></span><i className="fa-solid fa-arrow-right" aria-hidden="true" /></Link>
        <Link to={operationsPath}><span><strong>Operasyon ve uyum</strong><small>Evrak · işe alım · kritik roller</small></span><i className="fa-solid fa-arrow-right" aria-hidden="true" /></Link>
      </div>
    </div>
  );
}

function PeopleView() {
  const metricsQ = useDashboardMetrics();
  const loadQ = useManagerLoad();
  const voiceQ = useEmployeeVoice();
  if (metricsQ.isPending || loadQ.isPending || voiceQ.isPending) return <PageLoading />;
  if (metricsQ.isError) return <PageError error={metricsQ.error} />;
  if (loadQ.isError) return <PageError error={loadQ.error} />;
  if (voiceQ.isError) return <PageError error={voiceQ.error} />;

  const metrics = metricsQ.data;
  const load = loadQ.data;
  const voice = voiceQ.data;
  const employees = metrics.employees?.length ?? 0;
  const departments = [...(metrics.departmentRisk ?? [])].sort((a, b) => (b.risk ?? 0) - (a.risk ?? 0));
  const trend = metrics.riskTrend ?? [];
  return (
    <div className="ez-risk-view">
      <div className="ez-risk-metrics-grid">
        <MetricTile label="Ayrılma riski" value={metrics.attritionHigh} unit="kişi" progress={employees && metrics.attritionHigh != null ? metrics.attritionHigh / employees * 100 : null} tone="high" to={appPath("/risk/attrition")} />
        <MetricTile label="Tükenmişlik" value={metrics.burnoutRisk} unit="kişi" progress={employees && metrics.burnoutRisk != null ? metrics.burnoutRisk / employees * 100 : null} tone="medium" to={appPath("/risk/burnout")} />
        <MetricTile label="Yönetici yükü" value={metrics.managerLoadIndex} unit="/100" progress={metrics.managerLoadIndex} tone="medium" to={appPath("/risk/manager-load")} />
        <MetricTile label="Çalışan nabzı" value={voice.pulseScore} unit="/100" progress={voice.pulseScore} tone="primary" to={appPath("/risk/employee-voice")} />
      </div>
      <div className="ez-risk-analysis-grid">
        <section className="ez-risk-surface" aria-labelledby="risk-trend-title">
          <div className="ez-risk-section-heading"><div><h2 id="risk-trend-title">Risk eğilimi</h2><small>Son {count(trend.length)} dönem</small></div></div>
          {trend.length === 0 ? <p className="ez-risk-empty">Karşılaştırılacak dönem verisi yok.</p> : <RiskTrendChart trend={trend} />}
        </section>
        <section className="ez-risk-surface" aria-labelledby="department-risk-title">
          <div className="ez-risk-section-heading"><h2 id="department-risk-title">Departman karşılaştırması</h2></div>
          {departments.length === 0 ? <p className="ez-risk-empty">Bu kapsamda departman verisi yok.</p> : (
            <div className="ez-risk-departments">
              {departments.map((department) => (
                <div className="ez-risk-department" key={department.departmentId}>
                  <div><strong>{department.dept}</strong><b>{count(department.risk)}</b></div>
                  <Bar value={department.risk} tone={department.risk == null ? "" : getRiskLevel(department.risk)} />
                  <small>{count(department.employeeCount)} çalışan · {count(department.highAttritionCount)} ayrılma · {count(department.highBurnoutCount)} tükenmişlik</small>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>
      <div className="ez-risk-people-links">
        <Link to={appPath("/risk/manager-load")}>{count(load.pendingApprovals)} bekleyen onay · {count(load.openActions)} açık yönetici işi <span aria-hidden="true">→</span></Link>
        <Link to={appPath("/risk/employee-voice")}>{count(voice.decliningTeams)} ekipte nabız düşüşü <span aria-hidden="true">→</span></Link>
      </div>
    </div>
  );
}

function OperationsView() {
  const { user } = useAuth();
  const metricsQ = useDashboardMetrics();
  const complianceQ = useComplianceRisk();
  if (metricsQ.isPending || complianceQ.isPending) return <PageLoading />;
  if (metricsQ.isError) return <PageError error={metricsQ.error} />;
  if (complianceQ.isError) return <PageError error={complianceQ.error} />;

  const metrics = metricsQ.data;
  const compliance = complianceQ.data;
  const capacity = (metrics.talentCapacity ?? []).filter((item) => item.label !== "Kültür / Nabız");
  return (
    <div className="ez-risk-view">
      <section className="ez-risk-surface" aria-labelledby="compliance-title">
        <div className="ez-risk-section-heading"><h2 id="compliance-title">Evrak uyumu</h2><Link to={appPath("/risk/compliance")}>Ayrıntılar <span aria-hidden="true">→</span></Link></div>
        <div className="ez-risk-compliance-grid">
          <Ring value={compliance.documentComplianceScore} label="Evrak uyumu" tone="primary" />
          <div className="ez-risk-compliance-stats">
            <div><strong>{count(compliance.missingDocuments)}</strong><span>Eksik evrak</span></div>
            <div><strong>{count(compliance.upcomingDocuments)}</strong><span>Yaklaşan tarih</span></div>
            <div><strong>{compliance.auditReadinessRisk || "—"}</strong><span>Denetim riski</span></div>
          </div>
        </div>
      </section>
      <section className="ez-risk-surface" aria-labelledby="capacity-title">
        <div className="ez-risk-section-heading"><h2 id="capacity-title">İş gücü kapasitesi</h2>{user?.role === "hr-admin" && <Link to={appPath("/recruitment")}>İşe alımı aç <span aria-hidden="true">→</span></Link>}</div>
        {capacity.length === 0 ? <p className="ez-risk-empty">Bu alanda henüz gösterge yok.</p> : (
          <div className="ez-risk-capacity-list">
            {capacity.map((item) => (
              <div className="ez-risk-capacity-row" key={item.label}>
                <div><strong>{item.label}</strong><b>{count(item.value)}{item.value == null || item.label === "Kritik Rol Riski" ? "" : "%"}</b></div>
                {item.label !== "Kritik Rol Riski" && item.value != null && <Bar value={item.value} tone={item.tone || "primary"} />}
                <small>{item.meta}</small>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}

export function RiskCenterPage() {
  const [params] = useSearchParams();
  const view = params.get("view") === "people" ? "people" : params.get("view") === "operations" ? "operations" : "overview";
  return (
    <div className="ez-risk-page">
      <header className="ez-risk-header"><span className="ez-risk-eyebrow">ANALİZ</span><h1>Risk Merkezi</h1></header>
      <nav className="ez-risk-nav" aria-label="Risk Merkezi görünümleri">
        <Link to={overviewPath} aria-current={view === "overview" ? "page" : undefined}>Özet</Link>
        <Link to={peoplePath} aria-current={view === "people" ? "page" : undefined}>İnsan ve ekip</Link>
        <Link to={operationsPath} aria-current={view === "operations" ? "page" : undefined}>Operasyon ve uyum</Link>
      </nav>
      {view === "people" ? <PeopleView /> : view === "operations" ? <OperationsView /> : <Overview />}
    </div>
  );
}
