# Yeni Kimlik + Landing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ekipzen'e indigo/Plus Jakarta Sans tabanlı yeni görsel kimlik, temiz URL'li çok sayfalı landing sitesi, yeniden tasarlanmış giriş/davet ekranları ve e-posta ile bildirilen demo talebi uç noktası eklemek.

**Architecture:** Token katmanı (`tokens.css`, `--ez-*` amaç değişkenleri) eski `main.css` değişkenlerine alias olarak bağlanır; böylece uygulama yapısı değişmeden yeni kimliğe geçer. Router hash'ten browser router'a geçer: landing kökte, uygulama `/app/*` altında. Backend SPA fallback ve `POST /api/public/demo-requests` (IEmailSender) ekler.

**Tech Stack:** React 18, Vite 8, TypeScript, react-router-dom 7, @tanstack/react-query 5, Vitest + Testing Library; .NET 9, MediatR, FluentValidation, xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-09-17-yeni-kimlik-landing-design.md`

## Global Constraints

- Yeni npm/NuGet bağımlılığı eklenmez.
- Renkler yalnız token'lardan gelir. İstisna: `frontend/public/favicon.svg` statik dosyası.
- Purpose token'ları `--ez-` önekiyle tanımlanır. Spec'teki `--text`, `--surface` vb. adlar eski `main.css` değişkenleriyle çakıştığı için bu önek zorunludur; ramp'ler öneksizdir (`--indigo-700`).
- Vurgu `#4338CA` (`--indigo-700`), ana metin `#0F172A` (`--slate-900`), font `"Plus Jakarta Sans"`, mono `"JetBrains Mono"`.
- Tüm UI metinleri Türkçe.
- Sahte müşteri logosu, yorum ya da metrik kullanılmaz. Güvenlik iddiaları yalnız kodda var olan özelliklerdir.
- Fiyat rakamı gösterilmez ("Teklif alın").
- Self-signup eklenmez; demo talebi yalnız e-posta bildirimi üretir, DB kaydı yok.
- Landing ve auth ekranları her zaman açık temadır (`.ez-light`); uygulama teması değişmeden çalışır.
- Dokunma hedefleri ≥ 44px; odak halkası görünür; kontrast WCAG AA.
- Worktree'den Project Memory yazılmaz.
- Commit mesajları `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>` satırıyla biter.
- Spec'ten bilinçli sapmalar:
  - Demo ucu 202 yerine **204** döner. `apiFetch` yalnız 204'te gövde okumaz.
  - Rate limit 429 davranışı entegrasyon testi yerine öznitelik ve politika testiyle doğrulanır. Test fabrikası limitleri ortam değişkeniyle etkisiz kılar.

## Dosya haritası

| Dosya | Sorumluluk |
|---|---|
| `frontend/src/styles/tokens.css` (yeni) | Ramp'ler, `--ez-*` amaç token'ları, açık/koyu tema |
| `frontend/src/styles/main.css` | `:root` bloğu → alias katmanı; kök font 16px; body overflow düzeltmesi |
| `frontend/src/styles/layout.css`, `settings.css` | Sabit teal hex'leri → `--ez-nav-*` |
| `frontend/src/styles/ui.css` (yeni) | `ez-` bileşen stilleri |
| `frontend/src/ui/*` (yeni) | Button, ButtonLink, Field, Input, Textarea, Select, Checkbox, Card, Badge, Logo, usePageMeta, index |
| `frontend/src/legacyHash.ts` (yeni) | Eski `#/` ve kök uygulama yollarını yeni yollara çevirme |
| `frontend/src/routes.tsx`, `App.tsx`, `main.tsx` | Browser router, `/app` öneki, marketing ağacı |
| `frontend/src/marketing/*` (yeni) | Layout, içerik, görseller, sayfalar, demo formu |
| `frontend/src/styles/marketing.css` (yeni) | Landing stilleri |
| `frontend/src/auth/AuthLayout.tsx` (yeni), `LoginPage.tsx`, `AcceptInvitePage.tsx`, `styles/auth.css` | Yeni auth ekranları |
| `frontend/public/favicon.svg`, `robots.txt`, `sitemap.xml` (yeni) | Statik varlıklar |
| `backend/src/Ekipzen.API/Hosting/PilotHosting.cs` | `MapSpaFallback` |
| `backend/src/Ekipzen.API/Hosting/ProductionConfiguration.cs` | `DemoRequests:NotifyEmail` kontrolü |
| `backend/src/Ekipzen.API/Program.cs` | `public-form` rate limit, ayar kaydı, fallback |
| `backend/src/Ekipzen.API/Controllers/PublicController.cs` (yeni) | Demo talebi ucu |
| `backend/src/Ekipzen.Application/Features/DemoRequests/SubmitDemoRequestCommand.cs` (yeni) | Komut, doğrulayıcı, handler, ayar kaydı |
| `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs:468` | Davet linki `/davet?...` |

---

### Task 1: Token sistemi ve yeni kimliğin uygulamaya yayılması

