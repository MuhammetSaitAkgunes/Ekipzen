import { useQueryClient } from "@tanstack/react-query";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { GuidanceOperationJourney } from "../guidance/GuidanceOperationJourney";
import { DocumentsTab } from "./DocumentsTab";
import { PersonnelModal } from "./PersonnelModal";

const detail = { id: 1, firstName: "Ada", lastName: "Kayıt", name: "Ada Kayıt", title: "Uzman", initials: "AK",
  status: "active", hireDate: "2026-01-01", contractType: "indefinite", departmentId: 1, department: "İK",
  managerId: null, nationalId: null, recordToken: "A".repeat(64), profile: { employmentType: "Tam Zamanlı", emergencyContactRelation: "Kardeşi" }, documents: [] };
const source = (process: string, step: string) => ({ id: 22, contentKey: `${process}-${step}`, processKey: process, contentVersion: 1,
  isCurrent: true, body: { title: "Test işlem kaynağı", stepKey: step, action: "Kaydı kontrol et", prerequisites: "Yetkiyi kontrol et",
    why: "Gerçek kayıt doğrulaması", example: "Örnek", correction: "Gerekçeli düzelt", expertEscalation: "Uzmana ilet",
    sourceKind: "good-practice", sourceReference: "Test", provision: "Kayıt güvenliği", applicability: "Standart işlem" } });
const routes = (process = "personnel", step = "record-review") => ({
  "/api/guidance/catalog": [source(process, step)], "/api/guidance/catalog/versions/22": source(process, step),
  "/api/guidance/help-preferences": [], "/api/employees/1": detail,
  "/api/departments": [{ id: 1, name: "İK" }, { id: 2, name: "Operasyon" }],
  "/api/employees/manager-choices": [{ id: 2, name: "Ece Yönetici", title: "Sorumlu" }],
  "/api/employees/1/personnel-history": [], "/api/employees/1/employment-foundation": { employeeId: 1, employmentPeriods: [], compensationPackages: [] },
});
function render(element: React.ReactNode) { return renderPage(<AuthProvider><ToastProvider>{element}</ToastProvider></AuthProvider>); }
async function chooseSource() {
  await userEvent.click(screen.getByText("Gerçek işlemde kullandığım rehber sürümü"));
  await userEvent.click(await screen.findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
  await screen.findByRole("region", { name: "Seçilen adımın açıklaması" });
}
beforeEach(() => {
  localStorage.clear(); localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: {
    id: "u", name: "İK", email: "u@example.test", role: "hr-admin", roleLabel: "İK", initials: "İK", employeeId: null } }));
});
afterEach(() => vi.unstubAllGlobals());

test("kart düzeltmesi açık kaynak, gerekçe, departman ve yönetici seçimini eski kart tokenıyla gönderir", async () => {
  stubApi(routes()); const saved = vi.fn();
  render(<PersonnelModal employeeId={1} readOnly={false} onClose={saved} />);
  await screen.findByText("Personel Kartı — Ada Kayıt"); await chooseSource();
  await userEvent.click(screen.getByRole("button", { name: /^Kaydet$/ }));
  expect(await screen.findByText("Rehberli düzeltmede gerekçe yazın.")).toBeInTheDocument();
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "PUT")).toBe(false);
  await userEvent.type(screen.getByLabelText("Düzeltme gerekçesi (rehber bağlantısında zorunlu)"), "Organizasyon kararı");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.selectOptions(screen.getByLabelText("Departman"), "2");
  await userEvent.selectOptions(screen.getByLabelText("Yönetici"), "2");
  await userEvent.click(screen.getByRole("button", { name: /^Kaydet$/ }));
  await waitFor(() => expect(saved).toHaveBeenCalled());
  const posted = vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === "PUT")!;
  expect(JSON.parse(String(posted[1]?.body))).toMatchObject({ expectedRecordToken: detail.recordToken, guidanceContentRevisionId: 22,
    correctionReason: "Organizasyon kararı", managerId: 2, departmentId: 2, nationalId: null,
    profile: { rehireEligibility: null, exitCode: null, emergencyContactRelation: "Kardeşi" } });
});

