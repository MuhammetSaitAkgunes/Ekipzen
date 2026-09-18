# Landing v2 — Canlı Ürün Vitrini Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ekipzen tanıtım sitesini hareketli, görsel ağırlıklı bir "canlı ürün vitrini"ne dönüştürmek ve "gelişen ürün" anlatısını durum etiketleri, yol haritası ve şeffaflık bölümleriyle kurmak.

**Architecture:** Yeni bağımlılık olmadan küçük bir hareket altyapısı (`marketing/motion/`: IntersectionObserver hook'u, reduced-motion hook'u, `Reveal`, kart ışığı) ve `marketing-motion.css` eklenir. İçerik modeli (`content.ts`) durum/yol haritası/ilke/taahhüt verileriyle genişler. Sayfalar bu veriyi okuyan küçük, tek sorumluluklu bileşenlerle (vitrin, yolculuk, yapışkan menü, katman şeması, süreç çizgisi, olgunluk, yol haritası panosu) yeniden kurulur.

**Tech Stack:** React 18, TypeScript, react-router-dom 7, Vitest + Testing Library (jsdom), düz CSS (`--ez-*` token'ları).

**Spec:** `docs/superpowers/specs/2026-09-18-landing-v2-canli-vitrin-design.md`

## Global Constraints

- Yeni npm bağımlılığı eklenmez; CSP değişmez.
- Renkler yalnız `--ez-*` token'larından ve ramp'lerden (`--indigo-*`, `--slate-*` …) gelir; yeni hex sabitlenmez.
- `@media (prefers-reduced-motion: reduce)` altında bütün hareket kapanır; içerik tam görünür kalır. Bileşenler `usePrefersReducedMotion()` ile otomatik ilerlemeyi de kapatır.
- Dokunma hedefleri ≥ 44px, odak halkası görünür, kontrast WCAG AA, 375px'te yatay taşma yok.
- Metin üslubu: ölçülü ve kurumsal; "Türkiye için", "tek tıkla", "sürpriz", "kurtar", "kolayca", "kısa sürede", "N dakikalık" yok; tarih/çeyrek/fiyat taahhüdü yok; sahte müşteri/metrik yok. Örnek ekranlar "örnek görünüm"dür.
- Modül durumları: Personel, İzin, İşe alım = `active`; Mesai ve Puantaj, Bordro = `pilot`; Risk Merkezi = `building`.
- Etiketler: `active` → "Kullanımda", `pilot` → "Pilotta", `building` → "Geliştiriliyor".
- Bütün metinler Türkçe; spec'teki metinler aynen kullanılır.
- Commit trailer: `Co-Authored-By: Claude <commit'i yazan model> <noreply@anthropic.com>`. Push/merge yok.

## Dosya haritası

| Dosya | Sorumluluk |
|---|---|
| `frontend/src/test/intersectionObserver.ts` (yeni) | jsdom için tetiklenebilir IntersectionObserver taklidi |
| `frontend/src/test/setup.ts` | taklidi ve `matchMedia` taklidini kurar |
| `frontend/src/marketing/motion/usePrefersReducedMotion.ts` (yeni) | reduced-motion tercihi |
| `frontend/src/marketing/motion/useInView.ts` (yeni) | görünürlük hook'u |
| `frontend/src/marketing/motion/Reveal.tsx` (yeni) | kaydırmayla belirme sarmalayıcısı |
| `frontend/src/marketing/motion/spotlight.ts` (yeni) | kart ışığı pointer handler'ı |
| `frontend/src/styles/marketing-motion.css` (yeni) | tüm hareket/efekt stilleri |
| `frontend/src/marketing/content.ts` | durum, yol haritası, ilke, taahhüt, süreç, yolculuk, güvenlik verileri |
| `frontend/src/marketing/StatusBadge.tsx` (yeni) | durum etiketi |
| `frontend/src/marketing/visuals.tsx` | yeni örnek ekranlar, düz hero kompozisyonu |
| `frontend/src/marketing/ProductShowcase.tsx` (yeni) | sekmeli canlı vitrin |
| `frontend/src/marketing/EmployeeJourney.tsx` (yeni) | yapışkan kaydırma anlatısı |
| `frontend/src/marketing/StickyModuleNav.tsx`, `DataFlowDiagram.tsx` (yeni) | Özellikler |
| `frontend/src/marketing/SecurityLayers.tsx` (yeni) | Güvenlik katman şeması |
| `frontend/src/marketing/ProcessTimeline.tsx`, `ModuleMaturity.tsx`, `RoadmapBoard.tsx` (yeni) | Hakkımızda |
| `frontend/src/marketing/pages/*.tsx` | sayfa kompozisyonları |

---

### Task 1: Hareket altyapısı ve test ortamı

**Files:**
- Create: `frontend/src/test/intersectionObserver.ts`
- Modify: `frontend/src/test/setup.ts`
- Create: `frontend/src/marketing/motion/usePrefersReducedMotion.ts`, `useInView.ts`, `Reveal.tsx`, `spotlight.ts`, `index.ts`
- Create: `frontend/src/marketing/motion/motion.test.tsx`
- Create: `frontend/src/styles/marketing-motion.css`
- Modify: `frontend/src/main.tsx` (`import "./styles/marketing-motion.css";` marketing.css importundan hemen sonra)

**Interfaces:**
- Produces:
  - `triggerIntersections(isIntersecting: boolean, predicate?: (el: Element) => boolean): void` ve `resetIntersectionObservers(): void` (`frontend/src/test/intersectionObserver.ts`)
  - `setReducedMotion(value: boolean): void` (aynı dosya; `matchMedia` taklidi)
  - `usePrefersReducedMotion(): boolean`
  - `useInView<T extends Element>(options?: { once?: boolean; threshold?: number; rootMargin?: string }): { ref: (node: T | null) => void; inView: boolean }`. `IntersectionObserver` yoksa `inView` true döner.
  - `Reveal({ as?: ElementType; delay?: number; className?: string; children; ...HTMLAttributes })`: `ez-reveal` sınıfı; görünür olunca ya da hareket azaltılmışsa `is-visible` eklenir. `delay` ms'dir ve `--ez-reveal-delay` değişkenine yazılır.
  - `onSpotlightMove(event: React.PointerEvent<HTMLElement>): void`: `--mx` / `--my` değişkenlerini yazar.
  - `motion/index.ts` hepsini yeniden export eder.
  - CSS sınıfları: `ez-reveal`, `is-visible`, `ez-spotlight`, `ez-aurora`, `ez-float`, `ez-float--slow`.

- [ ] **Step 1: Test taklitleri** — `frontend/src/test/intersectionObserver.ts`

```ts
type Callback = (entries: IntersectionObserverEntry[], observer: IntersectionObserver) => void;

class TestIntersectionObserver {
  static instances = new Set<TestIntersectionObserver>();
  readonly root = null;
  readonly rootMargin = "";
  readonly thresholds = [0];
  private readonly elements = new Set<Element>();
  constructor(private readonly callback: Callback) {
    TestIntersectionObserver.instances.add(this);
  }
  observe(element: Element) { this.elements.add(element); }
  unobserve(element: Element) { this.elements.delete(element); }
  disconnect() { this.elements.clear(); TestIntersectionObserver.instances.delete(this); }
  takeRecords(): IntersectionObserverEntry[] { return []; }
  trigger(isIntersecting: boolean, predicate: (el: Element) => boolean) {
    const entries = [...this.elements].filter(predicate).map((target) => ({
      isIntersecting, target, intersectionRatio: isIntersecting ? 1 : 0,
      time: 0, boundingClientRect: target.getBoundingClientRect(),
      intersectionRect: target.getBoundingClientRect(), rootBounds: null,
    }) as IntersectionObserverEntry);
    if (entries.length) this.callback(entries, this as unknown as IntersectionObserver);
  }
}

export function installIntersectionObserver() {
  (globalThis as unknown as { IntersectionObserver: unknown }).IntersectionObserver = TestIntersectionObserver;
}

/** Gözlenen öğeleri görünür/görünmez yapar (sırayla, belge sırasına göre). */
export function triggerIntersections(isIntersecting: boolean, predicate: (el: Element) => boolean = () => true) {
  for (const observer of [...TestIntersectionObserver.instances]) observer.trigger(isIntersecting, predicate);
}

export function resetIntersectionObservers() {
  TestIntersectionObserver.instances.clear();
}

let reducedMotion = false;
const listeners = new Set<() => void>();

export function installMatchMedia() {
  window.matchMedia = ((query: string) => ({
    get matches() { return query.includes("prefers-reduced-motion") ? reducedMotion : false; },
    media: query,
    onchange: null,
    addEventListener: (_: string, cb: () => void) => listeners.add(cb),
    removeEventListener: (_: string, cb: () => void) => listeners.delete(cb),
    addListener: (cb: () => void) => listeners.add(cb),
    removeListener: (cb: () => void) => listeners.delete(cb),
    dispatchEvent: () => false,
  })) as unknown as typeof window.matchMedia;
}

export function setReducedMotion(value: boolean) {
  reducedMotion = value;
  for (const cb of [...listeners]) cb();
}
```

`frontend/src/test/setup.ts` şu hale gelir:

```ts
import "@testing-library/jest-dom/vitest";
import { afterEach } from "vitest";
import { installIntersectionObserver, installMatchMedia, resetIntersectionObservers, setReducedMotion } from "./intersectionObserver";

installIntersectionObserver();
installMatchMedia();

afterEach(() => {
  resetIntersectionObservers();
  setReducedMotion(false);
});
```

- [ ] **Step 2: Failing test** — `frontend/src/marketing/motion/motion.test.tsx`

```tsx
import { act, fireEvent, render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { setReducedMotion, triggerIntersections } from "../../test/intersectionObserver";
import { Reveal, onSpotlightMove, useInView, usePrefersReducedMotion } from ".";

test("Reveal görünür olana kadar is-visible taşımaz, görünür olunca taşır", () => {
  render(<Reveal data-testid="r">içerik</Reveal>);
  const el = screen.getByTestId("r");
  expect(el).toHaveClass("ez-reveal");
  expect(el).not.toHaveClass("is-visible");
  act(() => triggerIntersections(true));
  expect(el).toHaveClass("is-visible");
});

test("Reveal hareket azaltılmışsa anında görünür ve gecikmeyi değişkene yazar", () => {
  setReducedMotion(true);
  render(<Reveal data-testid="r" delay={120}>içerik</Reveal>);
  const el = screen.getByTestId("r");
  expect(el).toHaveClass("is-visible");
  expect(el.style.getPropertyValue("--ez-reveal-delay")).toBe("120ms");
});

test("Reveal as ile farklı etiket render eder", () => {
  render(<ul><Reveal as="li" data-testid="r">öğe</Reveal></ul>);
  expect(screen.getByTestId("r").tagName).toBe("LI");
});

function InViewProbe({ once }: { once?: boolean }) {
  const { ref, inView } = useInView<HTMLDivElement>({ once });
  return <div ref={ref} data-testid="p">{inView ? "evet" : "hayır"}</div>;
}

test("useInView görünürlüğü izler; once ile görünür kalır", () => {
  const { unmount } = render(<InViewProbe />);
  act(() => triggerIntersections(true));
  expect(screen.getByTestId("p")).toHaveTextContent("evet");
  act(() => triggerIntersections(false));
  expect(screen.getByTestId("p")).toHaveTextContent("hayır");
  unmount();
  render(<InViewProbe once />);
  act(() => triggerIntersections(true));
  act(() => triggerIntersections(false));
  expect(screen.getByTestId("p")).toHaveTextContent("evet");
});

function MotionProbe() {
  return <span data-testid="m">{usePrefersReducedMotion() ? "azaltılmış" : "normal"}</span>;
}

test("usePrefersReducedMotion tercihi ve değişimini izler", () => {
  render(<MotionProbe />);
  expect(screen.getByTestId("m")).toHaveTextContent("normal");
  act(() => setReducedMotion(true));
  expect(screen.getByTestId("m")).toHaveTextContent("azaltılmış");
});

test("onSpotlightMove imleç konumunu CSS değişkenlerine yazar", () => {
  render(<div data-testid="s" onPointerMove={onSpotlightMove} />);
  const el = screen.getByTestId("s");
  fireEvent.pointerMove(el, { clientX: 30, clientY: 12 });
  expect(el.style.getPropertyValue("--mx")).toBe("30px");
  expect(el.style.getPropertyValue("--my")).toBe("12px");
});
```

jsdom'da `getBoundingClientRect` sıfır döndüğü için beklenen değerler `clientX` / `clientY` ile aynıdır.

- [ ] **Step 3: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing/motion`
Expected: FAIL (`Failed to resolve import "."`)

- [ ] **Step 4: Uygulama**

`frontend/src/marketing/motion/usePrefersReducedMotion.ts`:

```ts
import { useEffect, useState } from "react";

const QUERY = "(prefers-reduced-motion: reduce)";

export function usePrefersReducedMotion(): boolean {
  const [reduced, setReduced] = useState(() =>
    typeof window !== "undefined" && typeof window.matchMedia === "function" && window.matchMedia(QUERY).matches);
  useEffect(() => {
    if (typeof window.matchMedia !== "function") return;
    const mql = window.matchMedia(QUERY);
    const update = () => setReduced(mql.matches);
    update();
    mql.addEventListener("change", update);
    return () => mql.removeEventListener("change", update);
  }, []);
  return reduced;
}
```

`frontend/src/marketing/motion/useInView.ts`:

```ts
import { useCallback, useEffect, useRef, useState } from "react";

export type InViewOptions = { once?: boolean; threshold?: number; rootMargin?: string };

export function useInView<T extends Element>({ once = false, threshold = 0.15, rootMargin = "0px 0px -10% 0px" }: InViewOptions = {}) {
  const supported = typeof IntersectionObserver !== "undefined";
  const [inView, setInView] = useState(!supported);
  const observerRef = useRef<IntersectionObserver | null>(null);

  const ref = useCallback((node: T | null) => {
    observerRef.current?.disconnect();
    observerRef.current = null;
    if (!node || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          setInView(true);
          if (once) { observer.disconnect(); observerRef.current = null; }
        } else if (!once) {
          setInView(false);
        }
      }
    }, { threshold, rootMargin });
    observer.observe(node);
    observerRef.current = observer;
  }, [once, threshold, rootMargin]);

  useEffect(() => () => observerRef.current?.disconnect(), []);
  return { ref, inView };
}
```

`frontend/src/marketing/motion/Reveal.tsx`:

```tsx
import type { CSSProperties, ElementType, HTMLAttributes, ReactNode } from "react";
import { useInView } from "./useInView";
import { usePrefersReducedMotion } from "./usePrefersReducedMotion";

type RevealProps = HTMLAttributes<HTMLElement> & { as?: ElementType; delay?: number; className?: string; children?: ReactNode };

export function Reveal({ as: Tag = "div", delay = 0, className, style, children, ...rest }: RevealProps) {
  const reduced = usePrefersReducedMotion();
  const { ref, inView } = useInView<HTMLElement>({ once: true });
  const visible = reduced || inView;
  const mergedStyle = { ...style, "--ez-reveal-delay": `${delay}ms` } as CSSProperties;
  return (
    <Tag ref={ref} className={["ez-reveal", visible && "is-visible", className].filter(Boolean).join(" ")} style={mergedStyle} {...rest}>
      {children}
    </Tag>
  );
}
```

`frontend/src/marketing/motion/spotlight.ts`:

```ts
import type { PointerEvent } from "react";

/** Kart ışığı: imleç konumunu --mx/--my değişkenlerine yazar (.ez-spotlight kullanır). */
export function onSpotlightMove(event: PointerEvent<HTMLElement>) {
  const el = event.currentTarget;
  const rect = el.getBoundingClientRect();
  el.style.setProperty("--mx", `${event.clientX - rect.left}px`);
  el.style.setProperty("--my", `${event.clientY - rect.top}px`);
}
```

`frontend/src/marketing/motion/index.ts`:

```ts
export { usePrefersReducedMotion } from "./usePrefersReducedMotion";
export { useInView, type InViewOptions } from "./useInView";
export { Reveal } from "./Reveal";
export { onSpotlightMove } from "./spotlight";
```

`frontend/src/styles/marketing-motion.css`:

```css
/* Tanıtım sitesi hareket ve efektleri — yalnız token'lar; reduced-motion'da kapanır. */
.ez-reveal { opacity: 0; transform: translateY(18px); transition: opacity 600ms var(--ease-out), transform 600ms var(--ease-out); transition-delay: var(--ez-reveal-delay, 0ms); }
.ez-reveal.is-visible { opacity: 1; transform: none; }

