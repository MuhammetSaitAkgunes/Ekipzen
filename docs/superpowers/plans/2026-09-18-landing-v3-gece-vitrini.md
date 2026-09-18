# Landing v3 — Gece Vitrini Implementation Plan

> **For agentic workers:** Bu plan kullanıcı kararıyla **satır içi** yürütülür (superpowers:executing-plans tarzı). Alt ajan (subagent-driven-development, Explore, review ajanları) **kullanılmaz**. Adımlar `- [ ]` ile izlenir.

**Goal:** Tanıtım sitesini ve giriş/davet ekranlarını koyu, dokulu, büyük ürün ekranlarıyla dolu "gece vitrini" diline taşımak. İçerik, etkileşim mantığı ve testler korunur.

**Architecture:**
- Siteye ve auth köküne özel `.ez-night` sınıfı, mevcut `--ez-*` token'larını koyu değerlerle yeniden tanımlar; uygulama (`:root`) etkilenmez.
- Doku, ışık ve cam yardımcıları `night.css`'te; büyük ürün ekranları `screens.css` ve `visuals.tsx`'te.
- Sayfa düzenleri `marketing.css` / `marketing-motion.css` içinde, sayfa sayfa yeniden yazılır.

**Tech Stack:** React 18, Vite, TypeScript (`erasableSyntaxOnly`), react-router-dom 7, düz CSS, Vitest + Testing Library (jsdom).

**Spec:** `docs/superpowers/specs/2026-09-18-landing-v3-gece-vitrini-design.md`

## Global Constraints

- **Yeni bağımlılık yok.** Efektler yalnız CSS + küçük React/JS.
- **Bulanıklık** için `filter: blur()` kullanılmaz; ışık bulutları radyal degrade. `backdrop-filter` yalnız cam yüzeylerde.
- **Değişmeyenler:**
  - `content.ts` metinleri; modül durumları: Personel/İzin/İşe alım = Kullanımda, Puantaj/Bordro = Pilotta, Risk Merkezi = Geliştiriliyor.
  - Fiyat ve kapsam taahhüdü yok.
  - Anchor adresleri ve `aria-labelledby` başlık id'leri.
- **Metin üslubu:** Ölçülü, kurumsal, özgün (tone guard testi yeşil kalır).
- **Kontrast:** Tüm metin/zemin çiftleri WCAG AA ≥4.5:1.
- **Hareketi azalt:** `prefers-reduced-motion: reduce` açıkken tüm içerik anında görünür, süzülme ve ışık animasyonları durur.
- **Görünürlük:** Ekranın üst kısmındaki (giriş bölümü) içerik IntersectionObserver'a bağlı olamaz; yalnız CSS giriş animasyonu.
- **Kapsam:** Uygulama (`/app/*`) stilleri ve bileşenleri değişmez.
- **Doğrulama:**
  - Her görev sonunda `npm --prefix frontend run lint`, `npm --prefix frontend test -- --run`, `npm --prefix frontend run build` temiz olmalı.
  - Görsel görevlerde 1280px ve 375px'te gerçek ekran görüntüsüyle baştan sona kontrol yapılır; yatay taşma (`scrollWidth > innerWidth`) ve kalıcı gizli `.ez-reveal` olmamalı.
- Commit mesajları `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>` ile biter. `.claude/launch.json` commit'lenmez.

## Dosya haritası

| Dosya | Sorumluluk |
|---|---|
| `frontend/src/styles/night.css` (yeni) | `.ez-night` token'ları, zemin tonları, gren, ızgara, ışık bulutu, cam (`.ez-glass`), degrade metin, koyu buton/alan ayarları |
| `frontend/src/styles/screens.css` (yeni) | `AppFrame`, modül ekranları, `FloatingNotice` stilleri |
| `frontend/src/marketing/visuals.tsx` (yeniden yazım) | `AppFrame`, 7 ekran, `FloatingNotice`, `HeroScreen`, `ModuleVisual` |
| `frontend/src/marketing/visuals.test.tsx` | Yeni ekran testleri |
| `frontend/src/marketing/useScrolled.ts` (yeni) | Üst menünün kaydırma durumu |
| `frontend/src/marketing/MarketingLayout.tsx` | `.ez-night`, kaydırma sınıfı, logo tonu |
| `frontend/src/marketing/sections.tsx` | `PageHero` ışık rengi (`glow` prop) |
| `frontend/src/marketing/pages/*.tsx` | Sayfa düzeni sınıfları / yeni görseller |
| `frontend/src/styles/marketing.css`, `marketing-motion.css` | Sayfa düzenleri (koyu dil) |
| `frontend/src/auth/AuthLayout.tsx`, `styles/auth.css` | Koyu giriş/davet ekranı |
| `frontend/src/styles/contrast.test.ts`, `tokens.test.ts` | Koyu token kontrastları |
| `frontend/src/main.tsx` | Yeni CSS import'ları |

---

### Task 1: Gece teması katmanı (`night.css`)

**Files:**
- Create: `frontend/src/styles/night.css`
- Modify: `frontend/src/main.tsx` (import `./styles/night.css` after `marketing-motion.css`, before `auth.css`)
- Test: `frontend/src/styles/contrast.test.ts`

**Interfaces:**
- Produces:
  - `.ez-night` token kapsamı.
  - `.ez-glass` (cam yüzey).
  - `.ez-grain` (sabit gren katmanı, kökte tek bir eleman).
  - `.ez-glow` + `--glow-a` / `--glow-b` (ışık bulutu için değişkenler).
  - `.ez-gridlines` (ızgara).
  - `.ez-gradient-text` (degrade metin).
  - `--ez-night-1`, `--ez-night-2`, `--ez-night-3` (zemin tonları).
  - `--ez-glass-bg`, `--ez-glass-border`.
  - `--ez-accent-cyan` (`#7dd3fc`), `--ez-accent-violet` (`#c084fc`), `--ez-grad-primary`.

