import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { CreatePeriodModal } from "./PayrollPeriodGuidance";

const preview = { year: 2026, month: 10, name: "Ekim 2026", token: "reviewed-token", minimumWageIncomeTaxBaseBefore: 100,
  settings: { version: 7, effectiveFrom: "2026-01-01", sourceReference: "Uzman incelemesi" },
  rows: [{ employeeId: 1, name: "Ayşe Kaynak", employmentPeriodId: 2, compensationPackageId: null, compensationVersion: null,
    sourceReference: null, grossSalary: 0, premiumPay: 0, roadAllowance: 0, mealAllowance: 0, benefitPay: 0,
    workedDays: 30, overtimeHours: 2, previousTaxBase: 1000, ibanComplete: false, timesheetComplete: false }] };
function show() {
  renderPage(<AuthProvider><ToastProvider><CreatePeriodModal latest={{ id: 1, year: 2026, month: 9, status: "approved", name: "Eylül" }} onClose={() => {}} onCreated={() => {}} /></ToastProvider></AuthProvider>);
  return screen.getByRole("dialog", { name: "Yeni bordro dönemi" });
}
afterEach(() => { vi.unstubAllGlobals(); });

test.each([null, { ...preview, token: "" }, { ...preview, rows: null }])("eksik önizleme yanıtında başlangıç onayını kapalı tutar (%j)", async result => {
  stubApi({ "/api/payroll/periods/creation-preview": result });
  const dialog = show();
  await userEvent.click(within(dialog).getByRole("button", { name: "Gerçek başlangıç girdilerini önizle" }));
  expect(await within(dialog).findByRole("alert")).toHaveTextContent("Önizleme yanıtı eksik");
  expect(within(dialog).getByRole("checkbox")).toBeDisabled();
  expect(within(dialog).getByRole("button", { name: "Oluştur" })).toBeDisabled();
});

test("sunucu çakışmasında ayı korur, eski önizleme ve onayı kaldırır", async () => {
  stubApi({ "/api/payroll/periods/creation-preview": preview });
  const base = fetch;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (String(input) === "/api/payroll/periods" && init?.method === "POST")
      return new Response(JSON.stringify({ title: "Ücret paketi değişti; yeniden inceleyin." }), { status: 409 });
    return base(input, init);
  }));
  const dialog = show();
  await userEvent.click(within(dialog).getByRole("button", { name: "Gerçek başlangıç girdilerini önizle" }));
  expect(await within(dialog).findByText("Ayşe Kaynak")).toBeInTheDocument();
  expect(within(dialog).getByText(/Ay başında ücret paketi yok/)).toBeInTheDocument();
  await userEvent.click(within(dialog).getByRole("checkbox"));
  await userEvent.click(within(dialog).getByRole("button", { name: "Oluştur" }));
  expect(await within(dialog).findByRole("alert")).toHaveTextContent("Ücret paketi değişti");
  expect(within(dialog).getByLabelText("Ay")).toHaveValue("10");
  expect(within(dialog).getByRole("checkbox")).not.toBeChecked();
  expect(within(dialog).getByRole("checkbox")).toBeDisabled();
  const write = vi.mocked(fetch).mock.calls.find(([url, init]) => String(url) === "/api/payroll/periods" && init?.method === "POST");
  expect(JSON.parse(String(write?.[1]?.body))).toMatchObject({ expectedPreviewToken: preview.token });
});

test("ay değişirken eski önizleme yanıtı onay açamaz", async () => {
  let finish!: (response: Response) => void;
  const pending = new Promise<Response>(resolve => { finish = resolve; });
  stubApi({}); const base = fetch;
  vi.stubGlobal("fetch", vi.fn((input: RequestInfo | URL, init?: RequestInit) => String(input).includes("creation-preview") ? pending : base(input, init)));
  const dialog = show();
  await userEvent.click(within(dialog).getByRole("button", { name: "Gerçek başlangıç girdilerini önizle" }));
  await userEvent.selectOptions(within(dialog).getByLabelText("Ay"), "11");
  finish(new Response(JSON.stringify(preview), { status: 200 }));
  await waitFor(() => expect(within(dialog).getByRole("button", { name: "Oluştur" })).toBeDisabled());
  expect(within(dialog).getByRole("checkbox")).toBeDisabled();
  expect(within(dialog).queryByText("Ayşe Kaynak")).not.toBeInTheDocument();
});
