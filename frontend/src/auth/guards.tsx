import type { ReactNode } from "react";
import { Navigate, Outlet } from "react-router-dom";
import { useAuth } from "./AuthContext";
import { type AppRoute, type Role } from "../routes";
import { LOGIN_PATH, roleHomeFor } from "../paths";

export function RequireAuth() {
  const { user } = useAuth();
  if (!user) return <Navigate to={LOGIN_PATH} replace />;
  return <Outlet />;
}

export function PublicOnly({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  if (user) return <Navigate to={roleHomeFor(user.role)} replace />;
  return <>{children}</>;
}

/** Eski renderRestrictedRoute paritesi: yetkisiz rol redirect edilmez, kilit ekranı görür. */
export function RouteGate({ route, children }: { route: AppRoute; children: ReactNode }) {
  const { user } = useAuth();
  if (user && !route.roles.includes(user.role as Role)) {
    return (
      <section className="surface empty-state">
        <i aria-hidden="true" className="fa-solid fa-lock" />
        <h2>Bu alan için yetki gerekli</h2>
        <p>Bu rol ile seçilen modüle erişim kapalı. Yetki kontrolü backend policy katmanında da uygulanır.</p>
      </section>
    );
  }
  return <>{children}</>;
}
