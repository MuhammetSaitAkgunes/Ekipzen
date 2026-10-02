import { useState } from "react";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { ToastProvider } from "../../layout/ToastProvider";
import { renderPage } from "../../test/renderPage";
import { stubApi } from "../../test/apiStub";
import { PersonnelModal } from "../personnel/PersonnelModal";
import { CandidateDetail } from "../recruitment/CandidateDetail";
import { ApprovalsList } from "../inbox/ApprovalsList";
import { GuidanceOperationSource } from "./GuidanceOperationSource";
import { GuidanceOperationJourney } from "./GuidanceOperationJourney";
import { TenantSetupPanel } from "../settings/TenantSetupPanel";
import { AttendanceEntryModal } from "../attendance/AttendanceEntryModal";
import { DocumentModal } from "../compliance/DocumentModal";
import { CreatePeriodModal, SubmitPeriodModal } from "../payroll/PayrollPeriodGuidance";
import type { PayrollPeriodDetailDto } from "../payroll/queries";
import { CompliancePage } from "../compliance/CompliancePage";

const source = (process = "leave", step = "decision-effect", id = 22) => ({ id, contentKey: `${process}-${step}`, processKey: process,
  contentVersion: id - 21, isCurrent: true, body: { title: "Kaynaklı işlem", stepKey: step, prerequisites: "Ön koşulu incele",
    action: "Gerçek kaydı kontrol et", why: "Kaynak ile doğrula", example: "Ayrıntılı örnek", correction: "Hata düzeltme yolu",
    expertEscalation: "Uzmana ilet", sourceKind: "good-practice", sourceReference: "Ekipzen", provision: "İşlem güvenliği", applicability: "Standart kayıt" } });
function routes(process = "leave", step = "decision-effect") {
  return { "/api/guidance/catalog": [source(process, step)], "/api/guidance/catalog/versions/22": source(process, step), "/api/guidance/help-preferences": [] };
}
function render(element: React.ReactNode) {
  return renderPage(<AuthProvider><ToastProvider>{element}</ToastProvider></AuthProvider>);
}
async function chooseSource(label = "Gerçek işlemde kullandığım rehber sürümü") {
  await userEvent.click(screen.getByText(label));
  await userEvent.click(await screen.findByRole("button", { name: "Bu rehber sürümünü işlemime bağla" }));
  await screen.findByRole("region", { name: "Seçilen adımın açıklaması" });
}
beforeEach(() => {
  localStorage.clear();
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: { id: "u", name: "İK", email: "u@example.test",
    role: "hr-admin", roleLabel: "İK", initials: "İK", employeeId: null } }));
});
afterEach(() => vi.unstubAllGlobals());

test("kaynak açıkça seçilir; yeni yayın eski seçimi değiştirmez ve seçim kanıt yazmaz", async () => {
  let current = source(); stubApi(routes()); const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => String(input).startsWith("/api/guidance/catalog?")
    ? new Response(JSON.stringify([current])) : original(input, init));
  function Harness() { const [value, setValue] = useState<number | null>(null); return <><output aria-label="Seçilen kimlik">{value ?? "yok"}</output>
    <GuidanceOperationSource process="leave" step="decision-effect" value={value} onChange={setValue} /></>; }
  render(<Harness />); expect(fetch).not.toHaveBeenCalled();
  await chooseSource(); expect(screen.getByLabelText("Seçilen kimlik")).toHaveTextContent("22");
  current = source("leave", "decision-effect", 23);
  await userEvent.click(screen.getByRole("button", { name: "İşlem rehberini yeniden yükle" }));
  await screen.findByText("Kaynaklı işlem · Sürüm 2");
  expect(screen.getByLabelText("Seçilen kimlik")).toHaveTextContent("22");
  expect(mock.mock.calls.some(([, init]) => init?.method === "POST" || init?.method === "PUT")).toBe(false);
  await userEvent.click(screen.getByRole("button", { name: "Rehber sürümü bağlantısını kaldır" }));
  expect(screen.getByLabelText("Seçilen kimlik")).toHaveTextContent("yok");
});

