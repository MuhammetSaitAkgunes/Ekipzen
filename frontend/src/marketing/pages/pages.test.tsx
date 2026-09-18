import { act, screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { triggerIntersections } from "../../test/intersectionObserver";
import { modules, plans } from "../content";
import { AboutPage } from "./AboutPage";
import { FeaturesPage } from "./FeaturesPage";
import { KvkkPage } from "./KvkkPage";
import { PricingPage } from "./PricingPage";
import { SecurityPage } from "./SecurityPage";

test("Özellikler: her modül anchor id'li bir bölümdür; yapışkan menü görünen modülü izler", () => {
  renderPage(<FeaturesPage />);
  expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  for (const m of modules) {
    const section = document.getElementById(m.id);
    expect(section, m.id).not.toBeNull();
    expect(within(section!).getByRole("heading", { level: 2, name: m.title })).toBeInTheDocument();
    expect(section!.querySelector(".ez-status")).toHaveAttribute("data-status", m.status);
  }
  const nav = screen.getByRole("navigation", { name: "Modül menüsü" });
  act(() => triggerIntersections(true, (el) => el.id === "bordro"));
  expect(within(nav).getByRole("link", { name: /Bordro/ })).toHaveAttribute("aria-current", "true");
  expect(screen.getByRole("heading", { name: "İK yöneticisi, yönetici ve çalışan için ayrı çalışma alanları" })).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "Tüm modüller aynı personel kaydına bağlıdır" })).toBeInTheDocument();
  expect(document.title).toBe("Özellikler · Ekipzen");
});

test("Fiyatlandırma: üç paket, rakam yok, teklif CTA'sı ve karşılaştırma tablosu", () => {
  renderPage(<PricingPage />);
  for (const p of plans) {
    expect(screen.getByRole("heading", { level: 2, name: p.name })).toBeInTheDocument();
  }
  expect(screen.getAllByRole("link", { name: /teklif alın/i })).toHaveLength(plans.length);
  expect(screen.queryByText(/₺|TL\/ay|\$/)).toBeNull();
  const table = screen.getByRole("table", { name: "Paket karşılaştırması" });
  expect(within(table).getAllByRole("columnheader")).toHaveLength(4);
});

test("Güvenlik: katmanlar, kategoriler, şeffaflık ve KVKK bölümü", () => {
  renderPage(<SecurityPage />);
  expect(screen.getByRole("region", { name: "Katman katman güvenlik" })).toBeInTheDocument();
  const categories = screen.getByRole("region", { name: "Kontroller kategoriye göre" });
  expect(within(categories).getAllByRole("heading", { level: 3 })).toHaveLength(4);
  const transparencyRegion = screen.getByRole("region", { name: "Bugün sağlananlar ve üzerinde çalıştıklarımız" });
  expect(within(transparencyRegion).getByText("Çok faktörlü kimlik doğrulama")).toBeInTheDocument();
  expect(within(transparencyRegion).getAllByText("Geliştiriliyor")).toHaveLength(2);
  expect(screen.getByRole("heading", { name: "KVKK ve kişisel veriler" })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "KVKK aydınlatma metni" })).toHaveAttribute("href", "/kvkk-aydinlatma");
});

test("Hakkımızda: değerler listelenir, uydurma kişi ya da sayı yok", () => {
  renderPage(<AboutPage />);
  expect(screen.getByRole("heading", { name: "Değerlerimiz" })).toBeInTheDocument();
  expect(screen.queryByText(/kurucu|CEO|\d+\+ müşteri/i)).toBeNull();
});

test("KVKK: veri sorumlusu, saklama süresi ve haklar bölümleri", () => {
  renderPage(<KvkkPage />);
  expect(screen.getByRole("heading", { level: 1, name: "KVKK aydınlatma metni" })).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "Saklama süresi" })).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "İlgili kişi olarak haklarınız" })).toBeInTheDocument();
});
