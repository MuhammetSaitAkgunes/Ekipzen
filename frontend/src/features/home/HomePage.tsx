import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";
import { useOverview } from "../overview/queries";
import { useLeaveBalance, useMyLeaves, usePendingLeaves, useTeamLeaves } from "../leaves/queries";
import { useGlobalActions } from "../actions/queries";
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
  return <div>{actions.slice(0, 3).map((action) => <Link className="ez-action-row" key={action.id} to={appPath("/inbox?tab=actions")}><span className={`ez-priority-dot ${action.priority}`} /><div><strong>{action.title}</strong><small>{action.source} {action.due ? `· ${action.due}` : ""}</small></div><span aria-hidden="true">↗</span></Link>)}
    {actions.length === 0 && <div className="ez-empty"><h3>İyi bir başlangıç</h3><p>Şu anda açık bir aksiyonunuz yok.</p></div>}
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
  return <div className="ez-home">
    <div className="ez-home-heading"><div><span className="ez-overline">{new Date().toLocaleDateString("tr-TR", { weekday: "long", day: "numeric", month: "long" })}</span><h1>Merhaba, {user?.name?.split(" ")[0] || "hoş geldiniz"}<span className="ez-greeting-dot">.</span></h1><p>{management ? "Ekibinizin günü burada başlıyor. Önce önemli işlere bakalım." : "Gününüzü planlayın, size ait her şeye kolayca ulaşın."}</p></div><Link className="btn btn-primary" to={appPath(admin ? "/personnel/new" : "/leaves?new=1")}><i className="fa-solid fa-plus" aria-hidden="true" />{admin ? "Çalışan ekle" : "İzin talebi oluştur"}</Link></div>
    {management ? <ManagementMetrics /> : <PersonalMetrics />}
    <div className="ez-home-grid">
      <div className="ez-home-main">
        {management && <section className="ez-panel"><div className="ez-panel-heading"><div><span className="ez-overline">GERÇEK KAYITLARDAN</span><h2>Bugün yapılacaklar</h2><p>Her işin neden göründüğünü ve sonraki adımı inceleyin.</p></div><Link className="ez-text-link" to={appPath("/inbox?tab=daily")}>Tümünü gör <span aria-hidden="true">→</span></Link></div><DailyGuidanceTasks limit={4} /></section>}
        {!management && <section className="ez-panel"><div className="ez-panel-heading"><div><span className="ez-overline">ÖNCE BUNLARA BAKALIM</span><h2>İzin taleplerim</h2></div></div><MyRequests /></section>}
        <section className="ez-panel"><div className="ez-panel-heading"><div><h2>Öncelikli işler</h2><p>Bugünü kolaylaştıracak küçük adımlar.</p></div><Link className="ez-text-link" to={appPath("/inbox?tab=actions")}>Tümünü gör <span aria-hidden="true">→</span></Link></div><PriorityActions /></section>
      </div>
      <aside className="ez-home-side">
        <section className="ez-panel ez-quick-panel"><div className="ez-panel-heading"><h2>Hızlı erişim</h2><i className="fa-solid fa-arrow-up-right-from-square" aria-hidden="true" /></div>
          <div className="ez-quick-links">
            {management && <Link to={appPath("/personnel")}><i className="fa-solid fa-user-group" aria-hidden="true" />Çalışanları bul<span aria-hidden="true">→</span></Link>}
            <Link to={appPath("/leaves?new=1")}><i className="fa-regular fa-calendar-plus" aria-hidden="true" />İzin talebi oluştur<span aria-hidden="true">→</span></Link>
            {user?.role !== "manager" && <Link to={appPath("/payroll")}><i className="fa-solid fa-wallet" aria-hidden="true" />{admin ? "Bordro dönemleri" : "Bordrolarım"}<span aria-hidden="true">→</span></Link>}
            {management && <Link to={appPath("/overview")}><i className="fa-solid fa-chart-pie" aria-hidden="true" />Şirket özeti<span aria-hidden="true">→</span></Link>}
          </div>
        </section>
        <section className="ez-panel"><div className="ez-panel-heading"><h2>Bugün kimler izinli?</h2><span className="ez-soft-icon"><i className="fa-solid fa-sun" aria-hidden="true" /></span></div><AwayList /></section>
        <p className="ez-home-note"><i className="fa-regular fa-circle-check" aria-hidden="true" />Küçük adımlar, daha iyi bir ekip deneyimi.</p>
      </aside>
    </div>
  </div>;
}
