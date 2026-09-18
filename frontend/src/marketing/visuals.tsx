import type { ReactNode } from "react";
import { modules } from "./content";

/* Tanıtım sitesindeki ürün ekranları. Tüm değerler örnektir; ekranlar dekoratiftir
   (aria-hidden) ve her biri ekran okuyucuya tek cümlelik bir açıklamayla sunulur. */

export type ScreenId = "overview" | "personel" | "izin" | "puantaj" | "bordro" | "ise-alim" | "risk-merkezi";

const NAV: [ScreenId, string, string][] = [
  ["overview", "Genel bakış", "fa-chart-pie"],
  ["personel", "Personel", "fa-users"],
  ["izin", "İzinler", "fa-calendar-check"],
  ["puantaj", "Mesai ve puantaj", "fa-clock"],
  ["bordro", "Bordro", "fa-file-invoice-dollar"],
  ["ise-alim", "İşe alım", "fa-user-plus"],
  ["risk-merkezi", "Risk Merkezi", "fa-shield-heart"],
];

export function AppFrame({ active, children }: { active: ScreenId; children: ReactNode }) {
  return (
    <div className="ez-app" data-active={active}>
      <div className="ez-app__bar"><i /><i /><i /><span className="ez-app__url">ekipzen.app</span></div>
      <div className="ez-app__body">
        <div className="ez-app__side">
          <span className="ez-app__brand">Ekipzen</span>
          {NAV.map(([id, label, icon]) => (
            <span key={id} className={`ez-app__nav-item${id === active ? " is-active" : ""}`}>
              <i className={`fa-solid ${icon}`} />{label}
            </span>
          ))}
        </div>
        <div className="ez-app__main">{children}</div>
      </div>
    </div>
  );
}

type NoticeTone = "success" | "primary" | "violet" | "warning";

export function FloatingNotice({ tone, icon, title, detail, className }: { tone: NoticeTone; icon: string; title: string; detail: string; className?: string }) {
  return (
    <div className={["ez-notice", `ez-notice--${tone}`, className].filter(Boolean).join(" ")}>
      <span className="ez-notice__icon"><i className={`fa-solid ${icon}`} /></span>
      <span><strong>{title}</strong>{detail}</span>
    </div>
  );
}

