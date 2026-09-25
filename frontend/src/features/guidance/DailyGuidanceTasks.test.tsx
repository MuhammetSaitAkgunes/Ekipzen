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
