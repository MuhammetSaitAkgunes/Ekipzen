import { Suspense, lazy, type ComponentType } from "react";
import { Navigate, useLocation, type RouteObject } from "react-router-dom";
import { PageLoading } from "./features/shared/PageState";
import { LoginPage } from "./auth/LoginPage";
import { AcceptInvitePage } from "./auth/AcceptInvitePage";
import { PublicOnly, RequireAuth, RouteGate } from "./auth/guards";
import { PlaceholderPage } from "./pages/PlaceholderPage";
import { AppShell } from "./layout/AppShell";
import { useAuth } from "./auth/AuthContext";
import { MarketingLayout } from "./marketing/MarketingLayout";
import { HomePage } from "./marketing/pages/HomePage";
import { NotFoundPage } from "./marketing/pages/NotFoundPage";
import { LEGACY_APP_ROOT_PATHS, mapLegacyPath } from "./legacyHash";
import { APP_BASE, INVITE_PATH, LOGIN_PATH, appPath, roleHomeFor } from "./paths";

export { APP_BASE, INVITE_PATH, LOGIN_PATH, appPath, roleHomeFor } from "./paths";
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

const FeaturesPage = lazy(() => import("./marketing/pages/FeaturesPage").then((m) => ({ default: m.FeaturesPage })));
const PricingPage = lazy(() => import("./marketing/pages/PricingPage").then((m) => ({ default: m.PricingPage })));
const AboutPage = lazy(() => import("./marketing/pages/AboutPage").then((m) => ({ default: m.AboutPage })));
const SecurityPage = lazy(() => import("./marketing/pages/SecurityPage").then((m) => ({ default: m.SecurityPage })));
const KvkkPage = lazy(() => import("./marketing/pages/KvkkPage").then((m) => ({ default: m.KvkkPage })));
const ContactPage = lazy(() => import("./marketing/pages/ContactPage").then((m) => ({ default: m.ContactPage })));

const marketingPage = (Page: ComponentType) => (
  <Suspense fallback={<div className="ez-page-loading" aria-busy="true" />}><Page /></Suspense>
);

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
  { key: "dashboard", path: appPath("/dashboard"), title: "Risk Merkezi", eyebrow: "Ekipzen", navKey: "dashboard", roles: MGMT },
  { key: "overview", path: appPath("/overview"), title: "Genel Durum", eyebrow: "Ekipzen", navKey: "overview", roles: ALL },
  { key: "actions", path: appPath("/actions"), title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "actions", roles: ALL },
  { key: "personnel", path: appPath("/personnel"), title: "Personel Yönetimi", eyebrow: "Ekipzen", navKey: "personnel", roles: MGMT },
  { key: "recruitment", path: appPath("/recruitment"), title: "İşe Alım", eyebrow: "Ekipzen", navKey: "recruitment", roles: ["hr-admin"] },
  { key: "attendance", path: appPath("/attendance"), title: "Mesai & Puantaj", eyebrow: "Ekipzen", navKey: "attendance", roles: MGMT },
  { key: "leaves", path: appPath("/leaves"), title: "İzinlerim", eyebrow: "Ekipzen", navKey: "leaves", roles: ALL },
  { key: "payroll", path: appPath("/payroll"), title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin", "employee"] },
  { key: "payroll-calculator", path: appPath("/payroll/calculator"), title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"] },
  { key: "payroll-settings", path: appPath("/payroll/settings"), title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"] },
  { key: "manager", path: appPath("/manager"), title: "Yönetici Konsolu", eyebrow: "Ekipzen", navKey: "manager", roles: MGMT },
  { key: "settings", path: appPath("/settings"), title: "Ayarlar", eyebrow: "Ekipzen", navKey: "settings", roles: ["hr-admin"] },
  { key: "attrition-risk", path: appPath("/risk/attrition"), title: "Ayrılma Riski Detayı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "burnout-risk", path: appPath("/risk/burnout"), title: "Tükenmişlik Sinyali", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "manager-load", path: appPath("/risk/manager-load"), title: "Yönetici Yükü", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "action-center", path: appPath("/risk/action-center"), title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "actions", roles: MGMT },
  { key: "employee-voice", path: appPath("/risk/employee-voice"), title: "Çalışan Nabzı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "compliance-risk", path: appPath("/risk/compliance"), title: "Uyum Risk Merkezi", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
];

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

// Eski yol → yeni yol; sorgu dizesi korunur (davet linklerindeki email/token).
function LegacyRedirect() {
  const location = useLocation();
  return <Navigate to={`${mapLegacyPath(location.pathname)}${location.search}`} replace />;
}

function AppHomeRedirect() {
  const { user } = useAuth();
  return <Navigate to={roleHomeFor(user?.role)} replace />;
}

export function buildRouteObjects(): RouteObject[] {
  return [
    {
      element: <MarketingLayout />,
      children: [
        { path: "/", element: <HomePage /> },
        { path: "/ozellikler", element: marketingPage(FeaturesPage) },
        { path: "/fiyatlandirma", element: marketingPage(PricingPage) },
        { path: "/hakkimizda", element: marketingPage(AboutPage) },
        { path: "/guvenlik", element: marketingPage(SecurityPage) },
        { path: "/kvkk-aydinlatma", element: marketingPage(KvkkPage) },
        { path: "/iletisim", element: marketingPage(ContactPage) },
        { path: "*", element: <NotFoundPage /> },
      ],
    },
    { path: LOGIN_PATH, element: <PublicOnly><LoginPage /></PublicOnly> },
    { path: INVITE_PATH, element: <PublicOnly><AcceptInvitePage /></PublicOnly> },
    { path: "/login", element: <LegacyRedirect /> },
    { path: "/accept-invite", element: <LegacyRedirect /> },
    // Self-servis kayıt sayfalarının İKİSİ DE kaldırıldı. Şirket hesabı yalnız
    // operatör tarafından, platform anahtarıyla açılır (bkz. TenancyController
    // xmldoc). Eski yer imleri ve paylaşılmış linkler demo talebine düşer.
    { path: "/signup", element: <Navigate to="/iletisim" replace /> },
    { path: "/register-company", element: <Navigate to="/iletisim" replace /> },
    ...LEGACY_APP_ROOT_PATHS.map((root) => ({ path: `${root}/*`, element: <LegacyRedirect /> })),
    {
      path: APP_BASE,
      element: <RequireAuth />,
      children: [
        { index: true, element: <AppHomeRedirect /> },
        {
          element: <AppShell />,
          children: appRoutes.map((route) => ({
            path: route.path,
            element: <GatedPage route={route} />,
          })),
        },
        { path: "*", element: <AppHomeRedirect /> },
      ],
    },
  ];
}
