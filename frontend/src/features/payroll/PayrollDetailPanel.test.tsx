import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { PayrollDetailPanel } from "./PayrollDetailPanel";

const baseRow = {
  id: 7, employeeId: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım",
  grossSalary: 118000, workedDays: 30, overtimeHours: 12, premiumPay: 3500,
  roadAllowance: 1200, mealAllowance: 1800, benefitPay: 3200, specialDeductions: 1800,
  previousTaxBase: 312000, ibanComplete: true, timesheetComplete: true,
  approvalStatus: "Kontrol", notes: "SGK tavanına yakın kazanç.",
  hourlyRate: 524, overtimePay: 9440, baseGross: 118000, grossEarnings: 137140,
  sgkBase: 137140, sgkEmployee: 19200, unemploymentEmployee: 1371, incomeTaxBase: 116569,
  incomeTax: 25000, stampTax: 790, totalDeductions: 48161, netPay: 88979,
  employerSgk: 28114, employerUnemployment: 2743, employerCost: 167997, warnings: ["Kontrol bekliyor"],
};

beforeEach(() =>
  stubApi({
    "/api/payroll/periods/3/rows/7": { ...baseRow },
    "/api/payroll/periods/3/rows/7/approve": { ...baseRow, approvalStatus: "Onaylandı" },
    "/api/payroll/periods/3/rows/7/payslip": {},
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderPanel = (row = baseRow) =>
  renderPage(
    <ToastProvider>
      <PayrollDetailPanel periodId={3} periodName="Temmuz 2026" row={row} onClose={() => {}} />
    </ToastProvider>,
  );

test("panel satır bilgileri ve uyarılarıyla açılır", async () => {
  renderPanel();
  expect(screen.getByRole("heading", { name: "Ahmet Yılmaz" })).toBeInTheDocument();
  expect(screen.getByText("Kontrol bekliyor")).toBeInTheDocument();
  expect(screen.getByText("Bordro Pusulası Önizleme")).toBeInTheDocument();
  expect(screen.getByText("Puantaj sistem tarafından tamamlandı")).toBeInTheDocument();
  expect(screen.queryByRole("checkbox", { name: "Puantaj tamam" })).not.toBeInTheDocument();
  expect(screen.queryByLabelText("Önceki GV matrahı")).not.toBeInTheDocument();
});

test("girdi kaydetme PUT row ucuna doğru gövdeyle gider", async () => {
  renderPanel();
  const gross = screen.getByLabelText("Brüt ücret");
  await userEvent.clear(gross);
  await userEvent.type(gross, "120000");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/payroll/periods/3/rows/7" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    const body = JSON.parse(String(put![1]?.body));
    expect(body).toMatchObject({ grossSalary: 120000, workedDays: 30, overtimeHours: 12 });
    expect(body).not.toHaveProperty("timesheetComplete");
    expect(body).not.toHaveProperty("previousTaxBase");
  });
});

test("onaya gönder approve ucuna gider", async () => {
  renderPanel();
  await userEvent.click(screen.getByRole("button", { name: /Onaya gönder/ }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/3/rows/7/approve");
    expect(hit).toBe(true);
  });
});

test("onaylı satırda düzenleme formu gizli, PDF butonu görünür", async () => {
  vi.stubGlobal("URL", { ...URL, createObjectURL: vi.fn(() => "blob:x"), revokeObjectURL: vi.fn() });
  renderPanel({ ...baseRow, approvalStatus: "Onaylandı" });
  expect(screen.queryByLabelText("Brüt ücret")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /Pusula İndir/ }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/3/rows/7/payslip");
    expect(hit).toBe(true);
  });
});