.ez-spotlight { position: relative; isolation: isolate; }
.ez-spotlight::before {
  content: ""; position: absolute; inset: 0; border-radius: inherit; z-index: -1; opacity: 0; pointer-events: none;
  background: radial-gradient(320px circle at var(--mx, 50%) var(--my, 50%), color-mix(in srgb, var(--ez-primary) 14%, transparent), transparent 70%);
  transition: opacity var(--dur-base) var(--ease-out);
}
.ez-spotlight:hover::before { opacity: 1; }

.ez-aurora { position: relative; overflow: hidden; isolation: isolate; }
.ez-aurora::before, .ez-aurora::after {
  content: ""; position: absolute; z-index: -1; width: 46rem; height: 46rem; border-radius: 50%; filter: blur(70px); opacity: 0.55; pointer-events: none;
}
.ez-aurora::before { top: -22rem; right: -12rem; background: var(--indigo-200); animation: ez-drift 22s ease-in-out infinite alternate; }
.ez-aurora::after { bottom: -26rem; left: -14rem; background: var(--sky-50); animation: ez-drift 28s ease-in-out infinite alternate-reverse; }
@keyframes ez-drift { from { transform: translate3d(0, 0, 0) scale(1); } to { transform: translate3d(-6rem, 4rem, 0) scale(1.12); } }

.ez-float { animation: ez-float 7s ease-in-out infinite; }
.ez-float--slow { animation-duration: 10s; animation-delay: -3s; }
@keyframes ez-float { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(-10px); } }

@media (prefers-reduced-motion: reduce) {
  .ez-reveal { opacity: 1; transform: none; transition: none; }
  .ez-aurora::before, .ez-aurora::after, .ez-float { animation: none; }
}
```

`frontend/src/main.tsx`: `import "./styles/marketing.css";` satırının altına `import "./styles/marketing-motion.css";`.

- [ ] **Step 5: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run src/marketing/motion`
Expected: 6 test PASS

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS (setup değişikliği mevcut testleri bozmaz)

- [ ] **Step 6: Commit**

```bash
git add frontend/src/test frontend/src/marketing/motion frontend/src/styles/marketing-motion.css frontend/src/main.tsx
git commit -m "feat(landing): add motion primitives with reduced-motion support"
```

(Trailer: Global Constraints.)

---

### Task 2: İçerik modeli ve durum etiketi

**Files:**
- Modify: `frontend/src/marketing/content.ts`
- Create: `frontend/src/marketing/StatusBadge.tsx`
- Modify: `frontend/src/marketing/content.test.ts`
- Create: `frontend/src/marketing/StatusBadge.test.tsx`

**Interfaces:**
- Consumes: `Badge({ tone: "neutral" | "primary" | "success" | "warning" | "danger", children })` (`frontend/src/ui`).
- Produces (content.ts):
  - `type ModuleStatus = "active" | "pilot" | "building"`
  - `STATUS_META: Record<ModuleStatus, { label: string; description: string; tone: "success" | "warning" | "primary" }>`
  - `MarketingModule` gains `status: ModuleStatus`; `SHOWCASE_ORDER: string[]`
  - `type RoadmapStage = "now" | "next" | "planned"`
  - `ROADMAP_STAGES: { id: RoadmapStage; title: string; subtitle: string }[]`
  - `roadmap: { stage: RoadmapStage; title: string; text: string }[]`
  - `ROADMAP_NOTE: string`
  - `devProcess: { title: string; text: string }[]`
  - `principles: { icon: string; title: string; text: string }[]`
  - `commitments: { title: string; text: string }[]`
  - `journeySteps: { id: string; title: string; text: string; field: { label: string; value: string } }[]`
  - `securityLayers: { id: string; title: string; short: string; text: string }[]`
  - `securityCategories: { id: string; icon: string; title: string; items: string[] }[]`
  - `transparency: { provided: string[]; inProgress: string[] }`
  - `aboutScenes: { icon: string; title: string; text: string }[]`
- Produces (StatusBadge.tsx): `StatusBadge({ status }: { status: ModuleStatus })`: `<span className="ez-status" data-status={status} title={description}><Badge tone={tone}>{label}</Badge></span>`.

- [ ] **Step 1: Failing testler**

`frontend/src/marketing/content.test.ts` sonuna eklenir (dosyanın mevcut importu `import { securityPillars, faqs } from "./content";` genişletilir):

```ts
import {
  STATUS_META, SHOWCASE_ORDER, ROADMAP_NOTE, ROADMAP_STAGES, commitments, devProcess, journeySteps, modules, principles,
  roadmap, securityCategories, securityLayers, transparency,
} from "./content";

test("her modülün geçerli bir durumu ve spec'teki dağılımı vardır", () => {
  const byId = Object.fromEntries(modules.map((m) => [m.id, m.status]));
  expect(byId).toEqual({
    "risk-merkezi": "building", personel: "active", izin: "active",
    puantaj: "pilot", bordro: "pilot", "ise-alim": "active",
  });
  for (const m of modules) expect(Object.keys(STATUS_META)).toContain(m.status);
  expect(STATUS_META.active.label).toBe("Kullanımda");
  expect(STATUS_META.pilot.label).toBe("Pilotta");
  expect(STATUS_META.building.label).toBe("Geliştiriliyor");
});

test("geliştirilen modül kullanımda ya da tahmin yapıyormuş gibi anlatılmaz", () => {
  for (const m of modules.filter((x) => x.status === "building")) {
    const text = [m.summary, ...m.points].join(" ");
    expect(text).not.toMatch(/tahmin|kullanımda/i);
  }
});

test("yol haritası ve taahhütler tarih, çeyrek ya da fiyat içermez", () => {
  const texts = [...roadmap.flatMap((r) => [r.title, r.text]), ...commitments.flatMap((c) => [c.title, c.text]), ROADMAP_NOTE];
  for (const t of texts) {
    expect(t).not.toMatch(/\b20\d{2}\b/);
    expect(t).not.toMatch(/çeyrek/i);
    expect(t).not.toMatch(/₺|\bTL\b/);
  }
});

test("içerik listeleri spec'teki yapıdadır", () => {
  expect(ROADMAP_STAGES.map((s) => s.id)).toEqual(["now", "next", "planned"]);
  expect(roadmap.filter((r) => r.stage === "now")).toHaveLength(2);
  expect(roadmap.filter((r) => r.stage === "next")).toHaveLength(2);
  expect(roadmap.filter((r) => r.stage === "planned")).toHaveLength(3);
  expect(ROADMAP_NOTE).toBe("Tarih taahhüdü değildir; öncelikler pilot geri bildirimleriyle şekillenir.");
  expect(devProcess).toHaveLength(6);
  expect(principles).toHaveLength(4);
  expect(commitments).toHaveLength(3);
  expect(journeySteps.map((s) => s.title)).toEqual(["İşe alım", "Özlük", "İzin", "Puantaj", "Bordro"]);
  expect(securityLayers).toHaveLength(5);
  expect(securityCategories.map((c) => c.title)).toEqual(["Veri", "Erişim", "İşletim", "Gizlilik (KVKK)"]);
  expect(transparency.inProgress).toEqual(["Çok faktörlü kimlik doğrulama", "Yedekleme ve geri dönüş güvencesi"]);
  expect(SHOWCASE_ORDER).toEqual(["personel", "izin", "puantaj", "bordro", "ise-alim", "risk-merkezi"]);
});
```

`frontend/src/marketing/StatusBadge.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { StatusBadge } from "./StatusBadge";

test.each([
  ["active", "Kullanımda", "ez-badge--success"],
  ["pilot", "Pilotta", "ez-badge--warning"],
  ["building", "Geliştiriliyor", "ez-badge--primary"],
] as const)("%s durumu %s etiketi ve doğru tonla gösterilir", (status, label, cls) => {
  render(<StatusBadge status={status} />);
  const badge = screen.getByText(label);
  expect(badge).toHaveClass(cls);
  expect(badge.closest(".ez-status")).toHaveAttribute("data-status", status);
  expect(badge.closest(".ez-status")!.getAttribute("title")).toBeTruthy();
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing/content.test.ts src/marketing/StatusBadge.test.tsx`
Expected: FAIL (exportlar yok)

- [ ] **Step 3: `content.ts` genişletmesi**

`MarketingModule` tipi ve modül listesi:
- Tipe `status: ModuleStatus` eklenir.
- Her modül nesnesine `status` alanı eklenir: `risk-merkezi: "building"`, `personel: "active"`, `izin: "active"`, `puantaj: "pilot"`, `bordro: "pilot"`, `ise-alim: "active"`.
- Risk Merkezi'nin `summary` ve `points` değerleri şununla değiştirilir:

```ts
    summary: "Devamsızlık, fazla mesai, kullanılmayan izin ve çalışan nabzı verilerinden departman düzeyinde risk sinyalleri üretmeye yönelik modül.",
    points: ["Departman bazında risk skoru ve dönemsel eğilim", "Yönetici yükü ve çalışan nabzı görünümü", "Sinyale bağlı aksiyon önerileri"],
```

`MarketingModule` tanımının üstüne eklenir:

```ts
export type ModuleStatus = "active" | "pilot" | "building";

export const STATUS_META: Record<ModuleStatus, { label: string; description: string; tone: "success" | "warning" | "primary" }> = {
  active: { label: "Kullanımda", description: "Genel kullanıma açık; müşteri ortamlarında kullanılabilir.", tone: "success" },
  pilot: { label: "Pilotta", description: "Pilot müşteride gerçek verilerle doğrulanıyor.", tone: "warning" },
  building: { label: "Geliştiriliyor", description: "Aktif olarak geliştiriliyor; genel kullanıma açılmadan önce pilotta doğrulanacak.", tone: "primary" },
};
```

`modules` dizisinin altına eklenir:

```ts
/** Ana sayfa vitrininde sekme sırası. */
export const SHOWCASE_ORDER = ["personel", "izin", "puantaj", "bordro", "ise-alim", "risk-merkezi"];
```

Dosya sonuna eklenir:

```ts
export type RoadmapStage = "now" | "next" | "planned";

export const ROADMAP_STAGES: { id: RoadmapStage; title: string; subtitle: string }[] = [
  { id: "now", title: "Şimdi", subtitle: "Pilotta doğrulanıyor" },
  { id: "next", title: "Sıradaki", subtitle: "Geliştirme sırasında" },
  { id: "planned", title: "Planlanan", subtitle: "Yol haritasında" },
];

export const roadmap: { stage: RoadmapStage; title: string; text: string }[] = [
  { stage: "now", title: "Mesai ve puantaj için çalışma düzeni profilleri", text: "Çalışma haftası, mola ve fazla mesai kurallarının şirket düzenine göre tanımlanması." },
  { stage: "now", title: "Bordro için paralel doğrulama dönemi", text: "İlk bordroların mevcut bordroyla paralel hesaplanıp uzman kabulünden geçirilmesi." },
  { stage: "next", title: "Canlı İK verisinden risk sinyalleri", text: "Risk Merkezi'nin şirketin kendi izin, mesai ve devamsızlık kayıtlarıyla çalışması." },
  { stage: "next", title: "Yedekleme ve geri dönüş güvencesi", text: "Düzenli yedek, sunucu dışı kopya ve denenmiş geri dönüş prosedürü." },
  { stage: "planned", title: "Çok faktörlü kimlik doğrulama", text: "Girişte ikinci doğrulama adımı." },
  { stage: "planned", title: "Genişleyen mevzuat kapsamı", text: "Farklı çalışan statüleri ve bordro senaryoları için desteğin artırılması." },
  { stage: "planned", title: "Ek raporlar", text: "İK ve yönetim için hazır rapor setleri." },
];

export const ROADMAP_NOTE = "Tarih taahhüdü değildir; öncelikler pilot geri bildirimleriyle şekillenir.";

export const devProcess = [
  { title: "İhtiyaç", text: "İK ekiplerinden gelen gerçek iş akışları ve geri bildirimler." },
  { title: "Geliştirme", text: "Küçük, gözden geçirilebilir adımlar." },
  { title: "Otomatik testler", text: "Her değişiklik birim ve entegrasyon testlerinden geçer." },
  { title: "Pilotta doğrulama", text: "Gerçek verilerle, kontrollü bir müşteri ortamında." },
  { title: "Uzman kabulü", text: "Bordro gibi hesaplamalar alan uzmanının onayından geçer." },
  { title: "Genel kullanım", text: "Doğrulanan modül tüm müşterilere açılır." },
];

export const principles = [
  { icon: "fa-scale-balanced", title: "Önce doğruluk", text: "Hesaplamalar doğrulanmadan canlı kullanım için önerilmez." },
  { icon: "fa-database", title: "Veri ayrımı", text: "Her şirketin verisi kendi veritabanında tutulur." },
  { icon: "fa-comments", title: "Açık iletişim", text: "Neyin hazır, neyin geliştirilmekte olduğunu açıkça söyleriz." },
  { icon: "fa-layer-group", title: "Sadelik", text: "Her ekran belirli bir işi tamamlamaya hizmet eder." },
];

export const commitments = [
  { title: "Hazır olanla geliştirileni ayırırız.", text: "Her modülün durumu sitede ve üründe açıkça belirtilir." },
  { title: "Verinizin sahibi sizsiniz.", text: "Verileriniz şirketinize ait ayrı bir veritabanında tutulur; her kullanıcı kendi verisini dışa aktarabilir." },
  { title: "Hesaplamalar doğrulanmadan kullanıma alınmaz.", text: "Bordro, uzman kabulünden önce ödeme veya beyan için esas alınmaz." },
];

export const journeySteps = [
  { id: "ise-alim", title: "İşe alım", text: "Aday işe alındığında davetle sisteme dahil edilir.", field: { label: "Durum", value: "İşe alındı" } },
  { id: "ozluk", title: "Özlük", text: "Personel kartı ve belgeler tek kayıtta toplanır.", field: { label: "Belgeler", value: "4 belge" } },
  { id: "izin", title: "İzin", text: "Talepler aynı kayıt üzerinden onaylanır, bakiye güncellenir.", field: { label: "Yıllık izin bakiyesi", value: "14 gün" } },
  { id: "puantaj", title: "Puantaj", text: "Günlük mesai kayıtları dönem puantajına dönüşür.", field: { label: "Dönem puantajı", value: "21 iş günü" } },
  { id: "bordro", title: "Bordro", text: "Bordro, dönemin her iş gününün puantaj ya da onaylı izinle karşılandığını kontrol eder.", field: { label: "Bordro durumu", value: "Hesaplamaya hazır" } },
];

export const securityLayers = [
  { id: "baglanti", title: "Şifreli bağlantı", short: "HTTPS, HSTS, CSP", text: "Tüm trafik HTTPS üzerinden şifrelenir; HSTS ve içerik güvenliği politikası (CSP) uygulanır." },
  { id: "kimlik", title: "Kimlik doğrulama", short: "Oturum, parola, deneme sınırı", text: "Kısa ömürlü oturum anahtarları, güçlü parola kuralları ve giriş denemelerine karşı sınırlama." },
  { id: "yetki", title: "Rol bazlı yetki", short: "Arayüzde ve sunucuda", text: "İK yöneticisi, yönetici ve çalışan yalnız rolünün gerektirdiği verilere erişir; yetki kontrolü sunucu tarafında da uygulanır." },
  { id: "veritabani", title: "Şirkete ait veritabanı", short: "Veri ayrımı", text: "İK verileriniz şirketinize ait ayrı bir veritabanında tutulur; başka müşterilerin İK verileriyle aynı tabloda bulunmaz." },
  { id: "denetim", title: "Denetim izi", short: "Kim, ne zaman", text: "Kayıtlardaki değişiklikler, değişikliği yapan kişi ve zaman bilgisiyle birlikte saklanır." },
];

export const securityCategories = [
  { id: "veri", icon: "fa-database", title: "Veri", items: ["Şirkete ait ayrı veritabanı", "Denetim izi"] },
  { id: "erisim", icon: "fa-user-lock", title: "Erişim", items: ["Rol bazlı yetki", "Kısa ömürlü oturum anahtarları", "Güçlü parola kuralları"] },
  { id: "isletim", icon: "fa-gears", title: "İşletim", items: ["Şifreli bağlantı (HTTPS, HSTS, CSP)", "Giriş denemesi sınırlaması", "Yayın öncesi otomatik testler"] },
  { id: "gizlilik", icon: "fa-scale-balanced", title: "Gizlilik (KVKK)", items: ["Kişisel verilerin dışa aktarımı", "KVKK aydınlatma metni"] },
];

export const transparency = {
  provided: ["Şirkete ait ayrı veritabanı", "Rol bazlı yetki", "Şifreli bağlantı", "Denetim izi", "Kişisel veri dışa aktarımı", "Giriş denemesi sınırı"],
  inProgress: ["Çok faktörlü kimlik doğrulama", "Yedekleme ve geri dönüş güvencesi"],
};

export const aboutScenes = [
  { icon: "fa-table-cells", title: "Dağınık tablolar ve yazışmalar", text: "Personel bilgileri, izin talepleri ve puantaj farklı dosyalarda ve e-posta zincirlerinde tutulur." },
  { icon: "fa-clone", title: "Aynı bilginin tekrar girilmesi", text: "Bir çalışanın bilgisi her süreçte yeniden girilir; kayıtlar arasında tutarsızlıklar oluşur." },
  { icon: "fa-id-card", title: "Tek bir kayıt düzeni", text: "Ekipzen'de tüm süreçler aynı personel kaydı üzerinden ilerler." },
];
```

