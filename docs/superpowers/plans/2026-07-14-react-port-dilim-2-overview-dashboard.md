# React Port — Dilim 2: Overview + Risk Merkezi — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Genel Durum (`/overview`), Risk Merkezi (`/dashboard`) ve 5 risk detay sayfasını (`/risk/attrition|burnout|manager-load|employee-voice|compliance`) gerçek API'ye bağlı React sayfaları olarak portla; Chart.js grafikleri birebir taşı.

**Architecture:** Her sayfa `src/features/<alan>/` altında: sayfa component'i + `queries.ts` (TanStack Query hook'ları). Veri backend'den gelir (mock alanlar türetilir veya çıkarılır — aşağıdaki eşleme tablosu). Grafikler react-chartjs-2 ile; renkler CSS token'larından (`chartToken`). Sayfalar `routes.tsx`'teki `pageFor` eşlemesine kaydolur; guard/shell Dilim 1'den hazır.

**Tech Stack:** Dilim 1 stack'i + chart.js@^4 + react-chartjs-2@^5.

## Global Constraints

- Dilim 1 planındaki tüm kısıtlar geçerli (CSS'e dokunma, aynı class/DOM, Türkçe metinler birebir, hash URL'ler, her görev sonunda `cd frontend && npm test -- --run` yeşil).
- Backend tek veri kaynağıdır; **mock'a özel hiçbir sabit veri taşınmaz.**
- Testlerde gerçek ağ yok: `fetch`, `src/test/apiStub.ts` ile stub'lanır; react-chartjs-2, Vitest alias'ı ile `src/test/chartStub.tsx`'e yönlenir.
- Tipler `src/api/schema.d.ts`'ten (`components["schemas"][...]`) alınır; elle DTO yazılmaz.

### Veri eşleme kararları (mock ↔ backend farkları)

