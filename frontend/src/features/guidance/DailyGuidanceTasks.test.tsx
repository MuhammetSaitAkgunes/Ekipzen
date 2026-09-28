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
        key: "payroll-period:3", kind: "payroll-period", recordId: 3,
        title: "Temmuz 2026 bordro dönemini incele",
        reason: "Dönem taslak durumunda; henüz onaylanmadı.",
        nextAction: "Satırları ve uyarıları inceleyin.",
        dueDate: null, dateNote: "Bu dönem için kayıtlı son tarih yok.",
        targetPath: "/payroll?periodId=3",
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
  const payrollCard = screen.getByText("Temmuz 2026 bordro dönemini incele").closest("article")!;
  expect(within(payrollCard).getByText("BORDRO DÖNEMİ")).toBeInTheDocument();
  expect(within(payrollCard).getByText(/kayıtlı son tarih yok/)).toBeInTheDocument();
  expect(within(payrollCard).getByRole("link", { name: /Kaydı incele/ }))
    .toHaveAttribute("href", "/app/payroll?periodId=3");
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
