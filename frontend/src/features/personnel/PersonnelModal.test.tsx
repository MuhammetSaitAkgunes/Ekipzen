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
const compensationPackage = {
  id: 2, version: 1, employmentPeriodId: 1, effectiveFrom: "2026-09-01", effectiveTo: null,
  monthlyGrossSalary: 50000, regularPremium: 0, roadAllowance: 0, mealAllowance: 0,
  otherRegularBenefits: 0, monthlyRecurringTotal: 50000, sourceReference: "Eski karar",
};
const correctionRoutes = (canCorrect: boolean) => ({
  "/api/departments": departments,
  "/api/employees/1": detail,
  "/api/employees/1/employment-foundation": {
    employeeId: 1, contractType: "indefinite",
    employmentPeriods: [{ id: 1, startDate: "2021-03-12", endDate: null, contractType: "indefinite" }],
    compensationPackages: [compensationPackage],
  },
  "/api/employees/1/compensation-packages/2/correction-context": {
    package: compensationPackage, canCorrect,
    blockingReasons: canCorrect ? [] : ["Bu yürürlük aralığında çalışanın bordro satırı var; paket doğrudan düzeltilemez."],
    history: [],
  },
  "/api/employees/1/compensation-packages/2/remediation-case": {
    package: compensationPackage, canOpen: !canCorrect, case: null,
    currentImpact: { potentialPayrollRows: !canCorrect ? [{ rowId: 9, periodId: 7,
      year: 2026, month: 9, periodStatus: "draft", rowStatus: "precalc",
      recordedGrossSalary: 50000 }] : [], offboardingCases: [] },
  },
  "/api/employees/1/compensation-packages/2": { employeeId: 1, compensationPackages: [compensationPackage] },
  "/api/employees/1/compensation-packages/2/date-correction-preview": {
    package: compensationPackage, previousPackage: {
      ...compensationPackage, id: 1, version: 2,
      effectiveFrom: "2026-01-01", effectiveTo: "2026-08-31",
    },
    proposedEffectiveFrom: "2026-10-01", proposedPreviousEffectiveTo: "2026-09-30",
    canApply: true, blockingReasons: [], history: [],
  },
  "/api/employees/1/compensation-packages/2/effective-date": {
    employeeId: 1, compensationPackages: [{ ...compensationPackage, effectiveFrom: "2026-10-01" }],
  },
});

