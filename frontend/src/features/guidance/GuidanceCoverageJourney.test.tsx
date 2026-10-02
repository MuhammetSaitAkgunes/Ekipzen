import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { GuidanceCoverageJourney } from "./GuidanceCoverageJourney";

beforeEach(() => { localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: { id: "u", role: "hr-admin", name: "İK" } })); });
afterEach(() => vi.unstubAllGlobals());
const content = { id: 21, state: "published", isCurrent: true, processKey: "payroll", contentVersion: 2, body: { stepKey: "approval" } };
function render() { return renderPage(<AuthProvider><GuidanceCoverageJourney /></AuthProvider>); }
test("tüm süreç görünümü açılmadan okumaz; eski kanıtı yeni sürüme aktarmadan gösterir", async () => {
  stubApi({ "/api/guidance/catalog": [content], "/api/guidance/catalog/readings": [{ contentRevisionId: 20, isCurrent: false }],
    "/api/guidance/catalog/practice-progress": [{ contentRevisionId: 20, processKey: "payroll", stepKey: "approval", lastCorrect: true, isCurrent: false }],
    "/api/guidance/operations": [{ isCurrent: false, evidence: { content: { contentRevisionId: 20, processKey: "payroll", stepKey: "approval" } } }] });
  render(); expect(fetch).not.toHaveBeenCalled(); await userEvent.click(screen.getByText("Tüm süreçlerde güncel öğrenme durumum"));
  const label = await screen.findByText("Satır ve dönem onayını ayırma · Sürüm 2"); const row = label.closest("li")!;
  expect(within(row).getByText(/İnceleme beyanı yok.*Alıştırma yanıtı yok.*Gerçek işlem kanıtı yok/)).toBeInTheDocument();
  expect(within(row).getByRole("link")).toHaveAttribute("href", "/app/guidance/catalog?version=21");
  expect(screen.getAllByText("Bu adımın güncel onaylı içeriği henüz yayımlanmamış.")).toHaveLength(37);
});
test("güncel kişisel kanıtlar ayrı gösterilir; başka adımın işi başarı sayılmaz", async () => {
  stubApi({ "/api/guidance/catalog": [content], "/api/guidance/catalog/readings": [{ contentRevisionId: 21, isCurrent: true }],
    "/api/guidance/catalog/practice-progress": [{ contentRevisionId: 21, processKey: "payroll", stepKey: "approval", lastCorrect: true, isCurrent: true }],
    "/api/guidance/operations": [{ isCurrent: true, evidence: { content: { contentRevisionId: 21, processKey: "payroll", stepKey: "period-create" } } }] });
  render(); await userEvent.click(screen.getByText("Tüm süreçlerde güncel öğrenme durumum"));
  const row = (await screen.findByText("Satır ve dönem onayını ayırma · Sürüm 2")).closest("li")!;
  expect(within(row).getByText(/İncelediğinize dair beyan var.*Son alıştırma yanıtı doğru.*Gerçek işlem kanıtı yok/)).toBeInTheDocument();
  expect(screen.getByText(/bağımsız yeterlilik/)).toBeInTheDocument();
});
test("okuma hatasında eksik kanıt diye sonuç üretmez", async () => {
  stubApi({ "/api/guidance/catalog": [content] }); render();
  await userEvent.click(screen.getByText("Tüm süreçlerde güncel öğrenme durumum"));
  await screen.findByRole("alert"); expect(screen.queryByText(/İnceleme beyanı yok/)).not.toBeInTheDocument();
  await waitFor(() => expect(screen.getByRole("button", { name: "Durumu yeniden yükle" })).toBeEnabled());
});
