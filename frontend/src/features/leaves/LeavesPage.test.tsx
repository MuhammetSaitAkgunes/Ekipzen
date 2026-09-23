import { AuthProvider } from "../../auth/AuthContext";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { LeavesPage } from "./LeavesPage";

const balance = { year: 2026, entitledDays: 20, carriedOverDays: 4, usedDays: 10, pendingDays: 1, remainingDays: 14, throughDate: "2026-09-01", nextAccrualDate: "2027-03-12", employmentPeriodId: 1 };
const ledger = [
  { id: 1, employmentPeriodId: 1, leaveRequestId: null, occurredOn: "2026-03-12", type: "service-anniversary", days: 20, serviceYear: 5, sourceReference: "5. hizmet yılı" },
  { id: 2, employmentPeriodId: 1, leaveRequestId: 1, occurredOn: "2026-08-12", type: "usage", days: -5, serviceYear: null, sourceReference: "İzin talebi #1" },
];
const myLeaves = [
  { id: 1, employeeId: 5, employeeName: "Ben", leaveTypeId: 1, leaveTypeName: "Yıllık İzin", startDate: "2026-08-12", endDate: "2026-08-18", days: 5, status: "approved", description: null, substituteEmployeeId: null, decisionNote: null, decisionAtUtc: null, createdAtUtc: "2026-07-01T08:00:00Z" },
  { id: 2, employeeId: 5, employeeName: "Ben", leaveTypeId: 3, leaveTypeName: "Mazeret İzni", startDate: "2026-09-20", endDate: "2026-09-20", days: 1, status: "pending", description: null, substituteEmployeeId: null, decisionNote: null, decisionAtUtc: null, createdAtUtc: "2026-07-10T08:00:00Z" },
];
const team = [
  { employeeId: 7, employeeName: "Selin Koç", initials: "SK", leaveTypeName: "Raporlu", startDate: "2026-07-14", endDate: "2026-07-15" },
];

beforeEach(() =>
  stubApi({
    "/api/leaves/balance": balance,
    "/api/leaves/ledger": ledger,
    "/api/leaves/my": myLeaves,
    "/api/leaves/team": team,
    "/api/leaves/1/cancel": null,
    "/api/leaves/2/cancel": null,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderLeaves = () =>
  renderPage(
    <AuthProvider><ToastProvider>
      <LeavesPage />
    </ToastProvider></AuthProvider>,
  );

test("bakiye kartları backend verisiyle dolar", async () => {
  renderLeaves();
  expect(await screen.findByText("Kalan Yıllık İzin")).toBeInTheDocument();
  expect(screen.getByText("Hak ediş: 24 gün")).toBeInTheDocument();
  expect(screen.getByText("10 gün kullanıldı")).toBeInTheDocument();
  expect(screen.getByText("Bekleyen rezervasyon: 1 gün")).toBeInTheDocument();
  expect(screen.getByText("Sıradaki hak ediş: 12 Mar 2027")).toBeInTheDocument();
  expect(screen.getByText(/mazeret izni talebiniz yönetici onayı bekliyor/i)).toBeInTheDocument();
});

test("hareket tablosu durum pill'leri ve iptal butonu", async () => {
  renderLeaves();
  expect(await screen.findByText("Yıllık İzin")).toBeInTheDocument();
  expect(screen.getByText("Onaylandı")).toHaveClass("status-pill", "approved");
  expect(screen.getAllByTitle("Talebi iptal et")).toHaveLength(2);
});

test("hak ediş ve kullanım izin defterinde görünür", async () => {
  renderLeaves();
  expect(await screen.findByText("İzin Bakiye Defteri")).toBeInTheDocument();
  expect(screen.getByText("Hizmet yılı hak edişi")).toBeInTheDocument();
  expect(screen.getByText("5. hizmet yılı")).toBeInTheDocument();
  expect(screen.getByText("-5 gün")).toBeInTheDocument();
});

test("iptal isteği cancel ucuna gider", async () => {
  renderLeaves();
  await screen.findByText("Mazeret İzni");
  await userEvent.click(screen.getByRole("button", { name: "Mazeret İzni talebini iptal et" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/leaves/2/cancel");
    expect(posted).toBe(true);
  });
});

test("ekip yokluk widget'ı dolar", async () => {
  renderLeaves();
  await userEvent.click(screen.getByRole("tab", { name: "Ekip izinleri" }));
  expect(await screen.findByText("Selin Koç")).toBeInTheDocument();
  expect(screen.getByText(/Raporlu/)).toBeInTheDocument();
});
