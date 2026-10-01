import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { GuidanceCatalogPage } from "./GuidanceCatalogPage";
import type { GuidanceContent, GuidanceEditorial } from "./catalogQueries";

const content: GuidanceContent = {
  id: 8, contentKey: "personnel-check", processKey: "personnel", contentVersion: 1, state: "published", isCurrent: true,
  expertName: "İnceleyen uzman", expertRole: "İK uzmanı", reviewedOn: "2026-09-30", reviewDueOn: "2026-10-30", publishedAtUtc: "2026-09-30T12:00:00Z",
  body: { title: "Kartı doğrulama", audience: ["hr-admin", "employee"], learningGoal: "Doğru kişiyi seçmek", prerequisites: "Doğru kaydı bul",
    action: "Kartı kaynak belgeyle karşılaştır", why: "Aynı isimde iki kişi olabilir", example: "Belgeyle doğrulanmış anonim örnek",
    counterExample: "Yalnız isme güvenmek", terms: "Personel kartı: çalışanın kaydı", outcome: "Doğru kişiyi belirledik",
    completionEvidence: "İlgili işlemin sunucu sonucu", nextStep: "İlgili işlem ekranını aç", correction: "Kaydın durumuna göre düzelt",
    expertEscalation: "Çelişkide uzmanı bekle", sourceKind: "good-practice", sourceReference: "https://example.test/reference",
    provision: "Kontrol ilkesi", applicability: "Günlük standart işlem", effectiveFrom: "2026-09-30", effectiveTo: null },
};
const entry: GuidanceEditorial = {
  content: { ...content, state: "in-review", isCurrent: false }, revision: 3, basedOnId: null,
  contributors: [{ userId: "u", name: "İçeriği hazırlayan" }], expertReference: null, canPublish: false,
  events: [{ revision: 3, action: "submit", actorName: "İçeriği hazırlayan", reason: null, atUtc: "2026-09-30T12:00:00Z" }],
};
function setUser(role = "hr-admin") {
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R",
    user: { id: "u", name: "Yayın kullanıcısı", email: "u@example.test", role, roleLabel: role, initials: "YK", employeeId: null } }));
}
beforeEach(() => { localStorage.clear(); setUser(); });
afterEach(() => vi.unstubAllGlobals());
const renderCatalog = (path = "/app/guidance/catalog") => renderPage(<AuthProvider><GuidanceCatalogPage /></AuthProvider>, [path]);
const routes = (canManage = false, entries: GuidanceEditorial[] = [entry]) => ({
  "/api/guidance/catalog": [content], "/api/guidance/catalog/versions/8": content,
  "/api/guidance/catalog/access": { canManage }, "/api/guidance/catalog/editorial": entries,
});

test("çalışan incelenmiş gerekçe ve örnekleri okur; içerik yayınına yetki kazanmaz", async () => {
  setUser("employee"); stubApi(routes()); renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: /Kartı doğrulama/ }));
  expect(await screen.findByText("Aynı isimde iki kişi olabilir")).toBeInTheDocument();
  expect(screen.getByText("Belgeyle doğrulanmış anonim örnek")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "https://example.test/reference" })).toHaveAttribute("rel", "noopener noreferrer");
  expect(screen.getByText(/İnceleyen uzman/)).toBeInTheDocument();
  expect(screen.getByText(/Rehberi okumak bir işlemi tamamlamaz/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "İçerik yayınını yönet" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("eski sürüm açık bağlantıda kalır; güncel işlem rehberi gibi sunulmaz", async () => {
  stubApi({ ...routes(), "/api/guidance/catalog/versions/8": { ...content, isCurrent: false, state: "retired" } });
  renderCatalog("/app/guidance/catalog?version=8");
  expect(await screen.findByText(/Bu sürüm şu anda güncel rehber olarak kullanılmıyor/)).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "Kartı doğrulama" })).toBeInTheDocument();
});

test("boş veya erişilemeyen katalog yapılacak iş olmadığına çevrilmez", async () => {
  stubApi({ ...routes(), "/api/guidance/catalog": [] }); renderCatalog();
  expect(await screen.findByText(/Bu durum süreçte yapılacak iş olmadığı anlamına gelmez/)).toBeInTheDocument();
});

