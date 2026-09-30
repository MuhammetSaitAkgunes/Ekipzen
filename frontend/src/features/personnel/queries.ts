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
type PayrollRowSchema = components["schemas"]["PayrollRowDto"];
export type CompensationPayrollRow = PayrollRowSchema & Required<Pick<PayrollRowSchema,
  "grossSalary" | "premiumPay" | "roadAllowance" | "mealAllowance" |
  "benefitPay" | "grossEarnings" | "netPay">>;
export type CompensationPayrollApplication = components["schemas"]["CompensationPayrollApplicationDto"] & {
  id: number; before: CompensationPayrollRow; after: CompensationPayrollRow;
  appliedAtUtc: string;
};
export type CompensationPayrollPreview = components["schemas"]["CompensationPayrollPreviewDto"] & {
  canApply: boolean; blockingReasons: string[]; previewToken: string;
  before: CompensationPayrollRow; after: CompensationPayrollRow | null;
  application: CompensationPayrollApplication | null;
};
export type ApprovedCompensationPayrollImpact = {
  periodId: number; rowId: number; periodStatus: string; remediationCaseId: number;
  expertReviewReference: string; reviewedAtUtc: string; canEstimate: boolean;
  reviewWarnings: string[];
  recorded: { grossEarnings: number; incomeTaxBase: number; incomeTax: number;
    netPay: number; employerCost: number };
  hypothetical: ApprovedCompensationPayrollImpact["recorded"] | null;
  laterSameYearRows: Array<{ periodId: number; rowId: number; year: number;
    month: number; status: string }>;
  laterApprovedRows: Array<{ periodId: number; rowId: number; year: number;
    month: number; status: string }>;
  canApply: boolean; blockingReasons: string[]; previewToken: string;
  revision: ApprovedPayrollRevision | null;
  chainImpact: {
    canEstimate: boolean; blockingReasons: string[]; previewToken: string;
    laterApprovedRows: Array<{
      periodId: number; rowId: number; year: number; month: number;
      recordedPreviousTaxBase: number; projectedPreviousTaxBase: number | null;
      recorded: ApprovedCompensationPayrollImpact["recorded"] | null;
      projected: ApprovedCompensationPayrollImpact["recorded"] | null;
      blockingReason: string | null;
    }>;
  };
  canApplyChain: boolean;
  batchRevisions: ApprovedPayrollRevisionBatchRow[];
};
export type ApprovedPayrollRevision = {
  id: number; revisionNumber: number; payrollRowId: number; remediationCaseId: number;
  before: CompensationPayrollRow; after: CompensationPayrollRow;
  reason: string; expertReviewReference: string; reviewNotes: string;
  approvedByUserId: string; approvedAtUtc: string; batchId: number | null;
};
export type ApprovedPayrollRevisionBatchRow = {
  periodId: number; revision: ApprovedPayrollRevision;
};
export type ApprovedPayrollRevisionBatch = {
  id: number; employeeId: number; sourcePayrollRowId: number; year: number;
  expertReviewReference: string; reviewNotes: string;
  approvedByUserId: string; approvedAtUtc: string;
  rows: ApprovedPayrollRevisionBatchRow[];
};
export type PayrollRevisionExternalAction = {
  id: number; kind: "payment" | "notification";
  outcome: "pending" | "completed" | "not-required"; previousRecordId: number | null;
  responsibleName: string; evidenceReference: string; actionDate: string | null;
  recordedAmount: number | null; notes: string; recordedByUserId: string;
  recordedAtUtc: string;
};
export type PayrollRevisionExternalReconciliation = {
  revisionId: number; revisionNumber: number; originalNetPay: number;
  revisedNetPay: number; calculatedNetDifference: number; isCurrent: boolean;
  payment: PayrollRevisionExternalAction | null;
  notification: PayrollRevisionExternalAction | null;
  history: PayrollRevisionExternalAction[];
};
type OffboardingSettlementValuesSchema = components["schemas"]["OffboardingSettlementValuesDto"];
export type OffboardingSettlementValues = OffboardingSettlementValuesSchema & {
  monthlyGrossSalarySnapshot: number; regularPremiumSnapshot: number;
  roadAllowanceSnapshot: number; mealAllowanceSnapshot: number;
  otherRegularBenefitsSnapshot: number; unusedLeaveDays: number;
  unusedLeavePayment: number; severancePayment: number; noticePayment: number;
  totalPayment: number; settlementSource: string; settlementReference: string;
  settlementNotes: string | null;
};
type OffboardingSettlementRevisionSchema = components["schemas"]["OffboardingSettlementRevisionDto"];
export type OffboardingSettlementRevision = OffboardingSettlementRevisionSchema & {
  id: number; revisionNumber: number; version: number; status: "draft" | "approved";
  remediationCaseId: number; basedOnApprovedRevisionId: number | null;
  values: OffboardingSettlementValues; reason: string;
  openedByUserId: string; openedAtUtc: string; approvedByUserId: string | null;
  approvedAtUtc: string | null; expertReviewReference: string | null;
  reviewNotes: string | null;
};
type OffboardingSettlementRevisionContextSchema = components["schemas"]["OffboardingSettlementRevisionContextDto"];
export type OffboardingSettlementRevisionContext = Omit<OffboardingSettlementRevisionContextSchema,
  "original" | "effective" | "draft" | "history"> & {
  offboardingCaseId: number; offboardingCaseVersion: number; remediationCaseId: number;
  original: OffboardingSettlementValues; effective: OffboardingSettlementValues;
  draft: OffboardingSettlementRevision | null;
  history: OffboardingSettlementRevision[];
  canOpen: boolean; blockingReasons: string[];
};
type ExternalActionSchema = components["schemas"]["OffboardingExternalActionDto"];
export type OffboardingExternalAction = ExternalActionSchema & {
  id: number; kind: "payment" | "notification";
  outcome: "pending" | "completed" | "not-required"; previousRecordId: number | null;
  responsibleName: string; evidenceReference: string; actionDate: string | null;
  recordedAmount: number | null; notes: string;
  recordedByUserId: string; recordedAtUtc: string;
};
type ExternalReconciliationSchema = components["schemas"]["OffboardingExternalReconciliationDto"];
export type OffboardingExternalReconciliation = Omit<ExternalReconciliationSchema,
  "payment" | "notification" | "history"> & {
  settlementRevisionId: number; revisionNumber: number;
  originalTotal: number; previousEffectiveTotal: number;
  revisedTotal: number; calculationDifference: number;
  isCurrent: boolean; payment: OffboardingExternalAction | null;
  notification: OffboardingExternalAction | null;
  history: OffboardingExternalAction[];
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

const payrollRemediationUrl = (employeeId: number, packageId: number, caseId: number,
  periodId: number, rowId: number) =>
  `/employees/${employeeId}/compensation-packages/${packageId}/remediation-case/${caseId}`
  + `/payroll-periods/${periodId}/rows/${rowId}`;

export type PayrollRevisionExternalArgs = {
  employeeId: number; packageId: number; caseId: number; periodId: number;
  rowId: number; revisionId: number;
};
const payrollRevisionExternalUrl = (args: PayrollRevisionExternalArgs) =>
  `${payrollRemediationUrl(args.employeeId, args.packageId, args.caseId,
    args.periodId, args.rowId)}/revisions/${args.revisionId}/external-reconciliation`;
const payrollRevisionExternalKey = (args: PayrollRevisionExternalArgs) =>
  ["employees", args.employeeId, "payroll-revision", args.revisionId,
    "external-reconciliation"] as const;

export const usePayrollRevisionExternalReconciliation = (args: PayrollRevisionExternalArgs) =>
  useQuery({ queryKey: payrollRevisionExternalKey(args),
    queryFn: () => apiFetch<PayrollRevisionExternalReconciliation>(
      payrollRevisionExternalUrl(args)) });

export const useRecordPayrollRevisionExternalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: PayrollRevisionExternalArgs & {
      kind: "payment" | "notification";
      outcome: "pending" | "completed" | "not-required";
      responsibleName: string; evidenceReference: string; actionDate: string | null;
      recordedAmount: number | null; notes: string; expectedLatestRecordId: number | null;
    }) => apiFetch<PayrollRevisionExternalAction>(payrollRevisionExternalUrl(args), {
      method: "POST", body: JSON.stringify({
        kind: args.kind, outcome: args.outcome,
        responsibleName: args.responsibleName,
        evidenceReference: args.evidenceReference,
        actionDate: args.actionDate, recordedAmount: args.recordedAmount,
        notes: args.notes, expectedLatestRecordId: args.expectedLatestRecordId,
        confirmRecordedStatus: true,
      }),
    }),
    onSuccess: (_record, args) => Promise.all([
      queryClient.invalidateQueries({ queryKey: payrollRevisionExternalKey(args) }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const usePreviewCompensationPayrollApplication = () => useMutation({
  mutationFn: ({ employeeId, packageId, caseId, periodId, rowId }: {
    employeeId: number; packageId: number; caseId: number; periodId: number; rowId: number;
  }) => apiFetch<CompensationPayrollPreview>(
    `${payrollRemediationUrl(employeeId, packageId, caseId, periodId, rowId)}/preview`),
});

export const useApprovedCompensationPayrollImpact = () => useMutation({
  mutationFn: ({ employeeId, packageId, caseId, periodId, rowId }: {
    employeeId: number; packageId: number; caseId: number; periodId: number; rowId: number;
  }) => apiFetch<ApprovedCompensationPayrollImpact>(
    `${payrollRemediationUrl(employeeId, packageId, caseId, periodId, rowId)}/approved-impact`),
});

export const useApplyApprovedCompensationPayrollRevision = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, packageId, caseId, periodId, rowId,
      expectedPreviewToken, expertReviewReference, reviewNotes }: {
      employeeId: number; packageId: number; caseId: number; periodId: number; rowId: number;
      expectedPreviewToken: string; expertReviewReference: string; reviewNotes: string;
    }) => apiFetch<ApprovedPayrollRevision>(
      `${payrollRemediationUrl(employeeId, packageId, caseId, periodId, rowId)}/approved-impact/apply`,
      { method: "POST", body: JSON.stringify({ expectedPreviewToken, expertReviewReference,
        reviewNotes, confirmSourceLink: true, confirmExternalReconciliation: true }) }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useApplyApprovedCompensationPayrollChain = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, packageId, caseId, periodId, rowId,
      expectedPreviewToken, expertReviewReference, reviewNotes }: {
      employeeId: number; packageId: number; caseId: number; periodId: number; rowId: number;
      expectedPreviewToken: string; expertReviewReference: string; reviewNotes: string;
    }) => apiFetch<ApprovedPayrollRevisionBatch>(
      `${payrollRemediationUrl(employeeId, packageId, caseId, periodId, rowId)}/approved-impact/chain/apply`,
      { method: "POST", body: JSON.stringify({ expectedPreviewToken, expertReviewReference,
        reviewNotes, confirmSourceLink: true, confirmChainImpact: true,
        confirmExternalReconciliation: true }) }),
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