- [ ] **Step 4: `StatusBadge.tsx`**

```tsx
import { Badge } from "../ui";
import { STATUS_META, type ModuleStatus } from "./content";

export function StatusBadge({ status }: { status: ModuleStatus }) {
  const meta = STATUS_META[status];
  return (
    <span className="ez-status" data-status={status} title={meta.description}>
      <Badge tone={meta.tone}>{meta.label}</Badge>
    </span>
  );
}
```

- [ ] **Step 5: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS. Risk Merkezi metni değiştiği için başka bir test kırılırsa yalnız metin beklentisi güncellenir.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/marketing/content.ts frontend/src/marketing/content.test.ts frontend/src/marketing/StatusBadge.tsx frontend/src/marketing/StatusBadge.test.tsx
git commit -m "feat(landing): add module status, roadmap and trust content model"
```

---

### Task 3: Yeni örnek ekranlar ve düz hero kompozisyonu

**Files:**
- Modify: `frontend/src/marketing/visuals.tsx`
- Modify: `frontend/src/styles/marketing.css` (`.ez-hero-visual*` kuralları)
- Create: `frontend/src/marketing/visuals.test.tsx`

**Interfaces:**
- Produces:
  - `PersonnelListCard()`, `TimesheetGridCard()`, `RecruitmentBoardCard()` (dekoratif, `aria-hidden` bir kapsayıcı içinde kullanılır)
  - `ModuleVisual({ id })` eşlemesi:

    | id | Görseller |
    |---|---|
    | `personel` | PersonnelListCard |
    | `izin` | LeaveCalendarCard |
    | `puantaj` | TimesheetGridCard |
    | `bordro` | PayrollSummaryCard |
    | `ise-alim` | RecruitmentBoardCard |
    | `risk-merkezi` | RiskScoreCard + ActionListCard |

  - `HeroVisual()`: düz katmanlı kompozisyon. Ana ekran `PersonnelListCard`; üzerinde süzülen `LeaveCalendarCard` (`ez-float`) ve `PayrollSummaryCard` (`ez-float ez-float--slow`). Hiçbir `rotate` yok.
  - Sınıflar: `ez-hero-visual__main`, `ez-hero-visual__float`, `ez-hero-visual__float--a`, `ez-hero-visual__float--b`, `ez-mock__table`, `ez-mock__grid`, `ez-mock__kanban`.

- [ ] **Step 1: Failing test** — `frontend/src/marketing/visuals.test.tsx`

```tsx
import { render } from "@testing-library/react";
import { expect, test } from "vitest";
import { HeroVisual, ModuleVisual } from "./visuals";
import { modules } from "./content";

test("her modülün örnek ekranı vardır ve erişilebilir açıklama taşır", () => {
  for (const m of modules) {
    const { container, unmount } = render(<ModuleVisual id={m.id} />);
    expect(container.querySelector(".ez-mock"), m.id).not.toBeNull();
    expect(container.querySelector("figcaption")).toHaveTextContent("örnek görünüm");
    unmount();
  }
});

test("hero kompozisyonu düzdür ve süzülen iki kart içerir", () => {
  const { container } = render(<HeroVisual />);
  expect(container.querySelector(".ez-hero-visual__main .ez-mock")).not.toBeNull();
  expect(container.querySelectorAll(".ez-hero-visual__float")).toHaveLength(2);
  expect(container.querySelector("figcaption")).toHaveTextContent("örnek görünüm");
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing/visuals.test.tsx`
Expected: FAIL (hero testi: `.ez-hero-visual__main` ve `.ez-hero-visual__float` yok)

- [ ] **Step 3: `visuals.tsx` eklemeleri** — `ActionListCard` fonksiyonundan sonra eklenir:

```tsx
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
```

`HeroVisual` ve `moduleVisuals` şununla değiştirilir:

```tsx
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
```

`ModuleVisual` içindeki figcaption metni `"Modülden örnek görünüm."` olur.

- [ ] **Step 4: CSS** — `marketing.css` içinde şu kurallar silinir:
  - `.ez-hero-visual__stack`
  - onun `:nth-child` kuralları
  - 520px medya kuralındaki `.ez-hero-visual__stack` satırları

Yerlerine eklenir:

```css
.ez-hero-visual__stage { position: relative; padding: 2.5rem 1.5rem 3rem 0; }
.ez-hero-visual__main { position: relative; z-index: 1; }
.ez-hero-visual__main .ez-mock { box-shadow: var(--ez-shadow-lg); }
.ez-hero-visual__float { position: absolute; z-index: 2; width: min(62%, 260px); }
.ez-hero-visual__float--a { top: 0; right: 0; }
.ez-hero-visual__float--b { bottom: 0; left: -1.5rem; }
@media (max-width: 960px) { .ez-hero-visual__stage { padding: 2rem 0 2.5rem; } .ez-hero-visual__float--b { left: 0; } }
@media (max-width: 520px) {
  .ez-hero-visual__stage { padding: 0; display: grid; gap: 0.75rem; }
  .ez-hero-visual__float { position: static; width: auto; }
  .ez-hero-visual__float--b { display: none; }
}

.ez-mock__table { list-style: none; display: grid; gap: 0.5rem; }
.ez-mock__table li { display: grid; grid-template-columns: 32px 1fr auto; align-items: center; gap: 0.625rem; padding: 0.375rem 0; border-bottom: 1px solid var(--ez-border-soft); }
.ez-mock__table li:last-child { border-bottom: 0; }
.ez-mock__avatar { display: grid; place-items: center; width: 32px; height: 32px; border-radius: 50%; background: var(--ez-primary-soft); color: var(--ez-primary); font-size: var(--fs-12); font-weight: 700; }
.ez-mock__muted { color: var(--ez-text-3); font-size: var(--fs-12); }
.ez-mock__grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 0.5rem; align-items: end; }
.ez-mock__grid > div { display: grid; gap: 0.25rem; justify-items: center; }
.ez-mock__grid strong { font-family: var(--ez-font-mono); font-size: var(--fs-12); color: var(--ez-text); }
.ez-mock__bar { width: 100%; border-radius: 4px 4px 0 0; background: var(--indigo-200); }
.ez-mock__grid > div:nth-child(3) .ez-mock__bar { background: var(--ez-primary); }
.ez-mock__kanban { display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.5rem; }
.ez-mock__kanban > div { display: grid; gap: 0.375rem; align-content: start; padding: 0.5rem; border-radius: var(--r-md); background: var(--ez-surface-2); }
.ez-mock__kanban strong { font-size: var(--fs-18); color: var(--ez-text); }
.ez-mock__chip { height: 10px; border-radius: 4px; background: var(--ez-border-strong); }
```

- [ ] **Step 5: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/marketing/visuals.tsx frontend/src/marketing/visuals.test.tsx frontend/src/styles/marketing.css
git commit -m "feat(landing): add module screen mocks and flat layered hero visual"
```

---

### Task 4: Canlı ürün vitrini (`ProductShowcase`)

**Files:**
- Create: `frontend/src/marketing/ProductShowcase.tsx`
- Create: `frontend/src/marketing/ProductShowcase.test.tsx`
- Modify: `frontend/src/styles/marketing-motion.css` (vitrin stilleri)

**Interfaces:**
- Consumes: `modules`, `SHOWCASE_ORDER` (Task 2); `StatusBadge` (Task 2); `ModuleVisual` (Task 3); `usePrefersReducedMotion` (Task 1).
- Produces: `ProductShowcase({ intervalMs?: number })`, varsayılan `intervalMs` 6000.
  - `role="tablist"` (`aria-label="Modüller"`), 6 `role="tab"` (id `showcase-tab-<moduleId>`), `role="tabpanel"` (id `showcase-panel`).
  - Otomatik ilerleme yalnız şu koşulların hepsi sağlanınca çalışır: fare üstünde değil, odak içeride değil, kullanıcı henüz sekme seçmemiş, hareket azaltılmamış.

- [ ] **Step 1: Failing test** — `frontend/src/marketing/ProductShowcase.test.tsx`

```tsx
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { setReducedMotion } from "../test/intersectionObserver";
import { ProductShowcase } from "./ProductShowcase";

beforeEach(() => { vi.useFakeTimers(); });
afterEach(() => { vi.useRealTimers(); });

const selectedTab = () => screen.getAllByRole("tab").find((t) => t.getAttribute("aria-selected") === "true")!;

test("sekme listesi ARIA ile bağlıdır; ilk modül seçilidir", () => {
  render(<ProductShowcase />);
  expect(screen.getByRole("tablist", { name: "Modüller" })).toBeInTheDocument();
  const tabs = screen.getAllByRole("tab");
  expect(tabs).toHaveLength(6);
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
  expect(tabs[0]).toHaveAttribute("tabindex", "0");
  expect(tabs[1]).toHaveAttribute("tabindex", "-1");
  const panel = screen.getByRole("tabpanel");
  expect(panel).toHaveAttribute("aria-labelledby", "showcase-tab-personel");
  expect(panel).toHaveTextContent("Kullanımda");
});

test("belirli aralıkla bir sonraki modüle geçer", () => {
  render(<ProductShowcase intervalMs={6000} />);
  act(() => { vi.advanceTimersByTime(6000); });
  expect(selectedTab()).toHaveTextContent("İzin Yönetimi");
});

test("kullanıcı sekme seçince otomatik ilerleme durur", () => {
  render(<ProductShowcase intervalMs={6000} />);
  fireEvent.click(screen.getAllByRole("tab")[3]);
  expect(selectedTab()).toHaveTextContent("Bordro");
  expect(screen.getByRole("tabpanel")).toHaveTextContent("Pilotta");
  act(() => { vi.advanceTimersByTime(20000); });
  expect(selectedTab()).toHaveTextContent("Bordro");
});

test("ok, Home ve End tuşlarıyla gezinilir ve odak taşınır", () => {
  render(<ProductShowcase />);
  const tablist = screen.getByRole("tablist");
  fireEvent.keyDown(tablist, { key: "ArrowRight" });
  expect(selectedTab()).toHaveTextContent("İzin Yönetimi");
  expect(selectedTab()).toHaveFocus();
  fireEvent.keyDown(tablist, { key: "End" });
  expect(selectedTab()).toHaveTextContent("Risk Merkezi");
  expect(screen.getByRole("tabpanel")).toHaveTextContent("Geliştiriliyor");
  fireEvent.keyDown(tablist, { key: "ArrowRight" });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
  fireEvent.keyDown(tablist, { key: "ArrowLeft" });
  expect(selectedTab()).toHaveTextContent("Risk Merkezi");
  fireEvent.keyDown(tablist, { key: "Home" });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
});

test("fare üzerindeyken durur, ayrılınca devam eder", () => {
  const { container } = render(<ProductShowcase intervalMs={6000} />);
  const root = container.querySelector(".ez-showcase")!;
  fireEvent.mouseEnter(root);
  act(() => { vi.advanceTimersByTime(12000); });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
  fireEvent.mouseLeave(root);
  act(() => { vi.advanceTimersByTime(6000); });
  expect(selectedTab()).toHaveTextContent("İzin Yönetimi");
});

test("hareket azaltılmışsa otomatik ilerlemez", () => {
  setReducedMotion(true);
  render(<ProductShowcase intervalMs={6000} />);
  act(() => { vi.advanceTimersByTime(18000); });
  expect(selectedTab()).toHaveTextContent("Personel Yönetimi");
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing/ProductShowcase.test.tsx`
Expected: FAIL (modül yok)

- [ ] **Step 3: Uygulama** — `frontend/src/marketing/ProductShowcase.tsx`

```tsx
import { useEffect, useRef, useState, type FocusEvent, type KeyboardEvent } from "react";
import { SHOWCASE_ORDER, modules } from "./content";
import { StatusBadge } from "./StatusBadge";
import { ModuleVisual } from "./visuals";
import { usePrefersReducedMotion } from "./motion";

const items = SHOWCASE_ORDER.map((id) => {
  const found = modules.find((m) => m.id === id);
  if (!found) throw new Error(`Vitrin modülü bulunamadı: ${id}`);
  return found;
});

export function ProductShowcase({ intervalMs = 6000 }: { intervalMs?: number }) {
  const [active, setActive] = useState(0);
  const [hovered, setHovered] = useState(false);
  const [userSelected, setUserSelected] = useState(false);
  const reduced = usePrefersReducedMotion();
  const tabRefs = useRef<(HTMLButtonElement | null)[]>([]);
  const running = !reduced && !hovered && !userSelected;

  useEffect(() => {
    if (!running) return;
    const timer = window.setInterval(() => setActive((i) => (i + 1) % items.length), intervalMs);
    return () => window.clearInterval(timer);
  }, [running, intervalMs]);

  const select = (index: number, moveFocus = false) => {
    setActive(index);
    setUserSelected(true);
    if (moveFocus) tabRefs.current[index]?.focus();
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    const last = items.length - 1;
    const next =
      event.key === "ArrowRight" ? (active === last ? 0 : active + 1)
      : event.key === "ArrowLeft" ? (active === 0 ? last : active - 1)
      : event.key === "Home" ? 0
      : event.key === "End" ? last
      : null;
    if (next === null) return;
    event.preventDefault();
    select(next, true);
  };

  const onBlur = (event: FocusEvent<HTMLDivElement>) => {
    if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setHovered(false);
  };

  const current = items[active];
  return (
    <div className="ez-showcase" onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)} onFocus={() => setHovered(true)} onBlur={onBlur}>
      <div role="tablist" aria-label="Modüller" className="ez-showcase__tabs" onKeyDown={onKeyDown}>
        {items.map((m, i) => (
          <button
            key={m.id}
            ref={(el) => { tabRefs.current[i] = el; }}
            type="button"
            role="tab"
            id={`showcase-tab-${m.id}`}
            aria-selected={i === active}
            aria-controls="showcase-panel"
            tabIndex={i === active ? 0 : -1}
            className="ez-showcase__tab"
            onClick={() => select(i)}
          >
            <i aria-hidden="true" className={`fa-solid ${m.icon}`} />
            <span>{m.title}</span>
            {i === active && running && <span className="ez-showcase__progress" style={{ animationDuration: `${intervalMs}ms` }} aria-hidden="true" />}
          </button>
        ))}
      </div>
      <div role="tabpanel" id="showcase-panel" aria-labelledby={`showcase-tab-${current.id}`} className="ez-showcase__panel ez-spotlight">
        <div key={current.id} className="ez-showcase__content">
          <div className="ez-showcase__copy">
            <StatusBadge status={current.status} />
            <h3>{current.title}</h3>
            <p>{current.summary}</p>
            <ul className="ez-check-list">{current.points.map((p) => <li key={p}>{p}</li>)}</ul>
          </div>
          <div className="ez-showcase__screen"><ModuleVisual id={current.id} /></div>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: CSS** — `marketing-motion.css` sonuna (reduced-motion bloğundan ÖNCE eklenir; reduced-motion bloğuna `.ez-showcase__content, .ez-showcase__progress { animation: none; }` satırı eklenir):

```css
.ez-showcase { display: grid; gap: 1.25rem; }
.ez-showcase__tabs { display: flex; gap: 0.5rem; overflow-x: auto; padding-bottom: 0.25rem; scrollbar-width: thin; }
.ez-showcase__tab {
  position: relative; display: inline-flex; align-items: center; gap: 0.5rem; flex-shrink: 0; min-height: 44px; padding: 0 1rem;
  border: 1px solid var(--ez-border); border-radius: var(--r-full); background: var(--ez-surface); color: var(--ez-text-2);
  font: 600 var(--fs-14)/1 var(--ez-font-sans); cursor: pointer; overflow: hidden;
  transition: background-color var(--dur-base) var(--ease-out), color var(--dur-base) var(--ease-out), border-color var(--dur-base) var(--ease-out);
}
.ez-showcase__tab:hover { border-color: var(--indigo-200); color: var(--ez-text); }
.ez-showcase__tab[aria-selected="true"] { background: var(--ez-primary); border-color: var(--ez-primary); color: var(--ez-on-primary); }
.ez-showcase__tab:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-showcase__progress { position: absolute; left: 0; bottom: 0; height: 3px; width: 100%; background: color-mix(in srgb, var(--ez-on-primary) 70%, transparent); transform-origin: left; animation: ez-progress linear forwards; }
@keyframes ez-progress { from { transform: scaleX(0); } to { transform: scaleX(1); } }
.ez-showcase__panel { border: 1px solid var(--ez-border); border-radius: var(--r-xl); background: var(--ez-surface); box-shadow: var(--ez-shadow-md); padding: clamp(1.25rem, 3vw, 2rem); }
.ez-showcase__content { display: grid; grid-template-columns: 0.9fr 1.1fr; gap: clamp(1.5rem, 4vw, 3rem); align-items: center; animation: ez-panel-in 420ms var(--ease-out); }
.ez-showcase__copy { display: grid; gap: 0.75rem; justify-items: start; }
.ez-showcase__copy p { color: var(--ez-text-3); }
.ez-showcase__screen .ez-module-visual__stack { grid-template-columns: 1fr; }
@keyframes ez-panel-in { from { opacity: 0; transform: translateY(12px); } to { opacity: 1; transform: none; } }
@media (max-width: 860px) { .ez-showcase__content { grid-template-columns: 1fr; } }
```

- [ ] **Step 5: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run src/marketing/ProductShowcase.test.tsx`
Expected: 6 test PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/marketing/ProductShowcase.tsx frontend/src/marketing/ProductShowcase.test.tsx frontend/src/styles/marketing-motion.css
git commit -m "feat(landing): add accessible auto-advancing product showcase"
```

---

### Task 5: Bir çalışanın yolculuğu (`EmployeeJourney`)

**Files:**
- Create: `frontend/src/marketing/EmployeeJourney.tsx`
- Create: `frontend/src/marketing/EmployeeJourney.test.tsx`
- Modify: `frontend/src/styles/marketing-motion.css`

**Interfaces:**
- Consumes: `journeySteps` (Task 2); `useInView` (Task 1).
- Produces: `EmployeeJourney()`.
  - Adım listesi `<ol>`; aktif adım `aria-current="step"` taşır.
  - Yapışkan örnek personel kartı `<figure aria-label="Örnek personel kaydı">`. Aktif adıma kadarki alanlar dolu, sonrakiler "—".

- [ ] **Step 1: Failing test** — `frontend/src/marketing/EmployeeJourney.test.tsx`

```tsx
import { act, render, screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { triggerIntersections } from "../test/intersectionObserver";
import { EmployeeJourney } from "./EmployeeJourney";

test("başlangıçta ilk adım aktiftir ve yalnız ilk alan doludur", () => {
  render(<EmployeeJourney />);
  const steps = screen.getAllByRole("listitem");
  expect(steps).toHaveLength(5);
  expect(steps[0]).toHaveAttribute("aria-current", "step");
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("İşe alındı")).toBeInTheDocument();
  expect(within(card).queryByText("Hesaplamaya hazır")).toBeNull();
});

test("görünüme giren adım aktifleşir ve kart o adıma kadar dolar", () => {
  render(<EmployeeJourney />);
  act(() => triggerIntersections(true, (el) => el.textContent?.includes("Talepler aynı kayıt") ?? false));
  const steps = screen.getAllByRole("listitem");
  expect(steps[2]).toHaveAttribute("aria-current", "step");
  expect(steps[0]).not.toHaveAttribute("aria-current");
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("14 gün")).toBeInTheDocument();
  expect(within(card).queryByText("21 iş günü")).toBeNull();
});

