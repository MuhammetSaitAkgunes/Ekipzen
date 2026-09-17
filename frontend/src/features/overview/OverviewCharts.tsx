import { Bar, Doughnut } from "react-chartjs-2";
import { chartToken } from "../shared/chartSetup";
import type { components } from "../../api/schema";

type DepartmentCountDto = components["schemas"]["DepartmentCountDto"];
type RecruitmentFunnelSliceDto = components["schemas"]["RecruitmentFunnelSliceDto"];

// Kimlik renk sırası: indigo, sky, amber, emerald, slate (chartToken modül yüklenirken
// çalışmadığı için sabit hex kullanılır; değerler tokens.css ramp'leriyle birebir).
const DEPT_COLORS = ["#4338ca", "#0284c7", "#d97706", "#059669", "#64748b"];

export function DeptDistributionChart({ distribution }: { distribution: DepartmentCountDto[] }) {
  return (
    <div className="chart-container">
      <Doughnut
        data={{
          labels: distribution.map((d) => d.dept ?? ""),
          datasets: [
            {
              data: distribution.map((d) => d.count ?? 0),
              backgroundColor: distribution.map((_, i) => DEPT_COLORS[i % DEPT_COLORS.length]),
              borderWidth: 0,
            },
          ],
        }}
        options={{
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { position: "right", labels: { usePointStyle: true, boxWidth: 8 } } },
        }}
      />
    </div>
  );
}

export function RecruitmentFunnelChart({ funnel }: { funnel: RecruitmentFunnelSliceDto }) {
  return (
    <div className="chart-container">
      <Bar
        data={{
          labels: ["Başvuru", "Ön Görüşme", "Mülakat", "Teklif", "İşe Giriş"],
          datasets: [
            {
              label: "Aday",
              data: [funnel.total ?? 0, funnel.new ?? 0, funnel.interview ?? 0, funnel.offer ?? 0, funnel.hired ?? 0],
              backgroundColor: chartToken("--primary", "#4338ca"),
              borderRadius: 5,
            },
          ],
        }}
        options={{
          responsive: true,
          maintainAspectRatio: false,
          scales: {
            y: { beginAtZero: true, grid: { color: chartToken("--line-soft", "#f1f5f9") } },
            x: { grid: { display: false } },
          },
          plugins: { legend: { display: false } },
        }}
      />
    </div>
  );
}
