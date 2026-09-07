import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { PayrollDetailPanel } from "./PayrollDetailPanel";
import type { PayrollRowDto } from "./queries";

const baseRow: PayrollRowDto = {
  id: 7, employeeId: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım",
  grossSalary: 118000, statutoryProfile: "domesticPrivateSectorStandardFourA",
  workedDays: 30, sgkPremiumDaysConfirmed: true,
  overtimeHours: 12, overtimeTreatmentConfirmed: true, premiumPay: 3500,
  roadAllowance: 1200, mealAllowance: 1800, mealDays: 22,
  mealTreatment: "cashExemptWhereNoMealProvided", benefitPay: 3200,
  benefitPayFullyTaxableConfirmed: true, applyMinimumWageTaxExemption: true,
  employerSgkDiscount: "none", employerSgkDiscountReference: null,
  disabilityReliefType: "none", disabilityReliefReference: null,
  otherIncomeTaxBaseDeductionsConfirmedNone: true,
  previousEmployerIncomeTaxBase: 85000,
  previousEmployerIncomeTaxBaseReference: "Çalışan talebi TEST-321",
  cumulativeIncomeTaxBaseConfirmed: true,
  specialDeductions: 1800, previousTaxBase: 312000,
  ibanComplete: true, timesheetComplete: true, approvalStatus: "Kontrol",
  notes: "SGK tavanına yakın kazanç.",
  hourlyRate: 524, overtimePay: 9440, baseGross: 118000, grossEarnings: 137140,
  sgkBase: 137140, sgkEmployee: 19200, unemploymentEmployee: 1371, incomeTaxBase: 116569,
  incomeTax: 25000, stampTax: 790, totalDeductions: 48161, netPay: 88979,
  employerSgk: 28114, employerUnemployment: 2743, employerCost: 167997,
  sgkExemptMealAllowance: 1800, incomeTaxExemptMealAllowance: 1800,
  minimumWageIncomeTaxExemption: 4537.75, minimumWageStampTaxExemption: 250.7,
  appliedEmployerSgkRate: 21.75, disabilityIncomeTaxBaseDeduction: 0,
  warnings: ["Kontrol bekliyor"],
};

beforeEach(() => stubApi({
  "/api/payroll/periods/3/rows/7": { ...baseRow },
  "/api/payroll/periods/3/rows/7/approve": { ...baseRow, approvalStatus: "Onaylandı" },
  "/api/payroll/periods/3/rows/7/payslip": {},
}));
afterEach(() => vi.unstubAllGlobals());

const renderPanel = (row = baseRow) => renderPage(
  <ToastProvider><PayrollDetailPanel periodId={3} periodName="Temmuz 2026" row={row} onClose={() => {}} /></ToastProvider>,
);

test("panel kanuni sınıflandırmaları, istisnaları ve uyarıları gösterir", () => {
  renderPanel();
  expect(screen.getByRole("heading", { name: "Ahmet Yılmaz" })).toBeInTheDocument();
  expect(screen.getByText("Kontrol bekliyor")).toBeInTheDocument();
  expect(screen.getByText("Puantaj sistem tarafından tamamlandı")).toBeInTheDocument();
  expect(screen.getByLabelText("Kanuni çalışan profili")).toHaveValue("domesticPrivateSectorStandardFourA");
  expect(screen.getByText("Asgari ücret GV istisnası")).toBeInTheDocument();
  expect(screen.getByLabelText("Engellilik indirimi")).toHaveValue("none");
  expect(screen.getByText("Engellilik matrah indirimi")).toBeInTheDocument();
  expect(screen.getByLabelText("İlk dönem önceki işveren GV matrahı")).toHaveValue(85000);
  expect(screen.getByLabelText("Kümülatif GV matrahını ve varsa önceki işveren açılışını doğruladım")).toBeChecked();
  expect(screen.queryByLabelText("Önceki GV matrahı")).not.toBeInTheDocument();
});

test("girdi kaydetme tüm zorunlu sınıflandırmalarla PUT ucuna gider", async () => {
  renderPanel();
  const gross = screen.getByLabelText("Aylık brüt ücret");
  await userEvent.clear(gross);
  await userEvent.type(gross, "120000");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([url, init]) => String(url) === "/api/payroll/periods/3/rows/7" && init?.method === "PUT",
    );
    expect(put).toBeTruthy();
    const body = JSON.parse(String(put![1]?.body));
    expect(body).toMatchObject({
      grossSalary: 120000,
      statutoryProfile: "domesticPrivateSectorStandardFourA",
      workedDays: 30,
      sgkPremiumDaysConfirmed: true,
      overtimeTreatmentConfirmed: true,
      mealDays: 22,
      mealTreatment: "cashExemptWhereNoMealProvided",
      benefitPayFullyTaxableConfirmed: true,
      applyMinimumWageTaxExemption: true,
      employerSgkDiscount: "none",
      disabilityReliefType: "none",
      otherIncomeTaxBaseDeductionsConfirmedNone: true,
      previousEmployerIncomeTaxBase: 85000,
      previousEmployerIncomeTaxBaseReference: "Çalışan talebi TEST-321",
      cumulativeIncomeTaxBaseConfirmed: true,
    });
    expect(body).not.toHaveProperty("timesheetComplete");
    expect(body).not.toHaveProperty("previousTaxBase");
  });
});

test("satırı onayla approve ucuna gider", async () => {
  renderPanel();
  await userEvent.click(screen.getByRole("button", { name: /Satırı onayla/ }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([url]) => String(url) === "/api/payroll/periods/3/rows/7/approve");
    expect(hit).toBe(true);
  });
});

test("onaylı satırda düzenleme formu gizli, PDF butonu görünür", async () => {
  vi.stubGlobal("URL", { ...URL, createObjectURL: vi.fn(() => "blob:x"), revokeObjectURL: vi.fn() });
  renderPanel({ ...baseRow, approvalStatus: "Onaylandı" });
  expect(screen.queryByLabelText("Aylık brüt ücret")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /Pusula İndir/ }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([url]) => String(url) === "/api/payroll/periods/3/rows/7/payslip");
    expect(hit).toBe(true);
  });
});
