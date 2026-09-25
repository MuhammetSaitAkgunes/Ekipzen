import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type CandidateListItemDto = components["schemas"]["CandidateListItemDto"];
export type CandidateDetailDto = components["schemas"]["CandidateDetailDto"];
export type InterviewNoteDto = components["schemas"]["InterviewNoteDto"];
export type CandidateEvaluationDto = components["schemas"]["CandidateEvaluationDto"];
export type HireResultDto = components["schemas"]["HireResultDto"];
export type IdentityProvisioningDto = components["schemas"]["IdentityProvisioningDto"];
export type CreateCandidateCommand = components["schemas"]["CreateCandidateCommand"];
export type PositionDto = components["schemas"]["PositionDto"];
export type CreatePositionCommand = components["schemas"]["CreatePositionCommand"];

const candidatesPath = (search: string, status: string): string => {
  const params = new URLSearchParams();
  if (search) params.set("search", search);
  if (status) params.set("status", status);
  const query = params.toString();
  return query ? `/candidates?${query}` : "/candidates";
};

export const useCandidates = (search: string, status: string) =>
  useQuery({
    queryKey: ["recruitment", "candidates", search, status],
    queryFn: () => apiFetch<CandidateListItemDto[]>(candidatesPath(search, status)),
  });

export const useCandidate = (id: number | null) =>
  useQuery({
    queryKey: ["recruitment", "candidate", id],
    queryFn: () => apiFetch<CandidateDetailDto>(`/candidates/${id}`),
    enabled: id !== null,
  });

export const usePositions = () =>
  useQuery({ queryKey: ["recruitment", "positions"], queryFn: () => apiFetch<PositionDto[]>("/positions") });

export const useCreatePosition = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreatePositionCommand) =>
      apiFetch<PositionDto>("/positions", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment", "positions"] }),
  });
};

export const useCreateCandidate = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateCandidateCommand) =>
      apiFetch<CandidateDetailDto>("/candidates", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
  });
};

export const useSetCandidateStatus = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiFetch<CandidateDetailDto>(`/candidates/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
  });
};

export const useAddInterviewNote = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, noteType, text }: { id: number; noteType: string; text: string }) =>
      apiFetch<InterviewNoteDto>(`/candidates/${id}/notes`, {
        method: "POST",
        body: JSON.stringify({ noteType, text }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
  });
};

export const useAddCandidateEvaluation = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, criterion, score, maxScore }: {
      id: number; criterion: string; score: number; maxScore: number;
    }) =>
      apiFetch<CandidateEvaluationDto>("/candidates/" + id + "/evaluations", {
        method: "POST",
        body: JSON.stringify({ criterion, score, maxScore }),
      }),
    onSuccess: (_result, { id }) => queryClient.invalidateQueries({ queryKey: ["recruitment", "candidate", id] }),
  });
};

export const useHireCandidate = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, departmentId, email, title, hireDate }: {
      id: number; departmentId: number; email: string; title?: string | null; hireDate?: string | null;
    }) =>
      apiFetch<HireResultDto>(`/candidates/${id}/hire`, {
        method: "POST",
        body: JSON.stringify({ departmentId, email, title: title || null, hireDate: hireDate || null }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["recruitment"] });
      queryClient.invalidateQueries({ queryKey: ["employees"] });
    },
  });
};

export const useRetryCandidateProvisioning = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, email }: { id: number; email?: string | null }) =>
      apiFetch<IdentityProvisioningDto>(`/candidates/${id}/provisioning/retry`, {
        method: "POST",
        body: JSON.stringify({ email: email || null }),
      }),
    onSuccess: (_result, { id }) => {
      queryClient.invalidateQueries({ queryKey: ["recruitment", "candidate", id] });
      queryClient.invalidateQueries({ queryKey: ["employees"] });
    },
  });
};
