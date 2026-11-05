import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { useOverview } from "../overview/queries";
import { useLeaveBalance, useMyLeaves, usePendingLeaves, useTeamLeaves } from "../leaves/queries";
import { useGlobalActions } from "../actions/queries";
import { GuidanceDrawer } from "../guidance/GuidanceDrawer";
import { GuidanceStepLessons } from "../guidance/GuidanceStepLessons";
import { GuidanceSourceLink } from "../guidance/GuidanceSourceLink";
import { DailyGuidanceTasks } from "../guidance/DailyGuidanceTasks";
import { PageError, PageLoading } from "../shared/PageState";
import { formatLeaveDate } from "../leaves/format";

const value = (v?: number | null) => v == null ? "—" : v.toLocaleString("tr-TR");
function Metric({ label, amount, icon, hint }: { label: string; amount: string; icon: string; hint: string }) {
  return <div className="ez-metric"><div><span>{label}</span><i className={`fa-solid ${icon}`} aria-hidden="true" /></div><strong>{amount}</strong><small>{hint}</small></div>;
}
function ManagementMetrics() {
  const query = useOverview();
  const pending = usePendingLeaves(true);
  if (query.isPending || pending.isPending) return <PageLoading />;
  if (query.isError || pending.isError) return <PageError error={query.error ?? pending.error} />;
  const d = query.data;
  return <div className="ez-metrics">
    <Metric label="Çalışanlar" amount={value(d.activeEmployees)} icon="fa-user-group" hint="Aktif ekip üyeleri" />
    <Metric label="Bekleyen onaylar" amount={value(pending.data.length)} icon="fa-inbox" hint="İlginizi bekliyor" />
    <Metric label="Bugün izinli" amount={value(d.onLeaveToday)} icon="fa-calendar-days" hint="Günün ekip görünümü" />
    <Metric label="Ofiste" amount={value(d.inOfficeToday)} icon="fa-building" hint="Bugünkü çalışma durumu" />
  </div>;
}
function PersonalMetrics() {
  const balance = useLeaveBalance(); const requests = useMyLeaves();
  if (balance.isPending || requests.isPending) return <PageLoading />;
  if (balance.isError || requests.isError) return <PageError error={balance.error ?? requests.error} />;
  return <div className="ez-metrics ez-metrics-personal">
    <Metric label="Kalan yıllık izin" amount={value(balance.data.remainingDays)} icon="fa-sun" hint="Kullanabileceğiniz gün" />
    <Metric label="Bekleyen talepler" amount={value(requests.data.filter((r) => r.status === "pending").length)} icon="fa-hourglass-half" hint="Onay sürecindeki izinleriniz" />
    <Metric label="Kullanılan izin" amount={value(balance.data.usedDays)} icon="fa-calendar-check" hint="Toplam kullanılan gün" />
  </div>;
}
function AwayList() {
  const query = useTeamLeaves();
  if (query.isPending) return <PageLoading />;
  if (query.isError) return <PageError error={query.error} />;
  const today = new Date().toLocaleDateString("sv-SE");
  const people = query.data.filter((p) => p.startDate && p.endDate && p.startDate <= today && p.endDate >= today);
  return <div className="ez-away-list">{people.slice(0, 5).map((person) => <div className="ez-away-person" key={person.employeeId}><span className="ez-person-avatar">{person.initials}</span><div><strong>{person.employeeName}</strong><small>{person.leaveTypeName} · {formatLeaveDate(person.endDate)} tarihine kadar</small></div></div>)}
    {people.length === 0 && <p className="ez-quiet">Bugün kayıtlı izinli ekip üyesi yok.</p>}
    <Link to={appPath("/leaves?tab=team")} className="ez-section-link">Ekip izinlerini gör <span aria-hidden="true">→</span></Link>
  </div>;
}
function PriorityActions() {
  const query = useGlobalActions({ priority: "", source: "", owner: "" });
  if (query.isPending) return <PageLoading />;
  if (query.isError) return <PageError error={query.error} />;
  const rank: Record<string, number> = { high: 0, medium: 1, low: 2 };
  const actions = query.data.filter((a) => a.status !== "done").sort((a, b) => (rank[a.priority ?? "low"] ?? 2) - (rank[b.priority ?? "low"] ?? 2));
  return <div>{actions.slice(0, 3).map((action) => <Link className="ez-action-row" key={action.id} to={appPath(`/inbox?tab=actions&actionId=${action.id}`)}><span className={`ez-priority-dot ${action.priority}`} /><div><strong>{action.title}</strong><small>{action.source} {action.due ? `· ${action.due}` : ""}</small></div><span aria-hidden="true">↗</span></Link>)}
    {actions.length === 0 && <p className="ez-quiet">Bekleyen işiniz yok.</p>}
  </div>;
}
function MyRequests() {
  const query = useMyLeaves();
  if (query.isPending) return <PageLoading />;
  if (query.isError) return <PageError error={query.error} />;
  const labels: Record<string, string> = { pending: "Onay bekliyor", approved: "Onaylandı", rejected: "Reddedildi", cancelled: "İptal edildi" };
  return <div>{query.data.slice(0, 4).map((r) => <div className="ez-action-row" key={r.id}><div><strong>{r.leaveTypeName}</strong><small>{formatLeaveDate(r.startDate)} – {formatLeaveDate(r.endDate)}</small></div><span className={`status-pill ${r.status}`}>{labels[r.status ?? ""] ?? r.status}</span></div>)}
    {query.data.length === 0 && <div className="ez-empty"><h3>Henüz bir talebiniz yok</h3><p>Bir sonraki molanızı İzinlerim bölümünden planlayabilirsiniz.</p></div>}
    <Link className="ez-section-link" to={appPath("/leaves")}>Tüm taleplerim <span aria-hidden="true">→</span></Link>
  </div>;
}
export function HomePage() {
  const { user } = useAuth();
  const management = user?.role === "hr-admin" || user?.role === "manager";
  const admin = user?.role === "hr-admin";
  return <div className="ez-home ez-home-focused">
    <div className="ez-home-heading">
      <div><span className="ez-overline">{new Date().toLocaleDateString("tr-TR", { weekday: "long", day: "numeric", month: "long" })}</span><h1>Bekleyen işler</h1></div>
      <GuidanceDrawer title="Günlük işler rehberi"><GuidanceSourceLink processKey="daily" /><GuidanceStepLessons process="daily" /></GuidanceDrawer>
    </div>
    <section className="ez-panel ez-pending-panel" aria-label="Bekleyen işler">
      {management ? <DailyGuidanceTasks limit={5} compact /> : <PriorityActions />}
      <Link className="ez-section-link" to={appPath(management ? "/inbox?tab=daily" : "/inbox?tab=actions")}>Tümünü gör <span aria-hidden="true">→</span></Link>
    </section>
    <nav className="ez-home-shortcuts" aria-label="Sık kullanılan işlemler">
      {admin && <Link to={appPath("/personnel/new")}><i className="fa-solid fa-user-plus" aria-hidden="true" />Çalışan ekle<span aria-hidden="true">↗</span></Link>}
      {admin && <Link to={appPath("/attendance")}><i className="fa-solid fa-clock" aria-hidden="true" />Puantajı aç<span aria-hidden="true">↗</span></Link>}
      {!admin && <Link to={appPath("/leaves?new=1")}><i className="fa-regular fa-calendar-plus" aria-hidden="true" />İzin talebi oluştur<span aria-hidden="true">↗</span></Link>}
      {user?.role === "manager" ? <Link to={appPath("/personnel")}><i className="fa-solid fa-user-group" aria-hidden="true" />Çalışanları bul<span aria-hidden="true">↗</span></Link> : <Link to={appPath("/payroll")}><i className="fa-solid fa-wallet" aria-hidden="true" />{admin ? "Bordro dönemleri" : "Bordrolarım"}<span aria-hidden="true">↗</span></Link>}
    </nav>
    <div className="ez-home-secondary">
      {management ? <ManagementMetrics /> : <PersonalMetrics />}
      <div className="ez-home-grid">
        <section className="ez-panel"><div className="ez-panel-heading"><h2>Bugün izinli</h2></div><AwayList /></section>
        {!management && <section className="ez-panel"><div className="ez-panel-heading"><h2>İzin taleplerim</h2></div><MyRequests /></section>}
      </div>
    </div>
  </div>;
}
