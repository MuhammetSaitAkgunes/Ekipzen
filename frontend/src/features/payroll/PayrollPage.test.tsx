import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { PayrollPage } from "./PayrollPage";

const periods = [
  { id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft", employeeCount: 4 },
  { id: 2, name: "Haziran 2026", year: 2026, month: 6, status: "approved", employeeCount: 4 },
];

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: 5 },
  }));

beforeEach(() => {
  localStorage.clear();
  stubApi({ "/api/payroll/periods": periods, "/api/payroll/my": [] });
});
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <PayrollPage tab="period" />
      </ToastProvider>
    </AuthProvider>,
  );

test("hr-admin sekmeleri ve dönem seçicisini görür, ilk dönem seçilir", async () => {
  setRole("hr-admin");
  renderShell();
  expect(await screen.findByText("Dönem Bordrosu")).toBeInTheDocument();
  expect(screen.getByText("Tekil Hesaplama")).toBeInTheDocument();
  expect(screen.getByText("Bordro Ayarları")).toBeInTheDocument();
  expect(screen.getByLabelText("Bordro dönemi")).toHaveValue("3");
});

test("çalışan sekme kabuğu yerine Bordrolarım görünümünü alır", async () => {
  setRole("employee");
  renderShell();
  expect(await screen.findByText("Henüz bordro kaydınız yok.")).toBeInTheDocument();
  expect(screen.queryByText("Dönem Bordrosu")).not.toBeInTheDocument();
});


const detail = {
  id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft",
  settings: { effectiveFrom: "2026-01-01", version: 1 },
  controls: [], totals: {},
  rows: [
    { id: 31, name: "Ayşe Yılmaz", approvalStatus: "Onaylandı" },
    { id: 32, name: "Can Kaya", approvalStatus: "Kontrol" },
  ],
};

test("açık son dönem varken yeni dönem oluşturmayı durdurur", async () => {
  setRole("hr-admin");
  renderShell();
  await userEvent.click(await screen.findByRole("button", { name: "Yeni Dönem" }));
  const dialog = screen.getByRole("dialog", { name: "Yeni bordro dönemi" });
  expect(within(dialog).getByText(/Önce son dönemin satırlarını/)).toBeInTheDocument();
  expect(within(dialog).getByRole("button", { name: "Oluştur" })).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) => String(url) === "/api/payroll/periods" && init?.method === "POST")).toBe(false);
});

test("onaylı son dönemden sonraki ayı seçer ve kontrol sonrası oluşturur", async () => {
  setRole("hr-admin");
  stubApi({
    "/api/payroll/periods": [{ ...periods[0], status: "approved" }, periods[1]],
    "/api/payroll/periods/3": { ...detail, status: "approved" },
    "/api/payroll/my": [],
  });
  renderShell();
  await userEvent.click(await screen.findByRole("button", { name: "Yeni Dönem" }));
  const dialog = screen.getByRole("dialog", { name: "Yeni bordro dönemi" });
  expect(within(dialog).getByLabelText("Ay")).toHaveValue("8");
  expect(within(dialog).getByRole("button", { name: "Oluştur" })).toBeDisabled();
  await userEvent.click(within(dialog).getByLabelText("Ayı ve son dönem durumunu kontrol ettim."));
  await userEvent.click(within(dialog).getByRole("button", { name: "Oluştur" }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.find(([url, init]) => String(url) === "/api/payroll/periods" && init?.method === "POST");
    expect(JSON.parse(String(hit?.[1]?.body))).toEqual({ year: 2026, month: 8 });
  });
});

test("dönem onayı eksik satırları gösterir ve gönderimi durdurur", async () => {
  setRole("hr-admin");
  stubApi({ "/api/payroll/periods": periods, "/api/payroll/periods/3": detail });
  renderShell();
  await userEvent.click(await screen.findByRole("button", { name: "Dönemi onayla" }));
  const dialog = screen.getByRole("dialog", { name: "Dönem onayı ön kontrolü" });
  expect(within(dialog).getByText(/2 bordro satırının 1 tanesi onaylı/)).toBeInTheDocument();
  expect(within(dialog).getByText(/Kapandı durumuna geçirmez/)).toBeInTheDocument();
  expect(within(dialog).getByRole("button", { name: "Dönemi onayla" })).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).endsWith("/submit"))).toBe(false);
});

test("tüm satırlar onaylıysa açık teyitle dönem onayını gönderir", async () => {
  setRole("hr-admin");
  stubApi({
    "/api/payroll/periods": periods,
    "/api/payroll/periods/3": { ...detail, rows: detail.rows.map((row) => ({ ...row, approvalStatus: "Onaylandı" })) },
    "/api/payroll/periods/3/submit": { ...detail, status: "approved" },
  });
  renderShell();
  await userEvent.click(await screen.findByRole("button", { name: "Dönemi onayla" }));
  const dialog = screen.getByRole("dialog", { name: "Dönem onayı ön kontrolü" });
  expect(within(dialog).getByRole("button", { name: "Dönemi onayla" })).toBeDisabled();
  await userEvent.click(within(dialog).getByLabelText("Satırları ve dönem sonucunu kontrol ettim."));
  await userEvent.click(within(dialog).getByRole("button", { name: "Dönemi onayla" }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/payroll/periods/3/submit" && init?.method === "POST",
  )).toBe(true));
  expect(await screen.findByText(/Bu işlem ödeme veya dış bildirim yapmaz/)).toBeInTheDocument();
});
