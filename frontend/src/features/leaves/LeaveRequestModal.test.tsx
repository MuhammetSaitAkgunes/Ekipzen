import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { LeaveRequestModal } from "./LeaveRequestModal";

const types = [
  { id: 1, name: "Yıllık İzin", code: "YIL", deductsFromAnnualBalance: true, requiresApproval: true },
  { id: 2, name: "Raporlu", code: "RAP", deductsFromAnnualBalance: false, requiresApproval: false },
];
const balance = { year: 2026, entitledDays: 20, carriedOverDays: 4, usedDays: 10, remainingDays: 14 };
const created = { id: 9, leaveTypeId: 1, leaveTypeName: "Yıllık İzin", startDate: "2026-08-01", endDate: "2026-08-03", days: 2, status: "pending" };

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: 5 },
  }));

beforeEach(() => {
  localStorage.clear();
  stubApi({
    "/api/leaves/types": types,
    "/api/leaves/balance": balance,
    "/api/leaves": created,
    "/api/employees": { items: [{ id: 7, name: "Selin Koç", title: "UI", departmentId: 2, department: "Tasarım", status: "active", initials: "SK", hireDate: "2022-01-01", nationalIdMasked: "1" }], total: 1, page: 1, pageSize: 50, totalPages: 1 },
  });
});
afterEach(() => vi.unstubAllGlobals());

const renderModal = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <LeaveRequestModal onClose={() => {}} />
      </ToastProvider>
    </AuthProvider>,
  );

test("izin türleri backend'den dolar, yıllık kartında kalan gün yazar", async () => {
  setRole("employee");
  renderModal();
  expect(await screen.findByText("Yıllık İzin")).toBeInTheDocument();
  expect(screen.getByText("Kalan: 14 gün")).toBeInTheDocument();
  expect(screen.getByText("Belge gerekli")).toBeInTheDocument();
});

test("tarih seçilince süre ve işe dönüş ön izlemesi hesaplanır", async () => {
  setRole("employee");
  renderModal();
  await screen.findByText("Yıllık İzin");
  await userEvent.type(screen.getByLabelText("Başlangıç Tarihi"), "2026-08-01");
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-08-03");
  expect(screen.getByText("3 gün")).toBeInTheDocument();
  expect(screen.getByText("04 Ağu 2026")).toBeInTheDocument();
});

test("talep gönderilince POST /leaves doğru gövdeyle gider", async () => {
  setRole("manager");
  renderModal();
  await screen.findByText("Yıllık İzin");
  await userEvent.type(screen.getByLabelText("Başlangıç Tarihi"), "2026-08-01");
  await userEvent.type(screen.getByLabelText("Bitiş Tarihi"), "2026-08-03");
  await userEvent.selectOptions(await screen.findByLabelText("Yerine Bakacak Kişi"), "7");
  await userEvent.click(screen.getByRole("button", { name: /Talebi Gönder/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/leaves" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    const body = JSON.parse(String(posted![1]?.body));
    expect(body).toMatchObject({ leaveTypeId: 1, startDate: "2026-08-01", endDate: "2026-08-03", substituteEmployeeId: 7 });
  });
});

test("tarihler boşsa uyarı toast'ı, istek gitmez", async () => {
  setRole("employee");
  renderModal();
  await screen.findByText("Yıllık İzin");
  await userEvent.click(screen.getByRole("button", { name: /Talebi Gönder/ }));
  expect(await screen.findByText("Geçerli bir izin türü ve tarih aralığı seçin.")).toBeInTheDocument();
  const posted = vi.mocked(fetch).mock.calls.some(([u, i]) => String(u) === "/api/leaves" && i?.method === "POST");
  expect(posted).toBe(false);
});
