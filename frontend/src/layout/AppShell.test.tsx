import userEvent from "@testing-library/user-event";
import { render, screen, within } from "@testing-library/react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { buildRouteObjects } from "../routes";
import { AuthProvider } from "../auth/AuthContext";
import { ToastProvider } from "./ToastProvider";
import { SESSION_KEY } from "../api/session";
import { stubApi } from "../test/apiStub";

const renderShellAt = (path: string, role: string, name: string) => {
  localStorage.setItem(
    SESSION_KEY,
    JSON.stringify({ token: "T", refreshToken: "R", user: { id: "u", name, email: "x@x", role, roleLabel: name, initials: "XX", employeeId: null } }),
  );
  // Shell rozeti + dilim 2 sayfa sorguları: path bazlı stub (gerçek ağ yok).
  stubApi({
    "/api/actions/badge": { openCount: 5 },
    "/api/actions": [],
    "/api/dashboard/metrics": { riskScore: 0, riskTrend: [], departmentRisk: [], talentCapacity: [] },
    "/api/dashboard/manager-load": { managers: [] },
    "/api/dashboard/employee-voice": { departments: [], signals: [], recommendedActions: [] },
    "/api/dashboard/compliance": { records: [], deadlines: [] },
    "/api/dashboard/overview": { departmentDistribution: [], recruitmentFunnel: {} },
  });
  const router = createMemoryRouter(buildRouteObjects(), { initialEntries: [path] });
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <AuthProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>,
  );
};

beforeEach(() => localStorage.clear());
afterEach(() => vi.unstubAllEnvs());

test("hr-admin tüm menü gruplarını görür, başlık rotadan gelir", async () => {
  renderShellAt("/app/dashboard", "hr-admin", "İK Yöneticisi");
  expect(await screen.findByText("Risk Merkezi", { selector: ".header-title" })).toBeInTheDocument();
  expect(screen.getByText("Ayarlar")).toBeInTheDocument();
  expect(await screen.findByText("5", { selector: ".header-action-button span" })).toBeInTheDocument();
});

test("employee menüsünde yalnız yetkili modüller görünür", () => {
  renderShellAt("/app/overview", "employee", "Ahmet Yılmaz");
  expect(screen.queryByText("Ayarlar")).not.toBeInTheDocument();
  expect(screen.queryByText("Çalışanlar")).not.toBeInTheDocument();
  expect(screen.getByText("İzinlerim")).toBeInTheDocument();
});

test("üretimde demo rol geçişi sunulmaz, gerçek oturum korunur", async () => {
  vi.stubEnv("DEV", false);
  renderShellAt("/app/overview", "employee", "Pilot Kullanıcı");
  expect(screen.queryByRole("combobox", { name: "Demo rol değiştir" })).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Profil menüsü" }));
  expect(screen.getByRole("button", { name: "Çıkış yap" })).toBeInTheDocument();
  expect(JSON.parse(localStorage.getItem(SESSION_KEY)!).user.name).toBe("Pilot Kullanıcı");
});

test("geliştirmede demo rol geçişi korunur", async () => {
  vi.stubEnv("DEV", true);
  renderShellAt("/app/overview", "employee", "Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: "Profil menüsü" }));
  expect(screen.getByRole("combobox", { name: "Demo rol değiştir" })).toHaveValue("employee");
});

test("kabuk yeni Ekipzen logosunu ve kiracı adını gösterir", async () => {
  renderShellAt("/app/overview", "employee", "Ahmet Yılmaz");
  const sidebar = document.querySelector("aside.sidebar") as HTMLElement;
  expect(within(sidebar).getByRole("img", { name: "Ekipzen" })).toBeInTheDocument();
  expect(sidebar.querySelector(".fa-users-gear")).toBeNull();
});

test("sekme başlığı rotaya göre güncellenir", async () => {
  renderShellAt("/app/dashboard", "hr-admin", "İK Yöneticisi");
  await screen.findByText("Risk Merkezi", { selector: ".header-title" });
  expect(document.title).toBe("Risk Merkezi · Ekipzen");
});
