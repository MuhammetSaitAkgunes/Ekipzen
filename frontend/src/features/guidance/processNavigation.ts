import type { Role } from "../../routes";

const routes: Record<string, { path: string; roles: Role[] }> = {
  company: { path: "/settings", roles: ["hr-admin"] },
  recruitment: { path: "/recruitment", roles: ["hr-admin"] },
  "personnel-entry": { path: "/personnel/new", roles: ["hr-admin"] },
  personnel: { path: "/personnel", roles: ["hr-admin", "manager"] },
  leave: { path: "/inbox?tab=approvals", roles: ["hr-admin", "manager"] },
  attendance: { path: "/attendance", roles: ["hr-admin", "manager"] },
  payroll: { path: "/payroll", roles: ["hr-admin"] },
  compliance: { path: "/risk/compliance", roles: ["hr-admin", "manager"] },
  offboarding: { path: "/personnel", roles: ["hr-admin"] },
  daily: { path: "/inbox?tab=daily", roles: ["hr-admin", "manager"] },
};
export function guidanceProcessPath(processKey: string, role: string | null | undefined) {
  const route = routes[processKey];
  return route && route.roles.some(item => item === role) ? route.path : null;
}
