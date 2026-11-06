import { useEffect, useRef, useState } from "react";
import { Link, NavLink, Outlet, matchPath, useLocation, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { apiDownload, apiFetch } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import { appRoutes, navigation, type Role } from "../routes";
import { LOGIN_PATH, appPath, roleHomeFor } from "../paths";
import { WriteRecoveryNotice } from "./WriteRecoveryNotice";
import { GuidanceWorkspaceProvider, useGuidanceWorkspace } from "../features/guidance/GuidanceWorkspace";
import { GlobalSearch } from "./GlobalSearch";
import { useToast } from "./ToastProvider";
import { Logo, usePageMeta } from "../ui";
import { useDialog } from "../ui/useDialog";

export function AppShell() {
  return <GuidanceWorkspaceProvider><AppShellFrame /></GuidanceWorkspaceProvider>;
}

function AppShellFrame() {
  const guidance = useGuidanceWorkspace();
  const { user, login, logout } = useAuth();
  const { showToast } = useToast();
  const navigate = useNavigate();
  const location = useLocation();
  const [theme, setTheme] = useState(() => localStorage.getItem("ekipzen-theme") === "dark" ? "dark" : "light");
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem("ekipzen-sidebar") === "collapsed");
  const [compactViewport, setCompactViewport] = useState(() => window.matchMedia?.("(max-width: 900px)").matches ?? false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const sidebarRef = useRef<HTMLElement>(null);
  const profileRef = useRef<HTMLDivElement>(null);
  const current = appRoutes.find((route) => matchPath({ path: route.path, end: true }, location.pathname));
  const role = (user?.role ?? "employee") as Role;
  usePageMeta(current?.title ?? "Ekipzen");
  useDialog(sidebarRef, mobileOpen, () => setMobileOpen(false));
  useDialog(profileRef, profileOpen, () => setProfileOpen(false));

  useEffect(() => {
    const media = window.matchMedia?.("(max-width: 900px)");
    if (!media) return;
    const changed = () => { setCompactViewport(media.matches); if (!media.matches) setMobileOpen(false); };
    media.addEventListener("change", changed);
    return () => media.removeEventListener("change", changed);
  }, []);
  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
    localStorage.setItem("ekipzen-theme", theme);
    return () => document.documentElement.removeAttribute("data-theme");
  }, [theme]);
  useEffect(() => { localStorage.setItem("ekipzen-sidebar", collapsed ? "collapsed" : "expanded"); }, [collapsed]);
  useEffect(() => { setMobileOpen(false); setProfileOpen(false); }, [location.pathname, location.search]);
  useEffect(() => {
    const closeOutside = (event: PointerEvent) => {
      if (profileRef.current && !profileRef.current.contains(event.target as Node)) setProfileOpen(false);
    };
    document.addEventListener("pointerdown", closeOutside);
    return () => document.removeEventListener("pointerdown", closeOutside);
  }, []);
  useEffect(() => {
    if (!mobileOpen) return;
    const main = document.getElementById("main-content");
    const header = document.querySelector(".header");
    main?.setAttribute("inert", ""); header?.setAttribute("inert", "");
    return () => { main?.removeAttribute("inert"); header?.removeAttribute("inert"); };
  }, [mobileOpen]);

  const badge = useQuery({ queryKey: ["actions", "badge"], queryFn: () => apiFetch<{ openCount: number }>("/actions/badge") });
  const count = badge.isSuccess ? badge.data.openCount : null;
  const switchDemoRole = async (nextRole: string) => {
    if (!import.meta.env.DEV) return;
    const emails: Record<string, string> = { "hr-admin": "ik@hrmaster.local", manager: "ece.arslan@hrmaster.local", employee: "ahmet.yilmaz@hrmaster.local" };
    try { await login(emails[nextRole], "demo123"); navigate(roleHomeFor(nextRole)); }
    catch { showToast("Rol değiştirilemedi.", "error"); }
  };
  const handleDataExport = async () => {
    try {
      const { blob, fileName } = await apiDownload("/me/data-export");
      const url = URL.createObjectURL(blob); const link = document.createElement("a");
      link.href = url; link.download = fileName ?? "ekipzen-verilerim.json"; link.click(); URL.revokeObjectURL(url);
      showToast("Verileriniz indirildi.", "success");
    } catch { showToast("Veriler indirilemedi. Lütfen tekrar deneyin.", "error"); }
  };
  const toggleNavigation = () => {
    if (window.matchMedia("(max-width: 900px)").matches) setMobileOpen(true);
    else setCollapsed((value) => !value);
  };

  return (
    <div className={`app-container ez-workspace ${collapsed ? "sidebar-collapsed" : ""} ${mobileOpen ? "mobile-nav-open" : ""}`} data-app-theme={theme}>
      <a className="ez-skip" href="#main-content">İçeriğe geç</a>
      {mobileOpen && <div className="ez-nav-backdrop" onClick={() => setMobileOpen(false)} />}
      <aside ref={sidebarRef} id="app-navigation" className="sidebar" aria-label="Uygulama menüsü" role={mobileOpen ? "dialog" : undefined} aria-modal={mobileOpen || undefined}>
        <Link to={appPath("/home")} className="brand"><Logo variant="mark" tone={theme === "dark" ? "light" : "dark"} /><div><strong>Ekipzen<span className="ez-brand-dot">.</span></strong><span>{user?.tenantName || "Birlikte, daha iyi."}</span></div></Link>
        <button className="btn-icon ez-mobile-close" onClick={() => setMobileOpen(false)} aria-label="Menüyü kapat"><i className="fa-solid fa-xmark" aria-hidden="true" /></button>
        <nav className="ez-navigation" aria-label="Ana gezinme">
          {navigation.map((group) => {
            const items = group.items.filter((item) => item.roles.includes(role) && (item.key !== "guidance-catalog" || role === "employee"));
            return items.length > 0 && <div className={`ez-nav-group ${group.label === "" ? "ez-nav-bottom" : ""}`} key={group.label}>
              {group.label && <div className="nav-group-label">{group.label}</div>}
              {items.map((item) => <NavLink key={item.key} to={item.path} title={role === "employee" && item.employeeLabel ? item.employeeLabel : item.label} className={`nav-item ${current?.navKey === item.key ? "active" : ""}`}>
                <i className={`fa-solid ${item.icon}`} aria-hidden="true" /><span>{role === "employee" && item.employeeLabel ? item.employeeLabel : item.label}</span>
                {item.key === "inbox" && count !== null && count > 0 && <small className="ez-nav-count">{count}</small>}
              </NavLink>)}
            </div>;
          })}
        </nav>
      </aside>
      <header className="header">
        <button className="btn-icon shell-toggle" onClick={toggleNavigation} aria-controls="app-navigation" aria-expanded={compactViewport ? mobileOpen : !collapsed} aria-label="Menüyü aç veya daralt"><i className="fa-solid fa-bars" aria-hidden="true" /></button>
        <div className="ez-breadcrumb"><span>Çalışma alanı</span><i className="fa-solid fa-chevron-right" aria-hidden="true" /><span className="header-title">{current?.title ?? "Ekipzen"}</span></div>
        <GlobalSearch />
        <Link className="header-action-button" to={appPath("/inbox?tab=actions")} aria-label={`İş kutusu${count !== null ? `: ${count} açık aksiyon` : ""}`}><i className="fa-solid fa-inbox" aria-hidden="true" />{count !== null && count > 0 && <span>{count}</span>}</Link>
        <div className="ez-profile" ref={profileRef}>
          <button className="ez-profile-trigger" aria-expanded={profileOpen} aria-controls="profile-panel" aria-label="Profil menüsü" onClick={() => setProfileOpen(!profileOpen)}><span className="user-avatar">{user?.initials}</span><span className="ez-profile-name">{user?.name}<small>{user?.roleLabel}</small></span><i className="fa-solid fa-chevron-down" aria-hidden="true" /></button>
          {profileOpen && <div className="ez-profile-panel" id="profile-panel" role="dialog" aria-label="Hesap ve görünüm">
            <div className="ez-profile-info"><strong>{user?.name}</strong><span>{user?.roleLabel}</span></div>
            <button onClick={() => setTheme(theme === "dark" ? "light" : "dark")}><i className={`fa-solid ${theme === "dark" ? "fa-sun" : "fa-moon"}`} aria-hidden="true" />{theme === "dark" ? "Açık temaya geç" : "Koyu temaya geç"}</button>
            <button onClick={handleDataExport}><i className="fa-solid fa-file-export" aria-hidden="true" />Verilerimi indir</button>
            {import.meta.env.DEV && <label className="ez-demo-role">Demo rolü<select value={role} onChange={(event) => switchDemoRole(event.target.value)} aria-label="Demo rol değiştir"><option value="hr-admin">İK Admin</option><option value="manager">Yönetici</option><option value="employee">Çalışan</option></select></label>}
            <button onClick={async () => { try { await logout(); navigate(LOGIN_PATH); } catch { showToast("Çıkış yapılamadı.", "error"); } }}><i className="fa-solid fa-arrow-right-from-bracket" aria-hidden="true" />Çıkış yap</button>
            <button className="ez-profile-close" onClick={() => setProfileOpen(false)}>Kapat</button>
          </div>}
        </div>
      </header>
      <main className="main-content" id="main-content" tabIndex={-1} onFocusCapture={guidance?.focus}><WriteRecoveryNotice /><Outlet /></main>
    </div>
  );
}
