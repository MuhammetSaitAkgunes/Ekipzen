import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { guidanceStepDrafts } from "../guidance/stepDrafts";
import { SettingsPage } from "./SettingsPage";

const company = { name: "Örnek", logoPath: null, recordToken: "profile", logoRecordToken: "logo-original" };
const notifications = { newPersonnelEmail: true, leaveRequestEmail: true, weeklyReportEmail: false, recordToken: "notifications-original" };
const versions = ["company-company-profile", "company-departments"].map((key, index) => ({
  ...guidanceStepDrafts.find(x => x.contentKey === key)!, id: 61 + index, contentVersion: 1, state: "published", isCurrent: true,
}));
const response = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json" } });
beforeEach(() => {
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: { id: "owner", role: "hr-admin", roleLabel: "İK" } }));
  stubApi({ "/api/settings": { company, notifications, security: {}, subscription: {} },
    "/api/setup": { needsInitialization: false, hasEffectiveApprovedPayrollSettings: true },
    "/api/departments": [{ id: 3, name: "Ürün", code: "UR", employeeCount: 0 }],
    "/api/departments/3/deletion-preview": { id: 3, name: "Ürün", code: "UR", employeeCount: 0, positionCount: 0, canDelete: true, recordToken: "delete-original" },
    "/api/guidance/catalog": versions, "/api/guidance/catalog/versions/61": versions[0], "/api/guidance/catalog/versions/62": versions[1],
    "/api/guidance/help-preferences": [], "/api/guidance/operations": [],
    "/api/settings/notifications": { ...notifications, newPersonnelEmail: false },
    "/api/settings/company/logo": { logoPath: "logo/new.png" },
  });
});
afterEach(() => { vi.unstubAllGlobals(); localStorage.clear(); });
const render = () => renderPage(<AuthProvider><ToastProvider><SettingsPage /></ToastProvider></AuthProvider>);
const writes = (path: string, method: string) => vi.mocked(fetch).mock.calls.filter(([url, init]) => String(url).split("?")[0] === path && init?.method === method);
async function choose(label: string) {
  await userEvent.click(screen.getByText(label, { exact: true }));
  await userEvent.click(await within(screen.getByText(label, { exact: true }).closest("aside")!).findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
}

test("logo seçmek yazmaz; kaynak değişimi teyidi sıfırlar ve 409 dosya ile eski belirteci korur", async () => {
  const base = fetch;
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => init?.method === "POST" && String(url) === "/api/settings/company/logo"
    ? response({ title: "Logo siz incelerken değişti" }, 409) : base(url, init)));
  render(); await screen.findByLabelText("Şirket Adı");
  await userEvent.upload(screen.getByLabelText("Logo dosyası"), new File(["PNG"], "firma.png", { type: "image/png" }));
  expect(writes("/api/settings/company/logo", "POST")).toHaveLength(0);
  await userEvent.click(screen.getByLabelText("Logo değişimini teyit ediyorum"));
  await choose("Logo yüklerken kullandığım rehber sürümü");
  expect(screen.getByRole("button", { name: "Logoyu kaydet" })).toBeDisabled();
  await userEvent.click(screen.getByLabelText("Logo değişimini teyit ediyorum"));
  await userEvent.click(screen.getByRole("button", { name: "Logoyu kaydet" }));
  await screen.findByText("Logo siz incelerken değişti");
  expect(screen.getByText(/Yeni dosya: firma.png/)).toBeInTheDocument();
  expect(screen.getByLabelText("Logo değişimini teyit ediyorum")).not.toBeChecked();
  const form = writes("/api/settings/company/logo", "POST")[0][1]!.body as FormData;
  expect(form.get("expectedRecordToken")).toBe("logo-original"); expect(form.get("guidanceContentRevisionId")).toBe("61");
});

test("bildirim önizlemesi ayrı kaynak ve ilk belirteçle kaydedilir; hata taslağı korur", async () => {
  const base = fetch;
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => init?.method === "PUT" && String(url) === "/api/settings/notifications"
    ? response({ title: "Tercih siz incelerken değişti" }, 409) : base(url, init)));
  render(); await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: "Bildirimler" }));
  await userEvent.click(screen.getByLabelText("Yeni Personel Kaydı"));
  expect(writes("/api/settings/notifications", "PUT")).toHaveLength(0);
  await choose("Bildirim tercihinde kullandığım rehber sürümü");
  await userEvent.click(screen.getByLabelText("Bildirim tercihlerini teyit ediyorum"));
  await userEvent.click(screen.getByRole("button", { name: "Bildirim tercihlerini kaydet" }));
  await screen.findByText("Tercih siz incelerken değişti");
  expect(screen.getByLabelText("Yeni Personel Kaydı")).not.toBeChecked();
  expect(JSON.parse(String(writes("/api/settings/notifications", "PUT")[0][1]!.body))).toMatchObject({ expectedRecordToken: "notifications-original", guidanceContentRevisionId: 61, newPersonnelEmail: false, leaveRequestEmail: true, weeklyReportEmail: false });
  expect(screen.getByText(/e-posta gönderildiği veya teslim edildiği anlamına gelmez/)).toBeInTheDocument();
});

test("departman silme önizlemesi yazmaz; açık teyit ve kaynakla siler, çakışmada önizleme korunur", async () => {
  const base = fetch;
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => init?.method === "DELETE"
    ? response({ title: "Departman bağlantıları değişti" }, 409) : base(url, init)));
  render(); await screen.findByText("Ürün");
  await userEvent.click(screen.getByRole("button", { name: "Silme etkisini incele" }));
  await screen.findByRole("region", { name: "Departman silme önizlemesi" });
  expect(writes("/api/departments/3", "DELETE")).toHaveLength(0);
  await choose("Departman silerken kullandığım rehber sürümü");
  await userEvent.click(screen.getByLabelText("Bu departmanın silinmesini teyit ediyorum"));
  await userEvent.click(screen.getByRole("button", { name: "Departmanı sil" }));
  await screen.findByText("Departman bağlantıları değişti");
  const url = new URL(String(writes("/api/departments/3", "DELETE")[0][0]), "https://test.local");
  expect(url.searchParams.get("expectedRecordToken")).toBe("delete-original"); expect(url.searchParams.get("guidanceContentRevisionId")).toBe("62");
  expect(screen.getByLabelText("Bu departmanın silinmesini teyit ediyorum")).not.toBeChecked();
  expect(screen.getByRole("region", { name: "Departman silme önizlemesi" })).toBeInTheDocument();
});

test("bağlı pozisyon varken silme kapalıdır; erişim rehberi gerçek ekranlara gider ve yazmaz", async () => {
  const base = fetch;
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => String(url).endsWith("/deletion-preview")
    ? response({ id: 3, name: "Ürün", employeeCount: 0, positionCount: 1, canDelete: false, recordToken: "blocked" }) : base(url, init)));
  render(); await screen.findByText("Ürün");
  await userEvent.click(screen.getByRole("button", { name: "Silme etkisini incele" }));
  await screen.findByText(/1 pozisyon bağlı/);
  expect(screen.getByRole("button", { name: "Departmanı sil" })).toBeDisabled();
  await userEvent.click(screen.getByRole("button", { name: "Erişim hazırlığına git" }));
  const access = screen.getByRole("region", { name: "Erişimi hazırlayın ve doğrulayın" });
  expect(within(access).getByRole("link", { name: "Personel kartını açın" })).toHaveAttribute("href", "/app/personnel");
  expect(within(access).getByRole("link", { name: "İşe alım ekranında" })).toHaveAttribute("href", "/app/recruitment");
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true));
});
