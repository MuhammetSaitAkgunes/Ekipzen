import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
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
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["actions"] }), queryClient.invalidateQueries({ queryKey: dailyTasksKey })]),
  });
};

export const useSetActionStatus = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiFetch<GlobalActionDto>(`/actions/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status }),
      }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["actions"] }), queryClient.invalidateQueries({ queryKey: dailyTasksKey })]),
  });
};

export const useDeleteGlobalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiFetch<null>(`/actions/${id}`, { method: "DELETE" }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["actions"] }), queryClient.invalidateQueries({ queryKey: dailyTasksKey })]),
  });
};
