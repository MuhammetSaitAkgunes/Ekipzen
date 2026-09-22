import { useSearchParams } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { plans, siteInfo } from "../content";
import { DemoRequestForm } from "../DemoRequestForm";
import { SceneBackdrop } from "../sections";
import { ModuleVisual } from "../visuals";

export function ContactPage() {
  usePageMeta("Demo talebi ve iletişim", "Ekipzen'i kendi süreçleriniz üzerinden değerlendirmek için demo talep edin.");
  const [params] = useSearchParams();
  const plan = plans.find((p) => p.id === params.get("paket"));
  const initialMessage = plan ? `${plan.name} paketi için teklif almak istiyorum.` : "";

  return (
    <section className="ez-section ez-contact">
      <SceneBackdrop />
      <div className="ez-container ez-contact__grid">
        <div className="ez-contact__intro">
          <p className="ez-eyebrow">İletişim</p>
          <h1>Demo talep edin</h1>
          <p className="ez-lead">Formu doldurmanızın ardından ekibimiz sizinle iletişime geçer. Görüşmede İK süreçlerinizi dinler, ürünü bu süreçler üzerinden gösterir ve ihtiyaçlarınıza uygun teklifi hazırlarız.</p>
          <ul className="ez-check-list">
            <li>Süreçlerinize göre planlanan canlı tanıtım</li>
            <li>İhtiyaçlarınıza uygun paket ve modül önerisi</li>
            <li>Şirketinize ayrılmış bir ortamda kurulum</li>
          </ul>
          <div className="ez-contact__screen">
            <ModuleVisual id="izin" />
          </div>
          <div className="ez-contact__card ez-glass">
            <h2>E-posta ile iletişim</h2>
            <p><i aria-hidden="true" className="fa-solid fa-envelope" /> <a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a></p>
            <p className="ez-muted-note">Mevcut müşterilerimiz destek taleplerini uygulama içinden ya da bu adres üzerinden iletebilir.</p>
          </div>
        </div>
        <div className="ez-contact__form ez-glass">
          <DemoRequestForm key={initialMessage} initialMessage={initialMessage} />
        </div>
      </div>
    </section>
  );
}