**Files:**
- Create: `frontend/src/styles/tokens.css`
- Create: `frontend/src/styles/tokens.test.ts`
- Modify: `frontend/src/styles/main.css` (satır 1–82 `:root` ve dark blokları; `html` font-size; `body` overflow; 273, 658–665 hex'leri)
- Modify: `frontend/src/styles/layout.css` (sidebar hex/rgb değerleri)
- Modify: `frontend/src/styles/settings.css` (122–177 arası hex'ler)
- Modify: `frontend/src/main.tsx` (tokens.css importu en başa)
- Modify: `frontend/index.html` (font linki)

**Interfaces:**
- Produces: CSS değişkenleri
  - Ramp'ler: `--indigo-{50..950}`, `--slate-{0,25,50..950}`, `--emerald-*`, `--amber-*`, `--rose-*`, `--sky-*`.
  - Metin: `--ez-bg`, `--ez-surface`, `--ez-surface-2`, `--ez-text`, `--ez-text-2`, `--ez-text-3`, `--ez-text-4`.
  - Kenarlık: `--ez-border`, `--ez-border-strong`, `--ez-border-soft`.
  - Vurgu: `--ez-primary`, `--ez-primary-hover`, `--ez-primary-soft`, `--ez-on-primary`, `--ez-accent`, `--ez-accent-soft`.
  - Durum: `--ez-success|warning|danger|info` ile `-strong` ve `-soft` sürümleri.
  - Odak ve gölge: `--ez-focus-ring`, `--ez-shadow-{xs,sm,md,lg}`.
  - Radius: `--r-{sm,md,lg,xl,full}`.
  - Font: `--ez-font-sans`, `--ez-font-mono`, `--fs-{12..60}`.
  - Hareket: `--dur-fast`, `--dur-base`, `--ease-out`.
  - Sidebar: `--ez-nav-{bg,text,muted,accent,active-bg,border}`.
  - `.ez-light` sınıfı açık temayı zorlar.

- [ ] **Step 1: Failing test yaz** — `frontend/src/styles/tokens.test.ts`

```ts
import { readFileSync, readdirSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";

const dir = __dirname;
const read = (name: string) => readFileSync(join(dir, name), "utf8");

test("tokens.css zorunlu amaç token'larını ve kimlik renklerini tanımlar", () => {
  const css = read("tokens.css");
  for (const name of ["--ez-bg", "--ez-surface", "--ez-text", "--ez-primary", "--ez-on-primary", "--ez-border", "--ez-focus-ring", "--ez-nav-bg", "--ez-font-sans"]) {
    expect(css).toContain(`${name}:`);
  }
  expect(css.toLowerCase()).toContain("--indigo-700: #4338ca");
  expect(css.toLowerCase()).toContain("--slate-900: #0f172a");
  expect(css).toContain('"Plus Jakarta Sans"');
  expect(css).toContain(':root[data-theme="dark"]');
  expect(css).toContain(".ez-light");
});

test("eski petrol/evergreen renkleri stil dosyalarında kalmaz", () => {
  const legacy = /#0f766e|#0b5a54|#2fbfae|#0c2b29|#b98a2f|rgb\(47 191 174|rgb\(15 118 110|IBM Plex/i;
  for (const file of readdirSync(dir).filter((f) => f.endsWith(".css"))) {
    expect(read(file), file).not.toMatch(legacy);
  }
});
```

- [ ] **Step 2: Testin başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/styles/tokens.test.ts`
Expected: FAIL (`ENOENT ... tokens.css`)

- [ ] **Step 3: `frontend/src/styles/tokens.css` oluştur**

```css
/* ============================================================
   Ekipzen — tasarım token'ları (kimlik: derin indigo + slate)
   Katmanlar: ramp (öneksiz) → amaç (--ez-*) → eski alias'lar (main.css)
   ============================================================ */
:root {
  --indigo-50: #eef2ff; --indigo-100: #e0e7ff; --indigo-200: #c7d2fe; --indigo-300: #a5b4fc;
  --indigo-400: #818cf8; --indigo-500: #6366f1; --indigo-600: #4f46e5; --indigo-700: #4338ca;
  --indigo-800: #3730a3; --indigo-900: #312e81; --indigo-950: #1e1b4b;
  --slate-0: #ffffff; --slate-25: #fbfcfe; --slate-50: #f8fafc; --slate-100: #f1f5f9;
  --slate-200: #e2e8f0; --slate-300: #cbd5e1; --slate-400: #94a3b8; --slate-500: #64748b;
  --slate-600: #475569; --slate-700: #334155; --slate-800: #1e293b; --slate-900: #0f172a; --slate-950: #020617;
  --emerald-50: #ecfdf5; --emerald-400: #34d399; --emerald-600: #059669; --emerald-700: #047857; --emerald-950: #022c22;
  --amber-50: #fffbeb; --amber-400: #fbbf24; --amber-600: #d97706; --amber-700: #b45309; --amber-950: #451a03;
  --rose-50: #fff1f2; --rose-400: #fb7185; --rose-600: #e11d48; --rose-700: #be123c; --rose-950: #4c0519;
  --sky-50: #f0f9ff; --sky-400: #38bdf8; --sky-600: #0284c7; --sky-700: #0369a1; --sky-950: #082f49;

  --ez-font-sans: "Plus Jakarta Sans", "Segoe UI", system-ui, -apple-system, Arial, sans-serif;
  --ez-font-mono: "JetBrains Mono", "Cascadia Mono", Consolas, monospace;
  --fs-12: 0.75rem; --fs-14: 0.875rem; --fs-16: 1rem; --fs-18: 1.125rem; --fs-20: 1.25rem;
  --fs-24: 1.5rem; --fs-30: 1.875rem; --fs-36: 2.25rem; --fs-48: 3rem; --fs-60: 3.75rem;

  --r-sm: 6px; --r-md: 10px; --r-lg: 14px; --r-xl: 20px; --r-full: 999px;
  --dur-fast: 120ms; --dur-base: 200ms; --ease-out: cubic-bezier(0.2, 0.8, 0.2, 1);

  --ez-nav-bg: var(--slate-900);
  --ez-nav-text: var(--slate-300);
  --ez-nav-muted: var(--slate-500);
  --ez-nav-accent: var(--indigo-400);
  --ez-nav-active-bg: rgb(129 140 248 / 0.16);
  --ez-nav-border: rgb(255 255 255 / 0.06);
}

/* Açık tema amaç token'ları. .ez-light, koyu tema açıkken de alt ağaca açık temayı zorlar. */
:root,
.ez-light {
  --ez-bg: var(--slate-50);
  --ez-surface: var(--slate-0);
  --ez-surface-2: var(--slate-100);
  --ez-text: var(--slate-900);
  --ez-text-2: var(--slate-700);
  --ez-text-3: var(--slate-500);
  --ez-text-4: var(--slate-400);
  --ez-border: var(--slate-200);
  --ez-border-strong: var(--slate-300);
  --ez-border-soft: var(--slate-100);
  --ez-primary: var(--indigo-700);
  --ez-primary-hover: var(--indigo-800);
  --ez-primary-soft: var(--indigo-50);
  --ez-on-primary: var(--slate-0);
  --ez-accent: var(--amber-600);
  --ez-accent-soft: var(--amber-50);
  --ez-success: var(--emerald-600); --ez-success-strong: var(--emerald-700); --ez-success-soft: var(--emerald-50);
  --ez-warning: var(--amber-600); --ez-warning-strong: var(--amber-700); --ez-warning-soft: var(--amber-50);
  --ez-danger: var(--rose-600); --ez-danger-strong: var(--rose-700); --ez-danger-soft: var(--rose-50);
  --ez-info: var(--sky-600); --ez-info-strong: var(--sky-700); --ez-info-soft: var(--sky-50);
  --ez-focus-ring: 0 0 0 3px rgb(67 56 202 / 0.28);
  --ez-shadow-xs: 0 1px 2px rgb(15 23 42 / 0.05);
  --ez-shadow-sm: 0 1px 3px rgb(15 23 42 / 0.08), 0 1px 2px rgb(15 23 42 / 0.04);
  --ez-shadow-md: 0 8px 24px rgb(15 23 42 / 0.08), 0 2px 6px rgb(15 23 42 / 0.04);
  --ez-shadow-lg: 0 24px 56px rgb(15 23 42 / 0.14), 0 4px 12px rgb(15 23 42 / 0.06);
  color-scheme: light;
}

:root[data-theme="dark"] {
  --ez-bg: #0b1120;
  --ez-surface: #111a2e;
  --ez-surface-2: #16213a;
  --ez-text: var(--slate-50);
  --ez-text-2: var(--slate-300);
  --ez-text-3: var(--slate-400);
  --ez-text-4: var(--slate-500);
  --ez-border: #1f2a44;
  --ez-border-strong: #2c3a5a;
  --ez-border-soft: #172036;
  --ez-primary: var(--indigo-400);
  --ez-primary-hover: var(--indigo-300);
  --ez-primary-soft: var(--indigo-950);
  --ez-on-primary: #0b1120;
  --ez-accent: var(--amber-400);
  --ez-accent-soft: var(--amber-950);
  --ez-success: var(--emerald-400); --ez-success-strong: var(--emerald-400); --ez-success-soft: var(--emerald-950);
  --ez-warning: var(--amber-400); --ez-warning-strong: var(--amber-400); --ez-warning-soft: var(--amber-950);
  --ez-danger: var(--rose-400); --ez-danger-strong: var(--rose-400); --ez-danger-soft: var(--rose-950);
  --ez-info: var(--sky-400); --ez-info-strong: var(--sky-400); --ez-info-soft: var(--sky-950);
  --ez-focus-ring: 0 0 0 3px rgb(129 140 248 / 0.35);
  --ez-shadow-xs: 0 1px 2px rgb(0 0 0 / 0.3);
  --ez-shadow-sm: 0 1px 3px rgb(0 0 0 / 0.35);
  --ez-shadow-md: 0 8px 24px rgb(0 0 0 / 0.4);
  --ez-shadow-lg: 0 24px 56px rgb(0 0 0 / 0.5);
  --ez-nav-bg: #070b16;
  color-scheme: dark;
}

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
```

- [ ] **Step 4: `main.css` alias katmanı**

`frontend/src/styles/main.css` içindeki başlık yorumu, `:root { ... }` bloğu ve `:root[data-theme="dark"] { ... }` bloğu (satır 1–82) şu blokla değiştirilir:

```css
/* ============================================================
   Ekipzen — uygulama stilleri
   Renk/tipografi token'ları tokens.css'te. Aşağıdaki adlar geriye
   uyum alias'larıdır; yeni kod doğrudan --ez-* kullanır.
   ============================================================ */

:root {
  --primary: var(--ez-primary);
  --primary-strong: var(--ez-primary-hover);
  --primary-soft: var(--ez-primary-soft);
  --primary-contrast: var(--ez-on-primary);
  --accent: var(--ez-accent);
  --accent-soft: var(--ez-accent-soft);
  --ink: var(--ez-text);
  --text: var(--ez-text-2);
  --muted: var(--ez-text-3);
  --subtle: var(--ez-text-4);
  --line: var(--ez-border);
  --line-strong: var(--ez-border-strong);
  --line-soft: var(--ez-border-soft);
  --surface: var(--ez-surface);
  --surface-soft: var(--ez-surface-2);
  --body: var(--ez-bg);
  --success: var(--ez-success); --success-strong: var(--ez-success-strong); --success-soft: var(--ez-success-soft);
  --warning: var(--ez-warning); --warning-strong: var(--ez-warning-strong); --warning-soft: var(--ez-warning-soft);
  --danger: var(--ez-danger); --danger-strong: var(--ez-danger-strong); --danger-soft: var(--ez-danger-soft);
  --info: var(--ez-info); --info-strong: var(--ez-info-strong); --info-soft: var(--ez-info-soft);
  --focus-ring: var(--ez-focus-ring);
  --shadow-sm: var(--ez-shadow-sm);
  --shadow-md: var(--ez-shadow-md);
  --shadow-lg: var(--ez-shadow-lg);
  --sidebar-width: 264px;
  --header-height: 72px;
  --radius-sm: var(--r-sm);
  --radius-md: var(--r-md);
  --radius-lg: var(--r-lg);
  --font-ui: var(--ez-font-sans);
  --font-data: var(--ez-font-mono);
}
```

Ardından aynı dosyada:
- `html { font-size: 15px; }` → `html { font-size: 16px; }`
- `body` kuralından `overflow: hidden;` satırı silinir ve body kuralının hemen altına eklenir:

```css
/* Uygulama kabuğu kendi içinde kayar; landing sayfası pencereyle kayar. */
body:has(.app-container) {
  overflow: hidden;
}
```

- Satır ~273 (eski numara ~355) `color: #fff;` → `color: var(--ez-on-primary);`
- `border: 1px solid #f2c46d;` → `border: 1px solid var(--amber-400);`; `background: #fff9eb;` → `background: var(--ez-warning-soft);`
- `border-color: #ef9a9a;` → `border-color: var(--rose-400);`; `background: #fff1f1;` → `background: var(--ez-danger-soft);`

- [ ] **Step 5: `layout.css` ve `settings.css` sabit renkleri**

Her iki dosyada şu eşlemeyle bul-değiştir yapılır (yalnız bu değerler):

| Eski | Yeni |
|---|---|
| `#0c2b29`, `#04201d`, `#10231f` | `var(--ez-nav-bg)` |
| `#cfe0dd`, `#a9c2be` | `var(--ez-nav-text)` |
| `#7fa39e`, `#6d928d` | `var(--ez-nav-muted)` |
| `#2fbfae`, `#7ad9cb` | `var(--ez-nav-accent)` |
| `rgb(47 191 174 / 0.14)` | `var(--ez-nav-active-bg)` |
| `rgb(47 191 174 / 0.4)` | `rgb(129 140 248 / 0.4)` |
| `border-right: 1px solid rgb(255 255 255 / 0.06)` | `border-right: 1px solid var(--ez-nav-border)` |

`#fff` değerleri kalır (koyu sidebar üzerinde beyaz metin).

Kontrol:

Run: `grep -nE "#0c2b29|#2fbfae|#7fa39e|#6d928d|#a9c2be|#cfe0dd|#7ad9cb|#04201d|#10231f|47 191 174" frontend/src/styles/*.css`
Expected: çıktı yok

- [ ] **Step 6: Font ve import**

`frontend/index.html` içindeki IBM Plex `<link href=...>` satırı şununla değiştirilir:

```html
    <link
      href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600&display=swap"
      rel="stylesheet"
    />
```

`frontend/src/main.tsx` içinde `import "./styles/main.css";` satırının ÜSTÜNE `import "./styles/tokens.css";` eklenir.

- [ ] **Step 7: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tokens testleri dahil tümü PASS. `auth.css` henüz eski hex içeriyorsa ikinci test FAIL eder; bu durumda `auth.css` içindeki eski teal/altın hex ve rgb değerleri Step 5 tablosundaki mantıkla geçici olarak `var(--ez-primary)` ve `var(--ez-nav-bg)` yapılır (Task 9 dosyayı tamamen yeniden yazar).

- [ ] **Step 8: Build ve commit**

Run: `npm --prefix frontend run build`
Expected: başarılı

```bash
git add frontend/index.html frontend/src/main.tsx frontend/src/styles
git commit -m "feat(ui): introduce indigo design tokens and alias legacy variables

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Paylaşılan UI bileşenleri

**Files:**
- Create: `frontend/src/ui/Button.tsx`, `Field.tsx`, `controls.tsx`, `Card.tsx`, `Logo.tsx`, `usePageMeta.ts`, `index.ts`
- Create: `frontend/src/styles/ui.css`
- Create: `frontend/src/ui/ui.test.tsx`
- Modify: `frontend/src/main.tsx` (`import "./styles/ui.css";` main.css importundan sonra)

**Interfaces:**
- Produces (`frontend/src/ui/index.ts` üzerinden):
  - `Button(props: ButtonProps & ButtonHTMLAttributes<HTMLButtonElement>)`; `ButtonLink(props: ButtonProps & { to: string } & Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "href">)`
    - `ButtonProps = { variant?: "primary" | "secondary" | "ghost" | "danger"; size?: "sm" | "md" | "lg"; block?: boolean }`
  - `Field({ label, hint?, error?, required?, id?, children: ReactElement })`: kontrolün id'sini, `aria-describedby` ve `aria-invalid` değerlerini bağlar.
  - `Input`, `Textarea`, `Select`: forwardRef, `ez-input` sınıfı.
  - `Checkbox({ children, ...InputHTMLAttributes })`
  - `Card({ as?, className?, children })`, `Badge({ tone?: "neutral" | "primary" | "success" | "warning" | "danger", children })`
  - `Logo({ variant?: "full" | "mark"; tone?: "dark" | "light"; className? })`
  - `usePageMeta(title: string, description?: string): void`: başlığı `"<title> · Ekipzen"` yapar; `title === "Ekipzen"` ise olduğu gibi bırakır.

- [ ] **Step 1: Failing test yaz** — `frontend/src/ui/ui.test.tsx`

```tsx
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { expect, test } from "vitest";
import { Badge, Button, ButtonLink, Checkbox, Field, Input, Logo, usePageMeta } from ".";

test("Button varsayılan olarak type=button ve varyant sınıfları taşır", () => {
  render(<Button variant="secondary" size="lg">Kaydet</Button>);
  const btn = screen.getByRole("button", { name: "Kaydet" });
  expect(btn).toHaveAttribute("type", "button");
  expect(btn).toHaveClass("ez-btn", "ez-btn--secondary", "ez-btn--lg");
});

test("ButtonLink router linki üretir", () => {
  render(<MemoryRouter><ButtonLink to="/iletisim">Demo iste</ButtonLink></MemoryRouter>);
  expect(screen.getByRole("link", { name: "Demo iste" })).toHaveAttribute("href", "/iletisim");
});

test("Field label, hint ve hatayı kontrole bağlar", () => {
  render(
    <Field label="E-posta" hint="İş adresiniz" error="Geçersiz adres">
      <Input type="email" />
    </Field>,
  );
  const input = screen.getByLabelText("E-posta");
  expect(input).toHaveAttribute("aria-invalid", "true");
  const describedBy = input.getAttribute("aria-describedby")!.split(" ");
  expect(describedBy).toHaveLength(2);
  expect(screen.getByRole("alert")).toHaveTextContent("Geçersiz adres");
});

test("Field verilen id'yi korur ve required'ı ezmez", () => {
  render(<Field label="Şifre" id="login-password"><Input type="password" required /></Field>);
  const input = screen.getByLabelText("Şifre");
  expect(input).toHaveAttribute("id", "login-password");
  expect(input).toBeRequired();
});

test("Checkbox etiketle erişilebilir", () => {
  render(<Checkbox>Onaylıyorum</Checkbox>);
  expect(screen.getByRole("checkbox", { name: "Onaylıyorum" })).toBeInTheDocument();
});

test("Badge tonu ve Logo erişilebilir adı", () => {
  render(<><Badge tone="success">Aktif</Badge><Logo /></>);
  expect(screen.getByText("Aktif")).toHaveClass("ez-badge--success");
  expect(screen.getByRole("img", { name: "Ekipzen" })).toBeInTheDocument();
});

function MetaProbe() {
  usePageMeta("Fiyatlandırma", "Paketler");
  return null;
}

test("usePageMeta başlık ve açıklamayı yazar", () => {
  render(<MetaProbe />);
  expect(document.title).toBe("Fiyatlandırma · Ekipzen");
  expect(document.querySelector('meta[name="description"]')).toHaveAttribute("content", "Paketler");
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/ui/ui.test.tsx`
Expected: FAIL (`Failed to resolve import "."`)

- [ ] **Step 3: Bileşenleri yaz**

`frontend/src/ui/Button.tsx`:

```tsx
import type { AnchorHTMLAttributes, ButtonHTMLAttributes } from "react";
import { Link } from "react-router-dom";

export type ButtonProps = {
  variant?: "primary" | "secondary" | "ghost" | "danger";
  size?: "sm" | "md" | "lg";
  block?: boolean;
};

const classes = ({ variant = "primary", size = "md", block }: ButtonProps, extra?: string) =>
  ["ez-btn", `ez-btn--${variant}`, `ez-btn--${size}`, block && "ez-btn--block", extra].filter(Boolean).join(" ");

export function Button({ variant, size, block, className, type = "button", ...rest }: ButtonProps & ButtonHTMLAttributes<HTMLButtonElement>) {
  return <button type={type} className={classes({ variant, size, block }, className)} {...rest} />;
}

export function ButtonLink({ variant, size, block, className, to, ...rest }: ButtonProps & { to: string } & Omit<AnchorHTMLAttributes<HTMLAnchorElement>, "href">) {
  return <Link to={to} className={classes({ variant, size, block }, className)} {...rest} />;
}
```

`frontend/src/ui/Field.tsx`:

```tsx
import { cloneElement, useId, type ReactElement } from "react";

type ControlProps = { id?: string; required?: boolean; "aria-describedby"?: string; "aria-invalid"?: boolean };

export function Field({ label, hint, error, required, id, children }: {
  label: string;
  hint?: string;
  error?: string;
  required?: boolean;
  id?: string;
  children: ReactElement<ControlProps>;
}) {
  const autoId = useId();
  const controlId = id ?? autoId;
  const hintId = hint ? `${controlId}-hint` : undefined;
  const errorId = error ? `${controlId}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(" ") || undefined;
  const control = cloneElement(children, {
    id: controlId,
    "aria-describedby": describedBy,
    "aria-invalid": error ? true : undefined,
    ...(required !== undefined && { required }),
  });
  return (
    <div className={`ez-field${error ? " ez-field--error" : ""}`}>
      <label className="ez-field__label" htmlFor={controlId}>
        {label}
        {required && <span aria-hidden="true" className="ez-field__req"> *</span>}
      </label>
      {control}
      {hint && <p id={hintId} className="ez-field__hint">{hint}</p>}
      {error && <p id={errorId} className="ez-field__error" role="alert">{error}</p>}
    </div>
  );
}
```

`frontend/src/ui/controls.tsx`:

```tsx
import { forwardRef, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";

const join = (...names: (string | undefined)[]) => names.filter(Boolean).join(" ");

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  ({ className, ...rest }, ref) => <input ref={ref} className={join("ez-input", className)} {...rest} />,
);
Input.displayName = "Input";

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(
  ({ className, ...rest }, ref) => <textarea ref={ref} className={join("ez-input", "ez-textarea", className)} {...rest} />,
);
Textarea.displayName = "Textarea";

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  ({ className, ...rest }, ref) => <select ref={ref} className={join("ez-input", "ez-select", className)} {...rest} />,
);
Select.displayName = "Select";

export function Checkbox({ children, className, ...rest }: InputHTMLAttributes<HTMLInputElement> & { children: ReactNode }) {
  return (
    <label className={join("ez-check", className)}>
      <input type="checkbox" className="ez-check__box" {...rest} />
      <span className="ez-check__label">{children}</span>
    </label>
  );
}
```

`frontend/src/ui/Card.tsx`:

```tsx
import type { ElementType, ReactNode } from "react";

export function Card({ as: Tag = "div", className, children }: { as?: ElementType; className?: string; children: ReactNode }) {
  return <Tag className={["ez-card", className].filter(Boolean).join(" ")}>{children}</Tag>;
}

export function Badge({ tone = "neutral", children }: { tone?: "neutral" | "primary" | "success" | "warning" | "danger"; children: ReactNode }) {
  return <span className={`ez-badge ez-badge--${tone}`}>{children}</span>;
}
```

`frontend/src/ui/Logo.tsx`:

```tsx
export function Logo({ variant = "full", tone = "dark", className }: { variant?: "full" | "mark"; tone?: "dark" | "light"; className?: string }) {
  return (
    <span role="img" aria-label="Ekipzen" className={["ez-logo", `ez-logo--${tone}`, className].filter(Boolean).join(" ")}>
      <svg className="ez-logo__mark" viewBox="0 0 32 32" width="32" height="32" aria-hidden="true">
        <rect className="ez-logo__bg" width="32" height="32" rx="9" />
        <path className="ez-logo__bars" d="M10 9.5h12M10 16h8M10 22.5h12" strokeWidth="3" strokeLinecap="round" fill="none" />
        <circle className="ez-logo__dot" cx="23" cy="16" r="2.2" />
      </svg>
      {variant === "full" && <span className="ez-logo__word" aria-hidden="true">Ekipzen</span>}
    </span>
  );
}
```

`frontend/src/ui/usePageMeta.ts`:

```ts
import { useEffect } from "react";

export function usePageMeta(title: string, description?: string) {
  useEffect(() => {
    document.title = title === "Ekipzen" ? title : `${title} · Ekipzen`;
    if (!description) return;
    let tag = document.querySelector<HTMLMetaElement>('meta[name="description"]');
    if (!tag) {
      tag = document.createElement("meta");
      tag.name = "description";
      document.head.appendChild(tag);
    }
    tag.content = description;
  }, [title, description]);
}
```

`frontend/src/ui/index.ts`:

```ts
export { Button, ButtonLink, type ButtonProps } from "./Button";
export { Field } from "./Field";
export { Input, Textarea, Select, Checkbox } from "./controls";
export { Card, Badge } from "./Card";
export { Logo } from "./Logo";
export { usePageMeta } from "./usePageMeta";
```

- [ ] **Step 4: `frontend/src/styles/ui.css` yaz**

```css
/* Paylaşılan bileşenler — yalnız --ez-* token'ları kullanır. */
.ez-btn {
  display: inline-flex; align-items: center; justify-content: center; gap: 0.5rem;
  min-height: 44px; padding: 0 1.125rem; border-radius: var(--r-md);
  border: 1px solid transparent; font: 600 var(--fs-14)/1 var(--ez-font-sans);
  text-decoration: none; cursor: pointer; white-space: nowrap;
  transition: background-color var(--dur-fast) var(--ease-out), border-color var(--dur-fast) var(--ease-out),
    color var(--dur-fast) var(--ease-out), box-shadow var(--dur-fast) var(--ease-out), transform var(--dur-fast) var(--ease-out);
}
.ez-btn:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-btn:active { transform: translateY(1px); }
.ez-btn:disabled { opacity: 0.6; cursor: not-allowed; }
.ez-btn--sm { min-height: 36px; padding: 0 0.75rem; font-size: var(--fs-12); }
.ez-btn--lg { min-height: 52px; padding: 0 1.5rem; font-size: var(--fs-16); border-radius: var(--r-lg); }
.ez-btn--block { width: 100%; }
.ez-btn--primary { background: var(--ez-primary); color: var(--ez-on-primary); box-shadow: var(--ez-shadow-xs); }
.ez-btn--primary:hover { background: var(--ez-primary-hover); }
.ez-btn--secondary { background: var(--ez-surface); color: var(--ez-text); border-color: var(--ez-border-strong); }
.ez-btn--secondary:hover { background: var(--ez-surface-2); }
.ez-btn--ghost { background: transparent; color: var(--ez-text-2); }
.ez-btn--ghost:hover { background: var(--ez-surface-2); color: var(--ez-text); }
.ez-btn--danger { background: var(--ez-danger); color: var(--slate-0); }
.ez-btn--danger:hover { background: var(--ez-danger-strong); }

.ez-field { display: grid; gap: 0.375rem; }
.ez-field__label { font: 600 var(--fs-14)/1.4 var(--ez-font-sans); color: var(--ez-text); }
.ez-field__req { color: var(--ez-danger); }
.ez-field__hint { font-size: var(--fs-12); color: var(--ez-text-3); }
.ez-field__error { font-size: var(--fs-12); color: var(--ez-danger); font-weight: 500; }

.ez-input {
  width: 100%; min-height: 44px; padding: 0.625rem 0.875rem;
  border: 1px solid var(--ez-border-strong); border-radius: var(--r-md);
  background: var(--ez-surface); color: var(--ez-text);
  font: 400 var(--fs-16)/1.4 var(--ez-font-sans);
  transition: border-color var(--dur-fast) var(--ease-out), box-shadow var(--dur-fast) var(--ease-out);
}
.ez-input::placeholder { color: var(--ez-text-4); }
.ez-input:hover { border-color: var(--ez-text-4); }
.ez-input:focus { outline: none; border-color: var(--ez-primary); box-shadow: var(--ez-focus-ring); }
.ez-field--error .ez-input { border-color: var(--ez-danger); }
.ez-textarea { min-height: 120px; resize: vertical; }
.ez-select { appearance: auto; }

.ez-check { display: flex; gap: 0.625rem; align-items: flex-start; cursor: pointer; font-size: var(--fs-14); color: var(--ez-text-2); line-height: 1.5; }
.ez-check__box { width: 20px; height: 20px; margin-top: 1px; accent-color: var(--ez-primary); flex-shrink: 0; }
.ez-check__box:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }

.ez-card { background: var(--ez-surface); border: 1px solid var(--ez-border); border-radius: var(--r-lg); box-shadow: var(--ez-shadow-xs); }

.ez-badge {
  display: inline-flex; align-items: center; gap: 0.375rem; padding: 0.25rem 0.625rem;
  border-radius: var(--r-full); font: 600 var(--fs-12)/1.2 var(--ez-font-sans);
}
.ez-badge--neutral { background: var(--ez-surface-2); color: var(--ez-text-2); }
.ez-badge--primary { background: var(--ez-primary-soft); color: var(--ez-primary); }
.ez-badge--success { background: var(--ez-success-soft); color: var(--ez-success-strong); }
.ez-badge--warning { background: var(--ez-warning-soft); color: var(--ez-warning-strong); }
.ez-badge--danger { background: var(--ez-danger-soft); color: var(--ez-danger-strong); }

.ez-logo { display: inline-flex; align-items: center; gap: 0.625rem; }
.ez-logo__mark { flex-shrink: 0; }
.ez-logo__bg { fill: var(--indigo-700); }
.ez-logo__bars { stroke: var(--slate-0); }
.ez-logo__dot { fill: var(--indigo-300); }
.ez-logo__word { font: 800 var(--fs-20)/1 var(--ez-font-sans); letter-spacing: -0.02em; color: var(--ez-text); }
.ez-logo--light .ez-logo__word { color: var(--slate-0); }
.ez-logo--light .ez-logo__bg { fill: var(--indigo-500); }
```

- [ ] **Step 5: Import ekle ve testleri çalıştır**

`frontend/src/main.tsx`: `import "./styles/main.css";` satırının altına `import "./styles/ui.css";` eklenir.

Run: `npm --prefix frontend test -- --run src/ui`
Expected: 7 test PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/ui frontend/src/styles/ui.css frontend/src/main.tsx
git commit -m "feat(ui): add shared button, field, control, card, badge and logo components

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Browser router, `/app` öneki ve eski URL uyumluluğu

**Files:**
- Create: `frontend/src/paths.ts`, `frontend/src/legacyHash.ts`, `frontend/src/legacyHash.test.ts`
- Create: `frontend/src/marketing/MarketingLayout.tsx` (iskelet; Task 6'da tamamlanır)
- Create: `frontend/src/marketing/pages/HomePage.tsx` (iskelet; Task 6'da tamamlanır)
- Create: `frontend/src/marketing/pages/NotFoundPage.tsx`
- Modify: `frontend/src/routes.tsx`, `frontend/src/App.tsx`, `frontend/src/auth/guards.tsx:8`, `frontend/src/layout/AppShell.tsx:47,119`
- Modify: `frontend/src/features/dashboard/RiskCenterPage.tsx` (11 `navigate("/...")` çağrısı + satır 11), `features/dashboard/BackToRisk.tsx:6`, `features/overview/OverviewPage.tsx:70`, `features/payroll/PayrollPage.tsx:14-16,89`
- Test: `frontend/src/routes.test.tsx`, `App.test.tsx`, `layout/AppShell.test.tsx`, `auth/session-expiry.test.tsx`, `auth/AcceptInvitePage.test.tsx`

**Interfaces:**
- Produces:
  - `routes.tsx` exportları:
    - `APP_BASE = "/app"`, `LOGIN_PATH = "/giris"`, `INVITE_PATH = "/davet"`
    - `appPath(path: string): string` → `` `/app${path}` ``
    - `appRoutes[].path` değerleri artık `/app/...`
    - `roleHomeFor(role)` → `"/app/overview"` | `"/app/dashboard"`
  - `legacyHash.ts`: `mapLegacyPath(path: string): string`, `legacyHashTarget(hash: string): string | null`, `LEGACY_APP_ROOT_PATHS: readonly string[]`
  - `MarketingLayout` (Outlet içerir), `HomePage`, `NotFoundPage`

- [ ] **Step 1: legacyHash için failing test** — `frontend/src/legacyHash.test.ts`

```ts
import { expect, test } from "vitest";
import { legacyHashTarget, mapLegacyPath } from "./legacyHash";

test.each([
  ["/login", "/giris"],
  ["/accept-invite", "/davet"],
  ["/dashboard", "/app/dashboard"],
  ["/risk/attrition", "/app/risk/attrition"],
  ["/payroll/settings", "/app/payroll/settings"],
  ["/ozellikler", "/ozellikler"],
  ["/riskli", "/riskli"],
])("mapLegacyPath(%s) → %s", (from, to) => {
  expect(mapLegacyPath(from)).toBe(to);
});

test("hash davet linki sorgusuyla birlikte yeni yola çevrilir", () => {
  expect(legacyHashTarget("#/accept-invite?email=a%40b.c&token=x%2By")).toBe("/davet?email=a%40b.c&token=x%2By");
});

test("hash olmayan ya da çapa olan değerler dokunulmadan kalır", () => {
  expect(legacyHashTarget("")).toBeNull();
  expect(legacyHashTarget("#moduller")).toBeNull();
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/legacyHash.test.ts`
Expected: FAIL (modül yok)

- [ ] **Step 3: `frontend/src/legacyHash.ts` yaz**

```ts
/**
 * Hash router döneminden kalan URL'ler (e-postalardaki /#/accept-invite davetleri,
 * yer imleri) ve köke asılı eski uygulama yolları yeni temiz yollara çevrilir.
 */
export const LEGACY_APP_ROOT_PATHS: readonly string[] = [
  "/dashboard", "/overview", "/actions", "/personnel", "/recruitment",
  "/attendance", "/leaves", "/payroll", "/manager", "/settings", "/risk",
];

export function mapLegacyPath(path: string): string {
  if (path === "/login") return "/giris";
  if (path === "/accept-invite") return "/davet";
  if (LEGACY_APP_ROOT_PATHS.some((root) => path === root || path.startsWith(`${root}/`))) return `/app${path}`;
  return path;
}

export function legacyHashTarget(hash: string): string | null {
  if (!hash.startsWith("#/")) return null;
  const raw = hash.slice(1);
  const queryStart = raw.indexOf("?");
  const path = queryStart === -1 ? raw : raw.slice(0, queryStart);
  const query = queryStart === -1 ? "" : raw.slice(queryStart);
  return `${mapLegacyPath(path)}${query}`;
}
```

- [ ] **Step 4: Testi geçir**

Run: `npm --prefix frontend test -- --run src/legacyHash.test.ts`
Expected: PASS

- [ ] **Step 5: Rota testlerini yeni davranışa göre güncelle (failing)**

`frontend/src/routes.test.tsx` içinde:
- `renderAt("/dashboard")` → `renderAt("/app/dashboard")`
- `renderShellAt("/dashboard")` → `renderShellAt("/app/dashboard")`
- `renderShellAt("/settings")` → `renderShellAt("/app/settings")`

Dosya sonuna eklenir:

```tsx
test("kök yol oturumsuz ziyaretçiye landing gösterir", async () => {
  renderAt("/");
  expect(await screen.findByRole("heading", { level: 1 })).toHaveTextContent("Ekibinizi tek yerden");
  expect(screen.getAllByRole("link", { name: "Giriş yap" })[0]).toHaveAttribute("href", "/giris");
});

test("eski /login yolu yeni giriş ekranına yönlenir", async () => {
  renderAt("/login");
  expect(await screen.findByRole("button", { name: /Giriş yap/ })).toBeInTheDocument();
});

test("oturumlu kullanıcı eski /dashboard yolundan /app/dashboard kabuğuna gider", async () => {
  // Employee, dashboard'da kilit ekranını görür — mevcut testteki kanıtlanmış senaryo.
  localStorage.setItem(SESSION_KEY, sessionFor("employee", "Ahmet Yılmaz"));
  renderShellAt("/dashboard");
  expect(await screen.findByText("Bu alan için yetki gerekli")).toBeInTheDocument();
});

test("bilinmeyen yol 404 sayfası gösterir", async () => {
  renderAt("/olmayan-sayfa");
  expect(await screen.findByRole("heading", { name: "Sayfa bulunamadı" })).toBeInTheDocument();
});
```

`frontend/src/App.test.tsx` tamamen şu olur:

```tsx
import { render, screen } from "@testing-library/react";
import App from "./App";

test("oturum yokken kök adres landing sayfasını açar", async () => {
  localStorage.clear();
  render(<App />);
  expect(await screen.findByRole("heading", { level: 1 })).toHaveTextContent("Ekibinizi tek yerden");
});
```

Diğer testlerde yol önekleri:
- `frontend/src/layout/AppShell.test.tsx`: `"/dashboard"` → `"/app/dashboard"`, `"/overview"` → `"/app/overview"`.
- `frontend/src/auth/session-expiry.test.tsx:43`: `"/dashboard"` → `"/app/dashboard"`.
- `frontend/src/auth/AcceptInvitePage.test.tsx:27`: `` `/accept-invite${query}` `` → `` `/davet${query}` ``.

Run: `npm --prefix frontend test -- --run src/routes.test.tsx src/App.test.tsx`
Expected: FAIL (landing ve `/app` rotaları yok)

- [ ] **Step 6: Marketing iskeleti**

`frontend/src/marketing/MarketingLayout.tsx` (Task 6 genişletir):

```tsx
import { Link, Outlet, ScrollRestoration } from "react-router-dom";
import { ButtonLink, Logo } from "../ui";

export function MarketingLayout() {
  return (
    <div className="ez-site ez-light">
      <a className="ez-skip" href="#icerik">İçeriğe geç</a>
      <header className="ez-header">
        <div className="ez-container ez-header__inner">
          <Link to="/" className="ez-header__brand" aria-label="Ekipzen ana sayfa"><Logo /></Link>
          <div className="ez-header__cta">
            <ButtonLink to="/giris" variant="ghost">Giriş yap</ButtonLink>
            <ButtonLink to="/iletisim">Demo iste</ButtonLink>
          </div>
        </div>
      </header>
      <main id="icerik" tabIndex={-1}>
        <Outlet />
      </main>
      <ScrollRestoration />
    </div>
  );
}
```

`frontend/src/marketing/pages/HomePage.tsx` (Task 6 tamamlar):

```tsx
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
```

`frontend/src/marketing/pages/NotFoundPage.tsx`:

```tsx
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
```

- [ ] **Step 7: `routes.tsx` güncelle**

Import değişiklikleri:

```tsx
import { Navigate, useLocation, type RouteObject } from "react-router-dom";
import { useAuth } from "./auth/AuthContext";
import { MarketingLayout } from "./marketing/MarketingLayout";
import { HomePage } from "./marketing/pages/HomePage";
import { NotFoundPage } from "./marketing/pages/NotFoundPage";
import { LEGACY_APP_ROOT_PATHS, mapLegacyPath } from "./legacyHash";
```

Yol sabitleri döngüsel importu önlemek için ayrı dosyada tutulur. `frontend/src/paths.ts` oluşturulur:

```ts
/** Uygulama genelindeki yol sabitleri. Bileşen importu yoktur; her yerden güvenle import edilir. */
export const APP_BASE = "/app";
export const LOGIN_PATH = "/giris";
export const INVITE_PATH = "/davet";
export const appPath = (path: string): string => `${APP_BASE}${path}`;
export const roleHomeFor = (role: string | null | undefined): string =>
  appPath(role === "employee" ? "/overview" : "/dashboard");
```

`routes.tsx` içinde:
- Mevcut `roleHomeFor` tanımı silinir.
- Yerine eklenir: `import { APP_BASE, INVITE_PATH, LOGIN_PATH, appPath, roleHomeFor } from "./paths";`
- Ayrıca eklenir: `export { APP_BASE, INVITE_PATH, LOGIN_PATH, appPath, roleHomeFor } from "./paths";` (mevcut `../routes` importları bozulmaz).

`appRoutes` dizisindeki her `path: "/x"` değeri `path: appPath("/x")` olur (18 satır; örn. `path: appPath("/dashboard")`, `path: appPath("/risk/compliance")`).

Yeni kod (marketing, auth, AppShell) yol sabitlerini `routes` yerine doğrudan `paths`'ten import eder.

`GatedPage` fonksiyonunun altına eklenir:

```tsx
// Eski yol → yeni yol; sorgu dizesi korunur (davet linklerindeki email/token).
function LegacyRedirect() {
  const location = useLocation();
  return <Navigate to={`${mapLegacyPath(location.pathname)}${location.search}`} replace />;
}

function AppHomeRedirect() {
  const { user } = useAuth();
  return <Navigate to={roleHomeFor(user?.role)} replace />;
}
```

`buildRouteObjects` gövdesi tamamen şu olur:

```tsx
export function buildRouteObjects(): RouteObject[] {
  return [
    {
      element: <MarketingLayout />,
      children: [
        { path: "/", element: <HomePage /> },
        { path: "*", element: <NotFoundPage /> },
      ],
    },
    { path: LOGIN_PATH, element: <PublicOnly><LoginPage /></PublicOnly> },
    { path: INVITE_PATH, element: <PublicOnly><AcceptInvitePage /></PublicOnly> },
    { path: "/login", element: <LegacyRedirect /> },
    { path: "/accept-invite", element: <LegacyRedirect /> },
    // Self-servis kayıt sayfalarının İKİSİ DE kaldırıldı. Şirket hesabı yalnız
    // operatör tarafından, platform anahtarıyla açılır (bkz. TenancyController
    // xmldoc). Eski yer imleri ve paylaşılmış linkler demo talebine düşer.
    { path: "/signup", element: <Navigate to="/iletisim" replace /> },
    { path: "/register-company", element: <Navigate to="/iletisim" replace /> },
    ...LEGACY_APP_ROOT_PATHS.map((root) => ({ path: `${root}/*`, element: <LegacyRedirect /> })),
    {
      path: APP_BASE,
      element: <RequireAuth />,
      children: [
        { index: true, element: <AppHomeRedirect /> },
        {
          element: <AppShell />,
          children: appRoutes.map((route) => ({
            path: route.path,
            element: <GatedPage route={route} />,
          })),
        },
        { path: "*", element: <AppHomeRedirect /> },
      ],
    },
  ];
}
```

- [ ] **Step 8: Guard, kabuk ve sayfa linkleri**

- `frontend/src/auth/guards.tsx:8`: `<Navigate to="/login" replace />` → `<Navigate to={LOGIN_PATH} replace />`; routes importuna `LOGIN_PATH` eklenir.
- `frontend/src/layout/AppShell.tsx:47`: `navigate("/login")` → `navigate(LOGIN_PATH)`; satır 119: `navigate("/actions")` → `navigate(appPath("/actions"))`; routes importuna `LOGIN_PATH, appPath` eklenir.
- `frontend/src/features/dashboard/RiskCenterPage.tsx`:
  - Her `navigate("/<yol>")` → `navigate(appPath("/<yol>"))`.
  - Satır 11'deki `?? "/actions"` → `?? appPath("/actions")`.
  - Routes importuna `appPath` eklenir.
- `frontend/src/features/dashboard/BackToRisk.tsx:6`: `navigate("/dashboard")` → `navigate(appPath("/dashboard"))`; `import { appPath } from "../../routes";`.
- `frontend/src/features/overview/OverviewPage.tsx:70`: `to="/manager"` → `to={appPath("/manager")}` + import.
- `frontend/src/features/payroll/PayrollPage.tsx`: satır 14–16 `path: "/payroll…"` → `path: appPath("/payroll…")`; satır 89 → `navigate(appPath("/payroll/calculator"))` + import.
- `frontend/src/features/actions/queries.ts:17` bir API yoludur, DEĞİŞMEZ.

Run: `grep -rnE "(navigate\(|to=)[\"'\`]/(dashboard|overview|actions|personnel|recruitment|attendance|leaves|payroll|manager|settings|risk|login)" frontend/src --include=*.tsx`
Expected: çıktı yok

- [ ] **Step 9: Browser router ve hash çevirisi**

`frontend/src/App.tsx` şu hale gelir. Router, modül değerlendirilirken konumu okur; bu yüzden çeviri `createBrowserRouter` çağrısından hemen önce yapılır:

```tsx
import { RouterProvider, createBrowserRouter } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "./auth/AuthContext";
import { ToastProvider } from "./layout/ToastProvider";
import { createQueryClient } from "./queryClient";
import { buildRouteObjects } from "./routes";
import { legacyHashTarget } from "./legacyHash";

// Hash router döneminden kalan bağlantılar (ör. /#/accept-invite?…) router kurulmadan çevrilir.
const legacyTarget = legacyHashTarget(window.location.hash);
if (legacyTarget) window.history.replaceState(null, "", legacyTarget);

const router = createBrowserRouter(buildRouteObjects());
const queryClient = createQueryClient();
```

(`App` bileşeninin gövdesi değişmez.) `main.tsx` ve `vite.config.ts` bu adımda değişmez.

- [ ] **Step 10: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS. Kalan başarısızlıklar yalnız yol öneki beklentisinden kaynaklanıyorsa ilgili testte `/app` öneki eklenir; başka davranış değiştirilmez.

- [ ] **Step 11: Lint, build, commit**

Run: `npm --prefix frontend run lint && npm --prefix frontend run build`
Expected: hata yok

```bash
git add frontend
git commit -m "feat(routing): switch to browser router with /app prefix and legacy URL redirects

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Backend — SPA fallback ve temiz davet linki

**Files:**
- Modify: `backend/src/Ekipzen.API/Hosting/PilotHosting.cs` (yeni `MapSpaFallback`)
- Modify: `backend/src/Ekipzen.API/Program.cs:226`
- Modify: `backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs:468`
- Modify: `backend/tests/Ekipzen.Tests.Integration/Hosting/ProductionHostingTests.cs`
- Create: `backend/tests/Ekipzen.Tests.Integration/Tenancy/DavetLinkiTests.cs`

**Interfaces:**
- Produces: `PilotHosting.MapSpaFallback(WebApplication app): void`
  - `/api/*`, `/health*` ve uzantılı (dosya) yollar → 404.
  - Diğer yollar → `wwwroot/index.html`, `Cache-Control: no-cache`.
- Davet e-postası linki: `{App:BaseUrl}/davet?email=…&token=…`

- [ ] **Step 1: Failing testler**

`ProductionHostingTests.cs` → `StartAsync` içinde `app.MapGet("/api/probe", () => "ok");` satırının altına `PilotHosting.MapSpaFallback(app);` eklenir. Sınıfa eklenir:

```csharp
    [Fact]
    public async Task SpaFallback_ServesShellForPageRoutes_ButNotForApiOrFiles()
    {
        await using var app = await StartAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://pilot.example.test");

        var page = await client.GetAsync("/ozellikler");
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await page.Content.ReadAsStringAsync()).Should().Contain("Pilot test shell");
        page.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        page.Headers.CacheControl!.NoCache.Should().BeTrue();

        (await client.GetStringAsync("/app/dashboard")).Should().Contain("Pilot test shell");
        (await client.GetAsync("/api/not-an-endpoint")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync("/assets/missing.js")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
```

`backend/tests/Ekipzen.Tests.Integration/Tenancy/DavetLinkiTests.cs`:

```csharp
using FluentAssertions;
using System.Text.Json;

namespace Ekipzen.Tests.Integration.Tenancy;

/// <summary>Davet e-postası hash router'sız temiz yola (/davet) işaret eder.</summary>
[Collection(ApiCollection.Name)]
public sealed class DavetLinkiTests(EkipzenApiFactory factory) : TenancyTestBase(factory)
{
    [Fact]
    public async Task DavetEpostasi_TemizDavetYolunuKullanir()
    {
        var email = $"link-{Guid.NewGuid():N}@example.test";
        await ProvisionTenantAsync("Link Testi", email);

        var outbox = Path.Combine(Factory.StorageRoot, "outbox");
        var bodies = Directory.EnumerateFiles(outbox, "*.json")
            .Select(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement)
            .Where(root => string.Equals(root.GetProperty("to").GetString(), email, StringComparison.OrdinalIgnoreCase))
            .Select(root => root.GetProperty("body").GetString() ?? "")
            .ToList();

        bodies.Should().ContainSingle();
        bodies[0].Should().Contain("/davet?email=").And.NotContain("/#/");
    }
}
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `dotnet test backend/Ekipzen.sln --filter "FullyQualifiedName~SpaFallback|FullyQualifiedName~DavetLinki"`
Expected: FAIL (`MapSpaFallback` yok, derleme hatası)

- [ ] **Step 3: Uygulama**

`PilotHosting.cs` sınıfına eklenir:

```csharp
    /// <summary>
    /// Temiz URL'li SPA fallback'i: sayfa yolları (/ozellikler, /app/...) index.html ile
    /// yanıtlanır. API/sağlık uçları ve uzantılı dosya istekleri HTML'e düşmez;
    /// eksik bir uç ya da varlık 404 olarak kalır.
    /// </summary>
    public static void MapSpaFallback(WebApplication app)
    {
        app.MapFallback(async context =>
        {
            var path = context.Request.Path;
            var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
            if (path.StartsWithSegments("/api") || path.StartsWithSegments("/health")
                || Path.HasExtension(path.Value) || !index.Exists)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(index);
        });
    }
```

`Program.cs`: `app.MapHealthChecks("/health");` satırının altına `PilotHosting.MapSpaFallback(app);`.

`IdentityService.cs:468`:

```csharp
        var link = $"{appUrl}/davet?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
```

- [ ] **Step 4: Testleri çalıştır**

Run: `dotnet test backend/Ekipzen.sln --filter "FullyQualifiedName~Hosting|FullyQualifiedName~Tenancy|FullyQualifiedName~Auth"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add backend
git commit -m "feat(api): serve SPA fallback and use clean invite links

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Backend — demo talebi uç noktası

**Files:**
- Create: `backend/src/Ekipzen.Application/Features/DemoRequests/SubmitDemoRequestCommand.cs`
- Create: `backend/src/Ekipzen.API/Controllers/PublicController.cs`
- Modify: `backend/src/Ekipzen.API/Program.cs` (rate limit politikası + ayar kaydı)
- Modify: `backend/src/Ekipzen.API/Hosting/ProductionConfiguration.cs` (NotifyEmail kontrolü)
- Modify: `backend/src/Ekipzen.API/appsettings.json`, `appsettings.Development.json`
- Modify: `backend/tests/Ekipzen.Tests.Integration/EkipzenApiFactory.cs` (ortam değişkenleri)
- Modify: `backend/tests/Ekipzen.Tests.Integration/Hosting/ProductionHostingTests.cs` (config sözlüğü + InlineData)
- Create: `backend/tests/Ekipzen.Tests.Integration/DemoRequests/DemoRequestTests.cs`
- Modify: `frontend/src/api/schema.d.ts` (`npm run gen:api` ile)

**Interfaces:**
- Produces: `POST /api/public/demo-requests`
  - Gövde: `{ fullName, email, company, companySize, phone?, message?, kvkkConsent, website? }`
  - Yanıtlar: 204 (başarılı ya da honeypot dolu); 400 ValidationProblemDetails (hata anahtarları PascalCase: `FullName`, `Email`, `Company`, `CompanySize`, `Phone`, `Message`, `KvkkConsent`); 429 (rate limit).
- `companySize` değerleri: `"1-10" | "11-50" | "51-200" | "201-500" | "500+"`
- `DemoRequestSettings(string NotifyEmail)` singleton
- Rate limit politikası adı `"public-form"`; `RateLimiting:PublicFormPermitPerHour` (varsayılan 5)

- [ ] **Step 1: Failing entegrasyon testleri**

`EkipzenApiFactory` yapıcısında `RateLimiting__AuthPermitPerMinute` satırının altına:

```csharp
        Environment.SetEnvironmentVariable("RateLimiting__PublicFormPermitPerHour", "1000000");
        Environment.SetEnvironmentVariable("DemoRequests__NotifyEmail", DemoNotifyEmail);
```

ve sınıfa `public const string DemoNotifyEmail = "demo-talepleri@ekipzen.test";`.

`backend/tests/Ekipzen.Tests.Integration/DemoRequests/DemoRequestTests.cs`:

```csharp
using Ekipzen.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace Ekipzen.Tests.Integration.DemoRequests;

[Collection(ApiCollection.Name)]
public sealed class DemoRequestTests(EkipzenApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object ValidBody(string company, string? website = null) => new
    {
        fullName = "Ayşe Demir",
        email = "ayse@firma.example",
        company,
        companySize = "51-200",
        phone = "+90 555 000 00 00",
        message = "Bordro modülünü görmek istiyoruz.",
        kvkkConsent = true,
        website,
    };

    private IEnumerable<JsonElement> OutboxMessagesFor(string company)
    {
        var outbox = Path.Combine(factory.StorageRoot, "outbox");
        if (!Directory.Exists(outbox)) return [];
        return Directory.EnumerateFiles(outbox, "*.json")
            .Select(file => JsonDocument.Parse(File.ReadAllText(file)).RootElement)
            .Where(root => root.GetProperty("to").GetString() == EkipzenApiFactory.DemoNotifyEmail
                && (root.GetProperty("body").GetString() ?? "").Contains(company))
            .ToList();
    }

    [Fact]
    public async Task GecerliTalep_204Doner_VeBildirimEpostasiGonderir()
    {
        var company = $"Firma-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", ValidBody(company));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var message = OutboxMessagesFor(company).Should().ContainSingle().Subject;
        message.GetProperty("subject").GetString().Should().Be($"Demo talebi: {company}");
        var body = message.GetProperty("body").GetString();
        body.Should().Contain("Ayşe Demir").And.Contain("ayse@firma.example").And.Contain("51-200").And.Contain("KVKK onayı: Evet");
    }

    [Fact]
    public async Task HoneypotDoluysa_204Doner_AmaEpostaGondermez()
    {
        var company = $"Bot-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", ValidBody(company, website: "http://spam.example"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        OutboxMessagesFor(company).Should().BeEmpty();
    }

    [Fact]
    public async Task KvkkOnayiYoksa_400Doner()
    {
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", new
        {
            fullName = "Ali", email = "ali@firma.example", company = "Firma", companySize = "1-10", kvkkConsent = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("KvkkConsent");
    }

    [Fact]
    public async Task GecersizAlanlar_400Doner()
    {
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", new
        {
            fullName = "", email = "gecersiz", company = new string('x', 151), companySize = "999", kvkkConsent = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("FullName").And.Contain("Email").And.Contain("Company").And.Contain("CompanySize");
    }

    [Fact]
    public async Task KonuSatiriYeniSatirIcermez()
    {
        var company = $"Satir-{Guid.NewGuid():N}";
        var response = await _client.PostAsJsonAsync("/api/public/demo-requests", ValidBody($"{company}\r\nBcc: x@y.z"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var subject = OutboxMessagesFor(company).Single().GetProperty("subject").GetString();
        subject.Should().NotContain("\r").And.NotContain("\n");
    }

    [Fact]
    public void Uc_AnonimVeRateLimitli()
    {
        var method = typeof(PublicController).GetMethod(nameof(PublicController.SubmitDemoRequest))!;
        method.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be("public-form");
        typeof(PublicController).GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>().Should().NotBeNull();
    }
}
```

`ProductionHostingTests.cs` config sözlüğüne `["DemoRequests:NotifyEmail"] = "satis@example.test",` eklenir; `UnsafeConfiguration_IsRejectedWithoutPrintingValues` teorisine iki satır eklenir:

```csharp
    [InlineData("DemoRequests:NotifyEmail", "")]
    [InlineData("DemoRequests:NotifyEmail", "not-an-email")]
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `dotnet test backend/Ekipzen.sln --filter "FullyQualifiedName~DemoRequest|FullyQualifiedName~ProductionHosting"`
Expected: FAIL (`PublicController` yok, derleme hatası)

- [ ] **Step 3: Komut, doğrulayıcı, handler**

`backend/src/Ekipzen.Application/Features/DemoRequests/SubmitDemoRequestCommand.cs`:

```csharp
using Ekipzen.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace Ekipzen.Application.Features.DemoRequests;

/// <summary>Demo taleplerinin bildirileceği adres (DemoRequests:NotifyEmail).</summary>
public sealed record DemoRequestSettings(string NotifyEmail);

/// <summary>
/// Landing sayfasındaki demo talep formu. Kayıt oluşturmaz, hesap açmaz: talebi
/// platform sahibine e-postayla iletir; hesap operatör tarafından manuel açılır
/// (self-servis kayıt bilinçli olarak yok — bkz. AuthController xmldoc).
/// <c>Website</c> görünmez bir bal küpü alanıdır; doluysa talep sessizce yok sayılır.
/// </summary>
public sealed record SubmitDemoRequestCommand(
    string FullName,
    string Email,
    string Company,
    string CompanySize,
    string? Phone,
    string? Message,
    bool KvkkConsent,
    string? Website) : IRequest;

public sealed class SubmitDemoRequestCommandValidator : AbstractValidator<SubmitDemoRequestCommand>
{
    public static readonly string[] CompanySizes = ["1-10", "11-50", "51-200", "201-500", "500+"];

    public SubmitDemoRequestCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Ad soyad zorunludur.").MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.").MaximumLength(256);
        RuleFor(x => x.Company).NotEmpty().WithMessage("Şirket adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.CompanySize).Must(size => CompanySizes.Contains(size))
            .WithMessage("Geçerli bir çalışan aralığı seçin.");
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Message).MaximumLength(2000);
        RuleFor(x => x.KvkkConsent).Equal(true).WithMessage("KVKK aydınlatma metnini onaylamanız gerekir.");
    }
}

public sealed class SubmitDemoRequestCommandHandler(IEmailSender emailSender, DemoRequestSettings settings)
    : IRequestHandler<SubmitDemoRequestCommand>
{
    public async Task Handle(SubmitDemoRequestCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website)) return;
        if (string.IsNullOrWhiteSpace(settings.NotifyEmail))
            throw new InvalidOperationException("DemoRequests:NotifyEmail yapılandırılmamış.");

        var subject = $"Demo talebi: {SingleLine(request.Company)}";
        var body =
            $"Yeni demo talebi\n\n" +
            $"Ad soyad: {request.FullName.Trim()}\n" +
            $"E-posta: {request.Email.Trim()}\n" +
            $"Şirket: {request.Company.Trim()}\n" +
            $"Çalışan sayısı: {request.CompanySize}\n" +
            $"Telefon: {(string.IsNullOrWhiteSpace(request.Phone) ? "-" : request.Phone.Trim())}\n" +
            $"KVKK onayı: Evet\n\n" +
            $"Mesaj:\n{(string.IsNullOrWhiteSpace(request.Message) ? "-" : request.Message.Trim())}\n";

        await emailSender.SendAsync(new EmailMessage(settings.NotifyEmail, subject, body), cancellationToken);
    }

    // Konu satırına kullanıcı girdisiyle başlık enjekte edilmesini engeller.
    private static string SingleLine(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
```

- [ ] **Step 4: Controller**

`backend/src/Ekipzen.API/Controllers/PublicController.cs`:

```csharp
using Ekipzen.Application.Features.DemoRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ekipzen.API.Controllers;

/// <summary>
/// Tanıtım sitesinin anonim uçları. Kiracı bağlamı yoktur; TenantAccessMiddleware
/// [AllowAnonymous] uçlara dokunmaz.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public sealed class PublicController(ISender sender) : ControllerBase
{
    /// <remarks>Demo talebini platform sahibine e-postayla iletir. Hesap açmaz.</remarks>
    [HttpPost("demo-requests")]
    [EnableRateLimiting("public-form")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SubmitDemoRequest(SubmitDemoRequestCommand command, CancellationToken cancellationToken)
    {
        await sender.Send(command, cancellationToken);
        return NoContent();
    }
}
```

- [ ] **Step 5: Program.cs, yapılandırma, üretim kapısı**

`Program.cs`, `var authPermitPerMinute` satırının altına:

```csharp
    var publicFormPermitPerHour = builder.Configuration.GetValue("RateLimiting:PublicFormPermitPerHour", 5);
```

`options.AddPolicy("auth", …);` çağrısının altına:

```csharp
        // Tanıtım sitesi formları: anonim, e-posta üretir → IP başına saatlik sınır.
        options.AddPolicy("public-form", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = publicFormPermitPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));
```

`builder.Services.AddHttpContextAccessor();` satırının üstüne:

```csharp
    builder.Services.AddSingleton(new Ekipzen.Application.Features.DemoRequests.DemoRequestSettings(
        builder.Configuration["DemoRequests:NotifyEmail"] ?? ""));
```

`appsettings.json` → `"Smtp"` bloğundan sonra:

```json
  "DemoRequests": {
    "_comment": "Landing demo talep formunun bildirileceği adres. Üretimde zorunlu.",
    "NotifyEmail": ""
  },
```

`appsettings.Development.json` → `"Jwt"` bloğundan önce:

```json
  "DemoRequests": {
    "NotifyEmail": "demo-talepleri@ekipzen.local"
  },
```

`ProductionConfiguration.GetErrors` içinde `Smtp:User` kontrolünün altına:

```csharp
        if (!System.Net.Mail.MailAddress.TryCreate(config["DemoRequests:NotifyEmail"], out _))
            errors.Add("DemoRequests:NotifyEmail geçerli bir e-posta adresi olmalıdır.");
```

- [ ] **Step 6: Testleri çalıştır**

Run: `dotnet test backend/Ekipzen.sln`
Expected: tümü PASS

- [ ] **Step 7: API şemasını yenile ve commit**

`ekipzen-api` çalışırken:

Run: `npm --prefix frontend run gen:api`
Expected: `frontend/src/api/schema.d.ts` içinde `SubmitDemoRequestCommand` şeması görünür.

```bash
git add backend frontend/src/api/schema.d.ts
git commit -m "feat(api): add anonymous demo request endpoint with email notification

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Marketing layout, içerik, görseller ve ana sayfa

> Başlamadan önce `ui-ux-pro-max` ve `frontend-design` skill'leri yüklenir. Landing checklist'i (hero netliği, CTA hiyerarşisi, kontrast, 375/768/1280 düzeni) bu görevde uygulanır.

**Files:**
- Create: `frontend/src/marketing/content.ts`
- Create: `frontend/src/marketing/visuals.tsx`
- Create: `frontend/src/marketing/sections.tsx` (tekrar kullanılan bloklar: `PageHero`, `CtaBand`, `FaqList`, `ModuleIcon`)
- Create: `frontend/src/styles/marketing.css`
- Modify: `frontend/src/marketing/MarketingLayout.tsx` (tam header/nav/mobil menü/footer)
- Modify: `frontend/src/marketing/pages/HomePage.tsx` (tam sayfa)
- Modify: `frontend/src/main.tsx` (`import "./styles/marketing.css";` ui.css importundan sonra)
- Test: `frontend/src/marketing/MarketingLayout.test.tsx`, `frontend/src/marketing/pages/HomePage.test.tsx`

**Interfaces:**
- Consumes: `Button`, `ButtonLink`, `Logo`, `Badge`, `Card`, `usePageMeta` (Task 2); `useAuth` (`frontend/src/auth/AuthContext.tsx`); `roleHomeFor`, `LOGIN_PATH` (Task 3).
- Produces:
  - `content.ts` exportları:
    - `siteInfo`, `navItems: { to: string; label: string }[]`
    - `modules: MarketingModule[]`; `MarketingModule = { id: string; icon: string; title: string; summary: string; points: string[]; roles: string }`
    - `trustPoints: { icon: string; label: string }[]`
    - `steps: { title: string; text: string }[]`
    - `faqs: { q: string; a: string }[]`
    - `securityPillars: { icon: string; title: string; text: string }[]`
    - `plans: Plan[]`; `Plan = { id: string; name: string; audience: string; highlighted?: boolean; features: string[] }`
    - `comparisonRows: { label: string; values: [boolean | string, boolean | string, boolean | string] }[]`
    - `pricingFaqs`
    - `roleViews: { title: string; text: string; points: string[] }[]`
    - `values: { title: string; text: string }[]`
  - `visuals.tsx`: `HeroVisual()`, `RiskScoreCard()`, `LeaveCalendarCard()`, `PayrollSummaryCard()`, `ActionListCard()`, `ModuleVisual({ id }: { id: string })`
  - `sections.tsx`:
    - `PageHero({ eyebrow, title, lead, children? })`
    - `CtaBand({ title?, text? })`
    - `FaqList({ items }: { items: { q: string; a: string }[] })`
    - `ModuleIcon({ icon }: { icon: string })`
  - CSS sınıfları: `ez-site`, `ez-container`, `ez-narrow`, `ez-section`, `ez-section--muted`, `ez-eyebrow`, `ez-lead`, `ez-actions`, `ez-grid-2|3`, `ez-feature-card`, `ez-hero`, `ez-header*`, `ez-footer*`, `ez-skip`, `ez-mock*`, `ez-faq*`, `ez-cta-band`, `ez-split`, `ez-check-list`

- [ ] **Step 1: Failing testler**

`frontend/src/marketing/MarketingLayout.test.tsx`:

```tsx
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, expect, test } from "vitest";
import { AuthProvider } from "../auth/AuthContext";
import { SESSION_KEY } from "../api/session";
import { MarketingLayout } from "./MarketingLayout";

const renderLayout = () => {
  const router = createMemoryRouter(
    [{ element: <MarketingLayout />, children: [{ path: "/", element: <p>içerik</p> }, { path: "/ozellikler", element: <p>özellikler</p> }] }],
    { initialEntries: ["/"] },
  );
  return render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider><RouterProvider router={router} /></AuthProvider>
    </QueryClientProvider>,
  );
};

beforeEach(() => localStorage.clear());

test("ana menü, giriş ve demo bağlantılarını içerir", () => {
  renderLayout();
  const nav = screen.getByRole("navigation", { name: "Ana menü" });
  for (const label of ["Özellikler", "Fiyatlandırma", "Güvenlik", "Hakkımızda"]) {
    expect(within(nav).getByRole("link", { name: label })).toBeInTheDocument();
  }
  expect(screen.getAllByRole("link", { name: "Giriş yap" })[0]).toHaveAttribute("href", "/giris");
  expect(screen.getAllByRole("link", { name: "Demo iste" })[0]).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: "İçeriğe geç" })).toHaveAttribute("href", "#icerik");
});

test("mobil menü düğmesi aria-expanded durumunu değiştirir ve gezinmede kapanır", async () => {
  renderLayout();
  const toggle = screen.getByRole("button", { name: "Menüyü aç" });
  expect(toggle).toHaveAttribute("aria-expanded", "false");
  await userEvent.click(toggle);
  expect(screen.getByRole("button", { name: "Menüyü kapat" })).toHaveAttribute("aria-expanded", "true");
  await userEvent.click(within(screen.getByRole("navigation", { name: "Ana menü" })).getByRole("link", { name: "Özellikler" }));
  expect(await screen.findByText("özellikler")).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Menüyü aç" })).toHaveAttribute("aria-expanded", "false");
});

test("oturum açık kullanıcı 'Uygulamaya git' görür", () => {
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "Ahmet", email: "a@x", role: "employee", roleLabel: "Çalışan", initials: "AY", employeeId: null },
  }));
  renderLayout();
  expect(screen.getAllByRole("link", { name: "Uygulamaya git" })[0]).toHaveAttribute("href", "/app/overview");
  expect(screen.queryByRole("link", { name: "Giriş yap" })).toBeNull();
});

test("footer yasal ve iletişim bağlantılarını içerir", () => {
  renderLayout();
  const footer = screen.getByRole("contentinfo");
  expect(within(footer).getByRole("link", { name: "KVKK aydınlatma metni" })).toHaveAttribute("href", "/kvkk-aydinlatma");
  expect(within(footer).getByRole("link", { name: "İletişim" })).toHaveAttribute("href", "/iletisim");
});
```

`frontend/src/marketing/pages/HomePage.test.tsx`:

```tsx
import { screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { HomePage } from "./HomePage";
import { modules } from "../content";

test("hero vaadi, iki CTA ve sayfa başlığı", () => {
  renderPage(<HomePage />);
  expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Ekibinizi tek yerden, güvenle yönetin");
  expect(screen.getAllByRole("link", { name: "Demo iste" })[0]).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: "Özellikleri incele" })).toHaveAttribute("href", "/ozellikler");
  expect(document.title).toBe("Ekipzen");
});

test("altı modül kartı özellik sayfasına bağlanır", () => {
  renderPage(<HomePage />);
  const section = screen.getByRole("region", { name: "Tek platformda tüm İK süreçleri" });
  expect(modules).toHaveLength(6);
  for (const m of modules) {
    expect(within(section).getByRole("link", { name: new RegExp(m.title) })).toHaveAttribute("href", `/ozellikler#${m.id}`);
  }
});

test("SSS, nasıl çalışır ve güvenlik bölümleri görünür", () => {
  renderPage(<HomePage />);
  expect(screen.getByRole("region", { name: "Sık sorulan sorular" })).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "Üç adımda başlayın" })).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "Verileriniz sizin kontrolünüzde" })).toBeInTheDocument();
});

test("sahte müşteri yorumu ya da logo şeridi yoktur", () => {
  renderPage(<HomePage />);
  expect(screen.queryByText(/müşterimiz|bize güvenen/i)).toBeNull();
});
```

`renderPage` MemoryRouter kullanır, MarketingLayout içermez; HomePage kendi başına render edilir.

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing`
Expected: FAIL (`content` modülü ve menü yok)

- [ ] **Step 3: `frontend/src/marketing/content.ts`**

```ts
/**
 * Tanıtım sitesi metinleri — tek yerden düzenlenir.
 * İÇERİK: siteInfo alanları yayın öncesi gerçek şirket bilgileriyle doğrulanmalıdır.
 * Kural: sahte müşteri, yorum ya da metrik eklenmez; güvenlik maddeleri üründe var olan
 * özelliklerdir (bkz. docs/superpowers/specs/2026-09-17-yeni-kimlik-landing-design.md).
 */
export const siteInfo = {
  brand: "Ekipzen",
  legalName: "Ekipzen",
  email: "iletisim@ekipzen.com",
  city: "Türkiye",
};

export const navItems = [
  { to: "/ozellikler", label: "Özellikler" },
  { to: "/fiyatlandirma", label: "Fiyatlandırma" },
  { to: "/guvenlik", label: "Güvenlik" },
  { to: "/hakkimizda", label: "Hakkımızda" },
];

export type MarketingModule = { id: string; icon: string; title: string; summary: string; points: string[]; roles: string };

export const modules: MarketingModule[] = [
  {
    id: "risk-merkezi", icon: "fa-shield-heart", title: "Risk Merkezi",
    summary: "Ayrılma, tükenmişlik ve uyum sinyallerini sorun büyümeden görün.",
    points: ["Departman bazında risk skoru ve trend", "Yönetici yükü ve çalışan nabzı görünümü", "Sinyalden aksiyona tek tıkla geçiş"],
    roles: "İK yöneticileri ve yöneticiler",
  },
  {
    id: "personel", icon: "fa-user-tie", title: "Personel Yönetimi",
    summary: "Özlük kayıtları, belgeler ve organizasyon tek, düzenli bir kaynakta.",
    points: ["Personel kartı ve belge arşivi", "Excel'den toplu içe aktarma", "Ayrılış süreci ve kayıt geçmişi"],
    roles: "İK ekipleri",
  },
  {
    id: "izin", icon: "fa-plane-departure", title: "İzin Yönetimi",
    summary: "Talep, onay ve bakiye takibi e-posta trafiği olmadan ilerler.",
    points: ["Çalışanın kendi talebini oluşturması", "Yönetici onay akışı", "Güncel izin bakiyeleri"],
    roles: "Tüm çalışanlar",
  },
  {
    id: "puantaj", icon: "fa-clock", title: "Mesai ve Puantaj",
    summary: "Mesai kayıtlarını ve puantajı bordroya hazır hale getirin.",
    points: ["Günlük mesai ve fazla mesai takibi", "Dönem puantaj özeti", "Bordroya veri aktarımı"],
    roles: "İK ve yöneticiler",
  },
  {
    id: "bordro", icon: "fa-money-check-dollar", title: "Bordro",
    summary: "Türkiye mevzuatına uygun, kontrollü ve izlenebilir bordro hesabı.",
    points: ["Dönem bordrosu ve tekil hesaplama", "SGK, vergi ve istisna ayarları", "Çalışanlara pusula erişimi"],
    roles: "İK ve bordro uzmanları",
  },
  {
    id: "ise-alim", icon: "fa-briefcase", title: "İşe Alım",
    summary: "Aday havuzundan teklif aşamasına kadar süreci tek ekrandan yönetin.",
    points: ["Aşama bazlı aday takibi", "Mülakat ve teklif durumları", "İşe alınan adayı davetle sisteme dahil etme"],
    roles: "İK ekipleri",
  },
];

export const trustPoints = [
  { icon: "fa-scale-balanced", label: "KVKK odaklı tasarım" },
  { icon: "fa-database", label: "Şirket başına izole veritabanı" },
  { icon: "fa-user-lock", label: "Rol bazlı yetkilendirme" },
  { icon: "fa-file-invoice", label: "Türkiye bordro mevzuatı" },
];

export const steps = [
  { title: "Demo talep edin", text: "Formu doldurun; ekibimiz ihtiyaçlarınızı dinlemek için sizinle iletişime geçsin." },
  { title: "Hesabınız hazırlansın", text: "Şirketinize ait, diğer müşterilerden izole bir ortam kurulur ve yönetici davetiniz gönderilir." },
  { title: "Ekibinizi davet edin", text: "Personelinizi içe aktarın, rolleri belirleyin ve süreçleri tek yerden yönetmeye başlayın." },
];

export const securityPillars = [
  { icon: "fa-database", title: "İzole veri", text: "Her şirketin verisi kendi veritabanında tutulur; müşteriler arasında paylaşılan tablo yoktur." },
  { icon: "fa-user-lock", title: "Rol bazlı erişim", text: "İK yöneticisi, yönetici ve çalışan rolleri yalnız yetkili oldukları verileri görür; kontrol sunucu tarafında da uygulanır." },
  { icon: "fa-lock", title: "Şifreli iletişim", text: "Trafik HTTPS ile şifrelenir; HSTS ve sıkı içerik güvenliği politikası uygulanır." },
  { icon: "fa-key", title: "Güçlü kimlik doğrulama", text: "Kısa ömürlü oturum anahtarları, güçlü parola kuralları ve giriş denemelerine karşı sınırlama." },
  { icon: "fa-clock-rotate-left", title: "Denetim izi", text: "Kayıtlardaki değişiklikler kim ve ne zaman bilgisiyle izlenir." },
  { icon: "fa-file-export", title: "Veri taşınabilirliği", text: "Her kullanıcı kendi kişisel verisini tek tıkla dışa aktarabilir." },
];

export const faqs = [
  { q: "Ekipzen hangi şirketler için uygun?", a: "Personel, izin, puantaj ve bordro süreçlerini tek yerde toplamak isteyen küçük ve orta ölçekli şirketler için tasarlandı." },
  { q: "Kendim kaydolabilir miyim?", a: "Hayır. Güvenlik gereği her şirket hesabı ekibimiz tarafından ayrı bir ortamda kurulur. Demo talebi bıraktığınızda sizinle iletişime geçiyoruz." },
  { q: "Verilerim nerede ve nasıl saklanıyor?", a: "Her şirketin verisi kendi izole veritabanında tutulur. Erişim rol bazlıdır ve tüm trafik şifrelidir." },
  { q: "Mevcut personel listemi aktarabilir miyim?", a: "Evet. Personel kayıtlarınızı Excel dosyasından toplu olarak içe aktarabilirsiniz." },
  { q: "Çalışanlar sisteme nasıl dahil oluyor?", a: "Her çalışan kendine özel bir davet bağlantısıyla şifresini belirler ve yalnız rolünün izin verdiği ekranları görür." },
];

export type Plan = { id: string; name: string; audience: string; highlighted?: boolean; features: string[] };

export const plans: Plan[] = [
  { id: "baslangic", name: "Başlangıç", audience: "Süreçlerini dijitale taşımaya başlayan küçük ekipler",
    features: ["Personel yönetimi", "İzin yönetimi", "Çalışan self-servis", "E-posta desteği"] },
  { id: "buyuyen", name: "Büyüyen", audience: "Bordro ve puantajı tek yerde toplamak isteyen şirketler", highlighted: true,
    features: ["Başlangıç paketindeki her şey", "Mesai ve puantaj", "Bordro", "İşe alım", "Öncelikli destek"] },
  { id: "kurumsal", name: "Kurumsal", audience: "Risk analitiği ve özel ihtiyaçları olan organizasyonlar",
    features: ["Büyüyen paketindeki her şey", "Risk Merkezi", "Yönetici konsolu", "Kurulum ve veri aktarımı desteği"] },
];

export const comparisonRows: { label: string; values: [boolean | string, boolean | string, boolean | string] }[] = [
  { label: "Personel ve belge yönetimi", values: [true, true, true] },
  { label: "İzin talep ve onay", values: [true, true, true] },
  { label: "Çalışan self-servis", values: [true, true, true] },
  { label: "Mesai ve puantaj", values: [false, true, true] },
  { label: "Bordro hesaplama", values: [false, true, true] },
  { label: "İşe alım", values: [false, true, true] },
  { label: "Risk Merkezi", values: [false, false, true] },
  { label: "Yönetici konsolu", values: [false, false, true] },
  { label: "Destek", values: ["E-posta", "Öncelikli", "Atanmış ekip"] },
];

export const pricingFaqs = [
  { q: "Neden fiyatlar sitede yok?", a: "Fiyat, çalışan sayınıza ve seçtiğiniz modüllere göre belirlenir. Size özel teklifi kısa sürede iletiyoruz." },
  { q: "Paketler arasında geçiş yapabilir miyim?", a: "Evet. İhtiyacınız değiştikçe paketinizi yükseltebilirsiniz; verileriniz olduğu gibi korunur." },
  { q: "Kurulum ücreti var mı?", a: "Kurulum kapsamı teklifinizde açıkça belirtilir; sürpriz kalem çıkmaz." },
];

export const roleViews = [
  { title: "İK yöneticisi", text: "Tüm süreçleri ve riskleri tek panelden yönetir.", points: ["Risk Merkezi ve aksiyonlar", "Personel, bordro ve işe alım", "Şirket ayarları"] },
  { title: "Yönetici", text: "Ekibinin durumunu ve onay bekleyen işleri görür.", points: ["Ekip izin ve mesai onayları", "Yönetici konsolu", "Ekip risk sinyalleri"] },
  { title: "Çalışan", text: "Kendi işlerini İK'ya e-posta atmadan halleder.", points: ["İzin talebi ve bakiye", "Bordro pusulası", "Kişisel veri dışa aktarımı"] },
];

export const values = [
  { title: "Önce güven", text: "İK verisi bir şirketin en hassas verisidir. Mimariyi bu sorumlulukla, izolasyon ve en az yetki ilkesiyle kurduk." },
  { title: "Sadelik", text: "Her ekran bir işi bitirmek için var. Gereksiz adımları ve kalabalığı ayıklıyoruz." },
  { title: "Yerel uygunluk", text: "Türkiye'nin bordro ve KVKK gerekliliklerini sonradan eklenen bir katman olarak değil, temelden ele alıyoruz." },
];
```

- [ ] **Step 4: `frontend/src/marketing/visuals.tsx`** (dekoratif, örnek veri; ekran okuyucuya tek cümle)

```tsx
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
```

- [ ] **Step 5: `frontend/src/marketing/sections.tsx`**

```tsx
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

export function CtaBand({ title = "Ekipzen'i ekibinizle birlikte görün", text = "30 dakikalık bir tanıtımda süreçlerinize nasıl uyduğunu birlikte inceleyelim." }: { title?: string; text?: string }) {
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
            <ButtonLink to="/iletisim" size="lg">Demo iste</ButtonLink>
            <ButtonLink to="/fiyatlandirma" size="lg" variant="secondary">Paketleri gör</ButtonLink>
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
```

- [ ] **Step 6: `MarketingLayout.tsx` tam hali**

```tsx
import { useEffect, useState } from "react";
import { Link, NavLink, Outlet, ScrollRestoration, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { LOGIN_PATH, roleHomeFor } from "../paths";
import { ButtonLink, Logo } from "../ui";
import { modules, navItems, siteInfo } from "./content";

export function MarketingLayout() {
  const { user } = useAuth();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);

  useEffect(() => setMenuOpen(false), [location.pathname]);

  const primaryAction = user
    ? <ButtonLink to={roleHomeFor(user.role)} variant="ghost">Uygulamaya git</ButtonLink>
    : <ButtonLink to={LOGIN_PATH} variant="ghost">Giriş yap</ButtonLink>;

  return (
    <div className="ez-site ez-light">
      <a className="ez-skip" href="#icerik">İçeriğe geç</a>
      <header className={`ez-header${menuOpen ? " is-open" : ""}`}>
        <div className="ez-container ez-header__inner">
          <Link to="/" className="ez-header__brand" aria-label="Ekipzen ana sayfa"><Logo /></Link>
          <nav id="ana-menu" className="ez-header__nav" aria-label="Ana menü">
            <ul>
              {navItems.map((item) => (
                <li key={item.to}><NavLink to={item.to} className="ez-header__link">{item.label}</NavLink></li>
              ))}
            </ul>
            <div className="ez-header__cta">
              {primaryAction}
              <ButtonLink to="/iletisim">Demo iste</ButtonLink>
            </div>
          </nav>
          <button
            type="button"
            className="ez-header__toggle"
            aria-expanded={menuOpen}
            aria-controls="ana-menu"
            aria-label={menuOpen ? "Menüyü kapat" : "Menüyü aç"}
            onClick={() => setMenuOpen((open) => !open)}
          >
            <i aria-hidden="true" className={`fa-solid ${menuOpen ? "fa-xmark" : "fa-bars"}`} />
          </button>
        </div>
      </header>

      <main id="icerik" tabIndex={-1}>
        <Outlet />
      </main>

      <footer className="ez-footer">
        <div className="ez-container ez-footer__grid">
          <div className="ez-footer__brand">
            <Logo tone="light" />
            <p>Personel, izin, puantaj, bordro ve işe alım süreçleriniz için güvenli İK platformu.</p>
          </div>
          <div>
            <h2 className="ez-footer__title">Ürün</h2>
            <ul>
              {modules.slice(0, 4).map((m) => <li key={m.id}><Link to={`/ozellikler#${m.id}`}>{m.title}</Link></li>)}
              <li><Link to="/fiyatlandirma">Fiyatlandırma</Link></li>
            </ul>
          </div>
          <div>
            <h2 className="ez-footer__title">Şirket</h2>
            <ul>
              <li><Link to="/hakkimizda">Hakkımızda</Link></li>
              <li><Link to="/guvenlik">Güvenlik</Link></li>
              <li><Link to="/iletisim">İletişim</Link></li>
            </ul>
          </div>
          <div>
            <h2 className="ez-footer__title">Yasal</h2>
            <ul>
              <li><Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link></li>
              <li><a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a></li>
            </ul>
          </div>
        </div>
        <div className="ez-container ez-footer__bottom">
          <p>© {new Date().getFullYear()} {siteInfo.legalName}. Tüm hakları saklıdır.</p>
        </div>
      </footer>
      <ScrollRestoration />
    </div>
  );
}
```

Not: Yol sabitleri `paths.ts`ten geldiği için `routes.tsx` ↔ `MarketingLayout` arasında döngüsel import oluşmaz.

- [ ] **Step 7: `HomePage.tsx` tam hali**

```tsx
import { Link } from "react-router-dom";
import { ButtonLink, usePageMeta } from "../../ui";
import { faqs, modules, securityPillars, steps, trustPoints } from "../content";
import { CtaBand, FaqList, ModuleIcon } from "../sections";
import { ActionListCard, HeroVisual, RiskScoreCard } from "../visuals";

export function HomePage() {
  usePageMeta("Ekipzen", "Ekipzen ile personel, izin, puantaj, bordro ve işe alım süreçlerinizi tek, güvenli platformda yönetin.");
  return (
    <>
      <section className="ez-hero">
        <div className="ez-container ez-hero__grid">
          <div className="ez-hero__copy">
            <p className="ez-eyebrow">Türkiye için İK platformu</p>
            <h1>Ekibinizi tek yerden, güvenle yönetin</h1>
            <p className="ez-lead">
              Personel, izin, puantaj, bordro ve işe alım tek platformda. Risk sinyallerini erken görün,
              ekibinizi e-posta trafiğinden kurtarın.
            </p>
            <div className="ez-actions">
              <ButtonLink to="/iletisim" size="lg">Demo iste</ButtonLink>
              <ButtonLink to="/ozellikler" size="lg" variant="secondary">Özellikleri incele</ButtonLink>
            </div>
            <ul className="ez-trust" aria-label="Öne çıkan güvenceler">
              {trustPoints.map((t) => (
                <li key={t.label}><i aria-hidden="true" className={`fa-solid ${t.icon}`} />{t.label}</li>
              ))}
            </ul>
          </div>
          <HeroVisual />
        </div>
      </section>

      <section className="ez-section" aria-labelledby="moduller-baslik">
        <div className="ez-container">
          <div className="ez-section__head">
            <p className="ez-eyebrow">Modüller</p>
            <h2 id="moduller-baslik">Tek platformda tüm İK süreçleri</h2>
            <p className="ez-lead">Dağınık tablolar ve e-posta zincirleri yerine, birbirine bağlı modüller.</p>
          </div>
          <ul className="ez-grid-3">
            {modules.map((m) => (
              <li key={m.id}>
                <Link to={`/ozellikler#${m.id}`} className="ez-feature-card">
                  <ModuleIcon icon={m.icon} />
                  <h3>{m.title}</h3>
                  <p>{m.summary}</p>
                  <span className="ez-feature-card__more">Detaylar <i aria-hidden="true" className="fa-solid fa-arrow-right" /></span>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="risk-baslik">
        <div className="ez-container ez-split">
          <div>
            <p className="ez-eyebrow">Risk Merkezi</p>
            <h2 id="risk-baslik">Sorunları istifa mektubundan önce görün</h2>
            <p className="ez-lead">
              Ayrılma, tükenmişlik ve uyum sinyalleri tek ekranda toplanır; her sinyal somut bir aksiyon önerisine bağlanır.
            </p>
            <ul className="ez-check-list">
              <li>Departman bazında risk skoru ve trend</li>
              <li>Yönetici yükü ve çalışan nabzı</li>
              <li>Önerilen aksiyonların takibi</li>
            </ul>
            <ButtonLink to="/ozellikler#risk-merkezi" variant="secondary">Risk Merkezi'ni incele</ButtonLink>
          </div>
          <div className="ez-split__visual" aria-hidden="true">
            <RiskScoreCard />
            <ActionListCard />
          </div>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="adimlar-baslik">
        <div className="ez-container">
          <div className="ez-section__head">
            <p className="ez-eyebrow">Nasıl çalışır</p>
            <h2 id="adimlar-baslik">Üç adımda başlayın</h2>
          </div>
          <ol className="ez-steps">
            {steps.map((s, i) => (
              <li key={s.title} className="ez-steps__item">
                <span className="ez-steps__num" aria-hidden="true">{i + 1}</span>
                <h3>{s.title}</h3>
                <p>{s.text}</p>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <section className="ez-section ez-section--dark" aria-labelledby="guvenlik-baslik">
        <div className="ez-container">
          <div className="ez-section__head">
            <p className="ez-eyebrow">Güvenlik</p>
            <h2 id="guvenlik-baslik">Verileriniz sizin kontrolünüzde</h2>
            <p className="ez-lead">İK verisi hassastır. Ekipzen'i izolasyon ve en az yetki ilkesiyle kurduk.</p>
          </div>
          <ul className="ez-grid-3">
            {securityPillars.slice(0, 3).map((p) => (
              <li key={p.title} className="ez-pillar">
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </li>
            ))}
          </ul>
          <ButtonLink to="/guvenlik" variant="secondary">Güvenlik yaklaşımımız</ButtonLink>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="sss-baslik">
        <div className="ez-container ez-narrow">
          <div className="ez-section__head">
            <h2 id="sss-baslik">Sık sorulan sorular</h2>
          </div>
          <FaqList items={faqs} />
        </div>
      </section>

      <CtaBand />
    </>
  );
}
```

- [ ] **Step 8: `frontend/src/styles/marketing.css`**

```css
/* Tanıtım sitesi — yalnız --ez-* token'ları. Kök: .ez-site.ez-light */
.ez-site { min-height: 100vh; background: var(--ez-surface); color: var(--ez-text-2); font-family: var(--ez-font-sans); display: flex; flex-direction: column; }
.ez-site main { flex: 1; outline: none; }
.ez-site h1, .ez-site h2, .ez-site h3 { color: var(--ez-text); letter-spacing: -0.02em; }
.ez-site h1 { font-size: clamp(2.25rem, 1.4rem + 3.4vw, 3.75rem); line-height: 1.08; font-weight: 800; }
.ez-site h2 { font-size: clamp(1.75rem, 1.3rem + 1.6vw, 2.25rem); line-height: 1.2; font-weight: 700; }
.ez-site h3 { font-size: var(--fs-18); line-height: 1.35; font-weight: 700; }
.ez-site p { line-height: 1.65; }
.ez-sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; border: 0; }

.ez-skip { position: absolute; left: 1rem; top: -3rem; z-index: 100; padding: 0.75rem 1rem; border-radius: var(--r-md); background: var(--ez-primary); color: var(--ez-on-primary); font-weight: 600; }
.ez-skip:focus { top: 1rem; }

.ez-container { width: 100%; max-width: 1200px; margin-inline: auto; padding-inline: clamp(1rem, 4vw, 2rem); }
.ez-narrow { max-width: 820px; }
.ez-section { padding-block: clamp(3.5rem, 8vw, 6rem); }
.ez-section--muted { background: var(--ez-bg); }
.ez-section--dark { background: var(--slate-900); }
.ez-section--dark h2, .ez-section--dark h3 { color: var(--slate-0); }
.ez-section--dark p { color: var(--slate-300); }
.ez-section--dark .ez-eyebrow { color: var(--indigo-300); }
.ez-section--dark .ez-pillar { background: rgb(255 255 255 / 0.04); border-color: rgb(255 255 255 / 0.08); }
.ez-section__head { max-width: 680px; margin-bottom: 2.5rem; display: grid; gap: 0.75rem; }
.ez-eyebrow { font: 700 var(--fs-12)/1.2 var(--ez-font-sans); letter-spacing: 0.08em; text-transform: uppercase; color: var(--ez-primary); }
.ez-lead { font-size: var(--fs-18); color: var(--ez-text-3); }
.ez-section--dark .ez-lead { color: var(--slate-300); }
.ez-actions { display: flex; flex-wrap: wrap; gap: 0.75rem; margin-top: 1.75rem; }

.ez-header { position: sticky; top: 0; z-index: 50; background: rgb(255 255 255 / 0.85); backdrop-filter: saturate(1.4) blur(12px); border-bottom: 1px solid var(--ez-border); }
.ez-header__inner { display: flex; align-items: center; gap: 2rem; min-height: 72px; }
.ez-header__brand { display: inline-flex; border-radius: var(--r-md); }
.ez-header__brand:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-header__nav { flex: 1; display: flex; align-items: center; justify-content: space-between; gap: 1.5rem; }
.ez-header__nav ul { display: flex; gap: 0.25rem; list-style: none; }
.ez-header__link { display: inline-flex; align-items: center; min-height: 44px; padding: 0 0.875rem; border-radius: var(--r-md); color: var(--ez-text-2); font-weight: 600; font-size: var(--fs-14); text-decoration: none; }
.ez-header__link:hover { background: var(--ez-surface-2); color: var(--ez-text); }
.ez-header__link.active { color: var(--ez-primary); }
.ez-header__link:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-header__cta { display: flex; gap: 0.5rem; align-items: center; }
.ez-header__toggle { display: none; margin-left: auto; width: 44px; height: 44px; border-radius: var(--r-md); border: 1px solid var(--ez-border); background: var(--ez-surface); color: var(--ez-text); font-size: var(--fs-18); cursor: pointer; }
.ez-header__toggle:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }

@media (max-width: 900px) {
  .ez-header__toggle { display: inline-flex; align-items: center; justify-content: center; }
  .ez-header__nav { display: none; position: absolute; inset: 72px 0 auto 0; padding: 1rem clamp(1rem, 4vw, 2rem) 1.5rem; background: var(--ez-surface); border-bottom: 1px solid var(--ez-border); box-shadow: var(--ez-shadow-md); flex-direction: column; align-items: stretch; }
  .ez-header.is-open .ez-header__nav { display: flex; }
  .ez-header__nav ul { flex-direction: column; }
  .ez-header__cta { flex-direction: column; align-items: stretch; }
}

.ez-hero { position: relative; overflow: hidden; padding-block: clamp(3rem, 7vw, 6rem); background:
  radial-gradient(60rem 30rem at 85% -10%, var(--indigo-100), transparent 60%),
  radial-gradient(40rem 24rem at -10% 110%, var(--indigo-50), transparent 60%), var(--ez-surface); }
.ez-hero__grid { display: grid; grid-template-columns: 1.05fr 1fr; gap: clamp(2rem, 5vw, 4rem); align-items: center; }
.ez-hero__copy { display: grid; gap: 1.25rem; }
.ez-hero__copy .ez-actions { margin-top: 0.5rem; }
.ez-trust { list-style: none; display: flex; flex-wrap: wrap; gap: 0.5rem 1.25rem; margin-top: 1rem; font-size: var(--fs-14); color: var(--ez-text-3); font-weight: 500; }
.ez-trust i { color: var(--ez-primary); margin-right: 0.375rem; }
@media (max-width: 960px) { .ez-hero__grid { grid-template-columns: 1fr; } }

.ez-hero-visual, .ez-module-visual { margin: 0; }
.ez-hero-visual__stack { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; transform: rotate(-1.5deg); }
.ez-hero-visual__stack > :nth-child(2) { transform: translateY(1.5rem); }
.ez-hero-visual__stack > :nth-child(4) { transform: translateY(1.5rem); }
.ez-module-visual__stack, .ez-split__visual { display: grid; gap: 1rem; }
@media (max-width: 520px) { .ez-hero-visual__stack { grid-template-columns: 1fr; transform: none; } .ez-hero-visual__stack > * { transform: none !important; } }

.ez-mock { background: var(--ez-surface); border: 1px solid var(--ez-border); border-radius: var(--r-lg); box-shadow: var(--ez-shadow-md); padding: 1rem 1.125rem; display: grid; gap: 0.875rem; font-size: var(--fs-14); }
.ez-mock__head { display: flex; justify-content: space-between; align-items: center; gap: 0.5rem; }
.ez-mock__title { font-weight: 700; color: var(--ez-text); }
.ez-mock__badge { font-size: var(--fs-12); font-weight: 600; padding: 0.125rem 0.5rem; border-radius: var(--r-full); background: var(--ez-primary-soft); color: var(--ez-primary); }
.ez-mock__metric strong { font-size: var(--fs-36); font-weight: 800; color: var(--ez-text); letter-spacing: -0.03em; }
.ez-mock__metric span { color: var(--ez-success); font-weight: 600; margin-left: 0.25rem; }
.ez-mock__bars { display: flex; align-items: flex-end; gap: 6px; height: 56px; }
.ez-mock__bars span { flex: 1; border-radius: 4px 4px 0 0; background: var(--indigo-200); }
.ez-mock__bars span:last-child { background: var(--ez-primary); }
.ez-mock__calendar { display: grid; grid-template-columns: repeat(7, 1fr); gap: 4px; }
.ez-mock__calendar span { display: grid; place-items: center; height: 28px; border-radius: 6px; background: var(--ez-surface-2); font: 500 var(--fs-12) var(--ez-font-mono); color: var(--ez-text-3); }
.ez-mock__calendar .is-busy { background: var(--ez-primary); color: var(--ez-on-primary); }
.ez-mock__row { display: flex; align-items: center; gap: 0.5rem; color: var(--ez-text-3); }
.ez-mock__dot { width: 8px; height: 8px; border-radius: 50%; background: var(--ez-warning); }
.ez-mock__list { display: grid; gap: 0.5rem; }
.ez-mock__list div { display: flex; justify-content: space-between; gap: 1rem; }
.ez-mock__list dt { color: var(--ez-text-3); }
.ez-mock__list dd { font-family: var(--ez-font-mono); font-weight: 600; color: var(--ez-text); font-variant-numeric: tabular-nums; }
.ez-mock__list div:last-child { padding-top: 0.5rem; border-top: 1px solid var(--ez-border); }
.ez-mock__tasks { list-style: none; display: grid; gap: 0.5rem; }
.ez-mock__tasks li { display: flex; gap: 0.5rem; align-items: flex-start; color: var(--ez-text-2); }
.ez-mock__tasks li span { width: 16px; height: 16px; margin-top: 2px; border-radius: 5px; border: 2px solid var(--ez-border-strong); flex-shrink: 0; }
.ez-mock__tasks li.is-done { color: var(--ez-text-4); text-decoration: line-through; }
.ez-mock__tasks li.is-done span { background: var(--ez-success); border-color: var(--ez-success); }

.ez-grid-2, .ez-grid-3 { list-style: none; display: grid; gap: 1.25rem; }
.ez-grid-2 { grid-template-columns: repeat(auto-fit, minmax(min(100%, 320px), 1fr)); }
.ez-grid-3 { grid-template-columns: repeat(auto-fit, minmax(min(100%, 280px), 1fr)); }
.ez-section--dark .ez-grid-3 { margin-bottom: 2rem; }

.ez-icon { display: inline-grid; place-items: center; width: 44px; height: 44px; border-radius: var(--r-md); background: var(--ez-primary-soft); color: var(--ez-primary); font-size: var(--fs-18); }
.ez-section--dark .ez-icon { background: rgb(129 140 248 / 0.16); color: var(--indigo-300); }

.ez-feature-card, .ez-pillar { height: 100%; display: grid; align-content: start; gap: 0.75rem; padding: 1.5rem; border-radius: var(--r-lg); border: 1px solid var(--ez-border); background: var(--ez-surface); text-decoration: none; color: inherit; }
.ez-feature-card { transition: border-color var(--dur-base) var(--ease-out), box-shadow var(--dur-base) var(--ease-out), transform var(--dur-base) var(--ease-out); }
.ez-feature-card:hover { border-color: var(--indigo-200); box-shadow: var(--ez-shadow-md); transform: translateY(-2px); }
.ez-feature-card:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-feature-card p { color: var(--ez-text-3); }
.ez-feature-card__more { font-weight: 600; font-size: var(--fs-14); color: var(--ez-primary); }

.ez-split { display: grid; grid-template-columns: 1fr 1fr; gap: clamp(2rem, 5vw, 4rem); align-items: center; }
.ez-split > div:first-child { display: grid; gap: 1rem; justify-items: start; }
.ez-split--reverse > div:first-child { order: 2; }
@media (max-width: 900px) { .ez-split { grid-template-columns: 1fr; } .ez-split--reverse > div:first-child { order: 0; } }

.ez-check-list { list-style: none; display: grid; gap: 0.625rem; }
.ez-check-list li { display: flex; gap: 0.625rem; align-items: flex-start; }
.ez-check-list li::before { content: ""; width: 20px; height: 20px; margin-top: 2px; flex-shrink: 0; border-radius: 50%; background: var(--ez-success-soft) center / 12px no-repeat; box-shadow: inset 0 0 0 5px var(--ez-success-soft), inset 0 0 0 10px var(--ez-success); }

.ez-steps { list-style: none; display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 260px), 1fr)); gap: 1.25rem; }
.ez-steps__item { display: grid; gap: 0.625rem; padding: 1.5rem; border-radius: var(--r-lg); background: var(--ez-bg); }
.ez-steps__num { display: grid; place-items: center; width: 36px; height: 36px; border-radius: 50%; background: var(--ez-primary); color: var(--ez-on-primary); font-weight: 700; }

.ez-faq { display: grid; gap: 0.75rem; }
.ez-faq__item { border: 1px solid var(--ez-border); border-radius: var(--r-lg); background: var(--ez-surface); }
.ez-faq__item summary { list-style: none; display: flex; justify-content: space-between; align-items: center; gap: 1rem; min-height: 56px; padding: 0.875rem 1.25rem; cursor: pointer; font-weight: 600; color: var(--ez-text); }
.ez-faq__item summary::-webkit-details-marker { display: none; }
.ez-faq__item summary:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); border-radius: var(--r-lg); }
.ez-faq__item summary i { color: var(--ez-text-4); transition: transform var(--dur-base) var(--ease-out); }
.ez-faq__item[open] summary i { transform: rotate(180deg); }
.ez-faq__item p { padding: 0 1.25rem 1.125rem; color: var(--ez-text-3); }

.ez-cta-band { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 1.5rem; padding: clamp(2rem, 5vw, 3rem); border-radius: var(--r-xl); background: linear-gradient(135deg, var(--indigo-700), var(--indigo-900)); }
.ez-cta-band h2 { color: var(--slate-0); }
.ez-cta-band p { color: var(--indigo-100); margin-top: 0.5rem; max-width: 520px; }
.ez-cta-band .ez-actions { margin-top: 0; }
.ez-cta-band .ez-btn--primary { background: var(--slate-0); color: var(--indigo-800); }
.ez-cta-band .ez-btn--primary:hover { background: var(--indigo-50); }
.ez-cta-band .ez-btn--secondary { background: transparent; color: var(--slate-0); border-color: rgb(255 255 255 / 0.4); }
.ez-cta-band .ez-btn--secondary:hover { background: rgb(255 255 255 / 0.1); }

.ez-page-hero { padding-block: clamp(3rem, 7vw, 5rem) clamp(2rem, 4vw, 3rem); background: radial-gradient(50rem 24rem at 50% -20%, var(--indigo-100), transparent 70%), var(--ez-surface); text-align: center; }
.ez-page-hero .ez-narrow { display: grid; gap: 1rem; justify-items: center; }
.ez-page-hero .ez-actions { justify-content: center; }

.ez-footer { background: var(--slate-900); color: var(--slate-400); padding-top: 3.5rem; }
.ez-footer__grid { display: grid; grid-template-columns: 2fr repeat(3, 1fr); gap: 2rem; }
.ez-footer__brand p { margin-top: 1rem; max-width: 320px; }
.ez-footer__title { font-size: var(--fs-14) !important; color: var(--slate-0) !important; margin-bottom: 0.75rem; letter-spacing: 0 !important; }
.ez-footer ul { list-style: none; display: grid; gap: 0.25rem; }
.ez-footer a { display: inline-flex; min-height: 32px; align-items: center; color: var(--slate-400); text-decoration: none; }
.ez-footer a:hover { color: var(--slate-0); }
.ez-footer a:focus-visible { outline: none; box-shadow: 0 0 0 3px rgb(129 140 248 / 0.5); border-radius: var(--r-sm); }
.ez-footer__bottom { margin-top: 2.5rem; padding-block: 1.5rem; border-top: 1px solid rgb(255 255 255 / 0.08); font-size: var(--fs-14); }
@media (max-width: 760px) { .ez-footer__grid { grid-template-columns: 1fr 1fr; } .ez-footer__brand { grid-column: 1 / -1; } }
```

`frontend/src/main.tsx`: `import "./styles/ui.css";` altına `import "./styles/marketing.css";`.

- [ ] **Step 9: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 10: Tarayıcıda görsel kontrol**

`ekipzen-frontend` launch config'i `preview_start` ile açılır. `/` adresinde 1280, 768 ve 375 genişliklerinde ekran görüntüsü alınır. Kontrol edilecekler: yatay kaydırma yok; mobil menü açılıyor; hero kartları taşmıyor; konsolda hata yok.

- [ ] **Step 11: Commit**

```bash
git add frontend/src/marketing frontend/src/styles/marketing.css frontend/src/main.tsx
git commit -m "feat(landing): add marketing layout, content and home page

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Özellikler, Fiyatlandırma, Hakkımızda, Güvenlik ve KVKK sayfaları

**Files:**
- Create: `frontend/src/marketing/pages/FeaturesPage.tsx`, `PricingPage.tsx`, `AboutPage.tsx`, `SecurityPage.tsx`, `KvkkPage.tsx`
- Create: `frontend/src/marketing/pages/pages.test.tsx`
- Modify: `frontend/src/routes.tsx` (marketing çocuk rotaları, lazy)
- Modify: `frontend/src/styles/marketing.css` (fiyat kartları, karşılaştırma tablosu, yasal metin)

**Interfaces:**
- Consumes: `content.ts`, `sections.tsx`, `visuals.tsx` (Task 6); `ButtonLink`, `Badge`, `usePageMeta` (Task 2).
- Produces: named exportlar `FeaturesPage`, `PricingPage`, `AboutPage`, `SecurityPage`, `KvkkPage`.
- Rotalar: `/ozellikler`, `/fiyatlandirma`, `/hakkimizda`, `/guvenlik`, `/kvkk-aydinlatma`.

- [ ] **Step 1: Failing test** — `frontend/src/marketing/pages/pages.test.tsx`

```tsx
import { screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { modules, plans } from "../content";
import { AboutPage } from "./AboutPage";
import { FeaturesPage } from "./FeaturesPage";
import { KvkkPage } from "./KvkkPage";
import { PricingPage } from "./PricingPage";
import { SecurityPage } from "./SecurityPage";

test("Özellikler: her modül anchor id'li bir bölümdür", () => {
  renderPage(<FeaturesPage />);
  expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  for (const m of modules) {
    const section = document.getElementById(m.id);
    expect(section, m.id).not.toBeNull();
    expect(within(section!).getByRole("heading", { level: 2, name: m.title })).toBeInTheDocument();
  }
  expect(screen.getByRole("heading", { name: "Her rol kendi ekranını görür" })).toBeInTheDocument();
  expect(document.title).toBe("Özellikler · Ekipzen");
});

test("Fiyatlandırma: üç paket, rakam yok, teklif CTA'sı ve karşılaştırma tablosu", () => {
  renderPage(<PricingPage />);
  for (const p of plans) {
    expect(screen.getByRole("heading", { level: 2, name: p.name })).toBeInTheDocument();
  }
  expect(screen.getAllByRole("link", { name: /teklif alın/i })).toHaveLength(plans.length);
  expect(screen.queryByText(/₺|TL\/ay|\$/)).toBeNull();
  const table = screen.getByRole("table", { name: "Paket karşılaştırması" });
  expect(within(table).getAllByRole("columnheader")).toHaveLength(4);
});

test("Güvenlik: altı güvence ve KVKK bölümü", () => {
  renderPage(<SecurityPage />);
  expect(screen.getAllByRole("heading", { level: 3 })).toHaveLength(6);
  expect(screen.getByRole("heading", { name: "KVKK ve kişisel veriler" })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "KVKK aydınlatma metni" })).toHaveAttribute("href", "/kvkk-aydinlatma");
});

test("Hakkımızda: değerler listelenir, uydurma kişi ya da sayı yok", () => {
  renderPage(<AboutPage />);
  expect(screen.getByRole("heading", { name: "Değerlerimiz" })).toBeInTheDocument();
  expect(screen.queryByText(/kurucu|CEO|\d+\+ müşteri/i)).toBeNull();
});

test("KVKK: veri sorumlusu ve haklar bölümleri", () => {
  renderPage(<KvkkPage />);
  expect(screen.getByRole("heading", { level: 1, name: "KVKK aydınlatma metni" })).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "İlgili kişi olarak haklarınız" })).toBeInTheDocument();
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing/pages/pages.test.tsx`
Expected: FAIL (modüller yok)

- [ ] **Step 3: `FeaturesPage.tsx`**

```tsx
import { ButtonLink, usePageMeta } from "../../ui";
import { modules, roleViews } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";
import { ModuleVisual } from "../visuals";

export function FeaturesPage() {
  usePageMeta("Özellikler", "Risk Merkezi, personel, izin, puantaj, bordro ve işe alım modüllerini inceleyin.");
  return (
    <>
      <PageHero
        eyebrow="Özellikler"
        title="İK operasyonunuz için ihtiyacınız olan her şey"
        lead="Birbirine bağlı modüller sayesinde bir kez girilen bilgi tüm süreçlerde doğru kalır."
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
            <p className="ez-eyebrow">Rol bazlı deneyim</p>
            <h2 id="roller-baslik">Her rol kendi ekranını görür</h2>
            <p className="ez-lead">Yetkiler hem arayüzde hem sunucuda uygulanır; kimse görmemesi gereken veriyi görmez.</p>
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
          <div className="ez-actions"><ButtonLink to="/guvenlik" variant="secondary">Yetkilendirme ve güvenlik</ButtonLink></div>
        </div>
      </section>

      <CtaBand />
    </>
  );
}
```

- [ ] **Step 4: `PricingPage.tsx`**

```tsx
import { ButtonLink, Badge, usePageMeta } from "../../ui";
import { comparisonRows, plans, pricingFaqs } from "../content";
import { CtaBand, FaqList, PageHero } from "../sections";

const cell = (value: boolean | string) =>
  typeof value === "string" ? value : value
    ? <><i aria-hidden="true" className="fa-solid fa-check ez-yes" /><span className="ez-sr-only">Var</span></>
    : <><i aria-hidden="true" className="fa-solid fa-minus ez-no" /><span className="ez-sr-only">Yok</span></>;

export function PricingPage() {
  usePageMeta("Fiyatlandırma", "Ekibinizin büyüklüğüne ve ihtiyacınız olan modüllere göre size özel teklif alın.");
  return (
    <>
      <PageHero
        eyebrow="Fiyatlandırma"
        title="İhtiyacınız kadar, sürprizsiz"
        lead="Fiyat; çalışan sayınıza ve seçtiğiniz modüllere göre belirlenir. Size özel teklifi kısa sürede iletiyoruz."
      />
      <section className="ez-section ez-section--flush" aria-label="Paketler">
        <div className="ez-container">
          <ul className="ez-plans">
            {plans.map((p) => (
              <li key={p.id} className={`ez-plan${p.highlighted ? " ez-plan--featured" : ""}`}>
                {p.highlighted && <Badge tone="primary">En çok tercih edilen</Badge>}
                <h2>{p.name}</h2>
                <p className="ez-plan__audience">{p.audience}</p>
                <p className="ez-plan__price">Size özel fiyat</p>
                <ButtonLink to={`/iletisim?paket=${p.id}`} variant={p.highlighted ? "primary" : "secondary"} block aria-label={`${p.name} paketi için teklif alın`}>
                  Teklif alın
                </ButtonLink>
                <ul className="ez-check-list">{p.features.map((f) => <li key={f}>{f}</li>)}</ul>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="karsilastirma-baslik">
        <div className="ez-container">
          <div className="ez-section__head"><h2 id="karsilastirma-baslik">Paketleri karşılaştırın</h2></div>
          <div className="ez-table-wrap">
            <table className="ez-compare">
              <caption className="ez-sr-only">Paket karşılaştırması</caption>
              <thead>
                <tr>
                  <th scope="col">Özellik</th>
                  {plans.map((p) => <th key={p.id} scope="col">{p.name}</th>)}
                </tr>
              </thead>
              <tbody>
                {comparisonRows.map((row) => (
                  <tr key={row.label}>
                    <th scope="row">{row.label}</th>
                    {row.values.map((v, i) => <td key={plans[i].id}>{cell(v)}</td>)}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="fiyat-sss-baslik">
        <div className="ez-container ez-narrow">
          <div className="ez-section__head"><h2 id="fiyat-sss-baslik">Fiyatlandırma hakkında sorular</h2></div>
          <FaqList items={pricingFaqs} />
        </div>
      </section>
      <CtaBand title="Size özel teklifi alın" text="Çalışan sayınızı ve ihtiyaçlarınızı paylaşın, net bir teklifle dönelim." />
    </>
  );
}
```

Testteki `/teklif alın/i` regex'i paket linklerinin aria-label'ını ("… paketi için teklif alın") yakalar. CtaBand'deki linkler ("Demo iste", "Paketleri gör") eşleşmez, bu yüzden sayı paket sayısına eşittir.

- [ ] **Step 5: `SecurityPage.tsx`**

```tsx
import { Link } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { securityPillars } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";

export function SecurityPage() {
  usePageMeta("Güvenlik ve KVKK", "Ekipzen'in veri izolasyonu, yetkilendirme, şifreleme ve KVKK yaklaşımı.");
  return (
    <>
      <PageHero
        eyebrow="Güvenlik"
        title="İK verisi için tasarlanmış güvenlik"
        lead="Ekipzen'i, bir şirketin en hassas verisini taşıyacağını bilerek; izolasyon ve en az yetki ilkeleriyle kurduk."
      />
      <section className="ez-section ez-section--flush" aria-label="Güvenceler">
        <div className="ez-container">
          <ul className="ez-grid-3">
            {securityPillars.map((p) => (
              <li key={p.title} className="ez-pillar">
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>
      <section className="ez-section ez-section--muted" aria-labelledby="kvkk-baslik">
        <div className="ez-container ez-narrow ez-prose">
          <h2 id="kvkk-baslik">KVKK ve kişisel veriler</h2>
          <p>
            Ekipzen, 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamındaki yükümlülüklerinizi yerine getirmenize
            yardımcı olacak şekilde tasarlanmıştır: erişim rol bazlıdır, veriler şirket başına ayrı tutulur ve her
            kullanıcı kendi kişisel verisini dışa aktarabilir.
          </p>
          <p>
            Ziyaretçi ve demo talebi verilerinin nasıl işlendiğini{" "}
            <Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link> sayfasında bulabilirsiniz.
          </p>
        </div>
      </section>
      <CtaBand title="Güvenlik sorularınızı konuşalım" text="Bilgi güvenliği ekibinizin sorularını demo görüşmesinde yanıtlayalım." />
    </>
  );
}
```

- [ ] **Step 6: `AboutPage.tsx`**

```tsx
import { usePageMeta } from "../../ui";
import { values } from "../content";
import { CtaBand, PageHero } from "../sections";

export function AboutPage() {
  usePageMeta("Hakkımızda", "Ekipzen'in neden var olduğu ve hangi değerlerle geliştirildiği.");
  return (
    <>
      <PageHero
        eyebrow="Hakkımızda"
        title="İK ekiplerinin işini kolaylaştırmak için buradayız"
        lead="Ekipzen, dağınık tablolar ve e-posta zincirleriyle yürüyen İK operasyonlarını tek, güvenilir bir platformda toplamak için doğdu."
      />
      <section className="ez-section ez-section--flush" aria-labelledby="misyon-baslik">
        <div className="ez-container ez-narrow ez-prose">
          <h2 id="misyon-baslik">Misyonumuz</h2>
          <p>
            İK ekiplerinin zamanını tekrar eden işlerden kurtarıp insanlara ayırmalarını sağlamak. Bunu yaparken
            çalışan verisini en yüksek özenle korumak ve Türkiye'nin mevzuatına baştan uyumlu bir ürün sunmak.
          </p>
          <h2>Neden Ekipzen?</h2>
          <p>
            Personel, izin, puantaj, bordro ve işe alım birbirinden kopuk araçlarda yönetildiğinde aynı bilgi defalarca
            girilir ve hatalar büyür. Ekipzen bu süreçleri tek veri modelinde birleştirir; risk sinyallerini de bu
            bütün resimden üretir.
          </p>
        </div>
      </section>
      <section className="ez-section ez-section--muted" aria-labelledby="degerler-baslik">
        <div className="ez-container">
          <div className="ez-section__head"><h2 id="degerler-baslik">Değerlerimiz</h2></div>
          <ul className="ez-grid-3">
            {values.map((v) => (
              <li key={v.title} className="ez-pillar"><h3>{v.title}</h3><p>{v.text}</p></li>
            ))}
          </ul>
        </div>
      </section>
      <CtaBand />
    </>
  );
}
```

- [ ] **Step 7: `KvkkPage.tsx`**

```tsx
import { Link } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { siteInfo } from "../content";

// İÇERİK: Bu metin genel bir çerçevedir; yayın öncesi hukuk danışmanı tarafından
// veri sorumlusu unvanı, adresi ve saklama süreleriyle kesinleştirilmelidir.
export function KvkkPage() {
  usePageMeta("KVKK aydınlatma metni", "Ekipzen web sitesi ve demo talep formu kapsamında kişisel verilerin işlenmesine ilişkin aydınlatma metni.");
  return (
    <section className="ez-section">
      <div className="ez-container ez-narrow ez-prose">
        <p className="ez-eyebrow">Yasal</p>
        <h1>KVKK aydınlatma metni</h1>
        <p>
          Bu metin, 6698 sayılı Kişisel Verilerin Korunması Kanunu'nun 10. maddesi uyarınca, Ekipzen web sitesini
          ziyaret eden ve demo talep formunu dolduran kişileri bilgilendirmek amacıyla hazırlanmıştır.
        </p>
        <h2>Veri sorumlusu</h2>
        <p>{siteInfo.legalName} ("Ekipzen"). İletişim: <a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a></p>
        <h2>İşlenen kişisel veriler</h2>
        <p>Demo talep formunda paylaştığınız ad soyad, iş e-posta adresi, şirket adı, çalışan sayısı aralığı, telefon numarası ve mesajınız.</p>
        <h2>İşleme amaçları ve hukuki sebep</h2>
        <p>
          Verileriniz; demo talebinize yanıt vermek, sizinle iletişime geçmek ve talep ettiğiniz teklifi hazırlamak
          amacıyla, KVKK m.5/2-c (bir sözleşmenin kurulmasıyla doğrudan ilgili olması) ve m.5/2-f (meşru menfaat)
          hukuki sebeplerine dayanılarak işlenir.
        </p>
        <h2>Aktarım</h2>
        <p>Verileriniz, talebinizi size iletmek için kullanılan e-posta hizmet sağlayıcısı dışında üçüncü kişilerle paylaşılmaz.</p>
        <h2>Toplama yöntemi</h2>
        <p>Verileriniz, web sitesindeki form aracılığıyla elektronik ortamda toplanır.</p>
        <h2>İlgili kişi olarak haklarınız</h2>
        <p>
          KVKK m.11 uyarınca verilerinizin işlenip işlenmediğini öğrenme, bilgi talep etme, düzeltilmesini veya
          silinmesini isteme ve itiraz etme haklarına sahipsiniz. Başvurularınızı{" "}
          <a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a> adresine iletebilirsiniz.
        </p>
        <p><Link to="/iletisim">İletişim sayfasına dön</Link></p>
      </div>
    </section>
  );
}
```

- [ ] **Step 8: Rotaları kaydet**

`routes.tsx` üst kısmına (diğer lazy tanımlarının yanına):

```tsx
const FeaturesPage = lazy(() => import("./marketing/pages/FeaturesPage").then((m) => ({ default: m.FeaturesPage })));
const PricingPage = lazy(() => import("./marketing/pages/PricingPage").then((m) => ({ default: m.PricingPage })));
const AboutPage = lazy(() => import("./marketing/pages/AboutPage").then((m) => ({ default: m.AboutPage })));
const SecurityPage = lazy(() => import("./marketing/pages/SecurityPage").then((m) => ({ default: m.SecurityPage })));
const KvkkPage = lazy(() => import("./marketing/pages/KvkkPage").then((m) => ({ default: m.KvkkPage })));

const marketingPage = (Page: ComponentType) => (
  <Suspense fallback={<div className="ez-page-loading" aria-busy="true" />}><Page /></Suspense>
);
```

MarketingLayout çocuklarına (`"/"` ile `"*"` arasına) eklenir:

```tsx
        { path: "/ozellikler", element: marketingPage(FeaturesPage) },
        { path: "/fiyatlandirma", element: marketingPage(PricingPage) },
        { path: "/hakkimizda", element: marketingPage(AboutPage) },
        { path: "/guvenlik", element: marketingPage(SecurityPage) },
        { path: "/kvkk-aydinlatma", element: marketingPage(KvkkPage) },
```

- [ ] **Step 9: Stiller** — `marketing.css` sonuna

```css
.ez-section--flush { padding-top: 0; }
.ez-anchor { scroll-margin-top: 88px; }
.ez-muted-note { font-size: var(--fs-14); color: var(--ez-text-4); }
.ez-page-loading { min-height: 60vh; }

.ez-chips { display: flex; flex-wrap: wrap; justify-content: center; gap: 0.5rem; margin-top: 1rem; }
.ez-chip { display: inline-flex; align-items: center; min-height: 40px; padding: 0 1rem; border-radius: var(--r-full); border: 1px solid var(--ez-border); background: var(--ez-surface); color: var(--ez-text-2); font-weight: 600; font-size: var(--fs-14); text-decoration: none; }
.ez-chip:hover { border-color: var(--indigo-200); color: var(--ez-primary); }
.ez-chip:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }

.ez-plans { list-style: none; display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 300px), 1fr)); gap: 1.25rem; align-items: stretch; }
.ez-plan { display: grid; align-content: start; gap: 1rem; padding: 2rem; border-radius: var(--r-xl); border: 1px solid var(--ez-border); background: var(--ez-surface); box-shadow: var(--ez-shadow-xs); }
.ez-plan--featured { border: 2px solid var(--ez-primary); box-shadow: var(--ez-shadow-lg); }
.ez-plan .ez-badge { justify-self: start; }
.ez-plan__audience { color: var(--ez-text-3); min-height: 3.3em; }
.ez-plan__price { font-size: var(--fs-24); font-weight: 800; color: var(--ez-text); }

.ez-table-wrap { overflow-x: auto; border: 1px solid var(--ez-border); border-radius: var(--r-lg); background: var(--ez-surface); }
.ez-compare { width: 100%; min-width: 640px; border-collapse: collapse; font-size: var(--fs-14); }
.ez-compare th, .ez-compare td { padding: 0.875rem 1.25rem; border-bottom: 1px solid var(--ez-border); text-align: center; }
.ez-compare th[scope="row"], .ez-compare thead th:first-child { text-align: left; font-weight: 500; color: var(--ez-text-2); }
.ez-compare thead th { font-weight: 700; color: var(--ez-text); background: var(--ez-bg); }
.ez-compare tr:last-child > * { border-bottom: 0; }
.ez-yes { color: var(--ez-success); }
.ez-no { color: var(--ez-text-4); }

.ez-prose { display: grid; gap: 1rem; }
.ez-prose h2 { font-size: var(--fs-24) !important; margin-top: 1rem; }
.ez-prose a { color: var(--ez-primary); font-weight: 600; }
.ez-prose a:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); border-radius: var(--r-sm); }
```

- [ ] **Step 10: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 11: Commit**

```bash
git add frontend/src/marketing frontend/src/routes.tsx frontend/src/styles/marketing.css
git commit -m "feat(landing): add features, pricing, about, security and KVKK pages

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: İletişim sayfası ve demo talep formu

**Files:**
- Create: `frontend/src/marketing/demoRequest.ts`
- Create: `frontend/src/marketing/DemoRequestForm.tsx`
- Create: `frontend/src/marketing/pages/ContactPage.tsx`
- Create: `frontend/src/marketing/DemoRequestForm.test.tsx`
- Modify: `frontend/src/routes.tsx` (`/iletisim`)
- Modify: `frontend/src/styles/marketing.css` (form düzeni)

**Interfaces:**
- Consumes: `apiFetch`, `ApiError` (`frontend/src/api/client.ts`); `Field`, `Input`, `Textarea`, `Select`, `Checkbox`, `Button`, `usePageMeta` (Task 2); `POST /api/public/demo-requests` (Task 5); `siteInfo`, `plans` (Task 6).
- Produces:
  - `demoRequest.ts`:
    - `DemoRequest` tipi: `{ fullName: string; email: string; company: string; companySize: string; phone: string; message: string; kvkkConsent: boolean; website: string }`
    - `COMPANY_SIZES`
    - `validateDemoRequest(r: DemoRequest): Partial<Record<keyof DemoRequest, string>>`
    - `submitDemoRequest(r: DemoRequest): Promise<void>`
    - `serverFieldErrors(error: unknown): Partial<Record<keyof DemoRequest, string>>`
  - `DemoRequestForm({ initialMessage?: string })`, `ContactPage`

- [ ] **Step 1: Failing test** — `frontend/src/marketing/DemoRequestForm.test.tsx`

```tsx
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { renderPage } from "../test/renderPage";
import { DemoRequestForm } from "./DemoRequestForm";

beforeEach(() => vi.stubGlobal("fetch", vi.fn()));
afterEach(() => vi.unstubAllGlobals());

const fillValid = async () => {
  await userEvent.type(screen.getByLabelText(/Ad soyad/), "Ayşe Demir");
  await userEvent.type(screen.getByLabelText(/İş e-postası/), "ayse@firma.example");
  await userEvent.type(screen.getByLabelText(/Şirket adı/), "Firma A.Ş.");
  await userEvent.selectOptions(screen.getByLabelText(/Çalışan sayısı/), "51-200");
  await userEvent.click(screen.getByRole("checkbox", { name: /KVKK aydınlatma metnini/ }));
};

test("geçerli form gönderilir ve teşekkür mesajı gösterilir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 204 }));
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));

  expect(await screen.findByRole("heading", { name: "Talebiniz bize ulaştı" })).toBeInTheDocument();
  const [url, init] = vi.mocked(fetch).mock.calls[0];
  expect(url).toBe("/api/public/demo-requests");
  expect(JSON.parse(init!.body as string)).toMatchObject({
    fullName: "Ayşe Demir", email: "ayse@firma.example", company: "Firma A.Ş.", companySize: "51-200", kvkkConsent: true, website: "",
  });
});

test("KVKK onayı ve zorunlu alanlar olmadan istek atılmaz", async () => {
  renderPage(<DemoRequestForm />);
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(fetch).not.toHaveBeenCalled();
  expect(screen.getByText("Ad soyad zorunludur.")).toBeInTheDocument();
  expect(screen.getByText("KVKK aydınlatma metnini onaylamanız gerekir.")).toBeInTheDocument();
  expect(screen.getByLabelText(/Ad soyad/)).toHaveFocus();
});

test("geçersiz e-posta istemcide yakalanır", async () => {
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.clear(screen.getByLabelText(/İş e-postası/));
  await userEvent.type(screen.getByLabelText(/İş e-postası/), "gecersiz");
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(fetch).not.toHaveBeenCalled();
  expect(screen.getByText("Geçerli bir e-posta adresi girin.")).toBeInTheDocument();
});

test("sunucu doğrulama hatası ilgili alanda gösterilir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response(
    JSON.stringify({ title: "Doğrulama hatası.", status: 400, errors: { Company: ["Şirket adı çok uzun."] } }),
    { status: 400, headers: { "Content-Type": "application/json" } },
  ));
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(await screen.findByText("Şirket adı çok uzun.")).toBeInTheDocument();
});

test("çok fazla istek ve beklenmeyen hata genel mesajla gösterilir", async () => {
  vi.mocked(fetch).mockResolvedValueOnce(new Response(null, { status: 429 }));
  renderPage(<DemoRequestForm />);
  await fillValid();
  await userEvent.click(screen.getByRole("button", { name: "Demo talebini gönder" }));
  expect(await screen.findByText(/Çok fazla talep gönderildi/)).toBeInTheDocument();
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing/DemoRequestForm.test.tsx`
Expected: FAIL (modül yok)

- [ ] **Step 3: `frontend/src/marketing/demoRequest.ts`**

```ts
import { ApiError, apiFetch } from "../api/client";

export type DemoRequest = {
  fullName: string;
  email: string;
  company: string;
  companySize: string;
  phone: string;
  message: string;
  kvkkConsent: boolean;
  /** Bal küpü: görünmez alan; bot doldurursa sunucu talebi sessizce yok sayar. */
  website: string;
};

export type DemoRequestErrors = Partial<Record<keyof DemoRequest, string>>;

// Sunucudaki SubmitDemoRequestCommandValidator.CompanySizes ile aynı.
export const COMPANY_SIZES = ["1-10", "11-50", "51-200", "201-500", "500+"] as const;

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function validateDemoRequest(r: DemoRequest): DemoRequestErrors {
  const errors: DemoRequestErrors = {};
  if (!r.fullName.trim()) errors.fullName = "Ad soyad zorunludur.";
  else if (r.fullName.length > 100) errors.fullName = "Ad soyad en fazla 100 karakter olabilir.";
  if (!r.email.trim()) errors.email = "E-posta zorunludur.";
  else if (!EMAIL.test(r.email.trim())) errors.email = "Geçerli bir e-posta adresi girin.";
  if (!r.company.trim()) errors.company = "Şirket adı zorunludur.";
  else if (r.company.length > 150) errors.company = "Şirket adı en fazla 150 karakter olabilir.";
  if (!(COMPANY_SIZES as readonly string[]).includes(r.companySize)) errors.companySize = "Geçerli bir çalışan aralığı seçin.";
  if (r.phone.length > 30) errors.phone = "Telefon en fazla 30 karakter olabilir.";
  if (r.message.length > 2000) errors.message = "Mesaj en fazla 2000 karakter olabilir.";
  if (!r.kvkkConsent) errors.kvkkConsent = "KVKK aydınlatma metnini onaylamanız gerekir.";
  return errors;
}

export async function submitDemoRequest(r: DemoRequest): Promise<void> {
  await apiFetch<void>("/public/demo-requests", {
    method: "POST",
    body: JSON.stringify({
      ...r,
      fullName: r.fullName.trim(),
      email: r.email.trim(),
      company: r.company.trim(),
      phone: r.phone.trim() || null,
      message: r.message.trim() || null,
    }),
  });
}

/** ValidationProblemDetails anahtarları PascalCase gelir (FullName) → form alanına çevrilir. */
export function serverFieldErrors(error: unknown): DemoRequestErrors {
  if (!(error instanceof ApiError) || !error.problem?.errors) return {};
  const result: DemoRequestErrors = {};
  for (const [key, messages] of Object.entries(error.problem.errors)) {
    const field = (key.charAt(0).toLowerCase() + key.slice(1)) as keyof DemoRequest;
    if (messages[0]) result[field] = messages[0];
  }
  return result;
}
```

- [ ] **Step 4: `frontend/src/marketing/DemoRequestForm.tsx`**

```tsx
import { useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { ApiError } from "../api/client";
import { Button, Checkbox, Field, Input, Select, Textarea } from "../ui";
import {
  COMPANY_SIZES, serverFieldErrors, submitDemoRequest, validateDemoRequest,
  type DemoRequest, type DemoRequestErrors,
} from "./demoRequest";

const FIELD_ORDER: (keyof DemoRequest)[] = ["fullName", "email", "company", "companySize", "phone", "message", "kvkkConsent"];

export function DemoRequestForm({ initialMessage = "" }: { initialMessage?: string }) {
  const [values, setValues] = useState<DemoRequest>({
    fullName: "", email: "", company: "", companySize: "", phone: "", message: initialMessage, kvkkConsent: false, website: "",
  });
  const [errors, setErrors] = useState<DemoRequestErrors>({});
  const [formError, setFormError] = useState<string | null>(null);
  const formRef = useRef<HTMLFormElement>(null);

  const mutation = useMutation({
    mutationFn: submitDemoRequest,
    onError: (error) => {
      const fieldErrors = serverFieldErrors(error);
      setErrors(fieldErrors);
      if (Object.keys(fieldErrors).length > 0) return;
      setFormError(error instanceof ApiError && error.status === 429
        ? "Çok fazla talep gönderildi. Lütfen bir süre sonra tekrar deneyin."
        : "Talebiniz şu anda gönderilemedi. Lütfen tekrar deneyin ya da bize e-postayla ulaşın.");
    },
  });

  const set = <K extends keyof DemoRequest>(key: K, value: DemoRequest[K]) => {
    setValues((v) => ({ ...v, [key]: value }));
    if (errors[key]) setErrors((e) => ({ ...e, [key]: undefined }));
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);
    const found = validateDemoRequest(values);
    setErrors(found);
    const first = FIELD_ORDER.find((key) => found[key]);
    if (first) {
      formRef.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus();
      return;
    }
    mutation.mutate(values);
  };

  if (mutation.isSuccess) {
    return (
      <div className="ez-form-success" role="status">
        <span className="ez-icon"><i aria-hidden="true" className="fa-solid fa-check" /></span>
        <h2>Talebiniz bize ulaştı</h2>
        <p>Teşekkürler {values.fullName.split(" ")[0]}. Ekibimiz en kısa sürede {values.email} adresinden sizinle iletişime geçecek.</p>
        <Link to="/ozellikler" className="ez-link">Bu arada özellikleri inceleyin</Link>
      </div>
    );
  }

  return (
    <form ref={formRef} className="ez-form" onSubmit={submit} noValidate aria-describedby="demo-form-note">
      <p id="demo-form-note" className="ez-form__note">* ile işaretli alanlar zorunludur.</p>
      <div className="ez-form__grid">
        <Field label="Ad soyad" required error={errors.fullName}>
          <Input name="fullName" autoComplete="name" value={values.fullName} onChange={(e) => set("fullName", e.target.value)} />
        </Field>
        <Field label="İş e-postası" required error={errors.email}>
          <Input name="email" type="email" autoComplete="email" inputMode="email" placeholder="ad@sirket.com" value={values.email} onChange={(e) => set("email", e.target.value)} />
        </Field>
        <Field label="Şirket adı" required error={errors.company}>
          <Input name="company" autoComplete="organization" value={values.company} onChange={(e) => set("company", e.target.value)} />
        </Field>
        <Field label="Çalışan sayısı" required error={errors.companySize}>
          <Select name="companySize" value={values.companySize} onChange={(e) => set("companySize", e.target.value)}>
            <option value="">Seçin</option>
            {COMPANY_SIZES.map((size) => <option key={size} value={size}>{size} çalışan</option>)}
          </Select>
        </Field>
        <Field label="Telefon" hint="İsteğe bağlı" error={errors.phone}>
          <Input name="phone" type="tel" autoComplete="tel" inputMode="tel" value={values.phone} onChange={(e) => set("phone", e.target.value)} />
        </Field>
      </div>
      <Field label="Mesajınız" hint="İsteğe bağlı — hangi süreçleri görmek istediğinizi yazabilirsiniz." error={errors.message}>
        <Textarea name="message" rows={4} value={values.message} onChange={(e) => set("message", e.target.value)} />
      </Field>
      <div className="ez-honeypot" aria-hidden="true">
        <label htmlFor="demo-website">Web siteniz</label>
        <input id="demo-website" name="website" tabIndex={-1} autoComplete="off" value={values.website} onChange={(e) => set("website", e.target.value)} />
      </div>
      <div className={errors.kvkkConsent ? "ez-field ez-field--error" : "ez-field"}>
        <Checkbox
          name="kvkkConsent"
          checked={values.kvkkConsent}
          onChange={(e) => set("kvkkConsent", e.target.checked)}
          aria-invalid={errors.kvkkConsent ? true : undefined}
          aria-describedby={errors.kvkkConsent ? "kvkk-error" : undefined}
        >
          <Link to="/kvkk-aydinlatma" target="_blank" rel="noopener">KVKK aydınlatma metnini</Link> okudum; talebime yanıt verilmesi için verilerimin işlenmesini kabul ediyorum. *
        </Checkbox>
        {errors.kvkkConsent && <p id="kvkk-error" className="ez-field__error" role="alert">{errors.kvkkConsent}</p>}
      </div>
      {formError && <p className="ez-form__error" role="alert">{formError}</p>}
      <Button type="submit" size="lg" disabled={mutation.isPending}>
        {mutation.isPending ? "Gönderiliyor…" : "Demo talebini gönder"}
      </Button>
    </form>
  );
}
```

Not: `Checkbox` etiketi bir link içerdiği için erişilebilir adı "KVKK aydınlatma metnini okudum; …" olur. Testteki `/KVKK aydınlatma metnini/` bununla eşleşir.

- [ ] **Step 5: `frontend/src/marketing/pages/ContactPage.tsx`**

```tsx
import { useSearchParams } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { plans, siteInfo } from "../content";
import { DemoRequestForm } from "../DemoRequestForm";

export function ContactPage() {
  usePageMeta("Demo talebi ve iletişim", "Ekipzen'i ekibinizle birlikte görmek için demo talep edin.");
  const [params] = useSearchParams();
  const plan = plans.find((p) => p.id === params.get("paket"));
  const initialMessage = plan ? `${plan.name} paketi için teklif almak istiyorum.` : "";

  return (
    <section className="ez-section ez-contact">
      <div className="ez-container ez-contact__grid">
        <div className="ez-contact__intro">
          <p className="ez-eyebrow">Demo talebi</p>
          <h1>Ekipzen'i birlikte keşfedelim</h1>
          <p className="ez-lead">Formu doldurun; süreçlerinizi dinleyip ürünü size göre gösterelim ve net bir teklif hazırlayalım.</p>
          <ul className="ez-check-list">
            <li>30 dakikalık, size özel canlı tanıtım</li>
            <li>Süreçlerinize göre modül önerisi</li>
            <li>Şirketinize özel, izole ortamda kurulum</li>
          </ul>
          <div className="ez-contact__card">
            <h2>Doğrudan ulaşın</h2>
            <p><i aria-hidden="true" className="fa-solid fa-envelope" /> <a href={`mailto:${siteInfo.email}`}>{siteInfo.email}</a></p>
            <p className="ez-muted-note">Zaten müşterimiz misiniz? Destek taleplerinizi uygulama içinden ya da bu adresten iletebilirsiniz.</p>
          </div>
        </div>
        <div className="ez-contact__form ez-card">
          <DemoRequestForm key={initialMessage} initialMessage={initialMessage} />
        </div>
      </div>
    </section>
  );
}
```

HomePage testindeki `/müşterimiz|bize güvenen/i` kontrolü yalnız HomePage'e uygulanır; buradaki "Zaten müşterimiz misiniz?" metni onu etkilemez.

`routes.tsx`: lazy tanımı ve MarketingLayout çocuğu eklenir:

```tsx
const ContactPage = lazy(() => import("./marketing/pages/ContactPage").then((m) => ({ default: m.ContactPage })));
// …
        { path: "/iletisim", element: marketingPage(ContactPage) },
```

- [ ] **Step 6: Stiller** — `marketing.css` sonuna

```css
.ez-contact { background: radial-gradient(50rem 24rem at 0% 0%, var(--indigo-50), transparent 70%), var(--ez-surface); }
.ez-contact__grid { display: grid; grid-template-columns: 0.9fr 1.1fr; gap: clamp(2rem, 5vw, 4rem); align-items: start; }
.ez-contact__intro { display: grid; gap: 1.25rem; }
.ez-contact__card { margin-top: 0.5rem; padding: 1.25rem; border-radius: var(--r-lg); background: var(--ez-bg); display: grid; gap: 0.5rem; }
.ez-contact__card h2 { font-size: var(--fs-16) !important; }
.ez-contact__card a, .ez-link { color: var(--ez-primary); font-weight: 600; }
.ez-contact__form { padding: clamp(1.5rem, 4vw, 2.5rem); box-shadow: var(--ez-shadow-lg); }
@media (max-width: 900px) { .ez-contact__grid { grid-template-columns: 1fr; } }

.ez-form { display: grid; gap: 1.25rem; }
.ez-form__grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 240px), 1fr)); gap: 1.25rem; }
.ez-form__note { font-size: var(--fs-12); color: var(--ez-text-3); }
.ez-form__error { padding: 0.75rem 1rem; border-radius: var(--r-md); background: var(--ez-danger-soft); color: var(--ez-danger-strong); font-weight: 500; }
.ez-form .ez-check a { color: var(--ez-primary); font-weight: 600; }
.ez-honeypot { position: absolute; left: -10000px; width: 1px; height: 1px; overflow: hidden; }
.ez-form-success { display: grid; gap: 1rem; justify-items: start; padding-block: 2rem; }
.ez-form-success .ez-icon { background: var(--ez-success-soft); color: var(--ez-success); }
```

- [ ] **Step 7: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 8: Uçtan uca kontrol**

`ekipzen-api` ve `ekipzen-frontend` birlikte çalıştırılır. `/iletisim?paket=buyuyen` adresinde mesaj alanının önceden dolu geldiği görülür. Form gönderilir ve teşekkür ekranı açılır. Dev outbox'ta (`backend/src/Ekipzen.API/App_Data/storage/outbox`, `Storage:Root` ayarına göre) `demo-talepleri@ekipzen.local` alıcılı JSON dosyası bulunur.

- [ ] **Step 9: Commit**

```bash
git add frontend/src/marketing frontend/src/routes.tsx frontend/src/styles/marketing.css
git commit -m "feat(landing): add contact page with demo request form

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: Giriş ve davet ekranlarının yeniden tasarımı

