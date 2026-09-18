import { useId, type ReactNode } from "react";
import { ButtonLink } from "../ui";

export function ModuleIcon({ icon }: { icon: string }) {
  return <span className="ez-icon"><i aria-hidden="true" className={`fa-solid ${icon}`} /></span>;
}

export function PageHero({ eyebrow, title, lead, children }: { eyebrow: string; title: string; lead: string; children?: ReactNode }) {
  return (
    <section className="ez-page-hero">
      <div className="ez-container ez-narrow">
        <p className="ez-eyebrow">{eyebrow}</p>
        <h1>{title}</h1>
        <p className="ez-lead">{lead}</p>
        {children}
      </div>
    </section>
  );
}

export function CtaBand({ title = "Ekipzen'i kendi süreçleriniz üzerinden değerlendirin", text = "Demo görüşmesinde ürünü şirketinizin izin, puantaj ve bordro akışları üzerinden birlikte inceleyelim." }: { title?: string; text?: string }) {
  const id = useId();
  return (
    <section className="ez-section" aria-labelledby={id}>
      <div className="ez-container">
        <div className="ez-cta-band">
          <div>
            <h2 id={id}>{title}</h2>
            <p>{text}</p>
          </div>
          <div className="ez-actions">
            <ButtonLink to="/iletisim" size="lg">Demo talep edin</ButtonLink>
            <ButtonLink to="/fiyatlandirma" size="lg" variant="secondary">Paketleri inceleyin</ButtonLink>
          </div>
        </div>
      </div>
    </section>
  );
}

export function FaqList({ items }: { items: { q: string; a: string }[] }) {
  return (
    <div className="ez-faq">
      {items.map((item) => (
        <details key={item.q} className="ez-faq__item">
          <summary>{item.q}<i aria-hidden="true" className="fa-solid fa-chevron-down" /></summary>
          <p>{item.a}</p>
        </details>
      ))}
    </div>
  );
}
