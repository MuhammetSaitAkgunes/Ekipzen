import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import { personnelEntryJourneyKey } from "../guidance/journeyQueries";
import { dailyTasksKey } from "../guidance/queries";
import type { components } from "../../api/schema";

export type EmployeeListItemDto = components["schemas"]["EmployeeListItemDto"];
export type EmployeePagedResult = components["schemas"]["EmployeeListItemDtoPagedResult"];
export type EmployeeDetailDto = components["schemas"]["EmployeeDetailDto"];
export type EmployeeUpsertModel = components["schemas"]["EmployeeUpsertModel"];
export type DepartmentDto = components["schemas"]["DepartmentDto"];
export type EmployeeDocumentDto = components["schemas"]["EmployeeDocumentDto"];
export type WithdrawnEmployeeDocumentDto = components["schemas"]["WithdrawnEmployeeDocumentDto"];
export type EmployeeOffboardingContextDto = components["schemas"]["EmployeeOffboardingContextDto"];
export type EmployeeOffboardingCaseDto = components["schemas"]["EmployeeOffboardingCaseDto"] & {
  calculationRuleId?: number | null;
  calculationRuleVersion?: number | null;
  calculationGeneratedAtUtc?: string | null;
};
export type TerminationCalculationRuleDto = {
  id: number;
  version: number;
  effectiveFrom: string;
  status: "draft" | "approved";
  dailyWageDivisor: number;
  severanceDaysPerYear: number;
  minimumSeveranceServiceDays: number;
  severanceCeiling: number;
  noticeDaysUnderSixMonths: number;
  noticeDaysSixToEighteenMonths: number;
  noticeDaysEighteenToThirtySixMonths: number;
  noticeDaysOverThirtySixMonths: number;
  noticeFirstThresholdMonths: number;
  noticeSecondThresholdMonths: number;
  noticeThirdThresholdMonths: number;
  includeRegularPremiumInDressedWage: boolean;
  includeRoadAllowanceInDressedWage: boolean;
  includeMealAllowanceInDressedWage: boolean;
  includeOtherRegularBenefitsInDressedWage: boolean;
  sourceReference: string;
  reviewNotes?: string | null;
  approvedAtUtc?: string | null;
  approvedByUserId?: string | null;
};
export type SaveTerminationCalculationRuleBody = Omit<
  TerminationCalculationRuleDto,
  "id" | "version" | "status" | "approvedAtUtc" | "approvedByUserId"
> & { expectedVersion?: number | null };
export type SaveOffboardingDraftBody = components["schemas"]["SaveOffboardingDraftBody"];
export type ManagerSuccessorDto = components["schemas"]["ManagerSuccessorDto"];
export type EmployeeEmploymentFoundationDto = components["schemas"]["EmployeeEmploymentFoundationDto"];
export type SaveCompensationPackageBody = components["schemas"]["SaveCompensationPackageBody"];
export type CompensationPackageValues = {
  monthlyGrossSalary: number; regularPremium: number; roadAllowance: number;
  mealAllowance: number; otherRegularBenefits: number; sourceReference: string;
};
export type CompensationCorrectionContext = {
  package: CompensationPackageValues & { id: number; version: number; effectiveFrom: string; effectiveTo: string | null };
  canCorrect: boolean;
  blockingReasons: string[];
  history: Array<{ fromVersion: number; toVersion: number; before: CompensationPackageValues;
    after: CompensationPackageValues; reason: string; actorUserId: string; correctedAtUtc: string }>;
};
export type CorrectCompensationPackageBody = CompensationPackageValues & { expectedVersion: number; reason: string };
export type CompensationRemediationImpact = {
  potentialPayrollRows: Array<{ rowId: number; periodId: number; year: number; month: number;
    periodStatus: string; rowStatus: string; recordedGrossSalary: number }>;
  offboardingCases: Array<{ caseId: number; status: string; terminationDate: string;
    grossSalarySnapshot: number }>;
};
export type CompensationRemediationCase = {
  id: number; version: number; status: "draft" | "approved"; packageVersion: number;
  issueDescription: string; proposedEffectiveFrom: string | null;
  proposedValues: CompensationPackageValues; impactSnapshot: CompensationRemediationImpact;
  openedByUserId: string; openedAtUtc: string; reviewedByUserId: string | null;
  reviewedAtUtc: string | null; expertReviewReference: string | null; reviewNotes: string | null;
};
export type CompensationRemediationContext = {
  package: CompensationCorrectionContext["package"];
  currentImpact: CompensationRemediationImpact;
  canOpen: boolean;
  case: CompensationRemediationCase | null;
};
export type SaveCompensationRemediationBody = CompensationPackageValues & {
  expectedPackageVersion: number; expectedCaseVersion: number | null;
  issueDescription: string; proposedEffectiveFrom: string | null;
};
export type CompensationDateCorrectionPreview = {
  package: CompensationCorrectionContext["package"];
  previousPackage: (CompensationCorrectionContext["package"] & { effectiveTo: string | null }) | null;
  proposedEffectiveFrom: string;
  proposedPreviousEffectiveTo: string | null;
  canApply: boolean;
  blockingReasons: string[];
  history: Array<{ oldEffectiveFrom: string; newEffectiveFrom: string;
    oldPreviousEffectiveTo: string | null; newPreviousEffectiveTo: string | null;
    fromVersion: number; toVersion: number; reason: string;
    actorUserId: string; correctedAtUtc: string }>;
};
export type RehireEmployeeBody = components["schemas"]["RehireEmployeeBody"];

