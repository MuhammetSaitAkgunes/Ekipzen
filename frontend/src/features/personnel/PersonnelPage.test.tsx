import { screen, waitFor, within } from "@testing-library/react";
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
    "/api/employees/1/offboarding/10/completion-preview": { action: "complete", caseId: 10, caseVersion: 1, token: "complete-preview", canApply: true,
      employeeName: "Personel", terminationDate: "2026-08-15", employmentPeriodId: 1, compensationPackageId: null,
      totalSettlement: 12000, settlementReference: "MM-2026-18", teamTransfers: [], pendingLeaveRequestCount: 1,
      accessOperations: [], blockingReasons: [] },
    "/api/employees/2/offboarding/20/reversal-preview": { action: "reverse", caseId: 20, caseVersion: 2, token: "reverse-preview", canApply: true,
      employeeName: "Personel", terminationDate: "2026-08-15", employmentPeriodId: 1, compensationPackageId: null,
      totalSettlement: 12000, settlementReference: "MM-2026-18", teamTransfers: [], pendingLeaveRequestCount: 1,
      accessOperations: [], blockingReasons: [] },
    "/api/employees/1/offboarding/10/complete": { ...draftCase, status: "completed", version: 2 },
    "/api/employees/2/offboarding-context": {
      employeeId: 2, employeeName: "Selin Koç", directReportCount: 0, pendingLeaveRequestCount: 0,
      currentCase: completedCase, successors: [],
    },
    "/api/employees/2/offboarding/20/reverse": { ...completedCase, status: "reversed", version: 3 },
  });
});
afterEach(() => vi.unstubAllGlobals());

