import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";

export type GuidanceHelpPreference = { processKey: string; mode: "detailed" | "compact"; revision: number };
export const useGuidanceHelpPreferences = (userId?: string | null) => useQuery({
  queryKey: ["guidance", "help-preferences", userId], enabled: !!userId,
  queryFn: () => apiFetch<GuidanceHelpPreference[]>("/guidance/help-preferences"),
});
export function useSaveGuidanceHelpPreference(userId?: string | null) {
  const cache = useQueryClient();
  return useMutation({
    mutationFn: ({ processKey, mode, expectedRevision }: { processKey: string; mode: GuidanceHelpPreference["mode"]; expectedRevision: number }) =>
      apiFetch<GuidanceHelpPreference>(`/guidance/help-preferences/${processKey}`, { method: "PUT", body: JSON.stringify({ mode, expectedRevision }) }),
    onSuccess: value => cache.setQueryData<GuidanceHelpPreference[]>(["guidance", "help-preferences", userId], old =>
      [...(old ?? []).filter(item => item.processKey !== value.processKey), value]),
  });
}