export type EmployeeFilters = { search: string; departmentId: string; status: string; page?: number };

const employeesPath = (filters: EmployeeFilters): string => {
  const params = new URLSearchParams({ pageSize: "50", page: String(filters.page ?? 1) });
  if (filters.search.trim()) params.set("search", filters.search.trim());
  if (filters.departmentId) params.set("departmentId", filters.departmentId);
  if (filters.status) params.set("status", filters.status);
  return `/employees?${params.toString()}`;
};

export const useEmployees = (filters: EmployeeFilters) =>
  useQuery({
    queryKey: ["employees", filters],
    queryFn: () => apiFetch<EmployeePagedResult>(employeesPath(filters)),
  });

export const useDepartments = () =>
  useQuery({ queryKey: ["departments"], queryFn: () => apiFetch<DepartmentDto[]>("/departments") });

export const useEmployee = (id: number | null) =>
  useQuery({
    queryKey: ["employees", id],
    queryFn: () => apiFetch<EmployeeDetailDto>(`/employees/${id}`),
    enabled: id !== null,
  });

export const useEmployeeEmploymentFoundation = (id: number | null, enabled = true) =>
  useQuery({
    queryKey: ["employees", id, "employment-foundation"],
    queryFn: () => apiFetch<EmployeeEmploymentFoundationDto>(`/employees/${id}/employment-foundation`),
    enabled: id !== null && enabled,
  });

export const useEmployeeOffboardingContext = (id: number | null) =>
  useQuery({
    queryKey: ["employees", id, "offboarding-context"],
    queryFn: () => apiFetch<EmployeeOffboardingContextDto>(`/employees/${id}/offboarding-context`),
    enabled: id !== null,
  });

export const useEmployeeDocuments = (id: number | null) =>
  useQuery({
    queryKey: ["employees", id, "documents"],
    queryFn: () => apiFetch<EmployeeDocumentDto[]>(`/employees/${id}/documents`),
    enabled: id !== null,
  });

export const useWithdrawnEmployeeDocuments = (id: number | null, enabled: boolean) =>
  useQuery({
    queryKey: ["employees", id, "documents", "withdrawn"],
    queryFn: () => apiFetch<WithdrawnEmployeeDocumentDto[]>(`/employees/${id}/documents/withdrawn`),
    enabled: id !== null && enabled,
  });

export const useSaveEmployee = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, model }: { id: number | null; model: EmployeeUpsertModel }) =>
      id === null
        ? apiFetch<EmployeeDetailDto>("/employees", { method: "POST", body: JSON.stringify(model) })
        : apiFetch<EmployeeDetailDto>(`/employees/${id}`, { method: "PUT", body: JSON.stringify(model) }),
    onSuccess: async (_employee, { id }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["employees"] }),
        queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
        ...(id === null ? [queryClient.invalidateQueries({ queryKey: personnelEntryJourneyKey })] : []),
      ]);
    },
  });
};