beforeEach(() => {
  localStorage.clear();
  stubApi({
    "/api/departments": departments,
    "/api/guidance/employee-entry/bookmark": { processKey: "personnel-entry", stepKey: "tab-mali", contentVersion: 1 },
    ...Object.fromEntries(
      ["tab-kimlik", "tab-iletisim", "tab-is", "tab-mali", "tab-ozluk", "tab-evrak"].map((stepKey) => [
        `/api/guidance/employee-entry/checks/${stepKey}`,
        {
          stepKey, contentVersion: 1,
          question: stepKey === "tab-is"
            ? "Çalışanın sözleşme türü henüz teyit edilmedi. Hangi yol doğrudur?"
            : "Bu adımda hangi bilgi doğrulanmalı?",
          choices: stepKey === "tab-is"
            ? ["Varsayılan türü seçip devam ederim", "İmzalı iş belgesini yetkili kişiyle doğrularım", "Başka çalışanın türünü kopyalarım"]
            : ["İlk seçeneği uygularım", "Kaynak belgeyi doğrularım", "Tahmin ederim"],
          lastSelectedChoice: null, lastCorrect: null, attemptCount: 0, explanation: null,
        },
      ]),
    ),
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

const renderModal = (employeeId: number | null, readOnly = false, onCreated?: (id: number) => void, guided = false) =>
  renderPage(
    <ToastProvider>
      <PersonnelModal employeeId={employeeId} readOnly={readOnly} onClose={() => {}} onCreated={onCreated} guided={guided} />
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
  await userEvent.click(screen.getByRole("button", { name: "Kaydetmeden önce kontrol et" }));
  const postedBeforeConfirmation = vi.mocked(fetch).mock.calls.some(
    ([u, i]) => String(u) === "/api/employees/1/compensation-packages" && i?.method === "POST",
  );
  expect(postedBeforeConfirmation).toBe(false);
  expect(screen.getByRole("region", { name: "Ücret paketi ön kontrolü" })).toHaveTextContent("Ücret yazısı-42");
  expect(screen.getByRole("region", { name: "Ücret paketi ön kontrolü" })).toHaveTextContent("Ahmet Yılmaz");

  await userEvent.click(screen.getByRole("button", { name: "Ücret paketini onayla ve kaydet" }));
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

test("aynı yürürlük tarihindeki paketi yeni kayıtla düzeltmeye izin vermez", async () => {
  stubApi({
    "/api/departments": departments,
    "/api/employees/1": detail,
    "/api/employees/1/employment-foundation": {
      employeeId: 1,
      contractType: "indefinite",
      employmentPeriods: [{ id: 1, startDate: "2021-03-12", endDate: null, contractType: "indefinite" }],
      compensationPackages: [{ id: 2, employmentPeriodId: 1, effectiveFrom: "2026-09-01", effectiveTo: null, monthlyRecurringTotal: 50000, sourceReference: "Eski karar" }],
    },
  });
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await screen.findByText(/Eski karar/);
  await userEvent.type(screen.getByLabelText("Yürürlük başlangıcı"), "2026-09-01");
  await userEvent.type(screen.getByLabelText("Aylık brüt maaş"), "55000");
  await userEvent.type(screen.getByLabelText("Kaynak / karar referansı"), "Yeni karar");
  await userEvent.click(screen.getByRole("button", { name: "Kaydetmeden önce kontrol et" }));
  expect(screen.getByText(/Aynı tarihe yeni kayıt ekleyerek hatayı düzeltemezsiniz/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(
    ([u, i]) => String(u) === "/api/employees/1/compensation-packages" && i?.method === "POST",
  )).toBe(false);
});

test("kullanılmamış pakette düzeltmeyi gerekçe ve sürümle onaya sunar", async () => {
  stubApi(correctionRoutes(true));
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  const gross = await screen.findByLabelText("Aylık brüt maaş", { selector: "#pm-correct-monthlyGrossSalary" });
  await userEvent.clear(gross);
  await userEvent.type(gross, "55000");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi"), "Kaynak yazıdaki rakam yanlış okunmuştu.");
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi kontrol et" }));
  expect(screen.getByRole("region", { name: "Düzeltme ön kontrolü" })).toHaveTextContent("55.000");
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) => String(u).endsWith("/compensation-packages/2") && i?.method === "PUT")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi onayla ve kaydet" }));
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([u, i]) => String(u).endsWith("/compensation-packages/2") && i?.method === "PUT");
    expect(JSON.parse(String(request?.[1]?.body))).toMatchObject({
      expectedVersion: 1, monthlyGrossSalary: 55000, sourceReference: "Eski karar",
      reason: "Kaynak yazıdaki rakam yanlış okunmuştu.",
    });
  });
});

test("son paketin tarih düzeltmesini sunucu önizlemesinden sonra gönderir", async () => {
  stubApi(correctionRoutes(true));
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  const date = await screen.findByLabelText("Önerilen yeni yürürlük tarihi");
  await userEvent.clear(date);
  await userEvent.type(date, "2026-10-01");
  await userEvent.type(screen.getByLabelText("Tarih düzeltme gerekçesi"), "Karardaki ay yanlış girildi.");
  await userEvent.click(screen.getByRole("button", { name: "Tarih etkisini önizle" }));
  expect(await screen.findByRole("region", { name: "Tarih düzeltme önizlemesi" })).toHaveTextContent("2026-09-30");
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) =>
    String(u).endsWith("/effective-date") && i?.method === "PUT")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Tarih düzeltmesini onayla ve kaydet" }));
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([u, i]) =>
      String(u).endsWith("/effective-date") && i?.method === "PUT");
    expect(JSON.parse(String(request?.[1]?.body))).toEqual({
      proposedEffectiveFrom: "2026-10-01", expectedPackageVersion: 1,
      expectedPreviousPackageId: 1, expectedPreviousPackageVersion: 2,
      reason: "Karardaki ay yanlış girildi.",
    });
  });
});