**Files:**
- Create: `frontend/src/auth/AuthLayout.tsx`
- Modify: `frontend/src/auth/LoginPage.tsx`, `frontend/src/auth/AcceptInvitePage.tsx`
- Modify (tamamen yeniden yazılır): `frontend/src/styles/auth.css`
- Test: `frontend/src/auth/LoginPage.test.tsx`, `frontend/src/auth/AcceptInvitePage.test.tsx`

**Interfaces:**
- Consumes: `Field`, `Input`, `Button`, `Badge`, `Logo`, `usePageMeta` (Task 2); `LOGIN_PATH` (Task 3); `RiskScoreCard`, `PayrollSummaryCard` (Task 6).
- Produces: `AuthLayout({ title: string; subtitle: ReactNode; badge?: ReactNode; children: ReactNode })`
- Korunan davranış:
  - Form id'si `auth-login`, input id'leri `login-email` / `login-password` / `invite-password` / `invite-confirm`.
  - Dev'de demo bilgileri öndoludur; "Demo erişim" / "Şirket hesabı" rozeti gösterilir.
  - Hata `role="alert"` ile gösterilir; başarılı girişte `roleHomeFor` rotasına gidilir.

- [ ] **Step 1: Testleri yeni tasarıma göre güncelle (failing)**

