import { usePageMeta } from "../../ui";

export function HomePage() {
  usePageMeta("Ekipzen", "Ekipzen ile personel, izin, puantaj, bordro ve işe alım süreçlerinizi tek, güvenli platformda yönetin.");
  return (
    <section className="ez-hero">
      <div className="ez-container">
        <h1>Ekibinizi tek yerden, güvenle yönetin</h1>
      </div>
    </section>
  );
}
