import { usePageMeta } from "../../ui";
import { values } from "../content";
import { CtaBand, PageHero } from "../sections";

export function AboutPage() {
  usePageMeta("Hakkımızda", "Ekipzen'in amacı, yaklaşımı ve ürünü geliştirirken gözettiği ilkeler.");
  return (
    <>
      <PageHero
        eyebrow="Hakkımızda"
        title="İK ekiplerinin günlük işini tek bir düzende toplamak için geliştiriyoruz"
        lead="Ekipzen, farklı tablolar ve yazışmalar üzerinden yürütülen İK süreçlerini tek bir kayıt düzeninde birleştirmek amacıyla geliştirilmektedir."
      />
      <section className="ez-section ez-section--flush" aria-labelledby="misyon-baslik">
        <div className="ez-container ez-narrow ez-prose">
          <h2 id="misyon-baslik">Misyonumuz</h2>
          <p>
            İK ekiplerinin tekrar eden işlere ayırdığı zamanı azaltmak ve bu zamanı çalışanlarla ilgili kararlara
            aktarmalarını sağlamak. Bunu yaparken çalışan verisini özenle korumak ve yürürlükteki mevzuatı ürünün
            temelinde gözetmek.
          </p>
          <h2>Neden Ekipzen?</h2>
          <p>
            Personel, izin, puantaj, bordro ve işe alım ayrı araçlarda yürütüldüğünde aynı bilgi birden fazla kez
            girilir ve kayıtlar arasında tutarsızlıklar oluşur. Ekipzen bu süreçleri tek bir veri modelinde birleştirir
            ve risk sinyallerini de bu bütünlüklü veriden üretir.
          </p>
        </div>
      </section>
      <section className="ez-section ez-section--muted" aria-labelledby="degerler-baslik">
        <div className="ez-container">
          <div className="ez-section__head"><h2 id="degerler-baslik">Değerlerimiz</h2></div>
          <ul className="ez-grid-3">
            {values.map((v) => (
              <li key={v.title} className="ez-pillar"><h3>{v.title}</h3><p>{v.text}</p></li>
            ))}
          </ul>
        </div>
      </section>
      <CtaBand />
    </>
  );
}