test("son adıma gelindiğinde tüm alanlar dolar", () => {
  render(<EmployeeJourney />);
  act(() => triggerIntersections(true));
  const card = screen.getByRole("figure", { name: "Örnek personel kaydı" });
  expect(within(card).getByText("Hesaplamaya hazır")).toBeInTheDocument();
  expect(screen.getAllByRole("listitem")[4]).toHaveAttribute("aria-current", "step");
});
```

- [ ] **Step 2: Başarısız olduğunu doğrula**

Run: `npm --prefix frontend test -- --run src/marketing/EmployeeJourney.test.tsx`
Expected: FAIL (modül yok)

- [ ] **Step 3: Uygulama** — `frontend/src/marketing/EmployeeJourney.tsx`

```tsx
import { useCallback, useEffect, useState } from "react";
import { journeySteps } from "./content";
import { useInView } from "./motion";

type Step = (typeof journeySteps)[number];

function JourneyStep({ step, index, active, onActive }: { step: Step; index: number; active: boolean; onActive: (i: number) => void }) {
  const { ref, inView } = useInView<HTMLLIElement>({ threshold: 0.6, rootMargin: "-35% 0px -35% 0px" });
  useEffect(() => { if (inView) onActive(index); }, [inView, index, onActive]);
  return (
    <li ref={ref} className={`ez-journey__step${active ? " is-active" : ""}`} aria-current={active ? "step" : undefined}>
      <span className="ez-journey__dot" aria-hidden="true">{index + 1}</span>
      <div>
        <h3>{step.title}</h3>
        <p>{step.text}</p>
      </div>
    </li>
  );
}

