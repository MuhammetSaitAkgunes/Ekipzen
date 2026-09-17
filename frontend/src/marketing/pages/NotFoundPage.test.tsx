import { afterEach, expect, test } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderPage } from "../../test/renderPage";
import { NotFoundPage } from "./NotFoundPage";

afterEach(cleanup);

test("404 sayfası takılıyken robots noindex meta etiketi eklenir ve ayrılınca kaldırılır", () => {
  expect(document.querySelector('meta[name="robots"]')).toBeNull();
  const { unmount } = renderPage(<NotFoundPage />);
  expect(screen.getByRole("heading", { name: "Sayfa bulunamadı" })).toBeInTheDocument();
  const meta = document.querySelector('meta[name="robots"]');
  expect(meta).not.toBeNull();
  expect(meta).toHaveAttribute("content", "noindex");
  unmount();
  expect(document.querySelector('meta[name="robots"]')).toBeNull();
});