test("tarih önizlemesi bordro etkisini kilitlerse kaydetme sunmaz", async () => {
  const routes = correctionRoutes(false);
  stubApi({ ...routes,
    "/api/employees/1/compensation-packages/2/date-correction-preview": {
      ...routes["/api/employees/1/compensation-packages/2/date-correction-preview"],
      canApply: false,
      blockingReasons: ["Değişecek yürürlük aralığında bordro satırı var; tarih doğrudan düzeltilemez."],
    },
  });
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  const date = await screen.findByLabelText("Önerilen yeni yürürlük tarihi");
  await userEvent.clear(date);
  await userEvent.type(date, "2026-10-01");
  await userEvent.type(screen.getByLabelText("Tarih düzeltme gerekçesi"), "Karardaki ay yanlış girildi.");
  await userEvent.click(screen.getByRole("button", { name: "Tarih etkisini önizle" }));
  expect(await screen.findByText(/Değişecek yürürlük aralığında bordro satırı var/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Tarih düzeltmesini onayla ve kaydet" })).not.toBeInTheDocument();
});

test("bordroda kullanılmış paketin düzeltme nedenini gösterir ve kaydı kapatır", async () => {
  stubApi(correctionRoutes(false));
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  expect(await screen.findByText(/bordro satırı var; paket doğrudan düzeltilemez/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Düzeltmeyi kontrol et" })).not.toBeInTheDocument();
});

test("kilitli pakette telafi inceleme dosyası açar; finansal düzeltme yapmaz", async () => {
  stubApi(correctionRoutes(false));
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  expect(await screen.findByText(/olası etki/)).toBeInTheDocument();
  await userEvent.type(screen.getByLabelText("Hatanın açıklaması ve kaynağı"), "Ücret yazısı yanlış okunmuş.");
  await userEvent.clear(screen.getByLabelText("Önerilen aylık brüt maaş"));
  await userEvent.type(screen.getByLabelText("Önerilen aylık brüt maaş"), "52000");
  await userEvent.click(screen.getByRole("button", { name: "İnceleme dosyasını aç" }));
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([u, i]) =>
      String(u).endsWith("/compensation-packages/2/remediation-case") && i?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toMatchObject({
      expectedPackageVersion: 1, monthlyGrossSalary: 52000,
      issueDescription: "Ücret yazısı yanlış okunmuş.",
    });
  });
  expect(vi.mocked(fetch).mock.calls.some(([u, i]) =>
    String(u).endsWith("/compensation-packages/2") && i?.method === "PUT")).toBe(false);
});

test("aynı İK yöneticisi uzman referansıyla telafi incelemesini onaylar", async () => {
  const routes = correctionRoutes(false);
  const remediation = routes["/api/employees/1/compensation-packages/2/remediation-case"];
  stubApi({ ...routes,
    "/api/employees/1/compensation-packages/2/remediation-case": {
      ...remediation, canOpen: false,
      case: {
        id: 11, version: 2, status: "draft", packageVersion: 1,
        issueDescription: "Yanlış kaynak", proposedEffectiveFrom: null,
        proposedValues: compensationPackage, impactSnapshot: remediation.currentImpact,
        openedByUserId: "ik-admin", openedAtUtc: "2026-09-29T07:00:00Z",
        reviewedByUserId: null, reviewedAtUtc: null,
        expertReviewReference: null, reviewNotes: null,
      },
    },
    "/api/employees/1/compensation-packages/2/remediation-case/11/approve": { id: 11, status: "approved" },
  });
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  await userEvent.type(await screen.findByLabelText("Hatanın açıklaması ve kaynağı"), " ek");
  expect(screen.getByRole("button", { name: "İnceleme dosyasını onayla" })).toBeDisabled();
  await userEvent.clear(screen.getByLabelText("Hatanın açıklaması ve kaynağı"));
  await userEvent.type(screen.getByLabelText("Hatanın açıklaması ve kaynağı"), "Yanlış kaynak");
  await userEvent.type(await screen.findByLabelText("Uzman inceleme referansı"), "Uzman raporu 42");
  await userEvent.type(screen.getByLabelText("Uzman inceleme notu"), "Kaynak ve bordro karşılaştırıldı.");
  await userEvent.click(screen.getByRole("checkbox", { name: /Kaynakları ve olası etkileri inceledim/ }));
  await userEvent.click(screen.getByRole("button", { name: "İnceleme dosyasını onayla" }));
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([u, i]) =>
      String(u).endsWith("/remediation-case/11/approve") && i?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toEqual({
      expectedCaseVersion: 2, expertReviewReference: "Uzman raporu 42",
      reviewNotes: "Kaynak ve bordro karşılaştırıldı.", confirmExpertReview: true,
    });
  });
});

