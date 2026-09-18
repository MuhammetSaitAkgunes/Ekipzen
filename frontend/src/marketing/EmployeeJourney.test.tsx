import { act, render, screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { triggerIntersections } from "../test/intersectionObserver";
import { EmployeeJourney } from "./EmployeeJourney";

test("başlangıçta ilk adım aktiftir ve yalnız ilk alan doludur", () => {
  render(<EmployeeJourney />);
  const steps = screen.getAllByRole("listitem");
  expect(steps).toHaveLength(5);
  expect(steps[0]).toHaveAttribute("aria-current", "step");
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("İşe alındı")).toBeInTheDocument();
  expect(within(card).queryByText("Hesaplamaya hazır")).toBeNull();
});

test("görünüme giren adım aktifleşir ve kart o adıma kadar dolar", () => {
  render(<EmployeeJourney />);
  act(() => triggerIntersections(true, (el) => el.textContent?.includes("Talepler aynı kayıt") ?? false));
  const steps = screen.getAllByRole("listitem");
  expect(steps[2]).toHaveAttribute("aria-current", "step");
  expect(steps[0]).not.toHaveAttribute("aria-current");
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("14 gün")).toBeInTheDocument();
  expect(within(card).queryByText("21 iş günü")).toBeNull();
});

test("son adıma gelindiğinde tüm alanlar dolar", () => {
  render(<EmployeeJourney />);
  act(() => triggerIntersections(true));
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("Hesaplamaya hazır")).toBeInTheDocument();
  expect(screen.getAllByRole("listitem")[4]).toHaveAttribute("aria-current", "step");
});
