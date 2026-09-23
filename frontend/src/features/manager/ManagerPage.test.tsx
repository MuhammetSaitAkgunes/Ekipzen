import { screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { ManagerPage } from "./ManagerPage";
test("izin analizi örnek veriyi şirket verisi olarak sunmaz", () => {
  renderPage(<ManagerPage />);
  expect(screen.getByText("Örnek analiz")).toBeInTheDocument();
  expect(screen.getByText(/şirketinizin gerçek izin kayıtlarını temsil etmez/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Bekleyen onaylara git" })).toHaveAttribute("href", "/app/inbox");
  expect(screen.queryByRole("button", { name: "Onayla" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Örnek raporu indir" })).toBeInTheDocument();
});
