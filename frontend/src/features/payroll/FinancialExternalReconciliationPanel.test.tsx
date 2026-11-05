import { chooseOperationGuidance } from "../../test/chooseOperationGuidance";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { guidanceStepDrafts } from "../guidance/stepDrafts";
import { FinancialExternalReconciliationPanel } from "./FinancialExternalReconciliationPanel";

const path = "/api/payroll/periods/3/rows/7/external-reconciliation";
const source = { ...guidanceStepDrafts.find(x => x.contentKey === "daily-external-followup")!,
  id: 85, contentVersion: 2, state: "published", isCurrent: true };
const initial = { sourceKind: "payroll", resourceId: 7, originalTotal: 22000, canRecord: true,
  sourceToken: "original-token", payment: null, notification: null, history: [] };
beforeEach(() => {
  localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: {
    id: "u", name: "İK", role: "hr-admin", roleLabel: "İK yöneticisi", initials: "İK", employeeId: 5,
  } }));
  stubApi({ [path]: initial, "/api/guidance/catalog": [source], "/api/guidance/catalog/versions/85": source,
    "/api/guidance/help-preferences": [], "/api/guidance/operations": [],
    "/api/guidance/practices": [], "/api/guidance/daily-tasks": [] });
});
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); localStorage.clear(); });
const render = () => renderPage(<AuthProvider><ToastProvider><FinancialExternalReconciliationPanel source="payroll" parentId={3} resourceId={7} /></ToastProvider></AuthProvider>);
const writes = () => vi.mocked(fetch).mock.calls.filter(([url, init]) => String(url) === path && init?.method === "POST");

async function preparePayment() {
  const region = await screen.findByRole("region", { name: "Dış ödeme kanıtı" });
  await userEvent.selectOptions(within(region).getByLabelText("Sonuç"), "completed");
  await userEvent.type(within(region).getByLabelText("Sorumlu kişi"), "Muhasebe");
  await userEvent.type(within(region).getByLabelText("Belge / işlem referansı"), "BANK-22");
  await userEvent.type(within(region).getByLabelText("Dış işlem tarihi"), "2026-10-02");
  await userEvent.type(within(region).getByLabelText("Belgelenen gerçek ödeme tutarı"), "21500");
  await userEvent.click(within(region).getByRole("checkbox"));
  return region;
}

test("ilk ödeme gerçek kaynak belirteci ve seçilmiş dersle kaydolur; kaynak seçimi teyidi yeniletir", async () => {
  render(); const region = await preparePayment();
  const save = within(region).getByRole("button", { name: "Kanıtı kaydet" });
  expect(save).toBeEnabled();
  await chooseOperationGuidance(undefined, region);
  expect(save).toBeDisabled();
  await userEvent.click(within(region).getByRole("checkbox"));
  await userEvent.click(save);
  await waitFor(() => expect(writes()).toHaveLength(1));
  expect(JSON.parse(String(writes()[0][1]?.body))).toMatchObject({ kind: "payment", recordedAmount: 21500,
    evidenceReference: "BANK-22", expectedSourceToken: "original-token", guidanceContentRevisionId: 85,
    expectedLatestRecordId: null, confirmRecordedStatus: true });
  expect(screen.getByRole("region", { name: "Dış bildirim kanıtı" })).toHaveTextContent("sorumlu ve kanıt bekliyor");
});

test("çakışma formu korur; eski kanıt zorlanmaz ve kullanıcı güncel sonucu yeniden inceler", async () => {
  const base = globalThis.fetch;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => String(input) === path && init?.method === "POST"
    ? new Response(JSON.stringify({ title: "Kaynak değişti; yeniden inceleyin." }), { status: 409 }) : base(input, init)));
  render(); const region = await preparePayment();
  await userEvent.click(within(region).getByRole("button", { name: "Kanıtı kaydet" }));
  expect(await within(region).findByRole("alert")).toHaveTextContent("Kaynak değişti");
  expect(within(region).getByLabelText("Belge / işlem referansı")).toHaveValue("BANK-22");
  expect(within(region).getByLabelText("Belgelenen gerçek ödeme tutarı")).toHaveValue(21500);
  await userEvent.click(screen.getByRole("button", { name: "Güncel dış kanıtları yeniden incele" }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.filter(([url, init]) => String(url) === path && !init?.method).length).toBeGreaterThan(1));
  expect(writes()).toHaveLength(1);
});

test("geçmiş ilk hesap kanıtı okunabilir; yeni sürüm varken yeni kayıt düğmesi açılmaz", async () => {
  stubApi({ [path]: { ...initial, canRecord: false, history: [{ id: 4, kind: "payment", outcome: "completed",
    responsibleName: "Eski sorumlu", evidenceReference: "OLD-BANK", notes: "İlk ödeme belgesi" }] } });
  render(); await screen.findByText(/OLD-BANK/);
  expect(screen.getByText(/İlk hesap yeni dış kanıt kaydı için uygun değil/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Kanıtı kaydet" })).not.toBeInTheDocument();
  expect(writes()).toHaveLength(0);
});