test("onaylı telafide açık bordro satırını önizler ve açık teyitle uygular", async () => {
  const routes = correctionRoutes(false);
  const remediation = routes["/api/employees/1/compensation-packages/2/remediation-case"];
  const rowUrl = "/api/employees/1/compensation-packages/2/remediation-case/11/payroll-periods/7/rows/9";
  const before = { grossSalary: 50000, premiumPay: 0, roadAllowance: 0,
    mealAllowance: 0, benefitPay: 0, grossEarnings: 50000, netPay: 35000,
    approvalStatus: "precalc" };
  const after = { ...before, grossSalary: 52000, grossEarnings: 52000, netPay: 36300 };
  stubApi({ ...routes,
    "/api/employees/1/compensation-packages/2/remediation-case": {
      ...remediation, canOpen: false,
      case: {
        id: 11, version: 3, status: "approved", packageVersion: 1,
        issueDescription: "Yanlış brüt ücret", proposedEffectiveFrom: null,
        proposedValues: { ...compensationPackage, monthlyGrossSalary: 52000 },
        impactSnapshot: remediation.currentImpact,
        openedByUserId: "ik-admin", openedAtUtc: "2026-09-29T07:00:00Z",
        reviewedByUserId: "ik-admin", reviewedAtUtc: "2026-09-29T08:00:00Z",
        expertReviewReference: "Uzman raporu 43", reviewNotes: "Satır doğrulandı.",
      },
    },
    [`${rowUrl}/preview`]: {
      rowId: 9, periodId: 7, canApply: true, blockingReasons: [],
      previewToken: "a".repeat(64), before, after, application: null,
    },
    [`${rowUrl}/apply`]: {
      id: 12, remediationCaseId: 11, payrollRowId: 9, before, after,
      actorUserId: "ik-admin", appliedAtUtc: "2026-09-29T09:00:00Z",
    },
  });
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  await userEvent.click(await screen.findByRole("button", { name: "Satır #9 için telafiyi önizle" }));
  expect(await screen.findByText("Net ödeme")).toBeInTheDocument();
  const apply = screen.getByRole("button", { name: "Önizlenen telafiyi uygula" });
  expect(apply).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox", { name: /Kaynak belgeyle bu bordro satırının/ }));
  await userEvent.click(apply);
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([u, i]) =>
      String(u).endsWith(`${rowUrl}/apply`) && i?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toEqual({
      expectedPreviewToken: "a".repeat(64), confirmSourceLink: true,
    });
  });
  expect(await screen.findByText(/Bu satıra telafi uygulandı/)).toBeInTheDocument();
});

