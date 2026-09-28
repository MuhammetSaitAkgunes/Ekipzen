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
const HomePageApp = lazy(() => import("./features/home/HomePage").then((m) => ({ default: m.HomePage })));
const InboxPage = lazy(() => import("./features/inbox/InboxPage").then((m) => ({ default: m.InboxPage })));
const PersonnelProfilePage = lazy(() => import("./features/personnel/PersonnelProfilePage").then((m) => ({ default: m.PersonnelProfilePage })));
const GuidanceJourneyPage = lazy(() => import("./features/guidance/GuidanceJourneyPage").then((m) => ({ default: m.GuidanceJourneyPage })));

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

// Route permissions remain independent of the navigation presentation.
export const appRoutes: AppRoute[] = [
  { key: "home", path: appPath("/home"), title: "Ana Sayfa", eyebrow: "Ekipzen", navKey: "home", roles: ALL },
  { key: "inbox", path: appPath("/inbox"), title: "İş Kutusu", eyebrow: "Ekipzen", navKey: "inbox", roles: ALL },
  { key: "personnel-new", path: appPath("/personnel/new"), title: "Çalışan ekle", eyebrow: "Çalışanlar", navKey: "personnel", roles: ["hr-admin"] },
  { key: "personnel-profile", path: appPath("/personnel/:employeeId"), title: "Çalışan profili", eyebrow: "Çalışanlar", navKey: "personnel", roles: MGMT },
  { key: "dashboard", path: appPath("/dashboard"), title: "Risk Merkezi", eyebrow: "Ekipzen", navKey: "dashboard", roles: MGMT },
  { key: "overview", path: appPath("/overview"), title: "Genel Durum", eyebrow: "Ekipzen", navKey: "overview", roles: ALL },
  { key: "actions", path: appPath("/actions"), title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "inbox", roles: ALL },
  { key: "personnel", path: appPath("/personnel"), title: "Çalışanlar", eyebrow: "Ekipzen", navKey: "personnel", roles: MGMT },
  { key: "guidance", path: appPath("/guidance"), title: "Öğrenme Yolculuğum", eyebrow: "İK Rehberi", navKey: "guidance", roles: MGMT },
  { key: "recruitment", path: appPath("/recruitment"), title: "İşe Alım", eyebrow: "Ekipzen", navKey: "recruitment", roles: ["hr-admin"] },
  { key: "attendance", path: appPath("/attendance"), title: "Mesai & Puantaj", eyebrow: "Ekipzen", navKey: "attendance", roles: MGMT },
  { key: "leaves", path: appPath("/leaves"), title: "İzinler", eyebrow: "Ekipzen", navKey: "leaves", roles: ALL },
  { key: "payroll", path: appPath("/payroll"), title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin", "employee"] },
  { key: "payroll-calculator", path: appPath("/payroll/calculator"), title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"] },
  { key: "payroll-settings", path: appPath("/payroll/settings"), title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"] },
  { key: "manager", path: appPath("/manager"), title: "İzin Analizleri", eyebrow: "İzinler", navKey: "leaves", roles: MGMT },
  { key: "settings", path: appPath("/settings"), title: "Ayarlar", eyebrow: "Ekipzen", navKey: "settings", roles: ["hr-admin"] },
  { key: "attrition-risk", path: appPath("/risk/attrition"), title: "Ayrılma Riski Detayı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "burnout-risk", path: appPath("/risk/burnout"), title: "Tükenmişlik Sinyali", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "manager-load", path: appPath("/risk/manager-load"), title: "Yönetici Yükü", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "action-center", path: appPath("/risk/action-center"), title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "inbox", roles: MGMT },
  { key: "employee-voice", path: appPath("/risk/employee-voice"), title: "Çalışan Nabzı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
  { key: "compliance-risk", path: appPath("/risk/compliance"), title: "Uyum Risk Merkezi", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: MGMT },
];

export const navIcons: Record<string, string> = {
  home: "fa-house", inbox: "fa-inbox",
  dashboard: "fa-shield-heart", overview: "fa-chart-line", actions: "fa-list-check",
  personnel: "fa-user-tie", guidance: "fa-book-open", recruitment: "fa-briefcase", attendance: "fa-clock",
  leaves: "fa-plane-departure", payroll: "fa-money-check-dollar",
  manager: "fa-chart-pie", settings: "fa-gear",
};


// Sayfa component eşlemesi: sonraki dilimler PlaceholderPage'i gerçek sayfayla değiştirir.
// Lazy bileşenler LazyExoticComponent tipindedir; sözlük ComponentType tutar.
const pageFor: Record<string, ComponentType> = {
  home: HomePageApp, inbox: InboxPage, "personnel-new": PersonnelProfilePage, "personnel-profile": PersonnelProfilePage,
  overview: OverviewPage,
  actions: InboxPage,
  "action-center": InboxPage,
  dashboard: RiskCenterPage,
  "attrition-risk": AttritionDetailPage,
  "burnout-risk": BurnoutDetailPage,
  "manager-load": ManagerLoadPage,
  "employee-voice": EmployeeVoicePage,
  "compliance-risk": CompliancePage,
  personnel: PersonnelPage,
  guidance: GuidanceJourneyPage,
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
  if (route.key === "manager") return <RouteGate route={route}><Navigate to={appPath("/leaves?tab=analysis")} replace /></RouteGate>;
  if (route.key === "actions" || route.key === "action-center") return <RouteGate route={route}><Navigate to={appPath("/inbox?tab=actions")} replace /></RouteGate>;
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

export type NavigationItem = { key: string; path: string; label: string; employeeLabel?: string; icon: string; roles: Role[] };
export const navigation: { label: string; items: NavigationItem[] }[] = [
  { label: "Çalışma alanı", items: [
    { key: "home", path: appPath("/home"), label: "Ana Sayfa", icon: "fa-house", roles: ALL },
    { key: "inbox", path: appPath("/inbox"), label: "İş Kutusu", icon: "fa-inbox", roles: ALL },
  ] },
  { label: "İnsan kaynakları", items: [
    { key: "personnel", path: appPath("/personnel"), label: "Çalışanlar", icon: "fa-user-group", roles: MGMT },
    { key: "guidance", path: appPath("/guidance"), label: "Öğrenme Yolculuğum", icon: "fa-book-open", roles: MGMT },
    { key: "leaves", path: appPath("/leaves"), label: "İzinler", employeeLabel: "İzinlerim", icon: "fa-calendar-days", roles: ALL },
    { key: "attendance", path: appPath("/attendance"), label: "Mesai ve Puantaj", icon: "fa-clock", roles: MGMT },
    { key: "recruitment", path: appPath("/recruitment"), label: "İşe Alım", icon: "fa-briefcase", roles: ["hr-admin"] },
    { key: "payroll", path: appPath("/payroll"), label: "Bordro", employeeLabel: "Bordrolarım", icon: "fa-wallet", roles: ["hr-admin", "employee"] },
  ] },
  { label: "Analiz", items: [{ key: "dashboard", path: appPath("/dashboard"), label: "Risk Merkezi", icon: "fa-chart-simple", roles: MGMT }] },
  { label: "", items: [{ key: "settings", path: appPath("/settings"), label: "Ayarlar", icon: "fa-gear", roles: ["hr-admin"] }] },
];
