import { Link } from "react-router-dom";
import { ButtonLink, Badge, usePageMeta } from "../../ui";
import { comparisonRows, plans, pricingFaqs, pricingLead } from "../content";
import { CtaBand, FaqList, PageHero } from "../sections";
import { Reveal, onSpotlightMove } from "../motion";
import { StatusBadge } from "../StatusBadge";

const cell = (value: boolean | string) =>
  typeof value === "string" ? value : value
    ? <><i aria-hidden="true" className="fa-solid fa-check ez-yes" /><span className="ez-sr-only">Var</span></>
    : <><i aria-hidden="true" className="fa-solid fa-minus ez-no" /><span className="ez-sr-only">Yok</span></>;

export function PricingPage() {
  usePageMeta("Fiyatlandırma", "Ekipzen paketleri ve kapsamları. Fiyat, çalışan sayısına ve kullanılacak modüllere göre belirlenir.");
  return (
    <>
      <PageHero
        glow="indigo"
        eyebrow="Fiyatlandırma"
        title="Şirketinizin ihtiyacına göre belirlenen paketler"
        lead={pricingLead}
      />
      <section className="ez-section ez-section--flush" aria-label="Paketler">
        <div className="ez-container">
          <ul className="ez-plans">
            {plans.map((p, i) => (
              <Reveal as="li" key={p.id} delay={i * 100} className="ez-plans__item">
                <div className={`ez-plan ez-spotlight${p.highlighted ? " ez-plan--featured" : ""}`} onPointerMove={onSpotlightMove}>
                  {p.highlighted && <Badge tone="primary">Önerilen</Badge>}
                  <h2>{p.name}</h2>
                  <p className="ez-plan__audience">{p.audience}</p>
                  <p className="ez-plan__price">Size özel fiyat</p>
                  <ButtonLink to={`/iletisim?paket=${p.id}`} variant={p.highlighted ? "primary" : "secondary"} block aria-label={`${p.name} paketi için teklif alın`}>
                    Teklif alın
                  </ButtonLink>
                  <ul className="ez-check-list">
                    {p.features.map((f) => (
                      <li key={f.label}>{f.label}{f.status && <StatusBadge status={f.status} />}</li>
                    ))}
                  </ul>
                </div>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="karsilastirma-baslik">
        <div className="ez-container">
          <div className="ez-section__head"><h2 id="karsilastirma-baslik">Paketleri karşılaştırın</h2></div>
          <div className="ez-table-wrap">
            <table className="ez-compare">
              <caption className="ez-sr-only">Paket karşılaştırması</caption>
              <thead>
                <tr>
                  <th scope="col">Özellik</th>
                  {plans.map((p) => <th key={p.id} scope="col">{p.name}</th>)}
                </tr>
              </thead>
              <tbody>
                {comparisonRows.map((row) => (
                  <tr key={row.label}>
                    <th scope="row">{row.label}{row.status && <StatusBadge status={row.status} />}</th>
                    {row.values.map((v, i) => <td key={plans[i].id}>{cell(v)}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="ez-muted-note">Pilot ve geliştirme durumundaki modüllerin kullanım kapsamı, teklif görüşmesinde ayrıca netleştirilir.</p>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="fiyat-sss-baslik">
        <div className="ez-container ez-narrow">
          <div className="ez-section__head"><h2 id="fiyat-sss-baslik">Fiyatlandırmaya ilişkin sorular</h2></div>
          <FaqList items={pricingFaqs} />
        </div>
      </section>
      <section className="ez-section ez-section--flush" aria-label="Ürünün gelişimi">
        <Reveal className="ez-container ez-narrow ez-pricing-roadmap">
          <i aria-hidden="true" className="fa-solid fa-route" />
          <p>Modüllerin güncel kullanım durumunu ve planlanan çalışmaları inceleyerek şirketiniz için uygun kapsamı belirleyebilirsiniz.</p>
          <Link to="/hakkimizda#yol-haritasi" className="ez-link">Yol haritasını inceleyin</Link>
        </Reveal>
      </section>
      <CtaBand title="Şirketinize özel teklif talep edin" text="Çalışan sayınızı ve öncelikli süreçlerinizi paylaşın; şirketinize uygun kapsam ve fiyatlandırmayı birlikte değerlendirelim." />
    </>
  );
}
