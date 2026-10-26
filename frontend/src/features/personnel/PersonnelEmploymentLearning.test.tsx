import { useQueryClient } from "@tanstack/react-query";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { GuidanceOperationJourney } from "../guidance/GuidanceOperationJourney";
import { CompensationCorrectionPanel } from "./CompensationCorrectionPanel";
import { PersonnelModal } from "./PersonnelModal";

const detail = { id: 1, firstName: "Ada", lastName: "Kayıt", name: "Ada Kayıt", title: "Uzman", initials: "AK",
  status: "active", hireDate: "2026-01-01", contractType: "indefinite", departmentId: 1, department: "İK",
  managerId: null, nationalId: null, recordToken: "A".repeat(64), profile: { employmentType: "Tam Zamanlı" }, documents: [] };
const salary = { id: 2, version: 1, employmentPeriodId: 1, effectiveFrom: "2026-09-01", effectiveTo: null,
  monthlyGrossSalary: 50000, regularPremium: 0, roadAllowance: 0, mealAllowance: 0, otherRegularBenefits: 0,
  monthlyRecurringTotal: 50000, sourceReference: "İlk karar" };
const source = (process: string, step: string) => ({ id: 22, contentKey: `${process}-${step}`, processKey: process, contentVersion: 1,
  isCurrent: true, body: { title: "Test işlem kaynağı", stepKey: step, action: "Kaydı kontrol et", prerequisites: "Yetkiyi kontrol et",
    why: "Gerçek kayıt doğrulaması", example: "Örnek", correction: "Gerekçeli düzelt", expertEscalation: "Uzmana ilet",
    sourceKind: "good-practice", sourceReference: "Test", provision: "Kayıt güvenliği", applicability: "Standart işlem" } });
const foundation = { employeeId: 1, contractType: "indefinite",
  employmentPeriods: [{ id: 1, startDate: "2026-01-01", endDate: null, contractType: "indefinite" }], compensationPackages: [] };
const routes = (process = "personnel-entry", step = "tab-mali") => ({
  "/api/guidance/catalog": [source(process, step)], "/api/guidance/catalog/versions/22": source(process, step),
  "/api/guidance/help-preferences": [], "/api/employees/1": detail,
  "/api/departments": [{ id: 1, name: "İK" }], "/api/employees/manager-choices": [],
  "/api/employees/1/personnel-history": [], "/api/employees/1/employment-foundation": foundation,
  "/api/employees/1/compensation-packages": foundation, "/api/employees/1/compensation-packages/2": foundation,
  "/api/employees/1/compensation-packages/2/correction-context": { package: salary, canCorrect: true, blockingReasons: [], history: [] },
  "/api/employees/1/compensation-packages/2/date-correction-preview": { package: salary, previousPackage: null,
    proposedEffectiveFrom: "2026-10-01", proposedPreviousEffectiveTo: null, canApply: true, blockingReasons: [], history: [] },
  "/api/employees/1/compensation-packages/2/effective-date": foundation,
});
function render(element: React.ReactNode) { return renderPage(<AuthProvider><ToastProvider>{element}</ToastProvider></AuthProvider>); }
async function chooseSource(label: string) {
  const panel = screen.getByText(label).closest("aside")!;
  await userEvent.click(await within(panel).findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
  await within(panel).findByRole("region", { name: "Seçilen adımın açıklaması" });
}
const requests = (method: string, path: string) => vi.mocked(fetch).mock.calls.filter(([url, init]) => String(url) === path && init?.method === method);
const body = (method: string, path: string) => JSON.parse(String(requests(method, path).at(-1)?.[1]?.body));
beforeEach(() => {
  localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: {
    id: "u", name: "İK", email: "u@example.test", role: "hr-admin", roleLabel: "İK", initials: "İK", employeeId: null } }));
});
afterEach(() => vi.unstubAllGlobals());

async function prepareRehire() {
  await screen.findByText("Personel Kartı — Ada Kayıt");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.type(screen.getByLabelText("İşe dönüş tarihi"), "2026-10-01");
  await userEvent.selectOptions(screen.getByLabelText("Yeni sözleşme türü"), "fixed-term");
  await userEvent.type(screen.getByLabelText("Yeniden işe alma gerekçesi / karar referansı"), "Yeni sözleşme kararı");
}
const rehireRoutes = () => ({ ...routes("personnel", "rehire"), "/api/employees/1": { ...detail, status: "passive" },
  "/api/employees/1/employment-foundation": { ...foundation,
    employmentPeriods: [{ ...foundation.employmentPeriods[0], endDate: "2026-09-30" }] }, "/api/employees/1/rehire": foundation });

