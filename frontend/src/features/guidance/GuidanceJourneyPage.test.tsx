import { screen, within } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { GuidanceJourneyPage } from "./GuidanceJourneyPage";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";

afterEach(() => vi.unstubAllGlobals());

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: null },
  }));
beforeEach(() => { localStorage.clear(); setRole("hr-admin"); });

const renderJourney = () => renderPage(<AuthProvider><GuidanceJourneyPage /></AuthProvider>);

const leaveJourney = {
  processKey: "leave-approval", verifiedDecisions: 0, lastVerifiedAtUtc: null,
  contentVersion: 1, answeredChecks: 0, correctChecks: 0,
};

const recruitmentJourney = {
  processKey: "recruitment", verifiedFirstReviews: 0, lastVerifiedAtUtc: null,
  contentVersion: 1, answeredChecks: 0, correctChecks: 0,
};

test("konumu kaydedilmemiş rehberde gerçek işlem kanıtı sıfır görünür", async () => {
  stubApi({
    "/api/guidance/leave-approval/journey": leaveJourney,
    "/api/guidance/recruitment/journey": recruitmentJourney,
    "/api/guidance/employee-entry/journey": {
    processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: false,
    bookmarkStepKey: "tab-kimlik", verifiedEmployeeRecords: 0, answeredChecks: 0, correctChecks: 0, lastVerifiedAtUtc: null,
  } });
  renderJourney();
  expect(await screen.findByText("Henüz konum kaydedilmedi")).toBeInTheDocument();
  expect(screen.getByText("0 kayıt")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /Gerçek bir işe girişte rehberi aç/ }))
    .toHaveAttribute("href", "/app/personnel/new");
});

test("doğrulanmış çalışan kartı ayrı gösterilir ve tüm sürece yeterlilik sayılmaz", async () => {
  stubApi({
    "/api/guidance/recruitment/journey": recruitmentJourney,
    "/api/guidance/employee-entry/journey": {
    processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: true,
    bookmarkStepKey: "tab-evrak", verifiedEmployeeRecords: 2, answeredChecks: 4, correctChecks: 3,
    lastVerifiedAtUtc: "2026-09-24T10:00:00Z",
  } });
  renderJourney();
  expect(await screen.findByText("2 kayıt")).toBeInTheDocument();
  expect(screen.getByText("Belgeler")).toBeInTheDocument();
  expect(screen.getByText("4/6")).toBeInTheDocument();
  expect(screen.getByText("3/6")).toBeInTheDocument();
  expect(screen.getByText(/doğru yanıt tek başına bağımsız yeterlilik değildir/)).toBeInTheDocument();
  expect(screen.getByText(/bağımsız yeterlilik kazanıldığı anlamına gelmez/)).toBeInTheDocument();
  expect(screen.getByText(/form taslağı değildir/)).toBeInTheDocument();
});


test("aday kararı yalnız gerçek işlem kanıtı olarak görünür", async () => {
  stubApi({
    "/api/guidance/employee-entry/journey": {
      processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: false,
      bookmarkStepKey: "tab-kimlik", verifiedEmployeeRecords: 0, answeredChecks: 0, correctChecks: 0, lastVerifiedAtUtc: null,
    },
    "/api/guidance/recruitment/journey": {
      processKey: "recruitment", verifiedFirstReviews: 2, lastVerifiedAtUtc: "2026-09-28T10:00:00Z",
      contentVersion: 1, answeredChecks: 2, correctChecks: 1,
    },
  });
  renderJourney();
  const card = await screen.findByRole("region", { name: "Aday ilk incelemesi" });
  expect(within(card).getByText("2 aday")).toBeInTheDocument();
  expect(within(card).getByText("2/2")).toBeInTheDocument();
  expect(within(card).getByText("1/2")).toBeInTheDocument();
  expect(within(card).getByText(/Kararın doğruluğunu/)).toBeInTheDocument();
  expect(within(card).getByRole("link", { name: "Aday havuzunu aç" }))
    .toHaveAttribute("href", "/app/recruitment");
});

