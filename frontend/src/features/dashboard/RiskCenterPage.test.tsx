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

test("özet yalnız durum, öncelikli işler ve iki anlamlı geçiş gösterir", async () => {
  renderRisk();
  const status = await screen.findByRole("heading", { name: "Orta risk" });
  expect(status).toBeInTheDocument();
  const nav = screen.getByRole("navigation", { name: "Risk Merkezi görünümleri" });
  expect(within(nav).getByRole("link", { name: "Özet" })).toHaveAttribute("aria-current", "page");
  expect(within(nav).getByRole("link", { name: "İnsan ve ekip" })).toHaveAttribute("href", "/app/dashboard?view=people");
  const priorities = screen.getByRole("region", { name: "Öncelikli işler" });
  expect(within(priorities).getByText("1 yüksek öncelikli iş açık.")).toBeInTheDocument();
  expect(within(priorities).getByRole("link", { name: /Tükenmişlik sinyali işini İş Kutusunda aç/ })).toHaveAttribute("href", "/app/inbox?tab=actions");
  expect(screen.queryByText("Kapanan iş")).not.toBeInTheDocument();
  expect(screen.queryByRole("heading", { name: "Departmanlar" })).not.toBeInTheDocument();
  expect(screen.queryByRole("heading", { name: "İş gücü kapasitesi" })).not.toBeInTheDocument();
});

test("insan ve ekip görünümü dört sinyali ve ekip ayrıntılarını açar", async () => {
  renderRisk();
  fireEvent.click(screen.getByRole("navigation", { name: "Risk Merkezi görünümleri" }).querySelector('a[href$="view=people"]')!);
  const signals = await screen.findByRole("region", { name: "Takip edilecek alanlar" });
  expect(within(signals).getByRole("link", { name: /Ayrılma riski/ })).toHaveAttribute("href", "/app/risk/attrition");
  expect(within(signals).getByRole("link", { name: /Tükenmişlik/ })).toHaveAttribute("href", "/app/risk/burnout");
  expect(within(signals).getByText("5 bekleyen onay · 14 açık iş")).toBeInTheDocument();
  expect(screen.getByText("18 çalışan · 2 yüksek ayrılma · 1 tükenmişlik")).toBeInTheDocument();
  expect(screen.getByText("74 / 100 · Yüksek risk")).toBeInTheDocument();
  expect(screen.getByText("Dönemsel risk eğilimini göster")).toBeInTheDocument();
  expect(screen.queryByRole("heading", { name: "İş gücü kapasitesi" })).not.toBeInTheDocument();
});

test("operasyon görünümü uyumu ve kapasiteyi gösterir, nabzı tekrar etmez", async () => {
  renderRisk();
  fireEvent.click(screen.getByRole("link", { name: "Operasyon ve uyum" }));
  const complianceSection = await screen.findByRole("region", { name: "Evrak ve uyum" });
  expect(within(complianceSection).getByRole("link", { name: /Eksik evraklar/ })).toHaveAttribute("href", "/app/risk/compliance");
  expect(screen.getByText("18 yaklaşan son tarih · Denetim hazırlığı: Orta")).toBeInTheDocument();
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

test("eksik değerler sıfır risk gibi gösterilmez", async () => {
  stubApi({
    "/api/dashboard/metrics": { ...metrics, riskScore: null, riskTrend: [], departmentRisk: [], talentCapacity: [] },
    "/api/dashboard/manager-load": managerLoad,
    "/api/dashboard/employee-voice": voice,
    "/api/dashboard/compliance": compliance,
    "/api/actions": [],
  });
  renderRisk();
  expect(await screen.findByRole("heading", { name: "Henüz risk verisi yok" })).toBeInTheDocument();
  expect(screen.getByText("Şu anda açık aksiyon yok. Yeni bir iş oluştuğunda burada görünecek.")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("link", { name: "İnsan ve ekip" }));
  expect(await screen.findByText("Bu kapsamda departman verisi yok.")).toBeInTheDocument();
  fireEvent.click(screen.getByText("Dönemsel risk eğilimini göster"));
  expect(screen.getByText("Henüz karşılaştırılacak dönem verisi yok.")).toBeInTheDocument();
});
