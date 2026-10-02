import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { guidanceOperationsKey } from "../guidance/operationQueries";
import { dailyTasksKey } from "../guidance/queries";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type GlobalActionDto = components["schemas"]["GlobalActionDto"];
export type AuditLogDto = components["schemas"]["AuditLogDto"];
export type CreateGlobalActionCommand = components["schemas"]["CreateGlobalActionCommand"];

export type ActionFilters = { priority: string; source: string; owner: string };

const actionsPath = (filters: ActionFilters): string => {
  const params = new URLSearchParams();
  if (filters.priority) params.set("priority", filters.priority);
  if (filters.source) params.set("source", filters.source);
  if (filters.owner) params.set("owner", filters.owner);
  const query = params.toString();
  return query ? `/actions?${query}` : "/actions";
};

export const useGlobalActions = (filters: ActionFilters) =>
  useQuery({
    queryKey: ["actions", "list", filters],
    queryFn: () => apiFetch<GlobalActionDto[]>(actionsPath(filters)),
  });

export const useAuditLogs = (enabled: boolean) =>
  useQuery({
    queryKey: ["actions", "audit"],
    queryFn: () => apiFetch<AuditLogDto[]>("/audit-logs"),
    enabled,
  });

export const useCreateGlobalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateGlobalActionCommand) =>
      apiFetch<GlobalActionDto>("/actions", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["actions"] }), queryClient.invalidateQueries({ queryKey: dailyTasksKey }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
};

export const useSetActionStatus = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status, expectedRevision, guidanceContentRevisionId }: { id: number; status: string; expectedRevision?: string | null; guidanceContentRevisionId?: number | null }) =>
      apiFetch<GlobalActionDto>(`/actions/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status, expectedRevision, guidanceContentRevisionId }),
      }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["actions"] }), queryClient.invalidateQueries({ queryKey: dailyTasksKey }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
};

export const useDeleteGlobalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: number | { id: number; expectedRevision?: string | null; guidanceContentRevisionId?: number | null }) => {
      const args = typeof input === "number" ? { id: input } : input;
      const params = new URLSearchParams();
      if (args.expectedRevision) params.set("expectedRevision", args.expectedRevision);
      if (args.guidanceContentRevisionId) params.set("guidanceContentRevisionId", String(args.guidanceContentRevisionId));
      return apiFetch<null>(`/actions/${args.id}${params.size ? "?" + params : ""}`, { method: "DELETE" });
    },
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["actions"] }), queryClient.invalidateQueries({ queryKey: dailyTasksKey }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
};

export const useUpdateGlobalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: components["schemas"]["ActionUpdateBody"] & { id: number }) =>
      apiFetch<GlobalActionDto>(`/actions/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["actions"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey })]),
  });
};

export async function readCurrentAction(id: number) {
  const list = await apiFetch<GlobalActionDto[]>("/actions");
  const current = list.find(item => item.id === id);
  if (!current) throw new Error("Aksiyon artık listede yok. Taslağınız korunuyor; kaynak kaydı kontrol edin.");
  return current;
}
