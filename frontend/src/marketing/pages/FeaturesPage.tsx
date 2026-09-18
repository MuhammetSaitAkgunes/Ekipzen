import { useCallback, useEffect, useState } from "react";
import { ButtonLink, usePageMeta } from "../../ui";
import { STATUS_META, modules, roleViews, type MarketingModule } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";
import { ModuleVisual } from "../visuals";
import { StatusBadge } from "../StatusBadge";
import { StickyModuleNav } from "../StickyModuleNav";
import { DataFlowDiagram } from "../DataFlowDiagram";
import { Reveal, onSpotlightMove, useInView, useScrollToHash } from "../motion";

function ModuleBlock({ m, onActive }: { m: MarketingModule; onActive: (id: string) => void }) {
  const { ref, inView } = useInView<HTMLElement>({ threshold: 0, rootMargin: "-30% 0px -50% 0px" });
  useEffect(() => { if (inView) onActive(m.id); }, [inView, m.id, onActive]);
  return (
    <section ref={ref} id={m.id} className="ez-feature-block ez-anchor" aria-labelledby={`${m.id}-baslik`}>
      <Reveal className="ez-feature-block__copy">
        <div className="ez-feature-block__head"><ModuleIcon icon={m.icon} /><StatusBadge status={m.status} /></div>
        <h2 id={`${m.id}-baslik`}>{m.title}</h2>
        <p className="ez-lead">{m.summary}</p>
        <ul className="ez-check-list">{m.points.map((p) => <li key={p}>{p}</li>)}</ul>
        <p className="ez-muted-note">Kimler için: {m.roles}</p>
      </Reveal>
      <Reveal delay={120} className="ez-feature-block__visual ez-spotlight" onPointerMove={onSpotlightMove}>
        <ModuleVisual id={m.id} />
      </Reveal>
    </section>
  );
}

export function FeaturesPage() {
  usePageMeta("Özellikler", "Ekipzen modülleri: Risk Merkezi, personel yönetimi, izin, mesai ve puantaj, bordro ve işe alım.");
  useScrollToHash();
  const [activeId, setActiveId] = useState(modules[0].id);
  const onActive = useCallback((id: string) => setActiveId(id), []);
  return (
    <>
      <PageHero
        eyebrow="Özellikler"
        title="İK süreçlerinin tamamı için tek bir çalışma alanı"
        lead="Modüller aynı personel kaydı üzerinde çalışır; bir kez girilen bilgi tüm süreçlerde tutarlı biçimde kullanılır."
      >
        <nav aria-label="Modüller" className="ez-chips">
          {modules.map((m) => (
            <a key={m.id} href={`#${m.id}`} className="ez-chip" title={STATUS_META[m.status].label}>
              <span className={`ez-dot ez-dot--${m.status}`} aria-hidden="true" />{m.title}
              <span className="ez-sr-only"> ({STATUS_META[m.status].label})</span>
            </a>
          ))}
        </nav>
      </PageHero>

      <div className="ez-container ez-features-layout">
        <aside className="ez-features-layout__nav">
          <StickyModuleNav items={modules.map((m) => ({ id: m.id, title: m.title, status: m.status }))} activeId={activeId} />
        </aside>
        <div className="ez-features-layout__blocks">
          {modules.map((m) => <ModuleBlock key={m.id} m={m} onActive={onActive} />)}
        </div>
      </div>

      <section className="ez-section ez-section--muted" aria-labelledby="akis-baslik">
        <div className="ez-container ez-split">
          <Reveal>
            <p className="ez-eyebrow">Bağlantılı veri</p>
            <h2 id="akis-baslik">Tüm modüller aynı personel kaydına bağlıdır</h2>
            <p className="ez-lead">Bir çalışanın bilgisi bir kez girilir; izin, puantaj, bordro ve işe alım aynı kaydı kullanır. Böylece kayıtlar arasında tutarsızlık oluşmaz.</p>
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
