import { screen, within } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { DailyGuidanceTasks } from "./DailyGuidanceTasks";

afterEach(() => vi.unstubAllGlobals());

test("görev gerekçesi, sonraki adım ve bilinen/bilinmeyen tarih açıkça görünür", async () => {
  stubApi({
    "/api/guidance/daily-tasks": [
      {
        key: "compliance-document:12", kind: "compliance-document", recordId: 12,
        title: "Ece Arslan · Sözleşme", reason: "Belge kaydı \"Eksik\" durumunda.",
        nextAction: "Kaydı ve sorumluyu inceleyin; gerçek duruma göre güncelleyin.",
        dueDate: null, dateNote: "Son tarih kayıtlı değil; öncelik tarihine göre değerlendirilemiyor.",
        targetPath: "/risk/compliance?documentId=12",
      },
      {
        key: "employee-compensation:42", kind: "employee-compensation", recordId: 42,
        title: "Yeni Kişi ücret hazırlığını kontrol et",
        reason: "Açık çalışma dönemi için bugün yürürlükte ücret paketi yok.",
        nextAction: "Tutarı, yürürlük tarihini ve kaynak referansını doğrulayın.",
        dueDate: null, dateNote: "İşe giriş: 24.09.2026. Bu tarih karar son tarihi değildir; kayıtlı son tarih yok.",
        targetPath: "/personnel/42?edit=1&guide=1&tab=tab-mali",
      },
      {
        key: "offboarding-case:17", kind: "offboarding-case", recordId: 17,
        title: "Yeni Kişi ayrılış taslağını incele",
        reason: "İşten ayrılış dosyası taslak durumunda; personel henüz pasife alınmadı.",
        nextAction: "Tarihi, fesih nedenini, ekip devrini ve uzman/harici hesap dayanağını doğrulayın.",
        dueDate: null, dateNote: "Taslakta kayıtlı ayrılış tarihi: 24.09.2026. Bu tarih karar son tarihi değildir; kayıtlı son tarih yok.",
        targetPath: "/personnel/42?offboarding=1",
      },
      {
        key: "payroll-period:3", kind: "payroll-period", recordId: 3,
        title: "Temmuz 2026 bordro dönemini incele",
        reason: "Dönem taslak durumunda; henüz onaylanmadı.",
        nextAction: "Satırları ve uyarıları inceleyin.",
        dueDate: null, dateNote: "Bu dönem için kayıtlı son tarih yok.",
        targetPath: "/payroll?periodId=3",
      },
      {
        key: "payroll-revision-external-payment:51",
        kind: "payroll-revision-external-payment", recordId: 51,
        title: "Yeni Kişi bordro düzeltmesi ödeme kanıtını incele",
        reason: "Parasal sürümün dış ödeme sonucu kanıtlanmadı.",
        nextAction: "Dış ödeme belgesini inceleyin.", dueDate: null,
        dateNote: "Güvenilir son tarih kayıtlı değil.",
        targetPath: "/personnel/42?tab=tab-mali",
      },
      {
        key: "payroll-revision-external-notification:51",
        kind: "payroll-revision-external-notification", recordId: 51,
        title: "Yeni Kişi bordro düzeltmesi bildirim kanıtını incele",
        reason: "Parasal sürümün dış bildirim sonucu kanıtlanmadı.",
        nextAction: "Dış bildirim belgesini inceleyin.", dueDate: null,
        dateNote: "Güvenilir son tarih kayıtlı değil.",
        targetPath: "/personnel/42?tab=tab-mali",
      },
      {
        key: "candidate-review:6", kind: "candidate-review", recordId: 6,
        title: "Selin Koç başvurusunu incele",
        reason: "UI/UX Designer başvurusu Yeni aşamasında; ilk değerlendirme bekliyor.",
        nextAction: "Adayın bilgilerini inceleyin.",
        dueDate: null, dateNote: "Başvuru tarihi karar son tarihi değildir; kayıtlı son tarih yok.",
        targetPath: "/recruitment?candidateId=6",
      },
      {
        key: "leave-approval:8", kind: "leave-approval", recordId: 8,
        title: "Ahmet Yılmaz izin talebini incele", reason: "Mazeret talebi onay bekliyor.",
        nextAction: "Talep ayrıntılarını inceleyip onay veya ret kararı verin.",
        dueDate: null, dateNote: "İzin başlangıcı: 30.09.2026. Karar için kayıtlı son tarih yok.",
        targetPath: "/inbox?tab=approvals&requestId=8",
      },
    ],
  });
  renderPage(<DailyGuidanceTasks />);
  expect(await screen.findByText(/Diğer İK işleri burada henüz üretilmez/)).toBeInTheDocument();
  const documentCard = screen.getByText("Ece Arslan · Sözleşme").closest("article")!;
  expect(within(documentCard).getByText(/Son tarih kayıtlı değil/)).toBeInTheDocument();
  expect(within(documentCard).getByText(/Neden?/)).toBeInTheDocument();
  expect(within(documentCard).getByRole("link", { name: /Kaydı incele/ }))
    .toHaveAttribute("href", "/app/risk/compliance?documentId=12");
  const compensationCard = screen.getByText("Yeni Kişi ücret hazırlığını kontrol et").closest("article")!;
  expect(within(compensationCard).getByText("ÜCRET HAZIRLIĞI")).toBeInTheDocument();
  expect(within(compensationCard).getByText(/Bu tarih karar son tarihi değildir/)).toBeInTheDocument();
  expect(within(compensationCard).getByRole("link", { name: /Kaydı incele/ }))
    .toHaveAttribute("href", "/app/personnel/42?edit=1&guide=1&tab=tab-mali");
  const offboardingCard = screen.getByText("Yeni Kişi ayrılış taslağını incele").closest("article")!;
  expect(within(offboardingCard).getByText("AYRILIŞ TASLAĞI")).toBeInTheDocument();
  expect(within(offboardingCard).getByText(/Bu tarih karar son tarihi değildir/)).toBeInTheDocument();
  expect(within(offboardingCard).getByRole("link", { name: /Kaydı incele/ }))
    .toHaveAttribute("href", "/app/personnel/42?offboarding=1");
  const payrollCard = screen.getByText("Temmuz 2026 bordro dönemini incele").closest("article")!;
  expect(within(payrollCard).getByText("BORDRO DÖNEMİ")).toBeInTheDocument();
  expect(within(payrollCard).getByText(/kayıtlı son tarih yok/)).toBeInTheDocument();
  expect(within(payrollCard).getByRole("link", { name: /Kaydı incele/ }))
    .toHaveAttribute("href", "/app/payroll?periodId=3");
  expect(within(screen.getByText("Yeni Kişi bordro düzeltmesi ödeme kanıtını incele")
    .closest("article")!).getByText("BORDRO DIŞ ÖDEME KANITI")).toBeInTheDocument();
  expect(within(screen.getByText("Yeni Kişi bordro düzeltmesi bildirim kanıtını incele")
    .closest("article")!).getByText("BORDRO DIŞ BİLDİRİM KANITI")).toBeInTheDocument();
  const candidateCard = screen.getByText("Selin Koç başvurusunu incele").closest("article")!;
  expect(within(candidateCard).getByText("ADAY İNCELEMESİ")).toBeInTheDocument();
  expect(within(candidateCard).getByText(/Başvuru tarihi karar son tarihi değildir/)).toBeInTheDocument();
  expect(within(candidateCard).getByRole("link", { name: /Kaydı incele/ }))
    .toHaveAttribute("href", "/app/recruitment?candidateId=6");
  const leaveCard = screen.getByText("Ahmet Yılmaz izin talebini incele").closest("article")!;
  expect(within(leaveCard).getByText(/Karar için kayıtlı son tarih yok/)).toBeInTheDocument();
  expect(within(leaveCard).getByRole("link", { name: /Kaydı incele/ }))
    .toHaveAttribute("href", "/app/inbox?tab=approvals&requestId=8");
});

test("boş liste bütün İK işlerinin bittiğini iddia etmez", async () => {
  stubApi({ "/api/guidance/daily-tasks": [] });
  renderPage(<DailyGuidanceTasks />);
  expect(await screen.findByText("Bu alanlarda bekleyen iş yok")).toBeInTheDocument();
  expect(screen.getByText(/Diğer işleri İş Kutusu'ndan kontrol edin/)).toBeInTheDocument();
});
