import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { DocumentsTab } from "./DocumentsTab";
import { AuthProvider } from "../../auth/AuthContext";

const documents = [
  { id: 9, documentType: "Kimlik", fileName: "kimlik.pdf", contentType: "application/pdf", sizeBytes: 1024, createdAtUtc: "2026-07-14T10:00:00Z" },
];

beforeEach(() => stubApi({
  "/api/employees/1/documents": documents,
  "/api/employees/1/documents/withdrawn": [],
  "/api/employees/1/documents/9/withdraw": {
    id: 9, documentType: "Kimlik", fileName: "kimlik.pdf",
    withdrawnAtUtc: "2026-07-15T10:00:00Z", withdrawalReason: "Yanlış kişi",
  },
}));
afterEach(() => vi.unstubAllGlobals());

const renderTab = (employeeId: number | null, readOnly = false) =>
  renderPage(
    <AuthProvider><ToastProvider>
      <DocumentsTab employeeId={employeeId} readOnly={readOnly} />
    </ToastProvider></AuthProvider>,
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
  expect(await screen.findByText(/Evrak listesi okunamadı/)).toBeInTheDocument();
  expect(screen.queryByText("Henüz evrak yok")).not.toBeInTheDocument();
});

test("evrak gerekçe ve son kontrol olmadan geri çekilmez", async () => {
  renderTab(1);
  await screen.findByText("kimlik.pdf");
  await userEvent.click(screen.getByRole("button", { name: "kimlik.pdf dosyasını geri çek" }));
  expect(screen.getByRole("heading", { name: "Evrakı geri çekmeden önce kontrol edin" })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Geri çekmeyi onayla" })).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/employees/1/documents/9/withdraw" && init?.method === "POST")).toBe(false);
  await userEvent.type(screen.getByLabelText("Geri çekme gerekçesi"), "Yanlış kişi");
  await userEvent.click(screen.getByRole("button", { name: "Geri çekmeyi onayla" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/1/documents/9/withdraw" && init?.method === "POST");
    expect(JSON.parse(String(posted?.[1]?.body))).toEqual({ reason: "Yanlış kişi" });
  });
});

test("salt okur kullanıcıya geri çekme ve geçmiş işlemi açılmaz", async () => {
  renderTab(1, true);
  await screen.findByText("kimlik.pdf");
  expect(screen.queryByRole("button", { name: "kimlik.pdf dosyasını geri çek" })).not.toBeInTheDocument();
  expect(screen.queryByRole("region", { name: "Geri çekilen evraklar" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url]) =>
    String(url) === "/api/employees/1/documents/withdrawn")).toBe(false);
});