`LoginPage.test.tsx` içindeki `"kayıt sekmesi yok"` testi şu hale gelir:

```tsx
test("kayıt sekmesi yok; hesabı olmayan demo talebine yönlendirilir", () => {
  renderLogin();
  expect(document.getElementById("auth-login")).toHaveClass("ez-auth__form");
  expect(document.getElementById("auth-signup")).toBeNull();
  expect(screen.getByRole("link", { name: "Demo isteyin" })).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: /Ana sayfaya dön/ })).toHaveAttribute("href", "/");
});
```

Dosya sonuna eklenir:

```tsx
test("giriş ekranı sayfa başlığını ve erişilebilir başlığı ayarlar", () => {
  renderLogin();
  expect(screen.getByRole("heading", { level: 1, name: "Hesabınıza giriş yapın" })).toBeInTheDocument();
  expect(document.title).toBe("Giriş yap · Ekipzen");
});

test("gönderim sırasında buton devre dışı kalır", async () => {
  let resolve!: (r: Response) => void;
  vi.mocked(fetch).mockReturnValueOnce(new Promise<Response>((r) => { resolve = r; }));
  renderLogin();
  await userEvent.click(screen.getByRole("button", { name: /Giriş yap/ }));
  expect(screen.getByRole("button", { name: /Giriş yapılıyor/ })).toBeDisabled();
  resolve(new Response(JSON.stringify(authBody), { status: 200, headers: { "Content-Type": "application/json" } }));
});
```

