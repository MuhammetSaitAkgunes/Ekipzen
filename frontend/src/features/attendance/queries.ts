import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type LiveBoardCardDto = components["schemas"]["LiveBoardCardDto"];
export type TimesheetDto = components["schemas"]["TimesheetDto"];
export type TimesheetRowDto = components["schemas"]["TimesheetRowDto"];
export type AttendanceSummaryDto = components["schemas"]["AttendanceSummaryDto"];
export type AttendanceEntryModel = components["schemas"]["AttendanceEntryModel"];
export type AttendanceEntryPreviewDto = {
  workedMinutes: number;
  overtimeMinutes: number;
  status: string;
  effectiveType: string;
  canSave: boolean;
  blockReason: string | null;
  warnings: string[];
};

export const usePreviewAttendanceEntry = () => useMutation({
  mutationFn: (request: { employeeId: number; entryId: number | null; model: AttendanceEntryModel }) =>
    apiFetch<AttendanceEntryPreviewDto>("/attendance/preview", { method: "POST", body: JSON.stringify(request) }),
});
type EmployeePagedResult = components["schemas"]["EmployeeListItemDtoPagedResult"];

export const useLiveBoard = () =>
  useQuery({ queryKey: ["attendance", "live"], queryFn: () => apiFetch<LiveBoardCardDto[]>("/attendance/live") });

export const useTimesheet = (employeeId: number | null, year: number, month: number) =>
  useQuery({
    queryKey: ["attendance", "timesheet", employeeId, year, month],
    queryFn: () => apiFetch<TimesheetDto>(`/attendance?employeeId=${employeeId}&year=${year}&month=${month}`),
    enabled: employeeId !== null,
  });

export const useAttendanceSummary = (year: number, month: number) =>
  useQuery({
    queryKey: ["attendance", "summary", year, month],
    queryFn: () => apiFetch<AttendanceSummaryDto[]>(`/attendance/summary?year=${year}&month=${month}`),
  });

export const useEmployeeOptions = () =>
  useQuery({
    queryKey: ["employees", "options"],
    queryFn: () => apiFetch<EmployeePagedResult>("/employees?status=active&pageSize=50"),
    select: (r) => r.items ?? [],
  });

export const useSaveAttendanceEntry = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, employeeId, model }: { id: number | null; employeeId: number; model: AttendanceEntryModel }) =>
      id === null
        ? apiFetch<TimesheetRowDto>("/attendance", { method: "POST", body: JSON.stringify({ employeeId, model }) })
        : apiFetch<TimesheetRowDto>(`/attendance/${id}`, { method: "PUT", body: JSON.stringify(model) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["attendance"] }),
  });
};
