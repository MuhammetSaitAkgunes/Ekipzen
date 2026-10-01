import { AuthProvider } from "../../auth/AuthContext";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, test, vi } from "vitest";
import { renderPage } from "../../test/renderPage";
import { TenantSetupPanel } from "./TenantSetupPanel";

const missing = {
  templateYear: 2026, companyProfileExists: false, missingLeaveTypeCount: 4,
  missingFullDayHolidayCount: 14, payrollSettingsExists: false,
  hasEffectiveApprovedPayrollSettings: false, needsInitialization: true,
};
afterEach(() => vi.unstubAllGlobals());

test("durum okuması otomatik kurulum veya onay yapmaz", async () => {
  vi.stubGlobal("fetch", vi.fn(async () => Response.json(missing)));
  renderPage(<AuthProvider><TenantSetupPanel /></AuthProvider>);
  expect(await screen.findByText("Eksik başlangıç kayıtları var.")).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
  expect(screen.getByText(/Bu kontrol pilot onayı değildir/)).toBeInTheDocument();
});

test("açık kurulumdan sonra durum yenilenir ve uzman onayı beklenir", async () => {
  const done = vi.fn();
  vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) =>
    Response.json(init?.method === "POST" ? { ...missing, needsInitialization: false } : missing)));
  renderPage(<AuthProvider><TenantSetupPanel onInitialized={done} /></AuthProvider>);
  await userEvent.click(await screen.findByRole("button", { name: "Kurulumu gözden geçir" }));
  expect(screen.getByRole("region", { name: "Başlangıç kayıtlarını oluşturmadan önce kontrol edin" })).toBeInTheDocument();
  expect(screen.getByText(/4 eksik tür eklenecek/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Kontrol ettim, kayıtları oluştur" })).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("checkbox", { name: /Eksik kayıtların oluşturulacağını/ }));
  await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, kayıtları oluştur" }));
  expect(await screen.findByText("Başlangıç kayıtları mevcut.")).toBeInTheDocument();
  await waitFor(() => expect(done).toHaveBeenCalledOnce());
  expect(screen.getByText(/Yürürlükte onaylı bordro seti yok/)).toBeInTheDocument();
  expect(screen.queryByRole("button")).not.toBeInTheDocument();
});

test("kurulum hatası görünür ve yeniden denemeye izin verir", async () => {
  vi.stubGlobal("fetch", vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) =>
    init?.method === "POST" ? Response.json({ title: "Kurulum başarısız" }, { status: 500 }) : Response.json(missing)));
  renderPage(<AuthProvider><TenantSetupPanel /></AuthProvider>);
  await userEvent.click(await screen.findByRole("button", { name: "Kurulumu gözden geçir" }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Eksik kayıtların oluşturulacağını/ }));
  await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, kayıtları oluştur" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Kurulum başarısız");
  expect(screen.getByRole("button", { name: "Kontrol ettim, kayıtları oluştur" })).toBeEnabled();
});

test("durum okuma hatasında kurulum butonu sunulmaz", async () => {
  vi.stubGlobal("fetch", vi.fn(async () => Response.json({ title: "Durum okunamadı" }, { status: 500 })));
  renderPage(<AuthProvider><TenantSetupPanel /></AuthProvider>);
  expect(await screen.findByRole("alert")).toHaveTextContent("Durum okunamadı");
  expect(screen.queryByRole("button")).not.toBeInTheDocument();
});
