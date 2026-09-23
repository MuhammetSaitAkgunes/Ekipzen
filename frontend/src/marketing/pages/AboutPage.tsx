import { usePageMeta } from "../../ui";
import { aboutScenes, commitments, principles } from "../content";
import { CtaBand, ModuleIcon, SceneBackdrop } from "../sections";
import { ProcessTimeline } from "../ProcessTimeline";
import { ModuleMaturity } from "../ModuleMaturity";
import { RoadmapBoard } from "../RoadmapBoard";
import { Reveal, onSpotlightMove, useScrollToHash } from "../motion";

export function AboutPage() {
  usePageMeta("Hakkımızda", "Ekipzen'in amacı, ürünü geliştirme yaklaşımı, bugünkü durumu ve yol haritası.");
  useScrollToHash();
  return (
    <>
      <section className="ez-manifesto">
        <SceneBackdrop tone="violet" />
        <svg className="ez-manifesto__lines" viewBox="0 0 1200 400" preserveAspectRatio="none" aria-hidden="true">
          <path pathLength={1} d="M0 320 C 240 260, 420 360, 640 280 S 1020 180, 1200 220" />
          <path pathLength={1} d="M0 360 C 300 300, 520 390, 760 320 S 1060 240, 1200 280" />
          <path pathLength={1} d="M0 280 C 260 220, 500 330, 720 250 S 1040 140, 1200 180" />
        </svg>
        <div className="ez-container ez-narrow">
          <div className="ez-hero-enter">
            <p className="ez-eyebrow">Hakkımızda</p>
            <h1>İK süreçlerine <span className="ez-gradient-text">tek bir çalışma düzeni</span> kazandırıyoruz.</h1>
            <p className="ez-lead">
              Ekipzen, personel kayıtlarını, izin taleplerini ve işe alımı ortak bir veri düzeninde buluşturur. İK ekiplerinin günlük işlerini daha izlenebilir hale getirmeyi amaçlar.
            </p>
          </div>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="neden-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="neden-baslik">Neden varız</h2>
            <p className="ez-lead">
              Personel bilgileri, izin talepleri ve işe alım ayrı araçlarda yürütüldüğünde kayıtları güncel tutmak zorlaşır. Ekipzen, bu temel süreçleri ortak bir veri düzeninde birleştirir.
            </p>
          </Reveal>
          <ol className="ez-scenes">
            {aboutScenes.map((scene, i) => (
              <Reveal as="li" key={scene.title} delay={i * 140} className={`ez-scene ez-scene--${i + 1}`}>
                <span className="ez-scene__num" aria-hidden="true">{String(i + 1).padStart(2, "0")}</span>
                <span className="ez-scene__icon" aria-hidden="true"><i className={`fa-solid ${scene.icon}`} /></span>
                <h3>{scene.title}</h3>
                <p>{scene.text}</p>
              </Reveal>
            ))}
          </ol>
        </div>
      </section>

      <section className="ez-section ez-section--dark" aria-labelledby="taahhut-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="taahhut-baslik">Taahhütlerimiz</h2>
          </Reveal>
          <ul className="ez-commitments">
            {commitments.map((c, i) => (
              <Reveal as="li" key={c.title} delay={i * 110} className="ez-commitment">
                <i aria-hidden="true" className="fa-solid fa-circle-check" />
                <div>
                  <h3>{c.title}</h3>
                  <p>{c.text}</p>
                </div>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="ilke-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="ilke-baslik">İlkelerimiz</h2>
            <p className="ez-lead">Ürün tasarımında veri güvenliği, açıklık ve günlük kullanım kolaylığını esas alıyoruz.</p>
          </Reveal>
          <ul className="ez-grid-2 ez-principles">
            {principles.map((p, i) => (
              <Reveal as="li" key={p.title} delay={i * 90} className="ez-pillar ez-spotlight" onPointerMove={onSpotlightMove}>
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="surec-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="surec-baslik">Ürünü nasıl geliştiriyoruz</h2>
            <p className="ez-lead">Her yetenek, genel kullanıma açılmadan önce aynı doğrulama adımlarından geçer.</p>
          </Reveal>
          <ProcessTimeline />
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="bugun-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="bugun-baslik">Ekipzen bugün</h2>
            <p className="ez-lead">Modüllerin kullanım durumunu ve kapsamını burada inceleyebilirsiniz.</p>
          </Reveal>
          <ModuleMaturity />
        </div>
      </section>

      <section id="yol-haritasi" className="ez-section ez-anchor" aria-labelledby="yol-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="yol-baslik">Yol haritası</h2>
            <p className="ez-lead">Planlanan geliştirmeleri ve değerlendirme aşamasındaki çalışmaları paylaşıyoruz.</p>
          </Reveal>
          <RoadmapBoard />
        </div>
      </section>



      <CtaBand
        title="İK süreçlerinizi birlikte değerlendirelim"
        text="Ekipzen'in mevcut modüllerini ve şirketinize uygun kullanım kapsamını demo görüşmesinde birlikte inceleyelim."
      />
    </>
  );
}
