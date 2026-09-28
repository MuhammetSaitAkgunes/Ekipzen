import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { personnelEntryJourneyKey, recruitmentJourneyKey } from "./journeyQueries";

export type GuidanceDecisionProcess = "employee-entry" | "recruitment";

export type GuidanceDecisionCheck = {
  stepKey: string;
  contentVersion: number;
  question: string;
  choices: string[];
  lastSelectedChoice: number | null;
  lastCorrect: boolean | null;
  attemptCount: number;
  explanation: string | null;
};

const checkKey = (process: GuidanceDecisionProcess, step: string) =>
  ["guidance", process, "checks", step] as const;
const checkPath = (process: GuidanceDecisionProcess, step: string) =>
  `/guidance/${process}/checks/${step}`;

export const useGuidanceDecisionCheck = (process: GuidanceDecisionProcess, step: string) =>
  useQuery({
    queryKey: checkKey(process, step),
    queryFn: () => apiFetch<GuidanceDecisionCheck>(checkPath(process, step)),
  });

export const useSubmitGuidanceDecisionCheck = () => {
  const queryClient = useQueryClient();
  return useMutation({
    scope: { id: "guidance-decision-check" },
    mutationFn: ({ process, step, selectedChoice }: {
      process: GuidanceDecisionProcess; step: string; selectedChoice: number;
    }) => apiFetch<GuidanceDecisionCheck>(`${checkPath(process, step)}/attempts`, {
      method: "POST",
      body: JSON.stringify({ selectedChoice }),
    }),
    onSuccess: (result, { process, step }) => {
      queryClient.setQueryData(checkKey(process, step), result);
      void queryClient.invalidateQueries({
        queryKey: process === "employee-entry" ? personnelEntryJourneyKey : recruitmentJourneyKey,
      });
    },
  });
};