`AcceptInvitePage.test.tsx` sonuna:

```tsx
test("davet ekranı yeni düzende başlık gösterir", () => {
  renderInvite("?email=yeni%40acme.local&token=abc");
  expect(screen.getByRole("heading", { level: 1, name: "Hesabınızı etkinleştirin" })).toBeInTheDocument();
  expect(screen.getByLabelText("Yeni şifre")).toHaveAttribute("id", "invite-password");
});
```

Run: `npm --prefix frontend test -- --run src/auth`
Expected: FAIL

- [ ] **Step 2: `frontend/src/auth/AuthLayout.tsx`**

```tsx
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Logo } from "../ui";
import { PayrollSummaryCard, RiskScoreCard } from "../marketing/visuals";

export function AuthLayout({ title, subtitle, badge, children }: {
  title: string;
  subtitle: ReactNode;
  badge?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="ez-auth ez-light">
      <main className="ez-auth__main">
        <div className="ez-auth__top">
          <Link to="/" aria-label="Ekipzen ana sayfa"><Logo /></Link>
          <Link to="/" className="ez-auth__back"><i aria-hidden="true" className="fa-solid fa-arrow-left" /> Ana sayfaya dön</Link>
        </div>
        <div className="ez-auth__panel">
          {badge}
          <h1>{title}</h1>
          <p className="ez-auth__subtitle">{subtitle}</p>
          {children}
        </div>
        <p className="ez-auth__legal">
          <Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link> · <Link to="/guvenlik">Güvenlik</Link>
        </p>
      </main>
      <aside className="ez-auth__aside" aria-label="Ekipzen hakkında">
        <div className="ez-auth__aside-inner">
          <p className="ez-auth__quote">Personel, izin, puantaj ve bordro — tek, güvenli platformda.</p>
          <ul className="ez-auth__points">
            <li><i aria-hidden="true" className="fa-solid fa-database" /> Şirketinize özel izole veritabanı</li>
            <li><i aria-hidden="true" className="fa-solid fa-user-lock" /> Rol bazlı yetkilendirme</li>
            <li><i aria-hidden="true" className="fa-solid fa-scale-balanced" /> KVKK odaklı tasarım</li>
          </ul>
          <div className="ez-auth__visual" aria-hidden="true">
            <RiskScoreCard />
            <PayrollSummaryCard />
          </div>
        </div>
      </aside>
    </div>
  );
}
```

