import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { DepartmentDto } from "../personnel/queries";

type DepartmentInput = { name: string; code: string | null };

export const useCreateDepartment = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: DepartmentInput) => apiFetch<DepartmentDto>("/departments", {
      method: "POST", body: JSON.stringify(input),
    }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["departments"] }),
  });
};

export const useUpdateDepartment = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...input }: DepartmentInput & { id: number }) => apiFetch<DepartmentDto>(`/departments/${id}`, {
      method: "PUT", body: JSON.stringify(input),
    }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["departments"] }),
  });
};
