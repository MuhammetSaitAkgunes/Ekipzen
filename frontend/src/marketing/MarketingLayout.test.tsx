import { act, render, screen, within } from "@testing-library/react";
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

test("site varsayılan olarak açık temada açılır, menü kaydırınca belirginleşir", () => {
  renderLayout();
  expect(document.querySelector(".ez-site")).toHaveClass("ez-light");
  expect(document.querySelector(".ez-grain")).toHaveAttribute("aria-hidden", "true");
  const header = document.querySelector(".ez-header")!;
  expect(header).not.toHaveClass("is-scrolled");
  act(() => {
    Object.defineProperty(window, "scrollY", { value: 120, configurable: true });
    window.dispatchEvent(new Event("scroll"));
  });
  expect(header).toHaveClass("is-scrolled");
  Object.defineProperty(window, "scrollY", { value: 0, configurable: true });
});

test("Escape mobil menüyü kapatır ve odağı açma düğmesine döndürür", async () => {
  renderLayout();
  await userEvent.click(screen.getByRole("button", { name: "Menüyü aç" }));
  within(screen.getByRole("navigation", { name: "Ana menü" })).getByRole("link", { name: "Özellikler" }).focus();
  await userEvent.keyboard("{Escape}");
  expect(screen.getByRole("button", { name: "Menüyü aç" })).toHaveFocus();
  expect(screen.getByRole("button", { name: "Menüyü aç" })).toHaveAttribute("aria-expanded", "false");
});


test("tema seçimi kalıcıdır ve tanıtım sitesi köküyle sınırlıdır", async () => {
  localStorage.setItem("ekipzen-marketing-theme", "dark");
  const first = renderLayout();
  const site = first.container.querySelector(".ez-site")!;
  expect(site).toHaveClass("ez-night");
  await userEvent.click(screen.getByRole("button", { name: "Açık temaya geç" }));
  expect(site).toHaveClass("ez-light");
  expect(site).not.toHaveClass("ez-night");
  expect(localStorage.getItem("ekipzen-marketing-theme")).toBe("light");
  expect(screen.getByRole("button", { name: "Koyu temaya geç" })).toHaveFocus();
  first.unmount();
  const second = renderLayout();
  expect(second.container.querySelector(".ez-site")).toHaveClass("ez-light");
  await userEvent.click(screen.getByRole("button", { name: "Koyu temaya geç" }));
  expect(second.container.querySelector(".ez-site")).toHaveClass("ez-night");
  expect(localStorage.getItem("ekipzen-marketing-theme")).toBe("dark");
});
