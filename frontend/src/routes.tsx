import { Suspense, lazy, type ComponentType } from "react";
import { Navigate, type RouteObject } from "react-router-dom";
import { PageLoading } from "./features/shared/PageState";
import { LoginPage } from "./auth/LoginPage";
import { AcceptInvitePage } from "./auth/AcceptInvitePage";
import { PublicOnly, RequireAuth, RouteGate } from "./auth/guards";
import { PlaceholderPage } from "./pages/PlaceholderPage";
import { AppShell } from "./layout/AppShell";
const ActionsPage = lazy(() => import("./features/actions/ActionsPage").then((m) => ({ default: m.ActionsPage })));
const OverviewPage = lazy(() => import("./features/overview/OverviewPage").then((m) => ({ default: m.OverviewPage })));
const RiskCenterPage = lazy(() => import("./features/dashboard/RiskCenterPage").then((m) => ({ default: m.RiskCenterPage })));
const AttritionDetailPage = lazy(() => import("./features/dashboard/AttritionDetailPage").then((m) => ({ default: m.AttritionDetailPage })));
const BurnoutDetailPage = lazy(() => import("./features/dashboard/BurnoutDetailPage").then((m) => ({ default: m.BurnoutDetailPage })));
const ManagerLoadPage = lazy(() => import("./features/dashboard/ManagerLoadPage").then((m) => ({ default: m.ManagerLoadPage })));
const EmployeeVoicePage = lazy(() => import("./features/dashboard/EmployeeVoicePage").then((m) => ({ default: m.EmployeeVoicePage })));
const CompliancePage = lazy(() => import("./features/compliance/CompliancePage").then((m) => ({ default: m.CompliancePage })));
const PersonnelPage = lazy(() => import("./features/personnel/PersonnelPage").then((m) => ({ default: m.PersonnelPage })));
const RecruitmentPage = lazy(() => import("./features/recruitment/RecruitmentPage").then((m) => ({ default: m.RecruitmentPage })));
const SettingsPage = lazy(() => import("./features/settings/SettingsPage").then((m) => ({ default: m.SettingsPage })));
const LeavesPage = lazy(() => import("./features/leaves/LeavesPage").then((m) => ({ default: m.LeavesPage })));
const AttendancePage = lazy(() => import("./features/attendance/AttendancePage").then((m) => ({ default: m.AttendancePage })));
const PayrollPeriodPage = lazy(() => import("./features/payroll/PayrollPage").then((m) => ({ default: m.PayrollPeriodPage })));
const PayrollCalculatorPage = lazy(() => import("./features/payroll/PayrollPage").then((m) => ({ default: m.PayrollCalculatorPage })));
const PayrollSettingsPage = lazy(() => import("./features/payroll/PayrollPage").then((m) => ({ default: m.PayrollSettingsPage })));
const ManagerPage = lazy(() => import("./features/manager/ManagerPage").then((m) => ({ default: m.ManagerPage })));

export type Role = "hr-admin" | "manager" | "employee";

export type AppRoute = {
  key: string;
  path: string;
  title: string;
  eyebrow: string;
  navKey: string;
  roles: Role[];
};

const ALL: Role[] = ["hr-admin", "manager", "employee"];
const MGMT: Role[] = ["hr-admin", "manager"];

