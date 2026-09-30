import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, test, vi } from "vitest";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { CompensationRemediationPanel } from "./CompensationRemediationPanel";

afterEach(() => vi.unstubAllGlobals());

const base = "/api/employees/1/compensation-packages/2/remediation-case";
const packageValues = {
  id: 2, version: 1, effectiveFrom: "2026-01-01", effectiveTo: null,
  monthlyGrossSalary: 50000, regularPremium: 0, roadAllowance: 0,
  mealAllowance: 0, otherRegularBenefits: 0, sourceReference: "İlk karar",
};
const impactSnapshot = {
  potentialPayrollRows: [{ rowId: 5, periodId: 4, year: 2026, month: 8,
    periodStatus: "approved", rowStatus: "approved", recordedGrossSalary: 52000 }],
  offboardingCases: [],
};
const oldCase = {
  id: 3, version: 2, status: "approved", packageVersion: 1,
  issueDescription: "İlk ücret hatası", proposedEffectiveFrom: null,
  proposedValues: { ...packageValues, monthlyGrossSalary: 52000 },
  impactSnapshot, openedByUserId: "ik", openedAtUtc: "2026-09-01T00:00:00Z",
  reviewedByUserId: "ik", reviewedAtUtc: "2026-09-02T00:00:00Z",
  expertReviewReference: "Uzman 1", reviewNotes: "İncelendi",
};
const nextCase = {
  ...oldCase, id: 4, issueDescription: "İkinci ücret hatası",
  proposedValues: { ...packageValues, monthlyGrossSalary: 54000 },
  expertReviewReference: "Uzman 2",
};

function renderWithRoutes(routes: Record<string, unknown>) {
  stubApi(routes);
  renderPage(<ToastProvider><CompensationRemediationPanel
    employeeId={1} packageId={2} /></ToastProvider>);
}

test("onaylı dosyayı değiştirmeden aynı paket için yeni uzman dosyası açar", async () => {
  renderWithRoutes({
    [base]: { package: packageValues, currentImpact: impactSnapshot,
      canOpen: true, case: oldCase, history: [oldCase] },
  });
  await userEvent.click(await screen.findByRole("button", {
    name: "Yeni uzman inceleme dosyası aç" }));
  expect(screen.getByLabelText("Önerilen aylık brüt maaş")).toHaveValue(52000);
  await userEvent.type(screen.getByLabelText("Hatanın açıklaması ve kaynağı"),
    "İkinci ücret hatası");
  await userEvent.click(screen.getByRole("button", { name: "İnceleme dosyasını aç" }));
  const request = vi.mocked(fetch).mock.calls.find(([url, init]) =>
    String(url) === base && init?.method === "POST");
  expect(request).toBeDefined();
  expect(JSON.parse(String(request?.[1]?.body))).toMatchObject({
    monthlyGrossSalary: 52000, issueDescription: "İkinci ücret hatası",
    expectedCaseVersion: null,
  });
});

test("ikinci sürümü geçerli sürümden üretir ve eski kanıtı geçmişte gösterir", async () => {
  const impactUrl = `${base}/4/payroll-periods/4/rows/5/approved-impact`;
  const firstRevision = {
    id: 10, revisionNumber: 1, payrollRowId: 5, remediationCaseId: 3,
    batchId: null, basedOnRevisionId: null,
    approvedAtUtc: "2026-09-03T00:00:00Z",
    before: { grossSalary: 50000, netPay: 35000 },
    after: { grossSalary: 52000, netPay: 36300 },
  };
  const secondRevision = {
    ...firstRevision, id: 11, revisionNumber: 2, remediationCaseId: 4,
    basedOnRevisionId: 10,
    before: firstRevision.after,
    after: { grossSalary: 54000, netPay: 37700 },
  };
  const amount = (grossEarnings: number, netPay: number) => ({
    grossEarnings, incomeTaxBase: 42500, incomeTax: 6000,
    netPay, employerCost: 60000,
  });
  renderWithRoutes({
    [base]: { package: packageValues, currentImpact: impactSnapshot,
      canOpen: true, case: nextCase, history: [nextCase, oldCase] },
    [impactUrl]: {
      periodId: 4, rowId: 5, periodStatus: "approved", remediationCaseId: 4,
      expertReviewReference: "Uzman 2", reviewedAtUtc: "2026-09-04T00:00:00Z",
      canEstimate: true, reviewWarnings: [],
      recorded: amount(52000, 36300), hypothetical: amount(54000, 37700),
      laterSameYearRows: [], laterApprovedRows: [], canApply: false,
      canApplyNext: true, blockingReasons: [], previewToken: "a".repeat(64),
      revision: firstRevision, revisionHistory: [firstRevision],
      batchRevisions: [], canApplyChain: false,
      chainImpact: { canEstimate: false, blockingReasons: [],
        previewToken: "", laterApprovedRows: [] },
    },
    [`${impactUrl}/revise`]: secondRevision,
    [`${base}/3/payroll-periods/4/rows/5/revisions/10/external-reconciliation`]: {
      revisionId: 10, revisionNumber: 1, originalNetPay: 35000,
      revisedNetPay: 36300, calculatedNetDifference: 1300,
      isCurrent: false, payment: null, notification: null, history: [],
    },
    [`${base}/4/payroll-periods/4/rows/5/revisions/11/external-reconciliation`]: {
      revisionId: 11, revisionNumber: 2, originalNetPay: 36300,
      revisedNetPay: 37700, calculatedNetDifference: 1400,
      isCurrent: true, payment: null, notification: null, history: [],
    },
  });
  const user = userEvent.setup();
  await user.click(await screen.findByRole("button", {
    name: "Onaylı satırın etki dosyasını aç" }));
  expect(await screen.findByRole("region", {
    name: "Sonraki bordro parasal revizyonu" })).toBeInTheDocument();
  await user.click(await screen.findByText("Uzman inceleme dosyası geçmişi (2)"));
  await user.click(screen.getByRole("button", { name: "Dosya #3 görüntüle" }));
  expect(screen.getByText(/Referans: Uzman 1/)).toBeInTheDocument();
  expect(screen.queryByRole("region", {
    name: "Sonraki bordro parasal revizyonu" })).not.toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Güncel dosyaya dön" }));
  await user.click(await screen.findByRole("button", {
    name: "Onaylı satırın etki dosyasını aç" }));
  const form = screen.getByRole("region", { name: "Sonraki bordro parasal revizyonu" });
  await user.type(within(form).getByLabelText("Son uzman inceleme referansı"), "Uzman 3");
  await user.type(within(form).getByLabelText("Uzman inceleme notu"), "İkinci fark incelendi");
  for (const checkbox of within(form).getAllByRole("checkbox")) await user.click(checkbox);
  await user.click(within(form).getByRole("button", {
    name: "Yeni geçerli parasal sürümü onayla" }));
  const request = vi.mocked(fetch).mock.calls.find(([url, init]) =>
    String(url) === `${impactUrl}/revise` && init?.method === "POST");
  expect(JSON.parse(String(request?.[1]?.body))).toMatchObject({
    expectedCurrentRevisionId: 10, expectedPreviewToken: "a".repeat(64),
  });
  expect(await screen.findByText(/Geçerli parasal sürüm #2/)).toBeInTheDocument();
  await user.click(screen.getByText("Önceki parasal sürümler ve kanıtları"));
  expect(screen.getAllByRole("region", {
    name: "Bordro dış ödeme ve bildirim uzlaştırması" })).toHaveLength(2);
});
