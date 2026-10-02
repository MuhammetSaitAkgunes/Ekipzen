import { guidanceOperationsKey } from "../guidance/operationQueries";
import { dailyTasksKey } from "../guidance/queries";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { DepartmentDto } from "../personnel/queries";

type DepartmentInput = { name: string; code: string | null; guidanceContentRevisionId?: number; expectedRecordToken?: string };

export const useCreateDepartment = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: DepartmentInput) => apiFetch<DepartmentDto>("/departments", {
      method: "POST", body: JSON.stringify(input),
    }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["departments"] }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }), queryClient.invalidateQueries({ queryKey: dailyTasksKey })]),
  });
};

export const useUpdateDepartment = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...input }: DepartmentInput & { id: number }) => apiFetch<DepartmentDto>(`/departments/${id}`, {
      method: "PUT", body: JSON.stringify(input),
    }),
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["departments"] }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }), queryClient.invalidateQueries({ queryKey: dailyTasksKey })]),
  });
};

export type DepartmentDeletionPreview = { id: number; name: string; code: string | null; employeeCount: number; positionCount: number; canDelete: boolean; recordToken: string };
export const usePreviewDepartmentDeletion = () => useMutation({
  mutationFn: (id: number) => apiFetch<DepartmentDeletionPreview>(`/departments/${id}/deletion-preview`),
});
export const useDeleteDepartment = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, expectedRecordToken, guidanceContentRevisionId }: { id: number; expectedRecordToken: string; guidanceContentRevisionId?: number }) => {
      const params = new URLSearchParams({ expectedRecordToken });
      if (guidanceContentRevisionId != null) params.set("guidanceContentRevisionId", String(guidanceContentRevisionId));
      return apiFetch<null>(`/departments/${id}?${params}`, { method: "DELETE" });
    },
    onSuccess: () => Promise.all([queryClient.invalidateQueries({ queryKey: ["departments"] }), queryClient.invalidateQueries({ queryKey: guidanceOperationsKey }), queryClient.invalidateQueries({ queryKey: dailyTasksKey })]),
  });
};
