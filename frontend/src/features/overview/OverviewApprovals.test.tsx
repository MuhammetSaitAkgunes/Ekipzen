import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { OverviewPage } from "./OverviewPage";

const overview = {
  activeEmployees: 3, pendingApprovals: 1, openPositions: 0, newApplications: 0,
  inOfficeToday: 2, onLeaveToday: 0, pulseScore: 60,
  departmentDistribution: [{ dept: "Yazılım", count: 3 }],
  recruitmentFunnel: { total: 0, new: 0, interview: 0, offer: 0, rejected: 0, hired: 0 },
};
const pending = [
  { id: 4, employeeId: 5, employeeName: "Ahmet Yılmaz", leaveTypeId: 1, leaveTypeName: "Yıllık İzin", startDate: "2026-08-12", endDate: "2026-08-18", days: 5, status: "pending" },
];

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: null },
  }));

beforeEach(() => {
  localStorage.clear();
  stubApi({
    "/api/dashboard/overview": overview,
    "/api/leaves/pending": pending,
    "/api/leaves/4/approve": { ...pending[0], status: "approved" },
  });
});
afterEach(() => vi.unstubAllGlobals());

const renderOverview = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <OverviewPage />
      </ToastProvider>
    </AuthProvider>,
  );

test("yönetici bekleyen izin taleplerini görür ve onaylar", async () => {
  setRole("manager");
  renderOverview();
  expect(await screen.findByText("Bekleyen onaylar")).toBeInTheDocument();
  expect(await screen.findByText("Ahmet Yılmaz")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "İncele" }));
  await userEvent.click(screen.getByRole("button", { name: "Onayla" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/leaves/4/approve");
    expect(posted).toBe(true);
  });
});

test("çalışan onay kartını görmez", async () => {
  setRole("employee");
  renderOverview();
  await screen.findByText("Aktif Personel");
  expect(screen.queryByText("Bekleyen onaylar")).not.toBeInTheDocument();
});