function ScreenHead({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="ez-app__head"><strong>{title}</strong>{children}</div>;
}

export function OverviewScreen() {
  const kpis: [string, string, string, string][] = [
    ["Aktif personel", "142", "+4 bu ay", "up"],
    ["Bekleyen izin", "7", "2 acil", "warn"],
    ["Mart bordrosu", "₺ 4,8 M", "hazır", "up"],
    ["Açık pozisyon", "5", "12 aday", "info"],
  ];
  const bars = [38, 52, 45, 66, 58, 80, 72, 88, 76, 95];
  const events = ["Ayşe Kaya'nın izni onaylandı", "Mart puantajı kapandı", "3 aday mülakat aşamasında", "2 sözleşme imza bekliyor"];
  return (
    <>
      <ScreenHead title="Genel bakış"><span className="ez-app__pill">Mart 2026</span></ScreenHead>
      <div className="ez-app__kpis">
        {kpis.map(([label, value, note, tone]) => (
          <div key={label} className="ez-app__panel ez-app__kpi">{label}<strong>{value}</strong><span className={`ez-app__note ez-app__note--${tone}`}>{note}</span></div>
        ))}
      </div>
      <div className="ez-app__split">
        <div className="ez-app__panel">
          <span className="ez-app__label">Aylık personel maliyeti</span>
          <div className="ez-app__chart">{bars.map((h, i) => <span key={i} style={{ height: `${h}%` }} />)}</div>
        </div>
        <div className="ez-app__panel ez-app__list">
          <span className="ez-app__label">Son işlemler</span>
          {events.map((e, i) => <span key={e}><i className={`fa-solid ${i < 2 ? "fa-circle-check" : "fa-circle-dot"}`} />{e}</span>)}
        </div>
      </div>
    </>
  );
}

export function PersonnelScreen() {
  const rows: [string, string, string, string, string][] = [
    ["AY", "Ahmet Yılmaz", "Yazılım", "Aktif", "ok"],
    ["EA", "Ece Arslan", "İnsan Kaynakları", "Aktif", "ok"],
    ["SK", "Selin Koç", "Tasarım", "İzinde", "info"],
    ["MD", "Mert Demir", "Satış", "Aktif", "ok"],
    ["BT", "Burak Tan", "Finans", "Aktif", "ok"],
    ["ZA", "Zeynep Aydın", "Operasyon", "Deneme süresi", "warn"],
  ];
  return (
    <>
      <ScreenHead title="Personel"><span className="ez-app__search"><i className="fa-solid fa-magnifying-glass" />Personel ara…</span><span className="ez-app__btn">+ Yeni personel</span></ScreenHead>
      <div className="ez-app__panel ez-app__table">
        <div className="ez-app__tr ez-app__tr--head"><span>Ad soyad</span><span>Departman</span><span>Durum</span></div>
        {rows.map(([ini, name, dept, status, tone], i) => (
          <div key={name} className="ez-app__tr">
            <span className="ez-app__person"><span className={`ez-app__avatar ez-app__avatar--${i % 5}`}>{ini}</span>{name}</span>
            <span>{dept}</span>
            <span><span className={`ez-app__tag ez-app__tag--${tone}`}>{status}</span></span>
          </div>
        ))}
      </div>
    </>
  );
}

export function LeaveScreen() {
  const busy = new Set([3, 4, 9, 10, 11, 17, 24, 25]);
  const days = Array.from({ length: 31 }, (_, i) => i + 1);
  const pending: [string, string][] = [["Ayşe Kaya", "Yıllık izin · 3 gün"], ["Can Öz", "Mazeret · 1 gün"], ["Deniz Er", "Yıllık izin · 5 gün"]];
  return (
    <>
      <ScreenHead title="İzinler"><span className="ez-app__pill">Ekim 2026</span></ScreenHead>
      <div className="ez-app__split">
        <div className="ez-app__panel">
          <div className="ez-app__cal">
            {["Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pz"].map((d) => <b key={d}>{d}</b>)}
            {days.map((d) => <span key={d} className={d === 15 ? "is-today" : busy.has(d) ? "is-busy" : undefined}>{d}</span>)}
          </div>
        </div>
        <div className="ez-app__panel ez-app__list">
          <span className="ez-app__label">Onay bekleyenler</span>
          {pending.map(([n, d]) => <span key={n} className="ez-app__approve"><span><strong>{n}</strong>{d}</span><span className="ez-app__btn ez-app__btn--sm">Onayla</span></span>)}
        </div>
      </div>
    </>
  );
}

export function TimesheetScreen() {
  const days = ["Pzt", "Sal", "Çar", "Per", "Cum"];
  const hours = [8, 8, 9.5, 8, 7];
  const summary: [string, string][] = [["Normal çalışma", "160 sa"], ["Fazla mesai", "12,5 sa"], ["Eksik süre", "2 sa"]];
  return (
    <>
      <ScreenHead title="Mesai ve puantaj"><span className="ez-app__pill">Mart dönemi</span></ScreenHead>
      <div className="ez-app__split">
        <div className="ez-app__panel">
          <span className="ez-app__label">Haftalık çizelge</span>
          <div className="ez-app__week">
            {days.map((d, i) => (
              <div key={d}><span className="ez-app__bar-v" style={{ height: `${hours[i] * 10}%` }} /><strong>{String(hours[i]).replace(".", ",")}</strong><span>{d}</span></div>
            ))}
          </div>
        </div>
        <div className="ez-app__panel ez-app__list">
          <span className="ez-app__label">Dönem özeti</span>
          {summary.map(([k, v]) => <span key={k} className="ez-app__kv"><span>{k}</span><strong>{v}</strong></span>)}
        </div>
      </div>
    </>
  );
}

export function PayrollScreen() {
  const totals: [string, string][] = [["Brüt toplam", "₺ 1.284.500"], ["SGK işveren payı", "₺ 198.400"], ["Net ödenecek", "₺ 912.300"]];
  const rows: [string, string, string][] = [["Ahmet Yılmaz", "₺ 48.200", "₺ 34.150"], ["Ece Arslan", "₺ 41.750", "₺ 29.980"], ["Selin Koç", "₺ 39.900", "₺ 28.710"], ["Mert Demir", "₺ 36.400", "₺ 26.320"]];
  return (
    <>
      <ScreenHead title="Bordro · Mart"><span className="ez-app__tag ez-app__tag--ok">Kontrol edildi</span></ScreenHead>
      <div className="ez-app__kpis ez-app__kpis--3">
        {totals.map(([k, v]) => <div key={k} className="ez-app__panel ez-app__kpi">{k}<strong>{v}</strong></div>)}
      </div>
      <div className="ez-app__panel ez-app__table">
        <div className="ez-app__tr ez-app__tr--head"><span>Çalışan</span><span>Brüt</span><span>Net</span></div>
        {rows.map(([n, b, net]) => <div key={n} className="ez-app__tr"><span>{n}</span><span className="ez-app__num">{b}</span><span className="ez-app__num">{net}</span></div>)}
      </div>
    </>
  );
}

export function RecruitmentScreen() {
  const cols: [string, number, [string, string][]][] = [
    ["Başvuru", 12, [["Kaan Er", "Ön Yüz Geliştirici"], ["Elif Su", "Muhasebe Uzmanı"], ["Onur Ak", "Satış Temsilcisi"]]],
    ["Mülakat", 5, [["Derya Kılıç", "Ön Yüz Geliştirici"], ["Cem Yurt", "Muhasebe Uzmanı"]]],
    ["Teklif", 2, [["İpek Ay", "Ürün Tasarımcısı"]]],
  ];
  return (
    <>
      <ScreenHead title="İşe alım"><span className="ez-app__pill">3 açık pozisyon</span></ScreenHead>
      <div className="ez-app__board">
        {cols.map(([name, count, cards]) => (
          <div key={name} className="ez-app__panel ez-app__col">
            <span className="ez-app__label">{name} <b>{count}</b></span>
            {cards.map(([n, role]) => <span key={n} className="ez-app__card"><strong>{n}</strong>{role}</span>)}
          </div>
        ))}
      </div>
    </>
  );
}

export function RiskScreen() {
  const trend = [42, 55, 48, 62, 58, 71, 64];
  const actions = ["Satış ekibiyle birebir görüşme planla", "Fazla mesai limitini gözden geçir", "İki pozisyon için teklif hazırla"];
  return (
    <>
      <ScreenHead title="Risk Merkezi"><span className="ez-app__tag ez-app__tag--dev">Geliştiriliyor</span></ScreenHead>
      <div className="ez-app__split">
        <div className="ez-app__panel">
          <span className="ez-app__label">Ayrılma riski</span>
          <div className="ez-app__score"><strong>18</strong><span>/100 · düşük</span></div>
          <div className="ez-app__chart ez-app__chart--sm">{trend.map((h, i) => <span key={i} style={{ height: `${h}%` }} />)}</div>
        </div>
        <div className="ez-app__panel ez-app__list">
          <span className="ez-app__label">Önerilen aksiyonlar</span>
          {actions.map((a, i) => <span key={a}><i className={`fa-solid ${i === 0 ? "fa-circle-check" : "fa-circle"}`} />{a}</span>)}
        </div>
      </div>
    </>
  );
}

const SCREENS: Record<Exclude<ScreenId, "overview">, { screen: () => ReactNode; notice: Parameters<typeof FloatingNotice>[0] }> = {
  personel: { screen: PersonnelScreen, notice: { tone: "primary", icon: "fa-user-plus", title: "Yeni çalışan eklendi", detail: "Zeynep Aydın · Operasyon" } },
  izin: { screen: LeaveScreen, notice: { tone: "success", icon: "fa-check", title: "İzin onaylandı", detail: "Ayşe Kaya · 3 gün" } },
  puantaj: { screen: TimesheetScreen, notice: { tone: "warning", icon: "fa-clock", title: "Mart puantajı kapandı", detail: "142 çalışan" } },
  bordro: { screen: PayrollScreen, notice: { tone: "primary", icon: "fa-turkish-lira-sign", title: "Mart bordrosu hazır", detail: "Kontrol bekliyor" } },
  "ise-alim": { screen: RecruitmentScreen, notice: { tone: "violet", icon: "fa-star", title: "Yeni aday", detail: "Ön Yüz Geliştirici · 2. tur" } },
  "risk-merkezi": { screen: RiskScreen, notice: { tone: "warning", icon: "fa-bell", title: "Yeni sinyal", detail: "Satış ekibi · izleniyor" } },
};

function isModuleScreen(id: string): id is Exclude<ScreenId, "overview"> {
  return Object.prototype.hasOwnProperty.call(SCREENS, id);
}

export function ModuleVisual({ id }: { id: string }) {
  if (!isModuleScreen(id)) return null;
  const { screen: Screen, notice } = SCREENS[id];
  const title = modules.find((m) => m.id === id)?.title ?? "Modül";
  return (
    <figure className="ez-module-visual">
      <figcaption className="ez-sr-only">{title} modülünden örnek görünüm.</figcaption>
      <div aria-hidden="true" className="ez-module-visual__stage">
        <AppFrame active={id}><Screen /></AppFrame>
        <FloatingNotice {...notice} className="ez-notice--br" />
      </div>
    </figure>
  );
}

export function HeroScreen() {
  return (
    <figure className="ez-hero-screen">
      <figcaption className="ez-sr-only">Ekipzen genel bakış ekranından örnek görünüm.</figcaption>
      <div aria-hidden="true" className="ez-hero-screen__stage">
        <AppFrame active="overview"><OverviewScreen /></AppFrame>
        <FloatingNotice tone="success" icon="fa-check" title="İzin onaylandı" detail="Ayşe Kaya · 3 gün" className="ez-notice--l" />
        <FloatingNotice tone="primary" icon="fa-turkish-lira-sign" title="Mart bordrosu hazır" detail="142 çalışan · kontrol bekliyor" className="ez-notice--r" />
        <FloatingNotice tone="violet" icon="fa-star" title="Yeni aday" detail="Ön Yüz Geliştirici · 2. tur" className="ez-notice--br" />
      </div>
    </figure>
  );
}

export function AuthScreen() {
  return (
    <figure className="ez-auth-screen">
      <figcaption className="ez-sr-only">Ekipzen genel bakış ekranından örnek görünüm.</figcaption>
      <div aria-hidden="true" className="ez-auth-screen__stage">
        <AppFrame active="overview"><OverviewScreen /></AppFrame>
        <FloatingNotice tone="success" icon="fa-check" title="İzin onaylandı" detail="Ayşe Kaya · 3 gün" className="ez-notice--br" />
      </div>
    </figure>
  );
}
