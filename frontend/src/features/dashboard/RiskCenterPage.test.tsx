import { fireEvent, screen, within } from "@testing-library/react";
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
    { label: "Kültür / Nabız", value: 64, meta: "Çalışan nabzı", tone: "medium" },
  ],
  employees: [{ employeeId: 1 }, { employeeId: 2 }, { employeeId: 3 }, { employeeId: 4 }],
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

test("özet risk halkası ve açık iş dağılımıyla hızlı durum verir", async () => {
  renderRisk();
  expect(await screen.findByRole("img", { name: "Genel risk: 100 üzerinden 62" })).toBeInTheDocument();
  expect(screen.getByRole("img", { name: "1 yüksek, 0 orta, 1 düşük öncelikli açık iş" })).toBeInTheDocument();
  const nav = screen.getByRole("navigation", { name: "Risk Merkezi görünümleri" });
  expect(within(nav).getByRole("link", { name: "Özet" })).toHaveAttribute("aria-current", "page");
  expect(screen.getByRole("link", { name: /Tükenmişlik sinyali işini İş Kutusunda aç/ })).toHaveAttribute("href", "/app/inbox?tab=actions&actionId=1");
  expect(screen.queryByText("Kapanan iş")).not.toBeInTheDocument();
  expect(screen.queryByRole("heading", { name: "Departman karşılaştırması" })).not.toBeInTheDocument();
});

test("insan ve ekip görünümü metrikleri, eğilimi ve departman çubuklarını gösterir", async () => {
  renderRisk();
  fireEvent.click(screen.getByRole("link", { name: "İnsan ve ekip" }));
  expect(await screen.findByRole("link", { name: /Ayrılma riski/ })).toHaveAttribute("href", "/app/risk/attrition");
  expect(screen.getByRole("link", { name: /Tükenmişlik/ })).toHaveAttribute("href", "/app/risk/burnout");
  expect(screen.getByRole("img", { name: /Dönemsel risk sinyali 48 puandan 62 puana değişti/ })).toBeInTheDocument();
  const departments = screen.getByRole("region", { name: "Departman karşılaştırması" });
  expect(within(departments).getByText("Yazılım")).toBeInTheDocument();
  expect(within(departments).getByText("2 ayrılma · 1 tükenmişlik", { exact: false })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /5 bekleyen onay · 14 açık yönetici işi/ })).toHaveAttribute("href", "/app/risk/manager-load");
});

test("operasyon görünümü evrak halkasını ve kapasite çubuklarını gösterir", async () => {
  renderRisk();
  fireEvent.click(screen.getByRole("link", { name: "Operasyon ve uyum" }));
  expect(await screen.findByRole("img", { name: "Evrak uyumu: 100 üzerinden 82" })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: /Ayrıntılar/ })).toHaveAttribute("href", "/app/risk/compliance");
  expect(screen.getByText("11")).toBeInTheDocument();
  expect(screen.getByText("18")).toBeInTheDocument();
  expect(screen.getByText("İşe Alım Sağlığı")).toBeInTheDocument();
  expect(screen.getByText("Kritik Rol Riski")).toBeInTheDocument();
  expect(screen.queryByText("Kültür / Nabız")).not.toBeInTheDocument();
  expect(screen.getByRole("link", { name: /İşe alımı aç/ })).toHaveAttribute("href", "/app/recruitment");
});

test("yöneticiye yetkisiz işe alım bağlantısı sunulmaz", async () => {
  renderRisk("manager");
  fireEvent.click(screen.getByRole("link", { name: "Operasyon ve uyum" }));
  expect(await screen.findByRole("heading", { name: "İş gücü kapasitesi" })).toBeInTheDocument();
  expect(screen.queryByRole("link", { name: /İşe alımı aç/ })).not.toBeInTheDocument();
});

test("eksik veri sıfır olarak görselleştirilmez", async () => {
  stubApi({
    "/api/dashboard/metrics": { ...metrics, riskScore: null, riskTrend: [], departmentRisk: [], talentCapacity: [] },
    "/api/dashboard/manager-load": managerLoad,
    "/api/dashboard/employee-voice": voice,
    "/api/dashboard/compliance": { ...compliance, documentComplianceScore: null },
    "/api/actions": [],
  });
  renderRisk();
  expect(await screen.findByRole("img", { name: "Genel risk: veri yok" })).toBeInTheDocument();
  expect(screen.getByText("Şu anda açık aksiyon yok.")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("link", { name: "İnsan ve ekip" }));
  expect(await screen.findByText("Bu kapsamda departman verisi yok.")).toBeInTheDocument();
  expect(screen.getByText("Karşılaştırılacak dönem verisi yok.")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("link", { name: "Operasyon ve uyum" }));
  expect(await screen.findByRole("img", { name: "Evrak uyumu: veri yok" })).toBeInTheDocument();
  expect(screen.getByText("Bu alanda henüz gösterge yok.")).toBeInTheDocument();
});
