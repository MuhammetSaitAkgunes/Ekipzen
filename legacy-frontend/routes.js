const AppRoutes = [
  { key: "login", path: "/login", title: "Giriş", public: true, authMode: "login" },
  { key: "signup", path: "/signup", title: "Hesap Oluştur", public: true, authMode: "signup" },

  { key: "dashboard", path: "/dashboard", title: "Risk Merkezi", eyebrow: "Ekipzen", navKey: "dashboard", roles: ["hr-admin", "manager"], render: () => Dashboard() },
  { key: "overview", path: "/overview", title: "Genel Durum", eyebrow: "Ekipzen", navKey: "overview", roles: ["hr-admin", "manager", "employee"], render: () => OverviewDashboard() },
  { key: "actions", path: "/actions", title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "actions", roles: ["hr-admin", "manager", "employee"], render: () => ActionsCenter() },
  { key: "personnel", path: "/personnel", title: "Personel Yönetimi", eyebrow: "Ekipzen", navKey: "personnel", roles: ["hr-admin", "manager"], render: () => Personnel() },
  { key: "recruitment", path: "/recruitment", title: "İşe Alım", eyebrow: "Ekipzen", navKey: "recruitment", roles: ["hr-admin"], render: () => Recruitment() },
  { key: "attendance", path: "/attendance", title: "Mesai & Puantaj", eyebrow: "Ekipzen", navKey: "attendance", roles: ["hr-admin", "manager"], render: () => Attendance() },
  { key: "leaves", path: "/leaves", title: "İzinlerim", eyebrow: "Ekipzen", navKey: "leaves", roles: ["hr-admin", "manager", "employee"], render: () => Leaves() },
  { key: "payroll", path: "/payroll", title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin", "employee"], render: () => Payroll() },
  { key: "payroll-calculator", path: "/payroll/calculator", title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"], render: () => Payroll(), afterRender: () => switchPayrollTab("payroll-calculator") },
  { key: "payroll-settings", path: "/payroll/settings", title: "Bordro", eyebrow: "Ekipzen", navKey: "payroll", roles: ["hr-admin"], render: () => Payroll(), afterRender: () => switchPayrollTab("payroll-settings") },
  { key: "manager", path: "/manager", title: "Yönetici Konsolu", eyebrow: "Ekipzen", navKey: "manager", roles: ["hr-admin", "manager"], render: () => ManagerDashboard() },
  { key: "settings", path: "/settings", title: "Ayarlar", eyebrow: "Ekipzen", navKey: "settings", roles: ["hr-admin"], render: () => Settings() },

  { key: "attrition-risk", path: "/risk/attrition", title: "Ayrılma Riski Detayı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: ["hr-admin", "manager"], render: () => AttritionRiskDetail() },
  { key: "burnout-risk", path: "/risk/burnout", title: "Tükenmişlik Sinyali", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: ["hr-admin", "manager"], render: () => BurnoutRiskDetail() },
  { key: "manager-load", path: "/risk/manager-load", title: "Yönetici Yükü", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: ["hr-admin", "manager"], render: () => ManagerLoadDetail() },
  // Tek aksiyon kaynağı: eski /risk/action-center rotası geriye dönük uyum için
  // korunur, ancak artık Global Aksiyon Merkezi'ne yönlenir.
  { key: "action-center", path: "/risk/action-center", title: "Aksiyonlar", eyebrow: "Ekipzen", navKey: "actions", roles: ["hr-admin", "manager"], render: () => ActionsCenter() },
  { key: "employee-voice", path: "/risk/employee-voice", title: "Çalışan Nabzı", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: ["hr-admin", "manager"], render: () => EmployeeVoiceDetail() },
  { key: "compliance-risk", path: "/risk/compliance", title: "Uyum Risk Merkezi", eyebrow: "Risk Merkezi", navKey: "dashboard", roles: ["hr-admin", "manager"], render: () => ComplianceRiskDetail() },
];

const DefaultProtectedRoute = "/dashboard";
const DefaultPublicRoute = "/login";

// Rol bazlı ana sayfa: ilk ekran her rol için uygun ağırlıkta açılır.
const RoleHomeRoutes = {
  "hr-admin": "/dashboard",
  manager: "/dashboard",
  employee: "/overview",
};

const getDefaultProtectedRoute = () => {
  const role = typeof getCurrentUser === "function" ? getCurrentUser()?.role : null;
  return RoleHomeRoutes[role] || DefaultProtectedRoute;
};

const getHashPath = () => {
  const hash = window.location.hash.replace(/^#/, "");
  return hash.startsWith("/") ? hash : "";
};

const setHashPath = (path) => {
  const nextHash = `#${path}`;
  if (window.location.hash !== nextHash) {
    window.location.hash = path;
    return true;
  }
  return false;
};

const findRoute = (routeOrPath = DefaultProtectedRoute) => {
  const normalized = String(routeOrPath || "").trim();
  const path = normalized.startsWith("/") ? normalized : `/${normalized}`;
  return (
    AppRoutes.find((route) => route.path === path) ||
    AppRoutes.find((route) => route.key === normalized) ||
    AppRoutes.find((route) => route.key === normalized.replace(/^\//, "")) ||
    null
  );
};

const getRouteByPath = (path) => findRoute(path);
const getRoutePath = (routeOrPath) => findRoute(routeOrPath)?.path || DefaultProtectedRoute;
const getNavigationRoutes = () => AppRoutes.filter((route) => route.navKey === route.key && !route.public);
