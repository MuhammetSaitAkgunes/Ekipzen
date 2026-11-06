import { Line } from "react-chartjs-2";
import { Link } from "react-router-dom";
import { appPath } from "../../paths";
import { chartToken } from "../shared/chartSetup";
import { downloadCsv, tableToCsvLines } from "../shared/csv";
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


export function ManagerPage() {
  const exportReport = () => {
    const table = document.querySelector<HTMLTableElement>(".mini-table");
    if (table) downloadCsv(tableToCsvLines(table), "ornek-departman-kullanim-raporu");
  };
  return <div id="manager-screen">
    <div className="page-header"><div><h2>İzin analizleri</h2><p>Ekip izinlerinin dönemsel görünümü.</p></div><Link className="btn btn-secondary" to={appPath("/inbox")}>Bekleyen onaylara git</Link></div>
    <div className="ez-demo-notice"><strong>Örnek analiz</strong><p>Bu bölümdeki grafik ve tablolar örnek verilerle gösterilir; şirketinizin gerçek izin kayıtlarını temsil etmez.</p></div>
    <div className="dashboard-grid ez-demo-analysis">
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


    </div>
    <button className="btn btn-secondary btn-sm" onClick={exportReport}>Örnek raporu indir</button>
      <div className="table-section">
        <div className="section-head manager-table-head">
          <h3>Departman Bazlı Kullanım Raporu</h3>
          <span className="status-pill info">Demo</span>
        </div>
        <div className="table-scroll" role="region" aria-label="Departman kullanım tablosu" tabIndex={0}>
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

  </div>;
}
