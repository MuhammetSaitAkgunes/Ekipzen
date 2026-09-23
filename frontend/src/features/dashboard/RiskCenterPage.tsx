import { Link, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { getRiskLabel, getRiskLevel, priorityLabel, topPriorityActions } from "./format";
import { RiskTrendChart } from "./RiskTrendChart";
import {
  useComplianceRisk, useDashboardMetrics, useEmployeeVoice, useManagerLoad, useOpenActions,
} from "./queries";

const count = (value?: number | null) => value == null ? "—" : value.toLocaleString("tr-TR");
const overviewPath = appPath("/dashboard");
const peoplePath = appPath("/dashboard?view=people");
const operationsPath = appPath("/dashboard?view=operations");

type SignalProps = {
  title: string;
  value: string;
  detail: string;
  to: string;
};

function Signal({ title, value, detail, to }: SignalProps) {
  return (
    <Link className="ez-risk-signal" to={to}>
      <span className="ez-risk-signal-copy"><strong>{title}</strong><small>{detail}</small></span>
      <span className="ez-risk-signal-end"><b>{value}</b><i className="fa-solid fa-arrow-right" aria-hidden="true" /></span>
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
  const urgentCount = actions.filter((action) => action.priority === "high").length;
  const riskScore = metrics.riskScore;

  return (
    <div className="ez-risk-view">
      <section className="ez-risk-status-card" aria-labelledby="risk-status-title">
        <div>
          <span className="ez-risk-eyebrow">GENEL DURUM</span>
          <h2 id="risk-status-title">{riskScore == null ? "Henüz risk verisi yok" : getRiskLabel(riskScore)}</h2>
          <p>Yetkili olduğunuz çalışanların ortalama risk düzeyi.</p>
        </div>
        <span className={"ez-risk-score-chip " + (riskScore == null ? "" : getRiskLevel(riskScore))}>
          {riskScore == null ? "Veri yok" : count(riskScore) + " / 100"}
        </span>
      </section>

      <section className="ez-risk-surface" aria-labelledby="risk-actions-title">
        <div className="ez-risk-section-heading">
          <div><span className="ez-risk-eyebrow">YAPILACAKLAR</span><h2 id="risk-actions-title">Öncelikli işler</h2></div>
          <Link to={appPath("/inbox?tab=actions")}>İş kutusunu aç <span aria-hidden="true">→</span></Link>
        </div>
        {actions.length === 0 ? (
          <p className="ez-risk-empty">Şu anda açık aksiyon yok. Yeni bir iş oluştuğunda burada görünecek.</p>
        ) : (
          <>
            <p className="ez-risk-context">{urgentCount > 0 ? count(urgentCount) + " yüksek öncelikli iş açık." : "Yüksek öncelikli iş yok."}</p>
            <div className="ez-risk-action-list">
              {topPriorityActions(actions, 3).map((action) => (
                <Link className="ez-risk-action" key={action.id} to={appPath("/inbox?tab=actions")} aria-label={(action.title || "Aksiyon") + " işini İş Kutusunda aç"}>
                  <span className={"ez-risk-priority " + (action.priority || "low")}>{priorityLabel(action.priority)}</span>
                  <span className="ez-risk-action-copy"><strong>{action.title}</strong><small>{action.owner || "Atanmamış"}{action.due ? " · " + action.due : ""}</small></span>
                  <i className="fa-solid fa-arrow-right" aria-hidden="true" />
                </Link>
              ))}
            </div>
          </>
        )}
      </section>

      <div className="ez-risk-destinations" aria-label="Ayrıntılı görünümler">
        <Link to={peoplePath}><span><strong>İnsan ve ekip</strong><small>Ayrılma, tükenmişlik ve ekip sinyalleri</small></span><i className="fa-solid fa-arrow-right" aria-hidden="true" /></Link>
        <Link to={operationsPath}><span><strong>Operasyon ve uyum</strong><small>Evrak, işe alım ve kritik roller</small></span><i className="fa-solid fa-arrow-right" aria-hidden="true" /></Link>
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
  const departments = [...(metrics.departmentRisk ?? [])].sort((a, b) => (b.risk ?? 0) - (a.risk ?? 0));
  const trend = metrics.riskTrend ?? [];
  return (
    <div className="ez-risk-view">
      <div className="ez-risk-view-intro"><h2>İnsan ve ekip</h2><p>Çalışan ve yönetici sinyallerini bir arada inceleyin.</p></div>
      <section className="ez-risk-surface" aria-labelledby="people-signals-title">
        <div className="ez-risk-section-heading"><h3 id="people-signals-title">Takip edilecek alanlar</h3></div>
        <div className="ez-risk-signal-list">
          <Signal title="Ayrılma riski" value={count(metrics.attritionHigh) + " kişi"} detail="Yüksek risk grubundaki çalışanlar" to={appPath("/risk/attrition")} />
          <Signal title="Tükenmişlik" value={count(metrics.burnoutRisk) + " sinyal"} detail="İş yükü ve çalışan durumu" to={appPath("/risk/burnout")} />
          <Signal title="Yönetici yükü" value={count(metrics.managerLoadIndex) + " / 100"} detail={count(load.pendingApprovals) + " bekleyen onay · " + count(load.openActions) + " açık iş"} to={appPath("/risk/manager-load")} />
          <Signal title="Çalışan nabzı" value={count(voice.pulseScore) + " / 100"} detail={count(voice.decliningTeams) + " ekipte düşüş sinyali"} to={appPath("/risk/employee-voice")} />
        </div>
      </section>
      <section className="ez-risk-surface" aria-labelledby="department-risk-title">
        <div className="ez-risk-section-heading"><div><h3 id="department-risk-title">Departmanlar</h3><p>Yüksek risk puanı olan ekipler önce gösterilir.</p></div></div>
        {departments.length === 0 ? <p className="ez-risk-empty">Bu kapsamda departman verisi yok.</p> : (
          <div className="ez-risk-departments">
            {departments.map((department) => (
              <div className="ez-risk-department" key={department.departmentId}>
                <span><strong>{department.dept}</strong><small>{count(department.employeeCount)} çalışan · {count(department.highAttritionCount)} yüksek ayrılma · {count(department.highBurnoutCount)} tükenmişlik</small></span>
                <span className={"ez-risk-department-level " + getRiskLevel(department.risk ?? 0)}>{count(department.risk)} / 100 · {getRiskLabel(department.risk ?? 0)}</span>
              </div>
            ))}
          </div>
        )}
      </section>
      <details className="ez-risk-details">
        <summary>Dönemsel risk eğilimini göster <i className="fa-solid fa-chevron-down" aria-hidden="true" /></summary>
        <div>{trend.length === 0 ? <p className="ez-risk-empty">Henüz karşılaştırılacak dönem verisi yok.</p> : <RiskTrendChart trend={trend} />}</div>
      </details>
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
      <div className="ez-risk-view-intro"><h2>Operasyon ve uyum</h2><p>Evrak yükümlülüklerini ve iş gücü kapasitesini izleyin.</p></div>
      <section className="ez-risk-surface" aria-labelledby="compliance-title">
        <div className="ez-risk-section-heading"><h3 id="compliance-title">Evrak ve uyum</h3></div>
        <Signal title="Eksik evraklar" value={count(compliance.missingDocuments)} detail={count(compliance.upcomingDocuments) + " yaklaşan son tarih · Denetim hazırlığı: " + (compliance.auditReadinessRisk || "—")} to={appPath("/risk/compliance")} />
      </section>
      <section className="ez-risk-surface" aria-labelledby="capacity-title">
        <div className="ez-risk-section-heading">
          <div><h3 id="capacity-title">İş gücü kapasitesi</h3><p>İşe alım, beceri ve kritik roller için göstergeler.</p></div>
          {user?.role === "hr-admin" && <Link to={appPath("/recruitment")}>İşe alımı aç <span aria-hidden="true">→</span></Link>}
        </div>
        {capacity.length === 0 ? <p className="ez-risk-empty">Bu alanda henüz gösterge yok.</p> : (
          <div className="ez-risk-capacity-list">
            {capacity.map((item) => (
              <div className="ez-risk-capacity-row" key={item.label}>
                <span><strong>{item.label}</strong><small>{item.meta}</small></span>
                <b>{count(item.value)}{item.value == null || item.label === "Kritik Rol Riski" ? "" : "%"}</b>
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
      <header className="ez-risk-header"><span className="ez-risk-eyebrow">ANALİZ</span><h1>Risk Merkezi</h1><p>Durumu görün, ilgilenmeniz gereken işe geçin.</p></header>
      <nav className="ez-risk-nav" aria-label="Risk Merkezi görünümleri">
        <Link to={overviewPath} aria-current={view === "overview" ? "page" : undefined}>Özet</Link>
        <Link to={peoplePath} aria-current={view === "people" ? "page" : undefined}>İnsan ve ekip</Link>
        <Link to={operationsPath} aria-current={view === "operations" ? "page" : undefined}>Operasyon ve uyum</Link>
      </nav>
      {view === "people" ? <PeopleView /> : view === "operations" ? <OperationsView /> : <Overview />}
    </div>
  );
}