test("arka plan yenilemesi taslağı ve eski tokenı korur; çakışmada açık yeniden hazırlama mümkündür", async () => {
  stubApi(routes()); const mock = vi.mocked(fetch), original = mock.getMockImplementation()!; let current = detail;
  mock.mockImplementation(async (input, init) => String(input) === "/api/employees/1"
    ? init?.method === "PUT" ? new Response(JSON.stringify({ title: "Personel kartı bu sırada değişti" }), { status: 409 }) : new Response(JSON.stringify(current))
    : original(input, init));
  function Harness() { const client = useQueryClient(); return <><button onClick={() => void client.invalidateQueries({ queryKey: ["employees", 1] })}>Arka plan yenile</button>
    <PersonnelModal employeeId={1} readOnly={false} onClose={() => {}} /></>; }
  render(<Harness />); await screen.findByText("Personel Kartı — Ada Kayıt");
  await userEvent.clear(screen.getByLabelText("Adı")); await userEvent.type(screen.getByLabelText("Adı"), "Taslak");
  current = { ...detail, firstName: "Güncel", name: "Güncel Kayıt", recordToken: "B".repeat(64) };
  await userEvent.click(screen.getByRole("button", { name: "Arka plan yenile" }));
  await screen.findByText("Personel Kartı — Güncel Kayıt"); expect(screen.getByLabelText("Adı")).toHaveValue("Taslak");
  await userEvent.click(screen.getByRole("button", { name: /^Kaydet$/ }));
  await screen.findByText("Personel kartı bu sırada değişti"); expect(screen.getByLabelText("Adı")).toHaveValue("Taslak");
  expect(JSON.parse(String(mock.mock.calls.find(([, init]) => init?.method === "PUT")![1]?.body))).toMatchObject({ firstName: "Taslak", expectedRecordToken: detail.recordToken });
  await userEvent.click(screen.getByRole("button", { name: "Taslağı bırakıp güncel kartı yükle" }));
  await waitFor(() => expect(screen.getByLabelText("Adı")).toHaveValue("Güncel"));
});

test("belge yükleme seçilmiş sürümü multipart gövdesinde taşır; önizleme kanıt göndermez", async () => {
  stubApi({ ...routes("personnel-entry", "tab-evrak"), "/api/employees/1/documents": [], "/api/employees/1/documents/withdrawn": [] });
  render(<DocumentsTab employeeId={1} readOnly={false} />);
  await screen.findByText("Henüz evrak yok");
  await userEvent.type(screen.getByLabelText("Evrak Türü"), "Kontrol belgesi");
  await userEvent.upload(screen.getByLabelText("Evrak dosyası seç"), new File(["example"], "kontrol.pdf", { type: "application/pdf" }));
  await chooseSource(); expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Dosyayı yükle" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === "POST")!;
    const body = posted[1]!.body as FormData;
    expect(body.get("guidanceContentRevisionId")).toBe("22");
    expect(body.get("documentType")).toBe("Kontrol belgesi");
  });
});

test("belge geri çekme gerekçesi ve sürümü birlikte taşır", async () => {
  stubApi({ ...routes("personnel-entry", "tab-evrak"), "/api/employees/1/documents": [{ id: 9, documentType: "Kontrol", fileName: "kontrol.pdf", sizeBytes: 5, createdAtUtc: "2026-10-01T10:00:00Z" }],
    "/api/employees/1/documents/withdrawn": [], "/api/employees/1/documents/9/withdraw": {} });
  render(<DocumentsTab employeeId={1} readOnly={false} />);
  await userEvent.click(await screen.findByRole("button", { name: "kontrol.pdf dosyasını geri çek" }));
  await chooseSource(); await userEvent.type(screen.getByLabelText("Geri çekme gerekçesi"), "Yanlış sürüm");
  await userEvent.click(screen.getByRole("button", { name: "Geri çekmeyi onayla" }));
  await waitFor(() => expect(JSON.parse(String(vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === "POST")![1]?.body)))
    .toEqual({ reason: "Yanlış sürüm", guidanceContentRevisionId: 22 }));
});

test("öğrenme yolculuğu değişen alanları ve gerekçeyi kullanılan eski sürümle gösterir", async () => {
  stubApi({ "/api/guidance/operations": [{ isCurrent: false, evidence: { evidenceKey: "employee-update:3", operationKey: "employee-update", resourceId: 1,
    outcome: "updated", occurredAtUtc: "2026-10-01T10:00:00Z", content: { contentRevisionId: 22, contentVersion: 1, title: "Kart kontrolü" },
    personnelChanges: { employeeId: 1, changedFields: ["İş / Departman", "İş / Yönetici"], reason: "Organizasyon kararı" } } }] });
  render(<GuidanceOperationJourney />);
  expect(await screen.findByText(/Çalışan #1 · İş \/ Departman, İş \/ Yönetici · Gerekçe: Organizasyon kararı/)).toBeInTheDocument();
  expect(screen.getByText(/geçmiş kanıt yeni sürüme aktarılmadı/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "İşlemde kullanılan açıklama ve dayanağı aç" })).toHaveAttribute("href", "/app/guidance/catalog?version=22");
});