test("yanlış adım veya yüklenemeyen kaynak kendiliğinden seçilmez", async () => {
  stubApi({ "/api/guidance/catalog": [source("leave", "review-request")] }); const changed = vi.fn();
  const first = render(<GuidanceOperationSource process="leave" step="decision-effect" value={null} onChange={changed} />);
  await userEvent.click(screen.getByText("Gerçek işlemde kullandığım rehber sürümü"));
  await screen.findByText(/güncel, incelenmiş adım yayımlanmamış/);
  expect(screen.queryByRole("button", { name: "Bu rehber sürümünü işlemime bağla" })).not.toBeInTheDocument();
  expect(changed).not.toHaveBeenCalled(); first.unmount(); stubApi({});
  render(<GuidanceOperationSource process="leave" step="decision-effect" value={null} onChange={changed} />);
  await userEvent.click(screen.getByText("Gerçek işlemde kullandığım rehber sürümü"));
  await screen.findByRole("alert"); expect(changed).not.toHaveBeenCalled();
});

test("yolculuk eski işlem sürümünü korur; yeni içerik veya bütün süreç başarısı diye göstermez", async () => {
  stubApi({ "/api/guidance/operations": [{ isCurrent: false, evidence: { evidenceKey: "leave-decision:1", operationKey: "leave-decision", resourceId: 8,
    outcome: "Rejected", occurredAtUtc: "2026-10-01T10:00:00Z", content: { contentRevisionId: 22, contentVersion: 1, title: "Eski karar açıklaması" } } }] });
  render(<GuidanceOperationJourney />);
  await screen.findByText(/Eski karar açıklaması · Sürüm 1/);
  expect(screen.getByText(/geçmiş kanıt yeni sürüme aktarılmadı/)).toBeInTheDocument();
  expect(screen.getByText(/bağımsız yeterliliği kanıtlamaz/)).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "İşlemde kullanılan açıklama ve dayanağı aç" })).toHaveAttribute("href", "/app/guidance/catalog?version=22");
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
});

test("yeni çalışan POST'u seçilen sürümü taşır; çakışmada form ve sürüm korunur", async () => {
  stubApi({ ...routes("personnel-entry", "tab-is"), "/api/departments": [{ id: 1, name: "Yazılım" }] });
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => init?.method === "POST" && String(input) === "/api/employees"
    ? new Response(JSON.stringify({ title: "Seçilen rehber sürümü güncel değil" }), { status: 409 }) : original(input, init));
  const created = vi.fn(); render(<PersonnelModal employeeId={null} readOnly={false} onClose={() => {}} onCreated={created} />);
  await chooseSource();
  await userEvent.type(screen.getByLabelText("TC Kimlik No *"), "98765432109");
  await userEvent.type(screen.getByLabelText("Adı"), "Yeni"); await userEvent.type(screen.getByLabelText("Soyadı"), "Kişi");
  await userEvent.click(screen.getByRole("button", { name: /İş & Kurumsal/ }));
  await userEvent.selectOptions(screen.getByLabelText("Departman"), "1");
  await userEvent.selectOptions(screen.getByLabelText("Sözleşme Türü"), "indefinite");
  await userEvent.selectOptions(screen.getByLabelText("Çalışma Şekli"), "Tam Zamanlı");
  await userEvent.type(screen.getByLabelText("Ünvan / Görev"), "Uzman");
  await userEvent.clear(screen.getByLabelText("İşe Giriş Tarihi"));
  await userEvent.type(screen.getByLabelText("İşe Giriş Tarihi"), "2026-10-01");
  await userEvent.click(screen.getByRole("button", { name: /^Kaydet$/ }));
  await screen.findByText("Seçilen rehber sürümü güncel değil");
  const input = mock.mock.calls.find(([url, init]) => String(url) === "/api/employees" && init?.method === "POST")!;
  expect(JSON.parse(String(input[1]?.body))).toMatchObject({ firstName: "Yeni", guidanceContentRevisionId: 22 });
  expect(screen.getByLabelText("Adı")).toHaveValue("Yeni"); expect(created).not.toHaveBeenCalled();
  expect(screen.getByText(/Gerçek işlemde kullandığım rehber sürümü · seçildi/)).toBeInTheDocument();
});

