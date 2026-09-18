import { act, render, screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { triggerIntersections } from "../test/intersectionObserver";
import { ProcessTimeline } from "./ProcessTimeline";
import { ModuleMaturity } from "./ModuleMaturity";
import { RoadmapBoard } from "./RoadmapBoard";
import { ROADMAP_NOTE, devProcess, roadmap } from "./content";

test("süreç çizgisi altı adımı gösterir ve görünür olunca etkinleşir", () => {
  const { container } = render(<ProcessTimeline />);
  expect(screen.getAllByRole("listitem")).toHaveLength(devProcess.length);
  expect(screen.getByText("Uzman kabulü")).toBeInTheDocument();
  const list = container.querySelector(".ez-process")!;
  expect(list).not.toHaveClass("is-active");
  act(() => triggerIntersections(true, (el) => el === list));
  expect(list).toHaveClass("is-active");
});

test("olgunluk görünümü modülleri durumlarına göre gruplar", () => {
  render(<ModuleMaturity />);
  const building = screen.getByRole("region", { name: "Geliştiriliyor" });
  expect(within(building).getByText("Risk Merkezi")).toBeInTheDocument();
  const pilot = screen.getByRole("region", { name: "Pilotta" });
  expect(within(pilot).getByText("Bordro")).toBeInTheDocument();
  expect(within(pilot).getByText("Mesai ve Puantaj")).toBeInTheDocument();
  const active = screen.getByRole("region", { name: "Kullanımda" });
  expect(within(active).getAllByRole("listitem")).toHaveLength(3);
});

test("yol haritası panosu üç aşamayı, tüm maddeleri ve tarih notunu gösterir", () => {
  render(<RoadmapBoard />);
  for (const stage of ["Şimdi", "Sıradaki", "Planlanan"]) expect(screen.getByRole("heading", { level: 3, name: stage })).toBeInTheDocument();
  expect(screen.getAllByRole("article")).toHaveLength(roadmap.length);
  expect(screen.getByText(ROADMAP_NOTE)).toBeInTheDocument();
});
