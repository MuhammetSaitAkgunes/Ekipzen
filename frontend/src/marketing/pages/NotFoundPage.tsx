import { ButtonLink, usePageMeta } from "../../ui";

export function NotFoundPage() {
  usePageMeta("Sayfa bulunamadı");
  return (
    <section className="ez-section">
      <div className="ez-container ez-narrow">
        <p className="ez-eyebrow">404</p>
        <h1>Sayfa bulunamadı</h1>
        <p className="ez-lead">Aradığınız sayfa taşınmış ya da hiç var olmamış olabilir.</p>
        <div className="ez-actions">
          <ButtonLink to="/">Ana sayfaya dön</ButtonLink>
          <ButtonLink to="/iletisim" variant="secondary">Bize ulaşın</ButtonLink>
        </div>
      </div>
    </section>
  );
}