test("aday ilk aşama PATCH'i kaynak kimliğini taşır ve seçmek tek başına karar göndermez", async () => {
  const candidate = { id: 5, version: 3, name: "Başvuran", appliedRole: "Uzman", status: "Yeni", score: null,
    skills: [], experience: [], notes: [], evaluations: [], history: [], stageDecisions: [] };
  stubApi({ ...routes("recruitment", "first-review"), "/api/candidates/5": candidate, "/api/candidates/5/status": { ...candidate, status: "Mülakat" } });
  render(<CandidateDetail id={5} />); await screen.findByRole("heading", { name: "Başvuran" });
  await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"), "Mülakat");
  await chooseSource("Aşama kararında kullandığım rehber sürümü");
  expect(vi.mocked(fetch).mock.calls.some(([, init]) => init?.method === "PATCH")).toBe(false);
  await userEvent.type(screen.getByLabelText("Aşama kararının gerekçesi ve dayanağı"), "Rol koşulları incelendi.");
  await userEvent.click(screen.getByLabelText("Aday kaydını ve aşama gerekçesini kontrol ettim."));
  await userEvent.click(screen.getByRole("button", { name: "Aşamayı kaydet" }));
  await waitFor(() => {
    const request = vi.mocked(fetch).mock.calls.find(([, init]) => init?.method === "PATCH")!;
    expect(JSON.parse(String(request[1]?.body))).toMatchObject({ status: "Mülakat", expectedVersion: 3, guidanceContentRevisionId: 22 });
  });
});

test("izin kararı sürüm ve notu birlikte gönderir; eski sürümde inceleme korunur", async () => {
  stubApi({ ...routes(), "/api/leaves/pending": [{ id: 8, employeeName: "Çalışan", leaveTypeName: "Mazeret", days: 1,
    startDate: "2026-10-05", endDate: "2026-10-05" }] });
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => String(input) === "/api/leaves/8/approve" && init?.method === "POST"
    ? new Response(JSON.stringify({ title: "Rehber sürümü değişti" }), { status: 409 }) : original(input, init));
  render(<ApprovalsList />);
  await userEvent.click(await screen.findByRole("button", { name: "İncele" }));
  await userEvent.click(screen.getByRole("button", { name: "Onayı incele" }));
  await chooseSource(); await userEvent.type(screen.getByLabelText("Karar notu (isteğe bağlı)"), "Kaynak kontrol edildi.");
  await userEvent.click(screen.getByRole("button", { name: "Onayı kaydet" }));
  await screen.findByText("Rehber sürümü değişti");
  const request = mock.mock.calls.find(([url, init]) => String(url) === "/api/leaves/8/approve" && init?.method === "POST")!;
  expect(JSON.parse(String(request[1]?.body))).toEqual({ note: "Kaynak kontrol edildi.", guidanceContentRevisionId: 22 });
  expect(screen.getByLabelText("Karar notu (isteğe bağlı)")).toHaveValue("Kaynak kontrol edildi.");
  expect(screen.getByText(/Gerçek işlemde kullandığım rehber sürümü · seçildi/)).toBeInTheDocument();
});


