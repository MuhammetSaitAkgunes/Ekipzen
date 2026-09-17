import { render, screen } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import App from "./App";

test("oturum yokken kök adres landing sayfasını açar", async () => {
  localStorage.clear();
  render(<App />);
  expect(await screen.findByRole("heading", { level: 1 })).toHaveTextContent("Ekibinizi tek yerden");
});

afterEach(() => {
  window.location.hash = "";
});

test("replaceState hata fırlatsa bile modül yüklenmesi çökmez", async () => {
  window.location.hash = "#/login";
  const spy = vi.spyOn(window.history, "replaceState").mockImplementationOnce(() => {
    // Yalnız App.tsx'in kendi (ilk) çağrısı hata fırlatır; router'ın kendi iç
    // history kurulumu normal çalışmaya devam eder.
    throw new DOMException("bloklandı", "SecurityError");
  });
  vi.resetModules();
  await expect(import("./App")).resolves.toHaveProperty("default");
  spy.mockRestore();
  vi.resetModules();
});
