import { guidanceOperationsKey } from "../guidance/operationQueries";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, apiFetch } from "../../api/client";
import { dailyTasksKey } from "../guidance/queries";
import type { components } from "../../api/schema";
import type {
  SaveTerminationCalculationRuleBody,
  TerminationCalculationRuleDto,
} from "../personnel/queries";

export type PayrollPeriodListItemDto = components["schemas"]["PayrollPeriodListItemDto"];
export type PayrollPeriodDetailDto = Omit<components["schemas"]["PayrollPeriodDetailDto"], "rows"> & { rows?: PayrollRowDto[] };
export type PayrollRowDto = components["schemas"]["PayrollRowDto"] & { revisionNumber?: number; revision?: string | null };
export type PayrollControlDto = components["schemas"]["PayrollControlDto"];
export type PayrollRowInputModel = components["schemas"]["PayrollRowInputModel"] & { expectedRevision?: string };
export type PayrollCalculation = components["schemas"]["PayrollCalculation"];
export type PreviewPayrollCommand = components["schemas"]["PreviewPayrollCommand"];
export type PayrollSettingsDto = components["schemas"]["PayrollSettingsDto"];
export type UpdatePayrollSettingsCommand = components["schemas"]["UpdatePayrollSettingsCommand"];
export type ApprovePayrollSettingsCommand = components["schemas"]["ApprovePayrollSettingsCommand"];
export type MyPayslipDto = components["schemas"]["MyPayslipDto"] & { hasRevision?: boolean };

export const usePayrollPeriods = (enabled: boolean) =>
  useQuery({
    queryKey: ["payroll", "periods"],
    queryFn: () => apiFetch<PayrollPeriodListItemDto[]>("/payroll/periods"),
    enabled,
  });

export const usePayrollPeriod = (id: number | null) =>
  useQuery({
    queryKey: ["payroll", "period", id],
    queryFn: () => apiFetch<PayrollPeriodDetailDto>(`/payroll/periods/${id}`),
    enabled: id !== null,
  });

export const useCreatePayrollPeriod = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: { year: number; month: number; expectedPreviewToken: string; guidanceContentRevisionId?: number }) =>
      apiFetch<PayrollPeriodDetailDto>("/payroll/periods", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
      queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }),
    ]),
  });
};

export const useUpdatePayrollRow = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ periodId, rowId, model }: { periodId: number; rowId: number; model: PayrollRowInputModel }) =>
      apiFetch<PayrollRowDto>(`/payroll/periods/${periodId}/rows/${rowId}`, {
        method: "PUT",
        body: JSON.stringify(model),
      }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
      queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }),
    ]),
  });
};

export const useRunPayrollCheck = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (periodId: number) =>
      apiFetch<PayrollPeriodDetailDto>(`/payroll/periods/${periodId}/check`, {
        method: "POST",
        body: JSON.stringify({}),
      }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
      queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }),
    ]),
  });
};

export const useApprovePayrollRow = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ periodId, rowId, guidanceContentRevisionId, expectedRevision }: { periodId: number; rowId: number; guidanceContentRevisionId?: number; expectedRevision: string }) =>
      apiFetch<PayrollRowDto>(`/payroll/periods/${periodId}/rows/${rowId}/approve`, {
        method: "POST",
        body: JSON.stringify({ guidanceContentRevisionId, expectedRevision }),
      }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
      queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }),
    ]),
  });
};

export const useSubmitPayrollPeriod = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: number | { periodId: number; guidanceContentRevisionId?: number }) =>
      apiFetch<PayrollPeriodDetailDto>(`/payroll/periods/${typeof input === "number" ? input : input.periodId}/submit`, {
        method: "POST",
        body: JSON.stringify(typeof input === "number" ? {} : { guidanceContentRevisionId: input.guidanceContentRevisionId }),
      }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
      queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }),
    ]),
  });
};

export const usePayrollPreview = () =>
  useMutation({
    mutationFn: (command: PreviewPayrollCommand) =>
      apiFetch<PayrollCalculation>("/payroll/preview", {
        method: "POST",
        body: JSON.stringify(command),
      }),
  });

export const usePayrollSettings = (enabled: boolean) =>
  useQuery({
    queryKey: ["payroll", "settings"],
    queryFn: () => apiFetch<PayrollSettingsDto>("/payroll/settings"),
    retry: (count, error) => !(error instanceof ApiError && error.status === 404) && count < 2,
    enabled,
  });

export const usePayrollSettingsDrafts = (enabled: boolean) =>
  useQuery({
    queryKey: ["payroll", "settings", "drafts"],
    queryFn: () => apiFetch<PayrollSettingsDto[]>("/payroll/settings/drafts"),
    enabled,
  });

export const useUpdatePayrollSettings = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: UpdatePayrollSettingsCommand) =>
      apiFetch<PayrollSettingsDto>("/payroll/settings", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll", "settings"] }),
  });
};

export const useApprovePayrollSettings = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: ApprovePayrollSettingsCommand) =>
      apiFetch<PayrollSettingsDto>(`/payroll/settings/${command.id}/approve`, {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll", "settings"] }),
  });
};
export const useTerminationCalculationRules = () =>
  useQuery({
    queryKey: ["termination-calculation-rules"],
    queryFn: () => apiFetch<TerminationCalculationRuleDto[]>("/termination-calculation-rules"),
  });

export const useSaveTerminationCalculationRule = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id?: number; body: SaveTerminationCalculationRuleBody }) =>
      apiFetch<TerminationCalculationRuleDto>(
        id ? `/termination-calculation-rules/${id}` : "/termination-calculation-rules",
        { method: id ? "PUT" : "POST", body: JSON.stringify(body) },
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["termination-calculation-rules"] }),
  });
};

export const useApproveTerminationCalculationRule = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, expectedVersion }: { id: number; expectedVersion: number }) =>
      apiFetch<TerminationCalculationRuleDto>(`/termination-calculation-rules/${id}/approve`, {
        method: "POST",
        body: JSON.stringify({ expectedVersion }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["termination-calculation-rules"] }),
  });
};

export const useMyPayslips = () =>
  useQuery({ queryKey: ["payroll", "my"], queryFn: () => apiFetch<MyPayslipDto[]>("/payroll/my") });
