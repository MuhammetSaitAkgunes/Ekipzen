import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { GuidanceStepLessons } from "./GuidanceStepLessons";

function setRole(role: string) {
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: {
    id: "u", name: "Görevli", email: "u@example.test", role, roleLabel: role, initials: "G", employeeId: null,
  } }));
}
beforeEach(() => { localStorage.clear(); setRole("hr-admin"); });
afterEach(() => vi.unstubAllGlobals());


test.each([
  ["company", "initial-setup", "access-preparation", 4],
  ["recruitment", "candidate-create", "hire-preparation", 3],
  ["personnel", "record-review", "rehire", 3],
  ["attendance", "entry-review", "period-summary", 3],
  ["payroll", "settings-review", "correction-external", 6],
  ["compliance", "record-review", "record-correction", 3],
  ["offboarding", "preparation", "correction-external", 5],
  ["daily", "task-review", "external-followup", 3],
])("%s sürecinde yalnız aktif adım gösterilir; gezinme gerçek işlem oluşturmaz", async (process, first, last, count) => {
  stubApi({ [`/api/guidance/catalog/steps/${process}/${first}`]: null, [`/api/guidance/catalog/steps/${process}/${last}`]: null });
  renderPage(<AuthProvider><GuidanceStepLessons process={process as string} /></AuthProvider>);
  const tabs = screen.getAllByRole("tab");
  expect(tabs).toHaveLength(count as number);
  expect(tabs[0]).toHaveAttribute("aria-selected", "true");
  expect(screen.getAllByRole("tabpanel")).toHaveLength(1);
  await screen.findByText(/henüz yayımlanmadı/);
  await userEvent.click(tabs[tabs.length - 1]);
  expect(tabs[tabs.length - 1]).toHaveAttribute("aria-selected", "true");
  await screen.findByText(/henüz yayımlanmadı/);
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url) === `/api/guidance/catalog/steps/${process}/${last}`)).toBe(true);
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => ["POST", "PUT", "PATCH", "DELETE"].includes(init?.method ?? "GET"))).toBe(false);
});

test("klavye ile adım seçilir ve odak korunur", async () => {
  stubApi({ "/api/guidance/catalog/steps/company/initial-setup": null, "/api/guidance/catalog/steps/company/company-profile": null });
  renderPage(<AuthProvider><GuidanceStepLessons process="company" /></AuthProvider>);
  const tabs = screen.getAllByRole("tab"); tabs[0].focus();
  await userEvent.keyboard("{ArrowRight}");
  expect(tabs[1]).toHaveFocus(); expect(tabs[1]).toHaveAttribute("aria-selected", "true");
  expect(tabs[0]).toHaveAttribute("tabindex", "-1");
  expect(vi.mocked(fetch).mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
});

test("yönetici yalnız yetkili adımları görür; çalışan için yönetim süreci açılmaz", () => {
  setRole("manager"); stubApi({ "/api/guidance/catalog/steps/compliance/record-review": null });
  const page = renderPage(<AuthProvider><GuidanceStepLessons process="compliance" /><GuidanceStepLessons process="payroll" /><GuidanceStepLessons process="offboarding" /></AuthProvider>);
  expect(screen.getAllByRole("tab")).toHaveLength(1);
  expect(screen.getByRole("tab")).toHaveTextContent("Gerçek belgeyi ve takip kaydını inceleme");
  expect(screen.queryByText("Doğrulanmış belge durumunu işleme")).not.toBeInTheDocument();
  expect(screen.queryByText("Bordro")).not.toBeInTheDocument(); page.unmount();
  setRole("employee"); vi.mocked(fetch).mockClear();
  renderPage(<AuthProvider><GuidanceStepLessons process="payroll" /><GuidanceStepLessons process="personnel" /></AuthProvider>);
  expect(screen.queryByRole("tab")).not.toBeInTheDocument(); expect(fetch).not.toHaveBeenCalled();
});