- [ ] **Step 3: `LoginPage.tsx`**

```tsx
import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiError } from "../api/client";
import { useAuth } from "./AuthContext";
import { roleHomeFor } from "../paths";
import { Badge, Button, Field, Input, usePageMeta } from "../ui";
import { AuthLayout } from "./AuthLayout";

export function LoginPage() {
  usePageMeta("Giriş yap", "Ekipzen hesabınıza giriş yapın.");
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState(import.meta.env.DEV ? "ik@hrmaster.local" : "");
  const [password, setPassword] = useState(import.meta.env.DEV ? "demo123" : "");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const user = await login(email, password);
      navigate(roleHomeFor(user?.role));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Beklenmeyen bir hata oluştu.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout
      title="Hesabınıza giriş yapın"
      subtitle={import.meta.env.DEV ? "Demo hesabıyla uygulamayı inceleyebilirsiniz." : "Kurumsal e-posta adresiniz ve şifrenizle devam edin."}
      badge={<Badge tone="primary">{import.meta.env.DEV ? "Demo erişim" : "Şirket hesabı"}</Badge>}
    >
      {/* Bu ekranda KAYIT YOKTUR — ne kullanıcı ne şirket. Hesap açmanın tek yolu
          operatörün platform anahtarıyla kiracı provizyonlaması, ardından gelen
          davet e-postasıdır (/davet). Kiracı başına ayrı veritabanında "kaydol" bir
          VERİTABANI oluşturur; anonim bir düğmenin bunu tetiklemesi kaynak tüketme açığıdır. */}
      <form id="auth-login" className="ez-auth__form" onSubmit={submit}>
        <Field label="E-posta" id="login-email">
          <Input type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
        </Field>
        <Field label="Şifre" id="login-password">
          <Input type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
        </Field>
        {error && <p className="ez-auth__error" role="alert">{error}</p>}
        <Button type="submit" size="lg" block disabled={busy}>
          {busy ? "Giriş yapılıyor…" : "Giriş yap"}
        </Button>
      </form>
      <p className="ez-auth__alt">
        Şirketinizin hesabı yok mu? <Link to="/iletisim">Demo isteyin</Link>
        <br />
        Hesabınız varsa davet e-postanızdaki bağlantıyla şifrenizi belirleyin.
      </p>
    </AuthLayout>
  );
}
```

