import { render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { StatusBadge } from "./StatusBadge";

test.each([
  ["active", "Kullanımda", "ez-badge--success"],
  ["pilot", "Pilotta", "ez-badge--warning"],
  ["building", "Geliştiriliyor", "ez-badge--primary"],
] as const)("%s durumu %s etiketi ve doğru tonla gösterilir", (status, label, cls) => {
  render(<StatusBadge status={status} />);
  const badge = screen.getByText(label);
  expect(badge).toHaveClass(cls);
  expect(badge.closest(".ez-status")).toHaveAttribute("data-status", status);
  expect(badge.closest(".ez-status")!.getAttribute("title")).toBeTruthy();
});
