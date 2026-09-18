import { useEffect } from "react";
import { ButtonLink, usePageMeta } from "../../ui";

export function NotFoundPage() {
  usePageMeta("Sayfa bulunamadı");
  useEffect(() => {
    // 404 arama motorlarınca indekslenmemeli; sayfadan ayrılınca etiket kaldırılır.
    const meta = document.createElement("meta");
    meta.name = "robots";
    meta.content = "noindex";
    document.head.appendChild(meta);
    return () => {
      meta.remove();
    };
  }, []);
  return (
    <section className="ez-section">
      <div className="ez-container ez-narrow">
        <p className="ez-eyebrow">404</p>
        <h1>Sayfa bulunamadı</h1>
        <p className="ez-lead">Aradığınız sayfa kaldırılmış ya da adresi değişmiş olabilir.</p>
        <div className="ez-actions">
          <ButtonLink to="/">Ana sayfaya dön</ButtonLink>
          <ButtonLink to="/iletisim" variant="secondary">Bize ulaşın</ButtonLink>
        </div>
      </div>
    </section>
  );
}