test("kurulum kaynağı seçimi ön kontrolü yeniler; eski sürüm hatasında seçim korunur", async () => {
  const missing = { templateYear: 2026, companyProfileExists: false, missingLeaveTypeCount: 4, missingFullDayHolidayCount: 14, payrollSettingsExists: false, hasEffectiveApprovedPayrollSettings: false, needsInitialization: true };
  stubApi({ ...routes("company", "initial-setup"), "/api/setup": missing, "/api/setup/preview": { status: missing, token: "setup-v1", companyName: null, leaveTypes: [], holidays: [], payrollDraft: null } });
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => String(input) === "/api/setup" && init?.method === "POST"
    ? Response.json({ title: "Seçilen sürüm güncel değil" }, { status: 409 }) : original(input, init));
  render(<TenantSetupPanel />);
  await userEvent.click(await screen.findByRole("button", { name: "Kurulumu gözden geçir" }));
  const ack = screen.getByRole("checkbox", { name: /Eksik kayıtların oluşturulacağını/ });
  await waitFor(() => expect(ack).toBeEnabled());
  await userEvent.click(ack); await chooseSource(); expect(ack).not.toBeChecked();
  expect(mock.mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
  await userEvent.click(ack); await userEvent.click(screen.getByRole("button", { name: "Kontrol ettim, kayıtları oluştur" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Seçilen sürüm güncel değil");
  expect(screen.getByText(/rehber sürümü · seçildi/)).toBeInTheDocument();
  const posted = mock.mock.calls.find(([u, i]) => String(u) === "/api/setup" && i?.method === "POST")!;
  expect(JSON.parse(String(posted[1]?.body))).toEqual({ guidanceContentRevisionId: 22, expectedPreviewToken: "setup-v1" });
});

const attendanceRow = { id: 33, workDate: "2026-10-01", type: "Tam", checkIn: "09:00:00", checkOut: "18:00:00", breakMinutes: 60, workedMinutes: 480, overtimeMinutes: 0, status: "ok", note: null, revision: "638000000000000000" };
const attendancePreview = { workedMinutes: 480, overtimeMinutes: 0, status: "ok", effectiveType: "Tam", canSave: true, blockReason: null, warnings: [] };
test.each([false, true])("puantaj kaynağı yalnız son yazıma gider; önizleme bağımsız kalır (düzenleme %s)", async (edit) => {
  stubApi({ ...routes("attendance", edit ? "entry-correction" : "entry-review"), "/api/employees": { items: [{ id: 1, name: "Gerçek Personel" }] }, "/api/attendance/preview": attendancePreview, "/api/attendance": attendanceRow, "/api/attendance/33": attendanceRow });
  const close = vi.fn(); render(<AttendanceEntryModal rowId={edit ? 33 : null} initial={edit ? attendanceRow : undefined} defaultEmployeeId={1} onClose={close} />);
  await chooseSource();
  if (!edit) {
    await userEvent.selectOptions(screen.getByLabelText("Personel"), "1"); await userEvent.selectOptions(screen.getByLabelText("Tip"), "Tam");
    await userEvent.type(screen.getByLabelText("Tarih"), "2026-10-01"); await userEvent.type(screen.getByLabelText("Mola (dk)"), "60");
  }
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" }));
  await screen.findByRole("heading", { name: "Kaydetmeden önce kontrol edin" });
  const preview = vi.mocked(fetch).mock.calls.find(([u]) => String(u) === "/api/attendance/preview")!;
  expect(JSON.parse(String(preview[1]?.body)).model.guidanceContentRevisionId).toBeUndefined(); expect(close).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" })); await waitFor(() => expect(close).toHaveBeenCalledOnce());
  const saved = vi.mocked(fetch).mock.calls.find(([u, i]) => String(u) === (edit ? "/api/attendance/33" : "/api/attendance") && i?.method === (edit ? "PUT" : "POST"))!;
  expect(JSON.parse(String(saved[1]?.body)).model.guidanceContentRevisionId).toBe(22);
});

const complianceDocument = { id: 7, revision: "document-v1", employeeId: 1, employee: "Gerçek Personel", dept: "İK", document: "Sözleşme", owner: "İK", dueDate: null, dueLabel: "-", status: "Eksik", level: "medium" };
test("uyum düzeltmesi tek sürümlü istektir; 409 bütün formu korur", async () => {
  stubApi({ ...routes("compliance", "record-correction"), "/api/employees": { items: [] }, "/api/compliance/documents/7": complianceDocument });
  const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => String(input) === "/api/compliance/documents/7" && init?.method === "PUT"
    ? Response.json({ title: "Rehber sürümü değişti" }, { status: 409 }) : original(input, init));
  const close = vi.fn(); render(<DocumentModal document={complianceDocument} onClose={close} />); await chooseSource();
  await userEvent.clear(screen.getByLabelText("Sorumlu")); await userEvent.type(screen.getByLabelText("Sorumlu"), "Yeni sorumlu");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" })); await userEvent.click(screen.getByRole("checkbox", { name: /Personel, belge, durum, risk/ }));
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Rehber sürümü değişti"); expect(close).not.toHaveBeenCalled();
  const write = mock.mock.calls.find(([u, i]) => String(u) === "/api/compliance/documents/7" && i?.method === "PUT")!;
  expect(JSON.parse(String(write[1]?.body))).toMatchObject({ guidanceContentRevisionId: 22, expectedRevision: "document-v1", ownerName: "Yeni sorumlu", updateOwner: true });
  expect(mock.mock.calls.some(([u]) => String(u).endsWith("/owner"))).toBe(false);
});

test("uyum oluşturma seçilen sürümle POST gönderir", async () => {
  stubApi({ ...routes("compliance", "record-review"), "/api/employees": { items: [{ id: 1, name: "Gerçek Personel" }] }, "/api/compliance/documents": complianceDocument });
  const close = vi.fn(); render(<DocumentModal document={null} onClose={close} />); await chooseSource();
  await userEvent.selectOptions(screen.getByLabelText("Personel"), "1"); await userEvent.type(screen.getByLabelText("Belge adı"), "Sözleşme");
  await userEvent.selectOptions(screen.getByLabelText("Durum"), "Eksik"); await userEvent.selectOptions(screen.getByLabelText("Risk seviyesi"), "medium");
  await userEvent.click(screen.getByRole("button", { name: "Kaydı gözden geçir" })); await userEvent.click(screen.getByRole("checkbox", { name: /Personel, belge, durum, risk/ }));
  await userEvent.click(screen.getByRole("button", { name: "Onayla ve kaydet" })); await waitFor(() => expect(close).toHaveBeenCalledOnce());
  const post = vi.mocked(fetch).mock.calls.find(([u, i]) => String(u) === "/api/compliance/documents" && i?.method === "POST")!;
  expect(JSON.parse(String(post[1]?.body)).guidanceContentRevisionId).toBe(22);
});

test("uyum durum ön kontrolü kaynakla kaydeder; yeni işlem eski seçimi taşımaz", async () => {
  stubApi({ ...routes("compliance", "status-review"), "/api/compliance/documents": [complianceDocument], "/api/compliance/readiness": {}, "/api/compliance/documents/7/status": complianceDocument });
  render(<CompliancePage />);
  const status = await screen.findByLabelText("Belge durumu"); await userEvent.selectOptions(status, "İncelemede"); await chooseSource();
  await userEvent.click(screen.getByRole("button", { name: "Durumu kaydet" }));
  await waitFor(() => expect(screen.queryByRole("button", { name: "Durumu kaydet" })).not.toBeInTheDocument());
  const patch = vi.mocked(fetch).mock.calls.find(([u, i]) => String(u) === "/api/compliance/documents/7/status" && i?.method === "PATCH")!;
  expect(JSON.parse(String(patch[1]?.body)).guidanceContentRevisionId).toBe(22);
  await userEvent.selectOptions(status, "Tamamlandı"); expect(screen.queryByText(/rehber sürümü · seçildi/)).not.toBeInTheDocument();
});

test("aynı satırın iki düzeltmesi ayrı görünür; kurulum kanıtında gerçek eklenen sayılar gösterilir", async () => {
  const content = { contentRevisionId: 22, contentVersion: 1, title: "Kaynaklı işlem" };
  stubApi({ "/api/guidance/operations": [
    ...[1, 2].map(id => ({ isCurrent: true, evidence: { evidenceKey: "attendance-update:" + id, operationKey: "attendance-update", resourceId: 33, outcome: "updated", occurredAtUtc: "2026-10-01T10:00:00Z", content } })),
    { isCurrent: true, evidence: { evidenceKey: "tenant-setup:1", operationKey: "tenant-setup", resourceId: 2026, outcome: "initialized", occurredAtUtc: "2026-10-01T10:00:00Z", content, setupChanges: { addedCompanyProfile: false, addedLeaveTypeCount: 2, addedHolidayCount: 3, addedPayrollSettings: true } } },
  ] });
  render(<GuidanceOperationJourney />); await screen.findByText(/Şablon yılı 2026/);
  expect(screen.getAllByText("Puantaj düzeltme")).toHaveLength(2); expect(screen.getByText(/İzin türü: 2 eklendi/)).toBeInTheDocument();
});

test("bordro açılışında kaynak değişimi teyidi sıfırlar; 409 tarih ve seçimi korur", async () => {
  stubApi({ ...routes("payroll", "period-create"), "/api/payroll/periods/creation-preview": {
    year: 2026, month: 10, name: "Ekim", token: "period-token", rows: [],
    settings: { version: 1 }, minimumWageIncomeTaxBaseBefore: 0 } }); const mock = vi.mocked(fetch), original = mock.getMockImplementation()!;
  mock.mockImplementation(async (input, init) => String(input) === "/api/payroll/periods" && init?.method === "POST"
    ? Response.json({ title: "Rehber sürümü değişti" }, { status: 409 }) : original(input, init));
  const onCreated = vi.fn(); render(<CreatePeriodModal latest={null} onClose={() => {}} onCreated={onCreated} />);
  await userEvent.clear(screen.getByLabelText("Yıl")); await userEvent.type(screen.getByLabelText("Yıl"), "2026");
  await userEvent.selectOptions(screen.getByLabelText("Ay"), "10");
  await userEvent.click(screen.getByRole("button", { name: "Gerçek başlangıç girdilerini önizle" }));
  await screen.findByText(/0 personel satırı taslak/);
  const ack = screen.getByRole("checkbox", { name: "Ayı, personel satırlarını, kaynakları ve eksikleri kontrol ettim." });
  await userEvent.click(ack); await chooseSource(); expect(ack).not.toBeChecked();
  expect(mock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  await userEvent.click(ack); await userEvent.click(screen.getByRole("button", { name: "Oluştur" }));
  await screen.findByText("Rehber sürümü değişti");
  const request = mock.mock.calls.find(([url, init]) => String(url) === "/api/payroll/periods" && init?.method === "POST")!;
  expect(JSON.parse(String(request[1]?.body))).toEqual({ year: 2026, month: 10, expectedPreviewToken: "period-token", guidanceContentRevisionId: 22 });
  expect(screen.getByLabelText("Yıl")).toHaveValue(2026); expect(screen.getByLabelText("Ay")).toHaveValue("10");
  expect(onCreated).not.toHaveBeenCalled(); expect(screen.getByText(/rehber sürümü · seçildi/)).toBeInTheDocument();
});

test("bordro dönemi onayı seçilen kaynağı yalnız son teyitten sonra iletir", async () => {
  stubApi(routes("payroll", "approval")); const onSubmit = vi.fn();
  const detail = { id: 1, name: "Ekim 2026", rows: [{ id: 8, approvalStatus: "Onaylandı" }] } as PayrollPeriodDetailDto;
  render(<SubmitPeriodModal detail={detail} pending={false} onClose={() => {}} onSubmit={onSubmit} />);
  const ack = screen.getByRole("checkbox", { name: "Satırları ve dönem sonucunu kontrol ettim." });
  await userEvent.click(ack); await chooseSource(); expect(ack).not.toBeChecked(); expect(onSubmit).not.toHaveBeenCalled();
  await userEvent.click(ack); await userEvent.click(screen.getByRole("button", { name: "Dönemi onayla" }));
  expect(onSubmit).toHaveBeenCalledExactlyOnceWith(22);
});
