import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { ImportModal } from "./ImportModal";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { GuidanceOperationJourney } from "../guidance/GuidanceOperationJourney";
import { guidanceStepDrafts } from "../guidance/stepDrafts";

const token = "A".repeat(64);
const ready = { satirNo: 2, adSoyad: "Ayşe Demir", unvan: "Analist", departman: "Yazılım", iseGirisTarihi: "2026-03-01", durum: "ready", aciklama: "Aktif personel oluşturulacak.", sorunlar: [] };
const bad = { ...ready, satirNo: 3, adSoyad: "Eksik Kişi", durum: "invalid", aciklama: "Hatalı satır atlanacak.", sorunlar: [{ satirNo: 3, alan: "Ad", mesaj: "Zorunlu alan." }] };
const duplicate = { ...ready, satirNo: 5, adSoyad: "Mevcut Kişi", durum: "duplicate", aciklama: "Mevcut kayıt değişmez." };
const rapor = { toplamSatir: 3, gecerliSatir: 1, hataliSatir: 1, mukerrerSatir: 1, bilinmeyenDepartmanlar: [], sorunlar: bad.sorunlar, satirlar: [ready, bad, duplicate], previewToken: token };
const response = (data: unknown, status = 200) => new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json" } });
const result = (id: string) => ({ olusturulanSatir: 1, atlananSatir: 2, sorunlar: bad.sorunlar, importRequestId: id,
  satirlar: [{ ...ready, durum: "created", employeeId: 42 }, bad, duplicate] });
const mockNormal = () => vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) =>
  response(String(url) === "/api/employees/import" ? result((init!.body as FormData).get("importRequestId") as string) : rapor)));
beforeEach(() => { mockNormal(); localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R",
  user: { id: "u", name: "İK", role: "hr-admin", roleLabel: "İK", initials: "İK" } })); });
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); });
const render = () => renderPage(<AuthProvider><ToastProvider><ImportModal open onClose={() => {}} /></ToastProvider></AuthProvider>);
const dosyaSec = async (name = "personel.xlsx") => userEvent.upload(screen.getByLabelText(/Excel dosyası/i), new File(["x"], name, { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" }));
const importCalls = () => vi.mocked(fetch).mock.calls.filter(([url, init]) => String(url) === "/api/employees/import" && init?.method === "POST");
const approve = async () => {
  await dosyaSec();
  await waitFor(() => expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Şablondaki örnek/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Tüm aktarılacak/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Hatalı ve mükerrer/ }));
};

const sourceSnapshot = { contentRevisionId: 22, contentKey: "personnel-import-review", processKey: "personnel", stepKey: "import-review",
  contentVersion: 1, title: "Aktarım dersi" };
const importLesson = { id: 22, contentKey: sourceSnapshot.contentKey, processKey: "personnel", contentVersion: 1,
  state: "published", isCurrent: true, body: guidanceStepDrafts.find(x => x.contentKey === sourceSnapshot.contentKey)!.body };
const chooseLesson = async () => {
  await userEvent.click(screen.getByText("Toplu aktarımda kullandığım ders sürümü"));
  await userEvent.click(await screen.findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
  await screen.findByRole("region", { name: "Seçilen adımın açıklaması" });
};
const recheck = async () => {
  for (const name of [/Şablondaki örnek/, /Tüm aktarılacak/, /Hatalı ve mükerrer/]) await userEvent.click(screen.getByRole("checkbox", { name }));
};
const learningRoutes = (importResponse?: (init: RequestInit) => Response | Promise<Response>, savedResponse?: () => Response,
  operations: () => unknown[] = () => []) => vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url === "/api/employees/import") return importResponse ? importResponse(init!)
      : response({ ...result((init!.body as FormData).get("importRequestId") as string), guidanceContent: sourceSnapshot });
    if (url.startsWith("/api/employees/import/results/")) return savedResponse?.() ?? new Response(null, { status: 204 });
    if (url.startsWith("/api/guidance/catalog/versions/")) return response(importLesson);
    if (url.startsWith("/api/guidance/catalog")) return response([importLesson]);
    if (url === "/api/guidance/operations") return response(operations());
    if (url === "/api/guidance/help-preferences") return response([]);
    return response(rapor);
  }));

