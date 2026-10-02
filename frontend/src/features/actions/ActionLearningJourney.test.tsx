import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { guidanceStepDrafts } from "../guidance/stepDrafts";
import { ActionModal } from "./ActionModal";
import { ActionOperationDialog } from "./ActionOperationDialog";

const source = { ...guidanceStepDrafts.find(x => x.contentKey === "daily-task-action")!,
  id: 86, contentVersion: 1, state: "published", isCurrent: true };
const action = { id: 7, title: "Bordro kontrolü", source: "Bordro", owner: "İK", due: "Bugün",
  sourceRoute: "payroll", priority: "medium", status: "open", action: "Kaydı kontrol et", revision: "original" };
beforeEach(() => {
  localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: {
    id: "u", name: "İK", role: "hr-admin", roleLabel: "İK yöneticisi", initials: "İK", employeeId: 5,
  } }));
  stubApi({ "/api/actions": [action], "/api/actions/7": action, "/api/actions/7/status": { ...action, status: "week" },
    "/api/guidance/catalog": [source], "/api/guidance/catalog/versions/86": source,
    "/api/guidance/help-preferences": [], "/api/guidance/operations": [], "/api/guidance/practices": [] });
});
afterEach(() => vi.unstubAllGlobals());
const render = (node: React.ReactNode) => renderPage(<AuthProvider><ToastProvider>{node}</ToastProvider></AuthProvider>);
const chooseSource = async () => {
  await userEvent.click(screen.getByText("Gerçek işlemde kullandığım rehber sürümü"));
  await userEvent.click(await screen.findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
};

test("yeni aksiyon gerçek kayıt gövdesine seçilen güncel ders sürümünü taşır", async () => {
  const closed = vi.fn(); render(<ActionModal onClose={closed} />);
  await userEvent.type(screen.getByLabelText("Başlık"), "Yeni takip");
  await userEvent.type(screen.getByLabelText("Kaynak"), "Bordro");
  await userEvent.type(screen.getByLabelText("Sahip"), "İK");
  await chooseSource();
  await userEvent.click(screen.getByRole("button", { name: "Kaydet" }));
  await waitFor(() => expect(closed).toHaveBeenCalledOnce());
  const call = vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === "POST")!;
  expect(JSON.parse(String(call[1]?.body))).toMatchObject({ title: "Yeni takip", guidanceContentRevisionId: 86 });
});

test("409 düzeltme taslağını korur; kullanıcı güncel kaydı inceleyip yeniden teyit eder", async () => {
  const closed = vi.fn(); const originalFetch = fetch;
  let saves = 0;
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    if (String(url) === "/api/actions/7" && init?.method === "PUT") {
      saves++;
      return new Response(JSON.stringify(saves === 1 ? { title: "Kayıt değişti; güncel kaydı inceleyin." } : action),
        { status: saves === 1 ? 409 : 200, headers: { "Content-Type": "application/json" } });
    }
    if (String(url) === "/api/actions") return new Response(JSON.stringify([{ ...action, owner: "Başka kişi", revision: "current" }]),
      { headers: { "Content-Type": "application/json" } });
    return originalFetch(url, init);
  }));
  render(<ActionModal action={action} onClose={closed} />);
  await userEvent.clear(screen.getByLabelText("Sahip"));
  await userEvent.type(screen.getByLabelText("Sahip"), "Yeni sorumlu");
  await chooseSource();
  await userEvent.click(screen.getByRole("checkbox"));
  await userEvent.click(screen.getByRole("button", { name: "Kaydet" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Kayıt değişti");
  expect(screen.getByLabelText("Sahip")).toHaveValue("Yeni sorumlu");
  expect(screen.getByRole("checkbox")).not.toBeChecked();
  expect(closed).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole("button", { name: "Güncel aksiyonu yeniden incele" }));
  await waitFor(() => expect(screen.getByLabelText("İncelenen güncel aksiyon")).toHaveTextContent("Başka kişi"));
  expect(screen.getByLabelText("Sahip")).toHaveValue("Yeni sorumlu");
  expect(saves).toBe(1);
  await userEvent.click(screen.getByRole("checkbox"));
  await userEvent.click(screen.getByRole("button", { name: "Kaydet" }));
  await waitFor(() => expect(closed).toHaveBeenCalledOnce());
  const calls = vi.mocked(fetch).mock.calls.filter(([, init]) => init?.method === "PUT");
  expect(JSON.parse(String(calls[1][1]?.body))).toMatchObject({ owner: "Yeni sorumlu", expectedRevision: "current", guidanceContentRevisionId: 86 });
});

test.each(["status", "delete"] as const)("%s kaynak seçimi önceki teyidi düşürür; yalnız teyitten sonra yazım olur", async kind => {
  render(<ActionOperationDialog action={action} kind={kind} onClose={() => {}} />);
  const dialog = screen.getByRole("dialog");
  await userEvent.click(within(dialog).getByRole("checkbox"));
  await chooseSource();
  expect(within(dialog).getByRole("checkbox")).not.toBeChecked();
  const save = within(dialog).getByRole("button", { name: kind === "delete" ? "Takip kaydını sil" : "Durumu kaydet" });
  expect(save).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => ["PATCH", "DELETE"].includes(init?.method ?? ""))).toBe(false);
  await userEvent.click(within(dialog).getByRole("checkbox")); await userEvent.click(save);
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === (kind === "delete" ? "DELETE" : "PATCH"))).toBe(true));
  const call = vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === (kind === "delete" ? "DELETE" : "PATCH"))!;
  if (kind === "delete") {
    const url = new URL(String(call[0]), "http://test");
    expect(url.searchParams.get("expectedRevision")).toBe("original");
    expect(url.searchParams.get("guidanceContentRevisionId")).toBe("86");
  } else expect(JSON.parse(String(call[1]?.body))).toMatchObject({ status: "week", expectedRevision: "original", guidanceContentRevisionId: 86 });
});
