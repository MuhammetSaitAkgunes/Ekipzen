import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { AttendanceEntryModal } from "./AttendanceEntryModal";

const employees = { items: [
  { id: 1, name: "Ahmet Yılmaz", title: "Dev", departmentId: 1, department: "Yazılım", status: "active", initials: "AY", hireDate: "2021-01-01", nationalIdMasked: "1" },
], total: 1, page: 1, pageSize: 50, totalPages: 1 };
const savedRow = { id: 33, workDate: "2026-07-14", type: "Tam", checkIn: "09:00:00", checkOut: "18:00:00", breakMinutes: 60, workedMinutes: 480, overtimeMinutes: 0, status: "ok", note: null };

beforeEach(() => stubApi({ "/api/employees": employees, "/api/attendance": savedRow, "/api/attendance/33": savedRow }));
afterEach(() => vi.unstubAllGlobals());

const renderModal = (rowId: number | null, initial?: typeof savedRow) =>
  renderPage(
    <ToastProvider>
      <AttendanceEntryModal rowId={rowId} initial={initial} defaultEmployeeId={1} onClose={() => {}} />
    </ToastProvider>,
  );

test("yeni kayıt POST /attendance gövdesiyle gider", async () => {
  renderModal(null);
  await screen.findByText("Manuel Puantaj Girişi");
  expect(screen.getByLabelText("Personel")).toHaveValue("");
  expect(screen.getByLabelText("Tip")).toHaveValue("");
  expect(screen.getByLabelText("Mola (dk)")).toHaveValue(null);
  await screen.findByRole("option", { name: /Ahmet Yılmaz/ });
  await userEvent.selectOptions(screen.getByLabelText("Personel"), "1");
  await userEvent.selectOptions(screen.getByLabelText("Tip"), "Tam");
  await userEvent.type(screen.getByLabelText("Mola (dk)"), "60");
  await userEvent.type(screen.getByLabelText("Tarih"), "2026-07-14");
  await userEvent.type(screen.getByLabelText("Giriş"), "09:00");
  await userEvent.type(screen.getByLabelText("Çıkış"), "18:00");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  expect(screen.getByRole("heading", { name: "Kaydetmeden önce kontrol edin" })).toBeInTheDocument();
  expect(screen.getByText(/2026-07-14 · Tam/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/attendance" && i?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/attendance" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    const body = JSON.parse(String(posted![1]?.body));
    expect(body.employeeId).toBe(1);
    expect(body.model).toMatchObject({ workDate: "2026-07-14", checkIn: "09:00", checkOut: "18:00", type: "Tam", breakMinutes: 60 });
  });
});

test("boş veya geçersiz mola süresi kayda dönüşmez", async () => {
  renderModal(null);
  await screen.findByRole("option", { name: /Ahmet Yılmaz/ });
  await userEvent.selectOptions(screen.getByLabelText("Personel"), "1");
  await userEvent.selectOptions(screen.getByLabelText("Tip"), "Tam");
  await userEvent.type(screen.getByLabelText("Tarih"), "2026-07-14");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Mola süresini");
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/attendance" && i?.method === "POST")).toBe(false);

  await userEvent.type(screen.getByLabelText("Mola (dk)"), "-1");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  expect(screen.getByRole("alert")).toHaveTextContent("Mola süresini");
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/attendance" && i?.method === "POST")).toBe(false);
});

test("düzenleme PUT /attendance/{id} ile gider ve alanlar dolu gelir", async () => {
  renderModal(33, savedRow);
  await screen.findByText("Puantaj Kaydını Düzenle");
  expect(screen.getByLabelText("Tarih")).toHaveValue("2026-07-14");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  expect(screen.getByText(/aynı çalışanın aylık puantajındaki satırı düzenleyebilirsiniz/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/attendance/33" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
  });
});

test("ön incelemeden geri dönünce kayıt gönderilmeden alanlar düzeltilebilir", async () => {
  renderModal(null);
  await screen.findByRole("option", { name: /Ahmet Yılmaz/ });
  await userEvent.selectOptions(screen.getByLabelText("Personel"), "1");
  await userEvent.selectOptions(screen.getByLabelText("Tip"), "Rapor");
  await userEvent.type(screen.getByLabelText("Mola (dk)"), "0");
  await userEvent.type(screen.getByLabelText("Tarih"), "2026-07-14");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  await userEvent.click(screen.getByRole("button", { name: "Düzelt" }));
  expect(screen.getByLabelText("Tarih")).toHaveValue("2026-07-14");
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/attendance" && i?.method === "POST")).toBe(false);
});