export const useApplyCompensationPayrollApplication = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ employeeId, packageId, caseId, periodId, rowId, expectedPreviewToken }: {
      employeeId: number; packageId: number; caseId: number; periodId: number; rowId: number;
      expectedPreviewToken: string;
    }) => apiFetch<CompensationPayrollApplication>(
      `${payrollRemediationUrl(employeeId, packageId, caseId, periodId, rowId)}/apply`,
      { method: "POST", body: JSON.stringify({ expectedPreviewToken, confirmSourceLink: true }) }),
    onSuccess: (_application, { employeeId, packageId }) => Promise.all([
      queryClient.invalidateQueries({ queryKey: remediationKey(employeeId, packageId) }),
      queryClient.invalidateQueries({ queryKey: ["payroll"] }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
  });
};

const settlementRevisionUrl = (employeeId: number, packageId: number,
  remediationCaseId: number, offboardingCaseId: number) =>
  `/employees/${employeeId}/compensation-packages/${packageId}`
  + `/remediation-case/${remediationCaseId}/offboarding/${offboardingCaseId}/settlement-revisions`;
const settlementRevisionKey = (employeeId: number, packageId: number,
  remediationCaseId: number, offboardingCaseId: number) =>
  ["employees", employeeId, "compensation-packages", packageId,
    "remediation-case", remediationCaseId, "offboarding", offboardingCaseId,
    "settlement-revisions"] as const;