// routes.js ile birebir (sıra dahil).
export const appRoutes: AppRoute[] = [
  { key: "dashboard", path: "/dashboard", title: "Risk Merkezi", eyebrow: "İK Pro", navKey: "dashboard", roles: MGMT },
  { key: "overview", path: "/overview", title: "Genel Durum", eyebrow: "İK Pro", navKey: "overview", roles: ALL },
  { key: "actions", path: "/actions", title: "Aksiyonlar", eyebrow: "İK Pro", navKey: "actions", roles: ALL },
  { key: "personnel", path: "/personnel", title: "Personel Yönetimi", eyebrow: "İK Pro", navKey: "personnel", roles: MGMT },
  { key: "recruitment", path: "/recruitment", title: "İşe Alım", eyebrow: "İK Pro", navKey: "recruitment", roles: ["hr-admin"] },
  { key: "attendance", path: "/attendance", title: "Mesai & Puantaj", eyebrow: "İK Pro", navKey: "attendance", roles: MGMT },
  { key: "leaves", path: "/leaves", title: "İzinlerim", eyebrow: "İK Pro", navKey: "leaves", roles: ALL },
  { key: "payroll", path: "/payroll", title: "Bordro", eyebrow: "İK Pro", navKey: "payroll", roles: ["hr-admin", "employee"] },
  { key: "payroll-calculator", path: "/payroll/calculator", title: "Bordro", eyebrow: "İK Pro", navKey: "payroll", roles: ["hr-admin"] },
  { key: "payroll-settings", path: "/payroll/settings", title: "Bordro", eyebrow: "İK Pro", navKey: "payroll", roles: ["hr-admin"] },
  { key: "manager", path: "/manager", title: "Yönetici Konsolu", eyebrow: "İK Pro", navKey: "manager", roles: MGMT },
  { key: "settings", path: "/settings", title: "Ayarlar", eyebrow: "İK Pro", navKey: "settings", roles: ["hr-admin"] },
  { key: "attrition-risk", path: "/risk/attrition", title: "Ayrılma Riski Detayı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "burnout-risk", path: "/risk/burnout", title: "Tükenmişlik Sinyali", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "manager-load", path: "/risk/manager-load", title: "Yönetici Yükü", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "action-center", path: "/risk/action-center", title: "Aksiyonlar", eyebrow: "İK Pro", navKey: "actions", roles: MGMT },
  { key: "employee-voice", path: "/risk/employee-voice", title: "Çalışan Nabzı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "compliance-risk", path: "/risk/compliance", title: "Uyum Risk Merkezi", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
];

export const roleHomeFor = (role: string | null | undefined): string =>
  role === "employee" ? "/overview" : "/dashboard";

// layout.js ile birebir.
export const navIcons: Record<string, string> = {
  dashboard: "fa-shield-heart", overview: "fa-chart-line", actions: "fa-list-check",
  personnel: "fa-user-tie", recruitment: "fa-briefcase", attendance: "fa-clock",
  leaves: "fa-plane-departure", payroll: "fa-money-check-dollar",
  manager: "fa-chart-pie", settings: "fa-gear",
};

export const navGroups = [
  { label: "Ana Sayfa", keys: ["dashboard", "overview", "actions"] },
  { label: "Çekirdek İK", keys: ["personnel", "recruitment", "attendance", "leaves"] },
  { label: "Bordro", keys: ["payroll"] },
  { label: "Yönetim", keys: ["manager", "settings"] },
];

// Sayfa component eşlemesi: sonraki dilimler PlaceholderPage'i gerçek sayfayla değiştirir.
// Lazy bileşenler LazyExoticComponent tipindedir; sözlük ComponentType tutar.
const pageFor: Record<string, ComponentType> = {
  overview: OverviewPage,
  actions: ActionsPage,
  "action-center": ActionsPage,
  dashboard: RiskCenterPage,
  "attrition-risk": AttritionDetailPage,
  "burnout-risk": BurnoutDetailPage,
  "manager-load": ManagerLoadPage,
  "employee-voice": EmployeeVoicePage,
  "compliance-risk": CompliancePage,
  personnel: PersonnelPage,
  recruitment: RecruitmentPage,
  leaves: LeavesPage,
  attendance: AttendancePage,
  payroll: PayrollPeriodPage,
  "payroll-calculator": PayrollCalculatorPage,
  "payroll-settings": PayrollSettingsPage,
  settings: SettingsPage,
  manager: ManagerPage,
};

// Component JSX olarak render edilir (düz fonksiyon çağrısı hook kurallarını bozar).
function GatedPage({ route }: { route: AppRoute }) {
  const Page = pageFor[route.key] ?? PlaceholderPage;
  return (
    <RouteGate route={route}>
      {/* Sayfalar rota bazlı bölünür; ilk yüklemede yalnız açılan sayfa iner. */}
      <Suspense fallback={<PageLoading />}>
        <Page />
      </Suspense>
    </RouteGate>
  );
}

export function buildRouteObjects(): RouteObject[] {
  return [
    { path: "/login", element: <PublicOnly><LoginPage /></PublicOnly> },
    { path: "/accept-invite", element: <PublicOnly><AcceptInvitePage /></PublicOnly> },
    // Self-servis kayıt sayfalarının İKİSİ DE kaldırıldı. Şirket hesabı yalnız
    // operatör tarafından, platform anahtarıyla açılır (bkz. TenancyController
    // xmldoc): kiracı başına veritabanına geçildiğinde kayıt bir satır değil bir
    // VERİTABANI oluşturur; bunu anonim bir uçtan tetiklenebilir bırakmak kaynak
    // tüketme açığıdır. Eski yer imleri ve paylaşılmış linkler girişe düşer.
    { path: "/signup", element: <Navigate to="/login" replace /> },
    { path: "/register-company", element: <Navigate to="/login" replace /> },
    {
      element: <RequireAuth />,
      children: [
        {
          element: <AppShell />,
          children: appRoutes.map((route) => ({
            path: route.path,
            element: <GatedPage route={route} />,
          })),
        },
      ],
    },
    { path: "*", element: <PublicOnly><LoginPage /></PublicOnly> },
  ];
}