export function EmployeeJourney() {
  const [active, setActive] = useState(0);
  const onActive = useCallback((i: number) => setActive(i), []);
  return (
    <div className="ez-journey">
      <div className="ez-journey__sticky">
        <figure className="ez-journey__card" aria-label="Örnek personel kaydı">
          <div className="ez-journey__person">
            <span className="ez-mock__avatar" aria-hidden="true">AY</span>
            <div><strong>Ahmet Yılmaz</strong><span>Yazılım · örnek kayıt</span></div>
          </div>
          <dl className="ez-journey__fields">
            {journeySteps.map((s, i) => (
              <div key={s.id} className={i <= active ? "is-filled" : undefined}>
                <dt>{s.field.label}</dt>
                <dd>{i <= active ? s.field.value : "—"}</dd>
              </div>
            ))}
          </dl>
        </figure>
      </div>
      <ol className="ez-journey__steps">
        {journeySteps.map((s, i) => <JourneyStep key={s.id} step={s} index={i} active={i === active} onActive={onActive} />)}
      </ol>
    </div>
  );
}
```

- [ ] **Step 4: CSS** — `marketing-motion.css` (reduced-motion bloğundan önce):

```css
.ez-journey { display: grid; grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr); gap: clamp(2rem, 5vw, 4rem); align-items: start; }
.ez-journey__sticky { position: sticky; top: 104px; }
.ez-journey__card { margin: 0; display: grid; gap: 1.25rem; padding: 1.5rem; border-radius: var(--r-xl); border: 1px solid var(--ez-border); background: var(--ez-surface); box-shadow: var(--ez-shadow-lg); }
.ez-journey__person { display: flex; gap: 0.75rem; align-items: center; }
.ez-journey__person strong { display: block; color: var(--ez-text); }
.ez-journey__person span:not(.ez-mock__avatar) { font-size: var(--fs-12); color: var(--ez-text-3); }
.ez-journey__fields { display: grid; gap: 0.5rem; }
.ez-journey__fields > div { display: flex; justify-content: space-between; gap: 1rem; padding: 0.625rem 0.75rem; border-radius: var(--r-md); background: var(--ez-surface-2); font-size: var(--fs-14); transition: background-color var(--dur-base) var(--ease-out); }
.ez-journey__fields dt { color: var(--ez-text-3); }
.ez-journey__fields dd { font-weight: 600; color: var(--ez-text-4); }
.ez-journey__fields .is-filled { background: var(--ez-primary-soft); }
.ez-journey__fields .is-filled dd { color: var(--ez-primary); animation: ez-panel-in 360ms var(--ease-out); }
.ez-journey__steps { list-style: none; display: grid; gap: clamp(2.5rem, 12vh, 7rem); padding-block: 2rem 30vh; }
.ez-journey__step { display: grid; grid-template-columns: 44px 1fr; gap: 1rem; opacity: 0.45; transition: opacity var(--dur-base) var(--ease-out); }
.ez-journey__step.is-active { opacity: 1; }
.ez-journey__step p { color: var(--ez-text-3); }
.ez-journey__dot { display: grid; place-items: center; width: 44px; height: 44px; border-radius: 50%; border: 2px solid var(--ez-border-strong); color: var(--ez-text-3); font-weight: 700; background: var(--ez-surface); transition: all var(--dur-base) var(--ease-out); }
.ez-journey__step.is-active .ez-journey__dot { border-color: var(--ez-primary); background: var(--ez-primary); color: var(--ez-on-primary); }
@media (max-width: 860px) {
  .ez-journey { grid-template-columns: 1fr; }
  .ez-journey__sticky { position: static; }
  .ez-journey__steps { padding-block: 0; gap: 1.5rem; }
  .ez-journey__step { opacity: 1; }
}
```

Reduced-motion bloğuna eklenir: `.ez-journey__step { opacity: 1; transition: none; } .ez-journey__fields .is-filled dd { animation: none; }`.

- [ ] **Step 5: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run src/marketing/EmployeeJourney.test.tsx`
Expected: 3 test PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/marketing/EmployeeJourney.tsx frontend/src/marketing/EmployeeJourney.test.tsx frontend/src/styles/marketing-motion.css
git commit -m "feat(landing): add sticky employee journey narrative"
```

---

### Task 6: Ana sayfa v2

**Files:**
- Modify: `frontend/src/marketing/pages/HomePage.tsx` (tamamen yeniden yazılır)
- Modify: `frontend/src/marketing/pages/HomePage.test.tsx`
- Modify: `frontend/src/styles/marketing.css` (hero/aurora, bento, yol haritası şeridi)

**Interfaces:**
- Consumes:
  - `ProductShowcase` (Task 4), `EmployeeJourney` (Task 5), `StatusBadge` (Task 2)
  - `Reveal`, `onSpotlightMove` (Task 1)
  - `HeroVisual` (Task 3)
  - `ROADMAP_STAGES`, `roadmap`, `modules`, `faqs`, `securityPillars`, `trustPoints` (content)
  - `CtaBand`, `FaqList`, `ModuleIcon` (sections)
- Produces: `HomePage()`.
  - Bölüm başlıkları (test ve erişilebilir bölge adları):
    - "Modülleri çalışırken görün"
    - "Bir çalışanın kaydı, işe alımdan bordroya"
    - "Birbirine bağlı altı modül"
    - "Ekipzen gelişmeye devam ediyor"
    - "Her şirketin verisi kendi veritabanında tutulur"
    - "Sık sorulan sorular"
  - Eski "Risk Merkezi" öne çıkan bölümü ve "Kullanıma geçiş üç adımda tamamlanır" bölümü kaldırılır.

- [ ] **Step 1: Testleri güncelle (failing)** — `HomePage.test.tsx` şu hale gelir:

```tsx
import { screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { renderPage } from "../../test/renderPage";
import { HomePage } from "./HomePage";
import { modules, roadmap } from "../content";

test("hero vaadi, iki CTA ve sayfa başlığı", () => {
  renderPage(<HomePage />);
  expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("İK süreçleriniz tek bir kayıt düzeninde");
  expect(screen.getAllByRole("link", { name: "Demo talep edin" })[0]).toHaveAttribute("href", "/iletisim");
  expect(screen.getByRole("link", { name: "Modülleri inceleyin" })).toHaveAttribute("href", "/ozellikler");
  expect(document.title).toBe("Ekipzen");
});

test("canlı vitrin ve çalışan yolculuğu bölümleri vardır", () => {
  renderPage(<HomePage />);
  const showcase = screen.getByRole("region", { name: "Modülleri çalışırken görün" });
  expect(within(showcase).getAllByRole("tab")).toHaveLength(6);
  expect(screen.getByRole("region", { name: "Bir çalışanın kaydı, işe alımdan bordroya" })).toBeInTheDocument();
});

test("altı modül kartı durum etiketiyle özellik sayfasına bağlanır", () => {
  renderPage(<HomePage />);
  const section = screen.getByRole("region", { name: "Birbirine bağlı altı modül" });
  for (const m of modules) {
    const link = within(section).getByRole("link", { name: new RegExp(m.title) });
    expect(link).toHaveAttribute("href", `/ozellikler#${m.id}`);
    expect(link.querySelector(".ez-status")).toHaveAttribute("data-status", m.status);
  }
});

test("gelişim şeridi yol haritasının ilk maddelerini ve tamamına bağlantıyı gösterir", () => {
  renderPage(<HomePage />);
  const strip = screen.getByRole("region", { name: "Ekipzen gelişmeye devam ediyor" });
  for (const stage of ["Şimdi", "Sıradaki", "Planlanan"]) expect(within(strip).getByText(stage)).toBeInTheDocument();
  expect(within(strip).getByText(roadmap[0].title)).toBeInTheDocument();
  expect(within(strip).getByRole("link", { name: "Yol haritasının tamamı" })).toHaveAttribute("href", "/hakkimizda#yol-haritasi");
});

test("güvenlik ve SSS bölümleri görünür; eski bölümler kaldırılmıştır", () => {
  renderPage(<HomePage />);
  expect(screen.getByRole("region", { name: "Her şirketin verisi kendi veritabanında tutulur" })).toBeInTheDocument();
  expect(screen.getByRole("region", { name: "Sık sorulan sorular" })).toBeInTheDocument();
  expect(screen.queryByRole("region", { name: "Kullanıma geçiş üç adımda tamamlanır" })).toBeNull();
  expect(document.querySelector(".ez-hero-visual__stack")).toBeNull();
});

test("sahte müşteri yorumu ya da logo şeridi yoktur", () => {
  renderPage(<HomePage />);
  expect(screen.queryByText(/müşterimiz|bize güvenen/i)).toBeNull();
});
```

Run: `npm --prefix frontend test -- --run src/marketing/pages/HomePage.test.tsx`
Expected: FAIL (yeni bölümler yok)

- [ ] **Step 2: `HomePage.tsx`**

```tsx
import { Link } from "react-router-dom";
import { ButtonLink, usePageMeta } from "../../ui";
import { ROADMAP_STAGES, faqs, modules, roadmap, securityPillars, trustPoints } from "../content";
import { CtaBand, FaqList, ModuleIcon } from "../sections";
import { HeroVisual } from "../visuals";
import { ProductShowcase } from "../ProductShowcase";
import { EmployeeJourney } from "../EmployeeJourney";
import { StatusBadge } from "../StatusBadge";
import { Reveal, onSpotlightMove } from "../motion";

export function HomePage() {
  usePageMeta("Ekipzen", "Özlük, izin, puantaj, bordro ve işe alım süreçlerinizi tek bir kayıt düzeninde yürütün. Ekipzen, İK ekipleri için tasarlanmış insan kaynakları yönetim platformudur.");
  return (
    <>
      <section className="ez-hero ez-aurora">
        <div className="ez-container ez-hero__grid">
          <Reveal className="ez-hero__copy">
            <p className="ez-eyebrow">İnsan kaynakları yönetimi</p>
            <h1>İK süreçleriniz tek bir kayıt düzeninde</h1>
            <p className="ez-lead">
              Özlük bilgileri, izinler, puantaj, bordro ve işe alım aynı veri üzerinde ilerler. Bir süreçte
              girilen bilgi, diğerlerinde yeniden girilmez.
            </p>
            <div className="ez-actions">
              <ButtonLink to="/iletisim" size="lg">Demo talep edin</ButtonLink>
              <ButtonLink to="/ozellikler" size="lg" variant="secondary">Modülleri inceleyin</ButtonLink>
            </div>
            <ul className="ez-trust" aria-label="Öne çıkan güvenceler">
              {trustPoints.map((t) => (
                <li key={t.label}><i aria-hidden="true" className={`fa-solid ${t.icon}`} />{t.label}</li>
              ))}
            </ul>
          </Reveal>
          <Reveal delay={150}><HeroVisual /></Reveal>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="vitrin-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Ürün</p>
            <h2 id="vitrin-baslik">Modülleri çalışırken görün</h2>
            <p className="ez-lead">Aşağıdaki ekranlar örnek verilerle hazırlanmış ürün görünümleridir.</p>
          </Reveal>
          <Reveal delay={100}><ProductShowcase /></Reveal>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="yolculuk-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Veri akışı</p>
            <h2 id="yolculuk-baslik">Bir çalışanın kaydı, işe alımdan bordroya</h2>
            <p className="ez-lead">Bilgi bir kez girilir; sonraki her süreç aynı kayıt üzerinden ilerler.</p>
          </Reveal>
          <EmployeeJourney />
        </div>
      </section>

      <section className="ez-section" aria-labelledby="moduller-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Modüller</p>
            <h2 id="moduller-baslik">Birbirine bağlı altı modül</h2>
            <p className="ez-lead">Tüm modüller aynı personel kaydı üzerinde çalışır. Her modülün durumu açıkça belirtilir.</p>
          </Reveal>
          <ul className="ez-bento">
            {modules.map((m, i) => (
              <Reveal as="li" key={m.id} delay={i * 70} className={`ez-bento__item ez-bento__item--${m.id}`}>
                <Link to={`/ozellikler#${m.id}`} className="ez-feature-card ez-spotlight" onPointerMove={onSpotlightMove}>
                  <div className="ez-feature-card__top"><ModuleIcon icon={m.icon} /><StatusBadge status={m.status} /></div>
                  <h3>{m.title}</h3>
                  <p>{m.summary}</p>
                  <span className="ez-feature-card__more">Detaylar <i aria-hidden="true" className="fa-solid fa-arrow-right" /></span>
                </Link>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="gelisim-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Gelişim</p>
            <h2 id="gelisim-baslik">Ekipzen gelişmeye devam ediyor</h2>
            <p className="ez-lead">Ürün, pilot müşterilerle birlikte doğrulanarak genişliyor. Yol haritasını açık tutuyoruz.</p>
          </Reveal>
          <ol className="ez-roadmap-strip">
            {ROADMAP_STAGES.map((stage, i) => (
              <Reveal as="li" key={stage.id} delay={i * 90} className={`ez-roadmap-strip__col ez-roadmap-strip__col--${stage.id}`}>
                <p className="ez-roadmap-strip__stage">{stage.title}</p>
                <p className="ez-roadmap-strip__sub">{stage.subtitle}</p>
                <ul>{roadmap.filter((r) => r.stage === stage.id).slice(0, 2).map((r) => <li key={r.title}>{r.title}</li>)}</ul>
              </Reveal>
            ))}
          </ol>
          <Link to="/hakkimizda#yol-haritasi" className="ez-link ez-roadmap-strip__more">Yol haritasının tamamı</Link>
        </div>
      </section>

      <section className="ez-section ez-section--dark" aria-labelledby="guvenlik-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Güvenlik</p>
            <h2 id="guvenlik-baslik">Her şirketin verisi kendi veritabanında tutulur</h2>
            <p className="ez-lead">Ekipzen, veri ayrımı ve en az yetki ilkesi üzerine kurulmuştur. Güvenlik önlemleri ürünün mimarisinin parçasıdır.</p>
          </Reveal>
          <ul className="ez-grid-3">
            {securityPillars.slice(0, 3).map((p, i) => (
              <Reveal as="li" key={p.title} delay={i * 80} className="ez-pillar">
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </Reveal>
            ))}
          </ul>
          <ButtonLink to="/guvenlik" variant="secondary">Güvenlik yaklaşımını inceleyin</ButtonLink>
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

- [ ] **Step 3: CSS** — `marketing.css` sonuna:

```css
.ez-hero.ez-aurora { background: var(--ez-surface); }
.ez-feature-card__top { display: flex; justify-content: space-between; align-items: center; gap: 0.75rem; }

.ez-bento { list-style: none; display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 1.25rem; }
.ez-bento__item { grid-column: span 2; }
.ez-bento__item--personel, .ez-bento__item--bordro { grid-column: span 3; }
.ez-bento__item--izin, .ez-bento__item--puantaj { grid-column: span 3; }
@media (max-width: 960px) { .ez-bento { grid-template-columns: repeat(2, minmax(0, 1fr)); } .ez-bento__item, .ez-bento__item--personel, .ez-bento__item--bordro, .ez-bento__item--izin, .ez-bento__item--puantaj { grid-column: span 1; } }
@media (max-width: 600px) { .ez-bento { grid-template-columns: 1fr; } }

.ez-roadmap-strip { list-style: none; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1.25rem; }
.ez-roadmap-strip__col { position: relative; display: grid; align-content: start; gap: 0.5rem; padding: 1.5rem; border-radius: var(--r-lg); background: var(--ez-surface); border: 1px solid var(--ez-border); }
.ez-roadmap-strip__col::before { content: ""; position: absolute; inset: 0 0 auto 0; height: 4px; border-radius: var(--r-lg) var(--r-lg) 0 0; background: var(--ez-border-strong); }
.ez-roadmap-strip__col--now::before { background: var(--ez-warning); }
.ez-roadmap-strip__col--next::before { background: var(--ez-primary); }
.ez-roadmap-strip__stage { font-weight: 700; color: var(--ez-text); }
.ez-roadmap-strip__sub { font-size: var(--fs-14); color: var(--ez-text-3); }
.ez-roadmap-strip__col ul { list-style: none; display: grid; gap: 0.5rem; margin-top: 0.5rem; }
.ez-roadmap-strip__col li { font-size: var(--fs-14); color: var(--ez-text-2); padding-left: 1rem; position: relative; }
.ez-roadmap-strip__col li::before { content: ""; position: absolute; left: 0; top: 0.55em; width: 6px; height: 6px; border-radius: 50%; background: var(--ez-primary); }
.ez-roadmap-strip__more { display: inline-flex; min-height: 44px; align-items: center; margin-top: 1rem; }
@media (max-width: 860px) { .ez-roadmap-strip { grid-template-columns: 1fr; } }
```

Eski `.ez-steps*` kuralları kullanılmıyorsa kaldırılır. Kontrol:

Run: `grep -rn "ez-steps" frontend/src --include=*.tsx`
Expected: çıktı yok

`content.ts`'deki `steps` artık kullanılmıyorsa silinir.

Run: `grep -rn "steps\b" frontend/src/marketing --include=*.tsx`
Expected: yalnız `journeySteps` / `devProcess` kullanımları

- [ ] **Step 4: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS. `routes.test.tsx` ve `App.test.tsx` hero h1'ini aramaya devam eder; değişmez.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/marketing frontend/src/styles/marketing.css
git commit -m "feat(landing): rebuild home page as a live product showcase"
```

---

### Task 7: Özellikler sayfası v2 ve hash kaydırma düzeltmesi

**Files:**
- Create: `frontend/src/marketing/StickyModuleNav.tsx`, `frontend/src/marketing/DataFlowDiagram.tsx`
- Create: `frontend/src/marketing/motion/useScrollToHash.ts` (+ `motion/index.ts` exportu)
- Create: `frontend/src/marketing/features.test.tsx`
- Modify: `frontend/src/marketing/pages/FeaturesPage.tsx` (tamamen yeniden yazılır)
- Modify: `frontend/src/marketing/MarketingLayout.tsx` (hash kaydırma)
- Modify: `frontend/src/marketing/MarketingLayout.test.tsx`
- Modify: `frontend/src/marketing/pages/pages.test.tsx` (Özellikler testi)
- Modify: `frontend/src/styles/marketing-motion.css`, `frontend/src/styles/marketing.css`

**Interfaces:**
- Consumes: `modules`, `roleViews`, `STATUS_META` (content); `StatusBadge`; `ModuleVisual`; `Reveal`, `useInView`, `onSpotlightMove`; `PageHero`, `CtaBand`, `ModuleIcon`.
- Produces:
  - `StickyModuleNav({ items: { id: string; title: string; status: ModuleStatus }[]; activeId: string })`
    - `<nav aria-label="Modül menüsü">`; her link `href="#<id>"`.
    - Aktif link `aria-current="true"` taşır.
    - `--progress` değişkeni `(aktifIndex + 1) / n` değerini alır.
  - `DataFlowDiagram()`: `<svg role="img" aria-labelledby>` başlığı "Modüller ve personel kaydı ilişkisi". Görünür olunca kapsayıcı `is-drawn` sınıfını alır.
  - `useScrollToHash(): void` — konumda hash varsa bir sonraki karede ilgili öğeye `scrollIntoView({ block: "start" })` uygular. Hareket azaltılmışsa `behavior: "auto"`, değilse `"smooth"` kullanılır. `MarketingLayout`, `FeaturesPage` ve `AboutPage` tarafından çağrılır.

- [ ] **Step 1: Failing testler**

`frontend/src/marketing/features.test.tsx`:

```tsx
import { act, render, screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { triggerIntersections } from "../test/intersectionObserver";
import { StickyModuleNav } from "./StickyModuleNav";
import { DataFlowDiagram } from "./DataFlowDiagram";
import { modules } from "./content";

const items = modules.map((m) => ({ id: m.id, title: m.title, status: m.status }));

test("yapışkan menü tüm modüllere bağlanır ve aktif olanı işaretler", () => {
  render(<StickyModuleNav items={items} activeId="bordro" />);
  const nav = screen.getByRole("navigation", { name: "Modül menüsü" });
  const links = within(nav).getAllByRole("link");
  expect(links).toHaveLength(6);
  expect(within(nav).getByRole("link", { name: /Bordro/ })).toHaveAttribute("href", "#bordro");
  expect(within(nav).getByRole("link", { name: /Bordro/ })).toHaveAttribute("aria-current", "true");
  expect(within(nav).getByRole("link", { name: /Personel/ })).not.toHaveAttribute("aria-current");
  const index = modules.findIndex((m) => m.id === "bordro");
  expect((nav as HTMLElement).style.getPropertyValue("--progress")).toBe(String((index + 1) / modules.length));
});

test("veri akışı şeması erişilebilir adla render edilir ve görünür olunca çizilir", () => {
  const { container } = render(<DataFlowDiagram />);
  expect(screen.getByRole("img", { name: "Modüller ve personel kaydı ilişkisi" })).toBeInTheDocument();
  const wrapper = container.querySelector(".ez-flow")!;
  expect(wrapper).not.toHaveClass("is-drawn");
  act(() => triggerIntersections(true));
  expect(wrapper).toHaveClass("is-drawn");
});
```

`frontend/src/marketing/pages/pages.test.tsx` içindeki Özellikler testi şu hale gelir (importlara `act` ve `triggerIntersections` eklenir):

```tsx
test("Özellikler: her modül anchor id'li bir bölümdür; yapışkan menü görünen modülü izler", () => {
  renderPage(<FeaturesPage />);
  expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  for (const m of modules) {
    const section = document.getElementById(m.id);
    expect(section, m.id).not.toBeNull();
    expect(within(section!).getByRole("heading", { level: 2, name: m.title })).toBeInTheDocument();
    expect(section!.querySelector(".ez-status")).toHaveAttribute("data-status", m.status);
  }
  const nav = screen.getByRole("navigation", { name: "Modül menüsü" });
  act(() => triggerIntersections(true, (el) => el.id === "bordro"));
  expect(within(nav).getByRole("link", { name: /Bordro/ })).toHaveAttribute("aria-current", "true");
  expect(screen.getByRole("heading", { name: "İK yöneticisi, yönetici ve çalışan için ayrı çalışma alanları" })).toBeInTheDocument();
  expect(screen.getByRole("heading", { name: "Tüm modüller aynı personel kaydına bağlıdır" })).toBeInTheDocument();
  expect(document.title).toBe("Özellikler · Ekipzen");
});
```

`MarketingLayout.test.tsx` sonuna:

```tsx
test("adreste hash varsa ilgili bölüme kaydırır", async () => {
  const scrollIntoView = vi.fn();
  Element.prototype.scrollIntoView = scrollIntoView;
  const router = createMemoryRouter(
    [{ element: <MarketingLayout />, children: [{ path: "/ozellikler", element: <section id="bordro">bordro</section> }] }],
    { initialEntries: ["/ozellikler#bordro"] },
  );
  render(
    <QueryClientProvider client={new QueryClient()}>
      <AuthProvider><RouterProvider router={router} /></AuthProvider>
    </QueryClientProvider>,
  );
  await waitFor(() => expect(scrollIntoView).toHaveBeenCalled());
});
```

(`vi` ve `waitFor` importları eklenir; `requestAnimationFrame` jsdom'da asenkrondur.)

Run: `npm --prefix frontend test -- --run src/marketing/features.test.tsx src/marketing/pages/pages.test.tsx src/marketing/MarketingLayout.test.tsx`
Expected: FAIL

- [ ] **Step 2: `StickyModuleNav.tsx`**

```tsx
import type { CSSProperties } from "react";
import type { ModuleStatus } from "./content";
import { STATUS_META } from "./content";

export function StickyModuleNav({ items, activeId }: { items: { id: string; title: string; status: ModuleStatus }[]; activeId: string }) {
  const index = Math.max(0, items.findIndex((i) => i.id === activeId));
  const style = { "--progress": String((index + 1) / items.length) } as CSSProperties;
  return (
    <nav aria-label="Modül menüsü" className="ez-module-nav" style={style}>
      <span className="ez-module-nav__track" aria-hidden="true"><span className="ez-module-nav__fill" /></span>
      <ol>
        {items.map((item) => (
          <li key={item.id}>
            <a href={`#${item.id}`} aria-current={item.id === activeId ? "true" : undefined} className="ez-module-nav__link">
              <span className={`ez-dot ez-dot--${item.status}`} aria-hidden="true" />
              <span>{item.title}</span>
              <span className="ez-sr-only"> ({STATUS_META[item.status].label})</span>
            </a>
          </li>
        ))}
      </ol>
    </nav>
  );
}
```

- [ ] **Step 3: `DataFlowDiagram.tsx`**

```tsx
import { modules } from "./content";
import { useInView } from "./motion";

const CENTER = { x: 300, y: 190 };
const POSITIONS = [
  { x: 110, y: 70 }, { x: 490, y: 70 }, { x: 70, y: 190 },
  { x: 530, y: 190 }, { x: 110, y: 310 }, { x: 490, y: 310 },
];

export function DataFlowDiagram() {
  const { ref, inView } = useInView<HTMLDivElement>({ once: true, threshold: 0.3 });
  return (
    <div ref={ref} className={`ez-flow${inView ? " is-drawn" : ""}`}>
      <svg viewBox="0 0 600 380" role="img" aria-labelledby="flow-title flow-desc">
        <title id="flow-title">Modüller ve personel kaydı ilişkisi</title>
        <desc id="flow-desc">Altı modül de ortadaki personel kaydına bağlıdır; bilgi bir kez girilir ve tüm modüllerde kullanılır.</desc>
        {modules.map((m, i) => (
          <path key={`l-${m.id}`} className="ez-flow__line" style={{ transitionDelay: `${i * 120}ms` }}
            d={`M ${CENTER.x} ${CENTER.y} L ${POSITIONS[i].x} ${POSITIONS[i].y}`} pathLength={1} />
        ))}
        <circle className="ez-flow__core-ring" cx={CENTER.x} cy={CENTER.y} r={62} />
        <circle className="ez-flow__core" cx={CENTER.x} cy={CENTER.y} r={52} />
        <text className="ez-flow__core-text" x={CENTER.x} y={CENTER.y - 4} textAnchor="middle">Personel</text>
        <text className="ez-flow__core-text" x={CENTER.x} y={CENTER.y + 16} textAnchor="middle">kaydı</text>
        {modules.map((m, i) => (
          <g key={`n-${m.id}`} className="ez-flow__node">
            <rect x={POSITIONS[i].x - 70} y={POSITIONS[i].y - 22} width={140} height={44} rx={12} />
            <text x={POSITIONS[i].x} y={POSITIONS[i].y + 5} textAnchor="middle">{m.title}</text>
          </g>
        ))}
      </svg>
    </div>
  );
}
```

- [ ] **Step 4: `FeaturesPage.tsx`**

```tsx
import { useCallback, useEffect, useState } from "react";
import { ButtonLink, usePageMeta } from "../../ui";
import { modules, roleViews, type MarketingModule } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";
import { ModuleVisual } from "../visuals";
import { StatusBadge } from "../StatusBadge";
import { StickyModuleNav } from "../StickyModuleNav";
import { DataFlowDiagram } from "../DataFlowDiagram";
import { Reveal, onSpotlightMove, useInView } from "../motion";

function ModuleBlock({ m, onActive }: { m: MarketingModule; onActive: (id: string) => void }) {
  const { ref, inView } = useInView<HTMLElement>({ threshold: 0.3, rootMargin: "-30% 0px -50% 0px" });
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
            <a key={m.id} href={`#${m.id}`} className="ez-chip"><span className={`ez-dot ez-dot--${m.status}`} aria-hidden="true" />{m.title}</a>
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
```

- [ ] **Step 5: Hash kaydırma**

SPA içinde `/sayfa#bolum` bağlantılarında router hash'e kaydırmaz. Lazy sayfalarda hedef öğe ilk karede henüz yoktur. Bu yüzden kaydırma bir hook'ta yapılır ve hem `MarketingLayout` hem de hedef bölüm barındıran sayfalar (`FeaturesPage`, Task 9'da `AboutPage`) mount olduklarında onu çağırır. `frontend/src/marketing/motion/useScrollToHash.ts`:

```ts
import { useEffect } from "react";
import { useLocation } from "react-router-dom";
import { usePrefersReducedMotion } from "./usePrefersReducedMotion";

export function useScrollToHash() {
  const { pathname, hash } = useLocation();
  const reduced = usePrefersReducedMotion();
  useEffect(() => {
    if (!hash) return;
    const id = decodeURIComponent(hash.slice(1));
    const frame = requestAnimationFrame(() => {
      document.getElementById(id)?.scrollIntoView({ behavior: reduced ? "auto" : "smooth", block: "start" });
    });
    return () => cancelAnimationFrame(frame);
  }, [pathname, hash, reduced]);
}
```

`motion/index.ts`'e `export { useScrollToHash } from "./useScrollToHash";` eklenir.

Hook şu yerlerde çağrılır:
- `MarketingLayout` gövdesinde, `useEffect(() => setMenuOpen(false), …)` satırının altında: `useScrollToHash();`. Import: `import { useScrollToHash } from "./motion";`.
- `FeaturesPage` fonksiyonunun başında: `useScrollToHash();`. Mevcut `../motion` importuna eklenir.

- [ ] **Step 6: CSS** — `marketing.css` sonuna:

```css
.ez-dot { display: inline-block; width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
.ez-dot--active { background: var(--ez-success); }
.ez-dot--pilot { background: var(--ez-warning); }
.ez-dot--building { background: var(--ez-primary); }
.ez-chip { gap: 0.5rem; }

.ez-features-layout { display: grid; grid-template-columns: 240px minmax(0, 1fr); gap: clamp(2rem, 4vw, 3.5rem); padding-block: 2rem 1rem; }
.ez-features-layout__nav { position: relative; }
.ez-module-nav { position: sticky; top: 104px; display: grid; grid-template-columns: 3px 1fr; gap: 1rem; }
.ez-module-nav__track { position: relative; border-radius: 3px; background: var(--ez-border); }
.ez-module-nav__fill { position: absolute; inset: 0 0 auto 0; height: calc(var(--progress, 0) * 100%); border-radius: 3px; background: var(--ez-primary); transition: height var(--dur-base) var(--ease-out); }
.ez-module-nav ol { list-style: none; display: grid; gap: 0.25rem; }
.ez-module-nav__link { display: flex; align-items: center; gap: 0.625rem; min-height: 44px; padding: 0 0.75rem; border-radius: var(--r-md); color: var(--ez-text-3); font-weight: 600; font-size: var(--fs-14); text-decoration: none; }
.ez-module-nav__link:hover { color: var(--ez-text); background: var(--ez-surface-2); }
.ez-module-nav__link[aria-current="true"] { color: var(--ez-primary); background: var(--ez-primary-soft); }
.ez-module-nav__link:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-feature-block { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: clamp(1.5rem, 4vw, 3rem); align-items: center; padding-block: clamp(2.5rem, 6vw, 4.5rem); border-bottom: 1px solid var(--ez-border-soft); }
.ez-feature-block:last-child { border-bottom: 0; }
.ez-feature-block__copy { display: grid; gap: 0.875rem; justify-items: start; }
.ez-feature-block__head { display: flex; align-items: center; gap: 0.75rem; }
.ez-feature-block__visual { border-radius: var(--r-xl); padding: 1.25rem; background: linear-gradient(160deg, var(--ez-primary-soft), var(--ez-surface-2)); }
.ez-roles { list-style: none; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1.25rem; }
.ez-roles__item--wide { grid-column: span 2; background: linear-gradient(160deg, var(--ez-primary-soft), var(--ez-surface)); }
@media (max-width: 960px) {
  .ez-features-layout { grid-template-columns: 1fr; padding-top: 0; }
  .ez-module-nav { position: sticky; top: 72px; z-index: 5; display: block; margin-inline: calc(-1 * clamp(1rem, 4vw, 2rem)); padding: 0.5rem clamp(1rem, 4vw, 2rem); background: color-mix(in srgb, var(--ez-surface) 92%, transparent); backdrop-filter: blur(8px); border-bottom: 1px solid var(--ez-border); }
  .ez-module-nav__track { display: none; }
  .ez-module-nav ol { display: flex; overflow-x: auto; gap: 0.25rem; }
  .ez-module-nav__link { white-space: nowrap; }
  .ez-feature-block { grid-template-columns: 1fr; }
  .ez-roles { grid-template-columns: 1fr; }
  .ez-roles__item--wide { grid-column: auto; }
}

.ez-flow svg { width: 100%; height: auto; }
.ez-flow__line { fill: none; stroke: var(--indigo-300); stroke-width: 2; stroke-dasharray: 1; stroke-dashoffset: 1; transition: stroke-dashoffset 900ms var(--ease-out); }
.ez-flow.is-drawn .ez-flow__line { stroke-dashoffset: 0; }
.ez-flow__core { fill: var(--ez-primary); }
.ez-flow__core-ring { fill: none; stroke: var(--indigo-200); stroke-width: 10; transform-origin: 300px 190px; animation: ez-pulse 3.2s ease-in-out infinite; }
.ez-flow__core-text { fill: var(--ez-on-primary); font: 700 15px var(--ez-font-sans); }
.ez-flow__node rect { fill: var(--ez-surface); stroke: var(--ez-border-strong); }
.ez-flow__node text { fill: var(--ez-text); font: 600 13px var(--ez-font-sans); }
@keyframes ez-pulse { 0%, 100% { transform: scale(1); opacity: 0.9; } 50% { transform: scale(1.08); opacity: 0.4; } }
```

`marketing-motion.css` reduced-motion bloğuna eklenir: `.ez-flow__line { transition: none; stroke-dashoffset: 0; } .ez-flow__core-ring { animation: none; }`.

- [ ] **Step 7: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 8: Commit**

```bash
git add frontend/src/marketing frontend/src/styles
git commit -m "feat(landing): rebuild features page with sticky module nav and data flow diagram"
```

---

### Task 8: Güvenlik sayfası v2

**Files:**
- Create: `frontend/src/marketing/SecurityLayers.tsx`, `frontend/src/marketing/SecurityLayers.test.tsx`
- Modify: `frontend/src/marketing/pages/SecurityPage.tsx` (tamamen yeniden yazılır)
- Modify: `frontend/src/marketing/pages/pages.test.tsx` (Güvenlik testi)
- Modify: `frontend/src/styles/marketing.css`, `frontend/src/styles/marketing-motion.css`

**Interfaces:**
- Consumes: `securityLayers`, `securityCategories`, `transparency` (content); `StatusBadge`; `Reveal`, `onSpotlightMove`; `PageHero`, `CtaBand`, `ModuleIcon`.
- Produces: `SecurityLayers()`.
  - `<ol className="ez-layers">`. Her katman bir `<h3>` içinde bir `<button aria-expanded aria-controls>` ile bir `role="region"` panelden oluşur.
  - Aynı anda tek katman açıktır; başlangıçta ilki açıktır.

- [ ] **Step 1: Failing testler** — `frontend/src/marketing/SecurityLayers.test.tsx`

```tsx
import { fireEvent, render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { SecurityLayers } from "./SecurityLayers";
import { securityLayers } from "./content";

test("beş katman listelenir; ilki açıktır", () => {
  render(<SecurityLayers />);
  const buttons = screen.getAllByRole("button");
  expect(buttons).toHaveLength(5);
  expect(buttons[0]).toHaveAttribute("aria-expanded", "true");
  expect(screen.getByText(securityLayers[0].text)).toBeVisible();
  expect(screen.getByText(securityLayers[3].text)).not.toBeVisible();
});

test("bir katman seçildiğinde açıklaması açılır, diğeri kapanır", () => {
  render(<SecurityLayers />);
  fireEvent.click(screen.getByRole("button", { name: /Şirkete ait veritabanı/ }));
  expect(screen.getByRole("button", { name: /Şirkete ait veritabanı/ })).toHaveAttribute("aria-expanded", "true");
  expect(screen.getByText(securityLayers[3].text)).toBeVisible();
  expect(screen.getAllByRole("button")[0]).toHaveAttribute("aria-expanded", "false");
  expect(screen.getByText(securityLayers[0].text)).not.toBeVisible();
});
```

`pages.test.tsx` Güvenlik testi şu hale gelir:

```tsx
test("Güvenlik: katmanlar, kategoriler, şeffaflık ve KVKK bölümü", () => {
  renderPage(<SecurityPage />);
  expect(screen.getByRole("region", { name: "Katman katman güvenlik" })).toBeInTheDocument();
  const categories = screen.getByRole("region", { name: "Kontroller kategoriye göre" });
  expect(within(categories).getAllByRole("heading", { level: 3 })).toHaveLength(4);
  const transparencyRegion = screen.getByRole("region", { name: "Bugün sağlananlar ve üzerinde çalıştıklarımız" });
  expect(within(transparencyRegion).getByText("Çok faktörlü kimlik doğrulama")).toBeInTheDocument();
  expect(within(transparencyRegion).getAllByText("Geliştiriliyor")).toHaveLength(2);
  expect(screen.getByRole("heading", { name: "KVKK ve kişisel veriler" })).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "KVKK aydınlatma metni" })).toHaveAttribute("href", "/kvkk-aydinlatma");
});
```

Run: `npm --prefix frontend test -- --run src/marketing/SecurityLayers.test.tsx src/marketing/pages/pages.test.tsx`
Expected: FAIL

- [ ] **Step 2: `SecurityLayers.tsx`**

```tsx
import { useState, type CSSProperties } from "react";
import { securityLayers } from "./content";

export function SecurityLayers() {
  const [openId, setOpenId] = useState(securityLayers[0].id);
  return (
    <ol className="ez-layers">
      {securityLayers.map((layer, i) => {
        const expanded = openId === layer.id;
        return (
          <li key={layer.id} className={`ez-layers__item${expanded ? " is-open" : ""}`} style={{ "--layer-index": i } as CSSProperties}>
            <h3 className="ez-layers__heading">
              <button type="button" id={`layer-btn-${layer.id}`} aria-expanded={expanded} aria-controls={`layer-${layer.id}`} className="ez-layers__button" onClick={() => setOpenId(layer.id)}>
                <span className="ez-layers__num" aria-hidden="true">{i + 1}</span>
                <span className="ez-layers__title">{layer.title}</span>
                <span className="ez-layers__short">{layer.short}</span>
              </button>
            </h3>
            <div id={`layer-${layer.id}`} role="region" aria-labelledby={`layer-btn-${layer.id}`} className="ez-layers__panel" hidden={!expanded}>
              <p>{layer.text}</p>
            </div>
          </li>
        );
      })}
    </ol>
  );
}
```

- [ ] **Step 3: `SecurityPage.tsx`**

```tsx
import { Link } from "react-router-dom";
import { usePageMeta } from "../../ui";
import { securityCategories, transparency } from "../content";
import { CtaBand, ModuleIcon, PageHero } from "../sections";
import { SecurityLayers } from "../SecurityLayers";
import { StatusBadge } from "../StatusBadge";
import { Reveal, onSpotlightMove } from "../motion";

export function SecurityPage() {
  usePageMeta("Güvenlik ve KVKK", "Ekipzen'de veri ayrımı, rol bazlı yetkilendirme, şifreleme ve kişisel verilerin korunmasına ilişkin yaklaşım.");
  return (
    <>
      <PageHero
        eyebrow="Güvenlik"
        title="İK verisinin hassasiyetine göre kurulmuş bir mimari"
        lead="Ekipzen, çalışan verisinin bir şirketin en hassas verileri arasında yer aldığı gözetilerek veri ayrımı ve en az yetki ilkeleri üzerine kurulmuştur."
      />

      <section className="ez-section ez-section--flush" aria-labelledby="katman-baslik">
        <div className="ez-container ez-security-layers">
          <Reveal className="ez-security-layers__intro">
            <p className="ez-eyebrow">Mimari</p>
            <h2 id="katman-baslik">Katman katman güvenlik</h2>
            <p className="ez-lead">Bir isteğin verinize ulaşmadan önce geçtiği katmanlar. Ayrıntısını görmek için bir katman seçin.</p>
            <div className="ez-core" aria-hidden="true"><i className="fa-solid fa-shield-halved" /></div>
          </Reveal>
          <Reveal delay={120}><SecurityLayers /></Reveal>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="kategori-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Kontroller</p>
            <h2 id="kategori-baslik">Kontroller kategoriye göre</h2>
          </Reveal>
          <ul className="ez-grid-2 ez-categories">
            {securityCategories.map((c, i) => (
              <Reveal as="li" key={c.id} delay={i * 80} className="ez-pillar ez-spotlight" onPointerMove={onSpotlightMove}>
                <ModuleIcon icon={c.icon} />
                <h3>{c.title}</h3>
                <ul className="ez-check-list">{c.items.map((item) => <li key={item}>{item}</li>)}</ul>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="seffaflik-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <p className="ez-eyebrow">Şeffaflık</p>
            <h2 id="seffaflik-baslik">Bugün sağlananlar ve üzerinde çalıştıklarımız</h2>
            <p className="ez-lead">Güvenlik yaklaşımımızı, bugün ürünün sağladıklarıyla ve henüz geliştirmekte olduklarımızla birlikte açıkça paylaşıyoruz.</p>
          </Reveal>
          <div className="ez-transparency">
            <Reveal className="ez-transparency__col">
              <h3>Bugün sağlanan</h3>
              <ul className="ez-check-list">{transparency.provided.map((item) => <li key={item}>{item}</li>)}</ul>
            </Reveal>
            <Reveal delay={120} className="ez-transparency__col ez-transparency__col--progress">
              <h3>Üzerinde çalışılan</h3>
              <ul className="ez-transparency__list">
                {transparency.inProgress.map((item) => <li key={item}><span>{item}</span><StatusBadge status="building" /></li>)}
              </ul>
            </Reveal>
          </div>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="kvkk-baslik">
        <div className="ez-container ez-narrow ez-prose">
          <h2 id="kvkk-baslik">KVKK ve kişisel veriler</h2>
          <p>
            Ekipzen, 6698 sayılı Kişisel Verilerin Korunması Kanunu kapsamındaki yükümlülüklerinizi yerine getirmenizi
            destekleyecek şekilde tasarlanmıştır. Erişim rol bazlıdır, veriler şirket başına ayrı tutulur ve her
            kullanıcı kendi kişisel verisini dışa aktarabilir.
          </p>
          <p>
            Ziyaretçi ve demo talebi verilerinin nasıl işlendiğini{" "}
            <Link to="/kvkk-aydinlatma">KVKK aydınlatma metni</Link> sayfasında bulabilirsiniz.
          </p>
        </div>
      </section>
      <CtaBand title="Güvenlik gereksinimlerinizi birlikte değerlendirelim" text="Bilgi güvenliği ekibinizin sorularını demo görüşmesinde ayrıntılı olarak yanıtlayabiliriz." />
    </>
  );
}
```

- [ ] **Step 4: CSS** — `marketing.css` sonuna:

```css
.ez-security-layers { display: grid; grid-template-columns: minmax(0, 0.85fr) minmax(0, 1.15fr); gap: clamp(2rem, 5vw, 4rem); align-items: start; }
.ez-security-layers__intro { display: grid; gap: 1rem; justify-items: start; position: sticky; top: 104px; }
.ez-core { position: relative; display: grid; place-items: center; width: 120px; height: 120px; margin-top: 1rem; border-radius: 50%; background: var(--ez-primary); color: var(--ez-on-primary); font-size: 2.5rem; }
.ez-core::before, .ez-core::after { content: ""; position: absolute; inset: -14px; border-radius: 50%; border: 2px solid var(--indigo-200); animation: ez-ring 3.6s ease-out infinite; }
.ez-core::after { animation-delay: 1.8s; }
@keyframes ez-ring { from { transform: scale(0.85); opacity: 0.9; } to { transform: scale(1.35); opacity: 0; } }
.ez-layers { list-style: none; display: grid; gap: 0.625rem; }
.ez-layers__item { margin-inline: calc(var(--layer-index) * clamp(0.25rem, 1.5vw, 1rem)); border-radius: var(--r-lg); border: 1px solid var(--ez-border); background: color-mix(in srgb, var(--ez-primary) calc(var(--layer-index) * 3%), var(--ez-surface)); transition: box-shadow var(--dur-base) var(--ease-out), border-color var(--dur-base) var(--ease-out); }
.ez-layers__item.is-open { border-color: var(--indigo-300); box-shadow: var(--ez-shadow-md); }
.ez-layers__heading { margin: 0; }
.ez-layers__button { width: 100%; display: grid; grid-template-columns: 36px 1fr auto; align-items: center; gap: 0.75rem; min-height: 56px; padding: 0.75rem 1rem; border: 0; background: transparent; text-align: left; cursor: pointer; font: inherit; color: var(--ez-text); border-radius: var(--r-lg); }
.ez-layers__button:focus-visible { outline: none; box-shadow: var(--ez-focus-ring); }
.ez-layers__num { display: grid; place-items: center; width: 36px; height: 36px; border-radius: 50%; background: var(--ez-primary-soft); color: var(--ez-primary); font-weight: 700; }
.ez-layers__item.is-open .ez-layers__num { background: var(--ez-primary); color: var(--ez-on-primary); }
.ez-layers__title { font-weight: 700; }
.ez-layers__short { font-size: var(--fs-12); color: var(--ez-text-3); }
.ez-layers__panel { padding: 0 1rem 1rem calc(1rem + 36px + 0.75rem); color: var(--ez-text-2); animation: ez-panel-in 320ms var(--ease-out); }
.ez-categories { margin-top: 0.5rem; }
.ez-transparency { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1.25rem; }
.ez-transparency__col { display: grid; align-content: start; gap: 1rem; padding: 1.75rem; border-radius: var(--r-xl); border: 1px solid var(--ez-border); background: var(--ez-surface); }
.ez-transparency__col--progress { background: var(--ez-bg); border-style: dashed; }
.ez-transparency__list { list-style: none; display: grid; gap: 0.75rem; }
.ez-transparency__list li { display: flex; justify-content: space-between; align-items: center; gap: 1rem; color: var(--ez-text-2); }
@media (max-width: 860px) {
  .ez-security-layers, .ez-transparency { grid-template-columns: 1fr; }
  .ez-security-layers__intro { position: static; }
  .ez-core { display: none; }
  .ez-layers__item { margin-inline: 0; }
  .ez-layers__short { display: none; }
  .ez-layers__panel { padding-left: 1rem; }
}
```

`marketing-motion.css` reduced-motion bloğuna eklenir: `.ez-core::before, .ez-core::after, .ez-layers__panel { animation: none; }`.

- [ ] **Step 5: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/marketing frontend/src/styles
git commit -m "feat(landing): rebuild security page with interactive layers and transparency table"
```

---

### Task 9: Hakkımızda sayfası v2

**Files:**
- Create: `frontend/src/marketing/ProcessTimeline.tsx`, `frontend/src/marketing/ModuleMaturity.tsx`, `frontend/src/marketing/RoadmapBoard.tsx`
- Create: `frontend/src/marketing/about.test.tsx`
- Modify: `frontend/src/marketing/pages/AboutPage.tsx` (tamamen yeniden yazılır)
- Modify: `frontend/src/marketing/pages/pages.test.tsx` (Hakkımızda testi)
- Modify: `frontend/src/styles/marketing.css`, `frontend/src/styles/marketing-motion.css`

**Interfaces:**
- Consumes:
  - content: `devProcess`, `modules`, `STATUS_META`, `ROADMAP_STAGES`, `roadmap`, `ROADMAP_NOTE`, `principles`, `commitments`, `aboutScenes`
  - `StatusBadge`
  - motion: `Reveal`, `useInView`, `onSpotlightMove`, `useScrollToHash`
  - sections: `CtaBand`, `ModuleIcon`
- Produces:
  - `ProcessTimeline()`: `<ol className="ez-process">`, 6 adım. Görünür olunca `is-active` sınıfını alır ve çizgi dolar.
  - `ModuleMaturity()`: `active` → `pilot` → `building` sırasıyla üç sütun. Her sütun `<section aria-labelledby>`; başlık `STATUS_META[x].label`; altında modül adları listelenir.
  - `RoadmapBoard()`: üç sütun (`ROADMAP_STAGES`). Her madde bir `<article>` içinde `<h4>` başlık ve metinden oluşur. Altta `ROADMAP_NOTE` yer alır.
  - `AboutPage()`: bölge başlıkları:
    - "Neden varız"
    - "İlkelerimiz"
    - "Ürünü nasıl geliştiriyoruz"
    - "Ekipzen bugün"
    - "Yol haritası" (`id="yol-haritasi"` taşıyan section)
    - "Taahhütlerimiz"

- [ ] **Step 1: Failing testler** — `frontend/src/marketing/about.test.tsx`

```tsx
import { act, render, screen, within } from "@testing-library/react";
import { expect, test } from "vitest";
import { triggerIntersections } from "../test/intersectionObserver";
import { ProcessTimeline } from "./ProcessTimeline";
import { ModuleMaturity } from "./ModuleMaturity";
import { RoadmapBoard } from "./RoadmapBoard";
import { ROADMAP_NOTE, devProcess, roadmap } from "./content";

test("süreç çizgisi altı adımı gösterir ve görünür olunca etkinleşir", () => {
  const { container } = render(<ProcessTimeline />);
  expect(screen.getAllByRole("listitem")).toHaveLength(devProcess.length);
  expect(screen.getByText("Uzman kabulü")).toBeInTheDocument();
  const list = container.querySelector(".ez-process")!;
  expect(list).not.toHaveClass("is-active");
  act(() => triggerIntersections(true, (el) => el === list));
  expect(list).toHaveClass("is-active");
});

test("olgunluk görünümü modülleri durumlarına göre gruplar", () => {
  render(<ModuleMaturity />);
  const building = screen.getByRole("region", { name: "Geliştiriliyor" });
  expect(within(building).getByText("Risk Merkezi")).toBeInTheDocument();
  const pilot = screen.getByRole("region", { name: "Pilotta" });
  expect(within(pilot).getByText("Bordro")).toBeInTheDocument();
  expect(within(pilot).getByText("Mesai ve Puantaj")).toBeInTheDocument();
  const active = screen.getByRole("region", { name: "Kullanımda" });
  expect(within(active).getAllByRole("listitem")).toHaveLength(3);
});

test("yol haritası panosu üç aşamayı, tüm maddeleri ve tarih notunu gösterir", () => {
  render(<RoadmapBoard />);
  for (const stage of ["Şimdi", "Sıradaki", "Planlanan"]) expect(screen.getByRole("heading", { level: 3, name: stage })).toBeInTheDocument();
  expect(screen.getAllByRole("article")).toHaveLength(roadmap.length);
  expect(screen.getByText(ROADMAP_NOTE)).toBeInTheDocument();
});
```

`pages.test.tsx` Hakkımızda testi şu hale gelir:

```tsx
test("Hakkımızda: manifesto, ilkeler, süreç, olgunluk, yol haritası ve taahhütler; uydurma kişi ya da sayı yok", () => {
  renderPage(<AboutPage />);
  expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("İK yazılımını, İK ekiplerinin günlük gerçekliğinden başlayarak geliştiriyoruz.");
  for (const name of ["Neden varız", "İlkelerimiz", "Ürünü nasıl geliştiriyoruz", "Ekipzen bugün", "Yol haritası", "Taahhütlerimiz"]) {
    expect(screen.getByRole("region", { name })).toBeInTheDocument();
  }
  expect(document.getElementById("yol-haritasi")).toHaveAccessibleName("Yol haritası");
  expect(screen.getByText("Hazır olanla geliştirileni ayırırız.")).toBeInTheDocument();
  expect(screen.queryByText(/kurucu|CEO|\d+\+ müşteri/i)).toBeNull();
});
```

Run: `npm --prefix frontend test -- --run src/marketing/about.test.tsx src/marketing/pages/pages.test.tsx`
Expected: FAIL

- [ ] **Step 2: Bileşenler**

`frontend/src/marketing/ProcessTimeline.tsx`:

```tsx
import { devProcess } from "./content";
import { useInView } from "./motion";

export function ProcessTimeline() {
  const { ref, inView } = useInView<HTMLOListElement>({ once: true, threshold: 0.3 });
  return (
    <ol ref={ref} className={`ez-process${inView ? " is-active" : ""}`}>
      {devProcess.map((step, i) => (
        <li key={step.title} className="ez-process__step" style={{ transitionDelay: `${i * 140}ms` }}>
          <span className="ez-process__marker" aria-hidden="true">{i + 1}</span>
          <h3>{step.title}</h3>
          <p>{step.text}</p>
        </li>
      ))}
    </ol>
  );
}
```

`frontend/src/marketing/ModuleMaturity.tsx`:

```tsx
import { STATUS_META, modules, type ModuleStatus } from "./content";
import { StatusBadge } from "./StatusBadge";

const ORDER: ModuleStatus[] = ["active", "pilot", "building"];

export function ModuleMaturity() {
  return (
    <div className="ez-maturity">
      {ORDER.map((status) => (
        <section key={status} className={`ez-maturity__col ez-maturity__col--${status}`} aria-labelledby={`olgunluk-${status}`}>
          <h3 id={`olgunluk-${status}`}>{STATUS_META[status].label}</h3>
          <p>{STATUS_META[status].description}</p>
          <ul>
            {modules.filter((m) => m.status === status).map((m) => (
              <li key={m.id}><i aria-hidden="true" className={`fa-solid ${m.icon}`} /><span>{m.title}</span><StatusBadge status={m.status} /></li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
```

Not: `StatusBadge` etiketi ile sütun başlığı aynı metni taşıdığından testlerde başlık `role="region"` adıyla sorgulanır, `getByText` ile değil.

`frontend/src/marketing/RoadmapBoard.tsx`:

```tsx
import { ROADMAP_NOTE, ROADMAP_STAGES, roadmap } from "./content";
import { Reveal } from "./motion";

export function RoadmapBoard() {
  return (
    <div className="ez-roadmap">
      <div className="ez-roadmap__board">
        {ROADMAP_STAGES.map((stage, col) => (
          <div key={stage.id} className={`ez-roadmap__col ez-roadmap__col--${stage.id}`}>
            <div className="ez-roadmap__head">
              <h3>{stage.title}</h3>
              <p>{stage.subtitle}</p>
            </div>
            {roadmap.filter((r) => r.stage === stage.id).map((item, i) => (
              <Reveal as="article" key={item.title} delay={col * 120 + i * 80} className="ez-roadmap__card">
                <h4>{item.title}</h4>
                <p>{item.text}</p>
              </Reveal>
            ))}
          </div>
        ))}
      </div>
      <p className="ez-roadmap__note"><i aria-hidden="true" className="fa-solid fa-circle-info" /> {ROADMAP_NOTE}</p>
    </div>
  );
}
```

- [ ] **Step 3: `AboutPage.tsx`**

```tsx
import { usePageMeta } from "../../ui";
import { aboutScenes, commitments, principles } from "../content";
import { CtaBand, ModuleIcon } from "../sections";
import { ProcessTimeline } from "../ProcessTimeline";
import { ModuleMaturity } from "../ModuleMaturity";
import { RoadmapBoard } from "../RoadmapBoard";
import { Reveal, onSpotlightMove, useScrollToHash } from "../motion";

export function AboutPage() {
  usePageMeta("Hakkımızda", "Ekipzen'in amacı, ürünü geliştirme yaklaşımı, bugünkü durumu ve yol haritası.");
  useScrollToHash();
  return (
    <>
      <section className="ez-manifesto ez-aurora">
        <svg className="ez-manifesto__lines" viewBox="0 0 1200 400" preserveAspectRatio="none" aria-hidden="true">
          <path pathLength={1} d="M0 320 C 240 260, 420 360, 640 280 S 1020 180, 1200 220" />
          <path pathLength={1} d="M0 360 C 300 300, 520 390, 760 320 S 1060 240, 1200 280" />
        </svg>
        <div className="ez-container ez-narrow">
          <Reveal>
            <p className="ez-eyebrow">Hakkımızda</p>
            <h1>İK yazılımını, İK ekiplerinin günlük gerçekliğinden başlayarak geliştiriyoruz.</h1>
            <p className="ez-lead">
              Ekipzen, İK ekiplerinin tekrar eden işlerini azaltmak ve çalışan verisini güvenle yönetmek için geliştirilen,
              gelişmekte olan bir insan kaynakları platformudur.
            </p>
          </Reveal>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="neden-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="neden-baslik">Neden varız</h2>
            <p className="ez-lead">
              Personel, izin, puantaj, bordro ve işe alım ayrı araçlarda yürütüldüğünde aynı bilgi birden fazla kez girilir
              ve kayıtlar arasında tutarsızlıklar oluşur. Ekipzen bu süreçleri tek bir veri modelinde birleştirir.
            </p>
          </Reveal>
          <ol className="ez-scenes">
            {aboutScenes.map((scene, i) => (
              <Reveal as="li" key={scene.title} delay={i * 140} className={`ez-scene ez-scene--${i + 1}`}>
                <span className="ez-scene__icon" aria-hidden="true"><i className={`fa-solid ${scene.icon}`} /></span>
                <h3>{scene.title}</h3>
                <p>{scene.text}</p>
              </Reveal>
            ))}
          </ol>
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="ilke-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="ilke-baslik">İlkelerimiz</h2>
            <p className="ez-lead">Ürünle ilgili her kararı bu dört ilkeye göre veriyoruz.</p>
          </Reveal>
          <ul className="ez-grid-2 ez-principles">
            {principles.map((p, i) => (
              <Reveal as="li" key={p.title} delay={i * 90} className="ez-pillar ez-spotlight" onPointerMove={onSpotlightMove}>
                <ModuleIcon icon={p.icon} />
                <h3>{p.title}</h3>
                <p>{p.text}</p>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <section className="ez-section" aria-labelledby="surec-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="surec-baslik">Ürünü nasıl geliştiriyoruz</h2>
            <p className="ez-lead">Her yetenek, genel kullanıma açılmadan önce aynı doğrulama adımlarından geçer.</p>
          </Reveal>
          <ProcessTimeline />
        </div>
      </section>

      <section className="ez-section ez-section--muted" aria-labelledby="bugun-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="bugun-baslik">Ekipzen bugün</h2>
            <p className="ez-lead">Modüllerin güncel durumu. Bir modül, pilotta doğrulandıktan sonra genel kullanıma açılır.</p>
          </Reveal>
          <ModuleMaturity />
        </div>
      </section>

      <section id="yol-haritasi" className="ez-section ez-anchor" aria-labelledby="yol-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="yol-baslik">Yol haritası</h2>
            <p className="ez-lead">Ekipzen, pilot müşterilerle birlikte doğrulanarak gelişiyor. Önümüzdeki çalışmaları açık tutuyoruz.</p>
          </Reveal>
          <RoadmapBoard />
        </div>
      </section>

      <section className="ez-section ez-section--dark" aria-labelledby="taahhut-baslik">
        <div className="ez-container">
          <Reveal className="ez-section__head">
            <h2 id="taahhut-baslik">Taahhütlerimiz</h2>
          </Reveal>
          <ul className="ez-commitments">
            {commitments.map((c, i) => (
              <Reveal as="li" key={c.title} delay={i * 110} className="ez-commitment">
                <i aria-hidden="true" className="fa-solid fa-circle-check" />
                <div>
                  <h3>{c.title}</h3>
                  <p>{c.text}</p>
                </div>
              </Reveal>
            ))}
          </ul>
        </div>
      </section>

      <CtaBand
        title="Ekipzen'in gelişimine erken dahil olun"
        text="Pilot sürecinde edindiğimiz geri bildirimler yol haritasını doğrudan şekillendiriyor. Süreçlerinizi birlikte değerlendirelim."
      />
    </>
  );
}
```

`content.ts`'deki `values` artık kullanılmıyorsa silinir.

Run: `grep -rn "values\b" frontend/src/marketing --include=*.tsx`
Expected: çıktı yok

- [ ] **Step 4: CSS** — `marketing.css` sonuna:

```css
.ez-manifesto { position: relative; padding-block: clamp(4rem, 10vw, 7.5rem) clamp(3rem, 7vw, 5rem); background: var(--ez-surface); }
.ez-manifesto h1 { font-size: clamp(2.25rem, 1.2rem + 3.6vw, 3.75rem); }
.ez-manifesto .ez-narrow > div { display: grid; gap: 1.25rem; }
.ez-manifesto__lines { position: absolute; inset: auto 0 0 0; width: 100%; height: 60%; pointer-events: none; }
.ez-manifesto__lines path { fill: none; stroke: var(--indigo-200); stroke-width: 1.5; stroke-dasharray: 1; stroke-dashoffset: 1; animation: ez-draw 3.2s var(--ease-out) forwards; }
.ez-manifesto__lines path + path { stroke: var(--indigo-100); animation-delay: 0.6s; }
@keyframes ez-draw { to { stroke-dashoffset: 0; } }

.ez-scenes { list-style: none; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1.25rem; counter-reset: scene; }
.ez-scene { position: relative; display: grid; align-content: start; gap: 0.75rem; padding: 1.75rem; border-radius: var(--r-xl); border: 1px solid var(--ez-border); background: var(--ez-surface); }
.ez-scene p { color: var(--ez-text-3); }
.ez-scene__icon { display: grid; place-items: center; width: 56px; height: 56px; border-radius: var(--r-lg); background: var(--ez-surface-2); color: var(--ez-text-3); font-size: 1.5rem; }
.ez-scene--3 { border-color: var(--indigo-300); background: linear-gradient(160deg, var(--ez-primary-soft), var(--ez-surface)); box-shadow: var(--ez-shadow-md); }
.ez-scene--3 .ez-scene__icon { background: var(--ez-primary); color: var(--ez-on-primary); }
.ez-scene:not(:last-child)::after { content: ""; position: absolute; top: 50%; right: -1.25rem; width: 1.25rem; height: 2px; background: var(--ez-border-strong); }
@media (max-width: 860px) { .ez-scenes { grid-template-columns: 1fr; } .ez-scene:not(:last-child)::after { display: none; } }

.ez-process { list-style: none; position: relative; display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 1rem; padding-top: 1.5rem; }
.ez-process::before { content: ""; position: absolute; top: calc(1.5rem + 21px); left: 22px; right: 22px; height: 2px; background: var(--ez-border); }
.ez-process::after { content: ""; position: absolute; top: calc(1.5rem + 21px); left: 22px; right: 22px; height: 2px; background: var(--ez-primary); transform: scaleX(0); transform-origin: left; transition: transform 1.4s var(--ease-out); }
.ez-process.is-active::after { transform: scaleX(1); }
.ez-process__step { position: relative; display: grid; align-content: start; gap: 0.5rem; opacity: 0.4; transition: opacity 500ms var(--ease-out); }
.ez-process.is-active .ez-process__step { opacity: 1; }
.ez-process__step h3 { font-size: var(--fs-16) !important; }
.ez-process__step p { font-size: var(--fs-14); color: var(--ez-text-3); }
.ez-process__marker { position: relative; z-index: 1; display: grid; place-items: center; width: 44px; height: 44px; border-radius: 50%; border: 2px solid var(--ez-primary); background: var(--ez-surface); color: var(--ez-primary); font-weight: 700; }
.ez-process.is-active .ez-process__marker { background: var(--ez-primary); color: var(--ez-on-primary); }
@media (max-width: 960px) {
  .ez-process { grid-template-columns: 1fr; padding-top: 0; }
  .ez-process::before, .ez-process::after { top: 22px; bottom: 22px; left: 21px; right: auto; width: 2px; height: auto; }
  .ez-process::after { transform: scaleY(0); transform-origin: top; }
  .ez-process.is-active::after { transform: scaleY(1); }
  .ez-process__step { grid-template-columns: 44px 1fr; column-gap: 1rem; }
  .ez-process__step h3, .ez-process__step p { grid-column: 2; }
  .ez-process__marker { grid-row: span 2; }
}

.ez-maturity { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1.25rem; }
.ez-maturity__col { display: grid; align-content: start; gap: 0.75rem; padding: 1.5rem; border-radius: var(--r-xl); background: var(--ez-surface); border: 1px solid var(--ez-border); border-top: 4px solid var(--ez-success); }
.ez-maturity__col--pilot { border-top-color: var(--ez-warning); }
.ez-maturity__col--building { border-top-color: var(--ez-primary); }
.ez-maturity__col h3 { font-size: var(--fs-18) !important; }
.ez-maturity__col > p { font-size: var(--fs-14); color: var(--ez-text-3); }
.ez-maturity__col ul { list-style: none; display: grid; gap: 0.5rem; }
.ez-maturity__col li { display: grid; grid-template-columns: 20px 1fr auto; align-items: center; gap: 0.625rem; padding: 0.625rem 0.75rem; border-radius: var(--r-md); background: var(--ez-surface-2); font-weight: 600; color: var(--ez-text); }
.ez-maturity__col li i { color: var(--ez-text-3); }
@media (max-width: 860px) { .ez-maturity { grid-template-columns: 1fr; } }

.ez-roadmap { display: grid; gap: 1.25rem; }
.ez-roadmap__board { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1.25rem; }
.ez-roadmap__col { display: grid; align-content: start; gap: 0.75rem; padding: 1rem; border-radius: var(--r-xl); background: var(--ez-bg); }
.ez-roadmap__head { display: grid; gap: 0.25rem; padding: 0.5rem 0.5rem 0.75rem; border-bottom: 2px solid var(--ez-border-strong); }
.ez-roadmap__col--now .ez-roadmap__head { border-bottom-color: var(--ez-warning); }
.ez-roadmap__col--next .ez-roadmap__head { border-bottom-color: var(--ez-primary); }
.ez-roadmap__head h3 { font-size: var(--fs-18) !important; }
.ez-roadmap__head p { font-size: var(--fs-14); color: var(--ez-text-3); }
.ez-roadmap__card { display: grid; gap: 0.375rem; padding: 1rem 1.125rem; border-radius: var(--r-lg); background: var(--ez-surface); border: 1px solid var(--ez-border); box-shadow: var(--ez-shadow-xs); }
.ez-roadmap__card h4 { font-size: var(--fs-16); color: var(--ez-text); }
.ez-roadmap__card p { font-size: var(--fs-14); color: var(--ez-text-3); }
.ez-roadmap__note { display: flex; gap: 0.5rem; align-items: center; font-size: var(--fs-14); color: var(--ez-text-3); }
@media (max-width: 860px) { .ez-roadmap__board { grid-template-columns: 1fr; } }

.ez-commitments { list-style: none; display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1.25rem; }
.ez-commitment { display: grid; grid-template-columns: auto 1fr; gap: 1rem; padding: 1.75rem; border-radius: var(--r-xl); background: rgb(255 255 255 / 0.04); border: 1px solid rgb(255 255 255 / 0.1); }
.ez-commitment i { font-size: 1.5rem; color: var(--indigo-300); }
.ez-commitment h3 { font-size: var(--fs-18) !important; margin-bottom: 0.5rem; }
@media (max-width: 860px) { .ez-commitments { grid-template-columns: 1fr; } }
```

`marketing-motion.css` reduced-motion bloğuna eklenir:

```css
.ez-manifesto__lines path { animation: none; stroke-dashoffset: 0; }
.ez-process::after { transition: none; transform: none; }
.ez-process__step { opacity: 1; transition: none; }
```

- [ ] **Step 5: Testleri çalıştır**

Run: `npm --prefix frontend test -- --run`
Expected: tümü PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/marketing frontend/src/styles
git commit -m "feat(landing): rebuild about page around principles, process, maturity and roadmap"
```

---

### Task 10: Fiyatlandırma efektleri, rozet düzeltmesi ve son kontroller

**Files:**
- Modify: `frontend/src/marketing/pages/PricingPage.tsx`
- Modify: `frontend/src/marketing/pages/pages.test.tsx` (Fiyatlandırma testi)
- Modify: `frontend/src/marketing/content.test.ts` (üslup koruması `motion` klasörünü ve yeni bileşenleri kapsar)
- Modify: `frontend/src/styles/marketing.css`, `frontend/src/styles/marketing-motion.css`

**Interfaces:**
- Consumes: `Reveal`, `onSpotlightMove` (Task 1).

**Neden rozet değişiyor:** "En çok tercih edilen" rozeti, pilot aşamasındaki bir ürün için doğrulanamayan bir kullanım iddiasıdır ve sahte sosyal kanıt yasağıyla çelişir. Rozet "Önerilen" olur.

- [ ] **Step 1: Failing test** — `pages.test.tsx` Fiyatlandırma testine eklenir:

```tsx
  expect(screen.queryByText("En çok tercih edilen")).toBeNull();
  expect(screen.getByText("Önerilen")).toBeInTheDocument();
  expect(screen.getByRole("link", { name: "Ürün nasıl gelişiyor?" })).toHaveAttribute("href", "/hakkimizda#yol-haritasi");
```

`content.test.ts`'deki üslup testinde `files` dizisine eklenir:

```ts
    ...readdirSync(join(src, "marketing", "motion")).filter((f) => f.endsWith(".ts") || (f.endsWith(".tsx") && !f.includes(".test."))).map((f) => join(src, "marketing", "motion", f)),
```

`banned` dizisine şu kalıplar eklenir:
- `/en çok tercih edilen/i`
- `/bliderb/i`
- `/binlerce/i`

Run: `npm --prefix frontend test -- --run src/marketing/pages/pages.test.tsx src/marketing/content.test.ts`
Expected: FAIL (rozet ve bağlantı yok)

- [ ] **Step 2: `PricingPage.tsx` değişiklikleri**

- Import eklenir: `import { Link } from "react-router-dom";` ve `import { Reveal, onSpotlightMove } from "../motion";`.
- Paket listesi `<li>` yerine şunu kullanır:

```tsx
            {plans.map((p, i) => (
              <Reveal as="li" key={p.id} delay={i * 100} className={`ez-plan ez-spotlight${p.highlighted ? " ez-plan--featured" : ""}`} onPointerMove={onSpotlightMove}>
                {p.highlighted && <Badge tone="primary">Önerilen</Badge>}
                <h2>{p.name}</h2>
                <p className="ez-plan__audience">{p.audience}</p>
                <p className="ez-plan__price">Size özel fiyat</p>
                <ButtonLink to={`/iletisim?paket=${p.id}`} variant={p.highlighted ? "primary" : "secondary"} block aria-label={`${p.name} paketi için teklif alın`}>
                  Teklif alın
                </ButtonLink>
                <ul className="ez-check-list">{p.features.map((f) => <li key={f}>{f}</li>)}</ul>
              </Reveal>
            ))}
```

- SSS bölümünden sonra, `<CtaBand … />` satırından önce eklenir:

```tsx
      <section className="ez-section ez-section--flush" aria-label="Ürünün gelişimi">
        <Reveal className="ez-container ez-narrow ez-pricing-roadmap">
          <i aria-hidden="true" className="fa-solid fa-route" />
          <p>Ekipzen, pilot müşterilerle birlikte doğrulanarak gelişmeye devam ediyor. Modüllerin durumunu ve yol haritasını açıkça paylaşıyoruz.</p>
          <Link to="/hakkimizda#yol-haritasi" className="ez-link">Ürün nasıl gelişiyor?</Link>
        </Reveal>
      </section>
```

- [ ] **Step 3: CSS** — `marketing.css` içinde `.ez-plan--featured` kuralı şununla değiştirilir, ardından eklemeler yapılır:

```css
.ez-plan { transition: transform var(--dur-base) var(--ease-out), box-shadow var(--dur-base) var(--ease-out); }
.ez-plan:hover { transform: translateY(-4px); box-shadow: var(--ez-shadow-lg); }
.ez-plan--featured {
  border: 2px solid transparent; box-shadow: var(--ez-shadow-lg);
  background: linear-gradient(var(--ez-surface), var(--ez-surface)) padding-box,
    linear-gradient(120deg, var(--indigo-700), var(--sky-400), var(--indigo-400), var(--indigo-700)) border-box;
  background-size: 100% 100%, 300% 300%;
  animation: ez-border-shift 9s linear infinite alternate;
}
@keyframes ez-border-shift { from { background-position: 0 0, 0% 50%; } to { background-position: 0 0, 100% 50%; } }
.ez-compare tbody tr { transition: background-color var(--dur-fast) var(--ease-out); }
.ez-compare tbody tr:hover > * { background: var(--ez-primary-soft); }
.ez-pricing-roadmap { display: grid; grid-template-columns: auto 1fr; gap: 0.5rem 1rem; align-items: center; padding: 1.5rem; border-radius: var(--r-xl); background: var(--ez-bg); border: 1px dashed var(--ez-border-strong); }
.ez-pricing-roadmap i { grid-row: span 2; font-size: 1.5rem; color: var(--ez-primary); }
.ez-pricing-roadmap p { color: var(--ez-text-2); }
.ez-pricing-roadmap .ez-link { justify-self: start; display: inline-flex; min-height: 44px; align-items: center; }
```

`marketing-motion.css` reduced-motion bloğuna eklenir: `.ez-plan--featured { animation: none; } .ez-plan:hover { transform: none; }`.

- [ ] **Step 4: Tam doğrulama**

Run: `npm --prefix frontend run lint && npm --prefix frontend test -- --run && npm --prefix frontend run build`
Expected: hata yok, tüm testler PASS

Hardcoded renk kontrolü (yalnız mevcut kabul edilmiş `rgb(255 255 255 / …)` kaplamaları kalabilir):

Run: `grep -nE "#[0-9a-fA-F]{3,6}\b" frontend/src/styles/marketing.css frontend/src/styles/marketing-motion.css`
Expected: çıktı yok

- [ ] **Step 5: Commit**

```bash
git add frontend/src/marketing frontend/src/styles
git commit -m "feat(landing): add pricing effects, replace unverifiable badge, extend tone guard"
```

- [ ] **Step 6: Tarayıcı doğrulaması (controller)**

`ekipzen-frontend-landing` önizlemesinde şu sayfalar kontrol edilir: `/`, `/ozellikler`, `/fiyatlandirma`, `/guvenlik`, `/hakkimizda`.

Her sayfada:
- 1280, 768 ve 375px genişliklerde yatay taşma olmamalı; konsolda hata olmamalı.
- Vitrin klavyeyle kullanılabilmeli.
- Yapışkan menü ve çalışan yolculuğu kaydırmayla ilerlemeli.
- `/hakkimizda#yol-haritasi` bağlantısı doğru bölüme kaymalı.
- Hareket azaltma tercihi emüle edildiğinde bütün içerik görünür olmalı ve otomatik ilerleme durmalı.

---

## Self-review notları

- **Spec kapsamı:**

  | Spec bölümü | Görev |
  |---|---|
  | Kararlar / içerik | Task 2 |
  | Efekt sistemi | Task 1 |
  | Ana sayfa | Task 3 (hero), 4, 5, 6 |
  | Özellikler | Task 7 |
  | Güvenlik | Task 8 |
  | Hakkımızda | Task 9 |
  | Fiyatlandırma | Task 10 |
  | Test ve doğrulama | her görev + Task 10 Step 4–6 |

- **Spec'e ek düzeltmeler:**
  - Hash kaydırma hatası (Task 7). SPA içinde `#bölüm` bağlantıları çalışmıyordu.
  - "En çok tercih edilen" rozeti (Task 10). Doğrulanamayan kullanım iddiasıydı.
- **Spec'ten küçük uyarlama:** Güvenlik katman şeması masaüstünde "iç içe halkalar + panel" yerine içe doğru daralan, iç içe katman şeritlerinden oluşan bir akordeon olarak kuruldu. Tek bir erişilebilir bileşen her genişlikte çalışır; katman hissi girinti ve renk derinliğiyle verilir.
- **Tip ve ad tutarlılığı:**
  - `ModuleStatus`, `STATUS_META`, `SHOWCASE_ORDER`, `ROADMAP_STAGES`, `ROADMAP_NOTE` Task 2'de tanımlanır ve sonraki görevlerde aynı adla kullanılır.
  - `useInView`, `Reveal`, `onSpotlightMove`, `usePrefersReducedMotion` Task 1'de; `useScrollToHash` Task 7'de tanımlanır ve Task 9'da kullanılır.
