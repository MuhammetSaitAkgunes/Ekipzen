import { screen, within } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { RiskCenterPage } from "./RiskCenterPage";

const metrics = {
  riskScore: 62, managerLoadIndex: 71, attritionHigh: 3, burnoutRisk: 2,
  criticalActions: 7, pulseScore: 64,
  riskTrend: [48, 51, 49, 54, 57, 59, 62, 64, 61, 67, 69, 62],
  departmentRisk: [
    { departmentId: 1, dept: "Yazılım", risk: 74, employeeCount: 18, highAttritionCount: 2, highBurnoutCount: 1 },
  ],
  talentCapacity: [
    { label: "İşe Alım Sağlığı", value: 72, meta: "8 açık pozisyon", tone: "medium" },
    { label: "Kritik Rol Riski", value: 3, meta: "Yedekleme planı yok", tone: "high" },
  ],
  employees: [{ employeeId: 1 }],
};
const managerLoad = { managerLoadIndex: 71, criticalManagerCount: 2, pendingApprovals: 5, openActions: 14, managers: [] };
const voice = { pulseScore: 64, eNps: 8, participationRate: 76, decliningTeams: 2, sentimentTrend: "Düşüş", departments: [], signals: [], recommendedActions: [] };
const compliance = { documentComplianceScore: 82, missingDocuments: 11, upcomingDocuments: 18, auditReadinessRisk: "Orta", auditReadinessScore: 74, records: [], deadlines: [] };
const actions = [
  { id: 1, title: "Tükenmişlik sinyali", source: "Risk Merkezi", sourceRoute: "burnout-risk", owner: "Ece Arslan", due: "Bugün", priority: "high", status: "open", action: "Kapasite görüşmesi planla." },
  { id: 2, title: "İzlenen konu", source: "İşe Alım", sourceRoute: "recruitment", owner: "İşe Alım", due: null, priority: "low", status: "open", action: null },
  { id: 3, title: "Kapanan iş", source: "Bordro", sourceRoute: null, owner: "İK", due: null, priority: "high", status: "done", action: null },
];

const renderRisk = (role = "hr-admin") => {
  localStorage.setItem(SESSION_KEY, JSON.stringify({ token: "T", refreshToken: "R", user: { id: role, role, name: "Test Kullanıcı", initials: "TK", roleLabel: role } }));
  return renderPage(<AuthProvider><RiskCenterPage /></AuthProvider>);
};

beforeEach(() => {
  localStorage.clear();
  stubApi({
    "/api/dashboard/metrics": metrics,
    "/api/dashboard/manager-load": managerLoad,
    "/api/dashboard/employee-voice": voice,
    "/api/dashboard/compliance": compliance,
    "/api/actions": actions,
  });
});
afterEach(() => vi.unstubAllGlobals());

test("öncelikli işler ilk bölümde, iş kutusuna yönlendirilir", async () => {
  renderRisk();
  const title = await screen.findByRole("heading", { name: "Öncelikli aksiyonlar" });
  const score = screen.getByRole("heading", { name: "İK risk düzeyi" });
  expect(title.compareDocumentPosition(score) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  const priorities = screen.getByRole("region", { name: "Öncelikli aksiyonlar" });
  expect(within(priorities).getByText("1 yüksek öncelikli iş açık.")).toBeInTheDocument();
  expect(within(priorities).getByRole("link", { name: /Tükenmişlik sinyali işini İş Kutusunda aç/ })).toHaveAttribute("href", "/app/inbox?tab=actions");
  expect(screen.queryByText("Kapanan iş")).not.toBeInTheDocument();
});

test("risk alanları erişilebilir ayrıntı rotalarını gösterir", async () => {
  renderRisk();
  const areas = await screen.findByRole("region", { name: "Risk alanları" });
  expect(within(areas).getByRole("link", { name: /Ayrılma riski/ })).toHaveAttribute("href", "/app/risk/attrition");
  expect(within(areas).getByRole("link", { name: /Tükenmişlik/ })).toHaveAttribute("href", "/app/risk/burnout");
  expect(within(areas).getByRole("link", { name: /Evrak ve uyum/ })).toHaveAttribute("href", "/app/risk/compliance");
  expect(screen.getByText("5 bekleyen onay · 14 açık iş")).toBeInTheDocument();
});

test("departmanlar risk sırasıyla ve kesilmemiş açıklamayla görünür", async () => {
  renderRisk();
  const departments = await screen.findByRole("region", { name: "Departman riskleri" });
  expect(within(departments).getByText("18 çalışan · 2 yüksek ayrılma · 1 tükenmişlik")).toBeInTheDocument();
  expect(within(departments).getByText("Yüksek risk")).toBeInTheDocument();
  expect(screen.getByText("İlk döneme göre +14 puan artış.")).toBeInTheDocument();
  expect(screen.getByRole("img", { name: /48 puandan 62 puana değişti/ })).toBeInTheDocument();
});

test("yöneticiye yetkisiz işe alım bağlantısı sunulmaz", async () => {
  renderRisk("manager");
  expect(await screen.findByRole("heading", { name: "Yetenek göstergeleri" })).toBeInTheDocument();
  expect(screen.queryByRole("link", { name: /İşe alımı aç/ })).not.toBeInTheDocument();
});

test("boş ve eksik veriler sıfır risk gibi gösterilmez", async () => {
  stubApi({
    "/api/dashboard/metrics": { ...metrics, riskScore: null, riskTrend: [], departmentRisk: [], talentCapacity: [] },
    "/api/dashboard/manager-load": managerLoad,
    "/api/dashboard/employee-voice": voice,
    "/api/dashboard/compliance": compliance,
    "/api/actions": [],
  });
  renderRisk();
  expect(await screen.findByText("Şu anda açık aksiyon yok")).toBeInTheDocument();
  expect(screen.getByText("Veri yok")).toBeInTheDocument();
  expect(screen.getByText("Bu kapsamda departman verisi yok.")).toBeInTheDocument();
  expect(screen.getByText("Henüz karşılaştırılacak dönem verisi yok.")).toBeInTheDocument();
});