type SettlementRevisionArgs = {
  employeeId: number; packageId: number; remediationCaseId: number;
  offboardingCaseId: number;
};

export const useOffboardingSettlementRevisions = (args: SettlementRevisionArgs) =>
  useQuery({
    queryKey: settlementRevisionKey(args.employeeId, args.packageId,
      args.remediationCaseId, args.offboardingCaseId),
    queryFn: () => apiFetch<OffboardingSettlementRevisionContext>(
      settlementRevisionUrl(args.employeeId, args.packageId,
        args.remediationCaseId, args.offboardingCaseId)),
  });

const invalidateSettlementRevision = (queryClient: ReturnType<typeof useQueryClient>,
  args: SettlementRevisionArgs) => Promise.all([
  queryClient.invalidateQueries({ queryKey: settlementRevisionKey(args.employeeId,
    args.packageId, args.remediationCaseId, args.offboardingCaseId) }),
  queryClient.invalidateQueries({ queryKey: ["employees", args.employeeId, "offboarding-context"] }),
]);

export const useOpenOffboardingSettlementRevision = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: SettlementRevisionArgs & { expectedOffboardingVersion: number }) =>
      apiFetch<OffboardingSettlementRevision>(settlementRevisionUrl(args.employeeId,
        args.packageId, args.remediationCaseId, args.offboardingCaseId),
      { method: "POST", body: JSON.stringify({
        expectedOffboardingVersion: args.expectedOffboardingVersion }) }),
    onSuccess: (_revision, args) => invalidateSettlementRevision(queryClient, args),
  });
};