test("aktarım dersini seçmek kayıt yapmaz; tercihleri yeniden teyit edip sürümü yalnız son istekte gönderir", async () => {
  learningRoutes(); render(); await approve(); await chooseLesson();
  expect(importCalls()).toHaveLength(0);
  expect(screen.getByRole("checkbox", { name: /Şablondaki örnek/ })).not.toBeChecked();
  expect(screen.getByRole("button", { name: "Kontrol ettim, aktar" })).toBeDisabled();
  expect(screen.getByRole("link", { name: "Seçtiğim aktarım dersini ve alıştırmasını aç" })).toHaveAttribute("href", "/app/guidance/catalog?version=22");
  expect(screen.getByText(/Kayıtlı sonucu sorgula veya Aynı gönderimi tekrar dene kullanın/)).toBeInTheDocument();
  await recheck(); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  await screen.findByText("Gerçek aktarım sonucu");
  expect((importCalls()[0][1]!.body as FormData).get("guidanceContentRevisionId")).toBe("22");
  expect(screen.getByText(/tek gerçek işlem kanıtıdır/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "İşlemde kullanılan aktarım dersini aç" })).toHaveAttribute("href", "/app/guidance/catalog?version=22");
});

test("belirsiz yanıtta ders seçimi sabittir; makbuz kurtarması kişisel kanıt listesini yeniden okur", async () => {
  let saved: ReturnType<typeof result> & { guidanceContent: typeof sourceSnapshot };
  learningRoutes(init => { saved = { ...result((init.body as FormData).get("importRequestId") as string), guidanceContent: sourceSnapshot };
    return response({ title: "Yanıt kayboldu" }, 500); }, () => response(saved), () => saved ? [{ isCurrent: false, evidence: {
      evidenceKey: "employee-import:1", operationKey: "employee-import", resourceId: 1, outcome: "imported", occurredAtUtc: "2026-10-02T06:00:00Z",
      content: sourceSnapshot, importChanges: { importRequestId: saved.importRequestId, createdCount: 1, skippedCount: 2, createdEmployeeIds: [42],
        skippedRows: [{ rowNumber: 3, outcome: "invalid", explanation: "Hatalı; atlandı", issues: ["Ad: Zorunlu alan."] }] } } }] : []);
  renderPage(<AuthProvider><ToastProvider><ImportModal open onClose={() => {}} /><GuidanceOperationJourney /></ToastProvider></AuthProvider>);
  await approve(); await chooseLesson(); await recheck(); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  await screen.findByText(/Yanıt kayboldu/);
  expect(screen.getByRole("button", { name: "Rehber sürümü bağlantısını kaldır" })).toBeDisabled();
  await userEvent.click(screen.getByRole("button", { name: "Kayıtlı sonucu sorgula" }));
  await screen.findByText("Gerçek aktarım sonucu");
  expect(await screen.findByRole("region", { name: "Aktarım kanıtının gerçek sonucu" })).toHaveTextContent("1 personel oluşturuldu · 2 satır atlandı");
  expect(screen.getByRole("link", { name: "Çalışan #42 kartını aç" })).toHaveAttribute("href", "/app/personnel/42");
  expect(within(screen.getByRole("region", { name: "Aktarım kanıtının gerçek sonucu" })).getByText(/Ad: Zorunlu alan/)).toBeInTheDocument();
  expect(importCalls()).toHaveLength(1);
});

