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

export function PersonnelListCard() {
  const rows: [string, string, string][] = [
    ["AY", "Ahmet Yılmaz", "Yazılım"],
    ["EA", "Ece Arslan", "İnsan Kaynakları"],
    ["SK", "Selin Koç", "Tasarım"],
    ["MD", "Mert Demir", "Satış"],
  ];
  return (
    <MockCard title="Personel" badge="42 kişi">
      <ul className="ez-mock__table">
        {rows.map(([initials, name, dept]) => (
          <li key={name}><span className="ez-mock__avatar">{initials}</span><span>{name}</span><span className="ez-mock__muted">{dept}</span></li>
        ))}
      </ul>
    </MockCard>
  );
}

export function TimesheetGridCard() {
  const days = ["Pzt", "Sal", "Çar", "Per", "Cum"];
  const hours = [8, 8, 9.5, 8, 7];
  return (
    <MockCard title="Haftalık puantaj" badge="Eylül">
      <div className="ez-mock__grid">
        {days.map((d, i) => (
          <div key={d}><span className="ez-mock__muted">{d}</span><strong>{hours[i]} sa</strong><span className="ez-mock__bar" style={{ height: `${hours[i] * 8}px` }} /></div>
        ))}
      </div>
    </MockCard>
  );
}

export function RecruitmentBoardCard() {
  const columns: [string, number][] = [["Başvuru", 12], ["Mülakat", 5], ["Teklif", 2]];
  return (
    <MockCard title="İşe alım" badge="3 pozisyon">
      <div className="ez-mock__kanban">
        {columns.map(([name, count]) => (
          <div key={name}><span className="ez-mock__muted">{name}</span><strong>{count}</strong>{Array.from({ length: Math.min(count, 3) }, (_, i) => <span key={i} className="ez-mock__chip" />)}</div>
        ))}
      </div>
    </MockCard>
  );
}

/** Hero kompozisyonu: düz, katmanlı panel parçaları. Değerler örnektir. */
export function HeroVisual() {
  return (
    <figure className="ez-hero-visual">
      <figcaption className="ez-sr-only">Ekipzen panelinden örnek görünüm: personel listesi, izin takvimi ve bordro özeti.</figcaption>
      <div aria-hidden="true" className="ez-hero-visual__stage">
        <div className="ez-hero-visual__main"><PersonnelListCard /></div>
        <div className="ez-hero-visual__float ez-hero-visual__float--a ez-float"><LeaveCalendarCard /></div>
        <div className="ez-hero-visual__float ez-hero-visual__float--b ez-float ez-float--slow"><PayrollSummaryCard /></div>
      </div>
    </figure>
  );
}

const moduleVisuals: Record<string, () => ReactNode> = {
  "risk-merkezi": () => <><RiskScoreCard /><ActionListCard /></>,
  personel: () => <PersonnelListCard />,
  izin: () => <LeaveCalendarCard />,
  puantaj: () => <TimesheetGridCard />,
  bordro: () => <PayrollSummaryCard />,
  "ise-alim": () => <RecruitmentBoardCard />,
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
