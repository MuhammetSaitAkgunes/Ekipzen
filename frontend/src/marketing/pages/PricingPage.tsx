import { ButtonLink, Badge, usePageMeta } from "../../ui";
import { comparisonRows, plans, pricingFaqs } from "../content";
import { CtaBand, FaqList, PageHero } from "../sections";

const cell = (value: boolean | string) =>
  typeof value === "string" ? value : value
    ? <><i aria-hidden="true" className="fa-solid fa-check ez-yes" /><span className="ez-sr-only">Var</span></>
    : <><i aria-hidden="true" className="fa-solid fa-minus ez-no" /><span className="ez-sr-only">Yok</span></>;

export function PricingPage() {
  usePageMeta("Fiyatlandırma", "Ekibinizin büyüklüğüne ve ihtiyacınız olan modüllere göre size özel teklif alın.");
  return (
    <>
      <PageHero
        eyebrow="Fiyatlandırma"
        title="İhtiyacınız kadar, sürprizsiz"
        lead="Fiyat; çalışan sayınıza ve seçtiğiniz modüllere göre belirlenir. Size özel teklifi kısa sürede iletiyoruz."
      />
      <section className="ez-section ez-section--flush" aria-label="Paketler">
        <div className="ez-container">
          <ul className="ez-plans">
            {plans.map((p) => (
              <li key={p.id} className={`ez-plan${p.highlighted ? " ez-plan--featured" : ""}`}>
                {p.highlighted && <Badge tone="primary">En çok tercih edilen</Badge>}
                <h2>{p.name}</h2>
                <p className="ez-plan__audience">{p.audience}</p>
                <p className="ez-plan__price">Size özel fiyat</p>
                <ButtonLink to={`/iletisim?paket=${p.id}`} variant={p.highlighted ? "primary" : "secondary"} block aria-label={`${p.name} paketi için teklif alın`}>
                  Teklif alın
                </ButtonLink>
                <ul className="ez-check-list">{p.features.map((f) => <li key={f}>{f}</li>)}</ul>
              </li>
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
                    <th scope="row">{row.label}</th>
                    {row.values.map((v, i) => <td key={plans[i].id}>{cell(v)}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="fiyat-sss-baslik">
        <div className="ez-container ez-narrow">
          <div className="ez-section__head"><h2 id="fiyat-sss-baslik">Fiyatlandırma hakkında sorular</h2></div>
          <FaqList items={pricingFaqs} />
        </div>
      </section>
      <CtaBand title="Size özel teklifi alın" text="Çalışan sayınızı ve ihtiyaçlarınızı paylaşın, net bir teklifle dönelim." />
    </>
  );
}
