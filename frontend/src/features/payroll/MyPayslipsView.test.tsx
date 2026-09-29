import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { formatPayrollMoney } from "./format";
import { MyPayslipsView } from "./MyPayslipsView";

const slips = [
  { periodId: 1, rowId: 11, periodName: "Haziran 2026", grossEarnings: 118000, totalDeductions: 38000, netPay: 80000, approvalStatus: "Onaylandı" },
  { periodId: 2, rowId: 22, periodName: "Mayıs 2026", grossEarnings: 110000, totalDeductions: 35000, netPay: 75000, approvalStatus: "Onaylandı" },
];

beforeEach(() => stubApi({ "/api/payroll/my": slips, "/api/payroll/periods/1/rows/11/payslip": {} }));
afterEach(() => vi.unstubAllGlobals());

const renderView = () =>
  renderPage(
    <ToastProvider>
      <MyPayslipsView />
    </ToastProvider>,
  );

test("pusula listesi backend'den dolar", async () => {
  renderView();
  expect(await screen.findByText("Haziran 2026")).toBeInTheDocument();
  expect(screen.getAllByText(formatPayrollMoney(80000)).length).toBeGreaterThan(0);
  expect(screen.getAllByText("Onaylandı")).toHaveLength(2);
});

test("pusula PDF'i indirme ucuna gider", async () => {
  vi.stubGlobal("URL", { ...URL, createObjectURL: vi.fn(() => "blob:x"), revokeObjectURL: vi.fn() });
  renderView();
  await screen.findByText("Haziran 2026");
  await userEvent.click(screen.getAllByTitle("Pusulayı indir")[0]);
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/1/rows/11/payslip");
    expect(hit).toBe(true);
  });
});

test("düzeltme sürümünde ilk onaylı pusula da indirilebilir", async () => {
  stubApi({ "/api/payroll/my": [{ ...slips[0], hasRevision: true }],
    "/api/payroll/periods/1/rows/11/payslip?original=true": {} });
  vi.stubGlobal("URL", { ...URL, createObjectURL: vi.fn(() => "blob:x"), revokeObjectURL: vi.fn() });
  renderView();
  expect(await screen.findByText("Geçerli düzeltme sürümü")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", {
    name: "Haziran 2026 ilk onaylı bordro pusulasını indir",
  }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([u]) =>
    String(u) === "/api/payroll/periods/1/rows/11/payslip?original=true")).toBe(true));
});

test("boş listede bilgi metni görünür", async () => {
  stubApi({ "/api/payroll/my": [] });
  renderView();
  expect(await screen.findByText("Henüz bordro kaydınız yok.")).toBeInTheDocument();
});