- [ ] **Step 1: Kontrast testini yaz (başarısız olmalı)**

`contrast.test.ts` sonuna ekle:

```ts
// Gece teması (.ez-night) — tanıtım sitesi ve giriş ekranı.
const NIGHT = {
  "--ez-text": "#f1f5f9",
  "--ez-text-2": "#cbd5e1",
  "--ez-text-3": "#94a3b8",
  "--ez-night-1": "#070b18",
  "--ez-night-2": "#0b1122",
  "--ez-night-3": "#0e1530",
  "--ez-surface": "#111a30", // cam yüzeyin koyu zemine karışmış yaklaşık tonu
  "--ez-primary": "#a5b4fc",
  "--ez-success": "#6ee7b7",
  "--ez-warning": "#fcd34d",
  "--ez-danger": "#fda4af",
  "--ez-info": "#7dd3fc",
} as const;

test("night.css gece token'larını bu testteki literal'lerle tanımlar", () => {
  const css = readFileSync(join(__dirname, "night.css"), "utf8").toLowerCase();
  expect(css).toContain(".ez-night");
  for (const [name, hex] of Object.entries(NIGHT)) expect(css, name).toContain(`${name}: ${hex}`);
});

test.each([
  ["--ez-text"], ["--ez-text-2"], ["--ez-text-3"], ["--ez-primary"],
  ["--ez-success"], ["--ez-warning"], ["--ez-danger"], ["--ez-info"],
] as const)("gece temasında %s tüm zemin tonlarında AA (>=4.5) sağlar", (fg) => {
  for (const bg of ["--ez-night-1", "--ez-night-2", "--ez-night-3", "--ez-surface"] as const) {
    expect(contrastRatio(NIGHT[fg], NIGHT[bg]), `${fg} / ${bg}`).toBeGreaterThanOrEqual(4.5);
  }
});
```

- [ ] **Step 2: Çalıştır, dosya yok diye FAIL gör**

Run: `npx vitest run src/styles/contrast.test.ts` (cwd `frontend`). Expected: `ENOENT night.css`.

- [ ] **Step 3: `night.css` yaz**

```css
/* Gece teması — tanıtım sitesi ve giriş ekranı. Kök: .ez-night (uygulama etkilenmez). */
.ez-night {
  --ez-night-1: #070b18;
  --ez-night-2: #0b1122;
  --ez-night-3: #0e1530;
  --ez-bg: var(--ez-night-1);
  --ez-surface: #111a30;
  --ez-surface-2: #16203a;
  --ez-text: #f1f5f9;
  --ez-text-2: #cbd5e1;
  --ez-text-3: #94a3b8;
  --ez-text-4: #64748b;
  --ez-border: rgb(255 255 255 / 0.1);
  --ez-border-strong: rgb(255 255 255 / 0.2);
  --ez-border-soft: rgb(255 255 255 / 0.06);
  --ez-primary: #a5b4fc;
  --ez-primary-hover: #c7d2fe;
  --ez-primary-soft: rgb(99 102 241 / 0.18);
  --ez-on-primary: #ffffff;
  --ez-accent-cyan: #7dd3fc;
  --ez-accent-violet: #c084fc;
  --ez-grad-primary: linear-gradient(135deg, #38bdf8, #6366f1);
  --ez-grad-text: linear-gradient(90deg, #7dd3fc, #a5b4fc, #f0abfc);
  --ez-success: #6ee7b7; --ez-success-strong: #6ee7b7; --ez-success-soft: rgb(52 211 153 / 0.15);
  --ez-warning: #fcd34d; --ez-warning-strong: #fcd34d; --ez-warning-soft: rgb(251 191 36 / 0.15);
  --ez-danger: #fda4af; --ez-danger-strong: #fda4af; --ez-danger-soft: rgb(244 63 94 / 0.15);
  --ez-info: #7dd3fc; --ez-info-strong: #7dd3fc; --ez-info-soft: rgb(56 189 248 / 0.15);
  --ez-glass-bg: rgb(255 255 255 / 0.045);
  --ez-glass-border: rgb(255 255 255 / 0.11);
  --ez-focus-ring: 0 0 0 3px rgb(125 211 252 / 0.55);
  --ez-shadow-xs: 0 1px 2px rgb(0 0 0 / 0.4);
  --ez-shadow-sm: 0 2px 6px rgb(0 0 0 / 0.4);
  --ez-shadow-md: 0 12px 30px rgb(0 0 0 / 0.45);
  --ez-shadow-lg: 0 24px 60px rgb(0 0 0 / 0.55);
  color-scheme: dark;
  background: var(--ez-night-1);
  color: var(--ez-text-2);
}

.ez-grain { position: fixed; inset: 0; z-index: 60; pointer-events: none; opacity: 0.13; mix-blend-mode: overlay;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='160' height='160'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='.85' numOctaves='2' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='160' height='160' filter='url(%23n)'/%3E%3C/svg%3E"); }

.ez-gridlines { background-image:
  linear-gradient(rgb(255 255 255 / 0.035) 1px, transparent 1px),
  linear-gradient(90deg, rgb(255 255 255 / 0.035) 1px, transparent 1px);
  background-size: 32px 32px; }

.ez-glow { position: relative; isolation: isolate; }
.ez-glow::before { content: ""; position: absolute; inset: 0; z-index: -1; pointer-events: none; background:
  radial-gradient(38rem 22rem at var(--glow-a-x, 15%) var(--glow-a-y, 0%), var(--glow-a, rgb(99 102 241 / 0.5)), transparent 70%),
  radial-gradient(34rem 22rem at var(--glow-b-x, 88%) var(--glow-b-y, 25%), var(--glow-b, rgb(56 189 248 / 0.28)), transparent 70%); }

.ez-glass { background: var(--ez-glass-bg); border: 1px solid var(--ez-glass-border); border-radius: var(--r-xl); backdrop-filter: blur(12px) saturate(1.2); }

.ez-gradient-text { background: var(--ez-grad-text); -webkit-background-clip: text; background-clip: text; color: transparent; }

/* Paylaşılan ui/ bileşenlerinin gece görünümü */
.ez-night .ez-btn--primary { background: var(--ez-grad-primary); color: #fff; box-shadow: 0 8px 24px rgb(99 102 241 / 0.4); }
.ez-night .ez-btn--primary:hover { background: var(--ez-grad-primary); filter: brightness(1.1); }
.ez-night .ez-btn--secondary { background: rgb(255 255 255 / 0.05); color: var(--ez-text); border-color: var(--ez-border-strong); }
.ez-night .ez-btn--secondary:hover { background: rgb(255 255 255 / 0.1); }
.ez-night .ez-btn--ghost { color: var(--ez-text-2); }
.ez-night .ez-btn--ghost:hover { background: rgb(255 255 255 / 0.08); color: var(--ez-text); }
```

