import { screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { HomePage } from "./HomePage";
import { modules } from "../content";

test("hero vaadi, iki CTA ve sayfa başlığı", () => {
  renderPage(<HomePage />);
  expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Ekibinizi tek yerden, güvenle yönetin");
  expect(screen.getAllByRole("link", { name: "Demo iste" })[0]).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: "Özellikleri incele" })).toHaveAttribute("href", "/ozellikler");
  expect(document.title).toBe("Ekipzen");
});

test("altı modül kartı özellik sayfasına bağlanır", () => {
  renderPage(<HomePage />);
  const section = screen.getByRole("region", { name: "Tek platformda tüm İK süreçleri" });
  expect(modules).toHaveLength(6);
  for (const m of modules) {
    expect(within(section).getByRole("link", { name: new RegExp(m.title) })).toHaveAttribute("href", `/ozellikler#${m.id}`);
  }
});

test("SSS, nasıl çalışır ve güvenlik bölümleri görünür", () => {
  renderPage(<HomePage />);
  expect(screen.getByRole("region", { name: "Sık sorulan sorular" })).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "Üç adımda başlayın" })).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "Verileriniz sizin kontrolünüzde" })).toBeInTheDocument();
});

test("sahte müşteri yorumu ya da logo şeridi yoktur", () => {
  renderPage(<HomePage />);
  expect(screen.queryByText(/müşterimiz|bize güvenen/i)).toBeNull();
});
