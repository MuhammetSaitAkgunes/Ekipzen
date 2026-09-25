import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { PositionModal } from "./PositionModal";

beforeEach(() => stubApi({
  "/api/departments": [{ id: 3, name: "Ürün", employeeCount: 5 }],
  "/api/positions": { id: 12, title: "QA Engineer", departmentId: 3, department: "Ürün", isOpen: true, openCount: 2, candidateCount: 0 },
}));
afterEach(() => vi.unstubAllGlobals());

const renderModal = () => renderPage(
  <ToastProvider>
    <PositionModal onClose={() => {}} />
  </ToastProvider>,
);

test("pozisyon ön kontrolden sonra seçilen departman ve kadro sayısıyla açılır", async () => {
  renderModal();
  await userEvent.type(screen.getByLabelText("Pozisyon adı"), "QA Engineer");
  await screen.findByRole("option", { name: "Ürün" });
  await userEvent.selectOptions(screen.getByLabelText("Departman"), "3");
  await userEvent.clear(screen.getByLabelText("Açık kadro sayısı"));
  await userEvent.type(screen.getByLabelText("Açık kadro sayısı"), "2");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  const review = screen.getByRole("region", { name: "Pozisyon ön kontrolü" });
  expect(review).toHaveTextContent("QA Engineer");
  expect(review).toHaveTextContent("Ürün");
  expect(review).toHaveTextContent("2");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Pozisyonu oluştur" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/positions" && i?.method === "POST",
    );
    expect(JSON.parse(String(posted?.[1]?.body))).toEqual({
      title: "QA Engineer", departmentId: 3, openCount: 2,
    });
  });
});

test("geçersiz kadro sayısı pozisyon kaydını durdurur", async () => {
  renderModal();
  await userEvent.type(screen.getByLabelText("Pozisyon adı"), "QA Engineer");
  await screen.findByRole("option", { name: "Ürün" });
  await userEvent.clear(screen.getByLabelText("Açık kadro sayısı"));
  await userEvent.type(screen.getByLabelText("Açık kadro sayısı"), "101");
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Açık kadro sayısı 1-100");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});

test("ön kontrolden sonra ad değişirse eski onay geçersiz olur", async () => {
  renderModal();
  await userEvent.type(screen.getByLabelText("Pozisyon adı"), "QA Engineer");
  await screen.findByRole("option", { name: "Ürün" });
  await userEvent.click(screen.getByRole("button", { name: "Bilgileri kontrol et" }));
  await userEvent.type(screen.getByLabelText("Pozisyon adı"), " Senior");
  expect(screen.queryByRole("button", { name: "Pozisyonu oluştur" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Bilgileri kontrol et" })).toBeInTheDocument();
});