- [ ] **Step 4:** `main.tsx`'e `import "./styles/night.css";` ekle (`marketing-motion.css` satırından sonra).
- [ ] **Step 5:** Testi çalıştır → PASS. Oranlardan biri 4.5 altındaysa yalnız o hex'i açarak düzelt ve hem testte hem CSS'te eşitle.
- [ ] **Step 6:** Lint, test ve build; ardından commit `feat(landing): gece teması token ve doku katmanı`.

### Task 2: Ürün ekranları (`visuals.tsx`, `screens.css`)

**Files:**
- Rewrite: `frontend/src/marketing/visuals.tsx`
- Create: `frontend/src/styles/screens.css` (+ `main.tsx` import, `night.css` sonrası)
- Rewrite: `frontend/src/marketing/visuals.test.tsx`

**Interfaces:**
- Produces:
  - `type ScreenId = "overview" | "personel" | "izin" | "puantaj" | "bordro" | "ise-alim" | "risk-merkezi"`
  - `AppFrame({ active, children }: { active: ScreenId; children: ReactNode })`: `.ez-app` kökü, menüde `aria-current` yok (dekoratif), seçili öğe `.is-active`.
  - `FloatingNotice({ tone, icon, title, detail, className }: { tone: "success" | "primary" | "violet" | "warning"; icon: string; title: string; detail: string; className?: string })`: `.ez-notice`.
  - `HeroScreen()`: `figure.ez-hero-screen`; `OverviewScreen` + 3 bildirim.
  - `ModuleVisual({ id }: { id: string })`: `figure.ez-module-visual`; ilgili ekran + 1 bildirim. Bilinmeyen id için `null`.
  - `AuthScreen()`: `figure.ez-auth-screen`; küçük overview + 1 bildirim.
  - Ekran içerikleri: `OverviewScreen`, `PersonnelScreen`, `LeaveScreen`, `TimesheetScreen`, `PayrollScreen`, `RecruitmentScreen`, `RiskScreen`. Hepsi dışa aktarılır, parametresiz.
- Removes: `RiskScoreCard`, `LeaveCalendarCard`, `PayrollSummaryCard`, `ActionListCard`, `PersonnelListCard`, `TimesheetGridCard`, `RecruitmentBoardCard`, `HeroVisual`.
  - `HomePage.tsx`: `HeroVisual` yerine `HeroScreen`.
  - `AuthLayout.tsx`: kartlar yerine `AuthScreen`. Bu iki import aynı commit'te güncellenir.

- [ ] **Step 1: Testleri yaz**

```tsx
import { render } from "@testing-library/react";
import { expect, test } from "vitest";
import { AuthScreen, HeroScreen, ModuleVisual } from "./visuals";
import { modules } from "./content";

test("her modülün büyük ürün ekranı vardır, menüde kendi modülü seçilidir", () => {
  for (const m of modules) {
    const { container, unmount } = render(<ModuleVisual id={m.id} />);
    const app = container.querySelector(".ez-app");
    expect(app, m.id).not.toBeNull();
    expect(app!.getAttribute("data-active")).toBe(m.id);
    expect(container.querySelector(".ez-notice"), m.id).not.toBeNull();
    expect(container.querySelector("figcaption")).toHaveTextContent("örnek görünüm");
    expect(container.querySelector("[aria-hidden='true'] .ez-app")).not.toBeNull();
    unmount();
  }
});

test("bilinmeyen modül için görsel çizilmez", () => {
  const { container } = render(<ModuleVisual id="yok" />);
  expect(container.firstChild).toBeNull();
});

test("giriş ekranı genel bakışı ve üç bildirimi gösterir", () => {
  const { container } = render(<HeroScreen />);
  expect(container.querySelector(".ez-app[data-active='overview']")).not.toBeNull();
  expect(container.querySelectorAll(".ez-notice")).toHaveLength(3);
  expect(container.querySelector("figcaption")).toHaveTextContent("örnek görünüm");
});

test("giriş sayfası görseli genel bakış ve bir bildirim içerir", () => {
  const { container } = render(<AuthScreen />);
  expect(container.querySelector(".ez-app[data-active='overview']")).not.toBeNull();
  expect(container.querySelectorAll(".ez-notice")).toHaveLength(1);
});

test("Risk Merkezi ekranı geliştirme aşamasında olduğunu belirtir", () => {
  const { container } = render(<ModuleVisual id="risk-merkezi" />);
  expect(container.textContent).toMatch(/geliştiriliyor/i);
});
```

- [ ] **Step 2:** Çalıştır → FAIL (export yok).
- [ ] **Step 3: `visuals.tsx` yaz**

