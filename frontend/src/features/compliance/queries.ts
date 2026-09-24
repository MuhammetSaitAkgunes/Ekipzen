import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { dailyTasksKey } from "../guidance/queries";
import type { components } from "../../api/schema";

export type ComplianceDocumentDto = components["schemas"]["ComplianceDocumentDto"];
export type ComplianceReadinessDto = components["schemas"]["ComplianceReadinessDto"];
export type CreateComplianceDocumentCommand = components["schemas"]["CreateComplianceDocumentCommand"];

export type ComplianceFilters = { search: string; status: string; level: string };

export const COMPLIANCE_STATUSES = ["Eksik", "İncelemede", "Süresi Yaklaşıyor", "Tamamlandı"];
export const RISK_LEVELS = ["high", "medium", "low"];

const documentsPath = (filters: ComplianceFilters): string => {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.level) params.set("level", filters.level);
  if (filters.search) params.set("search", filters.search);
  const query = params.toString();
  return query ? `/compliance/documents?${query}` : "/compliance/documents";
};

export const useComplianceDocuments = (filters: ComplianceFilters) =>
  useQuery({
    queryKey: ["compliance", "documents", filters],
    queryFn: () => apiFetch<ComplianceDocumentDto[]>(documentsPath(filters)),
  });

export const useComplianceReadiness = () =>
  useQuery({
    queryKey: ["compliance", "readiness"],
    queryFn: () => apiFetch<ComplianceReadinessDto>("/compliance/readiness"),
  });

export const useCreateComplianceDocument = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateComplianceDocumentCommand) =>
      apiFetch<ComplianceDocumentDto>("/compliance/documents", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["compliance"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
    },
  });
};

export const useUpdateComplianceDocument = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, documentName, dueDate, level }: {
      id: number; documentName: string; dueDate: string | null; level: string;
    }) =>
      apiFetch<ComplianceDocumentDto>(`/compliance/documents/${id}`, {
        method: "PUT",
        body: JSON.stringify({ documentName, dueDate, level }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["compliance"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
    },
  });
};

export const useSetComplianceStatus = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiFetch<ComplianceDocumentDto>(`/compliance/documents/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["compliance"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
    },
  });
};

export const useAssignComplianceOwner = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ownerName }: { id: number; ownerName: string }) =>
      apiFetch<ComplianceDocumentDto>(`/compliance/documents/${id}/owner`, {
        method: "PATCH",
        body: JSON.stringify({ ownerName }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["compliance"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
    },
  });
};
