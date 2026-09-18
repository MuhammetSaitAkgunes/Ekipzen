import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, test } from "vitest";
import { AuthProvider } from "../auth/AuthContext";
import { SESSION_KEY } from "../api/session";
import { MarketingLayout } from "./MarketingLayout";

const renderLayout = () => {
  const router = createMemoryRouter(
    [{ element: <MarketingLayout />, children: [{ path: "/", element: <p>içerik</p> }, { path: "/ozellikler", element: <p>özellikler</p> }] }],
    { initialEntries: ["/"] },
  );
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider><RouterProvider router={router} /></AuthProvider>
    </QueryClientProvider>,
  );
};

beforeEach(() => localStorage.clear());

test("ana menü, giriş ve demo bağlantılarını içerir", () => {
  renderLayout();
  const nav = screen.getByRole("navigation", { name: "Ana menü" });
  for (const label of ["Özellikler", "Fiyatlandırma", "Güvenlik", "Hakkımızda"]) {
    expect(within(nav).getByRole("link", { name: label })).toBeInTheDocument();
  }
  expect(screen.getAllByRole("link", { name: "Giriş yap" })[0]).toHaveAttribute("href", "/giris");
  expect(screen.getAllByRole("link", { name: "Demo talep edin" })[0]).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: "İçeriğe geç" })).toHaveAttribute("href", "#icerik");
});

test("mobil menü düğmesi aria-expanded durumunu değiştirir ve gezinmede kapanır", async () => {
  renderLayout();
  const toggle = screen.getByRole("button", { name: "Menüyü aç" });
  expect(toggle).toHaveAttribute("aria-expanded", "false");
  await userEvent.click(toggle);
  expect(screen.getByRole("button", { name: "Menüyü kapat" })).toHaveAttribute("aria-expanded", "true");
  await userEvent.click(within(screen.getByRole("navigation", { name: "Ana menü" })).getByRole("link", { name: "Özellikler" }));
  expect(await screen.findByText("özellikler")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Menüyü aç" })).toHaveAttribute("aria-expanded", "false");
});

test("oturum açık kullanıcı 'Uygulamaya git' görür", () => {
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "Ahmet", email: "a@x", role: "employee", roleLabel: "Çalışan", initials: "AY", employeeId: null },
  }));
  renderLayout();
  expect(screen.getAllByRole("link", { name: "Uygulamaya git" })[0]).toHaveAttribute("href", "/app/overview");
  expect(screen.queryByRole("link", { name: "Giriş yap" })).toBeNull();
});

test("footer yasal ve iletişim bağlantılarını içerir", () => {
  renderLayout();
  const footer = screen.getByRole("contentinfo");
  expect(within(footer).getByRole("link", { name: "KVKK aydınlatma metni" })).toHaveAttribute("href", "/kvkk-aydinlatma");
  expect(within(footer).getByRole("link", { name: "İletişim" })).toHaveAttribute("href", "/iletisim");
});