- **Menü öğeleri:** `[["overview","Genel bakış","fa-chart-pie"],["personel","Personel","fa-users"],["izin","İzinler","fa-calendar-check"],["puantaj","Mesai ve puantaj","fa-clock"],["bordro","Bordro","fa-file-invoice-dollar"],["ise-alim","İşe alım","fa-user-plus"],["risk-merkezi","Risk Merkezi","fa-shield-heart"]]`.
- **`AppFrame`:** `<div className="ez-app" data-active={active}>` içinde:
  - `.ez-app__bar` (üç nokta + `ekipzen.app` adres alanı),
  - `.ez-app__body` → `nav.ez-app__side` (div; öğeler `span.ez-app__nav-item`, seçili olan `.is-active`) + `.ez-app__main`.
- **Örnek veriler** (hepsi sabit dizi):
  - **Overview:**
    - 4 KPI: Aktif personel 142 (+4 bu ay), Bekleyen izin 7 (2 acil), Mart bordrosu ₺ 4,8 M (hazır), Açık pozisyon 5 (12 aday).
    - 10 çubuklu grafik: `[38,52,45,66,58,80,72,88,76,95]`.
    - "Son işlemler" 4 satır.
  - **Personnel:**
    - Arama kutusu görünümü ("Personel ara…") ve "Yeni personel" butonu görünümü.
    - Tablo 6 satır (AY Ahmet Yılmaz Yazılım Aktif, EA Ece Arslan İnsan Kaynakları Aktif, SK Selin Koç Tasarım İzinde, MD Mert Demir Satış Aktif, BT Burak Tan Finans Aktif, ZA Zeynep Aydın Operasyon Deneme süresi).
  - **Leave:**
    - Ekim ayı 5×7 takvim; dolu günler `{3,4,9,10,11,17,24,25}`, bugün `15`.
    - "Bekleyen onaylar" 3 satır (Ayşe Kaya 3 gün, Can Öz 1 gün, Deniz Er 5 gün).
  - **Timesheet:**
    - Günler Pzt–Cum, saatler `[8,8,9.5,8,7]`, çubuklar.
    - Dönem özeti: Normal 160 sa, Fazla mesai 12,5 sa, Eksik 2 sa.
  - **Payroll:**
    - Özet: Brüt ₺ 1.284.500, SGK işveren ₺ 198.400, Net ₺ 912.300.
    - 4 çalışan satırı (ad, brüt, net) ve "Kontrol edildi" rozeti.
  - **Recruitment:** 3 sütun; Başvuru 12, Mülakat 5, Teklif 2. Her sütunda 2–3 aday kartı (ad + pozisyon).
  - **Risk:**
    - Rozet "Geliştiriliyor".
    - Skor 18/100 düşük ve 7 çubuklu eğilim `[42,55,48,62,58,71,64]`.
    - "Önerilen aksiyonlar" 3 madde.
- **Bildirimler:**
  - Personel "Yeni çalışan eklendi · Zeynep Aydın"
  - İzin "İzin onaylandı · Ayşe Kaya · 3 gün"
  - Puantaj "Mart puantajı kapandı · 142 çalışan"
  - Bordro "Mart bordrosu hazır · Kontrol bekliyor"
  - İşe alım "Yeni aday · Ön Yüz Geliştirici · 2. tur"
  - Risk "Yeni sinyal · Satış ekibi · izleniyor"
- **Figcaption'lar:** "Ekipzen genel bakış ekranından örnek görünüm." ve "<Modül> modülünden örnek görünüm."
- **Sarmalayıcı:** `<figure className="ez-module-visual"><figcaption className="ez-sr-only">…</figcaption><div aria-hidden="true" className="ez-module-visual__stage"><AppFrame active=…>…</AppFrame><FloatingNotice className="ez-notice--br" …/></div></figure>`.
- **HeroScreen bildirim konumları:** `ez-notice--l`, `ez-notice--r`, `ez-notice--br`.

- [ ] **Step 4: `screens.css` yaz** — temel kurallar:

```css
.ez-app { --app-bg: #0f1629; border-radius: var(--r-xl) var(--r-xl) 0 0; background: var(--app-bg); border: 1px solid rgb(255 255 255 / 0.16); box-shadow: 0 -20px 80px rgb(99 102 241 / 0.35), var(--ez-shadow-lg); overflow: hidden; color: var(--ez-text-2); font-size: var(--fs-12); text-align: left; }
.ez-module-visual .ez-app, .ez-auth-screen .ez-app { border-radius: var(--r-xl); }
.ez-app__bar { display: flex; align-items: center; gap: 6px; padding: 10px 14px; border-bottom: 1px solid rgb(255 255 255 / 0.08); }
.ez-app__bar i { width: 9px; height: 9px; border-radius: 50%; background: rgb(255 255 255 / 0.18); }
.ez-app__url { margin-left: 12px; padding: 3px 12px; border-radius: 99px; background: rgb(255 255 255 / 0.05); color: var(--ez-text-3); font-size: 11px; }
.ez-app__body { display: grid; grid-template-columns: 170px 1fr; min-height: 300px; }
.ez-app__side { padding: 12px 10px; display: grid; gap: 2px; align-content: start; border-right: 1px solid rgb(255 255 255 / 0.07); }
.ez-app__nav-item { display: flex; gap: 8px; align-items: center; padding: 7px 9px; border-radius: 8px; color: var(--ez-text-3); }
.ez-app__nav-item.is-active { background: rgb(99 102 241 / 0.22); color: #fff; }
.ez-app__main { padding: 16px; display: grid; gap: 12px; align-content: start; min-width: 0; }
.ez-app__panel { padding: 12px; border-radius: 12px; background: rgb(255 255 255 / 0.04); border: 1px solid rgb(255 255 255 / 0.08); min-width: 0; }
.ez-app__kpis { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; }
.ez-app__kpi strong { display: block; font-size: var(--fs-20); color: #fff; }
.ez-app__up { color: var(--ez-success); font-weight: 700; }
.ez-app__chart { display: flex; align-items: flex-end; gap: 6px; height: 120px; }
.ez-app__chart span { flex: 1; border-radius: 4px 4px 0 0; background: linear-gradient(#7dd3fc, #6366f1); }
.ez-notice { position: absolute; z-index: 2; display: flex; gap: 10px; align-items: center; padding: 11px 14px; border-radius: 14px; background: rgb(20 28 52 / 0.78); border: 1px solid rgb(255 255 255 / 0.16); backdrop-filter: blur(12px); box-shadow: var(--ez-shadow-lg); color: var(--ez-text-2); font-size: var(--fs-12); animation: ez-float 7s ease-in-out infinite; }
.ez-notice strong { display: block; color: #fff; font-size: var(--fs-14); }
.ez-notice__icon { width: 30px; height: 30px; border-radius: 9px; display: grid; place-items: center; color: #fff; }
.ez-notice--success .ez-notice__icon { background: #10b981; }
.ez-notice--primary .ez-notice__icon { background: var(--ez-grad-primary); }
.ez-notice--violet .ez-notice__icon { background: #c026d3; }
.ez-notice--warning .ez-notice__icon { background: #d97706; }
.ez-notice--l { left: -2%; top: 38%; } .ez-notice--r { right: -2%; top: 18%; animation-delay: -2s; } .ez-notice--br { right: 4%; bottom: -18px; animation-delay: -4s; }
@keyframes ez-float { 50% { transform: translateY(-8px); } }
@media (max-width: 640px) { .ez-app__side { display: none; } .ez-app__body { grid-template-columns: 1fr; } .ez-app__kpis { grid-template-columns: repeat(2, minmax(0, 1fr)); } .ez-notice--l, .ez-notice--r { display: none; } .ez-notice--br { right: 8px; } }
@media (prefers-reduced-motion: reduce) { .ez-notice { animation: none; } }
```

