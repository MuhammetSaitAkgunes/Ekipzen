export const formatTimeAgo = (iso?: string | null): string => {
  if (!iso) return "-";
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return "-";
  const hours = Math.floor((Date.now() - then) / 3_600_000);
  if (hours < 1) return "Az önce";
  if (hours < 24) return `${hours}s önce`;
  return `${Math.floor(hours / 24)}g önce`;
};

// Eski CSS: .status-tag.yeni/.mülakat/.teklif/.red; "hired" Task 3'te eklenir.
export const statusTagClass = (status?: string | null): string =>
  status === "İşe Alındı" ? "hired" : (status ?? "").toLocaleLowerCase("tr-TR");

export const formatCandidateScore = (score?: number | null): string =>
  score == null ? "Henüz puan verilmedi" : `${score}/100 puan`;

export const scoreClass = (score?: number | null): "high" | "mid" =>
  (score ?? 0) > 80 ? "high" : "mid";

export const PIPELINE_STATUSES = ["Yeni", "Mülakat", "Teklif", "Red"];
export const NOTE_TYPES = ["Teknik Mülakat", "İK Görüşmesi"];
