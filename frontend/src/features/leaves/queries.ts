import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { dailyTasksKey } from "../guidance/queries";
import { leaveApprovalJourneyKey } from "../guidance/journeyQueries";
import type { components } from "../../api/schema";
import type { GuidanceOperationHistory } from "../guidance/operationQueries";

export type LeaveBalanceDto = components["schemas"]["LeaveBalanceDto"];
export type LeaveLedgerEntryDto = components["schemas"]["LeaveLedgerEntryDto"];
export type LeaveRequestDto = components["schemas"]["LeaveRequestDto"];
export type LeaveTypeDto = components["schemas"]["LeaveTypeDto"];
export type TeamLeaveDto = components["schemas"]["TeamLeaveDto"];
export type CreateLeaveRequestCommand = components["schemas"]["CreateLeaveRequestCommand"];
type EmployeePagedResult = components["schemas"]["EmployeeListItemDtoPagedResult"];

export type LeaveRequestPreviewDto = {
  workingDays: number;
  deductsFromAnnualBalance: boolean;
  requiresApproval: boolean;
  canSubmit: boolean;
  blockReason: string | null;
  previewedAtUtc: string;
  recordToken?: string | null;
};

export type LeaveCancellationPreview = { id: number; leaveTypeName: string; startDate: string; endDate: string;
  days: number; status: string; canCancel: boolean; releasedReservationDays: number; refundedDays: number; recordToken: string };
export const usePreviewLeaveCancellation = () => useMutation({
  mutationFn: (id: number) => apiFetch<LeaveCancellationPreview>(`/leaves/${id}/cancellation-preview`),
});
export const useLeaveLearningOperations = (userId?: string | null) => useQuery({
  queryKey: ["leaves", "learning-operations", userId], enabled: !!userId,
  queryFn: () => apiFetch<GuidanceOperationHistory[]>("/leaves/learning-operations"),
});

type PreviewLeaveRequest = Pick<CreateLeaveRequestCommand, "leaveTypeId" | "startDate" | "endDate" | "substituteEmployeeId">;

export const usePreviewLeave = () => useMutation({
  mutationFn: (request: PreviewLeaveRequest) =>
    apiFetch<LeaveRequestPreviewDto>("/leaves/preview", { method: "POST", body: JSON.stringify(request) }),
});

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
      queryClient.invalidateQueries({ queryKey: ["guidance", "operations"] });
    },
  });
};

export const useCancelLeave = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, expectedRecordToken, guidanceContentRevisionId }: { id: number; expectedRecordToken: string; guidanceContentRevisionId?: number }) => {
      const query = new URLSearchParams({ expectedRecordToken });
      if (guidanceContentRevisionId !== undefined) query.set("guidanceContentRevisionId", String(guidanceContentRevisionId));
      return apiFetch<null>(`/leaves/${id}/cancel?${query}`, { method: "POST" });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leaves"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
      queryClient.invalidateQueries({ queryKey: ["actions"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
      queryClient.invalidateQueries({ queryKey: ["guidance", "operations"] });
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
    mutationFn: ({ id, approve, note, guidanceContentRevisionId }: { id: number; approve: boolean; note?: string; guidanceContentRevisionId?: number }) =>
      apiFetch<LeaveRequestDto>(`/leaves/${id}/${approve ? "approve" : "reject"}`, {
        method: "POST",
        body: JSON.stringify({ note: note?.trim() || null, ...(guidanceContentRevisionId !== undefined ? { guidanceContentRevisionId } : {}) }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["leaves"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
      queryClient.invalidateQueries({ queryKey: ["actions"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
      queryClient.invalidateQueries({ queryKey: leaveApprovalJourneyKey });
      queryClient.invalidateQueries({ queryKey: ["guidance", "operations"] });
    },
  });
};
