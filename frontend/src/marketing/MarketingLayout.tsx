import { useEffect, useRef, useState } from "react";
import { Link, NavLink, Outlet, ScrollRestoration, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { LOGIN_PATH, roleHomeFor } from "../paths";
import { ButtonLink, Logo } from "../ui";
import { modules, navItems, siteInfo } from "./content";
import { useScrolled } from "./useScrolled";

export function MarketingLayout() {
  const { user } = useAuth();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const scrolled = useScrolled();
  const toggleRef = useRef<HTMLButtonElement>(null);

  useEffect(() => setMenuOpen(false), [location.pathname]);
  useEffect(() => {
    if (!menuOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      setMenuOpen(false);
      toggleRef.current?.focus();
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [menuOpen]);

  const primaryAction = user
    ? <ButtonLink to={roleHomeFor(user.role)} variant="ghost">Uygulamaya git</ButtonLink>
    : <ButtonLink to={LOGIN_PATH} variant="ghost">Giriş yap</ButtonLink>;

  return (
    <div className="ez-site ez-night">
      <div className="ez-grain" aria-hidden="true" />
      <a className="ez-skip" href="#icerik">İçeriğe geç</a>
      <header className={`ez-header${menuOpen ? " is-open" : ""}${scrolled ? " is-scrolled" : ""}`}>
        <div className="ez-container ez-header__inner">
          <Link to="/" className="ez-header__brand" aria-label="Ekipzen ana sayfa"><Logo tone="light" /></Link>
          <nav id="ana-menu" className="ez-header__nav" aria-label="Ana menü">
            <ul>
              {navItems.map((item) => (
                <li key={item.to}><NavLink to={item.to} className="ez-header__link">{item.label}</NavLink></li>
              ))}
            </ul>
            <div className="ez-header__cta">
              {primaryAction}
              <ButtonLink to="/iletisim">Demo talep edin</ButtonLink>
            </div>
          </nav>
          <button
            ref={toggleRef}
            type="button"
            className="ez-header__toggle"
            aria-expanded={menuOpen}
            aria-controls="ana-menu"
            aria-label={menuOpen ? "Menüyü kapat" : "Menüyü aç"}
            onClick={() => setMenuOpen((open) => !open)}
          >
            <i aria-hidden="true" className={`fa-solid ${menuOpen ? "fa-xmark" : "fa-bars"}`} />
          </button>
        </div>
      </header>

      <main id="icerik" tabIndex={-1}>
        <Outlet />
      </main>

      <footer className="ez-footer">
        <div className="ez-container ez-footer__grid">
          <div className="ez-footer__brand">
            <Logo tone="light" />
            <p>Özlük, izin, puantaj, bordro ve işe alım süreçlerini tek bir kayıt düzeninde yürüten insan kaynakları yönetim platformu.</p>
          </div>
          <div>
            <h2 className="ez-footer__title">Ürün</h2>
            <ul>
              {modules.slice(0, 4).map((m) => <li key={m.id}><Link to={`/ozellikler#${m.id}`}>{m.title}</Link></li>)}
              <li><Link to="/fiyatlandirma">Fiyatlandırma</Link></li>
            </ul>
          </div>
          <div>
            <h2 className="ez-footer__title">Şirket</h2>
            <ul>
              <li><Link to="/hakkimizda">Hakkımızda</Link></li>
              <li><Link to="/guvenlik">Güvenlik</Link></li>
              <li><Link to="/iletisim">İletişim</Link></li>
            </ul>
          </div>
          <div>
            <h2 className="ez-footer__title">Yasal</h2>
            <ul>
              <li><Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link></li>
              <li><a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a></li>
            </ul>
          </div>
        </div>
        <div className="ez-container ez-footer__bottom">
          <p>© {new Date().getFullYear()} {siteInfo.legalName}. Tüm hakları saklıdır.</p>
        </div>
      </footer>
      <ScrollRestoration />
    </div>
  );
}
