import { Line } from "react-chartjs-2";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { chartToken } from "../shared/chartSetup";
import { downloadCsv, tableToCsvLines } from "../shared/csv";
import { useOverview } from "../overview/queries";
import { useDecideLeave, usePendingLeaves } from "../leaves/queries";

// Demo analitik (backend'de izin-analitik ucu yok; eski mock ile birebir).
const DEMO_TREND = [12, 15, 8, 10, 25, 45, 80, 120, 30, 15, 10, 20];
const DEMO_MONTHS = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];
const DEMO_HEATMAP: [string, string[]][] = [
  ["Yazılım", ["hm-l1", "hm-l2", "hm-l4", "hm-l3", "hm-l1"]],
  ["Satış", ["hm-l2", "hm-l1", "hm-l1", "hm-l4", "hm-l2"]],
  ["İK", ["hm-l1", "hm-l3", "hm-l2", "hm-l1", "hm-l1"]],
];
const DEMO_DEPT_USAGE: [string, string, string, string, number][] = [
  ["Yazılım Ekibi", "42 kişi", "120 gün", "340 gün", 35],
  ["Satış & Pazarlama", "18 kişi", "45 gün", "120 gün", 28],
];

const initialsOf = (name?: string | null): string =>
  (name ?? "")
    .split(" ")
    .filter(Boolean)
    .map((part) => part[0]?.toLocaleUpperCase("tr-TR"))
    .slice(0, 2)
    .join("") || "?";

const formatDateRange = (start?: string, end?: string): string => {
  const fmt = (iso?: string) =>
    iso ? new Date(iso + "T00:00:00").toLocaleDateString("tr-TR", { day: "numeric", month: "long" }) : "";
  return `${fmt(start)} - ${fmt(end)}`;
};

