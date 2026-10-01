import { screen, within } from "@testing-library/react";
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
  { process: "payroll", title: "Bordro", first: "Hesap ayarlarının kaynak ve sürümünü inceleme", step: "settings-review", count: 6 },
  { process: "compliance", title: "Uyum ve belgeler", first: "Gerçek belgeyi ve takip kaydını inceleme", step: "record-review", count: 3 },
  { process: "offboarding", title: "İşten ayrılış", first: "Ayrılış dosyasını hazırlama", step: "preparation", count: 5 },
])("$title adımları gerçek işlem yapmadan sırayla açılır", async ({ process, title, first, step, count }) => {
  stubApi({ [`/api/guidance/catalog/steps/${process}/${step}`]: null });
  renderPage(<AuthProvider><GuidanceStepLessons process={process} /></AuthProvider>);
  expect(fetch).not.toHaveBeenCalled();
  await userEvent.click(screen.getByText(`${title} · Adım adım öğren`));
  const list = screen.getByRole("list");
  const headings = within(list).getAllByRole("heading");
  expect(headings).toHaveLength(count); expect(headings[0]).toHaveTextContent(first);
  expect(fetch).not.toHaveBeenCalled();
  await userEvent.click(within(list).getAllByText("Kaynaklı adım ve sürümlü alıştırma")[0]);
  await screen.findByText(/henüz yayımlanmadı/);
  expect(vi.mocked(fetch).mock.calls).toHaveLength(1);
  expect(vi.mocked(fetch).mock.calls[0][0]).toBe(`/api/guidance/catalog/steps/${process}/${step}`);
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("ekip yöneticisi yalnız uyum incelemesini görür; bordro/ayrılış işlemi ve uyum değişikliği önerilmez", async () => {
  setRole("manager"); stubApi({});
  renderPage(<AuthProvider><GuidanceStepLessons process="compliance" /><GuidanceStepLessons process="payroll" /><GuidanceStepLessons process="offboarding" /></AuthProvider>);
  await userEvent.click(screen.getByText("Uyum ve belgeler · Adım adım öğren"));
  expect(screen.getAllByRole("heading")).toHaveLength(1);
  expect(screen.getByText("Gerçek belgeyi ve takip kaydını inceleme")).toBeInTheDocument();
  expect(screen.queryByText("Doğrulanmış belge durumunu işleme")).not.toBeInTheDocument();
  expect(screen.queryByText("Belge bilgisi ve sorumlu hatasını düzeltme")).not.toBeInTheDocument();
  expect(screen.queryByText("Bordro · Adım adım öğren")).not.toBeInTheDocument();
  expect(screen.queryByText("İşten ayrılış · Adım adım öğren")).not.toBeInTheDocument();
  expect(fetch).not.toHaveBeenCalled();
});

test("şirket rehberi sırayı gösterir; adımı açmak kurulum veya kayıt işlemi göndermez", async () => {
  stubApi({ "/api/guidance/catalog/steps/company/initial-setup": null });
  renderPage(<AuthProvider><GuidanceStepLessons process="company" /></AuthProvider>);
  expect(fetch).not.toHaveBeenCalled();
  await userEvent.click(screen.getByText("Şirket hazırlığı · Adım adım öğren"));
  const list = screen.getByRole("list");
  expect(within(list).getAllByRole("heading").map(item => item.textContent)).toEqual([
    "Başlangıç kayıtlarını inceleme", "Şirket profilini doğrulama", "Departman hazırlığı", "Erişim sorumlularını belirleme",
  ]);
  expect(fetch).not.toHaveBeenCalled();
  await userEvent.click(within(list).getAllByText("Kaynaklı adım ve sürümlü alıştırma")[0]);
  await screen.findByText(/henüz yayımlanmadı/);
  expect(vi.mocked(fetch).mock.calls).toHaveLength(1);
  expect(vi.mocked(fetch).mock.calls[0][0]).toBe("/api/guidance/catalog/steps/company/initial-setup");
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("ekip yöneticisinde yalnız personel incelemesi açılır; içe aktarma ve işe dönüş önerilmez", async () => {
  setRole("manager"); stubApi({});
  renderPage(<AuthProvider><GuidanceStepLessons process="personnel" /></AuthProvider>);
  await userEvent.click(screen.getByText("Personel yönetimi · Adım adım öğren"));
  expect(screen.getByRole("heading", { name: "Doğru çalışanı bulma ve bilgiyi kontrol etme" })).toBeInTheDocument();
  expect(screen.queryByText("İçe aktarma önizlemesi ve sonuç kontrolü")).not.toBeInTheDocument();
  expect(screen.queryByText("İşe dönüşü ayrı dönem olarak hazırlama")).not.toBeInTheDocument();
  expect(fetch).not.toHaveBeenCalled();
});

test("puantajda yöneticinin üç adımı görünür; çalışan yönetici rehberine erişmez", async () => {
  setRole("manager"); stubApi({});
  const page = renderPage(<AuthProvider><GuidanceStepLessons process="attendance" /></AuthProvider>);
  await userEvent.click(screen.getByText("Mesai ve puantaj · Adım adım öğren"));
  expect(screen.getAllByRole("heading")).toHaveLength(3);
  expect(screen.getByText("Aylık özet ve bordro etkisini kontrol etme")).toBeInTheDocument();
  page.unmount(); setRole("employee");
  renderPage(<AuthProvider><GuidanceStepLessons process="attendance" /></AuthProvider>);
  expect(screen.queryByText("Mesai ve puantaj · Adım adım öğren")).not.toBeInTheDocument();
  expect(fetch).not.toHaveBeenCalled();
});
