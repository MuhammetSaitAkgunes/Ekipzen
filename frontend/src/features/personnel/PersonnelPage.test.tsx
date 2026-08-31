import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { PersonnelPage } from "./PersonnelPage";

const employees = {
  items: [
    { id: 1, name: "Ahmet Yılmaz", title: "Senior Developer", nationalIdMasked: "123*****901", departmentId: 1, department: "Yazılım", status: "active", initials: "AY", hireDate: "2021-03-12" },
    { id: 2, name: "Selin Koç", title: "UI Designer", nationalIdMasked: "234*****012", departmentId: 2, department: "Tasarım", status: "passive", initials: "SK", hireDate: "2022-05-01" },
  ],
  total: 2, page: 1, pageSize: 50, totalPages: 1,
};
const departments = [
  { id: 1, name: "Yazılım", code: "YZL", employeeCount: 1 },
  { id: 2, name: "Tasarım", code: "TSR", employeeCount: 1 },
];

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: null },
  }));

const renderPersonnel = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <PersonnelPage />
      </ToastProvider>
    </AuthProvider>,
  );

beforeEach(() => {
  localStorage.clear();
  stubApi({
    "/api/employees": employees,
    "/api/departments": departments,
    "/api/employees/bulk-deactivate": { deactivated: 1 },
    "/api/employees/1/offboarding-context": {
      employeeId: 1, employeeName: "Ahmet Yılmaz", directReportCount: 2, pendingLeaveRequestCount: 1,
      successors: [{ id: 2, name: "Selin Koç", title: "UI Designer", isDirectReport: true }],
    },
    "/api/employees/1/status": {},
  });
});
afterEach(() => vi.unstubAllGlobals());

test("liste maskeli TC ve durum rozetiyle dolar", async () => {
  setRole("hr-admin");
  renderPersonnel();
  expect(await screen.findByText("Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("123*****901")).toBeInTheDocument();
  expect(screen.getByText("Pasif", { selector: ".badge" })).toHaveClass("badge-passive");
});

test("satır seçilince bulk bar görünür, pasife al isteği gider", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");
  await userEvent.click(screen.getByLabelText("Ahmet Yılmaz kaydını seç"));
  expect(screen.getByText("1 kişi seçildi")).toBeInTheDocument();
  const terminationDate = screen.getByLabelText("Ayrılış tarihi");
  await userEvent.clear(terminationDate);
  await userEvent.type(terminationDate, "2026-08-15");
  await userEvent.click(screen.getByRole("button", { name: /Pasife al/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(([u]) => String(u).includes("bulk-deactivate"));
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toEqual({ ids: [1], terminationDate: "2026-08-15" });
  });
});

test("tekil işten çıkış halef seçimini status isteğine taşır", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");

  await userEvent.click(screen.getByRole("button", { name: "Ahmet Yılmaz için işten çıkış başlat" }));
  expect(await screen.findByRole("heading", { name: "İşten Çıkış ve Halef Devri" })).toBeInTheDocument();
  expect(screen.getByText(/2 doğrudan ekip üyesi ve 1 bekleyen izin talebi/)).toBeInTheDocument();

  await userEvent.selectOptions(screen.getByLabelText("Halef yönetici"), "2");
  const terminationDate = screen.getByLabelText("İşten ayrılma tarihi");
  await userEvent.clear(terminationDate);
  await userEvent.type(terminationDate, "2026-08-15");
  await userEvent.click(screen.getByRole("button", { name: /İşten çıkışı tamamla/ }));

  await waitFor(() => {
    const patched = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/1/status" && init?.method === "PATCH");
    expect(patched).toBeTruthy();
    expect(JSON.parse(String(patched![1]?.body))).toEqual({
      status: "passive",
      terminationDate: "2026-08-15",
      successorManagerId: 2,
    });
  });
});

test("manager mutasyon eylemlerini görmez", async () => {
  setRole("manager");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");
  expect(screen.queryByRole("button", { name: /Yeni Personel/ })).not.toBeInTheDocument();
  expect(screen.queryByTitle("Düzenle")).not.toBeInTheDocument();
  expect(screen.getAllByTitle("Görüntüle").length).toBeGreaterThan(0);
});

test("arama filtresi query paramına yansır", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");
  await userEvent.type(screen.getByLabelText("Personel ara"), "ahmet");
  await waitFor(() => {
    const searched = vi.mocked(fetch).mock.calls.some(([u]) => String(u).includes("search=ahmet"));
    expect(searched).toBe(true);
  }, { timeout: 2000 });
});
