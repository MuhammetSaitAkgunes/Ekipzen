import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { Role } from "../../routes";

export const processTitles: Record<string, string> = {
  company: "Şirket hazırlığı", recruitment: "İşe alım", "personnel-entry": "İşe giriş",
  personnel: "Personel yönetimi", leave: "İzin ve onay", attendance: "Mesai ve puantaj",
  payroll: "Bordro", compliance: "Uyum ve belgeler", offboarding: "İşten ayrılış", daily: "Günlük yönetim",
};
export const sourceTitles: Record<string, string> = {
  legislation: "Mevzuat", "company-policy": "Şirket politikası örneği", "good-practice": "İyi uygulama",
};
export type GuidanceContentBody = {
  title: string; audience: Role[]; learningGoal: string; prerequisites: string; action: string; why: string;
  example: string; counterExample: string; terms: string; outcome: string; completionEvidence: string;
  nextStep: string; correction: string; expertEscalation: string; sourceKind: string; sourceReference: string;
  provision: string; applicability: string; effectiveFrom: string; effectiveTo: string | null;
  stepKey?: string | null;
  practice?: { question: string; choices: string[]; correctChoice: number; explanation: string } | null;
};
export type GuidanceContent = {
  id: number; contentKey: string; processKey: string; contentVersion: number;
  state: "draft" | "in-review" | "published" | "retired"; body: GuidanceContentBody;
  expertName: string | null; expertRole: string | null; reviewedOn: string | null; reviewDueOn: string | null;
  publishedAtUtc: string | null; isCurrent: boolean;
};
export type GuidanceEditorial = {
  content: GuidanceContent; revision: number; basedOnId: number | null;
  contributors: { userId: string; name: string }[]; expertReference: string | null; canPublish: boolean;
  events: { revision: number; action: string; actorName: string; reason: string | null; atUtc: string }[];
};
export const useGuidanceCatalog = (role?: string | null) => useQuery({
  queryKey: ["guidance", "catalog", role], queryFn: () => apiFetch<GuidanceContent[]>("/guidance/catalog"),
});
export type GuidanceContentReading = {
  contentRevisionId: number; contentKey: string; processKey: string; contentVersion: number;
  title: string; firstReadAtUtc: string; isCurrent: boolean;
};
export const useGuidanceContentReadings = (userId?: string | null) => useQuery({
  queryKey: ["guidance", "catalog-readings", userId],
  queryFn: () => apiFetch<GuidanceContentReading[]>("/guidance/catalog/readings"),
});
export const useRecordGuidanceReading = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => apiFetch<GuidanceContentReading>(`/guidance/catalog/versions/${id}/readings`,
      { method: "POST", body: JSON.stringify({ confirmed: true }) }),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["guidance", "catalog-readings"] }); },
  });
};
export const useGuidanceCatalogVersion = (id: number | null, role?: string | null) => useQuery({
  queryKey: ["guidance", "catalog-version", role, id], enabled: id !== null,
  queryFn: () => apiFetch<GuidanceContent>(`/guidance/catalog/versions/${id}`),
});
export const useGuidanceEditorialAccess = (userId?: string | null) => useQuery({
  queryKey: ["guidance", "catalog-access", userId],
  queryFn: () => apiFetch<{ canManage: boolean }>("/guidance/catalog/access"),
});
export const useGuidanceEditorial = (enabled: boolean, userId?: string | null) => useQuery({
  queryKey: ["guidance", "catalog-editorial", userId], enabled,
  queryFn: () => apiFetch<GuidanceEditorial[]>("/guidance/catalog/editorial"),
});
export const useSaveGuidanceContent = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ path, method, body }: { path: string; method: "POST" | "PUT"; body: unknown }) =>
      apiFetch<GuidanceEditorial>("/guidance/catalog/editorial" + path, { method, body: JSON.stringify(body) }),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["guidance"] }); },
  });
};