test("yeni dosya eski ders seçimini ve tüm tercihleri bırakır", async () => {
  learningRoutes(); render(); await approve(); await chooseLesson(); await recheck();
  await dosyaSec("yalniz-eksikler.xlsx");
  await waitFor(() => expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  expect(screen.queryByRole("link", { name: "Seçtiğim aktarım dersini ve alıştırmasını aç" })).not.toBeInTheDocument();
  expect(screen.getByRole("checkbox", { name: /Şablondaki örnek/ })).not.toBeChecked();
  expect(screen.getByRole("button", { name: "Kontrol ettim, aktar" })).toBeDisabled();
});

test("tutarlı satırlar yanlış ders sürümünü başarı yapmaz; aynı gönderim ve kaynakla tekrar mümkündür", async () => {
  let attempts = 0;
  learningRoutes(init => response({ ...result((init.body as FormData).get("importRequestId") as string),
    guidanceContent: { ...sourceSnapshot, contentRevisionId: ++attempts === 1 ? 23 : 22 } }));
  render(); await approve(); await chooseLesson(); await recheck(); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Aktarım sonucu doğrulanamadı");
  expect(screen.queryByText("Gerçek aktarım sonucu")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Aynı gönderimi tekrar dene" }));
  await screen.findByText("Gerçek aktarım sonucu");
  const bodies = importCalls().map(([, init]) => init!.body as FormData);
  expect(bodies).toHaveLength(2);
  expect(bodies[0].get("importRequestId")).toBe(bodies[1].get("importRequestId"));
  expect(bodies.every(body => body.get("guidanceContentRevisionId") === "22")).toBe(true);
});

test("önizleme kayıt açmadan her kişiyi, Excel satırını ve atlama nedenini gösterir", async () => {
  render(); await dosyaSec();
  expect(await screen.findByText("Ayşe Demir")).toBeInTheDocument();
  expect(screen.getByText("Mevcut kayıt değişmez.")).toBeInTheDocument();
  expect(screen.getByText(/aktarılan herkes belirsiz süreli/)).toBeInTheDocument();
  expect(screen.getByText(/Yalnız 1 geçerli satır aktarılır/)).toBeInTheDocument();
  expect(importCalls()).toHaveLength(0);
});

test("dosya olmadan veya geçerli satır olmadan kayıt açılamaz", async () => {
  stubApi({ "/api/employees/import/preview": { ...rapor, gecerliSatir: 0, hataliSatir: 2, satirlar: [{ ...bad, satirNo: 2 }, bad, duplicate] } });
  render(); expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeDisabled();
  await dosyaSec(); expect(await screen.findByText(/Aktarılabilir satır yok/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeDisabled();
});

test("kısmi aktarım üç doğrulama, önizleme belirteci ve sabit gönderim kimliğiyle çalışır", async () => {
  render(); await dosyaSec();
  await waitFor(() => expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  const confirm = screen.getByRole("button", { name: "Kontrol ettim, aktar" }); expect(confirm).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox", { name: /Şablondaki örnek/ }));
  await userEvent.click(screen.getByRole("checkbox", { name: /Tüm aktarılacak/ })); expect(confirm).toBeDisabled();
  await userEvent.click(screen.getByRole("checkbox", { name: /Hatalı ve mükerrer/ })); await userEvent.click(confirm);
  expect(await screen.findByText("Gerçek aktarım sonucu")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Çalışan kartını aç" })).toHaveAttribute("href", "/app/personnel/42");
  const body = importCalls()[0][1]!.body as FormData;
  expect(body.get("expectedPreviewToken")).toBe(token);
  expect(body.get("importRequestId")).toMatch(/^[a-f0-9-]{36}$/);
  expect(body.get("file")).toBeInstanceOf(File);
});

test("dosya yeniden seçilince eski inceleme ve onaylar geçersiz olur", async () => {
  render(); await approve(); await dosyaSec("duzeltilmis.xlsx");
  expect(screen.queryByRole("region", { name: "Aktarmadan önce kontrol edin" })).not.toBeInTheDocument();
  await waitFor(() => expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeEnabled());
  await userEvent.click(screen.getByRole("button", { name: "Aktarımı gözden geçir" }));
  expect(screen.getByRole("checkbox", { name: /Şablondaki örnek/ })).not.toBeChecked();
  expect(screen.getByRole("button", { name: "Kontrol ettim, aktar" })).toBeDisabled();
});

test("bağlantı hatasında dosya değişmez ve aynı kimlikle güvenli tekrar yapılır", async () => {
  let attempts = 0;
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
    if (String(url) !== "/api/employees/import") return response(rapor);
    if (++attempts === 1) return response({ title: "Sunucu hatası" }, 500);
    return response(result((init!.body as FormData).get("importRequestId") as string));
  }));
  render(); await approve(); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Aktarım sonucu doğrulanamadı");
  expect(screen.getByLabelText(/Excel dosyası/i)).toBeDisabled(); expect(screen.getByRole("button", { name: "Kapat" })).toBeDisabled();
  await userEvent.click(screen.getByRole("button", { name: "Aynı gönderimi tekrar dene" }));
  expect(await screen.findByText("Gerçek aktarım sonucu")).toBeInTheDocument();
  const [first, second] = importCalls().map(([, init]) => init!.body as FormData);
  expect(first.get("importRequestId")).toBe(second.get("importRequestId")); expect(first.get("expectedPreviewToken")).toBe(second.get("expectedPreviewToken"));
});

test("kayıtlı sonucu sorgulamak dosyayı tekrar göndermeden çalışan bağlantılarını kurtarır", async () => {
  let key = "";
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
    if (String(url) === "/api/employees/import") { key = (init!.body as FormData).get("importRequestId") as string; throw new TypeError("network"); }
    if (String(url).includes("/import/results/")) return response(result(key));
    return response(rapor);
  }));
  render(); await approve(); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  await userEvent.click(await screen.findByRole("button", { name: "Kayıtlı sonucu sorgula" }));
  expect(await screen.findByText("Gerçek aktarım sonucu")).toBeInTheDocument(); expect(importCalls()).toHaveLength(1);
});

