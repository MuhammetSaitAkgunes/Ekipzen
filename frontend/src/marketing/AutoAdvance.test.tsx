import { act, fireEvent, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { renderPage } from "../test/renderPage";
import { setReducedMotion } from "../test/intersectionObserver";
import { ProductShowcase } from "./ProductShowcase";
import { EmployeeJourney } from "./EmployeeJourney";

beforeEach(() => { vi.useFakeTimers(); setReducedMotion(false); });
afterEach(() => { vi.useRealTimers(); setReducedMotion(false); });
const selected = () => screen.getAllByRole("tab").find((tab) => tab.getAttribute("aria-selected") === "true");
const advance = (ms: number) => act(() => { vi.advanceTimersByTime(ms); });

for (const { name, Component, intervalMs, rootClass } of [
  { name: "vitrin", Component: ProductShowcase, intervalMs: 6000, rootClass: ".ez-showcase" },
  { name: "yolculuk", Component: EmployeeJourney, intervalMs: 5000, rootClass: ".ez-journey" },
]) {
  test(name + ": açık durdurma kalıcıdır, sürdürme tam süreyle başlar", () => {
    renderPage(<Component intervalMs={intervalMs} />);
    const first = selected();
    advance(intervalMs - 100);
    fireEvent.click(screen.getByRole("button", { name: "Otomatik geçişi durdur" }));
    advance(intervalMs * 3);
    expect(selected()).toBe(first);
    fireEvent.click(screen.getByRole("button", { name: "Otomatik geçişi sürdür" }));
    advance(intervalMs - 1);
    expect(selected()).toBe(first);
    advance(1);
    expect(selected()).not.toBe(first);
  });

  test(name + ": fare ayrılsa da odak içerideyken geçiş durur", () => {
    const { container } = renderPage(<Component intervalMs={intervalMs} />);
    const first = selected()!;
    const root = container.querySelector(rootClass)!;
    fireEvent.mouseEnter(root);
    fireEvent.focus(first);
    fireEvent.mouseLeave(root);
    advance(intervalMs * 2);
    expect(selected()).toBe(first);
    fireEvent.blur(first, { relatedTarget: null });
    advance(intervalMs);
    expect(selected()).not.toBe(first);
  });

  test(name + ": elle seçimden sonra sürdürme odak ayrılana kadar bekler", () => {
    const { container } = renderPage(<Component intervalMs={intervalMs} />);
    const tab = screen.getAllByRole("tab")[2];
    fireEvent.click(tab);
    fireEvent.mouseLeave(container.querySelector(rootClass)!);
    advance(intervalMs * 2);
    expect(selected()).toBe(tab);
    const resume = screen.getByRole("button", { name: "Otomatik geçişi sürdür" });
    fireEvent.focus(resume);
    fireEvent.click(resume);
    advance(intervalMs * 2);
    expect(selected()).toBe(tab);
    fireEvent.blur(resume, { relatedTarget: null });
    advance(intervalMs);
    expect(selected()).not.toBe(tab);
  });

  test(name + ": azaltılmış harekette yalnız elle seçim yapılır", () => {
    setReducedMotion(true);
    renderPage(<Component intervalMs={intervalMs} />);
    expect(screen.queryByRole("button", { name: /Otomatik geçişi/ })).toBeNull();
    const tab = screen.getAllByRole("tab")[1];
    fireEvent.click(tab);
    advance(intervalMs * 3);
    expect(selected()).toBe(tab);
  });
}

test("mobil vitrin gezinmesi yalnız taşma olduğunda sunulur", () => {
  renderPage(<ProductShowcase />);
  expect(screen.queryByRole("button", { name: "Sonraki modül" })).toBeNull();
  const tabs = screen.getByRole("tablist");
  Object.defineProperty(tabs, "scrollWidth", { configurable: true, value: 1000 });
  Object.defineProperty(tabs, "clientWidth", { configurable: true, value: 320 });
  fireEvent(window, new Event("resize"));
  expect(screen.getByRole("button", { name: "Önceki modül" })).toBeDisabled();
  fireEvent.click(screen.getByRole("button", { name: "Sonraki modül" }));
  expect(selected()).toHaveTextContent("İzin Yönetimi");
  Object.defineProperty(tabs, "clientWidth", { configurable: true, value: 1200 });
  fireEvent(window, new Event("resize"));
  expect(screen.queryByRole("button", { name: "Sonraki modül" })).toBeNull();
});
