import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";

export type PersonnelEntryJourney = {
  processKey: string;
  contentVersion: number;
  hasSavedPosition: boolean;
  bookmarkStepKey: string;
  verifiedEmployeeRecords: number;
  lastVerifiedAtUtc: string | null;
};

export const personnelEntryJourneyKey = ["guidance", "employee-entry", "journey"] as const;

export const usePersonnelEntryJourney = () =>
  useQuery({
    queryKey: personnelEntryJourneyKey,
    queryFn: () => apiFetch<PersonnelEntryJourney>("/guidance/employee-entry/journey"),
  });
