import type { ReactNode } from "react";

function MockCard({ title, badge, children, className }: { title: string; badge?: string; children: ReactNode; className?: string }) {
  return (
    <div className={["ez-mock", className].filter(Boolean).join(" ")}>
      <div className="ez-mock__head">
        <span className="ez-mock__title">{title}</span>
        {badge && <span className="ez-mock__badge">{badge}</span>}
      </div>
      {children}
    </div>
  );
}

export function RiskScoreCard() {
  const bars = [42, 55, 48, 62, 58, 71, 64];
  return (
    <MockCard title="Ayrılma riski" badge="Bu ay">
      <div className="ez-mock__metric"><strong>18</strong><span>/100 · düşük</span></div>
      <div className="ez-mock__bars">
        {bars.map((h, i) => <span key={i} style={{ height: `${h}%` }} />)}
      </div>
    </MockCard>
  );
}

export function LeaveCalendarCard() {
  const days = Array.from({ length: 14 }, (_, i) => i + 1);
  const busy = new Set([3, 4, 9, 10, 11]);
  return (
    <MockCard title="İzin takvimi" badge="Ekim">
      <div className="ez-mock__calendar">
        {days.map((d) => <span key={d} className={busy.has(d) ? "is-busy" : undefined}>{d}</span>)}
      </div>
      <div className="ez-mock__row"><span className="ez-mock__dot" />3 onay bekliyor</div>
    </MockCard>
  );
}

export function PayrollSummaryCard() {
  const rows: [string, string][] = [["Brüt toplam", "₺ 1.284.500"], ["SGK işveren payı", "₺ 198.400"], ["Net ödenecek", "₺ 912.300"]];
  return (
    <MockCard title="Bordro özeti" badge="Kontrol edildi">
      <dl className="ez-mock__list">
        {rows.map(([k, v]) => (<div key={k}><dt>{k}</dt><dd>{v}</dd></div>))}
      </dl>
    </MockCard>
  );
}

export function ActionListCard() {
  const items = ["Satış ekibiyle birebir görüşme planla", "Fazla mesai limitini gözden geçir", "İki pozisyon için teklif hazırla"];
  return (
    <MockCard title="Önerilen aksiyonlar">
      <ul className="ez-mock__tasks">
        {items.map((t, i) => (<li key={t} className={i === 0 ? "is-done" : undefined}><span aria-hidden="true" />{t}</li>))}
      </ul>
    </MockCard>
  );
}

/** Hero kompozisyonu: katmanlı panel parçaları. Değerler örnektir. */
export function HeroVisual() {
  return (
    <figure className="ez-hero-visual">
      <figcaption className="ez-sr-only">Ekipzen panelinden örnek risk, izin, bordro ve aksiyon kartları.</figcaption>
      <div aria-hidden="true" className="ez-hero-visual__stack">
        <RiskScoreCard />
        <PayrollSummaryCard />
        <LeaveCalendarCard />
        <ActionListCard />
      </div>
    </figure>
  );
}

const moduleVisuals: Record<string, () => ReactNode> = {
  "risk-merkezi": () => <><RiskScoreCard /><ActionListCard /></>,
  personel: () => <ActionListCard />,
  izin: () => <LeaveCalendarCard />,
  puantaj: () => <LeaveCalendarCard />,
  bordro: () => <PayrollSummaryCard />,
  "ise-alim": () => <ActionListCard />,
};

export function ModuleVisual({ id }: { id: string }) {
  const render = moduleVisuals[id];
  if (!render) return null;
  return (
    <figure className="ez-module-visual">
      <figcaption className="ez-sr-only">Modülden örnek görünüm.</figcaption>
      <div aria-hidden="true" className="ez-module-visual__stack">{render()}</div>
    </figure>
  );
}
