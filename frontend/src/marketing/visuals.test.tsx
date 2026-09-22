import { render } from "@testing-library/react";
import { expect, test } from "vitest";
import { AuthScreen, HeroScreen, ModuleVisual } from "./visuals";
import { modules } from "./content";

test("her modülün büyük ürün ekranı vardır, menüde kendi modülü seçilidir", () => {
  for (const m of modules) {
    const { container, unmount } = render(<ModuleVisual id={m.id} />);
    const app = container.querySelector(".ez-app");
    expect(app, m.id).not.toBeNull();
    expect(app!.getAttribute("data-active")).toBe(m.id);
    expect(container.querySelector(".ez-app__nav-item.is-active"), m.id).not.toBeNull();
    expect(container.querySelector(".ez-notice"), m.id).not.toBeNull();
    expect(container.querySelector("figcaption")).toHaveTextContent("örnek görünüm");
    expect(container.querySelector("[aria-hidden='true'] .ez-app")).not.toBeNull();
    unmount();
  }
});

test("bilinmeyen modül için görsel çizilmez", () => {
  const { container } = render(<ModuleVisual id="yok" />);
  expect(container.firstChild).toBeNull();
});

test("giriş bölümü ekranı genel bakışı ve üç bildirimi gösterir", () => {
  const { container } = render(<HeroScreen />);
  expect(container.querySelector(".ez-app[data-active='overview']")).not.toBeNull();
  expect(container.querySelectorAll(".ez-notice")).toHaveLength(3);
  expect(container.querySelector("figcaption")).toHaveTextContent("Örnek verilerle hazırlanmış temsili ürün ekranı");
});

test("giriş sayfası görseli genel bakış ve bir bildirim içerir", () => {
  const { container } = render(<AuthScreen />);
  expect(container.querySelector(".ez-app[data-active='overview']")).not.toBeNull();
  expect(container.querySelectorAll(".ez-notice")).toHaveLength(1);
});

test("Risk Merkezi ekranı geliştirme aşamasında olduğunu belirtir", () => {
  const { container } = render(<ModuleVisual id="risk-merkezi" />);
  expect(container.textContent).toMatch(/geliştiriliyor/i);
});
