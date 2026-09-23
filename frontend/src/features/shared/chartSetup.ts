import { Chart, registerables } from "chart.js";

Chart.register(...registerables);

/** Grafik renkleri CSS token'larından okunur (eski chartToken paritesi). */
export const chartToken = (name: string, fallback: string): string =>
  getComputedStyle(document.querySelector(".ez-workspace") ?? document.documentElement).getPropertyValue(name).trim() || fallback;
