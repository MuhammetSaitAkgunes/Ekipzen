import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { formatPayrollMoney } from "./format";
import { CalculatorTab } from "./CalculatorTab";

const settings = {
  id: 1, version: 2, status: "approved", effectiveFrom: "2026-01-01",
  overtimeMultiplier: 1.5, monthlyWorkingHours: 225, defaultWorkedDays: 30,
  sgkEmployeeRate: 14, unemploymentEmployeeRate: 1, sgkEmployerRate: 21.75, unemploymentEmployerRate: 2,
  stampTaxRate: 0.759, sgkBaseMin: 33030, sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211.33, monthlyMinWageStampTaxExemption: 250.7,
  minWageGross: 33030, mealSgkDailyExemption: 158, mealIncomeTaxDailyExemption: 300,
  otherSectorEmployerDiscountPoints: 2, manufacturingEmployerDiscountPoints: 5,
  firstDegreeDisabilityDeduction: 12000, secondDegreeDisabilityDeduction: 7000,
  thirdDegreeDisabilityDeduction: 3000,
  sourceReference: "https://www.sgk.gov.tr/resmi", reviewNotes: "Uzman onaylı", taxBrackets: [],
};
const periods = [{ id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft", employeeCount: 1 }];
const detail = {
  id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft", settings,
  rows: [{
    id: 1, employeeId: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım",
    grossSalary: 118000, statutoryProfile: "domesticPrivateSectorStandardFourA",
    workedDays: 30, sgkPremiumDaysConfirmed: true,
    overtimeHours: 12, overtimeTreatmentConfirmed: true, premiumPay: 3500,
    roadAllowance: 1200, mealAllowance: 1800, mealDays: 22,
    mealTreatment: "cashExemptWhereNoMealProvided", benefitPay: 3200,
    benefitPayFullyTaxableConfirmed: true, applyMinimumWageTaxExemption: true,
    employerSgkDiscount: "none", disabilityReliefType: "none",
    otherIncomeTaxBaseDeductionsConfirmedNone: true, specialDeductions: 1800,
    previousTaxBase: 312000, ibanComplete: true, timesheetComplete: true,
    approvalStatus: "Kontrol", notes: null,
    hourlyRate: 0, overtimePay: 0, baseGross: 0, grossEarnings: 0, sgkBase: 0,
    sgkEmployee: 0, unemploymentEmployee: 0, incomeTaxBase: 0, incomeTax: 0, stampTax: 0,
    totalDeductions: 0, netPay: 0, employerSgk: 0, employerUnemployment: 0, employerCost: 0, warnings: [],
  }],
  totals: { gross: 0, net: 0, employerCost: 0, deductions: 0 }, controls: [],
};
const calculation = {
  hourlyRate: 524.44, overtimePay: 2360, additionalPay: 6200, baseGross: 118000,
  grossEarnings: 126560, sgkBase: 126560, sgkEmployee: 17718, unemploymentEmployee: 1265,
  incomeTaxBase: 107577, incomeTax: 21000, stampTax: 710, totalDeductions: 40693,
  netPay: 85867, employerSgk: 25944, employerUnemployment: 2531, employerCost: 155035,
  sgkExemptMealAllowance: 1800, incomeTaxExemptMealAllowance: 1800,
  minimumWageIncomeTaxExemption: 4537.75, appliedEmployerSgkRate: 21.75,
  disabilityIncomeTaxBaseDeduction: 0, warnings: [],
};

beforeEach(() => stubApi({
  "/api/payroll/settings": settings,
  "/api/payroll/periods": periods,
  "/api/payroll/periods/3": detail,
  "/api/payroll/preview": calculation,
}));
afterEach(() => vi.unstubAllGlobals());

const renderTab = () => renderPage(<ToastProvider><CalculatorTab /></ToastProvider>);

test("personel seçilince sınıflandırmalarla birlikte preview backend'e gider", async () => {
  renderTab();
  await screen.findByText("Kanuni Tekil Hesaplama");
  await userEvent.selectOptions(await screen.findByLabelText("Personel"), "1");
  expect(screen.getByLabelText("Aylık brüt ücret")).toHaveValue(118000);
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.filter(
      ([url, init]) => String(url) === "/api/payroll/preview" && init?.method === "POST",
    );
    expect(posted.length).toBeGreaterThan(0);
    const body = JSON.parse(String(posted[posted.length - 1][1]?.body));
    expect(body).toMatchObject({
      grossSalary: 118000,
      statutoryProfile: "domesticPrivateSectorStandardFourA",
      workedDays: 30,
      overtimeHours: 12,
      mealDays: 22,
      mealTreatment: "cashExemptWhereNoMealProvided",
      benefitPayFullyTaxableConfirmed: true,
      disabilityReliefType: "none",
      employerSgkDiscount: "none",
      asOf: "2026-07-01",
    });
  });
});

test("sonuç panelinde net ödeme ve kanuni istisnalar görünür", async () => {
  renderTab();
  await screen.findByText("Kanuni Tekil Hesaplama");
  expect(await screen.findByText(formatPayrollMoney(85867))).toBeInTheDocument();
  expect(screen.getByText("Asgari ücret GV istisnası")).toBeInTheDocument();
  expect(screen.getByText("Engellilik matrah indirimi")).toBeInTheDocument();
  expect(screen.getByText("Bu hesap dönem bordrosuna işlenmedi.")).toBeInTheDocument();
});
