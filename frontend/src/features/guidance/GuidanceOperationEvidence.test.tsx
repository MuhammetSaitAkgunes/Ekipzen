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
async function chooseSource() {
  await userEvent.click(screen.getByText("Gerçek işlemde kullandığım rehber sürümü"));
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
  stubApi({ "/api/guidance/operations": [{ isCurrent: false, evidence: { operationKey: "leave-decision", resourceId: 8,
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
  await chooseSource();
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
