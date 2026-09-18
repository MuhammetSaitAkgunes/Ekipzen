import { ButtonLink, usePageMeta } from "../../ui";
import { modules, roleViews } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";
import { ModuleVisual } from "../visuals";

export function FeaturesPage() {
  usePageMeta("Özellikler", "Ekipzen modülleri: Risk Merkezi, personel yönetimi, izin, mesai ve puantaj, bordro ve işe alım.");
  return (
    <>
      <PageHero
        eyebrow="Özellikler"
        title="İK süreçlerinin tamamı için tek bir çalışma alanı"
        lead="Modüller aynı personel kaydı üzerinde çalışır; bir kez girilen bilgi tüm süreçlerde tutarlı biçimde kullanılır."
      >
        <nav aria-label="Modüller" className="ez-chips">
          {modules.map((m) => <a key={m.id} href={`#${m.id}`} className="ez-chip">{m.title}</a>)}
        </nav>
      </PageHero>

      {modules.map((m, i) => (
        <section key={m.id} id={m.id} className={`ez-section ez-anchor${i % 2 ? " ez-section--muted" : ""}`} aria-labelledby={`${m.id}-baslik`}>
          <div className={`ez-container ez-split${i % 2 ? " ez-split--reverse" : ""}`}>
            <div>
              <ModuleIcon icon={m.icon} />
              <h2 id={`${m.id}-baslik`}>{m.title}</h2>
              <p className="ez-lead">{m.summary}</p>
              <ul className="ez-check-list">
                {m.points.map((p) => <li key={p}>{p}</li>)}
              </ul>
              <p className="ez-muted-note">Kimler için: {m.roles}</p>
            </div>
            <ModuleVisual id={m.id} />
          </div>
        </section>
      ))}

      <section className="ez-section" aria-labelledby="roller-baslik">
        <div className="ez-container">
          <div className="ez-section__head">
            <p className="ez-eyebrow">Roller</p>
            <h2 id="roller-baslik">İK yöneticisi, yönetici ve çalışan için ayrı çalışma alanları</h2>
            <p className="ez-lead">Her kullanıcı yalnız rolünün gerektirdiği ekranlara ve verilere erişir. Yetki kontrolü arayüzün yanı sıra sunucu tarafında da uygulanır.</p>
          </div>
          <ul className="ez-grid-3">
            {roleViews.map((r) => (
              <li key={r.title} className="ez-pillar">
                <h3>{r.title}</h3>
                <p>{r.text}</p>
                <ul className="ez-check-list">{r.points.map((p) => <li key={p}>{p}</li>)}</ul>
              </li>
            ))}
          </ul>
          <div className="ez-actions"><ButtonLink to="/guvenlik" variant="secondary">Yetkilendirme yaklaşımını inceleyin</ButtonLink></div>
        </div>
      </section>

      <CtaBand />
    </>
  );
}
