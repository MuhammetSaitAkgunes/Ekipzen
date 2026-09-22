import { Link } from "react-router-dom";
import { ButtonLink, usePageMeta } from "../../ui";
import { STATUS_META, faqs, homeCopy, homeRoles, homeTrustPoints, modules, onboardingNote, onboardingSteps, securityPillars } from "../content";
import { CtaBand, FaqList, ModuleIcon, SceneBackdrop } from "../sections";
import { HeroScreen } from "../visuals";
import { ProductShowcase } from "../ProductShowcase";
import { EmployeeJourney } from "../EmployeeJourney";
import { StatusBadge } from "../StatusBadge";
import { Reveal } from "../motion";

const SECURITY_LAYERS = ["Uygulama", "Oturum", "Yetki", "Veritabanı"];


export function HomePage() {
  usePageMeta("Ekipzen", "Özlük, izin, puantaj, bordro ve işe alım süreçlerinizi tek bir kayıt düzeninde yürütün. Ekipzen, İK ekipleri için tasarlanmış insan kaynakları yönetim platformudur.");
  return (
    <>
      <section className="ez-hero">
        <SceneBackdrop />
        <div className="ez-container">
          <div className="ez-hero__copy ez-hero-enter">
            <p className="ez-hero__pill ez-glass"><span aria-hidden="true" className="ez-hero__pulse" />{homeCopy.audience}</p>
            <h1>{homeCopy.headline} <span className="ez-gradient-text">{homeCopy.emphasis}</span></h1>
            <p className="ez-lead">{homeCopy.lead}</p>
            <div className="ez-actions">
              <ButtonLink to="/iletisim" size="lg">Demo talep edin</ButtonLink>
              <ButtonLink to="/ozellikler" size="lg" variant="secondary">Modülleri inceleyin</ButtonLink>
            </div>
            <ul className="ez-trust" aria-label="Öne çıkan güvenceler">
              {homeTrustPoints.map((t) => (
                <li key={t.label}><i aria-hidden="true" className={`fa-solid ${t.icon}`} />{t.label}</li>
              ))}
            </ul>
          </div>
          <div className="ez-hero__screen ez-hero-enter ez-hero-enter--late"><HeroScreen /></div>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="vitrin-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Ürün</p>
            <h2 id="vitrin-baslik">Ekipzen ekranlarını keşfedin</h2>
            <p className="ez-lead">Aşağıdaki ekranlar örnek verilerle hazırlanmış ürün görünümleridir.</p>
          </Reveal>
          <Reveal delay={100}><ProductShowcase /></Reveal>
          <div className="ez-scope" aria-label="Modüllerin güncel durumu">
            <ul>{(Object.keys(STATUS_META) as (keyof typeof STATUS_META)[]).map((status) => (
              <li key={status}><StatusBadge status={status} /><span>{modules.filter((m) => m.status === status).map((m) => m.title).join(", ")}</span></li>
            ))}</ul>
            <Link to="/hakkimizda#yol-haritasi" className="ez-link">Yol haritasının tamamı</Link>
          </div>
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

      <section className="ez-section" aria-labelledby="roller-baslik">
        <div className="ez-container">
          <div className="ez-section__head"><h2 id="roller-baslik">Ekibinizde herkes için bir çalışma alanı</h2></div>
          <ul className="ez-home-roles">{homeRoles.map((role) => <li key={role.title}><h3>{role.title}</h3><p>{role.text}</p></li>)}</ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="gecis-baslik">
        <div className="ez-container">
          <div className="ez-section__head"><h2 id="gecis-baslik">Ekipzen ile çalışmaya nasıl başlarsınız?</h2></div>
          <ol className="ez-onboarding">{onboardingSteps.map((step) => <li key={step.title}><h3>{step.title}</h3><p>{step.text}</p></li>)}</ol>
          <p className="ez-muted-note">{onboardingNote}</p>
        </div>
      </section>

      <section className="ez-section ez-section--dark ez-glow ez-security-band" aria-labelledby="guvenlik-baslik">
        <div className="ez-container ez-security-band__grid">
          <div>
            <Reveal className="ez-section__head">
              <p className="ez-eyebrow">Güvenlik</p>
              <h2 id="guvenlik-baslik">Her şirketin verisi kendi veritabanında tutulur</h2>
              <p className="ez-lead">Ekipzen, veri ayrımı ve en az yetki ilkesi üzerine kurulmuştur. Güvenlik önlemleri ürünün mimarisinin parçasıdır.</p>
            </Reveal>
            <ul className="ez-security-band__list">
              {securityPillars.slice(0, 3).map((p, i) => (
                <Reveal as="li" key={p.title} delay={i * 80} className="ez-security-band__item ez-glass">
                  <ModuleIcon icon={p.icon} />
                  <div><h3>{p.title}</h3><p>{p.text}</p></div>
                </Reveal>
              ))}
            </ul>
            <ButtonLink to="/guvenlik" variant="secondary">Güvenlik yaklaşımını inceleyin</ButtonLink>
          </div>
          <div className="ez-sec-layers" aria-hidden="true">
            {SECURITY_LAYERS.map((name, i) => <span key={name} className={`ez-sec-layers__layer ez-sec-layers__layer--${i + 1}`}>{name}</span>)}
          </div>
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
