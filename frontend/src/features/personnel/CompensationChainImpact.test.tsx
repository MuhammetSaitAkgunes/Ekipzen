import { AuthProvider } from "../../auth/AuthContext";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, test, vi } from "vitest";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { CompensationRemediationPanel } from "./CompensationRemediationPanel";

afterEach(() => vi.unstubAllGlobals());

const packageValues = {
  id: 2, version: 1, effectiveFrom: "2026-08-01", effectiveTo: null,
  monthlyGrossSalary: 50000, regularPremium: 0, roadAllowance: 0,
  mealAllowance: 0, otherRegularBenefits: 0, sourceReference: "İlk karar",
};
const rowUrl = "/api/employees/1/compensation-packages/2/remediation-case/3"
  + "/payroll-periods/4/rows/5/approved-impact";

function renderImpact(blocked: boolean) {
  const impactSnapshot = {
    potentialPayrollRows: [{ rowId: 5, periodId: 4, year: 2026, month: 8,
      periodStatus: "approved", rowStatus: "approved", recordedGrossSalary: 50000 }],
    offboardingCases: [],
  };
  const amount = (incomeTax: number, netPay: number, incomeTaxBase = 42500) => ({
    grossEarnings: 50000, incomeTaxBase, incomeTax, netPay, employerCost: 61000,
  });
  stubApi({
    "/api/employees/1/compensation-packages/2/remediation-case": {
      package: packageValues, canOpen: false, currentImpact: impactSnapshot,
      case: { id: 3, version: 2, status: "approved", packageVersion: 1,
        issueDescription: "Yanlış ücret", proposedEffectiveFrom: null,
        proposedValues: { ...packageValues, monthlyGrossSalary: 52000 },
        impactSnapshot, openedByUserId: "ik", openedAtUtc: "2026-09-01T00:00:00Z",
        reviewedByUserId: "ik", reviewedAtUtc: "2026-09-02T00:00:00Z",
        expertReviewReference: "Uzman 1", reviewNotes: "İncelendi" },
    },
    [rowUrl]: {
      periodId: 4, rowId: 5, periodStatus: "approved", remediationCaseId: 3,
      expertReviewReference: "Uzman 1", reviewedAtUtc: "2026-09-02T00:00:00Z",
      canEstimate: true, reviewWarnings: [],
      recorded: amount(6300, 35000), hypothetical: amount(6550, 36200, 44200),
      laterSameYearRows: [{ periodId: 6, rowId: 7, year: 2026, month: 9,
        status: "approved" }],
      laterApprovedRows: [{ periodId: 6, rowId: 7, year: 2026, month: 9,
        status: "approved" }],
      canApply: false, blockingReasons: ["Sonraki onaylı satır var."],
      previewToken: "a".repeat(64), revision: null,
      canApplyChain: !blocked, batchRevisions: [],
      chainImpact: {
        canEstimate: !blocked, previewToken: "b".repeat(64),
        blockingReasons: blocked ? ["2026/09 bordrosu: saklı önceki vergi matrahı uyuşmuyor."] : [],
        laterApprovedRows: [{ periodId: 6, rowId: 7, year: 2026, month: 9,
          recordedPreviousTaxBase: 42500, projectedPreviousTaxBase: blocked ? null : 44200,
          recorded: amount(6400, 34000), projected: blocked ? null : amount(6650, 33750),
          blockingReason: blocked ? "saklı önceki vergi matrahı uyuşmuyor." : null }],
      },
    },
  });
  renderPage(<AuthProvider><ToastProvider><CompensationRemediationPanel employeeId={1}
    packageId={2} /></ToastProvider></AuthProvider>);
}

