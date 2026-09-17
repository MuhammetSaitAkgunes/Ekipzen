import { Link } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { securityPillars } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";

export function SecurityPage() {
  usePageMeta("Güvenlik ve KVKK", "Ekipzen'in veri izolasyonu, yetkilendirme, şifreleme ve KVKK yaklaşımı.");
  return (
    <>
      <PageHero
        eyebrow="Güvenlik"
        title="İK verisi için tasarlanmış güvenlik"
        lead="Ekipzen'i, bir şirketin en hassas verisini taşıyacağını bilerek; izolasyon ve en az yetki ilkeleriyle kurduk."
      />
      <section className="ez-section ez-section--flush" aria-label="Güvenceler">
        <div className="ez-container">
          <ul className="ez-grid-3">
            {securityPillars.map((p) => (
              <li key={p.title} className="ez-pillar">
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>
      <section className="ez-section ez-section--muted" aria-labelledby="kvkk-baslik">
        <div className="ez-container ez-narrow ez-prose">
          <h2 id="kvkk-baslik">KVKK ve kişisel veriler</h2>
          <p>
            Ekipzen, 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamındaki yükümlülüklerinizi yerine getirmenize
            yardımcı olacak şekilde tasarlanmıştır: erişim rol bazlıdır, veriler şirket başına ayrı tutulur ve her
            kullanıcı kendi kişisel verisini dışa aktarabilir.
          </p>
          <p>
            Ziyaretçi ve demo talebi verilerinin nasıl işlendiğini{" "}
            <Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link> sayfasında bulabilirsiniz.
          </p>
        </div>
      </section>
      <CtaBand title="Güvenlik sorularınızı konuşalım" text="Bilgi güvenliği ekibinizin sorularını demo görüşmesinde yanıtlayalım." />
    </>
  );
}
