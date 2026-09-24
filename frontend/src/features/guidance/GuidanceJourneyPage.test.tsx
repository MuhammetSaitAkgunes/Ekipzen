import { screen } from "@testing-library/react";
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