Mevcut testteki "başarılı girişte oturum saklanır" `form#auth-login button[type='submit']` seçicisini kullanır; bu seçici çalışmaya devam eder.

- [ ] **Step 4: `AcceptInvitePage.tsx`**

Mantık (state, `submit`, `missingLink`, `minimumPasswordLength`) aynen korunur. Dosyanın xmldoc'undaki `(/#/accept-invite?email=...&token=...)` ifadesi `(/davet?email=...&token=...)` olur. `return` bloğu şu hale gelir:

```tsx
  return (
    <AuthLayout
      title="Hesabınızı etkinleştirin"
      subtitle={missingLink ? "Davet bağlantınızı kontrol edin." : <>{email} için bir şifre belirleyin.</>}
      badge={<Badge tone="primary">Hesap etkinleştirme</Badge>}
    >
      <form className="ez-auth__form" onSubmit={submit}>
        {missingLink ? (
          <p className="ez-auth__error" role="alert">
            Davet bağlantısı geçersiz veya eksik. Lütfen e-postanızdaki bağlantıyı yeniden açın.
          </p>
        ) : (
          <>
            {!import.meta.env.DEV && <p className="ez-auth__hint">En az 12 karakter, büyük harf ve rakam kullanın.</p>}
            <Field label="Yeni şifre" id="invite-password">
              <Input type="password" autoComplete="new-password" value={password} onChange={(e) => setPassword(e.target.value)} />
            </Field>
            <Field label="Şifre (tekrar)" id="invite-confirm">
              <Input type="password" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} />
            </Field>
            {error && <p className="ez-auth__error" role="alert">{error}</p>}
            <Button type="submit" size="lg" block disabled={busy}>
              {busy ? "Etkinleştiriliyor…" : "Şifreyi belirle ve giriş yap"}
            </Button>
          </>
        )}
      </form>
    </AuthLayout>
  );
```

