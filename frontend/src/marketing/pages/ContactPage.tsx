import { useSearchParams } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { plans, siteInfo } from "../content";
import { DemoRequestForm } from "../DemoRequestForm";

export function ContactPage() {
  usePageMeta("Demo talebi ve iletişim", "Ekipzen'i ekibinizle birlikte görmek için demo talep edin.");
  const [params] = useSearchParams();
  const plan = plans.find((p) => p.id === params.get("paket"));
  const initialMessage = plan ? `${plan.name} paketi için teklif almak istiyorum.` : "";

  return (
    <section className="ez-section ez-contact">
      <div className="ez-container ez-contact__grid">
        <div className="ez-contact__intro">
          <p className="ez-eyebrow">Demo talebi</p>
          <h1>Ekipzen'i birlikte keşfedelim</h1>
          <p className="ez-lead">Formu doldurun; süreçlerinizi dinleyip ürünü size göre gösterelim ve net bir teklif hazırlayalım.</p>
          <ul className="ez-check-list">
            <li>30 dakikalık, size özel canlı tanıtım</li>
            <li>Süreçlerinize göre modül önerisi</li>
            <li>Şirketinize özel, izole ortamda kurulum</li>
          </ul>
          <div className="ez-contact__card">
            <h2>Doğrudan ulaşın</h2>
            <p><i aria-hidden="true" className="fa-solid fa-envelope" /> <a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a></p>
            <p className="ez-muted-note">Zaten müşterimiz misiniz? Destek taleplerinizi uygulama içinden ya da bu adresten iletebilirsiniz.</p>
          </div>
        </div>
        <div className="ez-contact__form ez-card">
          <DemoRequestForm key={initialMessage} initialMessage={initialMessage} />
        </div>
      </div>
    </section>
  );
}