export function ManagerPage() {
  const { showToast } = useToast();
  const overviewQ = useOverview();
  const pendingQ = usePendingLeaves(true);
  const decide = useDecideLeave();

  if (overviewQ.isPending || pendingQ.isPending) return <PageLoading />;
  if (overviewQ.isError) return <PageError error={overviewQ.error} />;
  if (pendingQ.isError) return <PageError error={pendingQ.error} />;

  const overview = overviewQ.data;
  const pending = pendingQ.data;

  const resolve = async (id: number, name: string, approve: boolean) => {
    try {
      await decide.mutateAsync({ id, approve });
      showToast(approve ? `${name} talebi onaylandı.` : `${name} talebi reddedildi.`, approve ? "success" : "info");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "İşlem tamamlanamadı.", "error");
    }
  };

  const exportReport = () => {
    const table = document.querySelector<HTMLTableElement>(".mini-table");
    if (!table) return;
    downloadCsv(tableToCsvLines(table), "departman-kullanim-raporu");
    showToast("Rapor indirildi.", "success");
  };

  return (
    <div id="manager-screen">
      <div className="manager-header page-header">
        <div>
          <h2>Yönetici Konsolu</h2>
          <p>Şirket geneli izin analitikleri, yoğunluk görünümü ve onay işlemleri.</p>
        </div>
        <div className="mh-actions header-actions">
          <button className="btn btn-secondary" onClick={exportReport}>
            <i aria-hidden="true" className="fa-solid fa-cloud-arrow-down" /> Raporu İndir
          </button>
        </div>
      </div>

      <div className="stats-grid-pro">
        <div className="kpi-pro theme-blue">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-solid fa-users-viewfinder" /></div></div>
          <span className="kpi-val">{overview.onLeaveToday ?? 0} kişi</span>
          <span className="kpi-label">Şu An İzinli</span>
        </div>
        <div className="kpi-pro theme-orange">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-regular fa-folder-open" /></div><span className="kpi-trend text-orange">Kritik</span></div>
          <span className="kpi-val">{pending.length} talep</span>
          <span className="kpi-label">Onay Bekliyor</span>
        </div>
        <div className="kpi-pro theme-purple">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-solid fa-briefcase" /></div><span className="kpi-trend">Demo</span></div>
          <span className="kpi-val">142 gün</span>
          <span className="kpi-label">Planlanan İzin</span>
        </div>
        <div className="kpi-pro theme-green">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-solid fa-wallet" /></div><span className="kpi-trend">Demo</span></div>
          <span className="kpi-val">%42</span>
          <span className="kpi-label">İzin Kullanım Oranı</span>
        </div>
      </div>

      <div className="dashboard-grid">
        <div className="chart-section card">
          <div className="section-head">
            <div>
              <h3>İzin Kullanım Trendleri</h3>
              <span>Departman ve ay bazlı görünüm (demo)</span>
            </div>
            <span className="status-pill info">Demo</span>
          </div>
          <div className="chart-container manager-chart">
            <Line
              data={{
                labels: DEMO_MONTHS,
                datasets: [{
                  label: "Toplam izin günü",
                  data: DEMO_TREND,
                  borderColor: chartToken("--primary", "#4338ca"),
                  borderWidth: 2.5,
                  backgroundColor: "rgba(67, 56, 202, 0.12)",
                  fill: true,
                  tension: 0.35,
                  pointBackgroundColor: chartToken("--surface", "#ffffff"),
                  pointBorderColor: chartToken("--primary", "#4338ca"),
                  pointRadius: 4,
                }],
              }}
              options={{
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false }, tooltip: { mode: "index", intersect: false } },
                scales: {
                  y: { grid: { color: chartToken("--line-soft", "#f1f5f9") }, beginAtZero: true },
                  x: { grid: { display: false } },
                },
              }}
            />
          </div>

          <div className="section-head heat-title">
            <h3>Departman Yoğunluk Haritası</h3>
          </div>
          <div className="heatmap-container">
            {DEMO_HEATMAP.map(([label, cells]) => (
              <div key={label} className="heatmap-row">
                <span className="hm-label">{label}</span>
                <div className="hm-bars">
                  {cells.map((cell, index) => <div key={index} className={`hm-cell ${cell}`} />)}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="approval-panel card">
          <div className="panel-head">
            <h3>Onay Bekleyenler</h3>
            <span className="status-pill pending">{pending.length ? `${pending.length} bekleyen` : "Tamamlandı"}</span>
          </div>
          <div className="req-list">
            {pending.map((request) => (
              <div key={request.id} className="req-item">
                <div className="req-user">
                  <div className="req-avatar">{initialsOf(request.employeeName)}</div>
                  <div className="req-meta"><h4>{request.employeeName}</h4><span>{request.description || "İzin talebi"}</span></div>
                </div>
                <div className="req-details">
                  <div><span>İzin Türü</span><strong>{request.leaveTypeName}</strong></div>
                  <div><span>Süre</span><strong>{request.days} gün</strong></div>
                  <div><span>Tarihler</span><strong>{formatDateRange(request.startDate, request.endDate)}</strong></div>
                </div>
                <div className="req-actions">
                  <button className="btn btn-secondary btn-sm" disabled={decide.isPending} onClick={() => resolve(request.id!, request.employeeName ?? "Talep", false)}>Reddet</button>
                  <button className="btn btn-primary btn-sm" disabled={decide.isPending} onClick={() => resolve(request.id!, request.employeeName ?? "Talep", true)}>Onayla</button>
                </div>
              </div>
            ))}
            {pending.length === 0 && <p className="pending-desc">Bekleyen talep yok.</p>}
          </div>
        </div>
      </div>

      <div className="table-section">
        <div className="section-head manager-table-head">
          <h3>Departman Bazlı Kullanım Raporu</h3>
          <span className="status-pill info">Demo</span>
        </div>
        <table className="mini-table">
          <thead><tr><th>Departman</th><th>Toplam Personel</th><th>Kullanılan İzin</th><th>Kalan Hak</th><th>Doluluk</th></tr></thead>
          <tbody>
            {DEMO_DEPT_USAGE.map(([dept, staff, used, remaining, fill]) => (
              <tr key={dept}>
                <td><strong>{dept}</strong></td>
                <td>{staff}</td>
                <td>{used}</td>
                <td>{remaining}</td>
                <td><div className="progress-mini"><div className="p-fill" style={{ width: `${fill}%` }} /></div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
