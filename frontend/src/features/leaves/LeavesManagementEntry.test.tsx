import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { screen } from "@testing-library/react";
import { AuthProvider } from "../../auth/AuthContext";
import { clearSession, setSession } from "../../api/session";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { LeavesPage } from "./LeavesPage";

beforeEach(() => {
  localStorage.clear();
  stubApi({ "/api/leaves/team": [], "/api/leaves/my": [], "/api/leaves/ledger": [],
    "/api/leaves/learning-operations": [], "/api/leaves/balance": { remainingDays: 0, entitledDays: 0, carriedOverDays: 0 } });
});
afterEach(() => { clearSession(); vi.unstubAllGlobals(); });
function entry(role: string, employeeId: number | null, path = "/app/leaves") {
  setSession({ token: "test", refreshToken: "test", user: { id: "management", role, employeeId } });
  renderPage(<AuthProvider><ToastProvider><LeavesPage /></ToastProvider></AuthProvider>, [path]);
}
test.each([
  ["hr-admin", "/app/leaves"], ["hr-admin", "/app/leaves?tab=personal"],
  ["manager", "/app/leaves"], ["manager", "/app/leaves?tab=personal"],
])("personel kartı olmayan %s hesabı %s girişinde kişisel sorguya düşmez", async (role, path) => {
  entry(role, null, path);
  await screen.findByRole("heading", { name: "Ekip izinleri" });
  expect(screen.getByRole("tab", { name: "Ekip izinleri" })).toHaveAttribute("aria-selected", "true");
  expect(screen.queryByRole("tab", { name: "İzinlerim" })).not.toBeInTheDocument();
  expect(screen.getByText(/Hesabınıza bağlı personel kartı olmadığı/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /Bekleyen onaylar/ })).toHaveAttribute("href", "/app/inbox?tab=approvals");
  const paths = vi.mocked(fetch).mock.calls.map(([url]) => String(url));
  expect(paths).not.toContain("/api/leaves/balance");
  expect(paths).not.toContain("/api/leaves/ledger");
  expect(paths).not.toContain("/api/leaves/my");
});
test("personel kartı bağlı İK yöneticisinin kişisel izin görünümü korunur", async () => {
  entry("hr-admin", 5);
  await screen.findByRole("heading", { name: "İzin Bakiye Defteri" });
  expect(screen.getByRole("tab", { name: "İzinlerim" })).toHaveAttribute("aria-selected", "true");
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url) === "/api/leaves/balance")).toBe(true);
});
