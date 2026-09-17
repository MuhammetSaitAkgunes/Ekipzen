import { usePageMeta } from "../../ui";
import { values } from "../content";
import { CtaBand, PageHero } from "../sections";

export function AboutPage() {
  usePageMeta("Hakkımızda", "Ekipzen'in neden var olduğu ve hangi değerlerle geliştirildiği.");
  return (
    <>
      <PageHero
        eyebrow="Hakkımızda"
        title="İK ekiplerinin işini kolaylaştırmak için buradayız"
        lead="Ekipzen, dağınık tablolar ve e-posta zincirleriyle yürüyen İK operasyonlarını tek, güvenilir bir platformda toplamak için doğdu."
      />
      <section className="ez-section ez-section--flush" aria-labelledby="misyon-baslik">
        <div className="ez-container ez-narrow ez-prose">
          <h2 id="misyon-baslik">Misyonumuz</h2>
          <p>
            İK ekiplerinin zamanını tekrar eden işlerden kurtarıp insanlara ayırmalarını sağlamak. Bunu yaparken
            çalışan verisini en yüksek özenle korumak ve Türkiye'nin mevzuatına baştan uyumlu bir ürün sunmak.
          </p>
          <h2>Neden Ekipzen?</h2>
          <p>
            Personel, izin, puantaj, bordro ve işe alım birbirinden kopuk araçlarda yönetildiğinde aynı bilgi defalarca
            girilir ve hatalar büyür. Ekipzen bu süreçleri tek veri modelinde birleştirir; risk sinyallerini de bu
            bütün resimden üretir.
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
