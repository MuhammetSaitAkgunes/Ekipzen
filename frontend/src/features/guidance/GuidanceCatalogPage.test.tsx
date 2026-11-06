import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { GuidanceCatalogPage } from "./GuidanceCatalogPage";
import type { GuidanceContent, GuidanceEditorial } from "./catalogQueries";

test.each([
  { button: "Bordro · Hesap ayarlarının kaynak ve sürümünü inceleme taslağını hazırla", key: "payroll-settings-review", step: "settings-review", kind: "good-practice", correct: "1" },
  { button: "Uyum ve belgeler · Doğrulanmış belge durumunu işleme taslağını hazırla", key: "compliance-status-review", step: "status-review", kind: "legislation", correct: "0" },
  { button: "İşten ayrılış · Ayrılışı tamamlayıp dış işlemi ayırma taslağını hazırla", key: "offboarding-completion", step: "completion", kind: "legislation", correct: "2" },
  { button: "Günlük yönetim · Ödeme ve bildirim kanıtlarını ayrı takip etme taslağını hazırla", key: "daily-external-followup", step: "external-followup", kind: "good-practice", correct: "2" },
])("$key içeriği ve sorusu bağımsız inceleme için hazırlanır; otomatik yayımlanmaz", async ({ button, key, step, kind, correct }) => {
  stubApi(routes(true, [])); renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: "İçerik yayınını yönet" }));
  await userEvent.click(screen.getByText("Alt adım ve alıştırma taslakları"));
  const prepare = await screen.findByRole("button", { name: button });
  await waitFor(() => expect(prepare).toBeEnabled());
  await userEvent.click(prepare);
  expect(screen.getByLabelText("İçerik kimliği")).toHaveValue(key);
  expect(screen.getByLabelText("Alt adım (isteğe bağlı)")).toHaveValue(step);
  expect(screen.getByLabelText("Dayanak türü")).toHaveValue(kind);
  expect(screen.getByLabelText("Doğru seçenek")).toHaveValue(correct);
  expect(screen.getByLabelText("Alıştırma sorusu")).not.toHaveValue("");
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("alt adım taslağı soru ve doğru yanıtı yayın incelemesine taşır; seçim otomatik kayıt yapmaz", async () => {
  stubApi(routes(true, [])); renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: "İçerik yayınını yönet" }));
  await userEvent.click(screen.getByText("Alt adım ve alıştırma taslakları"));
  const prepare = await screen.findByRole("button", { name: "İşe giriş · Kimlik ve temel bilgiler taslağını hazırla" });
  await waitFor(() => expect(prepare).toBeEnabled());
  await userEvent.click(prepare);
  expect(screen.getByLabelText("İçerik kimliği")).toHaveValue("personnel-entry-tab-kimlik");
  expect(screen.getByLabelText("Alt adım (isteğe bağlı)")).toHaveValue("tab-kimlik");
  expect(screen.getByLabelText("Doğru seçenek")).toHaveValue("1");
  expect(screen.getByLabelText("Alıştırma sorusu")).toHaveValue("İşe girişte zorunlu kimlik bilgisi henüz doğrulanmadı. Nasıl ilerlersiniz?");
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => init?.method === "POST" ? new Response(JSON.stringify({
    ...entry, revision: 1, content: { ...content, id: 9, state: "draft", isCurrent: false,
      ...JSON.parse(String(init.body)), body: JSON.parse(String(init.body)).body },
  })) : original(input, init));
  await userEvent.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
  await waitFor(() => expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(true));
  const call = vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === "POST")!;
  expect(JSON.parse(String(call[1]?.body))).toMatchObject({ contentKey: "personnel-entry-tab-kimlik", processKey: "personnel-entry",
    body: { stepKey: "tab-kimlik", practice: { correctChoice: 1, choices: ["Tahminî bilgi girerim", "Doğrulanmış bilgiyi ister, sonra kaydederim", "Başka çalışanın bilgisini kullanırım"] } } });
});

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
  "/api/guidance/catalog/readings": [],
});

