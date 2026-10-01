import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { GuidanceHelpControl, GuidanceHelpSettings } from "./GuidanceHelpControl";
import { GuidanceStepExplanation } from "./GuidanceStepExplanation";

const path = "/api/guidance/help-preferences";
const source = { processKey: "daily", isCurrent: true, body: { prerequisites: "Ön koşulu kontrol et", action: "Gerçek işlemi doğrula",
  why: "Kaynak durum belirler", example: "Uzun öğretici örnek", correction: "Yanlış sonucu düzelt", expertEscalation: "Belirsizliği uzmana ilet",
  sourceKind: "good-practice", provision: "Ürün iş akışı", applicability: "Kaynak kayıt kapsamı", sourceReference: "Ekipzen günlük iş sözleşmesi" } };
function setUser(role = "hr-admin", id = "u") {
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: { id, name: "Görevli", email: "u@example.test",
    role, roleLabel: role, initials: "G", employeeId: null } }));
}
beforeEach(() => { localStorage.clear(); setUser(); });
afterEach(() => vi.unstubAllGlobals());

test("kısa anlatım örneği açtırır; kritik açıklamalar kalır ve kaydetme yalnız kişisel tercihi değiştirir", async () => {
  stubApi({ [path]: [{ processKey: "daily", mode: "compact", revision: 1 }], "/api/guidance/catalog/versions/21": source });
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => init?.method === "PUT"
    ? new Response(JSON.stringify({ processKey: "daily", mode: "detailed", revision: 2 })) : original(input, init));
  const page = renderPage(<AuthProvider><GuidanceStepExplanation id={21} /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Kısa anlatım" })).toBeChecked());
  expect(screen.getByText("Uzun öğretici örnek")).not.toBeVisible();
  for (const text of ["Ön koşulu kontrol et", "Gerçek işlemi doğrula", "Kaynak durum belirler", "Yanlış sonucu düzelt", "Belirsizliği uzmana ilet"])
    expect(screen.getByText(text)).toBeVisible();
  await userEvent.click(screen.getByText("Ayrıntılı örneği aç"));
  expect(screen.getByText("Uzun öğretici örnek")).toBeVisible();
  expect(mock.mock.calls.some(([, init]) => init?.method === "PUT")).toBe(false);
  await userEvent.click(screen.getByRole("radio", { name: "Ayrıntılı anlatım" }));
  await screen.findByText("Anlatım tercihiniz kaydedildi.");
  const put = mock.mock.calls.find(([, init]) => init?.method === "PUT")!;
  expect(put[0]).toBe(`${path}/daily`);
  expect(JSON.parse(String(put[1]?.body))).toEqual({ mode: "detailed", expectedRevision: 1 });
  expect(screen.getByText("Uzun öğretici örnek")).toBeVisible();
  expect(mock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  page.unmount();
});

test("çakışan tercih gizlice uygulanmaz; güncel kayıt yeniden okunur", async () => {
  let current = [{ processKey: "daily", mode: "detailed", revision: 1 }];
  stubApi({ [path]: current }); const mock = vi.mocked(fetch);
  mock.mockImplementation(async (_input, init) => {
    if (init?.method === "PUT") { current = [{ processKey: "daily", mode: "compact", revision: 2 }]; return new Response(JSON.stringify({ detail: "Tercih değişmiş" }), { status: 409 }); }
    return new Response(JSON.stringify(current));
  });
  renderPage(<AuthProvider><GuidanceHelpControl process="daily" /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Kısa anlatım" })).toBeEnabled());
  await userEvent.click(screen.getByRole("radio", { name: "Kısa anlatım" }));
  await screen.findByRole("alert");
  expect(screen.getByRole("radio", { name: "Ayrıntılı anlatım" })).toBeChecked();
  await userEvent.click(screen.getByRole("button", { name: "Güncel tercihi yeniden yükle" }));
  await waitFor(() => expect(screen.getByRole("radio", { name: "Kısa anlatım" })).toBeChecked());
});

test("sunucuda saklanan tercih yeniden açılır; farklı kullanıcıya taşınmaz", async () => {
  stubApi({ [path]: [{ processKey: "daily", mode: "compact", revision: 1 }] });
  const first = renderPage(<AuthProvider><GuidanceHelpControl process="daily" /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Kısa anlatım" })).toBeChecked()); first.unmount();
  const reopened = renderPage(<AuthProvider><GuidanceHelpControl process="daily" /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Kısa anlatım" })).toBeChecked()); reopened.unmount();
  setUser("hr-admin", "other"); stubApi({ [path]: [] });
  renderPage(<AuthProvider><GuidanceHelpControl process="daily" /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Ayrıntılı anlatım" })).toBeEnabled());
  expect(screen.getByRole("radio", { name: "Ayrıntılı anlatım" })).toBeChecked();
});

test("tercih yüklenemezse ayrıntılı anlatım korunur; tercih paneli rol kapsamını gösterir", async () => {
  setUser("manager"); stubApi({});
  renderPage(<AuthProvider><GuidanceHelpSettings /></AuthProvider>);
  expect(fetch).not.toHaveBeenCalled();
  await userEvent.click(screen.getByText("Anlatım tercihimi düzenle"));
  expect(screen.queryByRole("option", { name: "Bordro" })).not.toBeInTheDocument();
  expect(screen.getByRole("option", { name: "Günlük yönetim" })).toBeInTheDocument();
  await screen.findByRole("alert");
  expect(screen.getByRole("radio", { name: "Ayrıntılı anlatım" })).toBeChecked();
  expect(screen.getByRole("radio", { name: "Kısa anlatım" })).toBeDisabled();
});

test.each([
  ["güncel doğru yanıt ve işlem", 21, "task-action", true, true, true],
  ["önceki sürümün işlemi", 20, "task-action", true, true, false],
  ["başka adımın işlemi", 21, "task-review", true, true, false],
  ["yanlış alıştırma yanıtı", 21, "task-action", false, true, false],
  ["geçmiş içerik", 21, "task-action", true, false, false],
  ["eksik işlem", 0, "task-action", true, true, false],
])("uyarlanan anlatım: %s", async (_label, operationId, stepKey, correct, current, compact) => {
  stubApi({
    [path]: [{ processKey: "daily", mode: "adaptive", revision: 1 }],
    "/api/guidance/catalog/versions/21": { ...source, id: 21, isCurrent: current, body: { ...source.body, stepKey: "task-action" } },
    "/api/guidance/catalog/practice-progress": [{ contentRevisionId: 21, processKey: "daily", stepKey: "task-action", lastCorrect: correct, isCurrent: true }],
    "/api/guidance/operations": operationId ? [{ isCurrent: true, evidence: { content: { contentRevisionId: operationId, processKey: "daily", stepKey } } }] : [],
  });
  renderPage(<AuthProvider><GuidanceStepExplanation id={21} /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Uyarlanan anlatım" })).toBeChecked());
  if (compact) {
    await screen.findByText(/kendi doğru alıştırma yanıtınız ve gerçek işlem kanıtınız var/);
    expect(screen.getByText("Uzun öğretici örnek")).not.toBeVisible();
    await userEvent.click(screen.getByText("Ayrıntılı örneği aç"));
  } else {
    await screen.findByText(/birlikte doğrulanmadı/);
  }
  expect(screen.getByText("Uzun öğretici örnek")).toBeVisible();
  for (const text of ["Ön koşulu kontrol et", "Yanlış sonucu düzelt", "Dayanak türü: İyi uygulama · Ürün iş akışı"])
    if (text.startsWith("Dayanak")) expect(screen.getByText(/Dayanak türü:/)).toBeVisible();
    else expect(screen.getByText(text)).toBeVisible();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method && init.method !== "GET")).toBe(false);
});

test("uyarlanan anlatım kanıt okunamadığında örneği açık tutar", async () => {
  stubApi({ [path]: [{ processKey: "daily", mode: "adaptive", revision: 1 }],
    "/api/guidance/catalog/versions/21": { ...source, body: { ...source.body, stepKey: "task-action" } },
    "/api/guidance/catalog/practice-progress": [], });
  renderPage(<AuthProvider><GuidanceStepExplanation id={21} /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Uyarlanan anlatım" })).toBeChecked());
  await waitFor(() => expect(fetch).toHaveBeenCalledWith("/api/guidance/operations", expect.anything()));
  expect(screen.getByText("Uzun öğretici örnek")).toBeVisible();
});

test("uyarlanan anlatım seçimi kişisel tercih olarak kaydedilir", async () => {
  stubApi({ [path]: [] }); const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => init?.method === "PUT"
    ? new Response(JSON.stringify({ processKey: "daily", mode: "adaptive", revision: 1 })) : original(input, init));
  renderPage(<AuthProvider><GuidanceHelpControl process="daily" /></AuthProvider>);
  await waitFor(() => expect(screen.getByRole("radio", { name: "Uyarlanan anlatım" })).toBeEnabled());
  await userEvent.click(screen.getByRole("radio", { name: "Uyarlanan anlatım" }));
  await screen.findByText("Anlatım tercihiniz kaydedildi.");
  expect(JSON.parse(String(mock.mock.calls.find(([, init]) => init?.method === "PUT")![1]?.body)))
    .toEqual({ mode: "adaptive", expectedRevision: 0 });
});
