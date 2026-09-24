import { screen, within } from "@testing-library/react";
import { afterEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { GuidanceJourneyPage } from "./GuidanceJourneyPage";

afterEach(() => vi.unstubAllGlobals());

test("konumu kaydedilmemiş rehberde gerçek işlem kanıtı sıfır görünür", async () => {
  stubApi({ "/api/guidance/employee-entry/journey": {
    processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: false,
    bookmarkStepKey: "tab-kimlik", verifiedEmployeeRecords: 0, lastVerifiedAtUtc: null,
  } });
  renderPage(<GuidanceJourneyPage />);
  expect(await screen.findByText("Henüz konum kaydedilmedi")).toBeInTheDocument();
  expect(screen.getByText("0 kayıt")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /Gerçek bir işe girişte rehberi aç/ }))
    .toHaveAttribute("href", "/app/personnel/new");
});

test("doğrulanmış çalışan kartı ayrı gösterilir ve tüm sürece yeterlilik sayılmaz", async () => {
  stubApi({ "/api/guidance/employee-entry/journey": {
    processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: true,
    bookmarkStepKey: "tab-evrak", verifiedEmployeeRecords: 2,
    lastVerifiedAtUtc: "2026-09-24T10:00:00Z",
  } });
  renderPage(<GuidanceJourneyPage />);
  expect(await screen.findByText("2 kayıt")).toBeInTheDocument();
  expect(screen.getByText("Belgeler")).toBeInTheDocument();
  expect(screen.getByText(/bağımsız yeterlilik kazanıldığı anlamına gelmez/)).toBeInTheDocument();
  expect(screen.getByText(/form taslağı değildir/)).toBeInTheDocument();
});


test("süreç haritası bütün kapsam gruplarını, gerçek ekranları ve rehber sınırını gösterir", async () => {
  stubApi({ "/api/guidance/employee-entry/journey": {
    processKey: "personnel-entry", contentVersion: 1, hasSavedPosition: false,
    bookmarkStepKey: "tab-kimlik", verifiedEmployeeRecords: 0, lastVerifiedAtUtc: null,
  } });
  renderPage(<GuidanceJourneyPage />);
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
  expect(within(map).getAllByText("Ayrıntılı rehber hazırlanıyor")).toHaveLength(6);
  expect(within(map).getByText(/ekranın bulunması, açıklamalı eğitimin tamamlandığı anlamına gelmez/)).toBeInTheDocument();
});

test("ilerleme verisi yüklenemese de süreç haritası erişilebilir kalır", async () => {
  vi.stubGlobal("fetch", vi.fn(async () => new Response(
    JSON.stringify({ title: "Geçici hata" }),
    { status: 503, headers: { "Content-Type": "application/json" } },
  )));
  renderPage(<GuidanceJourneyPage />);
  expect(await screen.findByRole("region", { name: "İK süreç haritası" })).toBeInTheDocument();
  expect(await screen.findByText("Veri yüklenemedi")).toBeInTheDocument();
});
