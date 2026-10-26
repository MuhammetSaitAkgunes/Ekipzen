import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { appPath } from "../../paths";

export function GuidanceNavigation({ history = false }: { history?: boolean }) {
  const { user } = useAuth();
  const location = useLocation();
  const management = user?.role === "hr-admin" || user?.role === "manager";
  return <nav className="ez-guide-navigation" aria-label="İK rehberi bölümleri">
    {management && <Link to={appPath("/guidance")} aria-current={!history && location.pathname === appPath("/guidance") ? "page" : undefined}>Başlangıç</Link>}
    <Link to={appPath("/guidance/catalog")} aria-current={!history && location.pathname === appPath("/guidance/catalog") ? "page" : undefined}>Süreçler ve içerikler</Link>
    <Link to={appPath(management ? "/guidance?tab=history" : "/guidance/catalog?tab=history")} aria-current={history ? "page" : undefined}>Öğrenme geçmişim</Link>
  </nav>;
}