test("rehberli işe dönüş ön kontrolü yazmaz; onayda yeni sözleşme, gerekçe ve ayrı kaynak gönderilir", async () => {
  stubApi(rehireRoutes()); const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => String(input) === "/api/employees/1/rehire" && init?.method === "POST"
    ? new Response(JSON.stringify({ title: "Başka işlem yeni dönemi açtı" }), { status: 409 }) : original(input, init));
  render(<PersonnelModal employeeId={1} readOnly={false} onClose={() => {}} />); await prepareRehire();
  await chooseSource("Yeniden işe alma işleminde kullandığım rehber sürümü");
  await userEvent.click(screen.getByRole("button", { name: "İşe dönüşü kontrol et" }));
  expect(screen.getByRole("region", { name: "Yeniden işe alma ön kontrolü" })).toHaveTextContent("2026-10-01");
  expect(requests("POST", "/api/employees/1/rehire")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button", { name: "Yeni çalışma dönemini başlat" }));
  await screen.findByText("Başka işlem yeni dönemi açtı");
  expect(body("POST", "/api/employees/1/rehire")).toEqual({ rehireDate: "2026-10-01", contractType: "fixed-term",
    reason: "Yeni sözleşme kararı", guidanceContentRevisionId: 22 });
  expect(screen.getByLabelText("Yeniden işe alma gerekçesi / karar referansı")).toHaveValue("Yeni sözleşme kararı");
  expect(screen.queryByRole("button", { name: "Yeni çalışma dönemini başlat" })).not.toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Rehber sürümü bağlantısını kaldır" })).toBeInTheDocument();
});

test("karttaki kaydedilmemiş düzeltme ayrı yeniden işe alma işlemiyle kaybolmaz", async () => {
  stubApi(rehireRoutes()); render(<PersonnelModal employeeId={1} readOnly={false} onClose={() => {}} />);
  await screen.findByText("Personel Kartı — Ada Kayıt"); await userEvent.type(screen.getByLabelText("Adı"), " Taslak");
  await prepareRehire(); await userEvent.click(screen.getByRole("button", { name: "İşe dönüşü kontrol et" }));
  expect(await screen.findByText(/Kartta kaydedilmemiş değişiklik var/)).toBeInTheDocument();
  expect(requests("POST", "/api/employees/1/rehire")).toHaveLength(0);
  expect(screen.getByLabelText("Adı")).toHaveValue("Ada Taslak");
});

