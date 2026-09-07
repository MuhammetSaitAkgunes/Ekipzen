import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { SettingsTab } from "./SettingsTab";

const settings = {
  id: 1,
  version: 2,
  status: "approved",
  effectiveFrom: "2026-01-01",
  overtimeMultiplier: 1.5,
  monthlyWorkingHours: 225,
  defaultWorkedDays: 30,
  sgkEmployeeRate: 14,
  unemploymentEmployeeRate: 1,
  sgkEmployerRate: 21.75,
  unemploymentEmployerRate: 2,
  stampTaxRate: 0.759,
  sgkBaseMin: 33030,
  sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211.33,
  monthlyMinWageStampTaxExemption: 250.7,
  minWageGross: 33030,
  mealSgkDailyExemption: 158,
  mealIncomeTaxDailyExemption: 300,
  otherSectorEmployerDiscountPoints: 2,
  manufacturingEmployerDiscountPoints: 5,
  firstDegreeDisabilityDeduction: 12000,
  secondDegreeDisabilityDeduction: 7000,
  thirdDegreeDisabilityDeduction: 3000,
  sourceReference: "https://www.sgk.gov.tr/resmi",
  reviewNotes: "SMMM-42 tarafından doğrulandı",
  taxBrackets: [
    { order: 1, limit: 190000, base: 0, baseTax: 0, rate: 0.15 },
    { order: 2, limit: 400000, base: 190000, baseTax: 28500, rate: 0.2 },
    { order: 3, limit: 1500000, base: 400000, baseTax: 70500, rate: 0.27 },
    { order: 4, limit: 5300000, base: 1500000, baseTax: 367500, rate: 0.35 },
    { order: 5, limit: null, base: 5300000, baseTax: 1697500, rate: 0.4 },
  ],
};

let openDrafts: Array<typeof settings> = [];

beforeEach(() => {
  openDrafts = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input).split("?")[0];
    if (path === "/api/payroll/settings" && init?.method === "PUT") {
      const body = JSON.parse(String(init.body));
      return Response.json({ ...settings, ...body, id: 9, version: 1, status: "draft" });
    }
    if (path === "/api/payroll/settings/9/approve") {
      return Response.json({ ...settings, id: 9, version: 2, status: "approved" });
    }
    if (path === "/api/payroll/settings/drafts") return Response.json(openDrafts);
    if (path === "/api/payroll/settings") return Response.json(settings);
    if (path === "/api/termination-calculation-rules") return Response.json([]);
    return Response.json({});
  }));
});
afterEach(() => vi.unstubAllGlobals());

const renderTab = () => renderPage(<ToastProvider><SettingsTab /></ToastProvider>);

test("alanlar son onaylı mevzuat setinden dolar", async () => {
  renderTab();
  expect(await screen.findByLabelText("Fazla mesai çarpanı")).toHaveValue(1.5);
  expect(screen.getByLabelText("Brüt asgari ücret / SGK alt sınırı")).toHaveValue(33030);
  expect(screen.getByLabelText("Nakit yemek SGK günlük istisnası")).toHaveValue(158);
  expect(screen.getByText("Onaylı v2")).toBeInTheDocument();
});

test("sayfa yenilemesinden sonra açık taslak kaldığı yerden yüklenir", async () => {
  openDrafts = [{
    ...settings,
    id: 9,
    version: 3,
    status: "draft",
    effectiveFrom: "2026-10-01",
    overtimeMultiplier: 1.75,
    sourceReference: "https://www.gib.gov.tr/taslak",
    reviewNotes: "SMMM-99 incelemesi",
  }];

  renderTab();
  await userEvent.click(await screen.findByRole("button", { name: /2026-10-01 · v3 taslağını aç/ }));

  expect(screen.getByLabelText("Fazla mesai çarpanı")).toHaveValue(1.75);
  expect(screen.getByLabelText("Resmî HTTPS kaynakları")).toHaveValue("https://www.gib.gov.tr/taslak");
  expect(screen.getByRole("button", { name: /Mevzuat setini onayla/ })).toBeEnabled();
});

test("taslak tam kanuni parametre ve denetim kaydıyla kaydedilir", async () => {
  renderTab();
  const multiplier = await screen.findByLabelText("Fazla mesai çarpanı");
  await userEvent.clear(multiplier);
  await userEvent.type(multiplier, "2");
  await userEvent.type(screen.getByLabelText("Resmî HTTPS kaynakları"), "https://www.sgk.gov.tr/yeni");
  await userEvent.type(screen.getByLabelText("Uzman inceleme kaydı"), "SMMM-77 kontrolü");
  await userEvent.click(screen.getByRole("button", { name: /Taslağı kaydet/ }));

  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === "PUT");
    expect(put).toBeTruthy();
    const body = JSON.parse(String(put![1]?.body));
    expect(body).toMatchObject({
      overtimeMultiplier: 2,
      sgkBaseMin: 33030,
      minWageGross: 33030,
      sgkEmployerRate: 21.75,
      firstDegreeDisabilityDeduction: 12000,
      secondDegreeDisabilityDeduction: 7000,
      thirdDegreeDisabilityDeduction: 3000,
      sourceReference: "https://www.sgk.gov.tr/yeni",
      reviewNotes: "SMMM-77 kontrolü",
      expectedVersion: null,
    });
    expect(body.effectiveFrom).toMatch(/^\d{4}-\d{2}-01$/);
    expect(body.taxBrackets).toHaveLength(5);
  });
  expect(await screen.findByText(/taslak kaydedildi; hesaplarda henüz kullanılmaz/)).toBeInTheDocument();
});

test("kaydedilen taslak ayrı işlemle onaylanır", async () => {
  renderTab();
  await screen.findByLabelText("Fazla mesai çarpanı");
  await userEvent.type(screen.getByLabelText("Resmî HTTPS kaynakları"), "https://www.gib.gov.tr/yeni");
  await userEvent.type(screen.getByLabelText("Uzman inceleme kaydı"), "Uzman-12");
  await userEvent.click(screen.getByRole("button", { name: /Taslağı kaydet/ }));
  await screen.findByText(/taslak kaydedildi/);
  await userEvent.click(screen.getByRole("button", { name: /Mevzuat setini onayla/ }));

  await waitFor(() => {
    const post = vi.mocked(fetch).mock.calls.find(
      ([url, init]) => String(url) === "/api/payroll/settings/9/approve" && init?.method === "POST",
    );
    expect(post).toBeTruthy();
    expect(JSON.parse(String(post![1]?.body))).toEqual({ id: 9, expectedVersion: 1 });
  });
});

test("son onaylı setten hazırla formu sorgu değerlerine geri alır", async () => {
  renderTab();
  const multiplier = await screen.findByLabelText("Fazla mesai çarpanı");
  await userEvent.clear(multiplier);
  await userEvent.type(multiplier, "3");
  await userEvent.click(screen.getByRole("button", { name: /Son onaylı setten hazırla/ }));
  expect(screen.getByLabelText("Fazla mesai çarpanı")).toHaveValue(1.5);
});

test("işten çıkış kuralını onaysız taslak ve kaynak referansıyla oluşturur", async () => {
  renderTab();
  const source = await screen.findByLabelText("Resmî kaynak + uzman inceleme referansı");
  const ceiling = screen.getByLabelText("Kıdem tavanı (₺)");
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
    expect(body).toMatchObject({ severanceCeiling: 75000, sourceReference: "CSGB belge + SMMM-42" });
    expect(body.status).toBeUndefined();
  });
});
