import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Logo } from "../ui";
import { PayrollSummaryCard, RiskScoreCard } from "../marketing/visuals";

export function AuthLayout({ title, subtitle, badge, children }: {
  title: string;
  subtitle: ReactNode;
  badge?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="ez-auth ez-light">
      <main className="ez-auth__main">
        <div className="ez-auth__top">
          <Link to="/" aria-label="Ekipzen ana sayfa"><Logo /></Link>
          <Link to="/" className="ez-auth__back"><i aria-hidden="true" className="fa-solid fa-arrow-left" /> Ana sayfaya dön</Link>
        </div>
        <div className="ez-auth__panel">
          {badge}
          <h1>{title}</h1>
          <p className="ez-auth__subtitle">{subtitle}</p>
          {children}
        </div>
        <p className="ez-auth__legal">
          <Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link> · <Link to="/guvenlik">Güvenlik</Link>
        </p>
      </main>
      <aside className="ez-auth__aside" aria-label="Ekipzen hakkında">
        <div className="ez-auth__aside-inner">
          <p className="ez-auth__quote">Özlük, izin, puantaj ve bordro süreçleri tek bir kayıt düzeninde.</p>
          <ul className="ez-auth__points">
            <li><i aria-hidden="true" className="fa-solid fa-database" /> Şirketinize ait ayrı veritabanı</li>
            <li><i aria-hidden="true" className="fa-solid fa-user-lock" /> Rol bazlı yetkilendirme</li>
            <li><i aria-hidden="true" className="fa-solid fa-scale-balanced" /> KVKK gereklilikleri gözetilerek tasarlandı</li>
          </ul>
          <div className="ez-auth__visual" aria-hidden="true">
            <RiskScoreCard />
            <PayrollSummaryCard />
          </div>
        </div>
      </aside>
    </div>
  );
}