test("onaylı bordro etki dosyasında tek dönem farkını ve sonraki ayı gösterir", async () => {
  const routes = correctionRoutes(false);
  const remediation = routes["/api/employees/1/compensation-packages/2/remediation-case"];
  const rowUrl = "/api/employees/1/compensation-packages/2/remediation-case/11/payroll-periods/7/rows/9";
  const approvedImpact = { potentialPayrollRows: [{ rowId: 9, periodId: 7, year: 2026,
    month: 9, periodStatus: "approved", rowStatus: "approved",
    recordedGrossSalary: 50000 }], offboardingCases: [] };
  stubApi({ ...routes,
    "/api/employees/1/compensation-packages/2/remediation-case": {
      ...remediation, canOpen: false,
      currentImpact: approvedImpact,
      case: { id: 11, version: 3, status: "approved", packageVersion: 1,
        issueDescription: "Yanlış brüt ücret", proposedEffectiveFrom: null,
        proposedValues: { ...compensationPackage, monthlyGrossSalary: 52000 },
        impactSnapshot: approvedImpact,
        openedByUserId: "ik-admin", openedAtUtc: "2026-09-29T07:00:00Z",
        reviewedByUserId: "ik-admin", reviewedAtUtc: "2026-09-29T08:00:00Z",
        expertReviewReference: "Uzman raporu 43", reviewNotes: "Satır doğrulandı." },
    },
    [`${rowUrl}/approved-impact`]: {
      periodId: 7, rowId: 9, periodStatus: "approved", remediationCaseId: 11,
      expertReviewReference: "Uzman raporu 43", reviewedAtUtc: "2026-09-29T08:00:00Z",
      canEstimate: true, reviewWarnings: [],
      recorded: { grossEarnings: 50000, incomeTaxBase: 42000,
        incomeTax: 6300, netPay: 35000, employerCost: 61000 },
      hypothetical: { grossEarnings: 52000, incomeTaxBase: 43700,
        incomeTax: 6555, netPay: 36300, employerCost: 63400 },
      laterSameYearRows: [{ periodId: 8, rowId: 10, year: 2026, month: 10,
        status: "draft" }],
      laterApprovedRows: [],
      canApply: true, blockingReasons: [], previewToken: "b".repeat(64), revision: null,
    },
    [`${rowUrl}/approved-impact/apply`]: {
      id: 12, revisionNumber: 1, payrollRowId: 9, remediationCaseId: 11,
      before: { grossSalary: 50000, netPay: 35000 },
      after: { grossSalary: 52000, netPay: 36300 },
      reason: "Yanlış brüt ücret", expertReviewReference: "Uzman sonuç 52",
      reviewNotes: "Teyit edildi", approvedByUserId: "ik-admin",
      approvedAtUtc: "2026-09-29T10:00:00Z",
    },
  });
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  await userEvent.click(await screen.findByRole("button", { name: "Onaylı satırın etki dosyasını aç" }));
  expect(await screen.findByText("GV matrahı")).toBeInTheDocument();
  expect(screen.getByText(/2026\/10 \(#10, draft\)/)).toBeInTheDocument();
  expect(screen.getByText(/Onaylı bordro sonucu değişmedi/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Önizlenen telafiyi uygula" })).not.toBeInTheDocument();
  const apply = screen.getByRole("button", { name: "Yeni parasal sürümü onayla" });
  expect(apply).toBeDisabled();
  await userEvent.type(screen.getByLabelText("Son uzman inceleme referansı"), "Uzman sonuç 52");
  await userEvent.type(screen.getByLabelText("Uzman inceleme notu"), "Teyit edildi");
  await userEvent.click(screen.getByRole("checkbox", { name: /Kaynak ücret paketi ile/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Dış ödeme ve bildirim farklarının/ }));
  await userEvent.click(apply);
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([u, i]) =>
      String(u).endsWith(`${rowUrl}/approved-impact/apply`) && i?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toEqual({
      expectedPreviewToken: "b".repeat(64), expertReviewReference: "Uzman sonuç 52",
      reviewNotes: "Teyit edildi", confirmSourceLink: true,
      confirmExternalReconciliation: true,
    });
  });
  expect(await screen.findByText(/Geçerli parasal sürüm #1/)).toBeInTheDocument();
});

test("pasif personelde ayrılış parasal taslağına erişir ve uzman teyidiyle onaya gönderir", async () => {
  const routes = correctionRoutes(false);
  const remuneration = routes["/api/employees/1/compensation-packages/2/remediation-case"];
  const revisionUrl = "/api/employees/1/compensation-packages/2/remediation-case/11/offboarding/21/settlement-revisions";
  const original = {
    monthlyGrossSalarySnapshot: 50000, regularPremiumSnapshot: 0,
    roadAllowanceSnapshot: 0, mealAllowanceSnapshot: 0,
    otherRegularBenefitsSnapshot: 0, unusedLeaveDays: 2,
    unusedLeavePayment: 1500, severancePayment: 0, noticePayment: 0,
    totalPayment: 1500, settlementSource: "external-expert",
    settlementReference: "İlk hesap", settlementNotes: null,
  };
  const draft = {
    id: 31, revisionNumber: 1, version: 1, status: "draft",
    remediationCaseId: 11, basedOnApprovedRevisionId: null,
    values: { ...original, monthlyGrossSalarySnapshot: 52000,
      unusedLeavePayment: 2000, totalPayment: 2000,
      settlementReference: "Uzman hesap 31" },
    reason: "Ücret görüntüsü hatalıydı.", openedByUserId: "ik-admin",
    openedAtUtc: "2026-09-29T09:00:00Z", approvedByUserId: null,
    approvedAtUtc: null, expertReviewReference: null, reviewNotes: null,
  };
  stubApi({ ...routes,
    "/api/employees/1": { ...detail, status: "passive", terminationDate: "2026-09-28" },
    "/api/employees/1/compensation-packages/2/remediation-case": {
      ...remuneration, canOpen: false,
      currentImpact: { potentialPayrollRows: [], offboardingCases: [{
        caseId: 21, status: "completed", terminationDate: "2026-09-28",
        grossSalarySnapshot: 50000,
      }] },
      case: {
        id: 11, version: 2, status: "approved", packageVersion: 1,
        issueDescription: "Ücret görüntüsü hatalıydı.", proposedEffectiveFrom: null,
        proposedValues: { ...compensationPackage, monthlyGrossSalary: 52000 },
        impactSnapshot: { potentialPayrollRows: [], offboardingCases: [{
          caseId: 21, status: "completed", terminationDate: "2026-09-28",
          grossSalarySnapshot: 50000,
        }] },
        openedByUserId: "ik-admin", openedAtUtc: "2026-09-29T07:00:00Z",
        reviewedByUserId: "ik-admin", reviewedAtUtc: "2026-09-29T08:00:00Z",
        expertReviewReference: "Ücret incelemesi 11", reviewNotes: "Doğrulandı.",
      },
    },
    [revisionUrl]: {
      offboardingCaseId: 21, offboardingCaseVersion: 2, remediationCaseId: 11,
      original, effective: original, draft, history: [], canOpen: false,
      blockingReasons: ["Açık parasal düzeltme taslağını tamamlayın."],
    },
    [`${revisionUrl}/31/approve`]: { ...draft, status: "approved" },
  });
  renderModal(1, true);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.click(await screen.findByRole("button", { name: "2026-09-01 paketinin düzeltme durumunu aç" }));
  expect(await screen.findByRole("region", { name: "Parasal düzeltme taslağı" })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Kaydetmeden önce kontrol et" })).not.toBeInTheDocument();
  const approve = screen.getByRole("button", { name: "Yeni geçerli parasal sürümü onayla" });
  expect(approve).toBeDisabled();
  await userEvent.type(screen.getByLabelText("Uzman son inceleme referansı"), "Son kontrol 31");
  await userEvent.type(screen.getByLabelText("Uzman inceleme notu"), "Ödeme farkı ayrıca izlenecek.");
  await userEvent.click(screen.getByRole("checkbox", { name: /Tutarları, hak edişi/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Dış ödeme ve bildirim farkını/ }));
  await userEvent.click(approve);
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([url, init]) =>
      String(url) === `${revisionUrl}/31/approve` && init?.method === "POST");
    expect(JSON.parse(String(request?.[1]?.body))).toEqual({
      expectedRevisionVersion: 1, expertReviewReference: "Son kontrol 31",
      reviewNotes: "Ödeme farkı ayrıca izlenecek.",
      confirmFinancialReview: true, confirmExternalReconciliation: true,
    });
  });
});

test("ön kontrol form değişince geçersizleşir ve iptal edilebilir", async () => {
  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await screen.findByText("Henüz ücret paketi yok.");
  await userEvent.type(screen.getByLabelText("Yürürlük başlangıcı"), "2026-09-01");
  await userEvent.type(screen.getByLabelText("Aylık brüt maaş"), "50000");
  await userEvent.type(screen.getByLabelText("Kaynak / karar referansı"), "Ücret yazısı-42");
  await userEvent.click(screen.getByRole("button", { name: "Kaydetmeden önce kontrol et" }));
  expect(screen.getByRole("region", { name: "Ücret paketi ön kontrolü" })).toBeInTheDocument();
  await userEvent.type(screen.getByLabelText("Kaynak / karar referansı"), " güncel");
  expect(screen.queryByRole("region", { name: "Ücret paketi ön kontrolü" })).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Kaydetmeden önce kontrol et" }));
  await userEvent.click(screen.getByRole("button", { name: "Düzenlemeye dön" }));
  expect(vi.mocked(fetch).mock.calls.some(
    ([u, i]) => String(u) === "/api/employees/1/compensation-packages" && i?.method === "POST",
  )).toBe(false);
});

test("pasif personelin işe dönüşü yeni çalışma dönemi olarak gönderilir", async () => {
  const passiveDetail = { ...detail, status: "passive" };
  stubApi({
    "/api/departments": departments,
    "/api/employees/1": passiveDetail,
    "/api/employees/1/documents": [],
    "/api/employees/1/employment-foundation": {
      employeeId: 1,
      contractType: "indefinite",
      employmentPeriods: [{ id: 1, startDate: "2021-03-12", endDate: "2026-08-31", contractType: "indefinite" }],
      compensationPackages: [],
    },
    "/api/employees/1/rehire": { ...detail, hireDate: "2026-09-15" },
  });

  renderModal(1);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.type(screen.getByLabelText("İşe dönüş tarihi"), "2026-09-15");
  await userEvent.selectOptions(screen.getByLabelText("Yeni sözleşme türü"), "fixed-term");
  await userEvent.click(screen.getByRole("button", { name: "Yeni çalışma dönemini başlat" }));

  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([url, init]) => String(url) === "/api/employees/1/rehire" && init?.method === "POST",
    );
    expect(JSON.parse(String(posted?.[1]?.body))).toEqual({
      rehireDate: "2026-09-15",
      contractType: "fixed-term",
    });
  });
});

