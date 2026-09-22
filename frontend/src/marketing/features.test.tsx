import { act, render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { triggerIntersections } from "../test/intersectionObserver";
import { DataFlowDiagram } from "./DataFlowDiagram";
import { modules } from "./content";

test("veri akışı şeması erişilebilir adla render edilir ve görünür olunca çizilir", () => {
  const { container } = render(<DataFlowDiagram />);
  expect(screen.getByRole("img", { name: "Modüller ve personel kaydı ilişkisi" })).toBeInTheDocument();
  const wrapper = container.querySelector(".ez-flow")!;
  expect(wrapper).not.toHaveClass("is-drawn");
  act(() => triggerIntersections(true));
  expect(wrapper).toHaveClass("is-drawn");
});

test("her modül kendi panelinde, numarasıyla ve büyük ürün ekranıyla durur", async () => {
  const { renderPage } = await import("../test/renderPage");
  const { FeaturesPage } = await import("./pages/FeaturesPage");
  renderPage(<FeaturesPage />);
  expect(document.querySelector(".ez-module-nav")).toBeNull();
  modules.forEach((m, i) => {
    const panel = document.getElementById(m.id)!;
    expect(panel, m.id).toHaveClass("ez-module-panel");
    expect(panel.querySelector(".ez-module-panel__index")).toHaveTextContent(String(i + 1).padStart(2, "0"));
    expect(panel.querySelector(`.ez-module-visual .ez-app[data-active="${m.id}"]`), m.id).not.toBeNull();
  });
  expect(document.querySelector(".ez-page-hero")).toHaveClass("ez-page-hero--cyan");
});