export const useSaveOffboardingSettlementRevision = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: SettlementRevisionArgs & { revisionId: number;
      expectedRevisionVersion: number; unusedLeaveDays: number;
      unusedLeavePayment: number; severancePayment: number; noticePayment: number;
      reason: string; settlementReference: string; settlementNotes: string | null }) =>
      apiFetch<OffboardingSettlementRevision>(
        `${settlementRevisionUrl(args.employeeId, args.packageId,
          args.remediationCaseId, args.offboardingCaseId)}/${args.revisionId}`,
        { method: "PUT", body: JSON.stringify({
          expectedRevisionVersion: args.expectedRevisionVersion,
          unusedLeaveDays: args.unusedLeaveDays,
          unusedLeavePayment: args.unusedLeavePayment,
          severancePayment: args.severancePayment,
          noticePayment: args.noticePayment,
          reason: args.reason,
          settlementReference: args.settlementReference,
          settlementNotes: args.settlementNotes,
        }) }),
    onSuccess: (_revision, args) => invalidateSettlementRevision(queryClient, args),
  });
};

export const useApproveOffboardingSettlementRevision = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: SettlementRevisionArgs & { revisionId: number;
      expectedRevisionVersion: number; expertReviewReference: string;
      reviewNotes: string }) =>
      apiFetch<OffboardingSettlementRevision>(
        `${settlementRevisionUrl(args.employeeId, args.packageId,
          args.remediationCaseId, args.offboardingCaseId)}/${args.revisionId}/approve`,
        { method: "POST", body: JSON.stringify({
          expectedRevisionVersion: args.expectedRevisionVersion,
          expertReviewReference: args.expertReviewReference,
          reviewNotes: args.reviewNotes,
          confirmFinancialReview: true,
          confirmExternalReconciliation: true,
        }) }),
    onSuccess: (_revision, args) => invalidateSettlementRevision(queryClient, args),
  });
};

type ExternalReconciliationArgs = SettlementRevisionArgs & { revisionId: number };
const externalReconciliationUrl = (args: ExternalReconciliationArgs) =>
  `${settlementRevisionUrl(args.employeeId, args.packageId,
    args.remediationCaseId, args.offboardingCaseId)}/${args.revisionId}/external-reconciliation`;
const externalReconciliationKey = (args: ExternalReconciliationArgs) =>
  [...settlementRevisionKey(args.employeeId, args.packageId,
    args.remediationCaseId, args.offboardingCaseId), args.revisionId,
  "external-reconciliation"] as const;

export const useOffboardingExternalReconciliation = (args: ExternalReconciliationArgs) =>
  useQuery({ queryKey: externalReconciliationKey(args),
    queryFn: () => apiFetch<OffboardingExternalReconciliation>(externalReconciliationUrl(args)) });

export const useRecordOffboardingExternalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (args: ExternalReconciliationArgs & {
      kind: "payment" | "notification"; outcome: "pending" | "completed" | "not-required";
      responsibleName: string; evidenceReference: string; actionDate: string | null;
      recordedAmount: number | null; notes: string; expectedLatestRecordId: number | null;
    }) => apiFetch<OffboardingExternalAction>(externalReconciliationUrl(args), {
      method: "POST", body: JSON.stringify({
        kind: args.kind, outcome: args.outcome,
        responsibleName: args.responsibleName,
        evidenceReference: args.evidenceReference,
        actionDate: args.actionDate, recordedAmount: args.recordedAmount,
        notes: args.notes, expectedLatestRecordId: args.expectedLatestRecordId,
        confirmRecordedStatus: true,
      }),
    }),
    onSuccess: (_record, args) => Promise.all([
      queryClient.invalidateQueries({ queryKey: externalReconciliationKey(args) }),
      queryClient.invalidateQueries({ queryKey: dailyTasksKey }),
    ]),
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
