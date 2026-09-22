import { screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { HomePage } from "./HomePage";
import { modules, roadmap } from "../content";

test("hero vaadi, iki CTA ve sayfa başlığı", () => {
  renderPage(<HomePage />);
  const h1 = screen.getByRole("heading", { level: 1 });
  expect(h1).toHaveTextContent("İK süreçleriniz tek bir kayıt düzeninde");
  expect(screen.getAllByRole("link", { name: "Demo talep edin" })[0]).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: "Modülleri inceleyin" })).toHaveAttribute("href", "/ozellikler");
  expect(document.title).toBe("Ekipzen");
  expect(h1.closest(".ez-hero__copy")).not.toHaveClass("ez-reveal");
  expect(h1.querySelector(".ez-gradient-text")).toHaveTextContent("tek bir kayıt düzeninde");
});

test("giriş bölümü büyük ürün ekranını gösterir ve gözlemciye bağlı değildir", () => {
  renderPage(<HomePage />);
  const hero = document.querySelector(".ez-hero")!;
  expect(hero.querySelector(".ez-hero-screen .ez-app")).not.toBeNull();
  expect(hero.querySelector(".ez-reveal")).toBeNull();
  expect(hero.querySelector(".ez-scene-bg")).not.toBeNull();
  expect(hero.classList.contains("ez-gridlines")).toBe(false);
});

test("güvenlik bölümü dört katmanlı yığını gösterir", () => {
  renderPage(<HomePage />);
  const section = screen.getByRole("region", { name: "Her şirketin verisi kendi veritabanında tutulur" });
  const layers = section.querySelectorAll(".ez-sec-layers__layer");
  expect([...layers].map((l) => l.textContent)).toEqual(["Uygulama", "Oturum", "Yetki", "Veritabanı"]);
  expect(section.querySelector(".ez-sec-layers")).toHaveAttribute("aria-hidden", "true");
});

test("canlı vitrin ve çalışan yolculuğu bölümleri vardır", () => {
  renderPage(<HomePage />);
  const showcase = screen.getByRole("region", { name: "Modülleri çalışırken görün" });
  expect(within(showcase).getAllByRole("tab")).toHaveLength(6);
  expect(screen.getByRole("region", { name: "Bir çalışanın kaydı, işe alımdan bordroya" })).toBeInTheDocument();
});

test("altı modül kartı durum etiketiyle özellik sayfasına bağlanır", () => {
  renderPage(<HomePage />);
  const section = screen.getByRole("region", { name: "Birbirine bağlı altı modül" });
  for (const m of modules) {
    const link = within(section).getByRole("link", { name: new RegExp(m.title) });
    expect(link).toHaveAttribute("href", `/ozellikler#${m.id}`);
    expect(link.querySelector(".ez-status")).toHaveAttribute("data-status", m.status);
  }
});

test("gelişim şeridi yol haritasının ilk maddelerini ve tamamına bağlantıyı gösterir", () => {
  renderPage(<HomePage />);
  const strip = screen.getByRole("region", { name: "Ekipzen gelişmeye devam ediyor" });
  for (const stage of ["Şimdi", "Sıradaki", "Planlanan"]) expect(within(strip).getByText(stage)).toBeInTheDocument();
  expect(within(strip).getByText(roadmap[0].title)).toBeInTheDocument();
  expect(within(strip).getByRole("link", { name: "Yol haritasının tamamı" })).toHaveAttribute("href", "/hakkimizda#yol-haritasi");
});

test("güvenlik ve SSS bölümleri görünür; eski bölümler kaldırılmıştır", () => {
  renderPage(<HomePage />);
  expect(screen.getByRole("region", { name: "Her şirketin verisi kendi veritabanında tutulur" })).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "Sık sorulan sorular" })).toBeInTheDocument();
  expect(screen.queryByRole("region", { name: "Kullanıma geçiş üç adımda tamamlanır" })).toBeNull();
});

test("sahte müşteri yorumu ya da logo şeridi yoktur", () => {
  renderPage(<HomePage />);
  expect(screen.queryByText(/müşterimiz|bize güvenen/i)).toBeNull();
});
