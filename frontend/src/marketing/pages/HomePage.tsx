import { Link } from "react-router-dom";
import { ButtonLink, usePageMeta } from "../../ui";
import { faqs, modules, securityPillars, steps, trustPoints } from "../content";
import { CtaBand, FaqList, ModuleIcon } from "../sections";
import { ActionListCard, HeroVisual, RiskScoreCard } from "../visuals";

export function HomePage() {
  usePageMeta("Ekipzen", "Ekipzen ile personel, izin, puantaj, bordro ve işe alım süreçlerinizi tek, güvenli platformda yönetin.");
  return (
    <>
      <section className="ez-hero">
        <div className="ez-container ez-hero__grid">
          <div className="ez-hero__copy">
            <p className="ez-eyebrow">Türkiye için İK platformu</p>
            <h1>Ekibinizi tek yerden, güvenle yönetin</h1>
            <p className="ez-lead">
              Personel, izin, puantaj, bordro ve işe alım tek platformda. Risk sinyallerini erken görün,
              ekibinizi e-posta trafiğinden kurtarın.
            </p>
            <div className="ez-actions">
              <ButtonLink to="/iletisim" size="lg">Demo iste</ButtonLink>
              <ButtonLink to="/ozellikler" size="lg" variant="secondary">Özellikleri incele</ButtonLink>
            </div>
            <ul className="ez-trust" aria-label="Öne çıkan güvenceler">
              {trustPoints.map((t) => (
                <li key={t.label}><i aria-hidden="true" className={`fa-solid ${t.icon}`} />{t.label}</li>
              ))}
            </ul>
          </div>
          <HeroVisual />
        </div>
      </section>

      <section className="ez-section" aria-labelledby="moduller-baslik">
        <div className="ez-container">
          <div className="ez-section__head">
            <p className="ez-eyebrow">Modüller</p>
            <h2 id="moduller-baslik">Tek platformda tüm İK süreçleri</h2>
            <p className="ez-lead">Dağınık tablolar ve e-posta zincirleri yerine, birbirine bağlı modüller.</p>
          </div>
          <ul className="ez-grid-3">
            {modules.map((m) => (
              <li key={m.id}>
                <Link to={`/ozellikler#${m.id}`} className="ez-feature-card">
                  <ModuleIcon icon={m.icon} />
                  <h3>{m.title}</h3>
                  <p>{m.summary}</p>
                  <span className="ez-feature-card__more">Detaylar <i aria-hidden="true" className="fa-solid fa-arrow-right" /></span>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="risk-baslik">
        <div className="ez-container ez-split">
          <div>
            <p className="ez-eyebrow">Risk Merkezi</p>
            <h2 id="risk-baslik">Sorunları istifa mektubundan önce görün</h2>
            <p className="ez-lead">
              Ayrılma, tükenmişlik ve uyum sinyalleri tek ekranda toplanır; her sinyal somut bir aksiyon önerisine bağlanır.
            </p>
            <ul className="ez-check-list">
              <li>Departman bazında risk skoru ve trend</li>
              <li>Yönetici yükü ve çalışan nabzı</li>
              <li>Önerilen aksiyonların takibi</li>
            </ul>
            <ButtonLink to="/ozellikler#risk-merkezi" variant="secondary">Risk Merkezi'ni incele</ButtonLink>
          </div>
          <div className="ez-split__visual" aria-hidden="true">
            <RiskScoreCard />
            <ActionListCard />
          </div>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="adimlar-baslik">
        <div className="ez-container">
          <div className="ez-section__head">
            <p className="ez-eyebrow">Nasıl çalışır</p>
            <h2 id="adimlar-baslik">Üç adımda başlayın</h2>
          </div>
          <ol className="ez-steps">
            {steps.map((s, i) => (
              <li key={s.title} className="ez-steps__item">
                <span className="ez-steps__num" aria-hidden="true">{i + 1}</span>
                <h3>{s.title}</h3>
                <p>{s.text}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="ez-section ez-section--dark" aria-labelledby="guvenlik-baslik">
        <div className="ez-container">
          <div className="ez-section__head">
            <p className="ez-eyebrow">Güvenlik</p>
            <h2 id="guvenlik-baslik">Verileriniz sizin kontrolünüzde</h2>
            <p className="ez-lead">İK verisi hassastır. Ekipzen'i izolasyon ve en az yetki ilkesiyle kurduk.</p>
          </div>
          <ul className="ez-grid-3">
            {securityPillars.slice(0, 3).map((p) => (
              <li key={p.title} className="ez-pillar">
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </li>
            ))}
          </ul>
          <ButtonLink to="/guvenlik" variant="secondary">Güvenlik yaklaşımımız</ButtonLink>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="sss-baslik">
        <div className="ez-container ez-narrow">
          <div className="ez-section__head">
            <h2 id="sss-baslik">Sık sorulan sorular</h2>
          </div>
          <FaqList items={faqs} />
        </div>
      </section>

      <CtaBand />
    </>
  );
}
