import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { SettingsTab } from "./SettingsTab";

const settings = {
  effectiveFrom: "2026-01-01", overtimeMultiplier: 1.5, monthlyWorkingHours: 225, defaultWorkedDays: 30,
  sgkEmployeeRate: 14, unemploymentEmployeeRate: 1, sgkEmployerRate: 20.5, unemploymentEmployerRate: 2,
  stampTaxRate: 0.759, sgkBaseMin: 33030, sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211, monthlyMinWageStampTaxExemption: 250.7,
  minWageGross: 33030, taxBrackets: [],
};

beforeEach(() => stubApi({
  "/api/payroll/settings": settings,
  "/api/termination-calculation-rules": [],
}));
afterEach(() => vi.unstubAllGlobals());

const renderTab = () =>
  renderPage(
    <ToastProvider>
      <SettingsTab />
    </ToastProvider>,
  );

test("alanlar backend ayarlarıyla dolar", async () => {
  renderTab();
  expect(await screen.findByLabelText("Fazla mesai çarpanı")).toHaveValue(1.5);
  expect(screen.getByLabelText("SGK PEK alt sınırı")).toHaveValue(33030);
});

test("kaydet PUT settings ucuna tam komutla gider", async () => {
  renderTab();
  const multiplier = await screen.findByLabelText("Fazla mesai çarpanı");
  await userEvent.clear(multiplier);
  await userEvent.type(multiplier, "2");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/payroll/settings" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    const body = JSON.parse(String(put![1]?.body));
    expect(body).toMatchObject({
      effectiveFrom: "2026-01-01", overtimeMultiplier: 2, sgkBaseMin: 33030, minWageGross: 33030,
    });
    expect(body.taxBrackets).toBeUndefined();
  });
  expect(await screen.findByText(/2026-01-01 tarihinden itibaren geçerli/)).toBeInTheDocument();
});

test("varsayılana dön formu sorgu değerlerine geri alır", async () => {
  renderTab();
  const multiplier = await screen.findByLabelText("Fazla mesai çarpanı");
  await userEvent.clear(multiplier);
  await userEvent.type(multiplier, "3");
  await userEvent.click(screen.getByRole("button", { name: /Varsayılana dön/ }));
  expect(screen.getByLabelText("Fazla mesai çarpanı")).toHaveValue(1.5);
});

test("işten çıkış kuralını onaysız taslak ve kaynak referansıyla oluşturur", async () => {
  renderTab();
  const source = await screen.findByLabelText("Resmî kaynak + uzman inceleme referansı");
  const ceiling = screen.getByLabelText("Kıdem tavanı (₺)");
  expect(ceiling).toHaveValue(0);

  await userEvent.clear(ceiling);
  await userEvent.type(ceiling, "75000");
  await userEvent.type(source, "CSGB belge + SMMM-42");
  await userEvent.click(screen.getByRole("button", { name: /Taslak oluştur/ }));

  await waitFor(() => {
    const post = vi.mocked(fetch).mock.calls.find(
      ([url, init]) => String(url) === "/api/termination-calculation-rules" && init?.method === "POST",
    );
    expect(post).toBeTruthy();
    const body = JSON.parse(String(post![1]?.body));
    expect(body).toMatchObject({
      severanceCeiling: 75000,
      sourceReference: "CSGB belge + SMMM-42",
      dailyWageDivisor: 30,
      severanceDaysPerYear: 30,
      expectedVersion: null,
    });
    expect(body.status).toBeUndefined();
  });
});
