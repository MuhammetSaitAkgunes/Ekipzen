import { act, render, screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { triggerIntersections } from "../test/intersectionObserver";
import { StickyModuleNav } from "./StickyModuleNav";
import { DataFlowDiagram } from "./DataFlowDiagram";
import { modules } from "./content";

const items = modules.map((m) => ({ id: m.id, title: m.title, status: m.status }));

test("yapışkan menü tüm modüllere bağlanır ve aktif olanı işaretler", () => {
  render(<StickyModuleNav items={items} activeId="bordro" />);
  const nav = screen.getByRole("navigation", { name: "Modül menüsü" });
  const links = within(nav).getAllByRole("link");
  expect(links).toHaveLength(6);
  expect(within(nav).getByRole("link", { name: /Bordro/ })).toHaveAttribute("href", "#bordro");
  expect(within(nav).getByRole("link", { name: /Bordro/ })).toHaveAttribute("aria-current", "true");
  expect(within(nav).getByRole("link", { name: /Personel/ })).not.toHaveAttribute("aria-current");
  const index = modules.findIndex((m) => m.id === "bordro");
  expect((nav as HTMLElement).style.getPropertyValue("--progress")).toBe(String((index + 1) / modules.length));
});

test("veri akışı şeması erişilebilir adla render edilir ve görünür olunca çizilir", () => {
  const { container } = render(<DataFlowDiagram />);
  expect(screen.getByRole("img", { name: "Modüller ve personel kaydı ilişkisi" })).toBeInTheDocument();
  const wrapper = container.querySelector(".ez-flow")!;
  expect(wrapper).not.toHaveClass("is-drawn");
  act(() => triggerIntersections(true));
  expect(wrapper).toHaveClass("is-drawn");
});

test("özellikler sayfasında her modül bloğu büyük ürün ekranını gösterir ve yerleşim sırayla yön değiştirir", async () => {
  const { renderPage } = await import("../test/renderPage");
  const { FeaturesPage } = await import("./pages/FeaturesPage");
  renderPage(<FeaturesPage />);
  modules.forEach((m, i) => {
    const block = document.getElementById(m.id)!;
    expect(block.querySelector(`.ez-module-visual .ez-app[data-active="${m.id}"]`), m.id).not.toBeNull();
    if (i % 2 === 1) expect(block).toHaveClass("ez-feature-block--flip");
    else expect(block).not.toHaveClass("ez-feature-block--flip");
  });
  expect(document.querySelector(".ez-page-hero")).toHaveClass("ez-page-hero--cyan");
});