test("çalışanın kendi okuma geçmişi yönetim rotası açılmadan erişilebilir kalır", async () => {
  setUser("employee"); stubApi(routes());
  renderCatalog("/app/guidance/catalog?tab=history");
  await screen.findByRole("heading", { name: "İncelediğim içerik sürümleri" });
  expect(screen.getByRole("link", { name: "Öğrenme geçmişim" })).toHaveAttribute("aria-current", "page");
  expect(screen.queryByRole("link", { name: "Başlangıç" })).not.toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).endsWith("/journey"))).toBe(false);
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("katalog araması açık sürüm bağlantısını ve okuma beyanını korur", async () => {
  stubApi(routes()); renderCatalog("/app/guidance/catalog?version=8");
  await screen.findByRole("heading", { name: content.body.title });
  await userEvent.type(screen.getByRole("searchbox", { name: "İçerikte ara" }), "eşleşmeyen konu");
  expect(screen.getByText("Aramanızla eşleşen içerik bulunamadı.")).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: content.body.title })).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("içerikten listeye dönüş aramayı ve klavye odağını korur; inceleme kaydı oluşturmaz", async () => {
  stubApi(routes()); renderCatalog();
  const search = screen.getByRole("searchbox", { name: "İçerikte ara" });
  await userEvent.type(search, "Kartı");
  await userEvent.selectOptions(screen.getByLabelText("Süreç filtresi"), "personnel");
  const entryButton = await screen.findByRole("button", { name: /Kartı doğrulama/ });
  await userEvent.click(entryButton);
  const reading = await screen.findByRole("region", { name: "Seçili rehber içeriği" });
  await waitFor(() => expect(reading).toHaveFocus());
  await userEvent.click(within(reading).getByRole("button", { name: /İçerik listesine dön/ }));
  expect(screen.queryByRole("region", { name: "Seçili rehber içeriği" })).not.toBeInTheDocument();
  expect(search).toHaveValue("Kartı");
  expect(screen.getByLabelText("Süreç filtresi")).toHaveValue("personnel");
  expect(entryButton).toHaveFocus();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("inceleme kaydı yalnız açık beyanla gönderilir ve tekrar okuma tarihi değiştirmez", async () => {
  const reading = { contentRevisionId: 8, contentKey: content.contentKey, processKey: "personnel", contentVersion: 1,
    title: content.body.title, firstReadAtUtc: "2026-10-01T08:00:00Z", isCurrent: true };
  stubApi({ ...routes(), "/api/guidance/catalog/versions/8/readings": reading });
  renderCatalog("/app/guidance/catalog?version=8");
  const region = await screen.findByRole("region", { name: "İçerik inceleme beyanı" });
  const save = within(region).getByRole("button", { name: "İnceleme beyanımı kaydet" });
  expect(save).toBeDisabled();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  await userEvent.click(within(region).getByRole("checkbox"));
  await waitFor(() => expect(save).toBeEnabled());
  await userEvent.click(save);
  expect(await within(region).findByText(/Bu sürümü incelediğinizi/)).toBeInTheDocument();
  const posts = vi.mocked(fetch).mock.calls.filter(([, init]) => init?.method === "POST");
  expect(posts).toHaveLength(1);
  expect(posts[0][0]).toBe("/api/guidance/catalog/versions/8/readings");
  expect(JSON.parse(String(posts[0][1]?.body))).toEqual({ confirmed: true });
  expect(within(region).queryByRole("button", { name: "İnceleme beyanımı kaydet" })).not.toBeInTheDocument();
});

test("kaynaklı başlangıç metni inceleme taslağını doldurur; kendiliğinden kaydetmez veya yayınlamaz", async () => {
  stubApi(routes(true)); renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: "İçerik yayınını yönet" }));
  await userEvent.click(screen.getByText("On süreç için kaynaklı başlangıç taslakları"));
  expect(screen.getAllByRole("button", { name: /başlangıç taslağını hazırla/ })).toHaveLength(10);
  await userEvent.click(screen.getByRole("button", { name: "Bordro başlangıç taslağını hazırla" }));
  expect(screen.getByLabelText("İçerik kimliği")).toHaveValue("payroll-ilk-kontrol");
  expect(screen.getByLabelText("İçerik başlığı")).toHaveValue("Bordro girdisini, sonucu ve dış ödemeyi ayırma");
  expect((screen.getByLabelText("Kaynak bağlantısı veya belge referansı") as HTMLTextAreaElement).value).toContain("csgb.gov.tr");
  expect(screen.getByText(/henüz uzman onaylı değildir/)).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "İşe giriş başlangıç taslağını hazırla" }));
  expect(screen.getByLabelText("İçerik kimliği")).toHaveValue("personnel-entry-ilk-kontrol");
  expect((screen.getByLabelText("Kaynak bağlantısı veya belge referansı") as HTMLTextAreaElement).value).toContain("sgk.gov.tr");
});

test("süreç bağlantısı ilgili içerik grubunu filtreler; eski sürüm kayıt ve işlem düğmesi sunmaz", async () => {
  stubApi({ ...routes(), "/api/guidance/catalog/versions/8": { ...content, isCurrent: false } });
  renderCatalog("/app/guidance/catalog?process=leave&version=8");
  expect(await screen.findByLabelText("Süreç filtresi")).toHaveValue("leave");
  expect(screen.queryByRole("button", { name: /Kartı doğrulama/ })).not.toBeInTheDocument();
  expect(await screen.findByText(/Güncel olmayan sürüm için yeni inceleme beyanı kaydedilmez/)).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "İnceleme beyanımı kaydet" })).not.toBeInTheDocument();
  expect(screen.queryByRole("link", { name: "İlgili süreç ekranını aç" })).not.toBeInTheDocument();
});

test("çalışan incelenmiş gerekçe ve örnekleri okur; içerik yayınına yetki kazanmaz", async () => {
  setUser("employee"); stubApi(routes()); renderCatalog();
  await userEvent.click(await screen.findByRole("button", { name: /Kartı doğrulama/ }));
  expect(await screen.findByText("Aynı isimde iki kişi olabilir")).toBeInTheDocument();
  expect(screen.getByText("Belgeyle doğrulanmış anonim örnek")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Bu adımın resmî veya tanımlı kaynağını aç" })).toHaveAttribute("rel", "noopener noreferrer");
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
