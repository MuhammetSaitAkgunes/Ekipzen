import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type EmployeeListItemDto = components["schemas"]["EmployeeListItemDto"];
export type EmployeePagedResult = components["schemas"]["EmployeeListItemDtoPagedResult"];
export type EmployeeDetailDto = components["schemas"]["EmployeeDetailDto"];
export type EmployeeUpsertModel = components["schemas"]["EmployeeUpsertModel"];
export type DepartmentDto = components["schemas"]["DepartmentDto"];
export type EmployeeDocumentDto = components["schemas"]["EmployeeDocumentDto"];
export type EmployeeOffboardingContextDto = components["schemas"]["EmployeeOffboardingContextDto"];
export type EmployeeOffboardingCaseDto = components["schemas"]["EmployeeOffboardingCaseDto"];
export type SaveOffboardingDraftBody = components["schemas"]["SaveOffboardingDraftBody"];
export type ManagerSuccessorDto = components["schemas"]["ManagerSuccessorDto"];

export type EmployeeFilters = { search: string; departmentId: string; status: string };

const employeesPath = (filters: EmployeeFilters): string => {
  const params = new URLSearchParams({ pageSize: "50" });
  if (filters.search.trim()) params.set("search", filters.search.trim());
  if (filters.departmentId) params.set("departmentId", filters.departmentId);
  if (filters.status) params.set("status", filters.status);
  return `/employees?${params.toString()}`;
};

export const useEmployees = (filters: EmployeeFilters) =>
  useQuery({
    queryKey: ["employees", filters],
    queryFn: () => apiFetch<EmployeePagedResult>(employeesPath(filters)),
  });

export const useDepartments = () =>
  useQuery({ queryKey: ["departments"], queryFn: () => apiFetch<DepartmentDto[]>("/departments") });

export const useEmployee = (id: number | null) =>
  useQuery({
    queryKey: ["employees", id],
    queryFn: () => apiFetch<EmployeeDetailDto>(`/employees/${id}`),
    enabled: id !== null,
  });

export const useEmployeeOffboardingContext = (id: number | null) =>
  useQuery({
    queryKey: ["employees", id, "offboarding-context"],
    queryFn: () => apiFetch<EmployeeOffboardingContextDto>(`/employees/${id}/offboarding-context`),
    enabled: id !== null,
  });

export const useEmployeeDocuments = (id: number | null) =>
  useQuery({
    queryKey: ["employees", id, "documents"],
    queryFn: () => apiFetch<EmployeeDocumentDto[]>(`/employees/${id}/documents`),
    enabled: id !== null,
  });

export const useSaveEmployee = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, model }: { id: number | null; model: EmployeeUpsertModel }) =>
      id === null
        ? apiFetch<EmployeeDetailDto>("/employees", { method: "POST", body: JSON.stringify(model) })
        : apiFetch<EmployeeDetailDto>(`/employees/${id}`, { method: "PUT", body: JSON.stringify(model) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["employees"] }),
  });
};

export const useSaveOffboardingDraft = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, body }: { employeeId: number; body: SaveOffboardingDraftBody }) =>
      apiFetch<EmployeeOffboardingCaseDto>(`/employees/${employeeId}/offboarding-draft`, {
        method: "PUT",
        body: JSON.stringify(body),
      }),
    onSuccess: (_case, { employeeId }) =>
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "offboarding-context"] }),
  });
};

export const useCompleteOffboarding = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, caseId, expectedVersion }: {
      employeeId: number;
      caseId: number;
      expectedVersion: number;
    }) => apiFetch<EmployeeOffboardingCaseDto>(
      `/employees/${employeeId}/offboarding/${caseId}/complete`,
      { method: "POST", body: JSON.stringify({ expectedVersion }) },
    ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["employees"] }),
  });
};

export const useReverseOffboarding = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, caseId, expectedVersion, reason }: {
      employeeId: number;
      caseId: number;
      expectedVersion: number;
      reason: string;
    }) => apiFetch<EmployeeOffboardingCaseDto>(
      `/employees/${employeeId}/offboarding/${caseId}/reverse`,
      { method: "POST", body: JSON.stringify({ expectedVersion, reason }) },
    ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["employees"] }),
  });
};

export const useUploadPhoto = () =>
  useMutation({
    mutationFn: ({ id, file }: { id: number; file: File }) => {
      const form = new FormData();
      form.append("file", file);
      return apiFetch<{ photoPath: string }>(`/employees/${id}/photo`, { method: "POST", body: form });
    },
  });

export const useUploadDocument = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, file, documentType }: { id: number; file: File; documentType: string }) => {
      const form = new FormData();
      form.append("file", file);
      form.append("documentType", documentType);
      return apiFetch<EmployeeDocumentDto>(`/employees/${id}/documents`, { method: "POST", body: form });
    },
    onSuccess: (_data, { id }) =>
      queryClient.invalidateQueries({ queryKey: ["employees", id, "documents"] }),
  });
};

export type ImportRowIssue = components["schemas"]["ImportRowIssueDto"];
export type ImportPreviewDto = components["schemas"]["ImportPreviewDto"];
export type ImportResultDto = components["schemas"]["ImportResultDto"];

const dosyaGovdesi = (file: File): FormData => {
  const form = new FormData();
  form.append("file", file);
  return form;
};

export const usePreviewImport = () =>
  useMutation({
    mutationFn: (file: File) =>
      apiFetch<ImportPreviewDto>("/employees/import/preview", {
        method: "POST",
        body: dosyaGovdesi(file),
      }),
  });

export const useImportEmployees = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) =>
      apiFetch<ImportResultDto>("/employees/import", { method: "POST", body: dosyaGovdesi(file) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["employees"] }),
  });
};