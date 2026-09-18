import { act, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { expect, test, vi } from "vitest";
import { renderPage } from "../../test/renderPage";
import { triggerIntersections } from "../../test/intersectionObserver";
import { STATUS_META, modules, plans } from "../content";
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

// M1: hero'daki modül çipleri durumu yalnız nokta rengiyle taşıyordu (renk körlüğünde/renksiz
// okuyucularda kaybolur). Artık ekran okuyucu için metin ve fare için title ipucu da taşır.
test("Özellikler: hero modül çipleri durumu yalnız renkle değil, erişilebilir metin ve title ile de taşır", () => {
  renderPage(<FeaturesPage />);
  const chipsNav = screen.getByRole("navigation", { name: "Modüller" });
  for (const m of modules) {
    const link = within(chipsNav).getByRole("link", { name: new RegExp(`${m.title}.*${STATUS_META[m.status].label}`) });
    expect(link).toHaveAttribute("title", STATUS_META[m.status].label);
  }
});

// M2: MarketingLayout ve FeaturesPage her ikisi de useScrollToHash() çağırıyordu; aynı hash için
// scrollIntoView iki kez tetikleniyordu. Layout artık çağırmıyor; FeaturesPage kendi anchor'larını
// kendi yönetir (bkz. MarketingLayout.test.tsx'ten kaldırılan test).
test("Özellikler: adreste hash varsa ilgili modül bölümüne kaydırır", async () => {
  const scrollIntoView = vi.fn();
  Element.prototype.scrollIntoView = scrollIntoView;
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter initialEntries={["/ozellikler#bordro"]}>
        <FeaturesPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
  await waitFor(() => expect(scrollIntoView).toHaveBeenCalled());
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
  expect(screen.queryByText("En çok tercih edilen")).toBeNull();
  expect(screen.getByText("Önerilen")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Ürün nasıl gelişiyor?" })).toHaveAttribute("href", "/hakkimizda#yol-haritasi");
});

// I1: hover/lift efekti .ez-reveal.is-visible { transform: none } tarafından eziliyordu çünkü
// aynı eleman hem .ez-reveal hem .ez-plan taşıyordu. Kart artık .ez-plans__item (reveal wrapper)
// içinde ayrı bir .ez-plan div'i olarak render edilir.
test("Fiyatlandırma: paket kartının hover efekti reveal wrapper'dan ayrı bir elemanda çalışır", () => {
  renderPage(<PricingPage />);
  const items = document.querySelectorAll(".ez-plans__item");
  expect(items).toHaveLength(plans.length);
  for (const item of Array.from(items)) {
    expect(item).toHaveClass("ez-reveal");
    expect(item).not.toHaveClass("ez-plan");
    expect(item.querySelector(":scope > .ez-plan")).not.toBeNull();
  }
  const featured = document.querySelector(".ez-plan--featured")!;
  expect(featured).toHaveClass("ez-plan");
  expect(featured).not.toHaveClass("ez-reveal");
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

test("Hakkımızda: manifesto, ilkeler, süreç, olgunluk, yol haritası ve taahhütler; uydurma kişi ya da sayı yok", () => {
  renderPage(<AboutPage />);
  const h1 = screen.getByRole("heading", { level: 1 });
  expect(h1).toHaveTextContent("İK yazılımını, İK ekiplerinin günlük gerçekliğinden başlayarak geliştiriyoruz.");
  expect(h1.closest(".ez-reveal")).toBeNull();
  for (const name of ["Neden varız", "İlkelerimiz", "Ürünü nasıl geliştiriyoruz", "Ekipzen bugün", "Yol haritası", "Taahhütlerimiz"]) {
    expect(screen.getByRole("region", { name })).toBeInTheDocument();
  }
  expect(document.getElementById("yol-haritasi")).toHaveAccessibleName("Yol haritası");
  expect(screen.getByText("Hazır olanla geliştirileni ayırırız.")).toBeInTheDocument();
  expect(screen.queryByText(/kurucu|CEO|\d+\+ müşteri/i)).toBeNull();
});

test("KVKK: veri sorumlusu, saklama süresi ve haklar bölümleri", () => {
  renderPage(<KvkkPage />);
  expect(screen.getByRole("heading", { level: 1, name: "KVKK aydınlatma metni" })).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "Saklama süresi" })).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "İlgili kişi olarak haklarınız" })).toBeInTheDocument();
});