export const useRehireEmployee = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, body }: { employeeId: number; body: RehireEmployeeBody }) =>
      apiFetch<EmployeeDetailDto>(`/employees/${employeeId}/rehire`, {
        method: "POST",
        body: JSON.stringify(body),
      }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useSaveCompensationPackage = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, body }: { employeeId: number; body: SaveCompensationPackageBody }) =>
      apiFetch<EmployeeEmploymentFoundationDto>(`/employees/${employeeId}/compensation-packages`, {
        method: "POST",
        body: JSON.stringify(body),
      }),
    onSuccess: (_foundation, { employeeId }) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "employment-foundation"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useCompensationCorrectionContext = (employeeId: number, packageId: number) =>
  useQuery({
    queryKey: ["employees", employeeId, "compensation-packages", packageId, "correction-context"],
    queryFn: () => apiFetch<CompensationCorrectionContext>(
      `/employees/${employeeId}/compensation-packages/${packageId}/correction-context`),
  });

export const useCorrectCompensationPackage = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, packageId, body }: {
      employeeId: number; packageId: number; body: CorrectCompensationPackageBody;
    }) => apiFetch<EmployeeEmploymentFoundationDto>(`/employees/${employeeId}/compensation-packages/${packageId}`,
      { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: (_foundation, { employeeId, packageId }) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "employment-foundation"] }),
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "compensation-packages", packageId, "correction-context"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

const remediationKey = (employeeId: number, packageId: number) =>
  ["employees", employeeId, "compensation-packages", packageId, "remediation-case"] as const;

export const useCompensationRemediationContext = (employeeId: number, packageId: number) =>
  useQuery({
    queryKey: remediationKey(employeeId, packageId),
    queryFn: () => apiFetch<CompensationRemediationContext>(
      `/employees/${employeeId}/compensation-packages/${packageId}/remediation-case`),
  });

export const useSaveCompensationRemediationCase = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, packageId, caseId, body }: {
      employeeId: number; packageId: number; caseId: number | null;
      body: SaveCompensationRemediationBody;
    }) => apiFetch<CompensationRemediationCase>(
      `/employees/${employeeId}/compensation-packages/${packageId}/remediation-case${caseId === null ? "" : `/${caseId}`}`,
      { method: caseId === null ? "POST" : "PUT", body: JSON.stringify(body) }),
    onSuccess: (_case, { employeeId, packageId }) =>
      queryClient.invalidateQueries({ queryKey: remediationKey(employeeId, packageId) }),
  });
};

export const useApproveCompensationRemediationCase = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, packageId, caseId, expectedCaseVersion,
      expertReviewReference, reviewNotes }: {
      employeeId: number; packageId: number; caseId: number; expectedCaseVersion: number;
      expertReviewReference: string; reviewNotes: string;
    }) => apiFetch<CompensationRemediationCase>(
      `/employees/${employeeId}/compensation-packages/${packageId}/remediation-case/${caseId}/approve`,
      { method: "POST", body: JSON.stringify({ expectedCaseVersion, expertReviewReference,
        reviewNotes, confirmExpertReview: true }) }),
    onSuccess: (_case, { employeeId, packageId }) =>
      queryClient.invalidateQueries({ queryKey: remediationKey(employeeId, packageId) }),
  });
};

export const usePreviewCompensationDateCorrection = () =>
  useMutation({
    mutationFn: ({ employeeId, packageId, proposedEffectiveFrom }: {
      employeeId: number; packageId: number; proposedEffectiveFrom: string;
    }) => apiFetch<CompensationDateCorrectionPreview>(
      `/employees/${employeeId}/compensation-packages/${packageId}/date-correction-preview`,
      { method: "POST", body: JSON.stringify({ proposedEffectiveFrom }) }),
  });

export const useCorrectCompensationDate = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, packageId, proposedEffectiveFrom, expectedPackageVersion,
      expectedPreviousPackageId, expectedPreviousPackageVersion, reason }: {
      employeeId: number; packageId: number; proposedEffectiveFrom: string;
      expectedPackageVersion: number; expectedPreviousPackageId: number | null;
      expectedPreviousPackageVersion: number | null; reason: string;
    }) => apiFetch<EmployeeEmploymentFoundationDto>(
      `/employees/${employeeId}/compensation-packages/${packageId}/effective-date`,
      { method: "PUT", body: JSON.stringify({ proposedEffectiveFrom, expectedPackageVersion,
        expectedPreviousPackageId, expectedPreviousPackageVersion, reason }) }),
    onSuccess: (_foundation, { employeeId }) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "employment-foundation"] }),
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "compensation-packages"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useSaveOffboardingDraft = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, body }: { employeeId: number; body: SaveOffboardingDraftBody }) =>
      apiFetch<EmployeeOffboardingCaseDto>(`/employees/${employeeId}/offboarding-draft`, {
        method: "PUT",
        body: JSON.stringify(body),
      }),
    onSuccess: (_case, { employeeId }) => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "offboarding-context"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useTerminationCalculationRules = () =>
  useQuery({
    queryKey: ["termination-calculation-rules"],
    queryFn: () => apiFetch<TerminationCalculationRuleDto[]>("/termination-calculation-rules"),
  });