Tablo, takvim, pano ve risk alt öğeleri aynı panel diliyle (`.ez-app__table`, `.ez-app__cal`, `.ez-app__board`, `.ez-app__list`) eklenir. Tüm renkler gece token'larından ya da yukarıdaki literal'lerden alınır.

- [ ] **Step 5:** `ez-mock*` ve `ez-hero-visual*` kurallarını `marketing.css`'ten sil. `HomePage.tsx` ve `AuthLayout.tsx` import'larını güncelle.
- [ ] **Step 6:** Testler, lint ve build; ardından commit `feat(landing): büyük ürün ekranları ve süzülen bildirimler`.

### Task 3: Ortak parçalar (kök, üst menü, alt bilgi, sayfa girişi, bölüm ritmi)

**Files:**
- Create: `frontend/src/marketing/useScrolled.ts`
- Modify: `frontend/src/marketing/MarketingLayout.tsx`, `sections.tsx`, `styles/marketing.css` (genel, header, footer, page-hero, section, faq, cta-band, form alanları)
- Test: `frontend/src/marketing/MarketingLayout.test.tsx`

**Interfaces:**
- Produces:
  - `useScrolled(offset = 8): boolean`
  - `PageHero` yeni opsiyonel prop: `glow?: "cyan" | "teal" | "violet" | "indigo"` (varsayılan `indigo`). `section` sınıfı `ez-page-hero ez-glow ez-gridlines ez-page-hero--{glow}`.
  - Kök: `<div className="ez-site ez-night">` ve hemen içinde `<div className="ez-grain" aria-hidden="true" />`.
  - Header: kaydırılınca `is-scrolled` sınıfı. Logo her iki yerde `tone="light"`.

- [ ] **Step 1: Test yaz**

```tsx
test("site gece temasında açılır, menü kaydırınca koyulaşır", async () => {
  // mevcut render yardımcısıyla "/" render edilir
  const root = document.querySelector(".ez-site");
  expect(root).toHaveClass("ez-night");
  expect(document.querySelector(".ez-grain")).toHaveAttribute("aria-hidden", "true");
  const header = document.querySelector(".ez-header")!;
  expect(header).not.toHaveClass("is-scrolled");
  act(() => { Object.defineProperty(window, "scrollY", { value: 120, configurable: true }); window.dispatchEvent(new Event("scroll")); });
  expect(header).toHaveClass("is-scrolled");
});
```

Test dosyasındaki mevcut render deseni kullanılır; testten sonra `scrollY` 0'a geri alınır.

- [ ] **Step 2:** FAIL gör.
- [ ] **Step 3: `useScrolled` yaz**

```ts
import { useEffect, useState } from "react";

/** Sayfa belirli bir mesafeden fazla kaydırıldı mı? (pasif dinleyici) */
export function useScrolled(offset = 8) {
  const [scrolled, setScrolled] = useState(false);
  useEffect(() => {
    const update = () => setScrolled(window.scrollY > offset);
    update();
    window.addEventListener("scroll", update, { passive: true });
    return () => window.removeEventListener("scroll", update);
  }, [offset]);
  return scrolled;
}
```

- [ ] **Step 4:** `MarketingLayout` güncelle: kök sınıflar, gren, `is-scrolled`, üstte `<Logo tone="light" />`. `sections.tsx` → `PageHero` `glow` prop'u.
- [ ] **Step 5: `marketing.css` ortak kurallar**

- **Temel:**
  - `.ez-site` zemini `var(--ez-night-1)`.
  - Başlık renkleri `--ez-text`; `h1` `clamp(2.5rem, 1.5rem + 3.6vw, 3.75rem)`, `font-weight: 800`, `letter-spacing: -0.035em`.
- **Bölüm ritmi:**
  - `.ez-section` zemini `--ez-night-1`.
  - `.ez-section--muted` → `var(--ez-night-2)` + üst kenar `1px solid var(--ez-border-soft)`.
  - `.ez-section--dark` → `var(--ez-night-3)` + ışık (`ez-glow` ile).
