import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";

export type PersonnelEntryJourney = {
  processKey: string;
  contentVersion: number;
  hasSavedPosition: boolean;
  bookmarkStepKey: string;
  verifiedEmployeeRecords: number;
  lastVerifiedAtUtc: string | null;
  answeredChecks: number;
  correctChecks: number;
};

export const personnelEntryJourneyKey = ["guidance", "employee-entry", "journey"] as const;

export const usePersonnelEntryJourney = (enabled = true) =>
  useQuery({
    queryKey: personnelEntryJourneyKey,
    queryFn: () => apiFetch<PersonnelEntryJourney>("/guidance/employee-entry/journey"),
    enabled,
  });

export type RecruitmentJourney = {
  processKey: string;
  verifiedFirstReviews: number;
  lastVerifiedAtUtc: string | null;
  contentVersion: number;
  answeredChecks: number;
  correctChecks: number;
};

export const recruitmentJourneyKey = ["guidance", "recruitment", "journey"] as const;

export type LeaveApprovalJourney = {
  processKey: string;
  verifiedDecisions: number;
  lastVerifiedAtUtc: string | null;
  contentVersion: number;
  answeredChecks: number;
  correctChecks: number;
};

export const leaveApprovalJourneyKey = ["guidance", "leave-approval", "journey"] as const;

export const useLeaveApprovalJourney = () =>
  useQuery({
    queryKey: leaveApprovalJourneyKey,
    queryFn: () => apiFetch<LeaveApprovalJourney>("/guidance/leave-approval/journey"),
  });

export const useRecruitmentJourney = (enabled = true) =>
  useQuery({
    queryKey: recruitmentJourneyKey,
    queryFn: () => apiFetch<RecruitmentJourney>("/guidance/recruitment/journey"),
    enabled,
  });
