import { Link } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { securityCategories, transparency } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";
import { SecurityLayers } from "../SecurityLayers";
import { StatusBadge } from "../StatusBadge";
import { Reveal, onSpotlightMove } from "../motion";

export function SecurityPage() {
  usePageMeta("Güvenlik ve KVKK", "Ekipzen'de veri ayrımı, rol bazlı yetkilendirme, şifreleme ve kişisel verilerin korunmasına ilişkin yaklaşım.");
  return (
    <>
      <PageHero
        glow="teal"
        eyebrow="Güvenlik"
        title="İK verisinin hassasiyetine göre kurulmuş bir mimari"
        lead="Ekipzen, çalışan verisinin bir şirketin en hassas verileri arasında yer aldığı gözetilerek veri ayrımı ve en az yetki ilkeleri üzerine kurulmuştur."
      />

      <section className="ez-section ez-section--flush ez-glow ez-layers-band" aria-labelledby="katman-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Mimari</p>
            <h2 id="katman-baslik">Katman katman güvenlik</h2>
            <p className="ez-lead">Bir isteğin verinize ulaşmadan önce geçtiği katmanlar. Ayrıntısını görmek için bir katman seçin.</p>
          </Reveal>
          <Reveal delay={120}><SecurityLayers /></Reveal>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="kategori-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Kontroller</p>
            <h2 id="kategori-baslik">Kontroller kategoriye göre</h2>
          </Reveal>
          <ul className="ez-grid-2 ez-categories">
            {securityCategories.map((c, i) => (
              <Reveal as="li" key={c.id} delay={i * 80} className="ez-pillar ez-spotlight ez-category" onPointerMove={onSpotlightMove}>
                <ModuleIcon icon={c.icon} />
                <h3>{c.title}</h3>
                <ul className="ez-check-list">{c.items.map((item) => <li key={item}>{item}</li>)}</ul>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="seffaflik-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Şeffaflık</p>
            <h2 id="seffaflik-baslik">Bugün sağlananlar ve üzerinde çalıştıklarımız</h2>
            <p className="ez-lead">Güvenlik yaklaşımımızı, bugün ürünün sağladıklarıyla ve henüz geliştirmekte olduklarımızla birlikte açıkça paylaşıyoruz.</p>
          </Reveal>
          <div className="ez-transparency">
            <Reveal className="ez-transparency__col ez-glass">
              <h3>Bugün sağlanan</h3>
              <ul className="ez-check-list">{transparency.provided.map((item) => <li key={item}>{item}</li>)}</ul>
            </Reveal>
            <Reveal delay={120} className="ez-transparency__col ez-glass ez-transparency__col--progress">
              <h3>Üzerinde çalışılan</h3>
              <ul className="ez-transparency__list">
                {transparency.inProgress.map((item) => <li key={item}><span>{item}</span><StatusBadge status="building" /></li>)}
              </ul>
            </Reveal>
          </div>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="kvkk-baslik">
        <div className="ez-container ez-narrow ez-prose">
          <h2 id="kvkk-baslik">KVKK ve kişisel veriler</h2>
          <p>
            Ekipzen, 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamındaki yükümlülüklerinizi yerine getirmenizi
            destekleyecek şekilde tasarlanmıştır. Erişim rol bazlıdır, veriler şirket başına ayrı tutulur ve her
            kullanıcı kendi kişisel verisini dışa aktarabilir.
          </p>
          <p>
            Ziyaretçi ve demo talebi verilerinin nasıl işlendiğini{" "}
            <Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link> sayfasında bulabilirsiniz.
          </p>
        </div>
      </section>
      <CtaBand title="Güvenlik gereksinimlerinizi birlikte değerlendirelim" text="Bilgi güvenliği ekibinizin sorularını demo görüşmesinde ayrıntılı olarak yanıtlayabiliriz." />
    </>
  );
}
