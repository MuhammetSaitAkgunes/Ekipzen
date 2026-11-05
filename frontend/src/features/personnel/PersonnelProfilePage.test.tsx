import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { PersonnelProfilePage } from "./PersonnelProfilePage";

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "admin", name: "İK Yetkilisi", email: "ik@example.test", role: "hr-admin", roleLabel: "İK", initials: "İK", employeeId: null },
  }));
  const employee = {
    id: 42, firstName: "Yeni", lastName: "Kişi", name: "Yeni Kişi", title: "Uzman",
    nationalId: "98765432109", status: "active", hireDate: "2026-09-24",
    contractType: "indefinite", departmentId: 1, department: "Yazılım", managerId: null,
    profile: { employmentType: "Tam Zamanlı" }, documents: [],
  };
  stubApi({
    "/api/departments": [{ id: 1, name: "Yazılım", code: "YZL", employeeCount: 0 }],
    "/api/employees": employee,
    "/api/guidance/employee-entry/bookmark": { processKey: "personnel-entry", stepKey: "tab-kimlik", contentVersion: 1 },
    "/api/guidance/employee-entry/employees/42/evidence": { employeeId: 42, createdByCurrentUser: true, createdAtUtc: "2026-09-24T10:00:00Z" },
    "/api/employees/42": employee,
    "/api/employees/42/documents": [],
    "/api/termination-calculation-rules": [],
    "/api/employees/42/offboarding-context": {
      employeeId: 42, employeeName: "Yeni Kişi", directReportCount: 0, pendingLeaveRequestCount: 0,
      currentCase: {
        id: 17, employeeId: 42, status: "draft", version: 1,
        terminationDate: "2026-09-24", exitCode: "TEST-03", exitReason: "İstifa",
        terminationReason: "employee-resignation", noticeHandling: "not-applicable",
        unusedLeaveDays: 0, unusedLeavePayment: 0, severancePayment: 0, noticePayment: 0,
        settlementSource: "external-expert", settlementReference: "Uzman kayıt örneği",
      },
      successors: [],
    },
    "/api/employees/42/employment-foundation": {
      employeeId: 42, contractType: "indefinite", employmentPeriods: [], compensationPackages: [],
    },
  });
});

afterEach(() => vi.unstubAllGlobals());

test("günlük görev bağlantısı taslak ayrılış dosyasını açar ve kapanınca adresi temizler", async () => {
  function LocationProbe() {
    const location = useLocation();
    return <span data-testid="location">{location.pathname + location.search}</span>;
  }
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={["/app/personnel/42?offboarding=1"]}>
        <AuthProvider><ToastProvider><Routes>
          <Route path="/app/personnel/:employeeId" element={<><PersonnelProfilePage /><LocationProbe /></>} />
        </Routes></ToastProvider></AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );

  expect(await screen.findByRole("dialog", { name: "İşten Çıkış ve Finansal Kapanış" })).toBeInTheDocument();
  expect(await screen.findByText(/Yeni Kişi için denetlenebilir kapanış sürecini yönetin/)).toBeInTheDocument();
  expect(screen.getByTestId("location")).toHaveTextContent("/app/personnel/42?offboarding=1");
  await userEvent.click(screen.getByRole("button", { name: "İşten çıkış penceresini kapat" }));
  expect(screen.queryByRole("dialog", { name: "İşten Çıkış ve Finansal Kapanış" })).not.toBeInTheDocument();
  expect(screen.getByTestId("location")).toHaveTextContent("/app/personnel/42");
});

test("başarılı çalışan kaydından sonra aynı çalışanın evrak adımına geçer", async () => {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter initialEntries={["/app/personnel/new"]}>
        <AuthProvider><ToastProvider><Routes>
          <Route path="/app/personnel/new" element={<PersonnelProfilePage />} />
          <Route path="/app/personnel/:employeeId" element={<PersonnelProfilePage />} />
        </Routes></ToastProvider></AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );

  await userEvent.type(screen.getByLabelText("Adı"), "Yeni");
  await userEvent.type(screen.getByLabelText("Soyadı"), "Kişi");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.selectOptions(screen.getByLabelText("Departman"), "1");
  await userEvent.type(screen.getByLabelText("Ünvan / Görev"), "Uzman");
  await userEvent.type(screen.getByLabelText("İşe Giriş Tarihi"), "2026-09-24");
  await userEvent.selectOptions(screen.getByLabelText("Sözleşme Türü"), "indefinite");
  await userEvent.selectOptions(screen.getByLabelText("Çalışma Şekli"), "Tam Zamanlı");
  await userEvent.click(screen.getByRole("button", { name: /^Kaydet$/ }));

  expect(await screen.findByRole("heading", { name: "Çalışan kartı oluşturuldu; kalan adımları kontrol edin" })).toBeInTheDocument();
  expect(await screen.findByText(/Kartı oluşturma işleminiz sunucuda doğrulandı/)).toBeInTheDocument();
  expect(await screen.findByText("Bu çalışan için henüz dosya yüklenmemiş.")).toBeInTheDocument();
  expect(await screen.findByText("Etkin çalışma dönemi belirlenemedi; ücret hazırlığını tamamlandı saymayın.")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Öğrenme yolculuğumu gör" })).toHaveAttribute("href", "/app/guidance");
  await userEvent.click(screen.getAllByRole("button", { name: "Rehber" })[0]);
  expect(screen.getByRole("region", { name: "İşe giriş rehberi" })).toHaveTextContent("Belgeleri kayda bağla");
  await userEvent.click(screen.getByRole("button", { name: "Kapat" }));
  expect(screen.getByLabelText("Evrak dosyası seç")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Ücret bölümünü aç" }));
  await userEvent.click(screen.getAllByRole("button", { name: "Rehber" })[0]);
  expect(screen.getByRole("region", { name: "İşe giriş rehberi" })).toHaveTextContent("Ödeme hazırlığını kontrol et");
  await userEvent.click(screen.getByRole("button", { name: "Kapat" }));
  const salary = screen.getByLabelText("Aylık brüt maaş");
  await userEvent.type(salary, "50000");
  await userEvent.click(screen.getByRole("button", { name: "Belgeleri aç" }));
  expect(screen.getByLabelText("Evrak dosyası seç")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Ücret bölümünü aç" }));
  expect(screen.getByLabelText("Aylık brüt maaş")).toHaveValue(50000);
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) => String(url) === "/api/employees" && init?.method === "POST")).toBe(true);
});