test("sonraki onaylı ayın zincirleme net hesabını karar öncesi gösterir", async () => {
  renderImpact(false);
  await userEvent.click(await screen.findByRole("button", { name: "Onaylı satırın etki dosyasını aç" }));
  const dossier = await screen.findByRole("region", { name: "Zincirleme bordro etki dosyası" });
  expect(within(dossier).getByText(/2026\/09/)).toBeInTheDocument();
  expect(within(dossier).getByText(/toplam olası net hesap farkı/)).toBeInTheDocument();
  expect(within(dossier).getByText(/ödeme veya düzeltme beyannamesi tutarı değildir/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Yeni parasal sürümü onayla" }))
    .not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Tüm ayların parasal sürümlerini onayla" }))
    .toBeDisabled();
});

test("matrah tutarsızsa zincir toplamını üretmez", async () => {
  renderImpact(true);
  await userEvent.click(await screen.findByRole("button", { name: "Onaylı satırın etki dosyasını aç" }));
  const dossier = await screen.findByRole("region", { name: "Zincirleme bordro etki dosyası" });
  expect(within(dossier).getAllByText(/önceki vergi matrahı uyuşmuyor/).length).toBeGreaterThan(0);
  expect(within(dossier).queryByText(/toplam olası net hesap farkı/)).not.toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Tüm ayların parasal sürümlerini onayla" }))
    .not.toBeInTheDocument();
});

test("tek uzman onayıyla oluşan her ay için ayrı dış işlem paneli açar", async () => {
  renderImpact(false);
  const fetchMock = vi.mocked(fetch);
  const originalFetch = fetchMock.getMockImplementation()!;
  fetchMock.mockImplementation(async (input, init) => {
    const path = String(input);
    if (path === `${rowUrl}/chain/apply`) {
      expect(init?.method).toBe("POST");
      const body = JSON.parse(String(init?.body));
      expect(body.expectedPreviewToken).toBe("b".repeat(64));
      expect(body.confirmChainImpact).toBe(true);
      return new Response(JSON.stringify({ id: 9, rows: [
        { periodId: 4, revision: { id: 10, payrollRowId: 5, revisionNumber: 1,
          batchId: 9, approvedAtUtc: "2026-10-01T00:00:00Z" } },
        { periodId: 6, revision: { id: 11, payrollRowId: 7, revisionNumber: 1,
          batchId: 9, approvedAtUtc: "2026-10-01T00:00:00Z" } },
      ] }), { status: 200, headers: { "Content-Type": "application/json" } });
    }
    if (path.includes("/external-reconciliation")) {
      return new Response(JSON.stringify({ revisionId: path.includes("/10/") ? 10 : 11,
        originalNetPay: 35000, revisedNetPay: 36000, calculatedNetDifference: 1000,
        isCurrent: true, payment: null, notification: null, history: [] }),
      { status: 200, headers: { "Content-Type": "application/json" } });
    }
    return originalFetch(input, init);
  });
  const user = userEvent.setup();
  await user.click(await screen.findByRole("button", { name: "Onaylı satırın etki dosyasını aç" }));
  const form = screen.getByRole("region", { name: "Zincirleme bordro parasal revizyonu" });
  await user.type(within(form).getByLabelText("Son uzman inceleme referansı"), "Uzman 2");
  await user.type(within(form).getByLabelText("Uzman inceleme notu"), "Aylar incelendi");
  for (const checkbox of within(form).getAllByRole("checkbox")) await user.click(checkbox);
  await user.click(within(form).getByRole("button", {
    name: "Tüm ayların parasal sürümlerini onayla" }));
  expect(await screen.findByRole("region", { name: "Zincirleme sürümlerin dış işlem takibi" }))
    .toBeInTheDocument();
  expect(screen.getAllByRole("region", { name: "Bordro dış ödeme ve bildirim uzlaştırması" }))
    .toHaveLength(2);
  expect(screen.getByLabelText("Sorumlu kişi", { selector: "input#payroll-external-10-payment-owner" }))
    .toBeInTheDocument();
  expect(screen.getByLabelText("Sorumlu kişi", { selector: "input#payroll-external-11-payment-owner" }))
    .toBeInTheDocument();
});
