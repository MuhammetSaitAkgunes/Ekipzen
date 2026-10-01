import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";

export type PublishedPractice = {
  contentRevisionId: number; contentKey: string; processKey: string; stepKey: string; contentVersion: number;
  title: string; isCurrent: boolean; question: string; choices: string[]; attemptCount: number;
  lastSelectedChoice: number | null; lastCorrect: boolean | null; explanation: string | null;
};
export type PublishedPracticeProgress = Pick<PublishedPractice, "contentRevisionId" | "contentKey" | "processKey" |
  "stepKey" | "contentVersion" | "title" | "attemptCount" | "isCurrent"> & {
    lastSelectedChoice: number; lastCorrect: boolean; lastAnsweredAtUtc: string;
  };
export const usePublishedStep = (process: string, step: string, userId: string | null | undefined, enabled: boolean) => useQuery({
  queryKey: ["guidance", "published-step", userId, process, step], enabled,
  queryFn: () => apiFetch<PublishedPractice | null>(`/guidance/catalog/steps/${process}/${step}`),
});
export const usePublishedPractice = (id: number, userId?: string | null) => useQuery({
  queryKey: ["guidance", "published-practice", userId, id],
  queryFn: () => apiFetch<PublishedPractice>(`/guidance/catalog/versions/${id}/practice`),
});
export const usePublishedPracticeProgress = (userId?: string | null, enabled = true) => useQuery({
  queryKey: ["guidance", "published-progress", userId], enabled,
  queryFn: () => apiFetch<PublishedPracticeProgress[]>("/guidance/catalog/practice-progress"),
});
export function useSubmitPublishedPractice(userId?: string | null) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: number; selectedChoice: number; expectedAttemptCount: number; attemptId: string }) =>
      apiFetch<PublishedPractice>(`/guidance/catalog/versions/${id}/practice/attempts`, { method: "POST", body: JSON.stringify(body) }),
    onSuccess: async data => {
      client.setQueryData(["guidance", "published-practice", userId, data.contentRevisionId], data);
      await client.invalidateQueries({ queryKey: ["guidance", "published-progress", userId] });
    },
  });
}