test("rehber konumunu sunucudan yükler ve sekme değişimini kaydeder", async () => {
  renderModal(null, false, undefined, true);
  expect(await screen.findByText("Ödeme hazırlığını kontrol et")).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(
      ([url, init]) => String(url) === "/api/guidance/employee-entry/bookmark" && init?.method === "PUT",
    );
    expect(JSON.parse(String(request?.[1]?.body))).toEqual({ stepKey: "tab-is" });
  });
});

test("karar alıştırması denemeyi kaydeder, düzeltir ve işlem kanıtı saymaz", async () => {
  const baseFetch = globalThis.fetch;
  let attemptCount = 0;
  let lastChoice: number | null = null;
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    if (String(input) === "/api/guidance/employee-entry/checks/tab-is/attempts" && init?.method === "POST") {
      const { selectedChoice } = JSON.parse(String(init.body));
      attemptCount++;
      lastChoice = selectedChoice;
    }
    if (String(input) === "/api/guidance/employee-entry/checks/tab-is" ||
        String(input) === "/api/guidance/employee-entry/checks/tab-is/attempts") {
      return new Response(JSON.stringify({
        stepKey: "tab-is", contentVersion: 1,
        question: "Çalışanın sözleşme türü henüz teyit edilmedi. Hangi yol doğrudur?",
        choices: ["Varsayılan türü seçip devam ederim", "İmzalı iş belgesini yetkili kişiyle doğrularım", "Başka çalışanın türünü kopyalarım"],
        lastSelectedChoice: lastChoice, lastCorrect: lastChoice === null ? null : lastChoice === 1, attemptCount,
        explanation: lastChoice === null ? null : "Sözleşme türü sonraki süreçleri etkiler.",
      }), { status: 200, headers: { "Content-Type": "application/json" } });
    }
    return baseFetch(input, init);
  }));
  renderModal(null, false, undefined, true);
  await screen.findByText("Ödeme hazırlığını kontrol et");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.click(screen.getByText("Kısa karar alıştırması"));
  expect(await screen.findByText(/sözleşme türü henüz teyit edilmedi/)).toBeInTheDocument();
  await userEvent.click(screen.getByLabelText("Varsayılan türü seçip devam ederim"));
  expect(screen.queryByRole("status")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Yanıtı kontrol et" }));
  expect(await screen.findByRole("status")).toHaveTextContent("Bir daha düşünün.");
  await userEvent.click(screen.getByLabelText("İmzalı iş belgesini yetkili kişiyle doğrularım"));
  await userEvent.click(screen.getByRole("button", { name: "Yanıtı kontrol et" }));
  await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent("Doğru karar."));
  expect(screen.getByText(/2 kayıtlı deneme/)).toBeInTheDocument();
  expect(screen.getByText(/gerçek personel işlemi kanıtı veya yeterlilik onayı değildir/)).toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /İletişim/ }));
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.click(screen.getByText("Kısa karar alıştırması"));
  expect(await screen.findByRole("status")).toHaveTextContent("Doğru karar.");
  expect(screen.getByLabelText("İmzalı iş belgesini yetkili kişiyle doğrularım")).toBeChecked();
});

