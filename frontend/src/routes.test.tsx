import { render, screen } from "@testing-library/react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, test, vi } from "vitest";
import { buildRouteObjects } from "./routes";
import { AuthProvider } from "./auth/AuthContext";
import { ToastProvider } from "./layout/ToastProvider";
import { SESSION_KEY } from "./api/session";

const sessionFor = (role: string, name: string) =>
  JSON.stringify({ token: "T", refreshToken: "R", user: { id: "u", name, email: "x@x", role, roleLabel: name, initials: "XX", employeeId: null } });

// Gerçek ağaçtaki gibi: AuthProvider QueryClientProvider altında çalışır (App.tsx).
const renderAt = (path: string) => {
  const router = createMemoryRouter(buildRouteObjects(), { initialEntries: [path] });
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  );
};

const renderShellAt = (path: string) => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(
    new Response(JSON.stringify({ openCount: 5 }), { status: 200, headers: { "Content-Type": "application/json" } }),
  ));
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

test("oturum yokken korumalı rota login'e yönlenir", () => {
  renderAt("/app/dashboard");
  // Not: "Giriş yap" hem sekme butonunda hem submit butonunda geçiyor (App.test.tsx'teki
  // getAllByText paritesiyle aynı gerekçe) — bu yüzden getAllByRole kullanılıyor.
  expect(screen.getAllByRole("button", { name: /Giriş yap/ }).length).toBeGreaterThan(0);
});

test("employee, dashboard'da 'Yetki Gerekli' ekranı görür (redirect yok)", () => {
  localStorage.setItem(SESSION_KEY, sessionFor("employee", "Ahmet Yılmaz"));
  renderShellAt("/app/dashboard");
  expect(screen.getByText("Bu alan için yetki gerekli")).toBeInTheDocument();
});

test("hr-admin, settings'te gerçek Sistem Ayarları sayfasını görür", () => {
  localStorage.setItem(SESSION_KEY, sessionFor("hr-admin", "İK Yöneticisi"));
  renderShellAt("/app/settings");
  // Dilim 7: settings placeholder'ı gerçek sayfayla değişti; sayfa önce yükleniyor durumundadır.
  expect(screen.queryByText("Bu alan hazırlanıyor")).not.toBeInTheDocument();
});

test("kök yol oturumsuz ziyaretçiye landing gösterir", async () => {
  renderAt("/");
  expect(await screen.findByRole("heading", { level: 1 })).toHaveTextContent("Ekibinizi tek yerden");
  expect(screen.getAllByRole("link", { name: "Giriş yap" })[0]).toHaveAttribute("href", "/giris");
});

test("eski /login yolu yeni giriş ekranına yönlenir", async () => {
  renderAt("/login");
  expect(await screen.findByRole("button", { name: /Giriş yap/ })).toBeInTheDocument();
});

test("oturumlu kullanıcı eski /dashboard yolundan /app/dashboard kabuğuna gider", async () => {
  // Employee, dashboard'da kilit ekranını görür — mevcut testteki kanıtlanmış senaryo.
  localStorage.setItem(SESSION_KEY, sessionFor("employee", "Ahmet Yılmaz"));
  renderShellAt("/dashboard");
  expect(await screen.findByText("Bu alan için yetki gerekli")).toBeInTheDocument();
});

test("bilinmeyen yol 404 sayfası gösterir", async () => {
  renderAt("/olmayan-sayfa");
  expect(await screen.findByRole("heading", { name: "Sayfa bulunamadı" })).toBeInTheDocument();
});
