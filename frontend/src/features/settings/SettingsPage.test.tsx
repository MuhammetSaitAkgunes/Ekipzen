import { AuthProvider } from "../../auth/AuthContext";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { SettingsPage } from "./SettingsPage";

const settings = {
  company: { name: "HR Master Teknoloji A.Ş.", website: "www.hrmaster.com", systemEmail: "info@hrmaster.com", phone: "+90 212 555 00 00", headquartersAddress: "Maslak", logoPath: null },
  notifications: { newPersonnelEmail: true, leaveRequestEmail: true, weeklyReportEmail: false },
  security: { twoFactorSmsEnabled: false },
  subscription: { plan: "pro", planName: "HR Master Kurumsal", billingCycle: "Yıllık", price: 12000, renewalDate: "2026-10-12", paymentMethodMasked: "•••• •••• •••• 4582" },
};

beforeEach(() =>
  stubApi({
    "/api/setup": { templateYear: 2026, needsInitialization: false, hasEffectiveApprovedPayrollSettings: true },
    "/api/settings": settings,
    "/api/departments": [{ id: 1, name: "Yazılım", code: "YZL", employeeCount: 2 }],
    "/api/settings/company": settings.company,
    "/api/settings/notifications": { ...settings.notifications, newPersonnelEmail: false },
    "/api/auth/change-password": {},
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <AuthProvider><ToastProvider>
      <SettingsPage />
    </ToastProvider></AuthProvider>,
  );

test("şirket formu backend verisiyle dolar ve kaydet PUT atar", async () => {
  renderShell();
  const name = await screen.findByLabelText("Şirket Adı");
  expect(name).toHaveValue("HR Master Teknoloji A.Ş.");
  await userEvent.clear(name);
  await userEvent.type(name, "HR Master A.Ş.");
  await userEvent.click(screen.getByRole("button", { name: /Değişiklikleri Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/settings/company" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    expect(JSON.parse(String(put![1]?.body))).toMatchObject({ name: "HR Master A.Ş.", website: "www.hrmaster.com" });
  });
});

test("bildirim toggle değişimi önizlenir ve açık teyitten sonra PUT atar", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Bildirimler/ }));
  await userEvent.click(screen.getByLabelText("Yeni Personel Kaydı"));
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "PUT")).toBe(false);
  await userEvent.click(screen.getByLabelText("Bildirim tercihlerini teyit ediyorum"));
  await userEvent.click(screen.getByRole("button", { name: "Bildirim tercihlerini kaydet" }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/settings/notifications" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    expect(JSON.parse(String(put![1]?.body))).toMatchObject({ weeklyReportEmail: false, newPersonnelEmail: false });
  });
});

test("şifre değişikliği change-password ucuna gider; eşleşmeyen tekrar hata verir", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Güvenlik & Yetki/ }));
  await userEvent.type(screen.getByLabelText("Mevcut Şifre"), "demo123");
  await userEvent.type(screen.getByLabelText("Yeni Şifre"), "yeni12345");
  await userEvent.type(screen.getByLabelText("Yeni Şifre (Tekrar)"), "farkli");
  await userEvent.click(screen.getByRole("button", { name: "Şifreyi Güncelle" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Şifreler eşleşmiyor, kontrol edin.");
  await userEvent.clear(screen.getByLabelText("Yeni Şifre (Tekrar)"));
  await userEvent.type(screen.getByLabelText("Yeni Şifre (Tekrar)"), "yeni12345");
  await userEvent.click(screen.getByRole("button", { name: "Şifreyi Güncelle" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/auth/change-password" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({ currentPassword: "demo123", newPassword: "yeni12345" });
  });
});

test("abonelik bölümü salt-okur plan bilgisi gösterir", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Abonelik & Fatura/ }));
  expect(screen.getByText("HR Master Kurumsal")).toBeInTheDocument();
  expect(screen.getByText("•••• •••• •••• 4582")).toBeInTheDocument();
});

test("desteklenmeyen haftalık rapor ve SMS 2FA yanlışlıkla etkinleştirilemez", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Bildirimler/ }));
  expect(screen.getByLabelText("Haftalık Rapor")).toBeDisabled();
  expect(screen.getByLabelText("Haftalık Rapor")).not.toBeChecked();
  await userEvent.click(screen.getByRole("button", { name: /Güvenlik & Yetki/ }));
  expect(screen.getByLabelText("SMS ile doğrulama")).toBeDisabled();
  expect(screen.getByLabelText("SMS ile doğrulama")).not.toBeChecked();
});


