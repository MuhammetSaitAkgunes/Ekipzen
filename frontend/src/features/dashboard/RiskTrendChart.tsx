import { Line } from "react-chartjs-2";
import { chartToken } from "../shared/chartSetup";

export function RiskTrendChart({ trend }: { trend: number[] }) {
  const labels = trend.map((_, i) => (i === trend.length - 1 ? "Son dönem" : "D-" + (trend.length - i)));
  const summary = trend.length > 1 ? "Dönemsel risk sinyali " + trend[0] + " puandan " + trend[trend.length - 1] + " puana değişti." : "Dönemsel risk sinyali: " + (trend[0] ?? "veri yok") + " puan.";
  return (
    <div className="chart-container" role="img" aria-label={summary}>
      <Line
        data={{
          labels,
          datasets: [
            {
              label: "Dönemsel risk sinyali",
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
            y: { min: 0, suggestedMax: Math.min(100, Math.max(...trend) + 10), grid: { color: chartToken("--line-soft", "#f1f5f9") }, ticks: { color: chartToken("--muted", "#64748b") } },
            x: { grid: { display: false }, ticks: { color: chartToken("--muted", "#64748b"), autoSkip: true, maxTicksLimit: 6, maxRotation: 0 } },
          },
        }}
      />
    </div>
  );
}