test("aday yolculuğu yüklenemezse çalışan kanıtı görünür kalır", async () => {
  stubApi({
    "/api/guidance/employee-entry/journey": {
      processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: false,
      bookmarkStepKey: "tab-kimlik", verifiedEmployeeRecords: 1, answeredChecks: 0, correctChecks: 0, lastVerifiedAtUtc: null,
    },
  });
  renderJourney();
  expect(await screen.findByText("1 kayıt")).toBeInTheDocument();
  expect(await screen.findByText(/Aday yolculuğu yüklenemedi/)).toBeInTheDocument();
});

test("yönetici yalnız kendi yetkili yolculuğunu ve izin karar ilerlemesini görür", async () => {
  setRole("manager");
  stubApi({
    "/api/guidance/leave-approval/journey": {
      processKey: "leave-approval", verifiedDecisions: 3, lastVerifiedAtUtc: "2026-09-28T10:00:00Z",
      contentVersion: 1, answeredChecks: 2, correctChecks: 1,
    },
  });
  renderJourney();
  const leaveCard = await screen.findByRole("region", { name: "İzin kararlarını öğrenme" });
  expect(within(leaveCard).getByText("3 talep")).toBeInTheDocument();
  expect(within(leaveCard).getByText("2/2")).toBeInTheDocument();
  expect(within(leaveCard).getByText("1/2")).toBeInTheDocument();
  expect(within(leaveCard).getByRole("link", { name: "Bekleyen izinleri aç" }))
    .toHaveAttribute("href", "/app/inbox?tab=approvals");
  expect(screen.queryByRole("region", { name: "Aday ilk incelemesi" })).not.toBeInTheDocument();
  const map = screen.getByRole("region", { name: "İK süreç haritası" });
  expect(within(map).getAllByRole("listitem")).toHaveLength(5);
  expect(within(map).queryByRole("link", { name: "İşe alım ekranını aç" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url]) =>
    String(url) === "/api/guidance/recruitment/journey" ||
    String(url) === "/api/guidance/employee-entry/journey")).toBe(false);
});

test("süreç haritası bütün kapsam gruplarını, gerçek ekranları ve rehber sınırını gösterir", async () => {
  stubApi({
    "/api/guidance/recruitment/journey": recruitmentJourney,
    "/api/guidance/employee-entry/journey": {
    processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: false,
    bookmarkStepKey: "tab-kimlik", verifiedEmployeeRecords: 0, answeredChecks: 0, correctChecks: 0, lastVerifiedAtUtc: null,
  } });
  renderJourney();
  const map = await screen.findByRole("region", { name: "İK süreç haritası" });
  expect(within(map).getAllByRole("listitem")).toHaveLength(10);
  expect(within(map).getByRole("link", { name: "Şirket hazırlığı ekranını aç" }))
    .toHaveAttribute("href", "/app/settings");
  expect(within(map).getByRole("link", { name: "İşe giriş ekranını aç" }))
    .toHaveAttribute("href", "/app/personnel/new");
  expect(within(map).getByRole("link", { name: "İzin ve onay ekranını aç" }))
    .toHaveAttribute("href", "/app/inbox?tab=approvals");
  expect(within(map).getByRole("link", { name: "Günlük yönetim ve analiz ekranını aç" }))
    .toHaveAttribute("href", "/app/inbox?tab=daily");
  expect(within(map).getAllByText("Ayrıntılı rehber hazırlanıyor")).toHaveLength(1);
  expect(within(map).getAllByText("Karar öncesi kontrol var")).toHaveLength(6);
  expect(within(map).getByText(/ekranın bulunması, açıklamalı eğitimin tamamlandığı anlamına gelmez/)).toBeInTheDocument();
});

test("ilerleme verisi yüklenemese de süreç haritası erişilebilir kalır", async () => {
  vi.stubGlobal("fetch", vi.fn(async () => new Response(
    JSON.stringify({ title: "Geçici hata" }),
    { status: 503, headers: { "Content-Type": "application/json" } },
  )));
  renderJourney();
  expect(await screen.findByRole("region", { name: "İK süreç haritası" })).toBeInTheDocument();
  expect(await screen.findByText("Veri yüklenemedi")).toBeInTheDocument();
});
