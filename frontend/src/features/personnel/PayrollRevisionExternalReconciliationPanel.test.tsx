import { AuthProvider } from "../../auth/AuthContext";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, test, vi } from "vitest";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { PayrollRevisionExternalReconciliationPanel } from "./PayrollRevisionExternalReconciliationPanel";

afterEach(() => vi.unstubAllGlobals());

test("bordro farkını gerçek ödemeden ayırır ve ödeme kanıtını ayrı kaydeder", async () => {
  const url = "/api/employees/1/compensation-packages/2/remediation-case/3"
    + "/payroll-periods/4/rows/5/revisions/6/external-reconciliation";
  stubApi({ [url]: {
    revisionId: 6, revisionNumber: 1, originalNetPay: 35000,
    revisedNetPay: 36300, calculatedNetDifference: 1300, isCurrent: true,
    payment: null, notification: null, history: [],
  } });
  renderPage(<AuthProvider><ToastProvider><PayrollRevisionExternalReconciliationPanel
    employeeId={1} packageId={2} caseId={3} periodId={4} rowId={5}
    revisionId={6} /></ToastProvider></AuthProvider>);
  expect(await screen.findByText(/Net fark gerçek ödenen tutar/)).toBeInTheDocument();
  expect(screen.getAllByText(/kanıt bekliyor/)).toHaveLength(2);
  const payment = screen.getByRole("region", { name: "Dış ödeme kanıtı" });
  await userEvent.selectOptions(within(payment).getByLabelText("Sonuç"), "completed");
  await userEvent.type(within(payment).getByLabelText("Sorumlu kişi"), "Muhasebe");
  await userEvent.type(within(payment).getByLabelText("Belge / işlem referansı"), "Banka-51");
  await userEvent.type(within(payment).getByLabelText("Dış işlem tarihi"), "2026-09-30");
  await userEvent.type(within(payment).getByLabelText("Belgelenen gerçek ödeme tutarı"), "1200");
  await userEvent.click(within(payment).getByRole("checkbox"));
  await userEvent.click(within(payment).getByRole("button", { name: "Yeni kanıt kaydı ekle" }));
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([u, init]) =>
      String(u).endsWith(url) && init?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toMatchObject({
      kind: "payment", outcome: "completed", responsibleName: "Muhasebe",
      evidenceReference: "Banka-51", actionDate: "2026-09-30",
      recordedAmount: 1200, expectedLatestRecordId: null,
      confirmRecordedStatus: true,
    });
  });
  expect(payment).toBeInTheDocument();
});