test("kaynak değişikliğinde yeni önizleme ister ve önceki onayla kaydetmez", async () => {
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL) => {
    if (String(url) === "/api/employees/import") return response({ title: "Kaynak değişti; kayıt oluşturulmadı." }, 409);
    if (String(url).includes("/import/results/")) return new Response(null, { status: 204 });
    return response(rapor);
  }));
  render(); await approve(); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Dosyayı yeniden seçin");
  expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeDisabled();
  expect(screen.getByLabelText(/Excel dosyası/i)).toBeEnabled(); expect(importCalls()).toHaveLength(1);
});

test("tutarsız başarılı yanıt gerçek sonuç diye gösterilmez", async () => {
  vi.stubGlobal("fetch", vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => response(String(url) === "/api/employees/import"
    ? { ...result((init!.body as FormData).get("importRequestId") as string), olusturulanSatir: 2 } : rapor)));
  render(); await approve(); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, aktar" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Aktarım sonucu doğrulanamadı");
  expect(screen.queryByText("Gerçek aktarım sonucu")).not.toBeInTheDocument();
});

test("sayıları tam ama satır veya belirteci eksik önizleme kaydı durdurur", async () => {
  stubApi({ "/api/employees/import/preview": { ...rapor, satirlar: undefined } });
  render(); await dosyaSec(); expect(await screen.findByRole("alert")).toHaveTextContent("Önizleme raporu eksik");
  expect(screen.getByRole("button", { name: "Aktarımı gözden geçir" })).toBeDisabled(); expect(importCalls()).toHaveLength(0);
});

test("büyük dosyalarda 25 satırlık sayfalama ve işlem filtresi kullanılır", async () => {
  const rows = Array.from({ length: 30 }, (_, i) => ({ ...ready, satirNo: i + 2, adSoyad: "Kişi " + i }));
  stubApi({ "/api/employees/import/preview": { ...rapor, toplamSatir: 30, gecerliSatir: 30, hataliSatir: 0, mukerrerSatir: 0, sorunlar: [], satirlar: rows } });
  render(); await dosyaSec(); expect(await screen.findByText("Kişi 0")).toBeInTheDocument(); expect(screen.queryByText("Kişi 25")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: "Sonraki satırlar" })); expect(screen.getByText("Kişi 25")).toBeInTheDocument();
  await userEvent.selectOptions(screen.getByRole("combobox", { name: /Gösterilecek satırlar/ }), "invalid"); expect(screen.getByText("Bu grupta satır yok.")).toBeInTheDocument();
});
