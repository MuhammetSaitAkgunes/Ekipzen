import { AuthProvider } from "../../auth/AuthContext";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { DocumentModal } from "./DocumentModal";

const employees = {
  items: [{ id: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım", status: "Aktif" }],
  totalCount: 1, page: 1, pageSize: 100,
};
const existing = {
  id: 1, employeeId: 3, employee: "Ahmet Yılmaz", dept: "Yazılım", document: "İş Sözleşmesi",
  owner: "Ayşe Demir", dueDate: "2026-07-20", dueLabel: "4 gün", status: "Eksik", level: "high",
};

beforeEach(() =>
  stubApi({
    "/api/employees": employees,
    "/api/compliance/documents": existing,
    "/api/compliance/documents/1": existing,
    "/api/compliance/documents/1/owner": existing,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderModal = (doc: typeof existing | null) =>
  renderPage(
    <AuthProvider><ToastProvider>
      <DocumentModal document={doc} onClose={() => {}} />
    </ToastProvider></AuthProvider>,
  );

test("oluşturma modu POST documents ucuna tam gövdeyle gider", async () => {
  renderModal(null);
  await screen.findByRole("option", { name: "Ahmet Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Personel"), "3");
  await userEvent.type(screen.getByLabelText("Belge adı"), "Sağlık Raporu");
  await userEvent.type(screen.getByLabelText("Sorumlu"), "Ayşe Demir");
  expect(screen.getByLabelText("Durum")).toHaveValue("");
  expect(screen.getByLabelText("Risk seviyesi")).toHaveValue("");
  await userEvent.selectOptions(screen.getByLabelText("Durum"), "Eksik");
  await userEvent.selectOptions(screen.getByLabelText("Risk seviyesi"), "medium");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  expect(screen.getByRole("region", { name: "Belge kaydını kaydetmeden önce kontrol edin" })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/compliance/documents" && init?.method === "POST")).toBe(false);
  expect(screen.getByRole("button", { name: "Onayla ve kaydet" })).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox", { name: /Personel, belge, durum, risk/ }));
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      employeeId: 3, documentName: "Sağlık Raporu", ownerName: "Ayşe Demir", level: "medium", status: "Eksik",
    });
  });
});

test("düzenleme modu PUT atar, sorumlu değişince owner PATCH'i de gider", async () => {
  renderModal(existing);
  const name = await screen.findByLabelText("Belge adı");
  expect(name).toHaveValue("İş Sözleşmesi");
  await userEvent.clear(screen.getByLabelText("Sorumlu"));
  await userEvent.type(screen.getByLabelText("Sorumlu"), "Ece Arslan");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  expect(screen.getByText(/ayrı işlemle güncellenecek/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole("checkbox", { name: /Personel, belge, durum, risk/ }));
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents/1" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    expect(JSON.parse(String(put![1]?.body))).toMatchObject({ documentName: "İş Sözleşmesi", level: "high" });
    const owner = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents/1/owner" && i?.method === "PATCH",
    );
    expect(owner).toBeTruthy();
    expect(JSON.parse(String(owner![1]?.body))).toMatchObject({ ownerName: "Ece Arslan" });
  });
});

test("mevcut sorumluyu boşaltmayı kaydedilmiş gibi göstermez", async () => {
  renderModal(existing);
  await screen.findByLabelText("Sorumlu");
  await userEvent.clear(screen.getByLabelText("Sorumlu"));
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Mevcut sorumlu bu ekrandan kaldırılamaz");
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/compliance/documents/1" && init?.method === "PUT")).toBe(false);
});

test("sorumlu ataması başarısızsa ilk güncellemenin kaydedildiğini açıklar", async () => {
  const original = vi.mocked(fetch).getMockImplementation()!;
  vi.mocked(fetch).mockImplementation((input, init) =>
    String(input) === "/api/compliance/documents/1/owner" && init?.method === "PATCH"
      ? Promise.resolve(Response.json({ title: "Sorumlu atanamadı" }, { status: 500 }))
      : original(input, init));
  renderModal(existing);
  await screen.findByLabelText("Sorumlu");
  await userEvent.clear(screen.getByLabelText("Sorumlu"));
  await userEvent.type(screen.getByLabelText("Sorumlu"), "Ece Arslan");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Personel, belge, durum, risk/ }));
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" }));

  expect(await screen.findByRole("alert")).toHaveTextContent("Belge adı, tarih ve risk kaydedildi; sorumlu değişikliği tamamlanamadı");
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/compliance/documents/1" && init?.method === "PUT")).toBe(true);
  expect(screen.getByRole("button", { name: "Onayla ve kaydet" })).toBeDisabled();
});