Importlara eklenir: `import { Badge, Button, Field, Input, usePageMeta } from "../ui";` ve `import { AuthLayout } from "./AuthLayout";`. Fonksiyonun başına `usePageMeta("Hesap etkinleştirme");` eklenir. Mevcut testler şifre alanını `getElementById("invite-password")` ile bulduğu için id'ler korunur.

- [ ] **Step 5: `frontend/src/styles/auth.css`** (tamamı)

```css
/* Giriş ve davet ekranları — yalnız --ez-* token'ları. */
.ez-auth { min-height: 100vh; display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); background: var(--ez-surface); font-family: var(--ez-font-sans); color: var(--ez-text-2); }
.ez-auth__main { display: flex; flex-direction: column; gap: 2rem; padding: clamp(1.5rem, 4vw, 3rem); }
.ez-auth__top { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
.ez-auth__top a { border-radius: var(--r-md); }
.ez-auth__top a:focus-visible, .ez-auth__legal a:focus-visible, .ez-auth__alt a:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-auth__back { display: inline-flex; align-items: center; gap: 0.5rem; min-height: 44px; padding: 0 0.5rem; color: var(--ez-text-3); font-weight: 600; font-size: var(--fs-14); text-decoration: none; }
.ez-auth__back:hover { color: var(--ez-text); }
.ez-auth__panel { width: 100%; max-width: 420px; margin: auto; display: grid; gap: 1rem; }
.ez-auth__panel .ez-badge { justify-self: start; }
.ez-auth__panel h1 { font-size: var(--fs-30); line-height: 1.2; font-weight: 800; letter-spacing: -0.02em; color: var(--ez-text); }
.ez-auth__subtitle { color: var(--ez-text-3); margin-bottom: 0.5rem; }
.ez-auth__form { display: grid; gap: 1.125rem; }
.ez-auth__error { padding: 0.75rem 1rem; border-radius: var(--r-md); background: var(--ez-danger-soft); color: var(--ez-danger-strong); font-weight: 500; font-size: var(--fs-14); }
.ez-auth__hint { font-size: var(--fs-14); color: var(--ez-text-3); }
.ez-auth__alt { font-size: var(--fs-14); color: var(--ez-text-3); line-height: 1.6; margin-top: 0.5rem; }
.ez-auth__alt a, .ez-auth__legal a { color: var(--ez-primary); font-weight: 600; border-radius: var(--r-sm); }
.ez-auth__legal { font-size: var(--fs-12); color: var(--ez-text-4); text-align: center; }

.ez-auth__aside { position: relative; overflow: hidden; display: flex; align-items: center; padding: clamp(2rem, 5vw, 4rem); background:
  radial-gradient(36rem 24rem at 100% 0%, rgb(129 140 248 / 0.35), transparent 60%),
  linear-gradient(160deg, var(--indigo-800), var(--indigo-950)); color: var(--indigo-100); }
.ez-auth__aside-inner { width: 100%; max-width: 480px; margin: auto; display: grid; gap: 1.75rem; }
.ez-auth__quote { font-size: var(--fs-30); line-height: 1.25; font-weight: 700; color: var(--slate-0); letter-spacing: -0.02em; }
.ez-auth__points { list-style: none; display: grid; gap: 0.75rem; }
.ez-auth__points i { width: 1.5rem; color: var(--indigo-300); }
.ez-auth__visual { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }

@media (max-width: 960px) {
  .ez-auth { grid-template-columns: 1fr; }
  .ez-auth__aside { display: none; }
}
```

Kontrol: eski `.auth-*` sınıflarının başka yerde kullanılmadığı doğrulanır.

Run: `grep -rn "auth-shell\|auth-visual\|auth-panel\|auth-form\|auth-submit" frontend/src --include=*.tsx`
Expected: çıktı yok

- [ ] **Step 6: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 7: Görsel kontrol**

`/giris` ve `/davet?email=a%40b.c&token=x` adreslerinde 1280 ve 375 genişliklerinde ekran görüntüsü alınır. Demo hesapla giriş yapılınca `/app/dashboard` açılmalıdır.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/auth frontend/src/styles/auth.css
git commit -m "feat(auth): redesign login and invite screens with new identity

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 10: Uygulama kabuğu, statik varlıklar, doküman ve bitiş doğrulaması

**Files:**
- Modify: `frontend/src/layout/AppShell.tsx:69-75` (marka bloğu)
- Modify: `frontend/src/styles/layout.css` (`.brand-mark` kuralları)
- Create: `frontend/public/favicon.svg`, `frontend/public/robots.txt`, `frontend/public/sitemap.xml`
- Modify: `frontend/index.html` (favicon, description, OG, theme-color)
- Test: `frontend/src/layout/AppShell.test.tsx`, `frontend/src/test/staticAssets.test.ts` (yeni)
- Modify: `docs/rehberler/04-frontend-derinlemesine.md`

**Interfaces:**
- Consumes: `Logo` (Task 2), tüm önceki görevler.

- [ ] **Step 1: Failing testler**

`AppShell.test.tsx` sonuna:

```tsx
test("kabuk yeni Ekipzen logosunu ve kiracı adını gösterir", async () => {
  renderShellAt("/app/overview", "employee", "Ahmet Yılmaz");
  const sidebar = document.querySelector("aside.sidebar") as HTMLElement;
  expect(within(sidebar).getByRole("img", { name: "Ekipzen" })).toBeInTheDocument();
  expect(sidebar.querySelector(".fa-users-gear")).toBeNull();
});
```

(`within` importu `@testing-library/react`'ten eklenir.)

`frontend/src/test/staticAssets.test.ts`:

```ts
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "vitest";

const root = join(__dirname, "..", "..");
const read = (p: string) => readFileSync(join(root, p), "utf8");

test("index.html favicon, açıklama ve OG etiketlerini içerir", () => {
  const html = read("index.html");
  expect(html).toContain('rel="icon" type="image/svg+xml" href="/favicon.svg"');
  expect(html).toContain('name="description"');
  expect(html).toContain('property="og:title"');
  expect(html).toContain('name="theme-color" content="#4338ca"');
});

test("robots.txt uygulama yollarını dizinden çıkarır ve sitemap'i gösterir", () => {
  const robots = read("public/robots.txt");
  expect(robots).toContain("Disallow: /app/");
  expect(robots).toContain("Disallow: /davet");
  expect(robots).toMatch(/Sitemap: https:\/\/.+\/sitemap\.xml/);
});

test("sitemap tüm herkese açık sayfaları listeler", () => {
  const sitemap = read("public/sitemap.xml");
  for (const path of ["/", "/ozellikler", "/fiyatlandirma", "/hakkimizda", "/guvenlik", "/iletisim", "/kvkk-aydinlatma"]) {
    expect(sitemap).toContain(`<loc>https://ekipzen.com${path}</loc>`);
  }
});
```

Run: `npm --prefix frontend test -- --run src/layout src/test/staticAssets.test.ts`
Expected: FAIL

- [ ] **Step 2: Kabuk markası**

`AppShell.tsx` marka bloğu:

```tsx
        <div className="brand">
          <Logo variant="mark" tone="light" />
          <div>
            <strong>Ekipzen</strong>
            <span>{user?.tenantName || "İK platformu"}</span>
          </div>
        </div>
```

`import { Logo } from "../ui";` eklenir. `layout.css` içindeki `.brand-mark` kuralları kaldırılır. Varsa `.sidebar-collapsed .brand-mark` seçicileri `.sidebar-collapsed .ez-logo` olarak yeniden adlandırılır.

Run: `grep -rn "brand-mark" frontend/src`
Expected: çıktı yok

- [ ] **Step 3: Statik varlıklar**

`frontend/public/favicon.svg` (statik dosya olduğu için sabit hex kullanılır; Global Constraints istisnası):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="9" fill="#4338ca"/><path d="M10 9.5h12M10 16h8M10 22.5h12" stroke="#fff" stroke-width="3" stroke-linecap="round" fill="none"/><circle cx="23" cy="16" r="2.2" fill="#a5b4fc"/></svg>
```

`frontend/public/robots.txt`:

```text
User-agent: *
Allow: /
Disallow: /app/
Disallow: /giris
Disallow: /davet

Sitemap: https://ekipzen.com/sitemap.xml
```

`frontend/public/sitemap.xml`:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url><loc>https://ekipzen.com/</loc></url>
  <url><loc>https://ekipzen.com/ozellikler</loc></url>
  <url><loc>https://ekipzen.com/fiyatlandirma</loc></url>
  <url><loc>https://ekipzen.com/hakkimizda</loc></url>
  <url><loc>https://ekipzen.com/guvenlik</loc></url>
  <url><loc>https://ekipzen.com/iletisim</loc></url>
  <url><loc>https://ekipzen.com/kvkk-aydinlatma</loc></url>
</urlset>
```

Domain `ekipzen.com` varsayımdır. Kullanıcının gerçek domaini farklıysa Task 10 raporunda belirtilir ve bu iki dosyada güncellenir.

`frontend/index.html` `<head>` şu hale gelir (font ve Font Awesome linkleri korunur):

```html
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Ekipzen</title>
    <meta name="description" content="Ekipzen ile personel, izin, puantaj, bordro ve işe alım süreçlerinizi tek, güvenli platformda yönetin." />
    <meta name="theme-color" content="#4338ca" />
    <meta property="og:type" content="website" />
    <meta property="og:site_name" content="Ekipzen" />
    <meta property="og:title" content="Ekipzen — Ekibinizi tek yerden, güvenle yönetin" />
    <meta property="og:description" content="Personel, izin, puantaj, bordro ve işe alım tek platformda." />
    <meta property="og:locale" content="tr_TR" />
    <link rel="icon" type="image/svg+xml" href="/favicon.svg" />
```

- [ ] **Step 4: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 5: Uygulama içi görsel regresyon turu**

`ekipzen-api` ve `ekipzen-frontend` çalıştırılır; demo hesapla giriş yapılır. Şu sayfalar açık ve koyu temada gezilir: `/app/dashboard`, `/app/overview`, `/app/actions`, `/app/personnel`, `/app/recruitment`, `/app/attendance`, `/app/leaves`, `/app/payroll`, `/app/manager`, `/app/settings`.

Her sayfada kontrol edilecekler:
- okunmayan metin yok (kontrast)
- eski teal renk kalmamış
- sidebar aktif öğesi indigo
- konsolda hata yok

Bulunan sorunlar yalnız ilgili CSS dosyasında token kullanılarak düzeltilir; yapı değiştirilmez (yapısal revizyon Spec 2'dedir). Sidebar açık ve daraltılmış hali ekran görüntüsüyle belgelenir.

- [ ] **Step 6: Dokümantasyon**

`docs/rehberler/04-frontend-derinlemesine.md` dosyasına "Tasarım sistemi ve yönlendirme (2026-09)" başlıklı bölüm eklenir. İçeriği:
- token katmanları (`tokens.css` ramp → `--ez-*` → `main.css` alias'ları; yeni kod `--ez-*` kullanır)
- `frontend/src/ui/` bileşenleri ve `ez-` sınıf öneki
- browser router: landing kökte, uygulama `/app/*` altında; `legacyHash.ts` eski `#/` ve kök yolları çevirir
- landing içeriğinin `frontend/src/marketing/content.ts`'ten düzenlendiği
- demo formunun `POST /api/public/demo-requests` → `DemoRequests:NotifyEmail` akışı
- backend `PilotHosting.MapSpaFallback`

Router ya da renk bilgisi içeren eski ifadeler (hash router, petrol/evergreen, IBM Plex) güncellenir.

Run: `grep -rn "createHashRouter\|IBM Plex\|petrol" docs/rehberler frontend/src`
Expected: çıktı yok (tarihsel spec/plan dosyaları hariç)

- [ ] **Step 7: Tam doğrulama**

Run: `npm --prefix frontend run lint && npm --prefix frontend test -- --run && npm --prefix frontend run build`
Expected: hata yok, tüm testler PASS

Run: `dotnet test backend/Ekipzen.sln`
Expected: tüm testler PASS

Tarayıcıda (in-app browser, `ekipzen-api` + `ekipzen-frontend`):
1. `/` landing açılır; menüdeki her link doğru sayfayı açar; 375px'te mobil menü açılıp kapanır.
2. `/iletisim` formu gönderilir → teşekkür ekranı; outbox'ta e-posta vardır.
3. `/giris` → demo girişi → `/app/dashboard`.
4. `/#/accept-invite?email=a%40b.c&token=x` → `/davet?email=a%40b.c&token=x`.
5. `/dashboard` (oturumlu) → `/app/dashboard`; `/olmayan` → 404 sayfası.
6. 375 / 768 / 1280 ekran görüntüleri: ana sayfa, fiyatlandırma, iletişim, giriş. Yatay kaydırma yok.
7. `read_console_messages` ile hata yok.

Üretim CSP kontrolü: `npm --prefix frontend run build` çıktısında (`frontend/dist/index.html`) inline `<script>` olmadığı doğrulanır (CSP `script-src 'self'`). Fontların yalnız `fonts.googleapis.com` / `fonts.gstatic.com` üzerinden yüklendiği doğrulanır.

- [ ] **Step 8: Commit**

```bash
git add frontend docs/rehberler/04-frontend-derinlemesine.md
git commit -m "feat(ui): apply new identity to app shell, add favicon, SEO assets and docs

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 9: Senkronizasyon onayı ve hafıza**

- AGENTS.md'ye uygun rapor hazırlanır ve tek bir açık onay istenir. Rapor içeriği:
  - kaynak ref: `claude/ui-ux-landing-page-design-23fc85`, hedef: `main`, remote: `ekipzen`
  - commit listesi (`git log --oneline main..HEAD`) ve değişen dosyalar (`git diff --stat main...HEAD`)
  - doğrulama sonuçları
  - yetkilendirilecek git işlemleri (feature branch push, main'e merge, main push)
- Onay gelmeden push/merge yapılmaz.
- Merge sonrası, canonical checkout'tan (`C:\Users\Lenovo\OneDrive\Masaüstü\Ekipzen UI\Ekipzen`, `workspace_root` ile) Project Memory'deki UI conventions girdisi `knowledge_search` ile bulunur ve `knowledge_update` ile yeni kimliğe göre güncellenir.
- Kullanıcının otomatik hafızasındaki `ekipzen-design-system.md` girdisi "indigo + Plus Jakarta Sans; renkler `--ez-*` token'larından" olarak güncellenir.

---

## Self-review notları

- **Spec kapsamı:**

  | Spec bölümü | Görev |
  |---|---|
  | §1 token | Task 1 |
  | §2 bileşenler | Task 2 |
  | §3 router, meta | Task 3, Task 10 |
  | §4 landing sayfaları | Task 6, 7, 8 |
  | §5 auth | Task 9 |
  | §6 kabuk | Task 1, Task 10 |
  | §7 backend | Task 4, 5 |
  | §8 doküman ve hafıza | Task 10 |
  | Spec adım 1 (spec commit) | tamamlandı: `313cad8` |

- **Sapmalar** (Global Constraints'te belirtildi):
  - Purpose token'ları `--ez-` önekli.
  - Demo ucu 204 döner.
  - 429 davranışı öznitelik ve politika testiyle doğrulanır.
- **Tip ve ad tutarlılığı:**
  - `appPath` / `LOGIN_PATH` / `roleHomeFor` Task 3'te tanımlanır; Task 6, 9 ve 10'da kullanılır.
  - `DemoRequest` alanları backend `SubmitDemoRequestCommand` ile birebir aynıdır (camelCase JSON).
  - `COMPANY_SIZES`, `SubmitDemoRequestCommandValidator.CompanySizes` ile aynıdır.
