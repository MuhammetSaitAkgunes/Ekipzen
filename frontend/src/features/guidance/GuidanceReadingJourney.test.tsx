import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { GuidanceReadingJourney } from "./GuidanceReadingJourney";

beforeEach(() => { localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R",
  user: { id: "u", name: "U", email: "u@example.test", role: "hr-admin", roleLabel: "İK", initials: "U", employeeId: null } })); });
afterEach(() => vi.unstubAllGlobals());
test("eski inceleme kendi sürümünde kalır, yeni sürüm tamamlanmış öğrenme sayılmaz", async () => {
  stubApi({ "/api/guidance/catalog/readings": [{ contentRevisionId: 12, contentKey: "payroll-start", processKey: "payroll",
    contentVersion: 1, title: "Bordro kontrolü", firstReadAtUtc: "2026-10-01T08:00:00Z", isCurrent: false }] });
  renderPage(<AuthProvider><GuidanceReadingJourney /></AuthProvider>);
  expect(await screen.findByRole("link", { name: /Bordro kontrolü · Sürüm 1/ })).toHaveAttribute("href", "/app/guidance/catalog?version=12");
  expect(screen.getByText(/Güncel sürüm değil; yeniden kontrol edin/)).toBeInTheDocument();
  expect(screen.getByText(/Gerçek iş, doğru karar veya bağımsız yeterlilik kanıtı değildir/)).toBeInTheDocument();
});
test("inceleme servisi hata verirse sıfır ilerleme diye sunulmaz", async () => {
  stubApi({}); renderPage(<AuthProvider><GuidanceReadingJourney /></AuthProvider>);
  expect(await screen.findByText(/İçerik inceleme kayıtları yüklenemedi/)).toBeInTheDocument();
  expect(screen.queryByText(/Henüz bir içerik sürümünü/)).not.toBeInTheDocument();
});