test("şirket hazırlığı eksik kayıtları ve sonraki gerçek ekranı gösterir", async () => {
  stubApi({
    "/api/setup": {
      templateYear: 2026, needsInitialization: true, companyProfileExists: true,
      missingLeaveTypeCount: 2, missingFullDayHolidayCount: 3,
      payrollSettingsExists: false, hasEffectiveApprovedPayrollSettings: false,
    },
    "/api/settings": settings,
    "/api/departments": [],
  });
  renderShell();
  const guide = await screen.findByRole("region", { name: "İlk çalışan kaydından önce" });
  expect(await within(guide).findByText(/Eksik başlangıç kayıtları var/)).toBeInTheDocument();
  expect(await within(guide).findByText(/Henüz departman yok/)).toBeInTheDocument();
  expect(within(guide).getByText(/Kayıtlı şirket adı: HR Master Teknoloji/)).toBeInTheDocument();
  expect(within(guide).getByRole("link", { name: "Kurulum durumuna git" })).toHaveAttribute("href", "#settings-setup");
  expect(within(guide).getByRole("link", { name: "Şirket bilgilerine git" })).toHaveAttribute("href", "#settings-company");
  expect(within(guide).getByRole("link", { name: "çalışan kartı rehberine geçin" })).toHaveAttribute("href", "/app/personnel/new");
  expect(vi.mocked(fetch).mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
});

test("departman oluşturma ve ad düzeltme çalışan kartının önkoşulunu tamamlar", async () => {
  let departments = [{ id: 1, name: "Yazılım", code: "YZL", employeeCount: 2 }];
  const original = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation(async (input, init) => {
    if (String(input) === "/api/departments") {
      if (init?.method === "POST") {
        const body = JSON.parse(String(init.body));
        const created = { id: 2, name: body.name, code: body.code, employeeCount: 0 };
        departments = [...departments, created];
        return new Response(JSON.stringify(created), { status: 201 });
      }
      return new Response(JSON.stringify(departments), { status: 200 });
    }
    if (String(input) === "/api/departments/2" && init?.method === "PUT") {
      const body = JSON.parse(String(init.body));
      departments = departments.map((item) => item.id === 2 ? { ...item, ...body } : item);
      return new Response(JSON.stringify(departments[1]), { status: 200 });
    }
    return original(input, init);
  });
  renderShell();
  expect(await screen.findByRole("heading", { name: "Departmanlar" })).toBeInTheDocument();
  await userEvent.type(screen.getByLabelText("Yeni departman adı"), "Operasyon");
  await userEvent.type(screen.getByLabelText("Kısa kod (isteğe bağlı)"), "OP");
  await userEvent.click(screen.getByRole("button", { name: "Departman ekle" }));
  const createdRow = (await screen.findByText("Operasyon")).closest("li")!;
  expect(within(createdRow).getByText("OP · 0 çalışan")).toBeInTheDocument();
  await userEvent.click(within(createdRow).getByRole("button", { name: "Düzenle" }));
  expect(screen.getByText(/Adı veya kodu düzeltmek çalışanları başka departmana taşımaz/)).toBeInTheDocument();
  await userEvent.clear(screen.getByLabelText("Departman adı"));
  await userEvent.type(screen.getByLabelText("Departman adı"), "Operasyon ve Destek");
  await userEvent.click(screen.getByRole("button", { name: "Değişikliği kaydet" }));
  expect(await screen.findByText("Operasyon ve Destek")).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/departments/2" && init?.method === "PUT"
      && JSON.parse(String(init.body)).name === "Operasyon ve Destek")).toBe(true);
});

test("departman adı boşsa kayıt gönderilmez", async () => {
  renderShell();
  await screen.findByRole("heading", { name: "Departmanlar" });
  await userEvent.click(screen.getByRole("button", { name: "Departman ekle" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Departman adını girin.");
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/departments" && init?.method === "POST")).toBe(false);
});


test("yinelenen departman reddedilirse girilen ad korunur", async () => {
  const original = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation(async (input, init) => {
    if (String(input) === "/api/departments" && init?.method === "POST") {
      return new Response(JSON.stringify({ title: "'Yazılım' adında bir departman zaten var." }),
        { status: 409, headers: { "Content-Type": "application/json" } });
    }
    return original(input, init);
  });
  renderShell();
  await screen.findByRole("heading", { name: "Departmanlar" });
  await userEvent.type(screen.getByLabelText("Yeni departman adı"), "Yazılım");
  await userEvent.click(screen.getByRole("button", { name: "Departman ekle" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("zaten var");
  expect(screen.getByLabelText("Yeni departman adı")).toHaveValue("Yazılım");
});