export const useGenerateOffboardingCalculationDraft = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, caseId, expectedVersion, confirmSeveranceEligibility,
      confirmNoticeEligibility, expertReviewReference }: {
      employeeId: number; caseId: number; expectedVersion: number;
      confirmSeveranceEligibility: boolean; confirmNoticeEligibility: boolean;
      expertReviewReference: string;
    }) => apiFetch<EmployeeOffboardingCaseDto>(
      `/employees/${employeeId}/offboarding/${caseId}/calculation-draft`,
      { method: "POST", body: JSON.stringify({ expectedVersion, confirmSeveranceEligibility,
        confirmNoticeEligibility, expertReviewReference }) },
    ),
    onSuccess: (_case, { employeeId }) =>
      queryClient.invalidateQueries({ queryKey: ["employees", employeeId, "offboarding-context"] }),
  });
};

export const useCompleteOffboarding = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, caseId, expectedVersion }: {
      employeeId: number;
      caseId: number;
      expectedVersion: number;
    }) => apiFetch<EmployeeOffboardingCaseDto>(
      `/employees/${employeeId}/offboarding/${caseId}/complete`,
      { method: "POST", body: JSON.stringify({ expectedVersion }) },
    ),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useReverseOffboarding = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, caseId, expectedVersion, reason }: {
      employeeId: number;
      caseId: number;
      expectedVersion: number;
      reason: string;
    }) => apiFetch<EmployeeOffboardingCaseDto>(
      `/employees/${employeeId}/offboarding/${caseId}/reverse`,
      { method: "POST", body: JSON.stringify({ expectedVersion, reason }) },
    ),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useUploadPhoto = () =>
  useMutation({
    mutationFn: ({ id, file }: { id: number; file: File }) => {
      const form = new FormData();
      form.append("file", file);
      return apiFetch<{ photoPath: string }>(`/employees/${id}/photo`, { method: "POST", body: form });
    },
  });

export const useUploadDocument = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, file, documentType }: { id: number; file: File; documentType: string }) => {
      const form = new FormData();
      form.append("file", file);
      form.append("documentType", documentType);
      return apiFetch<EmployeeDocumentDto>(`/employees/${id}/documents`, { method: "POST", body: form });
    },
    onSuccess: (_data, { id }) =>
      queryClient.invalidateQueries({ queryKey: ["employees", id, "documents"] }),
  });
};

export const useWithdrawDocument = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, documentId, reason }: { id: number; documentId: number; reason: string }) =>
      apiFetch<WithdrawnEmployeeDocumentDto>(`/employees/${id}/documents/${documentId}/withdraw`, {
        method: "POST", body: JSON.stringify({ reason }),
      }),
    onSuccess: (_result, { id }) =>
      queryClient.invalidateQueries({ queryKey: ["employees", id, "documents"] }),
  });
};

export type ImportRowIssue = components["schemas"]["ImportRowIssueDto"];
export type ImportPreviewDto = components["schemas"]["ImportPreviewDto"];
export type ImportResultDto = components["schemas"]["ImportResultDto"];

const dosyaGovdesi = (file: File): FormData => {
  const form = new FormData();
  form.append("file", file);
  return form;
};

export const usePreviewImport = () =>
  useMutation({
    mutationFn: (file: File) =>
      apiFetch<ImportPreviewDto>("/employees/import/preview", {
        method: "POST",
        body: dosyaGovdesi(file),
      }),
  });

export const useImportEmployees = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) =>
      apiFetch<ImportResultDto>("/employees/import", { method: "POST", body: dosyaGovdesi(file) }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["employees"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};
