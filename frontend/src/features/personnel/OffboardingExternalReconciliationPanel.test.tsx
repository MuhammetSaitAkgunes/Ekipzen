import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, test, vi } from "vitest";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { OffboardingExternalReconciliationPanel } from "./OffboardingExternalReconciliationPanel";

const path = "/api/employees/1/compensation-packages/2/remediation-case/11/offboarding/21/settlement-revisions/31/external-reconciliation";
const args = { employeeId: 1, packageId: 2, remediationCaseId: 11,
  offboardingCaseId: 21, revisionId: 31 };
const context = {
  settlementRevisionId: 31, revisionNumber: 1, originalTotal: 1500,
  previousEffectiveTotal: 1500,
  revisedTotal: 2000, calculationDifference: 500, isCurrent: true,
  payment: null, notification: null, history: [],
};

afterEach(() => vi.unstubAllGlobals());

test("işlem bitmeden ödeme sorumlusunu bekliyor durumunda atar", async () => {
  stubApi({ [path]: context });
  renderPage(<ToastProvider><OffboardingExternalReconciliationPanel {...args} /></ToastProvider>);
  await screen.findByRole("region", { name: "Dış ödeme kanıtı" });
  await userEvent.type(screen.getByLabelText("Sorumlu kişi", { selector: "#external-payment-owner" }), "Bordro uzmanı");
  const section = screen.getByRole("region", { name: "Dış ödeme kanıtı" });
  await userEvent.click(section.querySelector('input[type="checkbox"]')!);
  await userEvent.click(section.querySelector("button")!);
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === path && init?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toMatchObject({
      kind: "payment", outcome: "pending", responsibleName: "Bordro uzmanı",
      evidenceReference: "", actionDate: null, recordedAmount: null,
    });
  });
});

test("ödeme ve bildirimi ayrı kanıtlarla kaydeder; teyit olmadan göndermez", async () => {
  stubApi({ [path]: context });
  renderPage(<ToastProvider><OffboardingExternalReconciliationPanel {...args} /></ToastProvider>);
  expect(await screen.findByRole("region", { name: "Dış ödeme kanıtı" })).toHaveTextContent("sorumlu ve kanıt bekliyor");
  expect(screen.getByRole("region", { name: "Dış bildirim kanıtı" })).toHaveTextContent("sorumlu ve kanıt bekliyor");
  expect(screen.getByText(/Bu fark ödenecek net tutar değildir/)).toBeInTheDocument();

  await userEvent.selectOptions(screen.getByLabelText("Sonuç", { selector: "#external-payment-outcome" }), "completed");
  await userEvent.type(screen.getByLabelText("Sorumlu kişi", { selector: "#external-payment-owner" }), "Bordro uzmanı");
  await userEvent.type(screen.getByLabelText("Belge / işlem referansı", { selector: "#external-payment-reference" }), "Dekont 123");
  await userEvent.type(screen.getByLabelText("Dış işlem tarihi", { selector: "#external-payment-date" }), "2025-07-02");
  await userEvent.type(screen.getByLabelText("Belgelenen gerçek ödeme tutarı"), "420");
  const paymentSection = screen.getByRole("region", { name: "Dış ödeme kanıtı" });
  const paymentSave = paymentSection.querySelector("button")!;
  expect(paymentSave).toBeDisabled();
  await userEvent.click(paymentSection.querySelector('input[type="checkbox"]')!);
  await userEvent.click(paymentSave);
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === path && init?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toEqual({
      kind: "payment", outcome: "completed", responsibleName: "Bordro uzmanı",
      evidenceReference: "Dekont 123", actionDate: "2025-07-02",
      recordedAmount: 420, notes: "", expectedLatestRecordId: null,
      confirmRecordedStatus: true,
    });
  });

  await userEvent.selectOptions(screen.getByLabelText("Sonuç", { selector: "#external-notification-outcome" }), "not-required");
  await userEvent.type(screen.getByLabelText("Sorumlu kişi", { selector: "#external-notification-owner" }), "İK yöneticisi");
  await userEvent.type(screen.getByLabelText("Belge / işlem referansı", { selector: "#external-notification-reference" }), "İnceleme 55");
  await userEvent.type(screen.getByLabelText("İnceleme / gerekçe notu", { selector: "#external-notification-notes" }), "Düzeltme bildirimi gerekmiyor.");
  const notificationSection = screen.getByRole("region", { name: "Dış bildirim kanıtı" });
  await userEvent.click(notificationSection.querySelector('input[type="checkbox"]')!);
  await userEvent.click(notificationSection.querySelector("button")!);
  await waitFor(() => {
    const requests = vi.mocked(fetch).mock.calls.filter(([url, init]) =>
      String(url) === path && init?.method === "POST");
    expect(requests).toHaveLength(2);
    expect(JSON.parse(String(requests[1][1]?.body))).toMatchObject({
      kind: "notification", outcome: "not-required",
      actionDate: null, recordedAmount: null,
      evidenceReference: "İnceleme 55",
    });
  });
});

test("geçmiş parasal sürümün dış kanıtlarını salt okunur gösterir", async () => {
  stubApi({ [path]: { ...context, isCurrent: false } });
  renderPage(<ToastProvider><OffboardingExternalReconciliationPanel {...args} /></ToastProvider>);
  expect(await screen.findByText(/Bu parasal sürüm geçmişte kaldı/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Sorumluyu kaydet" })).not.toBeInTheDocument();
});
