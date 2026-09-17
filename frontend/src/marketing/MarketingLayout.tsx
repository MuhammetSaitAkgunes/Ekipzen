import { Link, Outlet, ScrollRestoration } from "react-router-dom";
import { ButtonLink, Logo } from "../ui";
import { LOGIN_PATH } from "../paths";

export function MarketingLayout() {
  return (
    <div className="ez-site ez-light">
      <a className="ez-skip" href="#icerik">İçeriğe geç</a>
      <header className="ez-header">
        <div className="ez-container ez-header__inner">
          <Link to="/" className="ez-header__brand" aria-label="Ekipzen ana sayfa"><Logo /></Link>
          <div className="ez-header__cta">
            <ButtonLink to={LOGIN_PATH} variant="ghost">Giriş yap</ButtonLink>
            <ButtonLink to="/iletisim">Demo iste</ButtonLink>
          </div>
        </div>
      </header>
      <main id="icerik" tabIndex={-1}>
        <Outlet />
      </main>
      <ScrollRestoration />
    </div>
  );
}
