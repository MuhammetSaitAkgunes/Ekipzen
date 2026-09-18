import { Link } from "react-router-dom";
import { ButtonLink, usePageMeta } from "../../ui";
import { faqs, modules, securityPillars, steps, trustPoints } from "../content";
import { CtaBand, FaqList, ModuleIcon } from "../sections";
import { ActionListCard, HeroVisual, RiskScoreCard } from "../visuals";

export function HomePage() {
  usePageMeta("Ekipzen", "Özlük, izin, puantaj, bordro ve işe alım süreçlerinizi tek bir kayıt düzeninde yürütün. Ekipzen, İK ekipleri için tasarlanmış insan kaynakları yönetim platformudur.");
  return (
    <>
      <section className="ez-hero">
        <div className="ez-container ez-hero__grid">
          <div className="ez-hero__copy">
            <p className="ez-eyebrow">İnsan kaynakları yönetimi</p>
            <h1>İK süreçleriniz tek bir kayıt düzeninde</h1>
            <p className="ez-lead">
              Özlük bilgileri, izinler, puantaj, bordro ve işe alım aynı veri üzerinde ilerler. Bir süreçte
              girilen bilgi, diğerlerinde yeniden girilmez.
            </p>
            <div className="ez-actions">
              <ButtonLink to="/iletisim" size="lg">Demo talep edin</ButtonLink>
              <ButtonLink to="/ozellikler" size="lg" variant="secondary">Modülleri inceleyin</ButtonLink>
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
            <h2 id="moduller-baslik">Birbirine bağlı altı modül</h2>
            <p className="ez-lead">Tüm modüller aynı personel kaydı üzerinde çalışır. Bordro, dönemin her iş gününün puantaj ya da onaylı izin kaydıyla karşılandığını kontrol eder.</p>
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
            <h2 id="risk-baslik">Ayrılma ve tükenmişlik sinyallerini erken fark edin</h2>
            <p className="ez-lead">
              Risk Merkezi; devamsızlık, geç kalma, fazla mesai, kullanılmayan izin ve çalışan nabzı verilerini departman düzeyinde değerlendirir ve her sinyali izlenebilir bir aksiyona bağlar.
            </p>
            <ul className="ez-check-list">
              <li>Departman bazında risk skoru ve dönemsel eğilim</li>
              <li>Yönetici yükü ve çalışan nabzı</li>
              <li>Aksiyon önerileri ve takibi</li>
            </ul>
            <ButtonLink to="/ozellikler#risk-merkezi" variant="secondary">Risk Merkezi'ni inceleyin</ButtonLink>
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
            <p className="ez-eyebrow">Başlangıç süreci</p>
            <h2 id="adimlar-baslik">Kullanıma geçiş üç adımda tamamlanır</h2>
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
            <h2 id="guvenlik-baslik">Her şirketin verisi kendi veritabanında tutulur</h2>
            <p className="ez-lead">Ekipzen, veri ayrımı ve en az yetki ilkesi üzerine kurulmuştur. Güvenlik önlemleri ürünün mimarisinin parçasıdır.</p>
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
          <ButtonLink to="/guvenlik" variant="secondary">Güvenlik yaklaşımını inceleyin</ButtonLink>
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
