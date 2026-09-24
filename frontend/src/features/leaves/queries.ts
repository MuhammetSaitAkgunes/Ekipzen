import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { dailyTasksKey } from "../guidance/queries";
import type { components } from "../../api/schema";

export type LeaveBalanceDto = components["schemas"]["LeaveBalanceDto"];
export type LeaveLedgerEntryDto = components["schemas"]["LeaveLedgerEntryDto"];
export type LeaveRequestDto = components["schemas"]["LeaveRequestDto"];
export type LeaveTypeDto = components["schemas"]["LeaveTypeDto"];
export type TeamLeaveDto = components["schemas"]["TeamLeaveDto"];
export type CreateLeaveRequestCommand = components["schemas"]["CreateLeaveRequestCommand"];
type EmployeePagedResult = components["schemas"]["EmployeeListItemDtoPagedResult"];

export const useLeaveBalance = () =>
  useQuery({ queryKey: ["leaves", "balance"], queryFn: () => apiFetch<LeaveBalanceDto>("/leaves/balance") });

export const useLeaveLedger = () =>
  useQuery({ queryKey: ["leaves", "ledger"], queryFn: () => apiFetch<LeaveLedgerEntryDto[]>("/leaves/ledger") });

export const useMyLeaves = () =>
  useQuery({ queryKey: ["leaves", "my"], queryFn: () => apiFetch<LeaveRequestDto[]>("/leaves/my") });

export const useLeaveTypes = () =>
  useQuery({ queryKey: ["leaves", "types"], queryFn: () => apiFetch<LeaveTypeDto[]>("/leaves/types") });

export const useTeamLeaves = () =>
  useQuery({ queryKey: ["leaves", "team"], queryFn: () => apiFetch<TeamLeaveDto[]>("/leaves/team") });

export const useSubstituteOptions = (enabled: boolean) =>
  useQuery({
    queryKey: ["employees", "options"],
    queryFn: () => apiFetch<EmployeePagedResult>("/employees?status=active&pageSize=50"),
    select: (r) => r.items ?? [],
    enabled,
  });

export const useCreateLeave = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateLeaveRequestCommand) =>
      apiFetch<LeaveRequestDto>("/leaves", { method: "POST", body: JSON.stringify(command) }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leaves"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
      queryClient.invalidateQueries({ queryKey: ["actions"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
    },
  });
};

export const useCancelLeave = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) => apiFetch<null>(`/leaves/${id}/cancel`, { method: "POST" }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leaves"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
      queryClient.invalidateQueries({ queryKey: ["actions"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
    },
  });
};

export const usePendingLeaves = (enabled: boolean) =>
  useQuery({
    queryKey: ["leaves", "pending"],
    queryFn: () => apiFetch<LeaveRequestDto[]>("/leaves/pending"),
    enabled,
  });

export const useDecideLeave = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, approve, note }: { id: number; approve: boolean; note?: string }) =>
      apiFetch<LeaveRequestDto>(`/leaves/${id}/${approve ? "approve" : "reject"}`, {
        method: "POST",
        body: JSON.stringify({ note: note?.trim() || null }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leaves"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
      queryClient.invalidateQueries({ queryKey: ["actions"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
    },
  });
};
