import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { DocumentsTab } from "./DocumentsTab";

const documents = [
  { id: 9, documentType: "Kimlik", fileName: "kimlik.pdf", contentType: "application/pdf", sizeBytes: 1024, createdAtUtc: "2026-07-14T10:00:00Z" },
];

beforeEach(() => stubApi({ "/api/employees/1/documents": documents }));
afterEach(() => vi.unstubAllGlobals());

const renderTab = (employeeId: number | null, readOnly = false) =>
  renderPage(
    <ToastProvider>
      <DocumentsTab employeeId={employeeId} readOnly={readOnly} />
    </ToastProvider>,
  );

test("evrak listesi dolar", async () => {
  renderTab(1);
  expect(await screen.findByText("kimlik.pdf")).toBeInTheDocument();
  expect(screen.getByText("Kimlik")).toBeInTheDocument();
});

test("dosya seçilince FormData ile yükleme isteği gider", async () => {
  renderTab(1);
  await screen.findByText("kimlik.pdf");
  const file = new File(["icerik"], "ikametgah.pdf", { type: "application/pdf" });
  await userEvent.type(screen.getByLabelText("Evrak Türü"), "İkametgâh belgesi");
  await userEvent.upload(screen.getByLabelText("Evrak dosyası seç"), file);
  expect(screen.getByRole("heading", { name: "Yüklemeden önce kontrol edin" })).toBeInTheDocument();
  expect(screen.getByText(/ikametgah.pdf/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/employees/1/documents" && i?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Dosyayı yükle" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/employees/1/documents" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(posted![1]?.body).toBeInstanceOf(FormData);
    expect((posted![1]?.body as FormData).get("documentType")).toBe("İkametgâh belgesi");
  });
});

test("yeni kayıtta bilgi notu görünür", () => {
  renderTab(null);
  expect(screen.getByText(/önce personel kaydını oluşturun/i)).toBeInTheDocument();
});

test("yanlış dosya seçiminden vazgeçmek yükleme yapmaz", async () => {
  renderTab(1);
  await screen.findByText("kimlik.pdf");
  await userEvent.upload(screen.getByLabelText("Evrak dosyası seç"),
    new File(["icerik"], "yanlis.pdf", { type: "application/pdf" }));
  await userEvent.click(screen.getByRole("button", { name: "Seçimi kaldır" }));
  expect(screen.queryByRole("heading", { name: "Yüklemeden önce kontrol edin" })).not.toBeInTheDocument();
  expect(screen.getByLabelText("Evrak dosyası seç")).toHaveFocus();
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/employees/1/documents" && i?.method === "POST")).toBe(false);
});

test("belge listesi okunamazsa boş kayıt olarak gösterilmez", async () => {
  vi.stubGlobal("fetch", vi.fn(async () => new Response(
    JSON.stringify({ title: "Geçici hata" }),
    { status: 503, headers: { "Content-Type": "application/json" } },
  )));
  renderTab(1);
  expect(await screen.findByRole("alert")).toHaveTextContent("Evrak listesi okunamadı");
  expect(screen.queryByText("Henüz evrak yok")).not.toBeInTheDocument();
});
