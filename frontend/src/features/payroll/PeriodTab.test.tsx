import { screen, waitFor } from "@testing-library/react";
import { AuthProvider } from "../../auth/AuthContext";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { formatPayrollMoney } from "./format";
import { PeriodTab } from "./PeriodTab";

const row = (over: object) => ({
  id: 1, employeeId: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım",
  grossSalary: 118000, workedDays: 30, overtimeHours: 12, premiumPay: 3500,
  roadAllowance: 1200, mealAllowance: 1800, benefitPay: 3200, specialDeductions: 1800,
  previousTaxBase: 312000, ibanComplete: true, timesheetComplete: true,
  approvalStatus: "Kontrol", notes: null,
  hourlyRate: 524, overtimePay: 9440, baseGross: 118000, grossEarnings: 137140,
  sgkBase: 137140, sgkEmployee: 19200, unemploymentEmployee: 1371, incomeTaxBase: 116569,
  incomeTax: 25000, stampTax: 790, totalDeductions: 48161, netPay: 88979,
  employerSgk: 28114, employerUnemployment: 2743, employerCost: 167997, warnings: ["Kontrol bekliyor"],
  ...over,
});
const detail = {
  id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft",
  settings: {
    id: 1, version: 2, status: "approved", effectiveFrom: "2026-01-01", reviewNotes: "Uzman onaylı",
    sourceReference: "https://www.sgk.gov.tr/resmi", overtimeMultiplier: 1.5, monthlyWorkingHours: 225,
    minWageGross: 33030, sgkBaseMax: 297270, sgkEmployeeRate: 14, unemploymentEmployeeRate: 1,
    sgkEmployerRate: 21.75, unemploymentEmployerRate: 2, mealSgkDailyExemption: 158,
    mealIncomeTaxDailyExemption: 300, otherSectorEmployerDiscountPoints: 2,
    manufacturingEmployerDiscountPoints: 5, stampTaxRate: 0.759,
  },
  rows: [row({}), row({ id: 2, employeeId: 4, name: "Selin Koç", department: "Tasarım", approvalStatus: "Onaylandı", warnings: [] })],
  totals: { gross: 274280, net: 177958, employerCost: 335994, deductions: 96322 },
  controls: [
    { label: "Eksik Puantaj", value: 0, level: "high" },
    { label: "Eksik IBAN", value: 1, level: "high" },
    { label: "SGK Matrah Uyarısı", value: 0, level: "medium" },
    { label: "Vergi Matrahı Uyarısı", value: 0, level: "medium" },
    { label: "Onay Bekleyen", value: 1, level: "low" },
  ],
};
const settings = {
  effectiveFrom: "2026-01-01", overtimeMultiplier: 1.5, monthlyWorkingHours: 225, defaultWorkedDays: 30,
  sgkEmployeeRate: 14, unemploymentEmployeeRate: 1, sgkEmployerRate: 21.75, unemploymentEmployerRate: 2,
  stampTaxRate: 0.759, sgkBaseMin: 33030, sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211, monthlyMinWageStampTaxExemption: 250.7,
  minWageGross: 33030, taxBrackets: [],
};

beforeEach(() =>
  stubApi({
    "/api/payroll/periods/3": detail,
    "/api/payroll/periods/3/check": detail,
    "/api/payroll/settings": settings,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderTab = () =>
  renderPage(
    <AuthProvider><ToastProvider>
      <PeriodTab periodId={3} />
    </ToastProvider></AuthProvider>,
  );

test("KPI şeridi dönem toplamlarıyla dolar", async () => {
  renderTab();
  expect(await screen.findByText("Taslak")).toBeInTheDocument();
  expect(screen.getByText("Toplam Brüt").closest(".stat-box")).toHaveTextContent(formatPayrollMoney(274280));
  expect(screen.getByText("Çalışan").closest(".stat-box")).toHaveTextContent("1 onaylandı");
});

test("kontrol merkezi backend kartlarını statik detaylarla gösterir", async () => {
  renderTab();
  expect(await screen.findByText("Eksik IBAN")).toBeInTheDocument();
  expect(screen.getByText("Ödeme listesine girmeden tamamlanmalı.")).toBeInTheDocument();
});

test("kontrol adımı etkisini açıklar, eksik satırı bildirir ve check ucuna gider", async () => {
  renderTab();
  await screen.findByText("Eksik IBAN");
  expect(screen.getByText(/Kontrol, onaylanmamış satırların puantaj/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Kontrolü çalıştır" }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/3/check");
    expect(hit).toBe(true);
  });
  expect(await screen.findByText(/1 satırda eksik veya yeniden incelenecek veri var/)).toBeInTheDocument();
});

test("onaylı dönemde kontrol yeniden çalıştırılamaz", async () => {
  stubApi({ "/api/payroll/periods/3": { ...detail, status: "approved" } });
  renderTab();
  expect(await screen.findByText("Onaylandı", { selector: ".sb-val" })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /Kontrolü çalıştır|Kontrolü yeniden çalıştır/ })).not.toBeInTheDocument();
  expect(screen.getByText(/kontrol yeniden çalıştırılamaz/)).toBeInTheDocument();
});

test("tablo satırları ve departman filtresi çalışır", async () => {
  renderTab();
  expect(await screen.findByText("Ahmet Yılmaz")).toBeInTheDocument();
  await userEvent.selectOptions(screen.getByLabelText("Departman filtresi"), "Tasarım");
  expect(screen.queryByText("Ahmet Yılmaz")).not.toBeInTheDocument();
  expect(screen.getByText("Selin Koç")).toBeInTheDocument();
});

test("dönem seçili değilse boş durum görünür", async () => {
  renderPage(
    <AuthProvider><ToastProvider>
      <PeriodTab periodId={null} />
    </ToastProvider></AuthProvider>,
  );
  expect(await screen.findByText(/Henüz bordro dönemi yok/)).toBeInTheDocument();
});
