import { fireEvent, render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { SecurityLayers } from "./SecurityLayers";
import { securityLayers } from "./content";

test("beş katman listelenir; ilki açıktır", () => {
  render(<SecurityLayers />);
  const buttons = screen.getAllByRole("button");
  expect(buttons).toHaveLength(5);
  expect(buttons[0]).toHaveAttribute("aria-expanded", "true");
  expect(screen.getByText(securityLayers[0].text)).toBeVisible();
  expect(screen.getByText(securityLayers[3].text)).not.toBeVisible();
});

test("bir katman seçildiğinde açıklaması açılır, diğeri kapanır", () => {
  render(<SecurityLayers />);
  fireEvent.click(screen.getByRole("button", { name: /Şirkete ait veritabanı/ }));
  expect(screen.getByRole("button", { name: /Şirkete ait veritabanı/ })).toHaveAttribute("aria-expanded", "true");
  expect(screen.getByText(securityLayers[3].text)).toBeVisible();
  expect(screen.getAllByRole("button")[0]).toHaveAttribute("aria-expanded", "false");
  expect(screen.getByText(securityLayers[0].text)).not.toBeVisible();
});
