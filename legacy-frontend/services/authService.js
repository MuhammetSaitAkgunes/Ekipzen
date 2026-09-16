const AUTH_STORAGE_KEY = "ekipzen-demo-session";

const getCurrentSession = () => {
  try {
    return JSON.parse(localStorage.getItem(AUTH_STORAGE_KEY) || "null");
  } catch {
    return null;
  }
};

const persistSession = (user) => {
  const session = {
    user,
    token: `demo-token-${user.role}`,
    createdAt: new Date().toISOString(),
  };
  localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(session));
  return session;
};

const login = (credentials = {}) => {
  const selectedRole = credentials.role || "hr-admin";
  return persistSession(demoUsers[selectedRole] || demoUsers["hr-admin"]);
};

const signup = (payload = {}) => {
  const user = {
    ...demoUsers["hr-admin"],
    name: payload.name || "İK Yöneticisi",
    email: payload.email || "ik@hrmaster.local",
    initials: (payload.name || "İK").slice(0, 2).toLocaleUpperCase("tr-TR"),
  };
  return persistSession(user);
};

const logout = () => localStorage.removeItem(AUTH_STORAGE_KEY);
const getCurrentUser = () => getCurrentSession()?.user || null;
const isAuthenticated = () => Boolean(getCurrentUser());

const hasRole = (roleOrRoles) => {
  const userRole = getCurrentUser()?.role;
  const roles = Array.isArray(roleOrRoles) ? roleOrRoles : [roleOrRoles];
  return roles.includes(userRole);
};

const switchDemoRole = (role) => {
  persistSession(demoUsers[role] || demoUsers["hr-admin"]);
  const root = document.getElementById("root");
  if (root) root.innerHTML = "";
  bootstrapApp?.(role === "employee" ? "overview" : "dashboard");
};

const getRoleLabel = (role) => (demoUsers[role] || demoUsers["hr-admin"]).roleLabel;

const canAccessRoute = (routeOrKey) => {
  const route = typeof routeOrKey === "string" ? findRoute(routeOrKey) : routeOrKey;
  if (!route) return false;
  if (route.public) return true;

  const role = getCurrentUser()?.role || "employee";
  return !route.roles?.length || route.roles.includes(role);
};

const canAccessPage = (page) => canAccessRoute(page);
