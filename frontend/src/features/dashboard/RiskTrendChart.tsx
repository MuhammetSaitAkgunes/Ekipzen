import { Line } from "react-chartjs-2";
import { chartToken } from "../shared/chartSetup";

export function RiskTrendChart({ trend }: { trend: number[] }) {
  const labels = trend.map((_, i) => (i === trend.length - 1 ? "Bu hafta" : `H-${trend.length - i}`));
  return (
    <div className="chart-container">
      <Line
        data={{
          labels,
          datasets: [
            {
              label: "İK Risk Skoru",
              data: trend,
              borderColor: chartToken("--primary", "#4338ca"),
              backgroundColor: "rgba(67, 56, 202, 0.12)",
              fill: true,
              borderWidth: 2.5,
              tension: 0.35,
              pointRadius: 3,
              pointBackgroundColor: chartToken("--surface", "#ffffff"),
              pointBorderColor: chartToken("--primary", "#4338ca"),
            },
          ],
        }}
        options={{
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false }, tooltip: { mode: "index", intersect: false } },
          scales: {
            y: { min: 0, max: 100, grid: { color: chartToken("--line-soft", "#f1f5f9") }, ticks: { stepSize: 20 } },
            x: { grid: { display: false } },
          },
        }}
      />
    </div>
  );
}