- **Diğer ortak parçalar:**
  - `.ez-eyebrow` rengi `--ez-accent-cyan`.
  - Header: `background: rgb(7 11 24 / 0.55)`, kenar yok. `.is-scrolled` → `rgb(7 11 24 / 0.85)` + `border-bottom: 1px solid var(--ez-border)`. Mobil menü paneli `var(--ez-night-2)`.
  - Footer: `var(--ez-night-2)` + `border-top` yerine üstte `linear-gradient(90deg, transparent, #6366f1, #38bdf8, transparent)` 1px çizgi (`::before`).
  - `.ez-page-hero`:
    - `padding-block: clamp(4rem, 9vw, 7rem)`, ortalanmış.
    - Glow değişkenleri: `--cyan` (`--glow-a: rgb(56 189 248 / .45)`), `--teal` (`rgb(20 184 166 / .4)`), `--violet` (`rgb(168 85 247 / .45)`), `--indigo` (`rgb(99 102 241 / .5)`).
  - `.ez-faq__item` → `.ez-glass` görünümü (arka plan ve kenar token'ları).
  - `.ez-cta-band`:
    - `background: linear-gradient(135deg, rgb(56 189 248 / .22), rgb(99 102 241 / .32) 50%, rgb(192 38 211 / .28))`, kenar `rgb(255 255 255 / .18)`, `border-radius: 24px`.
    - İçindeki primary buton beyaz zemin + `#1e1b4b` metin.
  - Form alanları (`.ez-field input/select/textarea`): `background: rgb(255 255 255 / 0.04)`, kenar `var(--ez-border-strong)`, metin `--ez-text`, placeholder `--ez-text-3`.
- **Kapsam dışı:** `.ez-section--dark` altındaki eski slate override'ları silinir (artık gereksiz).

- [ ] **Step 6:** Tarayıcıda `/` üst ve alt kısmı 1280 ve 375'te ekran görüntüsüyle kontrol et; menü açık/kapalı ve kaydırılmış halini gör.
- [ ] **Step 7:** Testler, lint ve build; ardından commit `feat(landing): gece temasında menü, alt bilgi ve bölüm ritmi`.

### Task 4: Ana sayfa

**Files:**
- Modify: `frontend/src/marketing/pages/HomePage.tsx`, `styles/marketing.css` (hero, bento, roadmap-strip, pillar), `styles/marketing-motion.css` (showcase, journey)
- Test: `frontend/src/marketing/pages/HomePage.test.tsx`

**Interfaces:**
- Consumes:
  - `HeroScreen` (Task 2).
  - `.ez-glass`, `.ez-glow`, `.ez-gridlines`, `.ez-gradient-text` (Task 1).
- Produces: `.ez-hero--night` düzeni (ortalanmış kopya + altında taşan ekran), `.ez-sec-layers` (3B katman yığını, Task 6 da kullanır).

- [ ] **Step 1: Test güncelle.** Mevcut hero testini şu beklentilere çevir:
  - `h1` "İK süreçleriniz tek bir kayıt düzeninde" metnini içerir, ikinci yarısı `.ez-gradient-text` span'ında.
  - `.ez-hero .ez-hero-screen` var.
  - Hero `.ez-reveal` içermez.
  - Güvenlik bölümünde `.ez-sec-layers` dört katman içerir: Uygulama, Oturum, Yetki, Veritabanı.

  Ayrıca v2'den kalan `.ez-hero-visual__stack` yokluk testini kaldır (bileşen silindi).

- [ ] **Step 2:** FAIL gör.
- [ ] **Step 3: Markup**

- **Hero:**
  - `<section className="ez-hero ez-glow ez-gridlines">`, ortalanmış `.ez-hero__copy`: eyebrow pill (`.ez-glass`), h1, lead, iki buton, trust list.
  - Altında `<div className="ez-hero-enter ez-hero-enter--late ez-hero__screen"><HeroScreen /></div>`.
  - H1: `İK süreçleriniz <span className="ez-gradient-text">tek bir kayıt düzeninde</span>`. Metin aynı kalır, yalnız sarmalanır.
- **Modül bento:** Kartlar `ez-feature-card ez-glass`; her kartta modül rengi ışık için `--card-glow` inline değişkeni (`personel #6366f1`, `izin #38bdf8`, `puantaj #f59e0b`, `bordro #8b5cf6`, `ise-alim #c026d3`, `risk-merkezi #f43f5e`).
- **Yol haritası sütunları:** `ez-glass`.
- **Güvenlik bölümü:**
  - `ez-section ez-section--dark ez-glow` ve iki sütun.
  - Sol: başlık + pillar listesi (`ez-glass`, ikonlu satırlar).
  - Sağ: `<div className="ez-sec-layers" aria-hidden="true">` içinde 4 adet `<span className="ez-sec-layers__layer ez-sec-layers__layer--{n}">{ad}</span>`.
- **SSS ve CtaBand:** aynı kalır (Task 3 stilleri).

- [ ] **Step 4: CSS**

- **Hero:**
  - `.ez-hero` `text-align: center; padding-block: clamp(3rem,7vw,5.5rem) 0; overflow: hidden`.
  - `.ez-hero__copy` `max-width: 760px; margin-inline: auto; justify-items: center`.
  - `.ez-hero__screen` `margin-top: 3rem; width: min(1080px, 100%); margin-inline: auto; position: relative`.
- **Bento:**
  - `.ez-feature-card` `overflow: hidden; position: relative`.
  - `::after` `radial-gradient(12rem 12rem at 100% 100%, var(--card-glow), transparent 70%)` `opacity .35`, hover'da `.55`.
- **Yol haritası:** `.ez-roadmap-strip__col` üst kenar `box-shadow: inset 0 2px 0 var(--stage-color)`; `--now` `#fbbf24`, `--next` `#7dd3fc`, `--planned` `#c084fc`.
- **Güvenlik katmanları:**
  - `.ez-sec-layers` `position: relative; height: 320px; perspective: 900px`.
  - `.ez-sec-layers__layer` `position:absolute; left:50%; width: 280px; height: 130px; margin-left:-140px; border-radius: 20px; transform: rotateX(58deg) rotateZ(-38deg); border: 1px solid rgb(255 255 255 / .35); display:grid; place-items:center; color:#fff; font-weight:700`.
  - Katmanlar `--1..--4` sırasıyla `top: 0/60/120/180px` ve degradeler: pembe-mor, indigo, mavi, turkuaz.
  - Hafif süzülme animasyonu: her katman farklı gecikmeyle `translateZ` ±6px. Reduced-motion'da kapalı.
- **Vitrin:** `.ez-showcase` sekmeleri `ez-glass` çubuk; seçili sekme `background: var(--ez-primary-soft); color:#fff`.
- **Yolculuk:**
  - Kart `ez-glass`.
  - Etkin adım noktasında `box-shadow: 0 0 0 6px rgb(99 102 241 / .25), 0 0 24px rgb(99 102 241 / .6)`.
  - `.is-filled` alan zemini `rgb(99 102 241 / .18)`.

- [ ] **Step 5: Tarayıcı**
  - `/` 1280 ve 375'te baştan sona ekran görüntüsü.
  - Kalıcı gizli `.ez-reveal` yok, `scrollWidth === innerWidth`, konsol hatası yok.
  - Hareketi azalt açıkken tekrar kontrol et (`resize_window`/emülasyon yoksa CSS kuralını gözden geçir).
- [ ] **Step 6:** Testler, lint ve build; ardından commit `feat(landing): gece temasında ana sayfa`.
- [ ] **Step 7: KULLANICI KONTROL NOKTASI.**
  - Önizlemeyi (5174) kullanıcıya göster, onay iste.
  - Geri bildirim varsa uygula; onay gelmeden Task 5'e geçme.

### Task 5: Özellikler

**Files:** `pages/FeaturesPage.tsx`, `DataFlowDiagram.tsx` (yalnız sınıflar), `StickyModuleNav.tsx` (değişmez), `styles/marketing.css`, test `marketing/features.test.tsx`

- [ ] **Step 1: Test.** `features.test.tsx`'e ekle:
  - Her modül bloğunda `.ez-module-visual .ez-app[data-active=<id>]` var.
  - Blok sırası: çift indeksli bloklar `ez-feature-block--flip` taşımaz, tek indeksliler taşır.
- [ ] **Step 2:** FAIL gör.
- [ ] **Step 3: Markup ve CSS**
  - `PageHero glow="cyan"`.
  - `ModuleBlock`'a `index` prop'u, tek indekste `ez-feature-block--flip`.
  - `.ez-feature-block`:
    - `grid-template-columns: minmax(0,.85fr) minmax(0,1.15fr)`.
    - `--flip` `order` ile ters.
    - Görsel sütunu `position: relative` (bildirim taşması için), `padding-bottom: 24px`.
  - Çipler `ez-glass`; `.ez-module-nav` cam panel, etkin çizgi turkuaz→indigo.
  - `DataFlowDiagram` çizgileri `stroke: url(#…)` ile mevcut degrade; düğümler cam.
  - Roller kartları `ez-glass`.
- [ ] **Step 4: Tarayıcı**
  - 1280 ve 375'te baştan sona kontrol.
  - Yapışkan menünün etkin öğesi her blokta doğru olmalı (aria-current kontrolü).
- [ ] **Step 5:** Testler, lint ve build; ardından commit `feat(landing): gece temasında özellikler sayfası`.

### Task 6: Güvenlik

**Files:** `pages/SecurityPage.tsx`, `SecurityLayers.tsx`, `styles/marketing.css`, test `SecurityLayers.test.tsx`

- [ ] **Step 1: Test.** `SecurityLayers.test.tsx`'e ekle:
  - Bileşen `.ez-sec-layers` yığınını içerir (4+ katman).
  - Seçili katman düğmesi değişince yığında karşılık gelen katman `is-active` olur.
  - Mevcut klavye ve `aria-expanded` testleri aynen geçmeli.
- [ ] **Step 2:** FAIL gör.
- [ ] **Step 3: Uygulama**
  - `SecurityLayers` iki sütun olur. Solda mevcut buton listesi (ARIA aynen) cam satırlar; sağda `aria-hidden` yığın. Yığında `securityLayers` sayısı kadar katman, seçili olana `is-active` (`translateZ(24px)` + parlak kenar).
  - Kategoriler 2×2 `ez-glass`; şeffaflık iki sütun cam.
  - `PageHero glow="teal"`.
- [ ] **Step 4: Tarayıcı** — 1280 ve 375; katman seçimi tıklanarak kontrol.
- [ ] **Step 5:** Testler, lint ve build; ardından commit `feat(landing): gece temasında güvenlik sayfası`.

### Task 7: Hakkımızda

**Files:** `pages/AboutPage.tsx`, `ProcessTimeline.tsx`, `ModuleMaturity.tsx`, `RoadmapBoard.tsx` (yalnız sınıflar), `styles/marketing.css`, test `marketing/about.test.tsx`

- [ ] **Step 1: Test.** `about.test.tsx`'e ekle:
  - Manifesto `.ez-manifesto.ez-glow` taşır.
  - Sahne kartları 01/02/03 numaralarını gösterir.
  - Taahhütler bölümü `ez-section--dark` içindedir.
  - `#yol-haritasi` hâlâ vardır.
- [ ] **Step 2:** FAIL gör.
- [ ] **Step 3: Uygulama**
  - **Manifesto:** `ez-glow` (`--glow-a` mor) + `ez-gridlines`; h1 ikinci cümle `ez-gradient-text`.
  - **Sahneler:** `ez-glass`; köşede büyük numara (`.ez-scene__num`, degrade metin).
  - **İlkeler:** 2×2 cam.
  - **Süreç çizgisi:** `.ez-process::after` degrade; etkinleşince ışığın akışı `background-position` animasyonuyla.
  - **Olgunluk sütunları ve yol haritası kartları:** cam; sütun üst çizgisi durum rengi.
  - **Taahhütler:** `ez-section--dark ez-glow`, onay ikonları `--ez-success` ışıklı daire.
- [ ] **Step 4: Tarayıcı**
  - 1280 ve 375'te baştan sona kontrol.
  - `/hakkimizda#yol-haritasi` bağlantısıyla atlama test edilir.
- [ ] **Step 5:** Testler, lint ve build; ardından commit `feat(landing): gece temasında hakkımızda sayfası`.

### Task 8: Fiyatlandırma, İletişim, KVKK, 404

**Files:** `pages/PricingPage.tsx`, `ContactPage.tsx`, `KvkkPage.tsx`, `NotFoundPage.tsx`, `DemoRequestForm.tsx` (yalnız sınıflar), `styles/marketing.css`, test `pages.test.tsx`, `ContactPage.test.tsx`, `NotFoundPage.test.tsx`

- [ ] **Step 1: Test**
  - **Fiyat:** Önerilen kart `ez-plan--featured` ve içinde `.ez-plan__ring` var. Mevcut 3 h2, "Önerilen" ve "teklif alın" testleri sürer.
  - **İletişim:** Formun yanında `.ez-contact__screen figure` (küçük ürün ekranı) var.
  - **404:** `.ez-notfound__code` "404" gösterir.
- [ ] **Step 2:** FAIL gör.
- [ ] **Step 3: Uygulama**
  - **Fiyatlandırma:**
    - `PageHero glow="indigo"`; plan kartları `ez-glass`.
    - Önerilen kart için `@property --ez-ring-angle` + `conic-gradient` dönen kenar (`.ez-plan__ring` pseudo katmanı; reduced-motion'da sabit).
    - Karşılaştırma tablosu koyu, satır hover `rgb(255 255 255 / .04)`.
  - **İletişim:**
    - Sol `ez-glass` panelde form.
    - Sağda "Süreç nasıl işler" 3 adım (mevcut içerik) ve `ModuleVisual id="izin"` küçük boyutta (`.ez-contact__screen`).
  - **KVKK:** `.ez-prose` koyu renkler (`--ez-text-2`, başlıklar `--ez-text`, bağlantılar `--ez-accent-cyan`).
  - **404:** `ez-glow ez-gridlines`; `.ez-notfound__code` `font-size: clamp(6rem, 20vw, 12rem)`, `ez-gradient-text`.
- [ ] **Step 4: Tarayıcı**
  - Dört sayfa 1280 ve 375'te kontrol.
  - Form doğrulama hata mesajlarının koyu zeminde okunurluğu kontrol edilir.
- [ ] **Step 5:** Testler, lint ve build; ardından commit `feat(landing): gece temasında fiyat, iletişim, KVKK ve 404`.

### Task 9: Giriş ve davet ekranları

**Files:** `auth/AuthLayout.tsx`, `styles/auth.css`, test `auth/*.test.tsx` (mevcut LoginPage / AcceptInvitePage testleri)

- [ ] **Step 1: Test.** AuthLayout için:
  - Kök `ez-auth ez-night` sınıflarını taşır.
  - Sağ panelde `.ez-auth-screen` vardır.
  - Mevcut giriş ve davet davranış testleri aynen geçmeli.
- [ ] **Step 2:** FAIL gör.
- [ ] **Step 3: Uygulama**
  - Kök `ez-auth ez-night`; `<Logo tone="light" />`; sol panel form `ez-glass`.
  - Sağ aside `ez-glow ez-gridlines`, içinde güvence maddeleri + `<AuthScreen />`.
  - `auth.css` koyu dile göre yeniden yazılır:
    - İki sütun, ≤960px tek sütun ve aside gizli.
    - Form paneli `max-width: 420px`.
    - Hata kutusu `--ez-danger-soft` / `--ez-danger`.
- [ ] **Step 4: Tarayıcı**
  - `/giris` ve `/davet?email=a@b.c&token=x` 1280 ve 375'te kontrol.
  - Demo hesabıyla giriş denenip `/app/dashboard`'a gidildiği ve uygulamanın açık temada kaldığı doğrulanır.
- [ ] **Step 5:** Testler, lint ve build; ardından commit `feat(auth): gece temasında giriş ve davet ekranları`.

### Task 10: Temizlik ve son doğrulama

**Files:** `styles/marketing.css`, `marketing-motion.css`, `tokens.css`, `tokens.test.ts`

- [ ] **Step 1: Kullanılmayan CSS'i sil.** Her seçici için `frontend/src` altında grep yapılır; eşleşmesi yoksa silinir. Adaylar:
  - `ez-hero__grid`, `ez-aurora`
  - `ez-mock*`, `ez-hero-visual*`, `ez-module-visual__stack`
  - `.ez-section--dark` eski override'ları
- [ ] **Step 2: `.ez-light` kontrolü.** Kod tabanında `.ez-light` kullanımı kalmadıysa `tokens.css`'teki `.ez-light` seçicisi ve `tokens.test.ts`'teki beklentisi kaldırılır. Uygulamanın `:root` açık teması aynen kalır.
- [ ] **Step 3: Son doğrulama**
  - Tüm sayfalar (`/`, `/ozellikler`, `/guvenlik`, `/hakkimizda`, `/fiyatlandirma`, `/iletisim`, `/kvkk-aydinlatma`, `/olmayan`, `/giris`) 1280 ve 375'te son kez ekran görüntüsüyle gezilir.
  - Konsol hatası yok, taşma yok, kalıcı gizli içerik yok.
- [ ] **Step 4:** Lint, test ve build; ardından commit `chore(landing): gece teması sonrası kullanılmayan stilleri kaldır`.
- [ ] **Step 5: Kullanıcıya rapor.** AGENTS.md'ye uygun rapor verilir:
  - Refs, commitler, dosyalar ve doğrulama sonuçları.
  - Tek seferlik birleştirme onayı istenir: dal push, `main` merge, `main` push.
