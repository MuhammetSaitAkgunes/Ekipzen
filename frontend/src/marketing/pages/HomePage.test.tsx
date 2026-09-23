import { screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { HomePage } from "./HomePage";
import { modules } from "../content";

test("hero vaadi, iki CTA ve sayfa başlığı", () => {
  renderPage(<HomePage />);
  const h1 = screen.getByRole("heading", { level: 1 });
  expect(h1).toHaveTextContent("Personel ve izin süreçlerinizi tek yerden yönetin.");
  expect(screen.getAllByRole("link", { name: "Demo talep edin" })[0]).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: "Modülleri inceleyin" })).toHaveAttribute("href", "/ozellikler");
  expect(document.title).toBe("Ekipzen");
  expect(h1.closest(".ez-hero__copy")).not.toHaveClass("ez-reveal");
  expect(h1.querySelector(".ez-gradient-text")).toHaveTextContent("tek yerden yönetin.");
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
  const showcase = screen.getByRole("region", { name: "Ekipzen ekranlarını keşfedin" });
  expect(within(showcase).getAllByRole("tab")).toHaveLength(6);
  expect(screen.getByRole("region", { name: "Çalışan kaydı süreçler arasında nasıl ilerler?" })).toBeInTheDocument();
});

test("modül kapsamı katalogdan türetilir ve yol haritasına bağlanır", () => {
  renderPage(<HomePage />);
  const scope = screen.getByLabelText("Modüllerin güncel durumu");
  for (const m of modules) expect(scope).toHaveTextContent(m.title);
  expect(within(scope).getAllByRole("listitem")).toHaveLength(3);
  expect(within(scope).getByRole("link", { name: "Yol haritasının tamamı" })).toHaveAttribute("href", "/hakkimizda#yol-haritasi");
});

test("rol ve geçiş anlatımları ürün yolculuğundan sonra gelir", () => {
  renderPage(<HomePage />);
  const headings = screen.getAllByRole("heading", { level: 2 }).map((h) => h.textContent);
  expect(headings).toEqual(["Ekipzen ekranlarını keşfedin", "Çalışan kaydı süreçler arasında nasıl ilerler?", "Her rol için açık ve odaklı bir çalışma alanı", "Ekipzen ile kullanıma geçiş", "Her şirketin verisi kendi veritabanında tutulur", "Sık sorulan sorular", "İK süreçleriniz için Ekipzen'i keşfedin"]);
  expect(screen.getByText(/Kurulum ve veri aktarımı desteğinin kapsamı/)).toBeInTheDocument();
  expect(screen.getByText("Örnek verilerle hazırlanmış temsili ürün ekranı")).toBeVisible();
});

test("sahte müşteri yorumu ya da logo şeridi yoktur", () => {
  renderPage(<HomePage />);
  expect(screen.queryByText(/müşterimiz|bize güvenen/i)).toBeNull();
});
