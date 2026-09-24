import { act, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../auth/AuthContext";
import { ToastProvider } from "../layout/ToastProvider";
import { SESSION_KEY } from "../api/session";
import { buildRouteObjects } from "../routes";
import { stubApi } from "./apiStub";

const pending = { id: 4, employeeName: "Ayşe Demir", leaveTypeName: "Yıllık İzin", startDate: "2026-09-23", endDate: "2026-09-25", days: 3, description: "Aile ziyareti", status: "pending" };
const employee = { id: 1, name: "Ahmet Yılmaz", firstName: "Ahmet", lastName: "Yılmaz", nationalId: "12345678901", title: "Tasarımcı", departmentId: 1, status: "active", hireDate: "2024-01-01", profile: {} };
const routes = {
  "/api/actions/badge": { openCount: 2 }, "/api/actions": [],
  "/api/dashboard/overview": { activeEmployees: 12, pendingApprovals: 1, onLeaveToday: 2, inOfficeToday: 10 },
  "/api/leaves/pending": [pending], "/api/leaves/my": [],
  "/api/guidance/daily-tasks": [{
    key: "leave-approval:4", kind: "leave-approval", recordId: 4,
    title: "Ayşe Demir izin talebini incele", reason: "Yıllık İzin talebi onay bekliyor.",
    nextAction: "Talep ayrıntılarını inceleyip onay veya ret kararı verin.",
    dueDate: null, dateNote: "İzin başlangıcı: 23.09.2026. Karar için kayıtlı son tarih yok.",
    targetPath: "/inbox?tab=approvals&requestId=4",
  }],
  "/api/leaves/balance": { remainingDays: 14, usedDays: 6 }, "/api/leaves/team": [], "/api/leaves/ledger": [],
  "/api/employees/1": employee, "/api/employees/1/documents": [],
  "/api/employees/1/employment-foundation": { employmentPeriods: [], compensationPackages: [] },
  "/api/departments": [{ id: 1, name: "Ürün" }],
  "/api/employees": { items: [employee], total: 1, totalPages: 1 },
};
function setup(role = "hr-admin", path = "/app/home") {
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "test", refreshToken: "test", user: { id: role, role, name: "Test Kullanıcı", initials: "TK", roleLabel: role } }));
  const router = createMemoryRouter(buildRouteObjects(), { initialEntries: [path] });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><AuthProvider><ToastProvider><RouterProvider router={router} /></ToastProvider></AuthProvider></QueryClientProvider>);
  return { router, client };
}
beforeEach(() => { localStorage.clear(); stubApi(routes); });
afterEach(() => vi.unstubAllGlobals());

test("çalışan ana sayfası yalnız kişisel verileri ister ve yönetim bağlantısı sunmaz", async () => {
  setup("employee");
  await screen.findByText("Kalan yıllık izin");
  expect(screen.getByText("14")).toBeInTheDocument();
  expect(screen.queryByText("Onayınızı bekleyenler")).not.toBeInTheDocument();
  const nav = screen.getByRole("navigation", { name: "Ana gezinme" });
  expect(within(nav).queryByText("Çalışanlar")).not.toBeInTheDocument();
  expect(within(nav).getByText("Bordrolarım")).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([u]) => /leaves\/pending|dashboard\/overview/.test(String(u)))).toBe(false);
});

test("yönetici ana sayfası talepleri getirir, bordro ve çalışan ekleme sunmaz", async () => {
  setup("manager");
  expect(await screen.findByText("Ayşe Demir izin talebini incele")).toBeInTheDocument();
  expect(screen.queryByRole("link", { name: "Çalışan ekle" })).not.toBeInTheDocument();
  expect(screen.queryByRole("link", { name: "Bordro dönemleri" })).not.toBeInTheDocument();
});

test("özet servisi hatası sıfır göstermez ve diğer işleri engellemez", async () => {
  stubApi({ ...routes, "/api/dashboard/overview": undefined });
  const original = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation(async (url, init) => String(url).includes("dashboard/overview") ? new Response("{}", { status: 503 }) : original(url, init));
  setup();
  expect(await screen.findByText("Veri yüklenemedi")).toBeInTheDocument();
  expect(await screen.findByText("Ayşe Demir izin talebini incele")).toBeInTheDocument();
  expect(screen.queryByText("Aktif ekip üyeleri")).not.toBeInTheDocument();
});

test("çalışan iş kutusunda onay sekmesine URL ile erişemez", async () => {
  setup("employee", "/app/inbox?tab=approvals");
  expect(await screen.findByRole("tab", { name: "Aksiyonlar" })).toHaveAttribute("aria-selected", "true");
  expect(screen.queryByRole("tab", { name: "Onaylar" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u).includes("leaves/pending"))).toBe(false);
});