| Eski mock alanı | Backend karşılığı / karar |
|---|---|
| `departmentRisk[].drivers` (3 metin) | Yok → ısı haritası satır metni: `{employeeCount} çalışan · {highAttritionCount} yüksek ayrılma · {highBurnoutCount} tükenmişlik sinyali` |
| `metrics.actions` (dashboard aksiyon listesi) | `GET /api/actions` (`GlobalActionDto[]`, `status !== "done"` filtreli). Kart: öncelik etiketi `priorityLabel(priority)`, açıklama satırı `${source} · ${owner}`, öneri `action`. Tıklama `sourceRoute` route key'inin path'ine gider. |
| Yönetici KPI alt metni ("5 onay, 14 açık aksiyon…") | `GET /api/dashboard/manager-load` → `${pendingApprovals} onay, ${openActions} açık aksiyon, yoğun ekip` |
| "+8 puan" pill + trend cümlesi | `riskTrend` dizisinden türetilir: `delta = son − ilk`; pill `“{±delta} puan”`, cümle delta ≥ 0 ise "yukarı yönlü", değilse "aşağı yönlü" |
| Overview KPI 1 trend satırı ("Bu çeyrek %12 artış") | Uydurma → `${departmentDistribution.length} departmanda aktif kadro` |
| Overview KPI 4 ("Kritik Hatırlatma 2") | Backend'de yok → "Bugün İzinli" = `onLeaveToday`, alt metin "İzinli / raporlu çalışan" (aynı class'lar: `bg-red-light`, saat ikonu) |
| Anlık Çalışma Durumu (Ofiste/Uzaktan/İzinli) | Ofiste=`inOfficeToday`, İzinli / Raporlu=`onLeaveToday`, Kayıt Bekleyen=`max(0, activeEmployees − inOfficeToday − onLeaveToday)` (dot-green/dot-orange/dot-blue) |
| Overview alt grid (Bekleyen Aksiyonlar + Önemli Günler) | **Dilim 2'de yok** — gerçek izin onaylarıyla Dilim 4'te gelecek (uydurma onay kartı taşınmaz) |
| Nabız memnuniyet çubuğu (%78) | `overview.pulseScore` |
| EmployeeVoice `riskTeams` aside | Backend'de yok → `departments.filter(level !== "low")` ilk 3 kayıttan türetilir: etiket=`mood`, başlık=`dept`, açıklama=`driver` (owner/öneri satırı yok) |
| Compliance `auditChecklist` + `recommendedActions` | Backend'de yok → `detail-support-grid` bölümü Dilim 2'de çıkarılır (uyum modülü Dilim 6'da tamamlanır) |
| Burnout 4. kutu ("Yoğun Departman 3") | Backend'de yok → "Orta Sinyal" = `mediumCount` |
| Attrition KPI "Ortalama Nabız" | `RiskDetailDto.averagePulse` |
| İşe alım hunisi "Bu Ay/Bu Yıl" select'i | Backend filtre sunmuyor → select bu dilimde konmaz (tek dilim veri) |

---

### Task 1: Paylaşılan altyapı — bağımlılıklar, chart/api stub'ları, PageState, format yardımcıları

**Files:**
- Modify: `frontend/package.json` (chart.js, react-chartjs-2), `frontend/vite.config.ts` (test alias)
- Create: `frontend/src/test/chartStub.tsx`, `frontend/src/test/apiStub.ts`, `frontend/src/test/renderPage.tsx`
- Create: `frontend/src/features/shared/chartSetup.ts`, `frontend/src/features/shared/PageState.tsx`
- Create: `frontend/src/features/dashboard/format.ts`
- Test: `frontend/src/features/dashboard/format.test.ts`, `frontend/src/features/shared/PageState.test.tsx`

**Interfaces:**
- Produces:
  - `format.ts`: `getRiskLevel(score: number): "high"|"medium"|"low"`, `getRiskLabel(score: number): string`, `getLevelText(level?: string|null): string`, `priorityLabel(p?: string|null): string`, `topPriorityActions<T extends {priority?: string|null}>(items: T[], count?: number): T[]`, `formatToday(): string`
  - `PageState.tsx`: `PageLoading()`, `PageError({ error }: { error: unknown })`
  - `apiStub.ts`: `stubApi(routes: Record<string, unknown>): void` — anahtar **tam path** (`"/api/dashboard/metrics"`), query string yok sayılır
  - `renderPage.tsx`: `renderPage(ui: ReactElement)` — QueryClientProvider (retry:false) + MemoryRouter sarmalayıcı

- [x] **Step 1: Bağımlılıkları kur**

```bash
cd frontend
npm install chart.js@^4 react-chartjs-2@^5
```

- [x] **Step 2: Başarısız testleri yaz** — `src/features/dashboard/format.test.ts`:

```ts
import { expect, test } from "vitest";
import { getLevelText, getRiskLabel, getRiskLevel, priorityLabel, topPriorityActions } from "./format";

test("risk seviyesi eşikleri eski getRiskLevel ile birebir", () => {
  expect(getRiskLevel(70)).toBe("high");
  expect(getRiskLevel(55)).toBe("medium");
  expect(getRiskLevel(54)).toBe("low");
});

test("risk etiketi eski getRiskLabel ile birebir", () => {
  expect(getRiskLabel(70)).toBe("Yüksek risk");
  expect(getRiskLabel(55)).toBe("Orta risk");
  expect(getRiskLabel(40)).toBe("Kontrollü");
});

test("seviye metni bilinmeyen seviyede İzlemede döner", () => {
  expect(getLevelText("high")).toBe("Yüksek");
  expect(getLevelText(null)).toBe("İzlemede");
});

test("öncelik etiketi high/medium/low eşlemesi", () => {
  expect(priorityLabel("high")).toBe("Bugün müdahale et");
  expect(priorityLabel("medium")).toBe("Bu hafta takip et");
  expect(priorityLabel("low")).toBe("İzlemede kalsın");
});

test("topPriorityActions önceliğe göre sıralar ve keser", () => {
  const items = [{ priority: "low" }, { priority: "high" }, { priority: "medium" }, { priority: "high" }];
  expect(topPriorityActions(items, 3).map((i) => i.priority)).toEqual(["high", "high", "medium"]);
});
```

`src/features/shared/PageState.test.tsx`:

```tsx
import { render, screen } from "@testing-library/react";
import { expect, test } from "vitest";
import { ApiError } from "../../api/client";
import { PageError, PageLoading } from "./PageState";

test("yükleme durumu render edilir", () => {
  render(<PageLoading />);
  expect(screen.getByText("Yükleniyor")).toBeInTheDocument();
});

test("ApiError mesajı gösterilir", () => {
  render(<PageError error={new ApiError(409, "Çakışan kayıt.")} />);
  expect(screen.getByRole("alert")).toHaveTextContent("Çakışan kayıt.");
});
```

Run: `npm test -- --run src/features` → Beklenen: FAIL (modüller yok).

- [x] **Step 3: `format.ts` yaz**

```ts
export const getRiskLevel = (score: number): "high" | "medium" | "low" =>
  score >= 70 ? "high" : score >= 55 ? "medium" : "low";

export const getRiskLabel = (score: number): string =>
  score >= 70 ? "Yüksek risk" : score >= 55 ? "Orta risk" : "Kontrollü";

const LEVEL_TEXT: Record<string, string> = { high: "Yüksek", medium: "Orta", low: "Düşük" };
export const getLevelText = (level?: string | null): string => LEVEL_TEXT[level ?? ""] ?? "İzlemede";

const PRIORITY_LABEL: Record<string, string> = {
  high: "Bugün müdahale et",
  medium: "Bu hafta takip et",
  low: "İzlemede kalsın",
};
export const priorityLabel = (priority?: string | null): string =>
  PRIORITY_LABEL[priority ?? ""] ?? "İzlemede kalsın";

const PRIORITY_ORDER: Record<string, number> = { high: 0, medium: 1, low: 2 };
export const topPriorityActions = <T extends { priority?: string | null }>(
  items: T[],
  count = 3,
): T[] =>
  [...items]
    .sort((a, b) => (PRIORITY_ORDER[a.priority ?? ""] ?? 3) - (PRIORITY_ORDER[b.priority ?? ""] ?? 3))
    .slice(0, count);

export const formatToday = (): string =>
  new Date().toLocaleDateString("tr-TR", { weekday: "long", year: "numeric", month: "long", day: "numeric" });
```

- [x] **Step 4: `PageState.tsx` yaz**

```tsx
import { ApiError } from "../../api/client";

export function PageLoading() {
  return (
    <section className="surface empty-state" role="status">
      <i aria-hidden="true" className="fa-solid fa-circle-notch fa-spin" />
      <h2>Yükleniyor</h2>
      <p>Veriler yükleniyor, lütfen bekleyin.</p>
    </section>
  );
}

export function PageError({ error }: { error: unknown }) {
  const message = error instanceof ApiError ? error.message : "Beklenmeyen bir hata oluştu.";
  return (
    <section className="surface empty-state" role="alert">
      <i aria-hidden="true" className="fa-solid fa-triangle-exclamation" />
      <h2>Veri yüklenemedi</h2>
      <p>{message}</p>
    </section>
  );
}
```

- [x] **Step 5: `chartSetup.ts`, `chartStub.tsx`, `apiStub.ts`, `renderPage.tsx` yaz**

`src/features/shared/chartSetup.ts`:

```ts
import { Chart, registerables } from "chart.js";

Chart.register(...registerables);

/** Grafik renkleri CSS token'larından okunur (eski chartToken paritesi). */
export const chartToken = (name: string, fallback: string): string =>
  getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
```

`src/test/chartStub.tsx` (Vitest, react-chartjs-2 yerine bunu yükler — jsdom'da canvas yok):

```tsx
type ChartProps = { data?: unknown; options?: unknown };

export const Line = (_props: ChartProps) => <canvas data-testid="chart-line" />;
export const Doughnut = (_props: ChartProps) => <canvas data-testid="chart-doughnut" />;
export const Bar = (_props: ChartProps) => <canvas data-testid="chart-bar" />;
```

`src/test/apiStub.ts`:

```ts
import { vi } from "vitest";

/** Anahtar tam path'tir ("/api/dashboard/metrics"); query string yok sayılır. */
export function stubApi(routes: Record<string, unknown>): void {
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input).split("?")[0];
      if (!(path in routes)) {
        return new Response(JSON.stringify({ title: `Stub tanımlı değil: ${path}` }), { status: 404 });
      }
      return new Response(JSON.stringify(routes[path]), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }),
  );
}
```

`src/test/renderPage.tsx`:

```tsx
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { MemoryRouter } from "react-router-dom";

export const renderPage = (ui: ReactElement) =>
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <MemoryRouter>{ui}</MemoryRouter>
    </QueryClientProvider>,
  );
```

- [x] **Step 6: `vite.config.ts`'e test alias'ı ekle** — `test` bloğunu şu hale getir:

```ts
import { fileURLToPath } from "node:url";
// ...
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/test/setup.ts",
    alias: {
      "react-chartjs-2": fileURLToPath(new URL("./src/test/chartStub.tsx", import.meta.url)),
    },
  },
```

- [x] **Step 7: Testleri doğrula**

Run: `npm test -- --run` → Beklenen: eski 14 + yeni 7 test PASS.

- [x] **Step 8: Commit**

```bash
git add frontend/
git commit -m "feat(frontend): dilim 2 altyapısı — chart kurulumu, test stub'ları, PageState, risk format yardımcıları"
```

---

### Task 2: Genel Durum sayfası (Overview)

**Files:**
- Create: `frontend/src/features/overview/queries.ts`, `frontend/src/features/overview/OverviewCharts.tsx`, `frontend/src/features/overview/OverviewPage.tsx`
- Modify: `frontend/src/routes.tsx` (pageFor kaydı)
- Test: `frontend/src/features/overview/OverviewPage.test.tsx`

**Interfaces:**
- Consumes: `apiFetch`, `PageLoading/PageError`, `formatToday`, `chartToken`, `renderPage`, `stubApi`.
- Produces: `useOverview(): UseQueryResult<OverviewDto>`; `OverviewPage(): JSX.Element`; `DeptDistributionChart({ distribution })`, `RecruitmentFunnelChart({ funnel })`.

- [x] **Step 1: Başarısız testi yaz** — `OverviewPage.test.tsx`:

```tsx
import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { OverviewPage } from "./OverviewPage";

const overview = {
  activeEmployees: 42,
  pendingApprovals: 5,
  openPositions: 8,
  newApplications: 32,
  inOfficeToday: 30,
  onLeaveToday: 4,
  pulseScore: 66,
  departmentDistribution: [
    { dept: "Yazılım", count: 18 },
    { dept: "Satış", count: 12 },
  ],
  recruitmentFunnel: { total: 120, new: 64, interview: 45, offer: 12, rejected: 20, hired: 5 },
};

beforeEach(() => stubApi({ "/api/dashboard/overview": overview }));
afterEach(() => vi.unstubAllGlobals());

test("KPI kartları backend verisiyle dolar", async () => {
  renderPage(<OverviewPage />);
  expect(await screen.findByText("42")).toBeInTheDocument();
  expect(screen.getByText("Aktif Personel")).toBeInTheDocument();
  expect(screen.getByText("32 yeni başvuru")).toBeInTheDocument();
  expect(screen.getByText("2 departmanda aktif kadro")).toBeInTheDocument();
});

test("çalışma durumu satırları türetilmiş sayıları gösterir", async () => {
  renderPage(<OverviewPage />);
  expect(await screen.findByText("Ofiste")).toBeInTheDocument();
  expect(screen.getByText("30")).toBeInTheDocument();
  // Kayıt bekleyen = 42 - 30 - 4 = 8 → "8" hem açık pozisyonda hem burada olabilir; satır bazlı kontrol:
  expect(screen.getByText("Kayıt Bekleyen").closest(".status-item")).toHaveTextContent("8");
  expect(screen.getByText("%66 pozitif")).toBeInTheDocument();
});

test("grafikler render edilir (stub canvas)", async () => {
  renderPage(<OverviewPage />);
  expect(await screen.findByTestId("chart-doughnut")).toBeInTheDocument();
  expect(screen.getByTestId("chart-bar")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/overview` → Beklenen: FAIL.

- [x] **Step 2: `queries.ts` yaz**

```ts
import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type OverviewDto = components["schemas"]["OverviewDto"];

export const useOverview = () =>
  useQuery({ queryKey: ["dashboard", "overview"], queryFn: () => apiFetch<OverviewDto>("/dashboard/overview") });
```

- [x] **Step 3: `OverviewCharts.tsx` yaz** (eski initDashboardCharts paritesi)

```tsx
import { Bar, Doughnut } from "react-chartjs-2";
import { chartToken } from "../shared/chartSetup";
import type { components } from "../../api/schema";

type DepartmentCountDto = components["schemas"]["DepartmentCountDto"];
type RecruitmentFunnelSliceDto = components["schemas"]["RecruitmentFunnelSliceDto"];

// Eski overviewDeptChart palet sırası birebir.
const DEPT_COLORS = ["#0f766e", "#0e7490", "#b98a2f", "#157f3d", "#5b7c99"];

export function DeptDistributionChart({ distribution }: { distribution: DepartmentCountDto[] }) {
  return (
    <div className="chart-container">
      <Doughnut
        data={{
          labels: distribution.map((d) => d.dept ?? ""),
          datasets: [
            {
              data: distribution.map((d) => d.count ?? 0),
              backgroundColor: distribution.map((_, i) => DEPT_COLORS[i % DEPT_COLORS.length]),
              borderWidth: 0,
            },
          ],
        }}
        options={{
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { position: "right", labels: { usePointStyle: true, boxWidth: 8 } } },
        }}
      />
    </div>
  );
}

export function RecruitmentFunnelChart({ funnel }: { funnel: RecruitmentFunnelSliceDto }) {
  return (
    <div className="chart-container">
      <Bar
        data={{
          labels: ["Başvuru", "Ön Görüşme", "Mülakat", "Teklif", "İşe Giriş"],
          datasets: [
            {
              label: "Aday",
              data: [funnel.total ?? 0, funnel.new ?? 0, funnel.interview ?? 0, funnel.offer ?? 0, funnel.hired ?? 0],
              backgroundColor: chartToken("--primary", "#0f766e"),
              borderRadius: 5,
            },
          ],
        }}
        options={{
          responsive: true,
          maintainAspectRatio: false,
          scales: {
            y: { beginAtZero: true, grid: { color: chartToken("--line-soft", "#e9efef") } },
            x: { grid: { display: false } },
          },
          plugins: { legend: { display: false } },
        }}
      />
    </div>
  );
}
```

- [x] **Step 4: `OverviewPage.tsx` yaz** (eski OverviewDashboard DOM paritesi; alt grid Dilim 4'te)

```tsx
import { useNavigate } from "react-router-dom";
import { formatToday } from "../dashboard/format";
import { PageError, PageLoading } from "../shared/PageState";
import { DeptDistributionChart, RecruitmentFunnelChart } from "./OverviewCharts";
import { useOverview } from "./queries";

export function OverviewPage() {
  const navigate = useNavigate();
  const overview = useOverview();

  if (overview.isPending) return <PageLoading />;
  if (overview.isError) return <PageError error={overview.error} />;

  const data = overview.data;
  const active = data.activeEmployees ?? 0;
  const inOffice = data.inOfficeToday ?? 0;
  const onLeave = data.onLeaveToday ?? 0;
  const unknown = Math.max(0, active - inOffice - onLeave);
  const pulse = data.pulseScore ?? 0;
  const distribution = data.departmentDistribution ?? [];

  return (
    <div className="dashboard-wrapper">
      <div className="welcome-header">
        <div>
          <h2>Genel Durum</h2>
          <p className="text-muted">Şirkette bugün ne oluyor? Operasyonel İK görünümünü hızlıca tarayın.</p>
        </div>
        <div className="date-widget">
          <i aria-hidden="true" className="fa-regular fa-calendar" />
          <span>{formatToday()}</span>
        </div>
      </div>

      <div className="kpi-grid">
        <div className="kpi-card">
          <div className="kpi-icon bg-blue-light"><i aria-hidden="true" className="fa-solid fa-users" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Aktif Personel</span>
            <h3 className="kpi-value">{active}</h3>
            <span className="kpi-trend">
              <i aria-hidden="true" className="fa-solid fa-arrow-trend-up" /> {distribution.length} departmanda aktif kadro
            </span>
          </div>
        </div>
        <div className="kpi-card">
          <div className="kpi-icon bg-orange-light"><i aria-hidden="true" className="fa-solid fa-file-signature" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Onay Bekleyen</span>
            <h3 className="kpi-value">{data.pendingApprovals ?? 0}</h3>
            <button type="button" className="kpi-link" onClick={() => navigate("/manager")}>Talepleri incele</button>
          </div>
        </div>
        <div className="kpi-card">
          <div className="kpi-icon bg-purple-light"><i aria-hidden="true" className="fa-solid fa-briefcase" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Açık Pozisyon</span>
            <h3 className="kpi-value">{data.openPositions ?? 0}</h3>
            <span className="kpi-sub">{data.newApplications ?? 0} yeni başvuru</span>
          </div>
        </div>
        <div className="kpi-card">
          <div className="kpi-icon bg-red-light"><i aria-hidden="true" className="fa-solid fa-clock-rotate-left" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Bugün İzinli</span>
            <h3 className="kpi-value">{onLeave}</h3>
            <span className="kpi-sub">İzinli / raporlu çalışan</span>
          </div>
        </div>
      </div>

      <div className="charts-grid">
        <div className="card chart-card">
          <div className="card-header-clean">
            <div>
              <h4>Departman Dağılımı</h4>
              <p className="text-muted">Aktif çalışan kırılımı</p>
            </div>
          </div>
          <DeptDistributionChart distribution={distribution} />
        </div>

        <div className="card chart-card">
          <div className="card-header-clean">
            <div>
              <h4>İşe Alım Hunisi</h4>
              <p className="text-muted">Aday ilerleyişi</p>
            </div>
          </div>
          <RecruitmentFunnelChart funnel={data.recruitmentFunnel ?? {}} />
        </div>

        <div className="card status-widget">
          <div className="card-header-clean">
            <h4>Anlık Çalışma Durumu</h4>
          </div>
          <div className="status-list">
            <div className="status-item">
              <div className="status-info"><span className="dot dot-green" /><span>Ofiste</span></div>
              <strong>{inOffice}</strong>
            </div>
            <div className="status-item">
              <div className="status-info"><span className="dot dot-orange" /><span>İzinli / Raporlu</span></div>
              <strong>{onLeave}</strong>
            </div>
            <div className="status-item">
              <div className="status-info"><span className="dot dot-blue" /><span>Kayıt Bekleyen</span></div>
              <strong>{unknown}</strong>
            </div>
          </div>
          <div className="pulse-check">
            <small>Çalışan memnuniyeti</small>
            <div className="progress-bar"><div className="fill" style={{ width: `${pulse}%` }} /></div>
            <small className="text-right">%{pulse} pozitif</small>
          </div>
        </div>
      </div>
    </div>
  );
}
```

Not: eski `kpi-link` bir `<a href="#">` idi; React'te `<button type="button" className="kpi-link">` kullanılır (görsel fark yoksa sorun yok; parite kontrolünde doğrulanır).

- [x] **Step 5: `routes.tsx`'te kaydet** — `pageFor` satırını değiştir:

```tsx
import { OverviewPage } from "./features/overview/OverviewPage";
// ...
const pageFor: Record<string, () => JSX.Element> = {
  overview: OverviewPage,
};
```

- [x] **Step 6: Testleri doğrula**

Run: `npm test -- --run` → Beklenen: tümü PASS.

- [x] **Step 7: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): Genel Durum sayfası gerçek API + Chart.js ile portlandı"
```

---

### Task 3: Risk Merkezi sayfası (Dashboard)

**Files:**
- Create: `frontend/src/features/dashboard/queries.ts`, `frontend/src/features/dashboard/RiskTrendChart.tsx`, `frontend/src/features/dashboard/RiskCenterPage.tsx`
- Modify: `frontend/src/routes.tsx` (pageFor: dashboard)
- Test: `frontend/src/features/dashboard/RiskCenterPage.test.tsx`

**Interfaces:**
- Consumes: Task 1 yardımcıları; `appRoutes` (sourceRoute → path çevirisi).
- Produces: `useDashboardMetrics()`, `useManagerLoad()`, `useEmployeeVoice()`, `useComplianceRisk()`, `useOpenActions()`, `useAttritionDetail()`, `useBurnoutDetail()` hook'ları (sonraki task'lar da kullanır); `RiskCenterPage()`; `RiskTrendChart({ trend })`.

- [x] **Step 1: Başarısız testi yaz** — `RiskCenterPage.test.tsx`:

```tsx
import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { RiskCenterPage } from "./RiskCenterPage";

const metrics = {
  riskScore: 62,
  managerLoadIndex: 71,
  attritionHigh: 3,
  burnoutRisk: 2,
  criticalActions: 7,
  pulseScore: 64,
  riskTrend: [48, 51, 49, 54, 57, 59, 62, 64, 61, 67, 69, 62],
  departmentRisk: [
    { departmentId: 1, dept: "Yazılım", risk: 74, employeeCount: 18, highAttritionCount: 2, highBurnoutCount: 1 },
  ],
  talentCapacity: [
    { label: "İşe Alım Sağlığı", value: 72, meta: "8 açık pozisyon", tone: "medium" },
    { label: "Kritik Rol Riski", value: 3, meta: "Yedekleme planı yok", tone: "high" },
  ],
  employees: [],
};
const managerLoad = { managerLoadIndex: 71, criticalManagerCount: 2, pendingApprovals: 5, openActions: 14, managers: [] };
const voice = { pulseScore: 64, eNps: 8, participationRate: 76, decliningTeams: 2, sentimentTrend: "Son nabız ölçümünde 2 ekipte bağlılık geriledi", departments: [], signals: [], recommendedActions: [] };
const compliance = { documentComplianceScore: 82, missingDocuments: 11, upcomingDocuments: 18, auditReadinessRisk: "Orta", auditReadinessScore: 74, records: [], deadlines: [] };
const actions = [
  { id: 1, title: "Tükenmişlik sinyali", source: "Risk Merkezi", sourceRoute: "burnout-risk", owner: "Ece Arslan", due: "Bugün", priority: "high", status: "open", action: "Kapasite görüşmesi planla." },
  { id: 2, title: "İzlenen konu", source: "İşe Alım", sourceRoute: "recruitment", owner: "İşe Alım", due: null, priority: "low", status: "open", action: null },
  { id: 3, title: "Kapanan iş", source: "Bordro", sourceRoute: null, owner: "İK", due: null, priority: "high", status: "done", action: null },
];

beforeEach(() =>
  stubApi({
    "/api/dashboard/metrics": metrics,
    "/api/dashboard/manager-load": managerLoad,
    "/api/dashboard/employee-voice": voice,
    "/api/dashboard/compliance": compliance,
    "/api/actions": actions,
  }),
);
afterEach(() => vi.unstubAllGlobals());

test("KPI kartları ve türetilmiş alt metinler dolar", async () => {
  renderPage(<RiskCenterPage />);
  expect(await screen.findByText("İK Risk Skoru")).toBeInTheDocument();
  expect(screen.getByText("5 onay, 14 açık aksiyon, yoğun ekip")).toBeInTheDocument();
  // 62 - 48 = +14 puan pill
  expect(screen.getByText("+14 puan")).toBeInTheDocument();
});

test("ısı haritası satırı backend sayılarından türetilir", async () => {
  renderPage(<RiskCenterPage />);
  expect(await screen.findByText("18 çalışan · 2 yüksek ayrılma · 1 tükenmişlik sinyali")).toBeInTheDocument();
});

test("en acil aksiyonlar done hariç önceliğe göre listelenir", async () => {
  renderPage(<RiskCenterPage />);
  expect(await screen.findByText("Tükenmişlik sinyali")).toBeInTheDocument();
  expect(screen.queryByText("Kapanan iş")).not.toBeInTheDocument();
  expect(screen.getByText("Tümünü aç (2)")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/dashboard/RiskCenterPage.test.tsx` → Beklenen: FAIL.

- [x] **Step 2: `queries.ts` yaz**

```ts
import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type DashboardMetricsDto = components["schemas"]["DashboardMetricsDto"];
export type ManagerLoadDto = components["schemas"]["ManagerLoadDto"];
export type EmployeeVoiceDto = components["schemas"]["EmployeeVoiceDto"];
export type ComplianceRiskDto = components["schemas"]["ComplianceRiskDto"];
export type RiskDetailDto = components["schemas"]["RiskDetailDto"];
export type GlobalActionDto = components["schemas"]["GlobalActionDto"];
export type RiskEmployeeDto = components["schemas"]["RiskEmployeeDto"];

export const useDashboardMetrics = () =>
  useQuery({ queryKey: ["dashboard", "metrics"], queryFn: () => apiFetch<DashboardMetricsDto>("/dashboard/metrics") });

export const useManagerLoad = () =>
  useQuery({ queryKey: ["dashboard", "manager-load"], queryFn: () => apiFetch<ManagerLoadDto>("/dashboard/manager-load") });

export const useEmployeeVoice = () =>
  useQuery({ queryKey: ["dashboard", "employee-voice"], queryFn: () => apiFetch<EmployeeVoiceDto>("/dashboard/employee-voice") });

export const useComplianceRisk = () =>
  useQuery({ queryKey: ["dashboard", "compliance"], queryFn: () => apiFetch<ComplianceRiskDto>("/dashboard/compliance") });

export const useAttritionDetail = () =>
  useQuery({ queryKey: ["dashboard", "attrition"], queryFn: () => apiFetch<RiskDetailDto>("/dashboard/attrition") });

export const useBurnoutDetail = () =>
  useQuery({ queryKey: ["dashboard", "burnout"], queryFn: () => apiFetch<RiskDetailDto>("/dashboard/burnout") });

export const useOpenActions = () =>
  useQuery({
    queryKey: ["actions", "list"],
    queryFn: () => apiFetch<GlobalActionDto[]>("/actions"),
    select: (all) => all.filter((a) => a.status !== "done"),
  });
```

- [x] **Step 3: `RiskTrendChart.tsx` yaz** (eski riskTrendChart birebir; etiketler diziden türetilir: sonuncu "Bu hafta", öncekiler `H-{uzaklık}`)

```tsx
import { Line } from "react-chartjs-2";
import { chartToken } from "../shared/chartSetup";

export function RiskTrendChart({ trend }: { trend: number[] }) {
  const labels = trend.map((_, i) => (i === trend.length - 1 ? "Bu hafta" : `H-${trend.length - i}`));
  return (
    <div className="chart-container">
      <Line
        data={{
          labels,
          datasets: [
            {
              label: "İK Risk Skoru",
              data: trend,
              borderColor: chartToken("--primary", "#0f766e"),
              backgroundColor: "rgba(15, 118, 110, 0.12)",
              fill: true,
              borderWidth: 2.5,
              tension: 0.35,
              pointRadius: 3,
              pointBackgroundColor: chartToken("--surface", "#ffffff"),
              pointBorderColor: chartToken("--primary", "#0f766e"),
            },
          ],
        }}
        options={{
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false }, tooltip: { mode: "index", intersect: false } },
          scales: {
            y: { min: 0, max: 100, grid: { color: chartToken("--line-soft", "#e9efef") }, ticks: { stepSize: 20 } },
            x: { grid: { display: false } },
          },
        }}
      />
    </div>
  );
}
```

- [x] **Step 4: `RiskCenterPage.tsx` yaz** (eski Dashboard() DOM paritesi)

```tsx
import { useNavigate } from "react-router-dom";
import { appRoutes } from "../../routes";
import { PageError, PageLoading } from "../shared/PageState";
import { formatToday, getRiskLabel, getRiskLevel, priorityLabel, topPriorityActions } from "./format";
import { RiskTrendChart } from "./RiskTrendChart";
import {
  useComplianceRisk, useDashboardMetrics, useEmployeeVoice, useManagerLoad, useOpenActions,
} from "./queries";

const pathForRouteKey = (key?: string | null): string =>
  appRoutes.find((r) => r.key === key)?.path ?? "/actions";

export function RiskCenterPage() {
  const navigate = useNavigate();
  const metricsQ = useDashboardMetrics();
  const loadQ = useManagerLoad();
  const voiceQ = useEmployeeVoice();
  const complianceQ = useComplianceRisk();
  const actionsQ = useOpenActions();

  const queries = [metricsQ, loadQ, voiceQ, complianceQ, actionsQ];
  if (queries.some((q) => q.isPending)) return <PageLoading />;
  const failed = queries.find((q) => q.isError);
  if (failed) return <PageError error={failed.error} />;

  const metrics = metricsQ.data!;
  const load = loadQ.data!;
  const voice = voiceQ.data!;
  const compliance = complianceQ.data!;
  const actions = actionsQ.data!;

  const riskScore = metrics.riskScore ?? 0;
  const trend = metrics.riskTrend ?? [];
  const delta = trend.length >= 2 ? trend[trend.length - 1] - trend[0] : 0;
  const todayCount = actions.filter((a) => a.priority === "high").length;
  const weekCount = actions.filter((a) => a.priority === "medium").length;
  const watchCount = actions.filter((a) => a.priority === "low").length;

  return (
    <div className="dashboard-wrapper">
      <div className="welcome-header">
        <div>
          <h2>Risk & Aksiyon Merkezi</h2>
          <p className="text-muted">Bugünün odak sorusu: hangi risk büyüyor, neden büyüyor ve hangi aksiyon alınmalı?</p>
        </div>
        <div className="date-widget">
          <i aria-hidden="true" className="fa-regular fa-calendar" />
          <span>{formatToday()}</span>
        </div>
      </div>

      <div className="kpi-grid intelligence-kpis">
        <button className={`kpi-card risk-kpi ${getRiskLevel(riskScore)} metric-card-button`} onClick={() => navigate("/actions")}>
          <div className="kpi-icon bg-red-light"><i aria-hidden="true" className="fa-solid fa-shield-heart" /></div>
          <div className="kpi-content">
            <span className="kpi-label">İK Risk Skoru</span>
            <h3 className="kpi-value">{riskScore}<small>/100</small></h3>
            <span className="kpi-sub">{getRiskLabel(riskScore)} · 3 ana risk sürücüsü</span>
          </div>
        </button>
        <button className="kpi-card risk-kpi medium metric-card-button" onClick={() => navigate("/risk/manager-load")}>
          <div className="kpi-icon bg-orange-light"><i aria-hidden="true" className="fa-solid fa-user-group" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Yönetici Yük Endeksi</span>
            <h3 className="kpi-value">{metrics.managerLoadIndex ?? 0}<small>/100</small></h3>
            <span className="kpi-sub">{load.pendingApprovals ?? 0} onay, {load.openActions ?? 0} açık aksiyon, yoğun ekip</span>
          </div>
        </button>
        <button className="kpi-card risk-kpi high metric-card-button" onClick={() => navigate("/risk/attrition")}>
          <div className="kpi-icon bg-purple-light"><i aria-hidden="true" className="fa-solid fa-person-walking-arrow-right" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Ayrılma Riski Radarı</span>
            <h3 className="kpi-value">{metrics.attritionHigh ?? 0}</h3>
            <span className="kpi-sub">Yüksek riskli çalışan segmenti</span>
          </div>
        </button>
        <button className="kpi-card risk-kpi medium metric-card-button" onClick={() => navigate("/actions")}>
          <div className="kpi-icon bg-blue-light"><i aria-hidden="true" className="fa-solid fa-bolt" /></div>
          <div className="kpi-content">
            <span className="kpi-label">Bugünkü Kritik Aksiyon</span>
            <h3 className="kpi-value">{metrics.criticalActions ?? 0}</h3>
            <span className="kpi-sub">{todayCount} bugün, {weekCount} bu hafta, {watchCount} izlemede</span>
          </div>
        </button>
      </div>

      <div className="risk-dashboard-grid">
        <div className="risk-main">
          <div className="risk-decision-grid">
            <div className="card chart-card">
              <div className="card-header-clean">
                <div>
                  <h4>90 Günlük Risk Trendi</h4>
                  <p className="text-muted">Risk skoru son {trend.length} hafta içinde {delta >= 0 ? "yukarı" : "aşağı"} yönlü seyrediyor.</p>
                </div>
                <span className="status-pill pending">{delta >= 0 ? "+" : ""}{delta} puan</span>
              </div>
              <RiskTrendChart trend={trend} />
            </div>

            <div className="card heatmap-card">
              <div className="card-header-clean">
                <div>
                  <h4>Departman Bazlı Risk Isı Haritası</h4>
                  <p className="text-muted">Risk seviyesi ve öne çıkan nedenler.</p>
                </div>
              </div>
              <div className="risk-heatmap">
                {(metrics.departmentRisk ?? []).map((dept) => (
                  <div key={dept.departmentId} className={`heatmap-line ${getRiskLevel(dept.risk ?? 0)}`}>
                    <div className="heatmap-score">{dept.risk ?? 0}</div>
                    <div className="heatmap-body">
                      <strong>{dept.dept}</strong>
                      <span>{dept.employeeCount ?? 0} çalışan · {dept.highAttritionCount ?? 0} yüksek ayrılma · {dept.highBurnoutCount ?? 0} tükenmişlik sinyali</span>
                    </div>
                    <div className="heatmap-bar"><span style={{ width: `${dept.risk ?? 0}%` }} /></div>
                  </div>
                ))}
              </div>
            </div>
          </div>

          <div className="card capacity-card">
            <div className="card-header-clean">
              <div>
                <h4>Yetenek ve Kapasite</h4>
                <p className="text-muted">İşe alım, beceri, kritik rol ve kültür sinyalleri.</p>
              </div>
              <button className="btn btn-secondary btn-sm" onClick={() => navigate("/recruitment")}>
                <i aria-hidden="true" className="fa-solid fa-arrow-up-right-from-square" /> İşe alıma git
              </button>
            </div>
            <div className="capacity-grid">
              {(metrics.talentCapacity ?? []).map((item) => (
                <div key={item.label} className={`capacity-item ${item.tone ?? ""}`}>
                  <div className="capacity-top">
                    <span>{item.label}</span>
                    <strong>{item.value}{item.label === "Kritik Rol Riski" ? "" : "%"}</strong>
                  </div>
                  <div className="progress-bar">
                    <div className="fill" style={{ width: `${item.label === "Kritik Rol Riski" ? (item.value ?? 0) * 22 : item.value ?? 0}%` }} />
                  </div>
                  <small>{item.meta}</small>
                </div>
              ))}
            </div>
          </div>

          <div className="card signal-card">
            <div className="card-header-clean">
              <div>
                <h4>Kurumsal Sinyaller</h4>
                <p className="text-muted">Çalışan nabzı, sessiz memnuniyetsizlik ve evrak uyum risklerini birlikte izleyin.</p>
              </div>
              <div className="toolbar-actions">
                <button className="btn btn-secondary btn-sm" onClick={() => navigate("/risk/employee-voice")}>
                  <i aria-hidden="true" className="fa-solid fa-wave-square" /> Nabız detayı
                </button>
                <button className="btn btn-secondary btn-sm" onClick={() => navigate("/risk/compliance")}>
                  <i aria-hidden="true" className="fa-solid fa-file-shield" /> Uyum detayı
                </button>
              </div>
            </div>
            <div className="signal-grid">
              <button className="signal-item high metric-card-button" onClick={() => navigate("/risk/employee-voice")}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-heart-pulse" /></div>
                <div>
                  <span>Çalışan Nabız Skoru</span>
                  <strong>{voice.pulseScore ?? 0}<small>/100</small></strong>
                  <p>{voice.sentimentTrend}</p>
                </div>
              </button>
              <button className="signal-item medium metric-card-button" onClick={() => navigate("/risk/employee-voice")}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-comments" /></div>
                <div>
                  <span>eNPS / Bağlılık</span>
                  <strong>{(voice.eNps ?? 0) >= 0 ? "+" : ""}{voice.eNps ?? 0}</strong>
                  <p>{voice.decliningTeams ?? 0} ekipte sessiz memnuniyetsizlik sinyali var.</p>
                </div>
              </button>
              <button className="signal-item medium metric-card-button" onClick={() => navigate("/risk/compliance")}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-folder-check" /></div>
                <div>
                  <span>Evrak Uyum Skoru</span>
                  <strong>{compliance.documentComplianceScore ?? 0}<small>/100</small></strong>
                  <p>{compliance.missingDocuments ?? 0} eksik evrak, {compliance.upcomingDocuments ?? 0} yaklaşan son tarih.</p>
                </div>
              </button>
              <button className="signal-item medium metric-card-button" onClick={() => navigate("/risk/compliance")}>
                <div className="signal-icon"><i aria-hidden="true" className="fa-solid fa-clipboard-check" /></div>
                <div>
                  <span>Denetime Hazırlık</span>
                  <strong>{compliance.auditReadinessRisk}</strong>
                  <p>Hazırlık skoru {compliance.auditReadinessScore ?? 0}/100; kritik evrak aksiyonları izleniyor.</p>
                </div>
              </button>
            </div>
          </div>
        </div>

        <aside className="card action-center">
          <div className="card-header-clean">
            <div>
              <h4>En Acil Aksiyonlar</h4>
              <p className="text-muted">Önceliği en yüksek 3 müdahale.</p>
            </div>
            <button className="btn btn-secondary btn-sm" onClick={() => navigate("/actions")}>
              Tümünü aç ({actions.length})
            </button>
          </div>
          <div className="action-list">
            {topPriorityActions(actions, 3).map((action) => (
              <button key={action.id} className={`risk-action ${action.priority ?? ""}`} onClick={() => navigate(pathForRouteKey(action.sourceRoute))}>
                <div className="action-priority">{priorityLabel(action.priority)}</div>
                <strong>{action.title}</strong>
                <p>{action.source} · {action.owner}</p>
                {action.action && (
                  <div className="recommended-action">
                    <i aria-hidden="true" className="fa-solid fa-lightbulb" />
                    <span>{action.action}</span>
                  </div>
                )}
              </button>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}
```

- [x] **Step 5: `routes.tsx` `pageFor`'a ekle**

```tsx
import { RiskCenterPage } from "./features/dashboard/RiskCenterPage";
// ...
const pageFor: Record<string, () => JSX.Element> = {
  overview: OverviewPage,
  dashboard: RiskCenterPage,
};
```

- [x] **Step 6: Testleri doğrula**

Run: `npm test -- --run` → Beklenen: tümü PASS.

- [x] **Step 7: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): Risk Merkezi sayfası — 5 sorgu, trend grafiği, türetilmiş sinyaller"
```

---

### Task 4: Ayrılma Riski + Tükenmişlik detay sayfaları

**Files:**
- Create: `frontend/src/features/dashboard/BackToRisk.tsx`, `frontend/src/features/dashboard/AttritionDetailPage.tsx`, `frontend/src/features/dashboard/BurnoutDetailPage.tsx`
- Modify: `frontend/src/routes.tsx` (pageFor: attrition-risk, burnout-risk)
- Test: `frontend/src/features/dashboard/RiskDetailPages.test.tsx`

**Interfaces:**
- Consumes: `useAttritionDetail`, `useBurnoutDetail` (Task 3), `getLevelText`, `PageLoading/PageError`.
- Produces: `BackToRisk()` (diğer detay sayfaları da kullanır), `AttritionDetailPage()`, `BurnoutDetailPage()`.

- [x] **Step 1: Başarısız testleri yaz** — `RiskDetailPages.test.tsx`:

```tsx
import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AttritionDetailPage } from "./AttritionDetailPage";
import { BurnoutDetailPage } from "./BurnoutDetailPage";

const employee = {
  employeeId: 1, name: "Ahmet Yılmaz", title: "Senior Developer", dept: "Yazılım", manager: "Ece Arslan",
  absence: 18, lateness: 22, overtime: 74, unusedLeave: 82, pulse: 52, performance: 68,
  roleCriticality: 92, riskScore: 71, attritionRisk: "high", burnoutRisk: "high",
  trend: "Fazla mesai artıyor", action: "1:1 görüşme planla",
};
const detail = {
  highCount: 2, mediumCount: 1, criticalRoleCount: 1,
  averagePulse: 61, averageOvertime: 60, averageUnusedLeave: 64,
  employees: [employee],
};

beforeEach(() => stubApi({ "/api/dashboard/attrition": detail, "/api/dashboard/burnout": detail }));
afterEach(() => vi.unstubAllGlobals());

test("ayrılma detayı KPI ve tablo satırını gösterir", async () => {
  renderPage(<AttritionDetailPage />);
  expect(await screen.findByText("Ayrılma Riski Detayı")).toBeInTheDocument();
  expect(screen.getByText("Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("Kritik Rol Riski").closest(".stat-box")).toHaveTextContent("1");
});

test("tükenmişlik detayı ortalamaları ve seviye rozetini gösterir", async () => {
  renderPage(<BurnoutDetailPage />);
  expect(await screen.findByText("Tükenmişlik Sinyali")).toBeInTheDocument();
  expect(screen.getByText("Fazla Mesai Ort.").closest(".stat-box")).toHaveTextContent("60");
  expect(screen.getByText("74%")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/dashboard/RiskDetailPages.test.tsx` → Beklenen: FAIL.

- [x] **Step 2: `BackToRisk.tsx` yaz**

```tsx
import { useNavigate } from "react-router-dom";

export function BackToRisk() {
  const navigate = useNavigate();
  return (
    <button className="btn btn-secondary btn-sm" onClick={() => navigate("/dashboard")}>
      <i aria-hidden="true" className="fa-solid fa-arrow-left" /> Risk Merkezi
    </button>
  );
}
```

- [x] **Step 3: `AttritionDetailPage.tsx` yaz**

```tsx
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getLevelText } from "./format";
import { useAttritionDetail } from "./queries";

export function AttritionDetailPage() {
  const detail = useAttritionDetail();
  if (detail.isPending) return <PageLoading />;
  if (detail.isError) return <PageError error={detail.error} />;

  const data = detail.data;
  const employees = [...(data.employees ?? [])].sort((a, b) => (b.riskScore ?? 0) - (a.riskScore ?? 0));

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Ayrılma Riski Detayı</h2>
          <p>Riskli personelleri, sinyal nedenlerini ve önerilen takip aksiyonlarını görün.</p>
        </div>
        <BackToRisk />
      </div>
      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Yüksek Risk</span><strong className="sb-val text-red">{data.highCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Orta Risk</span><strong className="sb-val text-orange">{data.mediumCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Kritik Rol Riski</span><strong className="sb-val">{data.criticalRoleCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Ortalama Nabız</span><strong className="sb-val">{data.averagePulse ?? 0}<small>%</small></strong></div>
      </div>
      <div className="table-container">
        <table className="detail-table data-table">
          <thead>
            <tr><th>Personel</th><th>Departman</th><th>Yönetici</th><th>Risk</th><th>Son Sinyal</th><th>Önerilen Aksiyon</th></tr>
          </thead>
          <tbody>
            {employees.map((employee) => (
              <tr key={employee.employeeId}>
                <td><strong>{employee.name}</strong><small>{employee.title}</small></td>
                <td>{employee.dept}</td>
                <td>{employee.manager}</td>
                <td><span className={`risk-badge ${employee.attritionRisk ?? ""}`}>{getLevelText(employee.attritionRisk)}</span></td>
                <td>{employee.trend}</td>
                <td>{employee.action}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
```

- [x] **Step 4: `BurnoutDetailPage.tsx` yaz**

```tsx
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getLevelText } from "./format";
import { useBurnoutDetail } from "./queries";

export function BurnoutDetailPage() {
  const detail = useBurnoutDetail();
  if (detail.isPending) return <PageLoading />;
  if (detail.isError) return <PageError error={detail.error} />;

  const data = detail.data;
  const employees = [...(data.employees ?? [])].sort(
    (a, b) => (b.overtime ?? 0) + (b.unusedLeave ?? 0) - ((a.overtime ?? 0) + (a.unusedLeave ?? 0)),
  );

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Tükenmişlik Sinyali</h2>
          <p>Fazla mesai, kullanılmayan izin, geç çıkış ve ekip yoğunluğu kırılımlarını izleyin.</p>
        </div>
        <BackToRisk />
      </div>
      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Yüksek Sinyal</span><strong className="sb-val text-red">{data.highCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Fazla Mesai Ort.</span><strong className="sb-val">{data.averageOvertime ?? 0}<small>%</small></strong></div>
        <div className="stat-box"><span className="sb-label">Kullanılmayan İzin</span><strong className="sb-val">{data.averageUnusedLeave ?? 0}<small>%</small></strong></div>
        <div className="stat-box"><span className="sb-label">Orta Sinyal</span><strong className="sb-val">{data.mediumCount ?? 0}</strong></div>
      </div>
      <div className="table-container">
        <table className="detail-table data-table">
          <thead>
            <tr><th>Personel</th><th>Departman</th><th>Fazla Mesai</th><th>Kullanılmayan İzin</th><th>Nabız</th><th>Seviye</th><th>Önerilen Aksiyon</th></tr>
          </thead>
          <tbody>
            {employees.map((employee) => (
              <tr key={employee.employeeId}>
                <td><strong>{employee.name}</strong><small>{employee.title}</small></td>
                <td>{employee.dept}</td>
                <td>{employee.overtime}%</td>
                <td>{employee.unusedLeave}%</td>
                <td>{employee.pulse}%</td>
                <td><span className={`risk-badge ${employee.burnoutRisk ?? ""}`}>{getLevelText(employee.burnoutRisk)}</span></td>
                <td>{employee.action}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
```

- [x] **Step 5: `routes.tsx` `pageFor`'a ekle** (`attrition-risk: AttritionDetailPage, "burnout-risk": BurnoutDetailPage` — anahtarlar tırnaklı yazılır çünkü tire içerir)

- [x] **Step 6: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 7: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): ayrılma ve tükenmişlik risk detay sayfaları"
```

---

### Task 5: Yönetici Yükü + Çalışan Nabzı detay sayfaları

**Files:**
- Create: `frontend/src/features/dashboard/ManagerLoadPage.tsx`, `frontend/src/features/dashboard/EmployeeVoicePage.tsx`
- Modify: `frontend/src/routes.tsx` (pageFor: manager-load, employee-voice)
- Test: `frontend/src/features/dashboard/ManagerVoicePages.test.tsx`

**Interfaces:**
- Consumes: `useManagerLoad`, `useEmployeeVoice` (Task 3), `getRiskLevel`, `getLevelText`, `BackToRisk`.
- Produces: `ManagerLoadPage()`, `EmployeeVoicePage()`.

- [x] **Step 1: Başarısız testleri yaz** — `ManagerVoicePages.test.tsx`:

```tsx
import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { EmployeeVoicePage } from "./EmployeeVoicePage";
import { ManagerLoadPage } from "./ManagerLoadPage";

const managerLoad = {
  managerLoadIndex: 71, criticalManagerCount: 1, pendingApprovals: 5, openActions: 14,
  managers: [
    { employeeId: 2, name: "Ece Arslan", team: 18, approvals: 5, actions: 7, overtime: 68, pulse: 59, load: 78 },
    { employeeId: 3, name: "Can Uslu", team: 9, approvals: 1, actions: 2, overtime: 34, pulse: 76, load: 38 },
  ],
};
const voice = {
  pulseScore: 64, eNps: 8, participationRate: 76, decliningTeams: 2,
  sentimentTrend: "Son nabız ölçümünde 2 ekipte bağlılık geriledi",
  departments: [
    { departmentId: 1, dept: "Yazılım", pulse: 58, eNps: 2, participation: 82, mood: "Baskı yüksek", driver: "Teslim takvimi", level: "high" },
    { departmentId: 2, dept: "İK", pulse: 78, eNps: 24, participation: 88, mood: "Pozitif", driver: "Net iletişim", level: "low" },
  ],
  signals: ["Yazılım ekibinde teslim takvimi kaynaklı sinyal izleniyor."],
  recommendedActions: ["Yönetici ile 1:1 görüşme başlat"],
};

beforeEach(() => stubApi({ "/api/dashboard/manager-load": managerLoad, "/api/dashboard/employee-voice": voice }));
afterEach(() => vi.unstubAllGlobals());

test("yönetici yükü tablosu kritik takibi işaretler", async () => {
  renderPage(<ManagerLoadPage />);
  expect(await screen.findByText("Ece Arslan")).toBeInTheDocument();
  expect(screen.getByText("Kritik takip")).toBeInTheDocument();
  expect(screen.getByText("Aksiyon devri ve kapasite görüşmesi")).toBeInTheDocument();
  expect(screen.getByText("Haftalık takip yeterli")).toBeInTheDocument();
});

test("nabız sayfası departman tablosu + türetilmiş riskli ekipleri gösterir", async () => {
  renderPage(<EmployeeVoicePage />);
  expect(await screen.findByText("Çalışan Sesleri / Nabız Analitiği")).toBeInTheDocument();
  expect(screen.getByText("Baskı yüksek")).toBeInTheDocument(); // tabloda mood
  // Riskli Ekipler aside yalnız level != low: Yazılım var, İK yok
  const aside = screen.getByText("Riskli Ekipler").closest("aside")!;
  expect(aside).toHaveTextContent("Yazılım");
  expect(aside).not.toHaveTextContent("İK");
});
```

Run: `npm test -- --run src/features/dashboard/ManagerVoicePages.test.tsx` → Beklenen: FAIL.

- [x] **Step 2: `ManagerLoadPage.tsx` yaz**

```tsx
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getRiskLevel } from "./format";
import { useManagerLoad } from "./queries";

export function ManagerLoadPage() {
  const load = useManagerLoad();
  if (load.isPending) return <PageLoading />;
  if (load.isError) return <PageError error={load.error} />;

  const data = load.data;

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Yönetici Yükü Detayı</h2>
          <p>Yönetici bazlı ekip büyüklüğü, bekleyen onay, açık aksiyon ve ekip nabzını görün.</p>
        </div>
        <BackToRisk />
      </div>
      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Yük Endeksi</span><strong className="sb-val text-orange">{data.managerLoadIndex ?? 0}<small>/100</small></strong></div>
        <div className="stat-box"><span className="sb-label">Kritik Yönetici</span><strong className="sb-val">{data.criticalManagerCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Bekleyen Onay</span><strong className="sb-val">{data.pendingApprovals ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Açık Aksiyon</span><strong className="sb-val">{data.openActions ?? 0}</strong></div>
      </div>
      <div className="table-container">
        <table className="detail-table data-table">
          <thead>
            <tr><th>Yönetici</th><th>Ekip</th><th>Onay</th><th>Aksiyon</th><th>Fazla Mesai</th><th>Ekip Nabzı</th><th>Yük</th><th>Öneri</th></tr>
          </thead>
          <tbody>
            {(data.managers ?? []).map((manager) => (
              <tr key={manager.employeeId}>
                <td><strong>{manager.name}</strong><small>{(manager.load ?? 0) > 70 ? "Kritik takip" : "Normal takip"}</small></td>
                <td>{manager.team} kişi</td>
                <td>{manager.approvals}</td>
                <td>{manager.actions}</td>
                <td>{manager.overtime}%</td>
                <td>{manager.pulse}%</td>
                <td><span className={`risk-badge ${getRiskLevel(manager.load ?? 0)}`}>{manager.load}/100</span></td>
                <td>{(manager.load ?? 0) > 70 ? "Aksiyon devri ve kapasite görüşmesi" : "Haftalık takip yeterli"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
```

- [x] **Step 3: `EmployeeVoicePage.tsx` yaz** (riskTeams aside `level !== "low"` departmanlardan türetilir)

```tsx
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getLevelText } from "./format";
import { useEmployeeVoice } from "./queries";

export function EmployeeVoicePage() {
  const voice = useEmployeeVoice();
  if (voice.isPending) return <PageLoading />;
  if (voice.isError) return <PageError error={voice.error} />;

  const data = voice.data;
  const departments = data.departments ?? [];
  const riskTeams = departments.filter((d) => d.level !== "low").slice(0, 3);

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Çalışan Sesleri / Nabız Analitiği</h2>
          <p>Departman bazlı ruh hali, bağlılık sinyali ve takip önerilerini tek görünümde izleyin.</p>
        </div>
        <BackToRisk />
      </div>

      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Nabız Skoru</span><strong className="sb-val text-orange">{data.pulseScore ?? 0}<small>/100</small></strong></div>
        <div className="stat-box"><span className="sb-label">eNPS</span><strong className="sb-val">{(data.eNps ?? 0) >= 0 ? "+" : ""}{data.eNps ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Katılım Oranı</span><strong className="sb-val">{data.participationRate ?? 0}<small>%</small></strong></div>
        <div className="stat-box"><span className="sb-label">Düşen Takım</span><strong className="sb-val text-red">{data.decliningTeams ?? 0}</strong></div>
      </div>

      <div className="voice-layout">
        <div className="table-container">
          <table className="detail-table data-table">
            <thead>
              <tr><th>Departman</th><th>Ruh Hali</th><th>Nabız</th><th>eNPS</th><th>Katılım</th><th>Öne Çıkan Sinyal</th><th>Seviye</th></tr>
            </thead>
            <tbody>
              {departments.map((department) => (
                <tr key={department.departmentId}>
                  <td><strong>{department.dept}</strong></td>
                  <td>{department.mood}</td>
                  <td>{department.pulse}/100</td>
                  <td>{(department.eNps ?? 0) > 0 ? "+" : ""}{department.eNps}</td>
                  <td>{department.participation}%</td>
                  <td>{department.driver}</td>
                  <td><span className={`risk-badge ${department.level ?? ""}`}>{getLevelText(department.level)}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <aside className="card insight-panel">
          <div className="card-header-clean">
            <div>
              <h4>Riskli Ekipler</h4>
              <p className="text-muted">Sessiz memnuniyetsizlik ve bağlılık kırılmaları.</p>
            </div>
          </div>
          <div className="action-list">
            {riskTeams.map((team) => (
              <div key={team.departmentId} className={`risk-action ${team.level ?? ""}`}>
                <div className="action-priority">{team.mood}</div>
                <strong>{team.dept}</strong>
                <p>{team.driver}</p>
              </div>
            ))}
          </div>
        </aside>
      </div>

      <div className="detail-support-grid">
        <section className="card">
          <div className="card-header-clean"><h4>Son Nabız Sinyalleri</h4></div>
          <div className="signal-list">
            {(data.signals ?? []).map((signal) => (
              <div key={signal} className="signal-note"><i aria-hidden="true" className="fa-solid fa-circle-info" /><span>{signal}</span></div>
            ))}
          </div>
        </section>
        <section className="card">
          <div className="card-header-clean"><h4>Önerilen Aksiyonlar</h4></div>
          <div className="signal-list">
            {(data.recommendedActions ?? []).map((action) => (
              <div key={action} className="signal-note action"><i aria-hidden="true" className="fa-solid fa-check" /><span>{action}</span></div>
            ))}
          </div>
        </section>
      </div>
    </div>
  );
}
```

- [x] **Step 4: `routes.tsx` `pageFor`'a ekle** (`"manager-load": ManagerLoadPage, "employee-voice": EmployeeVoicePage`)

- [x] **Step 5: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 6: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): yönetici yükü ve çalışan nabzı detay sayfaları"
```

---

### Task 6: Uyum Risk Merkezi detay sayfası

**Files:**
- Create: `frontend/src/features/dashboard/ComplianceRiskPage.tsx`
- Modify: `frontend/src/routes.tsx` (pageFor: compliance-risk)
- Test: `frontend/src/features/dashboard/ComplianceRiskPage.test.tsx`

**Interfaces:**
- Consumes: `useComplianceRisk` (Task 3), `getLevelText`, `BackToRisk`.
- Produces: `ComplianceRiskPage()`.

- [x] **Step 1: Başarısız testi yaz** — `ComplianceRiskPage.test.tsx`:

```tsx
import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ComplianceRiskPage } from "./ComplianceRiskPage";

const compliance = {
  documentComplianceScore: 82, missingDocuments: 11, upcomingDocuments: 18,
  auditReadinessRisk: "Orta", auditReadinessScore: 74,
  records: [
    { id: 1, employee: "Burak Demir", dept: "Satış", document: "KVKK açık rıza eki", owner: "İK Operasyon", dueDate: "Bugün", status: "Eksik", level: "high" },
    { id: 2, employee: "Ayşe Vural", dept: "İK", document: "Personel dosyası kontrolü", owner: "İK Operasyon", dueDate: "Tamamlandı", status: "Tamamlandı", level: "low" },
  ],
  deadlines: [
    { title: "KVKK açık rıza eki", count: 4, dueDate: "Bugün", owner: "İK Operasyon", level: "high" },
  ],
};

beforeEach(() => stubApi({ "/api/dashboard/compliance": compliance }));
afterEach(() => vi.unstubAllGlobals());

test("uyum tablosu durum pill'lerini doğru sınıfla basar", async () => {
  renderPage(<ComplianceRiskPage />);
  expect(await screen.findByText("Uyum, Evrak ve Denetim Risk Merkezi")).toBeInTheDocument();
  expect(screen.getByText("Eksik")).toHaveClass("status-pill", "rejected");
  expect(screen.getByText("Tamamlandı", { selector: ".status-pill" })).toHaveClass("approved");
});

test("yaklaşan son tarihler aside'ı dolar", async () => {
  renderPage(<ComplianceRiskPage />);
  expect(await screen.findByText("Yaklaşan Son Tarihler")).toBeInTheDocument();
  expect(screen.getByText("4 kayıt · İK Operasyon")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/dashboard/ComplianceRiskPage.test.tsx` → Beklenen: FAIL.

- [x] **Step 2: `ComplianceRiskPage.tsx` yaz** (detail-support-grid yok — Dilim 6'da)

```tsx
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "./BackToRisk";
import { getLevelText } from "./format";
import { useComplianceRisk } from "./queries";

const statusPillClass = (status?: string | null): string =>
  status === "Tamamlandı" ? "approved" : status === "Eksik" ? "rejected" : "pending";

export function ComplianceRiskPage() {
  const compliance = useComplianceRisk();
  if (compliance.isPending) return <PageLoading />;
  if (compliance.isError) return <PageError error={compliance.error} />;

  const data = compliance.data;

  return (
    <div className="detail-page">
      <div className="page-header">
        <div>
          <h2>Uyum, Evrak ve Denetim Risk Merkezi</h2>
          <p>Eksik evrak, yaklaşan son tarih ve denetim hazırlığını operasyonel takip görünümünde yönetin.</p>
        </div>
        <BackToRisk />
      </div>

      <div className="detail-kpi-grid">
        <div className="stat-box"><span className="sb-label">Evrak Uyum Skoru</span><strong className="sb-val">{data.documentComplianceScore ?? 0}<small>/100</small></strong></div>
        <div className="stat-box"><span className="sb-label">Eksik Evrak</span><strong className="sb-val text-red">{data.missingDocuments ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Süresi Yaklaşan</span><strong className="sb-val text-orange">{data.upcomingDocuments ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Denetim Riski</span><strong className="sb-val text-orange">{data.auditReadinessRisk}</strong></div>
      </div>

      <div className="compliance-layout">
        <div className="table-container">
          <table className="detail-table data-table">
            <thead>
              <tr><th>Personel</th><th>Departman</th><th>Evrak</th><th>Sorumlu</th><th>Son Tarih</th><th>Durum</th><th>Risk</th></tr>
            </thead>
            <tbody>
              {(data.records ?? []).map((record) => (
                <tr key={record.id}>
                  <td><strong>{record.employee}</strong></td>
                  <td>{record.dept}</td>
                  <td>{record.document}</td>
                  <td>{record.owner}</td>
                  <td>{record.dueDate}</td>
                  <td><span className={`status-pill ${statusPillClass(record.status)}`}>{record.status}</span></td>
                  <td><span className={`risk-badge ${record.level ?? ""}`}>{getLevelText(record.level)}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <aside className="card insight-panel">
          <div className="card-header-clean">
            <div>
              <h4>Yaklaşan Son Tarihler</h4>
              <p className="text-muted">Kritik evrak aksiyonları ve sorumlular.</p>
            </div>
          </div>
          <div className="deadline-list">
            {(data.deadlines ?? []).map((deadline) => (
              <div key={deadline.title} className={`deadline-item ${deadline.level ?? ""}`}>
                <div>
                  <strong>{deadline.title}</strong>
                  <span>{deadline.count} kayıt · {deadline.owner}</span>
                </div>
                <em>{deadline.dueDate}</em>
              </div>
            ))}
          </div>
        </aside>
      </div>
    </div>
  );
}
```

- [x] **Step 3: `routes.tsx` `pageFor`'a ekle** (`"compliance-risk": ComplianceRiskPage`)

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.
Ek olarak build: `npm run build` → hatasız.

- [x] **Step 5: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): uyum risk merkezi detay sayfası"
```

---

### Task 7: Uçtan uca duman testi + görsel parite + günlük güncelleme

**Files:**
- Modify: `docs/gelistirme-gunlugu.md` (yeni kayıt + "Şu an neredeyiz"); gerekirse bulunan küçük hatalar düzeltilir.

- [x] **Step 1: Backend + frontend'i başlat**

Terminal 1: `cd backend && dotnet run --project src/Ekipzen.API --launch-profile http`
Terminal 2: `cd frontend && npm run dev` → `http://localhost:5173`

- [x] **Step 2: Duman testi (hr-admin: ik@hrmaster.local / demo123)**

1. `#/dashboard` → KPI kartları gerçek sayılarla dolar; risk trendi çizgi grafiği çizilir; ısı haritası departman satırları gelir.
2. "En Acil Aksiyonlar" kartına tıkla → ilgili risk detayına gider.
3. KPI kartlarından `#/risk/attrition` ve `#/risk/manager-load` → tablolar dolu.
4. `#/risk/employee-voice` → departman tablosu + Riskli Ekipler aside; `#/risk/compliance` → evrak tablosu + son tarihler.
5. `#/overview` → KPI + doughnut + bar grafikler; Anlık Çalışma Durumu üç satır.
6. Rol değiştirici → Çalışan → `#/overview` açılır (employee overview'a erişebilir, dashboard'a erişemez).
7. Backend'i durdur, dashboard'ı yenile → `PageError` ("Veri yüklenemedi") ekranı; backend'i tekrar başlat.

- [x] **Step 3: Görsel parite kontrolü**

Eski uygulamayı statik sunucuyla aç (kök `index.html`, ör. `npx http-server -p 4173 -s .`), hr-admin ile giriş yap. Eski/yeni `#/dashboard`, `#/overview` ve 5 risk detayını yan yana karşılaştır (renk, boşluk, ikon, tipografi). **Bilinçli farklar:** ısı haritası satır metinleri, KPI alt metinleri, Overview 4. KPI ("Bugün İzinli"), Overview alt grid'in olmaması, nabız sayfasında riskli ekip kartlarının sade hali, uyum sayfasında destek grid'inin olmaması — bunlar veri eşleme kararlarıdır, DOM/class farkı değildir. Bunun dışında fark bulunursa DOM/class düzeltmesi yap (CSS'e dokunma).

- [x] **Step 4: Günlüğü güncelle ve kapanış commit'i**

`docs/gelistirme-gunlugu.md`: "Şu an neredeyiz" → Dilim 3 (Personel + Departman) planı; kayıtlara Dilim 2 özeti eklenir.

```bash
git add frontend/ docs/
git commit -m "test(frontend): dilim 2 duman testi, parite kontrolü ve günlük güncellemesi"
```

---

## Sonraki dilimler

Dilim 3 (Personel + Departman) planı bu dilim main'e merge edildikten sonra yazılır. `pageFor` eşlemesi büyüdükçe `routes.tsx` aynı desenle genişler.
