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

export const usePersonnelEntryJourney = () =>
  useQuery({
    queryKey: personnelEntryJourneyKey,
    queryFn: () => apiFetch<PersonnelEntryJourney>("/guidance/employee-entry/journey"),
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

export const useRecruitmentJourney = () =>
  useQuery({
    queryKey: recruitmentJourneyKey,
    queryFn: () => apiFetch<RecruitmentJourney>("/guidance/recruitment/journey"),
  });
