import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { personnelEntryJourneyKey } from "../guidance/journeyQueries";
import type { PersonnelGuideStep } from "./PersonnelGuide";

export type PersonnelEntryBookmark = {
  processKey: string;
  stepKey: PersonnelGuideStep;
  contentVersion: number;
};

export type EmployeeCreationEvidence = {
  employeeId: number;
  createdByCurrentUser: boolean;
  createdAtUtc: string | null;
};

const bookmarkPath = "/guidance/employee-entry/bookmark";
const bookmarkKey = ["guidance", "employee-entry", "bookmark"] as const;

export const usePersonnelEntryBookmark = (enabled: boolean) =>
  useQuery({
    queryKey: bookmarkKey,
    queryFn: () => apiFetch<PersonnelEntryBookmark>(bookmarkPath),
    enabled,
  });

export const useSavePersonnelEntryBookmark = () => {
  const queryClient = useQueryClient();
  return useMutation({
    scope: { id: "personnel-entry-bookmark" },
    mutationFn: (stepKey: PersonnelGuideStep) =>
      apiFetch<PersonnelEntryBookmark>(bookmarkPath, {
        method: "PUT",
        body: JSON.stringify({ stepKey }),
      }),
    onSuccess: (bookmark) => {
      queryClient.setQueryData(bookmarkKey, bookmark);
      void queryClient.invalidateQueries({ queryKey: personnelEntryJourneyKey });
    },
  });
};

export const useEmployeeCreationEvidence = (employeeId: number | null, enabled: boolean) =>
  useQuery({
    queryKey: ["guidance", "employee-entry", "evidence", employeeId],
    queryFn: () => apiFetch<EmployeeCreationEvidence>(
      `/guidance/employee-entry/employees/${employeeId}/evidence`,
    ),
    enabled: enabled && employeeId !== null,
  });
