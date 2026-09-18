import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { setReducedMotion } from "../test/intersectionObserver";
import { ProductShowcase } from "./ProductShowcase";

beforeEach(() => { vi.useFakeTimers(); });
afterEach(() => { vi.useRealTimers(); });

const selectedTab = () => screen.getAllByRole("tab").find((t) => t.getAttribute("aria-selected") === "true")!;

test("sekme listesi ARIA ile bağlıdır; ilk modül seçilidir", () => {
  render(<ProductShowcase />);
  expect(screen.getByRole("tablist", { name: "Modüller" })).toBeInTheDocument();
  const tabs = screen.getAllByRole("tab");
  expect(tabs).toHaveLength(6);
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
  expect(tabs[0]).toHaveAttribute("tabindex", "0");
  expect(tabs[1]).toHaveAttribute("tabindex", "-1");
  const panel = screen.getByRole("tabpanel");
  expect(panel).toHaveAttribute("aria-labelledby", "showcase-tab-personel");
  expect(panel).toHaveTextContent("Kullanımda");
});

test("belirli aralıkla bir sonraki modüle geçer", () => {
  render(<ProductShowcase intervalMs={6000} />);
  act(() => { vi.advanceTimersByTime(6000); });
  expect(selectedTab()).toHaveTextContent("İzin Yönetimi");
});

test("kullanıcı sekme seçince otomatik ilerleme durur", () => {
  render(<ProductShowcase intervalMs={6000} />);
  fireEvent.click(screen.getAllByRole("tab")[3]);
  expect(selectedTab()).toHaveTextContent("Bordro");
  expect(screen.getByRole("tabpanel")).toHaveTextContent("Pilotta");
  act(() => { vi.advanceTimersByTime(20000); });
  expect(selectedTab()).toHaveTextContent("Bordro");
});

test("ok, Home ve End tuşlarıyla gezinilir ve odak taşınır", () => {
  render(<ProductShowcase />);
  const tablist = screen.getByRole("tablist");
  fireEvent.keyDown(tablist, { key: "ArrowRight" });
  expect(selectedTab()).toHaveTextContent("İzin Yönetimi");
  expect(selectedTab()).toHaveFocus();
  fireEvent.keyDown(tablist, { key: "End" });
  expect(selectedTab()).toHaveTextContent("Risk Merkezi");
  expect(screen.getByRole("tabpanel")).toHaveTextContent("Geliştiriliyor");
  fireEvent.keyDown(tablist, { key: "ArrowRight" });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
  fireEvent.keyDown(tablist, { key: "ArrowLeft" });
  expect(selectedTab()).toHaveTextContent("Risk Merkezi");
  fireEvent.keyDown(tablist, { key: "Home" });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
});

test("fare üzerindeyken durur, ayrılınca devam eder", () => {
  const { container } = render(<ProductShowcase intervalMs={6000} />);
  const root = container.querySelector(".ez-showcase")!;
  fireEvent.mouseEnter(root);
  act(() => { vi.advanceTimersByTime(12000); });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
  fireEvent.mouseLeave(root);
  act(() => { vi.advanceTimersByTime(6000); });
  expect(selectedTab()).toHaveTextContent("İzin Yönetimi");
});

test("hareket azaltılmışsa otomatik ilerlemez", () => {
  setReducedMotion(true);
  render(<ProductShowcase intervalMs={6000} />);
  act(() => { vi.advanceTimersByTime(18000); });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
});
