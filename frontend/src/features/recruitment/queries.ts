import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { dailyTasksKey } from "../guidance/queries";
import { recruitmentJourneyKey } from "../guidance/journeyQueries";
import type { components } from "../../api/schema";

export type CandidateListItemDto = components["schemas"]["CandidateListItemDto"];
export type CandidateDetailDto = Omit<components["schemas"]["CandidateDetailDto"], "notes" | "evaluations"> & {
  id: number; version: number; notes?: InterviewNoteDto[]; evaluations?: CandidateEvaluationDto[];
};
export type CandidateState = {
  name: string; appliedRole: string; positionId: number | null; positionTitle: string | null;
  score: number; location: string | null; experienceYears: number; summary: string | null; status: string;
};
export type CandidateCorrectionDto = {
  id: number; candidateId: number; fromVersion: number; toVersion: number;
  before: CandidateState; after: CandidateState; reason: string; evidenceReviewConfirmed: boolean;
  changedByUserId: string; changedByName: string; changedAtUtc: string;
};
export type CandidateCorrectionInput = {
  id: number; expectedVersion: number; name: string; appliedRole: string;
  positionId: number | null; expectedPositionVersion: number | null; score: number;
  location: string | null; experienceYears: number; summary: string | null;
  reason: string; confirmExistingEvidenceReview: boolean;
};
export type InterviewNoteDto = components["schemas"]["InterviewNoteDto"] & { version: number };
export type CandidateEvaluationDto = components["schemas"]["CandidateEvaluationDto"] & { version: number };
export type CandidateEvidenceState = {
  noteType: string | null; text: string | null; criterion: string | null;
  score: number | null; maxScore: number | null;
};
export type CandidateEvidenceHistory = {
  original: CandidateEvidenceState;
  revisions: { id: number; version: number; fromCandidateVersion: number; toCandidateVersion: number;
    before: CandidateEvidenceState; after: CandidateEvidenceState; reason: string;
    changedByName: string; changedByUserId: string; changedAtUtc: string }[];
};
export type CandidateEvidenceInput = {
  candidateId: number; recordId: number; kind: "notes" | "evaluations";
  expectedCandidateVersion: number; expectedRecordVersion: number; reason: string;
  noteType?: string; text?: string; criterion?: string; score?: number; maxScore?: number;
};
export type HireResultDto = components["schemas"]["HireResultDto"];
export type IdentityProvisioningDto = components["schemas"]["IdentityProvisioningDto"];
export type CreateCandidateCommand = components["schemas"]["CreateCandidateCommand"];
export type PositionDto = components["schemas"]["PositionDto"] & { id: number; version: number };
export type PositionCorrectionDto = {
  id: number; positionId: number; fromVersion: number; toVersion: number;
  before: { title: string; departmentId: number | null; isOpen: boolean; openCount: number };
  after: { title: string; departmentId: number | null; isOpen: boolean; openCount: number };
  reason: string; changedByUserId: string; changedAtUtc: string;
};
export type RecruitmentFunnelDto = components["schemas"]["RecruitmentFunnelDto"];
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

export const useCandidateCorrections = (id: number) =>
  useQuery({ queryKey: ["recruitment", "candidate", id, "corrections"],
    queryFn: () => apiFetch<CandidateCorrectionDto[]>(`/candidates/${id}/corrections`) });

export const useCorrectCandidate = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: CandidateCorrectionInput) =>
      apiFetch<CandidateDetailDto>(`/candidates/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
    onError: (_error, input) => queryClient.invalidateQueries({
      queryKey: ["recruitment", "candidate", input.id] }),
  });
};

export const useRecruitmentFunnel = () =>
  useQuery({
    queryKey: ["recruitment", "funnel"],
    queryFn: () => apiFetch<RecruitmentFunnelDto>("/recruitment/funnel"),
  });

export const useCandidateEvidenceHistory = (candidateId: number, kind: "notes" | "evaluations", recordId: number) =>
  useQuery({ queryKey: ["recruitment", "candidate", candidateId, kind, recordId, "revisions"],
    queryFn: () => apiFetch<CandidateEvidenceHistory>(`/candidates/${candidateId}/${kind}/${recordId}/revisions`) });

export const useReviseCandidateEvidence = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ candidateId, kind, recordId, ...body }: CandidateEvidenceInput) =>
      apiFetch<CandidateDetailDto>(`/candidates/${candidateId}/${kind}/${recordId}/revisions`, {
        method: "POST", body: JSON.stringify(body),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
    onError: (_error, input) => queryClient.invalidateQueries({
      queryKey: ["recruitment", "candidate", input.candidateId] }),
  });
};

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

export const usePositionCorrections = (positionId: number | null) =>
  useQuery({
    queryKey: ["recruitment", "positions", positionId, "corrections"],
    queryFn: () => apiFetch<PositionCorrectionDto[]>(`/positions/${positionId}/corrections`),
    enabled: positionId !== null,
  });

export const useCorrectPosition = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: {
      id: number; expectedVersion: number; title: string; departmentId: number | null;
      openCount: number; isOpen: boolean; reason: string;
    }) => apiFetch<PositionDto>(`/positions/${id}`, {
      method: "PUT", body: JSON.stringify(body),
    }),
    onSuccess: (_result, input) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["recruitment", "positions"] }),
      queryClient.invalidateQueries({ queryKey: ["recruitment", "positions", input.id, "corrections"] }),
    ]),
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
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
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
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
      queryClient.invalidateQueries({ queryKey: recruitmentJourneyKey }),
    ]),
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
    mutationFn: ({ id, departmentId, email, contractType, title, hireDate }: {
      id: number; departmentId: number; email: string; contractType: "indefinite" | "fixed-term";
      title?: string | null; hireDate?: string | null;
    }) =>
      apiFetch<HireResultDto>(`/candidates/${id}/hire`, {
        method: "POST",
        body: JSON.stringify({ departmentId, email, contractType, title: title || null, hireDate: hireDate || null }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["recruitment"] });
      queryClient.invalidateQueries({ queryKey: ["employees"] });
      queryClient.invalidateQueries({ queryKey: dailyTasksKey });
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
