import { act, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { expect, test, vi } from "vitest";
import { setReducedMotion } from "../test/intersectionObserver";
import { EmployeeJourney } from "./EmployeeJourney";

test("başlangıçta ilk adım seçilidir ve yalnız ilk alan doludur", () => {
  render(<EmployeeJourney />);
  const tabs = screen.getAllByRole("tab");
  expect(tabs).toHaveLength(5);
  expect(tabs[0]).toHaveAttribute("aria-selected", "true");
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("İşe alındı")).toBeInTheDocument();
  expect(within(card).queryByText("Hesaplamaya hazır")).toBeNull();
});

test("bir adım seçildiğinde kart o adıma kadar dolar", async () => {
  render(<EmployeeJourney />);
  await userEvent.click(screen.getByRole("tab", { name: /İzin/ }));
  const tabs = screen.getAllByRole("tab");
  expect(tabs[2]).toHaveAttribute("aria-selected", "true");
  expect(tabs[0]).toHaveAttribute("aria-selected", "false");
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("14 gün")).toBeInTheDocument();
  expect(within(card).queryByText("21 iş günü")).toBeNull();
});

test("son adımda tüm alanlar dolar", async () => {
  render(<EmployeeJourney />);
  await userEvent.click(screen.getByRole("tab", { name: /Bordro/ }));
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("Hesaplamaya hazır")).toBeInTheDocument();
});

test("ok tuşlarıyla adımlar arasında gezilir", async () => {
  render(<EmployeeJourney />);
  const tabs = screen.getAllByRole("tab");
  tabs[0].focus();
  await userEvent.keyboard("{ArrowRight}");
  expect(screen.getAllByRole("tab")[1]).toHaveAttribute("aria-selected", "true");
  await userEvent.keyboard("{ArrowLeft}{ArrowLeft}");
  expect(screen.getAllByRole("tab")[4]).toHaveAttribute("aria-selected", "true");
});

test("adımlar kendiliğinden ilerler; hareket azaltılmışsa durur", () => {
  vi.useFakeTimers();
  try {
    const { unmount } = render(<EmployeeJourney intervalMs={1000} />);
    act(() => { vi.advanceTimersByTime(1000); });
    expect(screen.getAllByRole("tab")[1]).toHaveAttribute("aria-selected", "true");
    unmount();

    setReducedMotion(true);
    render(<EmployeeJourney intervalMs={1000} />);
    act(() => { vi.advanceTimersByTime(3000); });
    expect(screen.getAllByRole("tab")[0]).toHaveAttribute("aria-selected", "true");
  } finally {
    vi.useRealTimers();
    setReducedMotion(false);
  }
});
