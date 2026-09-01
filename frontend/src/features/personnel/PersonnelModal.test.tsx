import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { PersonnelModal } from "./PersonnelModal";

const departments = [{ id: 1, name: "Yazılım", code: "YZL", employeeCount: 1 }];
const detail = {
  id: 1, firstName: "Ahmet", lastName: "Yılmaz", name: "Ahmet Yılmaz", initials: "AY",
  title: "Senior Developer", nationalId: "12345678901", status: "active", hireDate: "2021-03-12", contractType: "indefinite",
  departmentId: 1, department: "Yazılım", managerId: null, managerName: null,
  profile: { birthDate: "1990-01-01", gender: "Erkek", maritalStatus: "Evli", bloodType: "0 Rh+", mobilePhone: "(532) 111 11 11", personalEmail: "a@a", homeAddress: "İstanbul", emergencyContactName: "Ayşe Yılmaz", emergencyContactRelation: null, emergencyContactPhone: "0533", employmentType: "Tam Zamanlı", rehireEligibility: null, exitCode: null, iban: "TR11", bankName: "Banka", salaryType: "Net Maaş", pensionStatus: "Otomatik Katılım", mealCard: "Multinet", tshirtSize: "L", pantsSize: "32", coatSize: "L", shoeSize: "42", canWorkAtHeight: true, canWorkNightShift: false, canLiftHeavyLoads: false, healthNotes: "" },
  documents: [],
};

beforeEach(() => {
  localStorage.clear();
  stubApi({
    "/api/departments": departments,
    "/api/employees/1": detail,
    "/api/employees": detail,
    "/api/employees/1/documents": [],
    "/api/employees/1/employment-foundation": {
      employeeId: 1,
      contractType: "indefinite",
      employmentPeriods: [{ id: 1, startDate: "2021-03-12", endDate: null, contractType: "indefinite" }],
      compensationPackages: [],
    },
    "/api/employees/1/compensation-packages": {
      employeeId: 1,
      contractType: "indefinite",
      employmentPeriods: [],
      compensationPackages: [],
    },
  });
});
afterEach(() => vi.unstubAllGlobals());

const renderModal = (employeeId: number | null, readOnly = false) =>
  renderPage(
    <ToastProvider>
      <PersonnelModal employeeId={employeeId} readOnly={readOnly} onClose={() => {}} />
    </ToastProvider>,
  );

test("düzenleme modunda alanlar mevcut kayıtla dolar", async () => {
  renderModal(1);
  expect(await screen.findByText("Personel Kartı — Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.getByLabelText("TC Kimlik No *")).toHaveValue("12345678901");
  expect(screen.getByLabelText("Adı")).toHaveValue("Ahmet");
});

test("sekme değişimi içerik bölümünü değiştirir", async () => {
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  expect(screen.getByLabelText("IBAN Numarası").closest(".content-section")).toHaveClass("active");
});

test("yürürlük tarihli ücret paketini kaynak referansıyla kaydeder", async () => {
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.type(screen.getByLabelText("Yürürlük başlangıcı"), "2026-09-01");
  await userEvent.type(screen.getByLabelText("Aylık brüt maaş"), "50000");
  await userEvent.type(screen.getByLabelText("Kaynak / karar referansı"), "Ücret yazısı-42");
  await userEvent.click(screen.getByRole("button", { name: "Yeni ücret paketini kaydet" }));

  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/employees/1/compensation-packages" && i?.method === "POST",
    );
    expect(JSON.parse(String(posted?.[1]?.body))).toMatchObject({
      effectiveFrom: "2026-09-01",
      monthlyGrossSalary: 50000,
      sourceReference: "Ücret yazısı-42",
    });
  });
});

test("yeni kayıt kaydedilince POST body doğru kurulur", async () => {
  renderModal(null);
  await screen.findByText("Yeni Personel Kartı");
  await userEvent.type(screen.getByLabelText("TC Kimlik No *"), "98765432109");
  await userEvent.type(screen.getByLabelText("Adı"), "Yeni");
  await userEvent.type(screen.getByLabelText("Soyadı"), "Kişi");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.type(screen.getByLabelText("Ünvan / Görev"), "Uzman");
  const hire = screen.getByLabelText("İşe Giriş Tarihi");
  await userEvent.clear(hire);
  await userEvent.type(hire, "2026-07-14");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/employees" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    const body = JSON.parse(String(posted![1]?.body));
    expect(body.nationalId).toBe("98765432109");
    expect(body.firstName).toBe("Yeni");
    expect(body.profile.employmentType).toBe("Tam Zamanlı");
  });
});

test("salt-okur modda inputlar disabled, Kaydet yok", async () => {
  renderModal(1, true);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  expect(screen.getByLabelText("Adı")).toBeDisabled();
  expect(screen.queryByRole("button", { name: /Kaydet/ })).not.toBeInTheDocument();
});