test("hazırlayan kullanıcı incelemedeki kendi sürümünde yayın düğmesi görmez", async () => {
  stubApi(routes(true)); renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: "İçerik yayınını yönet" }));
  await userEvent.click(await screen.findByRole("button", { name: /Kartı doğrulama.*İncelemede/ }));
  expect(screen.getByText(/başka bir yetkili Ekipzen İK yöneticisi yayımlamalı/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "İncelenen sürümü yayımla" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Gerekçeyle taslağa döndür" })).toBeDisabled();
});

test("başka yayıncı uzman inceleme bilgisi ve açık teyitle beklenen sürümü gönderir", async () => {
  const reviewed = { ...entry, canPublish: true };
  stubApi(routes(true, [reviewed]));
  const fetchMock = vi.mocked(fetch); const original = fetchMock.getMockImplementation()!;
  fetchMock.mockImplementation(async (input, init) => String(input).endsWith("/8/publish")
    ? new Response(JSON.stringify({ ...reviewed, content, revision: 4 }), { status: 200, headers: { "Content-Type": "application/json" } })
    : original(input, init));
  renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: "İçerik yayınını yönet" }));
  await userEvent.click(await screen.findByRole("button", { name: /Kartı doğrulama.*İncelemede/ }));
  await userEvent.type(screen.getByLabelText("Uzmanın adı"), "İnceleme uzmanı");
  await userEvent.type(screen.getByLabelText("Uzmanlık / rol"), "İş hukuku uzmanı");
  await userEvent.type(screen.getByLabelText("İnceleme belgesi referansı"), "UZMAN-2026-1");
  await userEvent.type(screen.getByLabelText("Yeniden inceleme tarihi"), "2026-10-30");
  await userEvent.click(screen.getByLabelText(/Uzman incelemesi bu içerik sürümünü/));
  await userEvent.click(screen.getByRole("button", { name: "İncelenen sürümü yayımla" }));
  await screen.findByText("İçerik durumu sunucuda kaydedildi.");
  const call = fetchMock.mock.calls.find(([url]) => String(url).endsWith("/8/publish"));
  expect(JSON.parse(String(call?.[1]?.body))).toMatchObject({ expectedRevision: 3,
    review: { name: "İnceleme uzmanı", reference: "UZMAN-2026-1", confirmed: true } });
});

test("taslak girişleri çakışmada korunur; değişmiş alanlar kaydedilmeden incelemeye gönderilmez", async () => {
  const draft: GuidanceEditorial = { ...entry, content: { ...entry.content, state: "draft" }, revision: 1 };
  stubApi(routes(true, [draft]));
  const fetchMock = vi.mocked(fetch); const original = fetchMock.getMockImplementation()!;
  fetchMock.mockImplementation(async (input, init) => String(input).endsWith("/editorial/8") && init?.method === "PUT"
    ? new Response(JSON.stringify({ title: "İçerik değişmiş; güncel sürümü yeniden inceleyin." }), { status: 409, headers: { "Content-Type": "application/json" } })
    : original(input, init));
  renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: "İçerik yayınını yönet" }));
  await userEvent.click(await screen.findByRole("button", { name: /Kartı doğrulama.*Taslak/ }));
  const why = screen.getByLabelText("Neden yapacağız?");
  await userEvent.clear(why); await userEvent.type(why, "Kaybedilmemesi gereken yeni açıklama");
  expect(screen.getByRole("button", { name: "Kaydedilmiş taslağı incelemeye gönder" })).toBeDisabled();
  await userEvent.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
  await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("İçerik değişmiş"));
  expect(why).toHaveValue("Kaybedilmemesi gereken yeni açıklama");
  expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith("/8/submit"))).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Güncel kaydı yeniden yükle" }));
  await screen.findByText("Güncel kayıt yüklendi; yeniden inceleyin.");
  await waitFor(() => expect(screen.getByLabelText("Neden yapacağız?")).toHaveValue(content.body.why));
});
