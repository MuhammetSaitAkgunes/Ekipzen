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
    { id: 2, name: "Selin Koç", title: "UI Designer", nationalIdMasked: "234*****012", departmentId: 2, department: "Tasarım", status: "passive", initials: "SK", hireDate: "2022-05-01", terminationDate: "2026-08-15" },
  ],
  total: 2, page: 1, pageSize: 50, totalPages: 1,
};
const departments = [
  { id: 1, name: "Yazılım", code: "YZL", employeeCount: 1 },
  { id: 2, name: "Tasarım", code: "TSR", employeeCount: 1 },
];
const draftCase = {
  id: 10, employeeId: 1, status: "draft", version: 1,
  terminationDate: "2026-08-15", exitCode: "Kod-03", exitReason: "İstifa",
  successorManagerId: 2, unusedLeaveDays: 4.5, unusedLeavePayment: 12000,
  severancePayment: 0, noticePayment: 0, totalPayment: 12000,
  settlementSource: "external-expert", settlementReference: "MM-2026-18",
  identityProvisioningPending: false,
};
const completedCase = {
  ...draftCase, id: 20, employeeId: 2, status: "completed", version: 2,
  successorManagerId: null, completedAtUtc: "2026-08-15T10:00:00Z",
};

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
    "/api/employees/1/offboarding-context": {
      employeeId: 1, employeeName: "Ahmet Yılmaz", directReportCount: 2, pendingLeaveRequestCount: 1,
      currentCase: null,
      successors: [{ id: 2, name: "Selin Koç", title: "UI Designer", isDirectReport: true }],
    },
    "/api/employees/1/offboarding-draft": draftCase,
    "/api/employees/1/offboarding/10/complete": { ...draftCase, status: "completed", version: 2 },
    "/api/employees/2/offboarding-context": {
      employeeId: 2, employeeName: "Selin Koç", directReportCount: 0, pendingLeaveRequestCount: 0,
      currentCase: completedCase, successors: [],
    },
    "/api/employees/2/offboarding/20/reverse": { ...completedCase, status: "reversed", version: 3 },
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

test("seçim çubuğu yalnız raporlama sunar, toplu işten çıkış sunmaz", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");
  await userEvent.click(screen.getByLabelText("Ahmet Yılmaz kaydını seç"));
  expect(screen.getByText("1 kişi seçildi")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Seçilenleri dışa aktar/ })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /Pasife al/ })).not.toBeInTheDocument();
});

test("işten çıkış taslağını kaydedip sürümüyle tamamlar", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");

  await userEvent.click(screen.getByRole("button", { name: "Ahmet Yılmaz için işten çıkış dosyasını aç" }));
  expect(await screen.findByRole("heading", { name: "İşten Çıkış ve Finansal Kapanış" })).toBeInTheDocument();
  expect(screen.getByText(/hukuki tutar hesaplamaz/)).toBeInTheDocument();

  await userEvent.selectOptions(screen.getByLabelText("Halef yönetici"), "2");
  const date = screen.getByLabelText("İşten ayrılma tarihi");
  await userEvent.clear(date);
  await userEvent.type(date, "2026-08-15");
  await userEvent.type(screen.getByLabelText("Çıkış kodu"), "Kod-03");
  await userEvent.type(screen.getByLabelText("Çıkış gerekçesi"), "İstifa");
  await userEvent.clear(screen.getByLabelText("Ödenecek kullanılmamış izin (gün)"));
  await userEvent.type(screen.getByLabelText("Ödenecek kullanılmamış izin (gün)"), "4.5");
  await userEvent.clear(screen.getByLabelText("İzin ödemesi (₺)"));
  await userEvent.type(screen.getByLabelText("İzin ödemesi (₺)"), "12000");
  await userEvent.type(screen.getByLabelText("Uzman/harici hesap referansı"), "MM-2026-18");
  await userEvent.click(screen.getByRole("button", { name: /İşten çıkışı tamamla/ }));

  await waitFor(() => {
    const saved = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/1/offboarding-draft" && init?.method === "PUT");
    expect(saved).toBeTruthy();
    expect(JSON.parse(String(saved![1]?.body))).toMatchObject({
      expectedVersion: null,
      terminationDate: "2026-08-15",
      exitCode: "Kod-03",
      exitReason: "İstifa",
      successorManagerId: 2,
      unusedLeaveDays: 4.5,
      unusedLeavePayment: 12000,
      settlementReference: "MM-2026-18",
    });

    const finished = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/1/offboarding/10/complete" && init?.method === "POST");
    expect(JSON.parse(String(finished?.[1]?.body))).toEqual({ expectedVersion: 1 });
  });
});

test("tamamlanmış dosyayı gerekçeli ve sürümlü geri alır", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Selin Koç");

  await userEvent.click(screen.getByRole("button", { name: "Selin Koç için işten çıkış dosyasını aç" }));
  expect(await screen.findByRole("heading", { name: "İşten Çıkış Dosyası" })).toBeInTheDocument();
  expect(screen.getAllByText("₺12.000,00")).toHaveLength(2);

  await userEvent.type(screen.getByLabelText("Geri alma gerekçesi"), "Yanlış personel seçildi");
  await userEvent.click(screen.getByRole("button", { name: /İşten çıkışı geri al/ }));

  await waitFor(() => {
    const reversed = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/2/offboarding/20/reverse" && init?.method === "POST");
    expect(JSON.parse(String(reversed?.[1]?.body))).toEqual({
      expectedVersion: 2,
      reason: "Yanlış personel seçildi",
    });
  });
});

test("manager mutasyon eylemlerini görmez", async () => {
  setRole("manager");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");
  expect(screen.queryByRole("button", { name: /Yeni Personel/ })).not.toBeInTheDocument();
  expect(screen.queryByTitle("Düzenle")).not.toBeInTheDocument();
  expect(screen.queryByRole("button", { name: /işten çıkış dosyasını aç/ })).not.toBeInTheDocument();
});

test("arama filtresi query paramına yansır", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");
  await userEvent.type(screen.getByLabelText("Personel ara"), "ahmet");
  await waitFor(() => {
    expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes("search=ahmet"))).toBe(true);
  }, { timeout: 2000 });
});