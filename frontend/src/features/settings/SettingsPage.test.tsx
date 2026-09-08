import { screen, waitFor } from "@testing-library/react";
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
    "/api/settings/company": settings.company,
    "/api/settings/notifications": { ...settings.notifications, newPersonnelEmail: false },
    "/api/auth/change-password": {},
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <ToastProvider>
      <SettingsPage />
    </ToastProvider>,
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

test("bildirim toggle değişimi anında PUT notifications atar", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Bildirimler/ }));
  await userEvent.click(screen.getByLabelText("Yeni Personel Kaydı"));
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
