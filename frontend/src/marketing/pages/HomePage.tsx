import { Link } from "react-router-dom";
import { ButtonLink, usePageMeta } from "../../ui";
import { ROADMAP_STAGES, faqs, modules, roadmap, securityPillars, trustPoints } from "../content";
import { CtaBand, FaqList, ModuleIcon } from "../sections";
import { HeroVisual } from "../visuals";
import { ProductShowcase } from "../ProductShowcase";
import { EmployeeJourney } from "../EmployeeJourney";
import { StatusBadge } from "../StatusBadge";
import { Reveal, onSpotlightMove } from "../motion";

export function HomePage() {
  usePageMeta("Ekipzen", "Özlük, izin, puantaj, bordro ve işe alım süreçlerinizi tek bir kayıt düzeninde yürütün. Ekipzen, İK ekipleri için tasarlanmış insan kaynakları yönetim platformudur.");
  return (
    <>
      <section className="ez-hero ez-aurora">
        <div className="ez-container ez-hero__grid">
          <Reveal className="ez-hero__copy">
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
          </Reveal>
          <Reveal delay={150}><HeroVisual /></Reveal>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="vitrin-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Ürün</p>
            <h2 id="vitrin-baslik">Modülleri çalışırken görün</h2>
            <p className="ez-lead">Aşağıdaki ekranlar örnek verilerle hazırlanmış ürün görünümleridir.</p>
          </Reveal>
          <Reveal delay={100}><ProductShowcase /></Reveal>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="yolculuk-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Veri akışı</p>
            <h2 id="yolculuk-baslik">Bir çalışanın kaydı, işe alımdan bordroya</h2>
            <p className="ez-lead">Bilgi bir kez girilir; sonraki her süreç aynı kayıt üzerinden ilerler.</p>
          </Reveal>
          <EmployeeJourney />
        </div>
      </section>

      <section className="ez-section" aria-labelledby="moduller-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Modüller</p>
            <h2 id="moduller-baslik">Birbirine bağlı altı modül</h2>
            <p className="ez-lead">Tüm modüller aynı personel kaydı üzerinde çalışır. Her modülün durumu açıkça belirtilir.</p>
          </Reveal>
          <ul className="ez-bento">
            {modules.map((m, i) => (
              <Reveal as="li" key={m.id} delay={i * 70} className={`ez-bento__item ez-bento__item--${m.id}`}>
                <Link to={`/ozellikler#${m.id}`} className="ez-feature-card ez-spotlight" onPointerMove={onSpotlightMove}>
                  <div className="ez-feature-card__top"><ModuleIcon icon={m.icon} /><StatusBadge status={m.status} /></div>
                  <h3>{m.title}</h3>
                  <p>{m.summary}</p>
                  <span className="ez-feature-card__more">Detaylar <i aria-hidden="true" className="fa-solid fa-arrow-right" /></span>
                </Link>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="gelisim-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Gelişim</p>
            <h2 id="gelisim-baslik">Ekipzen gelişmeye devam ediyor</h2>
            <p className="ez-lead">Ürün, pilot müşterilerle birlikte doğrulanarak genişliyor. Yol haritasını açık tutuyoruz.</p>
          </Reveal>
          <ol className="ez-roadmap-strip">
            {ROADMAP_STAGES.map((stage, i) => (
              <Reveal as="li" key={stage.id} delay={i * 90} className={`ez-roadmap-strip__col ez-roadmap-strip__col--${stage.id}`}>
                <p className="ez-roadmap-strip__stage">{stage.title}</p>
                <p className="ez-roadmap-strip__sub">{stage.subtitle}</p>
                <ul>{roadmap.filter((r) => r.stage === stage.id).slice(0, 2).map((r) => <li key={r.title}>{r.title}</li>)}</ul>
              </Reveal>
            ))}
          </ol>
          <Link to="/hakkimizda#yol-haritasi" className="ez-link ez-roadmap-strip__more">Yol haritasının tamamı</Link>
        </div>
      </section>

      <section className="ez-section ez-section--dark" aria-labelledby="guvenlik-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Güvenlik</p>
            <h2 id="guvenlik-baslik">Her şirketin verisi kendi veritabanında tutulur</h2>
            <p className="ez-lead">Ekipzen, veri ayrımı ve en az yetki ilkesi üzerine kurulmuştur. Güvenlik önlemleri ürünün mimarisinin parçasıdır.</p>
          </Reveal>
          <ul className="ez-grid-3">
            {securityPillars.slice(0, 3).map((p, i) => (
              <Reveal as="li" key={p.title} delay={i * 80} className="ez-pillar">
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </Reveal>
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
