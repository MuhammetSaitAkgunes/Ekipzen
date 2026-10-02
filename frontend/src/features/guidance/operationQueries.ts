import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { GuidanceContent } from "./catalogQueries";

export const guidanceOperationsKey = ["guidance", "operations"] as const;
export type GuidanceOperationEvidence = {
  evidenceKey: string;
  importChanges?: { importRequestId: string; createdCount: number; skippedCount: number; createdEmployeeIds: number[];
    skippedRows: Array<{ rowNumber: number; outcome: string; explanation: string; issues: string[] }> } | null;
  setupChanges?: { addedCompanyProfile: boolean; addedLeaveTypeCount: number; addedHolidayCount: number; addedPayrollSettings: boolean } | null;
  personnelChanges?: { employeeId: number; documentId?: number | null; changedFields: string[]; reason?: string | null;
    employmentPeriodId?: number | null; compensationPackageId?: number | null; relatedPackageId?: number | null; fromVersion?: number | null; toVersion?: number | null } | null;
  operationKey: string; resourceId: number; outcome: string; occurredAtUtc: string;
  content: { contentRevisionId: number; contentKey: string; processKey: string; stepKey: string; contentVersion: number; title: string };
};
export type GuidanceOperationHistory = { evidence: GuidanceOperationEvidence; isCurrent: boolean };
export const useGuidanceOperations = (userId?: string | null, enabled = true) => useQuery({
  queryKey: [...guidanceOperationsKey, userId], enabled: !!userId && enabled,
  queryFn: () => apiFetch<GuidanceOperationHistory[]>("/guidance/operations"),
});
export const useGuidanceOperationSources = (process: string, userId?: string | null, enabled = false) => useQuery({
  queryKey: ["guidance", "operation-sources", userId, process], enabled: enabled && !!userId,
  queryFn: () => apiFetch<GuidanceContent[]>(`/guidance/catalog?processKey=${process}`),
});
