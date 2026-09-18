import { render } from "@testing-library/react";
import { expect, test } from "vitest";
import { HeroVisual, ModuleVisual } from "./visuals";
import { modules } from "./content";

test("her modülün örnek ekranı vardır ve erişilebilir açıklama taşır", () => {
  for (const m of modules) {
    const { container, unmount } = render(<ModuleVisual id={m.id} />);
    expect(container.querySelector(".ez-mock"), m.id).not.toBeNull();
    expect(container.querySelector("figcaption")).toHaveTextContent("örnek görünüm");
    unmount();
  }
});

test("hero kompozisyonu düzdür ve süzülen iki kart içerir", () => {
  const { container } = render(<HeroVisual />);
  expect(container.querySelector(".ez-hero-visual__main .ez-mock")).not.toBeNull();
  expect(container.querySelectorAll(".ez-hero-visual__float")).toHaveLength(2);
  expect(container.querySelector("figcaption")).toHaveTextContent("örnek görünüm");
});