test("liste temel bilgileri gösterir, kimlik ayrıntısını profile bırakır", async () => {
  setRole("hr-admin");
  renderPersonnel();
  expect(await screen.findByText("Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.queryByText("123*****901")).not.toBeInTheDocument();
  expect(screen.getByText("Pasif", { selector: ".badge" })).toHaveClass("badge-passive");
});

test("kayıt rehberi gerçek sonuç sayısını, rol sınırını ve filtre düzeltmesini gösterir", async () => {
  setRole("manager");
  stubApi({
    "/api/employees": { ...employees, items: [employees.items[0]], total: 1 },
    "/api/departments": departments,
  });
  renderPersonnel();
  const guide = await screen.findByRole("region", { name: "Mevcut çalışan kaydını kontrol et" });
  expect(within(guide).getByText("1 kayıt")).toBeInTheDocument();
  expect(within(guide).getByText(/düzeltmeyi yetkili İK kullanıcısına iletin/)).toBeInTheDocument();
  expect(within(guide).getByRole("button", { name: "Ahmet Yılmaz kartını aç" })).toBeInTheDocument();
  await userEvent.click(within(guide).getByRole("button", { name: "Arama alanına git" }));
  expect(screen.getByLabelText("Personel ara")).toHaveFocus();
  await userEvent.selectOptions(screen.getByLabelText("Durum filtresi"), "active");
  await userEvent.click(await screen.findByRole("button", { name: "Filtreleri temizle" }));
  expect(screen.getByLabelText("Durum filtresi")).toHaveValue("");
  expect(screen.getByLabelText("Personel ara")).toHaveValue("");
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
  expect(screen.getByText("İki güvenli kapanış yolu")).toBeInTheDocument();
  expect(screen.getByLabelText("Fesih nedeni")).toHaveValue("");
  expect(screen.getByLabelText("İhbar uygulaması")).toHaveValue("");

  await userEvent.selectOptions(screen.getByLabelText("Halef yönetici"), "2");
  const date = screen.getByLabelText("İşten ayrılma tarihi");
  await userEvent.clear(date);
  await userEvent.type(date, "2026-08-15");
  await userEvent.type(screen.getByLabelText("Çıkış kodu"), "Kod-03");
  await userEvent.type(screen.getByLabelText("Çıkış gerekçesi"), "İstifa");
  await userEvent.selectOptions(screen.getByLabelText("Fesih nedeni"), "employee-resignation");
  await userEvent.selectOptions(screen.getByLabelText("İhbar uygulaması"), "not-applicable");
  await userEvent.clear(screen.getByLabelText("Ödenecek kullanılmamış izin (gün)"));
  await userEvent.type(screen.getByLabelText("Ödenecek kullanılmamış izin (gün)"), "4.5");
  await userEvent.clear(screen.getByLabelText("İzin ödemesi (₺)"));
  await userEvent.type(screen.getByLabelText("İzin ödemesi (₺)"), "12000");
  await userEvent.type(screen.getByLabelText("Uzman/harici hesap referansı"), "MM-2026-18");
  await userEvent.click(screen.getByRole("button", { name: "Taslağı kaydet ve ayrılış etkisini incele" }));
  expect(await screen.findByRole("region", { name: "Tamamlamadan önce kontrol edin" })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/employees/1/offboarding-draft" && init?.method === "PUT")).toBe(true);
  expect(screen.getByRole("button", { name: "İşten çıkışı onayla ve tamamla" })).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox", { name: /Vaka bilgilerini, uzman hesap referansını/ }));
  await userEvent.click(screen.getByRole("button", { name: "İşten çıkışı onayla ve tamamla" }));

  await waitFor(() => {
    const saved = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/1/offboarding-draft" && init?.method === "PUT");
    expect(saved).toBeTruthy();
    expect(JSON.parse(String(saved![1]?.body))).toMatchObject({
      expectedVersion: null,
      terminationDate: "2026-08-15",
      exitCode: "Kod-03",
      exitReason: "İstifa",
      terminationReason: "employee-resignation",
      noticeHandling: "not-applicable",
      successorManagerId: 2,
      unusedLeaveDays: 4.5,
      unusedLeavePayment: 12000,
      settlementReference: "MM-2026-18",
    });

    const finished = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/1/offboarding/10/complete" && init?.method === "POST");
    expect(JSON.parse(String(finished?.[1]?.body))).toEqual({ expectedVersion: 1, expectedPreviewToken: "complete-preview" });
  });
});

test("geçersiz finansal tutarı kaydetmeden kullanıcıya düzeltme ister", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: "Ahmet Yılmaz için işten çıkış dosyasını aç" }));
  await screen.findByRole("heading", { name: "İşten Çıkış ve Finansal Kapanış" });

  await userEvent.selectOptions(screen.getByLabelText("Halef yönetici"), "2");
  await userEvent.type(screen.getByLabelText("İşten ayrılma tarihi"), "2026-08-15");
  await userEvent.type(screen.getByLabelText("Çıkış kodu"), "Kod-03");
  await userEvent.type(screen.getByLabelText("Çıkış gerekçesi"), "İstifa");
  await userEvent.selectOptions(screen.getByLabelText("Fesih nedeni"), "employee-resignation");
  await userEvent.selectOptions(screen.getByLabelText("İhbar uygulaması"), "not-applicable");
  await userEvent.type(screen.getByLabelText("Uzman/harici hesap referansı"), "MM-2026-18");
  await userEvent.clear(screen.getByLabelText("İzin ödemesi (₺)"));
  await userEvent.type(screen.getByLabelText("İzin ödemesi (₺)"), "-1");

  await userEvent.click(screen.getByRole("button", { name: "Taslağı kaydet ve ayrılış etkisini incele" }));
  expect(screen.getByRole("alert")).toHaveTextContent("İzin ödemesi boş, geçersiz veya negatif olamaz");
  expect(screen.queryByRole("region", { name: "Tamamlamadan önce kontrol edin" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/employees/1/offboarding-draft" && init?.method === "PUT")).toBe(false);
});

test("tamamlanmış dosyayı gerekçeli ve sürümlü geri alır", async () => {
  setRole("hr-admin");
  renderPersonnel();
  await screen.findByText("Selin Koç");

  await userEvent.click(screen.getByRole("button", { name: "Selin Koç için işten çıkış dosyasını aç" }));
  expect(await screen.findByRole("heading", { name: "İşten Çıkış Dosyası" })).toBeInTheDocument();
  expect(screen.getAllByText("₺12.000,00")).toHaveLength(2);

  await userEvent.type(screen.getByLabelText("Geri alma gerekçesi"), "Yanlış personel seçildi");
  await userEvent.click(screen.getByRole("button", { name: "İşten çıkışı geri almayı gözden geçir" }));
  expect(await screen.findByRole("region", { name: "Geri almadan önce kontrol edin" })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) =>
    String(url) === "/api/employees/2/offboarding/20/reverse" && init?.method === "POST")).toBe(false);
  expect(screen.getByRole("button", { name: "Geri almayı onayla" })).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox", { name: "Geri alma etkilerini kontrol ettim." }));
  await userEvent.click(screen.getByRole("button", { name: "Geri almayı onayla" }));

  await waitFor(() => {
    const reversed = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === "/api/employees/2/offboarding/20/reverse" && init?.method === "POST");
    expect(JSON.parse(String(reversed?.[1]?.body))).toEqual({
      expectedVersion: 2,
      reason: "Yanlış personel seçildi",
      expectedPreviewToken: "reverse-preview",
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

test("geri alma önizlemesi engel döndürürse teyit ve parasal geçmişi değiştiren gönderim açılmaz", async () => {
  setRole("hr-admin"); const baseFetch = fetch;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (String(input).endsWith("/reversal-preview")) return new Response(JSON.stringify({
      action: "reverse", caseId: 20, caseVersion: 2, token: "blocked", canApply: false,
      employeeName: "Selin Koç", terminationDate: "2026-08-15", employmentPeriodId: 1, compensationPackageId: null,
      totalSettlement: 12000, settlementReference: "MM", teamTransfers: [], pendingLeaveRequestCount: 0,
      accessOperations: [], blockingReasons: ["Parasal düzeltme geçmişi var; uzman incelemesi gerekir."] }), { status: 200 });
    return baseFetch(input, init);
  }));
  renderPersonnel();
  await userEvent.click(await screen.findByRole("button", { name: "Selin Koç için işten çıkış dosyasını aç" }));
  await userEvent.type(await screen.findByLabelText("Geri alma gerekçesi"), "Hata doğrulandı");
  await userEvent.click(screen.getByRole("button", { name: "İşten çıkışı geri almayı gözden geçir" }));
  expect(await screen.findByText("Parasal düzeltme geçmişi var; uzman incelemesi gerekir.")).toBeInTheDocument();
  expect(screen.getByRole("checkbox", { name: "Geri alma etkilerini kontrol ettim." })).toBeDisabled();
  expect(screen.getByRole("button", { name: "Geri almayı onayla" })).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) => String(url).endsWith("/reverse") && init?.method === "POST")).toBe(false);
});