test("ücret eklemenin kaynak bağlantısı kart düzeltmesinden ayrıdır ve yalnız son onayda gönderilir", async () => {
  stubApi(routes()); render(<PersonnelModal employeeId={1} readOnly={false} onClose={() => {}} />);
  await screen.findByText("Personel Kartı — Ada Kayıt"); await userEvent.click(screen.getByRole("button", { name: /Mali Bilgiler/ }));
  await userEvent.type(screen.getByLabelText("Yürürlük başlangıcı"), "2026-10-01");
  await userEvent.type(screen.getByLabelText("Aylık brüt maaş"), "50000");
  await userEvent.type(screen.getByLabelText("Kaynak / karar referansı"), "Yeni dönem kararı");
  await userEvent.click(screen.getByRole("button", { name: "Kaydetmeden önce kontrol et" }));
  await chooseSource("Ücret paketi eklemede kullandığım rehber sürümü");
  expect(requests("POST", "/api/employees/1/compensation-packages")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button", { name: "Ücret paketini onayla ve kaydet" }));
  await waitFor(() => expect(body("POST", "/api/employees/1/compensation-packages")).toMatchObject({
    effectiveFrom: "2026-10-01", monthlyGrossSalary: 50000, sourceReference: "Yeni dönem kararı", guidanceContentRevisionId: 22 }));
  expect(requests("PUT", "/api/employees/1")).toHaveLength(0);
});

test("yenilenen ücret bağlamı eski düzeltme taslağının değerlerini veya sürümünü değiştirmez", async () => {
  stubApi(routes()); const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  let current = salary;
  mock.mockImplementation(async (input, init) => String(input).endsWith("/correction-context")
    ? new Response(JSON.stringify({ package: current, canCorrect: true, blockingReasons: [], history: [] }))
    : String(input) === "/api/employees/1/compensation-packages/2" && init?.method === "PUT"
      ? new Response(JSON.stringify({ title: "Paket sürümü değişti" }), { status: 409 }) : original(input, init));
  function Harness() { const client = useQueryClient(); return <><button onClick={() => void client.invalidateQueries({
    queryKey: ["employees", 1, "compensation-packages", 2, "correction-context"] })}>Bağlamı yenile</button>
    <CompensationCorrectionPanel employeeId={1} packageId={2} onClose={() => {}} /></>; }
  render(<Harness />); const gross = await screen.findByLabelText("Aylık brüt maaş");
  await userEvent.clear(gross); await userEvent.type(gross, "55000");
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi"), "Kaynak doğrulandı");
  await chooseSource("Tutar düzeltmede kullandığım rehber sürümü");
  current = { ...salary, version: 2, monthlyGrossSalary: 60000, sourceReference: "Sonraki karar" };
  await userEvent.click(screen.getByRole("button", { name: "Bağlamı yenile" }));
  await screen.findByText(/Paket bu sırada değişti/); expect(gross).toHaveValue(55000);
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi kontrol et" }));
  expect(screen.getByRole("region", { name: "Düzeltme ön kontrolü" })).toHaveTextContent("50.000");
  expect(screen.getByRole("region", { name: "Düzeltme ön kontrolü" })).not.toHaveTextContent("60.000");
  await userEvent.click(screen.getByRole("button", { name: "Düzeltmeyi onayla ve kaydet" }));
  await screen.findByText("Paket sürümü değişti");
  expect(body("PUT", "/api/employees/1/compensation-packages/2")).toMatchObject({ expectedVersion: 1, monthlyGrossSalary: 55000,
    sourceReference: "İlk karar", reason: "Kaynak doğrulandı", guidanceContentRevisionId: 22 });
  expect(gross).toHaveValue(55000); expect(screen.getByRole("button", { name: "Rehber sürümü bağlantısını kaldır" })).toBeInTheDocument();
});

test("tarih kaynağı seçilince eski önizleme bırakılır; yeni sunucu önizlemesinin sürümüyle kaydedilir", async () => {
  stubApi(routes()); render(<CompensationCorrectionPanel employeeId={1} packageId={2} onClose={() => {}} />);
  const date = await screen.findByLabelText("Önerilen yeni yürürlük tarihi");
  await userEvent.clear(date); await userEvent.type(date, "2026-10-01");
  await userEvent.type(screen.getByLabelText("Tarih düzeltme gerekçesi"), "Tarih belgesi");
  await userEvent.click(screen.getByRole("button", { name: "Tarih etkisini önizle" }));
  await screen.findByRole("region", { name: "Tarih düzeltme önizlemesi" });
  await chooseSource("Tarih düzeltmede kullandığım rehber sürümü");
  expect(screen.queryByRole("region", { name: "Tarih düzeltme önizlemesi" })).not.toBeInTheDocument();
  expect(requests("PUT", "/api/employees/1/compensation-packages/2/effective-date")).toHaveLength(0);
  await userEvent.click(screen.getByRole("button", { name: "Tarih etkisini önizle" }));
  await userEvent.click(await screen.findByRole("button", { name: "Tarih düzeltmesini onayla ve kaydet" }));
  await waitFor(() => expect(body("PUT", "/api/employees/1/compensation-packages/2/effective-date")).toEqual({
    proposedEffectiveFrom: "2026-10-01", expectedPackageVersion: 1, expectedPreviousPackageId: null,
    expectedPreviousPackageVersion: null, reason: "Tarih belgesi", guidanceContentRevisionId: 22 }));
});

test("yolculuk parasal sürümü ve yeni dönemi gösterir; işe dönüş erişim tamamlandı sayılmaz", async () => {
  stubApi({ "/api/guidance/operations": [{ isCurrent: true, evidence: { evidenceKey: "personnel:9", operationKey: "employee-rehire",
    resourceId: 3, outcome: "rehired", occurredAtUtc: "2026-10-01T10:00:00Z", content: { contentRevisionId: 22, contentVersion: 1, title: "İşe dönüş" },
    personnelChanges: { employeeId: 1, employmentPeriodId: 3, compensationPackageId: 2, relatedPackageId: 1,
      fromVersion: 1, toVersion: 2, changedFields: ["İş / Yeni çalışma dönemi"], reason: "Yeni sözleşme" } } }] });
  render(<GuidanceOperationJourney />);
  expect(await screen.findByText(/Yeni dönem açıldı; erişim eşitlemesi istendi/)).toBeInTheDocument();
  expect(screen.getByText(/Çalışma dönemi #3.*Ücret paketi #2.*Sürüm 1 → 2/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "İşlemde kullanılan açıklama ve dayanağı aç" })).toHaveAttribute("href", "/app/guidance/catalog?version=22");
});