test("onay detayları önce incelenir; başarısız işlem korunur ve tekrar denenir", async () => {
  let failed = false;
  const original = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation(async (url, init) => {
    if (String(url) === "/api/leaves/4/approve") {
      if (!failed) { failed = true; return new Response(JSON.stringify({ title: "Geçici hata" }), { status: 503 }); }
      return new Response(JSON.stringify({ ...pending, status: "approved" }), { status: 200 });
    }
    return original(url, init);
  });
  const { client } = setup("hr-admin", "/app/inbox");
  const invalidate = vi.spyOn(client, "invalidateQueries");
  await screen.findByText("Ayşe Demir");
  expect(screen.queryByRole("button", { name: "Onayla" })).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "İncele" }));
  expect(screen.getByText("Aile ziyareti")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Onayı incele" }));
  expect(screen.getByText(/sistem onay anında bakiyeyi yeniden kontrol eder/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Onayı kaydet" }));
  expect(await screen.findByRole("alert")).toBeInTheDocument();
  expect(screen.getByText("Aile ziyareti")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Onayı kaydet" }));
  await waitFor(() => expect(invalidate).toHaveBeenCalledWith({ queryKey: ["leaves"] }));
  expect(invalidate).toHaveBeenCalledWith({ queryKey: ["actions"] });
  expect(invalidate).toHaveBeenCalledWith({ queryKey: ["dashboard"] });
});

test("eski aksiyon ve yönetici bağlantıları yeni bölümlere yönlenir", async () => {
  const { router } = setup("hr-admin", "/app/actions");
  await waitFor(() => expect(router.state.location.pathname).toBe("/app/inbox"));
  expect(router.state.location.search).toBe("?tab=actions");
  await router.navigate("/app/manager");
  await waitFor(() => expect(router.state.location.pathname).toBe("/app/leaves"));
  expect(router.state.location.search).toBe("?tab=analysis");
  expect(await screen.findByText("Örnek analiz")).toBeInTheDocument();
});

test("profil salt okunur açılır; İK bilinçli olarak düzenlemeye geçer", async () => {
  setup("hr-admin", "/app/personnel/1");
  await screen.findByRole("heading", { name: "Ahmet Yılmaz" });
  expect(screen.getByLabelText("Adı")).toBeDisabled();
  await userEvent.click(screen.getByRole("button", { name: "Profili düzenle" }));
  await waitFor(() => expect(screen.getByLabelText("Adı")).toBeEnabled());
  expect(screen.getByRole("button", { name: /Kaydet/ })).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Vazgeç" }));
  await waitFor(() => expect(screen.getByLabelText("Adı")).toBeDisabled());
});

test("yönetici profile edit parametresi eklese de kaydetme sunulmaz", async () => {
  setup("manager", "/app/personnel/1?edit=1");
  await screen.findByRole("heading", { name: "Ahmet Yılmaz" });
  expect(screen.getByLabelText("Adı")).toBeDisabled();
  expect(screen.queryByRole("button", { name: /Kaydet|Profili düzenle/ })).not.toBeInTheDocument();
});

test("çalışanın profil rotasına erişimi engellenir", async () => {
  setup("employee", "/app/personnel/1");
  expect(await screen.findByText("Bu alan için yetki gerekli")).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/employees/1")).toBe(false);
});

test("profil menüsü Escape ile kapanır ve tema tercihini saklar", async () => {
  setup();
  await screen.findByRole("button", { name: "Profil menüsü" });
  await userEvent.click(screen.getByRole("button", { name: "Profil menüsü" }));
  await userEvent.click(screen.getByRole("button", { name: "Koyu temaya geç" }));
  expect(localStorage.getItem("ekipzen-theme")).toBe("dark");
  await userEvent.keyboard("{Escape}");
  expect(screen.queryByRole("dialog", { name: "Hesap ve görünüm" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Profil menüsü" })).toHaveFocus();
});

test("çalışma alanından çıkınca koyu tema genel sayfalara sızmaz", async () => {
  localStorage.setItem("ekipzen-theme", "dark");
  const { router } = setup();
  await screen.findByText("Bugün yapılacaklar");
  expect(document.documentElement).toHaveAttribute("data-theme", "dark");
  await act(async () => { await router.navigate("/"); });
  await waitFor(() => expect(document.documentElement).not.toHaveAttribute("data-theme"));
  expect(document.querySelector(".ez-workspace")).toBeNull();
});
