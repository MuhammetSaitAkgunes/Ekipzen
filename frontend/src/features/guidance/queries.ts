import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";

export type DailyGuidanceTask = {
  key: string;
  kind: "leave-approval" | "compliance-document" | "payroll-period" | "candidate-review" | "employee-compensation" | "offboarding-case" | "offboarding-external-payment" | "offboarding-external-notification";
  recordId: number;
  title: string;
  reason: string;
  nextAction: string;
  dueDate: string | null;
  dateNote: string;
  targetPath: string;
};

export const dailyTasksKey = ["guidance", "daily-tasks"] as const;

export const useDailyGuidanceTasks = () =>
  useQuery({
    queryKey: dailyTasksKey,
    queryFn: () => apiFetch<DailyGuidanceTask[]>("/guidance/daily-tasks"),
  });
