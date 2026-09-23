import type { CSSProperties } from "react";
import { ButtonLink, usePageMeta } from "../../ui";
import { STATUS_META, modules, roleViews, type MarketingModule } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";
import { ModuleVisual } from "../visuals";
import { StatusBadge } from "../StatusBadge";
import { DataFlowDiagram } from "../DataFlowDiagram";
import { Reveal, onSpotlightMove, useScrollToHash } from "../motion";

const MODULE_GLOW: Record<string, string> = {
  personel: "#6366f1", izin: "#38bdf8", puantaj: "#f59e0b", bordro: "#8b5cf6", "ise-alim": "#c026d3", "risk-merkezi": "#f43f5e",
};

function ModulePanel({ m, index }: { m: MarketingModule; index: number }) {
  return (
    <Reveal as="section" id={m.id} className="ez-module-panel ez-anchor" aria-labelledby={`${m.id}-baslik`} style={{ "--panel-glow": MODULE_GLOW[m.id] } as CSSProperties}>
      <header className="ez-module-panel__head">
        <span className="ez-module-panel__index" aria-hidden="true">{String(index + 1).padStart(2, "0")}</span>
        <ModuleIcon icon={m.icon} />
        <h2 id={`${m.id}-baslik`}>{m.title}</h2>
        <StatusBadge status={m.status} />
      </header>
      <div className="ez-module-panel__body">
        <div className="ez-module-panel__copy">
          <p className="ez-lead">{m.summary}</p>
          <ul className="ez-check-list">{m.points.map((p) => <li key={p}>{p}</li>)}</ul>
          <p className="ez-muted-note">Kimler için: {m.roles}</p>
        </div>
        <div className="ez-module-panel__visual">
          <ModuleVisual id={m.id} />
        </div>
      </div>
    </Reveal>
  );
}

export function FeaturesPage() {
  usePageMeta("Özellikler", "Ekipzen modülleri: Risk Merkezi, personel yönetimi, izin, mesai ve puantaj, bordro ve işe alım.");
  useScrollToHash();
  return (
    <>
      <PageHero
        glow="cyan"
        eyebrow="Özellikler"
        title="Personel, izin ve işe alım için ortak çalışma alanı"
        lead="Personel bilgileri, izin talepleri ve işe alım kayıtları ortak bir veri düzeninde yönetilir. Her modülün kullanım durumu aşağıda belirtilir."
      >
        <nav aria-label="Modüller" className="ez-chips">
          {modules.map((m) => (
            <a key={m.id} href={`#${m.id}`} className="ez-chip ez-glass" title={STATUS_META[m.status].label}>
              <span className={`ez-dot ez-dot--${m.status}`} aria-hidden="true" />{m.title}
              <span className="ez-sr-only"> ({STATUS_META[m.status].label})</span>
            </a>
          ))}
        </nav>
      </PageHero>

      <div className="ez-container ez-modules">
        {modules.map((m, i) => <ModulePanel key={m.id} m={m} index={i} />)}
      </div>

      <section className="ez-section ez-section--muted ez-glow ez-flow-band" aria-labelledby="akis-baslik">
        <div className="ez-container ez-split">
          <Reveal>
            <p className="ez-eyebrow">Bağlantılı veri</p>
            <h2 id="akis-baslik">İK süreçleri ortak çalışan kaydıyla bağlantılıdır</h2>
            <p className="ez-lead">Personel kaydı, izin ve işe alım süreçleri arasında bilgi akışını sağlar. Pilot kapsamındaki puantaj ve bordro modülleri de bu veri düzenine dayanır.</p>
          </Reveal>
          <DataFlowDiagram />
        </div>
      </section>

      <section className="ez-section" aria-labelledby="roller-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Roller</p>
            <h2 id="roller-baslik">İK yöneticisi, yönetici ve çalışan için ayrı çalışma alanları</h2>
            <p className="ez-lead">Her kullanıcı yalnız rolünün gerektirdiği ekranlara ve verilere erişir. Yetki kontrolü arayüzün yanı sıra sunucu tarafında da uygulanır.</p>
          </Reveal>
          <ul className="ez-roles">
            {roleViews.map((r, i) => (
              <Reveal as="li" key={r.title} delay={i * 90} className={`ez-pillar ez-spotlight ez-roles__item${i === 0 ? " ez-roles__item--wide" : ""}`} onPointerMove={onSpotlightMove}>
                <h3>{r.title}</h3>
                <p>{r.text}</p>
                <ul className="ez-check-list">{r.points.map((p) => <li key={p}>{p}</li>)}</ul>
              </Reveal>
            ))}
          </ul>
          <div className="ez-actions"><ButtonLink to="/guvenlik" variant="secondary">Yetkilendirme yaklaşımını inceleyin</ButtonLink></div>
        </div>
      </section>

      <CtaBand />
    </>
  );
}
