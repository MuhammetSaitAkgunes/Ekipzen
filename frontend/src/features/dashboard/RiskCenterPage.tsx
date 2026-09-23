import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { PageError, PageLoading } from "../shared/PageState";
import { formatToday, getRiskLabel, getRiskLevel, priorityLabel, topPriorityActions } from "./format";
import { RiskTrendChart } from "./RiskTrendChart";
import {
  useComplianceRisk, useDashboardMetrics, useEmployeeVoice, useManagerLoad, useOpenActions,
} from "./queries";

const count = (value?: number | null) => value == null ? "—" : value.toLocaleString("tr-TR");
const percentWidth = (value?: number | null) => Math.max(0, Math.min(100, value ?? 0)) + "%";

export function RiskCenterPage() {
  const { user } = useAuth();
  const metricsQ = useDashboardMetrics();
  const loadQ = useManagerLoad();
  const voiceQ = useEmployeeVoice();
  const complianceQ = useComplianceRisk();
  const actionsQ = useOpenActions();

  const queries = [metricsQ, loadQ, voiceQ, complianceQ, actionsQ];
  if (queries.some((query) => query.isPending)) return <PageLoading />;
  const failed = queries.find((query) => query.isError);
  if (failed) return <PageError error={failed.error} />;

  const metrics = metricsQ.data!;
  const load = loadQ.data!;
  const voice = voiceQ.data!;
  const compliance = complianceQ.data!;
  const actions = actionsQ.data!;
  const riskScore = metrics.riskScore ?? null;
  const trend = metrics.riskTrend ?? [];
  const trendDelta = trend.length > 1 ? trend[trend.length - 1] - trend[0] : null;
  const urgent = actions.filter((action) => action.priority === "high");
  const departments = [...(metrics.departmentRisk ?? [])].sort((a, b) => (b.risk ?? 0) - (a.risk ?? 0));
  const areaCards = [
    {
      title: "Ayrılma riski", value: count(metrics.attritionHigh), unit: "kişi",
      detail: "Yüksek risk grubundaki çalışanları ve nedenleri inceleyin.",
      to: appPath("/risk/attrition"), icon: "fa-person-walking-arrow-right",
    },
    {
      title: "Tükenmişlik", value: count(metrics.burnoutRisk), unit: "sinyal",
      detail: "İş yükü ve çalışan nabzındaki değişimleri görün.",
      to: appPath("/risk/burnout"), icon: "fa-heart-pulse",
    },
    {
      title: "Yönetici yükü", value: count(metrics.managerLoadIndex), unit: "/100",
      detail: count(load.pendingApprovals) + " bekleyen onay · " + count(load.openActions) + " açık iş",
      to: appPath("/risk/manager-load"), icon: "fa-people-group",
    },
    {
      title: "Çalışan nabzı", value: count(voice.pulseScore), unit: "/100",
      detail: "eNPS " + count(voice.eNps) + " · " + count(voice.decliningTeams) + " ekipte düşüş sinyali",
      to: appPath("/risk/employee-voice"), icon: "fa-wave-square",
    },
    {
      title: "Evrak ve uyum", value: count(compliance.missingDocuments), unit: "eksik",
      detail: count(compliance.upcomingDocuments) + " yaklaşan son tarih · Denetim hazırlığı: " + (compliance.auditReadinessRisk || "—"),
      to: appPath("/risk/compliance"), icon: "fa-file-shield",
    },
  ];

  return (
    <div className="ez-risk-page">
      <header className="ez-risk-header">
        <div>
          <span className="ez-overline">ANALİZ / {formatToday()}</span>
          <h1>Risk Merkezi</h1>
          <p>Önce müdahale gerektiren işleri görün; ardından risk sinyallerini inceleyin.</p>
        </div>
        <Link className="btn btn-secondary" to={appPath("/inbox?tab=actions")}>
          <i className="fa-solid fa-inbox" aria-hidden="true" /> İş kutusuna git
        </Link>
      </header>

      <div className="ez-risk-first-row">
        <section className="ez-risk-panel ez-risk-priorities" aria-labelledby="risk-priorities-title">
          <div className="ez-risk-panel-head">
            <div>
              <span className="ez-overline">ŞİMDİ İLGİLENİLMESİ GEREKENLER</span>
              <h2 id="risk-priorities-title">Öncelikli aksiyonlar</h2>
              <p>{urgent.length > 0 ? count(urgent.length) + " yüksek öncelikli iş açık." : "Yüksek öncelikli açık iş yok."}</p>
            </div>
            <Link className="ez-text-link" to={appPath("/inbox?tab=actions")}>Tüm işler <span aria-hidden="true">→</span></Link>
          </div>
          {actions.length === 0 ? (
            <div className="ez-risk-empty"><i className="fa-regular fa-circle-check" aria-hidden="true" /><strong>Şu anda açık aksiyon yok</strong><span>Yeni bir iş oluştuğunda burada görünecek.</span></div>
          ) : (
            <div className="ez-risk-action-list">
              {topPriorityActions(actions, 3).map((action) => (
                <Link className="ez-risk-action-row" key={action.id} to={appPath("/inbox?tab=actions")} aria-label={(action.title || "Aksiyon") + " işini İş Kutusunda aç"}>
                  <span className={"ez-risk-priority " + (action.priority || "low")}>{priorityLabel(action.priority)}</span>
                  <span className="ez-risk-action-copy"><strong>{action.title}</strong><small>{action.source || "Aksiyon"} · {action.owner || "Atanmamış"}{action.due ? " · " + action.due : ""}</small></span>
                  <i className="fa-solid fa-arrow-up-right-from-square" aria-hidden="true" />
                </Link>
              ))}
            </div>
          )}
        </section>

        <section className={"ez-risk-panel ez-risk-score " + (riskScore == null ? "" : getRiskLevel(riskScore))} aria-labelledby="risk-score-title">
          <div className="ez-risk-panel-head"><div><span className="ez-overline">GENEL GÖRÜNÜM</span><h2 id="risk-score-title">İK risk düzeyi</h2></div><i className="fa-solid fa-shield-heart" aria-hidden="true" /></div>
          <div className="ez-risk-score-value">{count(riskScore)}<small>/100</small></div>
          <span className="ez-risk-status">{riskScore == null ? "Veri yok" : getRiskLabel(riskScore)}</span>
          <p>Yetkili olduğunuz çalışanlar için hesaplanan ortalama risk puanı.</p>
          <div className="ez-risk-score-footer"><span>{count(metrics.attritionHigh)} yüksek ayrılma</span><span>{count(metrics.burnoutRisk)} tükenmişlik sinyali</span></div>
        </section>
      </div>

      <section className="ez-risk-section" aria-labelledby="risk-areas-title">
        <div className="ez-risk-section-head"><div><span className="ez-overline">NEREDEN BAŞLAMALI?</span><h2 id="risk-areas-title">Risk alanları</h2><p>Her alanın ayrıntısına ve ilgili çalışanlara buradan ulaşın.</p></div></div>
        <div className="ez-risk-area-grid">
          {areaCards.map((area) => (
            <Link className="ez-risk-area" key={area.title} to={area.to}>
              <span className="ez-risk-area-icon"><i className={"fa-solid " + area.icon} aria-hidden="true" /></span>
              <span className="ez-risk-area-title">{area.title}</span>
              <span className="ez-risk-area-value">{area.value}<small>{area.unit}</small></span>
              <span className="ez-risk-area-detail">{area.detail}</span>
              <span className="ez-risk-area-link">Detayı incele <span aria-hidden="true">→</span></span>
            </Link>
          ))}
        </div>
      </section>

      <div className="ez-risk-evidence-grid">
        <section className="ez-risk-panel" aria-labelledby="department-risk-title">
          <div className="ez-risk-panel-head"><div><span className="ez-overline">EKİP GÖRÜNÜMÜ</span><h2 id="department-risk-title">Departman riskleri</h2><p>En yüksek puanlı departmanlar önce gösterilir.</p></div></div>
          {departments.length === 0 ? <p className="ez-risk-empty-text">Bu kapsamda departman verisi yok.</p> : (
            <div className="ez-risk-departments">
              {departments.map((department) => (
                <div className="ez-risk-department" key={department.departmentId}>
                  <span className={"ez-risk-dept-score " + getRiskLevel(department.risk ?? 0)}>{count(department.risk)}</span>
                  <div><strong>{department.dept}</strong><small>{count(department.employeeCount)} çalışan · {count(department.highAttritionCount)} yüksek ayrılma · {count(department.highBurnoutCount)} tükenmişlik</small></div>
                  <span className="ez-risk-dept-level">{getRiskLabel(department.risk ?? 0)}</span>
                </div>
              ))}
            </div>
          )}
        </section>
        <section className="ez-risk-panel ez-risk-trend" aria-labelledby="risk-trend-title">
          <div className="ez-risk-panel-head"><div><span className="ez-overline">DÖNEMSEL DEĞİŞİM</span><h2 id="risk-trend-title">Risk eğilimi</h2><p>Son {trend.length} dönem için ortalama risk sinyali.</p></div></div>
          {trend.length === 0 ? <p className="ez-risk-empty-text">Henüz karşılaştırılacak dönem verisi yok.</p> : <RiskTrendChart trend={trend} />}
          {trendDelta !== null && <p className="ez-risk-trend-note">İlk döneme göre {trendDelta > 0 ? "+" : ""}{trendDelta} puan {trendDelta > 0 ? "artış" : trendDelta < 0 ? "azalış" : "değişim yok"}.</p>}
        </section>
      </div>

      <section className="ez-risk-panel ez-risk-capacity" aria-labelledby="risk-capacity-title">
        <div className="ez-risk-panel-head"><div><span className="ez-overline">KAPASİTE VE GELİŞİM</span><h2 id="risk-capacity-title">Yetenek göstergeleri</h2><p>İşe alım, beceri ve kritik roller için ek sinyaller.</p></div>
          {user?.role === "hr-admin" && <Link className="ez-text-link" to={appPath("/recruitment")}>İşe alımı aç <span aria-hidden="true">→</span></Link>}
        </div>
        {(metrics.talentCapacity ?? []).length === 0 ? <p className="ez-risk-empty-text">Bu alanda henüz gösterge yok.</p> : (
          <div className="ez-risk-capacity-grid">
            {(metrics.talentCapacity ?? []).map((item) => (
              <div className="ez-risk-capacity-item" key={item.label}>
                <span>{item.label}</span><strong>{count(item.value)}{item.label === "Kritik Rol Riski" ? "" : "%"}</strong>
                <div className="ez-risk-capacity-bar" aria-hidden="true"><span style={{ width: percentWidth(item.label === "Kritik Rol Riski" ? (item.value ?? 0) * 20 : item.value) }} /></div>
                <small>{item.meta}</small>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