test("yeni kayıtta doğrulanmamış seçimleri varsaymaz ve sözleşme seçimini ister", async () => {
  renderModal(null);
  expect(screen.getByRole("region", { name: "İşe giriş rehberi" })).toHaveTextContent("Kişiyi doğru kaydet");
  expect(screen.getByLabelText("Cinsiyet")).toHaveValue("");
  await userEvent.click(screen.getByRole("button", { name: /^Kaydet$/ }));
  expect(screen.getByRole("alert")).toHaveTextContent("Sözleşme türünü imzalı belgeyle doğrulayıp seçin.");
  expect(screen.getByLabelText("Sözleşme Türü").closest(".content-section")).toHaveClass("active");
  await userEvent.selectOptions(screen.getByLabelText("Sözleşme Türü"), "indefinite");
  await userEvent.click(screen.getByRole("button", { name: /^Kaydet$/ }));
  expect(screen.getByRole("alert")).toHaveTextContent("Çalışma şeklini doğrulayıp seçin.");
  expect(vi.mocked(fetch).mock.calls.some(([url, init]) => String(url) === "/api/employees" && init?.method === "POST")).toBe(false);
});

test("yeni kayıt kaydedilince POST body doğru kurulur ve yalnız başarılı yanıtta devam eder", async () => {
  const onCreated = vi.fn();
  renderModal(null, false, onCreated);
  await screen.findByText("Yeni Personel Kartı");
  await userEvent.type(screen.getByLabelText("TC Kimlik No *"), "98765432109");
  await userEvent.type(screen.getByLabelText("Adı"), "Yeni");
  await userEvent.type(screen.getByLabelText("Soyadı"), "Kişi");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  expect(screen.getByRole("region", { name: "İşe giriş rehberi" })).toHaveTextContent("Çalışma ilişkisini tanımla");
  await userEvent.selectOptions(screen.getByLabelText("Departman"), "1");
  await userEvent.selectOptions(screen.getByLabelText("Sözleşme Türü"), "indefinite");
  await userEvent.selectOptions(screen.getByLabelText("Çalışma Şekli"), "Tam Zamanlı");
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
    expect(body.contractType).toBe("indefinite");
    expect(body.departmentId).toBe(1);
    expect(onCreated).toHaveBeenCalledWith(1);
  });
});

test("salt-okur modda inputlar disabled, Kaydet yok", async () => {
  renderModal(1, true);
  await screen.findByText("Personel Kartı — Ahmet Yılmaz");
  expect(screen.getByLabelText("Adı")).toBeDisabled();
  expect(screen.queryByRole("button", { name: /Kaydet/ })).not.toBeInTheDocument();
});


test("departman yoksa yeni çalışan için hazırlık adımına yönlendirir", async () => {
  stubApi({
    "/api/departments": [],
    "/api/guidance/employee-entry/bookmark": { processKey: "personnel-entry", stepKey: "tab-kimlik", contentVersion: 1 },
  });
  renderModal(null, false, undefined, true);
  expect(await screen.findByText(/Çalışan kartı için önce departman oluşturun/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Ayarlar bölümüne git" })).toHaveAttribute("href", "/app/settings");
  expect(screen.getByText(/Kaydedilmemiş form alanları bu geçişte korunmaz/)).toBeInTheDocument();
});
