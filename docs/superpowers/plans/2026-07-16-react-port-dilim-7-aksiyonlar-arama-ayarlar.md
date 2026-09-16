# React Port Dilim 7: Aksiyon Merkezi + Global Arama + Ayarlar + Yönetici Konsolu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eski mock `#/actions` (+`#/risk/action-center`), header global arama, `#/settings` ve `#/manager` ekranlarını gerçek backend uçlarına bağlı React sayfaları olarak porta et.

**Architecture:** Önceki dilimlerle aynı desen — `features/actions/`, `features/settings/`, `features/manager/` altında `queries.ts` + sayfa component'leri; `layout/GlobalSearch.tsx` gerçek `/api/search` sonuçlarıyla genişletilir. Eski DOM/class yapısı birebir korunur. `routes.tsx` `pageFor`'a `actions`, `action-center`, `settings`, `manager` eklenir.

**Tech Stack:** React 18 + TypeScript, TanStack Query, react-chartjs-2 (mevcut `chartSetup`), Vitest + RTL (`stubApi`/`renderPage`), mevcut CSS (`styles/actions.css`, `styles/settings.css`, `styles/manager.css`).

## Global Constraints

- Eski DOM paritesi: class adları, id'ler, ikonlar ve Türkçe metinler `components/actions.js`, `components/settings.js`, `components/manager.js`, `components/layout.js` ile birebir; bilinçli farklar görev notlarında.
- Renk/stil hardcode yok; mevcut class'lar ve token'lar kullanılır.
- TDD: önce başarısız test, sonra implementasyon, sonra `npm test -- --run` (frontend/ içinde).
- API çağrıları yalnız `apiFetch`/`apiDownload`; tipler `src/api/schema.d.ts`'ten.
- Rol denetimi `useAuth()` ile; test çoklu-eşleşme tuzağı: aynı metin birden çok yerde geçiyorsa `getAllByText`/`findAllByText` veya rol+exact kullan.

### API sözleşmesi (backend hazır, değişmez)

Aksiyonlar (`ActionsController`, sınıf `[Authorize]`):
- `GET /api/actions?priority=&source=&owner=&status=` → `GlobalActionDto[]` (`id,title,source,sourceRoute,owner,due,priority,status,action`) — tüm roller
- `GET /api/actions/badge` → `{openCount}` (AppShell'de zaten kullanılıyor)
- `POST /api/actions` (`CreateGlobalActionCommand`: `title,source,owner,sourceRoute?,due?,priority,recommendedAction?`) — yalnız hr-admin
- `PATCH /api/actions/{id}/status` `{status}` — hr-admin+manager; **ileri yönlü** `open→week→done`, geri dönüş 409
- `DELETE /api/actions/{id}` → 204 — yalnız hr-admin
- `GET /api/audit-logs?module=&search=&take=50` → `AuditLogDto[]` (`id,actor,action,module,detail,entityName,entityId,createdAtUtc`) — hr-admin+manager
- `GET /api/search?q=` → `SearchResultDto[]` (`type,label,hint,routeKey,entityId`) — tüm roller (personel rol kapsamlı + aksiyon + aday hr-admin)
- Katalog: `priority: high|medium|low`; `status: open|week|done`

Ayarlar (`SettingsController` yalnız hr-admin; logo GET tüm roller):
- `GET /api/settings` → `SettingsDto` (`company{name,website,systemEmail,phone,headquartersAddress,logoPath}`, `notifications{newPersonnelEmail,leaveRequestEmail,weeklyReportEmail}`, `security{twoFactorSmsEnabled}`, `subscription{plan,planName,billingCycle,price,renewalDate,paymentMethodMasked}`)
- `PUT /api/settings/company` (`UpdateCompanyProfileCommand`), `PUT /api/settings/notifications`, `PUT /api/settings/security`
- `POST /api/settings/company/logo` (FormData `file`, ≤4MB) → `{logoPath}`; `GET /api/settings/company/logo` → dosya
- Şifre: `POST /api/auth/change-password` (`{currentPassword,newPassword}`) — mevcut uç

Yönetici konsolu (özel uç yok — mevcut uçlar):
- `GET /api/dashboard/overview` → `OverviewDto` (`onLeaveToday`, `pendingApprovals` KPI'ları) — mevcut `useOverview`
- `GET /api/leaves/pending` + `POST /api/leaves/{id}/approve|reject` — mevcut `usePendingLeaves`/`useDecideLeave`

Mevcut yardımcılar: `formatTimeAgo` (`features/recruitment/format`), `tableToCsvLines`/`downloadCsv` (`features/shared/csv`), `chartToken` (`features/shared/chartSetup`), `useOverview` (`features/overview/queries`), `usePendingLeaves`/`useDecideLeave` (`features/leaves/queries`).

---

### Task 1: Aksiyon format yardımcıları + query katmanı

**Files:**
- Create: `frontend/src/features/actions/format.ts`
- Create: `frontend/src/features/actions/queries.ts`
- Test: `frontend/src/features/actions/format.test.ts`

**Interfaces:**
- Produces (format):
  - `actionLevelText(level?: string | null): string` — `high→Yüksek, medium→Orta, low→Düşük, diğer→Normal`
  - `actionStatusText(status?: string | null): string` — `open→Açık, week→Bu Hafta, done→Tamamlandı, diğer→Açık`
  - `actionPillClass(priority?: string | null): string` — `high→rejected, medium→pending, diğer→approved`
  - `nextActionStatus(status?: string | null): string | null` — `open→week, week→done, done→null`
  - `nextActionStatusLabel(status?: string | null): string | null` — `open→"Bu haftaya al", week→"Tamamlandı işaretle", done→null`
- Produces (queries):
  - `GlobalActionDto`, `AuditLogDto`, `CreateGlobalActionCommand` tipleri
  - `ActionFilters = { priority: string; source: string; owner: string }`
  - `useGlobalActions(filters: ActionFilters)` — `GET /actions` (boş filtreler param'a yazılmaz)
  - `useAuditLogs(enabled: boolean)` — `GET /audit-logs`
  - `useCreateGlobalAction()`, `useSetActionStatus()` (`{id,status}`), `useDeleteGlobalAction()` (`id`) — hepsi `["actions"]` invalidate eder (AppShell rozeti de `["actions","badge"]` altında olduğundan tazelenir)

- [x] **Step 1: Başarısız testleri yaz** — `format.test.ts`:

```ts
import { expect, test } from "vitest";
import {
  actionLevelText, actionPillClass, actionStatusText, nextActionStatus, nextActionStatusLabel,
} from "./format";

test("öncelik/durum etiketleri eski eşlemelerle birebir", () => {
  expect(actionLevelText("high")).toBe("Yüksek");
  expect(actionLevelText("medium")).toBe("Orta");
  expect(actionLevelText("low")).toBe("Düşük");
  expect(actionLevelText("bilinmeyen")).toBe("Normal");
  expect(actionStatusText("open")).toBe("Açık");
  expect(actionStatusText("week")).toBe("Bu Hafta");
  expect(actionStatusText("done")).toBe("Tamamlandı");
  expect(actionStatusText(null)).toBe("Açık");
});

test("öncelik pill sınıfı eski üçlü eşleme", () => {
  expect(actionPillClass("high")).toBe("rejected");
  expect(actionPillClass("medium")).toBe("pending");
  expect(actionPillClass("low")).toBe("approved");
});

test("ileri yönlü durum geçişi", () => {
  expect(nextActionStatus("open")).toBe("week");
  expect(nextActionStatus("week")).toBe("done");
  expect(nextActionStatus("done")).toBeNull();
  expect(nextActionStatusLabel("open")).toBe("Bu haftaya al");
  expect(nextActionStatusLabel("week")).toBe("Tamamlandı işaretle");
  expect(nextActionStatusLabel("done")).toBeNull();
});
```

Run: `npm test -- --run src/features/actions/format.test.ts` → FAIL.

- [x] **Step 2: `format.ts` yaz**

```ts
export const actionLevelText = (level?: string | null): string =>
  ({ high: "Yüksek", medium: "Orta", low: "Düşük" })[level ?? ""] ?? "Normal";

export const actionStatusText = (status?: string | null): string =>
  ({ open: "Açık", week: "Bu Hafta", done: "Tamamlandı" })[status ?? ""] ?? "Açık";

export const actionPillClass = (priority?: string | null): string =>
  priority === "high" ? "rejected" : priority === "medium" ? "pending" : "approved";

export const nextActionStatus = (status?: string | null): string | null =>
  status === "open" ? "week" : status === "week" ? "done" : null;

export const nextActionStatusLabel = (status?: string | null): string | null =>
  status === "open" ? "Bu haftaya al" : status === "week" ? "Tamamlandı işaretle" : null;
```

- [x] **Step 3: `queries.ts` yaz**

```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type GlobalActionDto = components["schemas"]["GlobalActionDto"];
export type AuditLogDto = components["schemas"]["AuditLogDto"];
export type CreateGlobalActionCommand = components["schemas"]["CreateGlobalActionCommand"];

export type ActionFilters = { priority: string; source: string; owner: string };

const actionsPath = (filters: ActionFilters): string => {
  const params = new URLSearchParams();
  if (filters.priority) params.set("priority", filters.priority);
  if (filters.source) params.set("source", filters.source);
  if (filters.owner) params.set("owner", filters.owner);
  const query = params.toString();
  return query ? `/actions?${query}` : "/actions";
};

export const useGlobalActions = (filters: ActionFilters) =>
  useQuery({
    queryKey: ["actions", "list", filters],
    queryFn: () => apiFetch<GlobalActionDto[]>(actionsPath(filters)),
  });

export const useAuditLogs = (enabled: boolean) =>
  useQuery({
    queryKey: ["actions", "audit"],
    queryFn: () => apiFetch<AuditLogDto[]>("/audit-logs"),
    enabled,
  });

export const useCreateGlobalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateGlobalActionCommand) =>
      apiFetch<GlobalActionDto>("/actions", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["actions"] }),
  });
};

export const useSetActionStatus = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiFetch<GlobalActionDto>(`/actions/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["actions"] }),
  });
};

export const useDeleteGlobalAction = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiFetch<void>(`/actions/${id}`, { method: "DELETE" }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["actions"] }),
  });
};
```

Not: `apiFetch<void>` 204'te gövde parse etmemeli — `src/api/client.ts`'e bak; 204 desteği yoksa (JSON parse hatası atarsa) `apiFetch` içinde `response.status === 204 ? undefined : ...` düzeltmesini bu görevde yap ve mevcut testlerin geçtiğini doğrula.

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/actions/ frontend/src/api/client.ts
git commit -m "feat(frontend): aksiyon format yardımcıları ve query katmanı"
```

---

### Task 2: ActionsPage kabuğu — KPI + filtreler + sekmeler + denetim izi + rota

**Files:**
- Create: `frontend/src/features/actions/ActionsPage.tsx`
- Modify: `frontend/src/routes.tsx` (`pageFor`'a `actions` ve `action-center` eklenir)
- Test: `frontend/src/features/actions/ActionsPage.test.tsx`

**Interfaces:**
- Consumes: `useGlobalActions`, `useAuditLogs`, format yardımcıları, `formatTimeAgo` (`../recruitment/format`), `useAuth`, `useNavigate`.
- Produces: `ActionsPage()`. İç state: `tab: "open" | "week" | "done" | "audit"`, `filters: ActionFilters`, `createOpen: boolean` (Task 3 kullanır). Kart eylem placeholder'ları Task 3'te dolar.

Davranış: eski `ActionsCenter()` DOM'u (`#actions-screen`). KPI'lar listeden türetilir (eski formüller): Bugün=`due==="Bugün"`, Geciken=`due==="Gecikti"`, Yüksek=`priority==="high" && status!=="done"`, Tamamlanan=`status==="done"`. Filtreler server-side (`priority/source/owner`); kaynak/sahip seçenekleri filtresiz listeden türetilir. Bilinçli farklar: filtreler server-side (eskide DOM gizleme), Denetim İzi sekmesi yalnız hr-admin+manager (uç Management; eskide herkese görünürdü), denetim izi gerçek `audit-logs` verisi (`formatTimeAgo` ile), "Kaynağa git" `sourceRoute` yoksa gizlenir.

- [x] **Step 1: Başarısız testleri yaz** — `ActionsPage.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { ActionsPage } from "./ActionsPage";

const actions = [
  { id: 1, title: "SGK matrah kontrolü", source: "Bordro", sourceRoute: "payroll", owner: "İK Operasyon", due: "Bugün", priority: "high", status: "open", action: "Tavana yaklaşan kayıtları incele." },
  { id: 2, title: "Eksik evrak takibi", source: "Uyum", sourceRoute: "compliance-risk", owner: "Ece Arslan", due: "3 gün", priority: "medium", status: "week", action: "KVKK eklerini tamamlat." },
  { id: 3, title: "Anket sonuç raporu", source: "Nabız", sourceRoute: null, owner: "İK Operasyon", due: "Gecikti", priority: "low", status: "done", action: "Raporu arşivle." },
];
const audit = [
  { id: 9, actor: "İK Yöneticisi", action: "Bordro dönemi onaylandı", module: "Bordro", detail: "Temmuz 2026 dönemi onaylandı.", entityName: "PayrollPeriod", entityId: "3", createdAtUtc: "2026-07-16T09:00:00Z" },
];

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: 5 },
  }));

beforeEach(() => {
  localStorage.clear();
  setRole("hr-admin");
  stubApi({ "/api/actions": actions, "/api/audit-logs": audit });
});
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <ActionsPage />
      </ToastProvider>
    </AuthProvider>,
  );

test("KPI'lar listeden türetilir, açık sekme kartları gösterir", async () => {
  renderShell();
  expect(await screen.findByText("Global Aksiyon Merkezi")).toBeInTheDocument();
  expect(screen.getByText("SGK matrah kontrolü")).toBeInTheDocument();
  // Açık sekmede yalnız status=open kartlar
  expect(screen.queryByText("Eksik evrak takibi")).not.toBeInTheDocument();
  expect(screen.getByText("Tavana yaklaşan kayıtları incele.")).toBeInTheDocument();
});

test("öncelik filtresi server-side sorgu atar", async () => {
  renderShell();
  await screen.findByText("SGK matrah kontrolü");
  await userEvent.selectOptions(screen.getByLabelText("Öncelik filtresi"), "high");
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/actions?priority=high");
    expect(hit).toBe(true);
  });
});

test("Bu Hafta sekmesi week kartlarını gösterir", async () => {
  renderShell();
  await screen.findByText("SGK matrah kontrolü");
  await userEvent.click(screen.getByRole("button", { name: "Bu Hafta" }));
  expect(screen.getByText("Eksik evrak takibi")).toBeInTheDocument();
  expect(screen.queryByText("SGK matrah kontrolü")).not.toBeInTheDocument();
});

test("Denetim İzi sekmesi gerçek audit kayıtlarını gösterir", async () => {
  renderShell();
  await screen.findByText("SGK matrah kontrolü");
  await userEvent.click(screen.getByRole("button", { name: "Denetim İzi" }));
  expect(await screen.findByText("Bordro dönemi onaylandı")).toBeInTheDocument();
  expect(screen.getByText("İK Yöneticisi · Bordro")).toBeInTheDocument();
});

test("employee rolünde Denetim İzi sekmesi görünmez", async () => {
  setRole("employee");
  renderShell();
  await screen.findByText("SGK matrah kontrolü");
  expect(screen.queryByRole("button", { name: "Denetim İzi" })).not.toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/actions/ActionsPage.test.tsx` → FAIL.

- [x] **Step 2: `ActionsPage.tsx` yaz** (kart eylemleri + Yeni Aksiyon Task 3'te)

```tsx
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthContext";
import { PageError, PageLoading } from "../shared/PageState";
import { formatTimeAgo } from "../recruitment/format";
import { appRoutes } from "../../routes";
import { actionLevelText, actionPillClass, actionStatusText } from "./format";
import { useAuditLogs, useGlobalActions, type GlobalActionDto } from "./queries";

const TABS: [string, string][] = [["open", "Açık"], ["week", "Bu Hafta"], ["done", "Tamamlanan"]];

const routePathFor = (routeKey?: string | null): string | null =>
  appRoutes.find((route) => route.key === routeKey)?.path ?? null;

export function ActionsPage() {
  const { user } = useAuth();
  const navigate = useNavigate();
  const isMgmt = user?.role === "hr-admin" || user?.role === "manager";
  const isAdmin = user?.role === "hr-admin";
  const [tab, setTab] = useState("open");
  const [filters, setFilters] = useState({ priority: "", source: "", owner: "" });
  const [createOpen, setCreateOpen] = useState(false);

  const actionsQ = useGlobalActions(filters);
  const optionsQ = useGlobalActions({ priority: "", source: "", owner: "" });
  const auditQ = useAuditLogs(isMgmt && tab === "audit");

  if (actionsQ.isPending) return <PageLoading />;
  if (actionsQ.isError) return <PageError error={actionsQ.error} />;

  const actions = actionsQ.data;
  const allActions = optionsQ.data ?? actions;
  const kpis = {
    today: actions.filter((item) => item.due === "Bugün").length,
    overdue: actions.filter((item) => item.due === "Gecikti").length,
    high: actions.filter((item) => item.priority === "high" && item.status !== "done").length,
    done: actions.filter((item) => item.status === "done").length,
  };
  const sources = [...new Set(allActions.map((item) => item.source ?? ""))].filter(Boolean);
  const owners = [...new Set(allActions.map((item) => item.owner ?? ""))].filter(Boolean);
  const visible = tab === "audit" ? [] : actions.filter((item) => item.status === tab);

  const setFilter = (key: "priority" | "source" | "owner") =>
    (e: React.ChangeEvent<HTMLSelectElement>) =>
      setFilters((f) => ({ ...f, [key]: e.target.value }));

  const renderCard = (action: GlobalActionDto) => {
    const sourcePath = routePathFor(action.sourceRoute);
    return (
      <article key={action.id} className={`global-action-card ${action.priority}`}>
        <div className="global-action-top">
          <span className={`status-pill ${actionPillClass(action.priority)}`}>{actionLevelText(action.priority)}</span>
          <span>{action.due}</span>
        </div>
        <h4>{action.title}</h4>
        <p>{action.action}</p>
        <div className="global-action-meta">
          <span><i aria-hidden="true" className="fa-solid fa-layer-group" /> {action.source}</span>
          <span><i aria-hidden="true" className="fa-solid fa-user" /> {action.owner}</span>
        </div>
        <div className="global-action-footer">
          <span className="status-pill info">{actionStatusText(action.status)}</span>
          <div className="toolbar-actions">
            {/* Durum ilerletme + silme Task 3'te */}
            {sourcePath && (
              <button className="btn btn-secondary btn-sm" onClick={() => navigate(sourcePath)}>Kaynağa git</button>
            )}
          </div>
        </div>
      </article>
    );
  };

  return (
    <div id="actions-screen">
      <div className="page-header">
        <div>
          <h2>Global Aksiyon Merkezi</h2>
          <p>Risk, bordro, uyum ve çalışan deneyimi aksiyonlarını tek operasyon merkezinde takip edin.</p>
        </div>
        {/* Yeni Aksiyon butonu Task 3'te (yalnız hr-admin) */}
      </div>

      <div className="actions-kpi-grid">
        <div className="stat-box"><span className="sb-label">Bugün</span><strong className="sb-val">{kpis.today}</strong><small>Kapanması beklenen</small></div>
        <div className="stat-box"><span className="sb-label">Geciken</span><strong className="sb-val text-red">{kpis.overdue}</strong><small>Riskli aksiyon</small></div>
        <div className="stat-box"><span className="sb-label">Yüksek Öncelik</span><strong className="sb-val text-orange">{kpis.high}</strong><small>Aktif takip</small></div>
        <div className="stat-box"><span className="sb-label">Tamamlanan</span><strong className="sb-val">{kpis.done}</strong><small>Bu hafta</small></div>
      </div>

      <section className="card actions-filter-bar">
        <label className="sr-only" htmlFor="actions-priority-filter">Öncelik filtresi</label>
        <select id="actions-priority-filter" className="small-select" value={filters.priority} onChange={setFilter("priority")}>
          <option value="">Öncelik: Tümü</option>
          <option value="high">Yüksek</option>
          <option value="medium">Orta</option>
          <option value="low">Düşük</option>
        </select>
        <label className="sr-only" htmlFor="actions-source-filter">Kaynak filtresi</label>
        <select id="actions-source-filter" className="small-select" value={filters.source} onChange={setFilter("source")}>
          <option value="">Kaynak: Tümü</option>
          {sources.map((source) => <option key={source} value={source}>{source}</option>)}
        </select>
        <label className="sr-only" htmlFor="actions-owner-filter">Sahip filtresi</label>
        <select id="actions-owner-filter" className="small-select" value={filters.owner} onChange={setFilter("owner")}>
          <option value="">Sahip: Tümü</option>
          {owners.map((owner) => <option key={owner} value={owner}>{owner}</option>)}
        </select>
      </section>

      <div className="actions-tabs">
        {TABS.map(([key, label]) => (
          <button key={key} className={`action-tab ${tab === key ? "active" : ""}`} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}
        {isMgmt && (
          <button className={`action-tab ${tab === "audit" ? "active" : ""}`} onClick={() => setTab("audit")}>
            Denetim İzi
          </button>
        )}
      </div>

      {tab !== "audit" && (
        <section className="actions-tab-content active">
          <div className="global-actions-grid">
            {visible.map(renderCard)}
            {visible.length === 0 && <div className="empty-lane">Bu filtrede aksiyon yok.</div>}
          </div>
        </section>
      )}

      {tab === "audit" && (
        <section className="actions-tab-content active">
          <section className="card">
            {auditQ.isPending && <PageLoading />}
            {auditQ.isError && <PageError error={auditQ.error} />}
            {auditQ.data && (
              <div className="audit-timeline">
                {auditQ.data.map((log) => (
                  <div key={log.id} className="audit-item">
                    <div className="audit-dot" />
                    <div className="audit-body">
                      <div className="audit-head">
                        <strong>{log.action}</strong>
                        <span>{formatTimeAgo(log.createdAtUtc)}</span>
                      </div>
                      <p>{log.detail}</p>
                      <small>{log.actor} · {log.module}</small>
                    </div>
                  </div>
                ))}
                {auditQ.data.length === 0 && <p className="pending-desc">Henüz denetim kaydı yok.</p>}
              </div>
            )}
          </section>
        </section>
      )}

      {/* Yeni Aksiyon modalı Task 3'te */}
      {createOpen && null}
    </div>
  );
}
```

- [x] **Step 3: `routes.tsx` `pageFor`'a ekle**

```tsx
// import bloğuna:
import { ActionsPage } from "./features/actions/ActionsPage";
// pageFor içine:
  actions: ActionsPage,
  "action-center": ActionsPage,
```

(Eski `routes.js`te `/risk/action-center` da `ActionsCenter()` render eder — birebir.)

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/actions/ frontend/src/routes.tsx
git commit -m "feat(frontend): aksiyon merkezi kabuğu — KPI, filtreler, sekmeler, denetim izi, rota"
```

---

### Task 3: Aksiyon mutasyonları — Yeni Aksiyon, durum ilerletme, silme

**Files:**
- Create: `frontend/src/features/actions/ActionModal.tsx`
- Modify: `frontend/src/features/actions/ActionsPage.tsx`
- Test: `frontend/src/features/actions/ActionModal.test.tsx` (+ `ActionsPage.test.tsx`e 2 test)

**Interfaces:**
- Consumes: `useCreateGlobalAction`, `useSetActionStatus`, `useDeleteGlobalAction`, `nextActionStatus`, `nextActionStatusLabel`, `useToast`.
- Produces: `ActionModal({ onClose }: { onClose: () => void })`.

Davranış (bilinçli farklar; eskide mutasyon yoktu): kartta durum ilerletme butonu (hr-admin+manager; `open→week→done`, `done`'da gizli), silme ikonu (yalnız hr-admin), header'da "Yeni Aksiyon" (yalnız hr-admin). 409 (geri dönüş) → toast error.

- [x] **Step 1: Başarısız testleri yaz** — `ActionModal.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { ActionModal } from "./ActionModal";

beforeEach(() =>
  stubApi({
    "/api/actions": { id: 7, title: "Yeni görev", source: "Bordro", owner: "İK", due: "Bugün", priority: "high", status: "open", action: "Denetle." },
  }),
);
afterEach(() => vi.unstubAllGlobals());

test("form doldurulup kaydedilince POST actions tam gövdeyle gider", async () => {
  renderPage(
    <ToastProvider>
      <ActionModal onClose={() => {}} />
    </ToastProvider>,
  );
  await userEvent.type(screen.getByLabelText("Başlık"), "Yeni görev");
  await userEvent.type(screen.getByLabelText("Kaynak"), "Bordro");
  await userEvent.type(screen.getByLabelText("Sahip"), "İK");
  await userEvent.type(screen.getByLabelText("Vade etiketi"), "Bugün");
  await userEvent.selectOptions(screen.getByLabelText("Öncelik"), "high");
  await userEvent.type(screen.getByLabelText("Önerilen aksiyon"), "Denetle.");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/actions" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      title: "Yeni görev", source: "Bordro", owner: "İK", due: "Bugün",
      priority: "high", recommendedAction: "Denetle.",
    });
  });
});

test("başlık boşsa form hatası gösterilir, istek atılmaz", async () => {
  renderPage(
    <ToastProvider>
      <ActionModal onClose={() => {}} />
    </ToastProvider>,
  );
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Başlık, kaynak ve sahip zorunludur.");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});
```

`ActionsPage.test.tsx`e eklenecek testler (`beforeEach` stub'ına `"/api/actions/1/status": { ...actions[0], status: "week" }` ve `"/api/actions/1": {}` eklenir):

```tsx
test("durum ilerletme PATCH status atar", async () => {
  renderShell();
  await screen.findByText("SGK matrah kontrolü");
  await userEvent.click(screen.getByRole("button", { name: "Bu haftaya al" }));
  await waitFor(() => {
    const patched = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/actions/1/status" && i?.method === "PATCH",
    );
    expect(patched).toBeTruthy();
    expect(JSON.parse(String(patched![1]?.body))).toMatchObject({ status: "week" });
  });
});

test("employee kartlarda ilerletme/silme görmez", async () => {
  setRole("employee");
  renderShell();
  await screen.findByText("SGK matrah kontrolü");
  expect(screen.queryByRole("button", { name: "Bu haftaya al" })).not.toBeInTheDocument();
  expect(screen.queryByLabelText(/aksiyonunu sil/)).not.toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/actions/` → FAIL.

- [x] **Step 2: `ActionModal.tsx` yaz**

```tsx
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useCreateGlobalAction } from "./queries";

export function ActionModal({ onClose }: { onClose: () => void }) {
  const { showToast } = useToast();
  const createAction = useCreateGlobalAction();
  const [form, setForm] = useState({
    title: "", source: "", owner: "", sourceRoute: "", due: "Bugün", priority: "medium", recommendedAction: "",
  });
  const [error, setError] = useState<string | null>(null);

  const set = (key: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
      setForm((f) => ({ ...f, [key]: e.target.value }));

  const submit = async () => {
    setError(null);
    if (!form.title.trim() || !form.source.trim() || !form.owner.trim()) {
      setError("Başlık, kaynak ve sahip zorunludur.");
      return;
    }
    try {
      await createAction.mutateAsync({
        title: form.title.trim(),
        source: form.source.trim(),
        owner: form.owner.trim(),
        sourceRoute: form.sourceRoute.trim() || null,
        due: form.due.trim() || null,
        priority: form.priority,
        recommendedAction: form.recommendedAction.trim() || null,
      });
      showToast("Aksiyon oluşturuldu.", "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Aksiyon oluşturulamadı.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>Yeni Aksiyon</h3>
            <p>Operasyon merkezine manuel takip kaydı ekleyin.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Aksiyon penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="act-title">Başlık</label>
              <input id="act-title" className="input-control" value={form.title} onChange={set("title")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-source">Kaynak</label>
              <input id="act-source" className="input-control" value={form.source} onChange={set("source")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-owner">Sahip</label>
              <input id="act-owner" className="input-control" value={form.owner} onChange={set("owner")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-due">Vade etiketi</label>
              <input id="act-due" className="input-control" value={form.due} onChange={set("due")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-priority">Öncelik</label>
              <select id="act-priority" className="input-control" value={form.priority} onChange={set("priority")}>
                <option value="high">Yüksek</option>
                <option value="medium">Orta</option>
                <option value="low">Düşük</option>
              </select>
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-route">Kaynak rota (ops.)</label>
              <input id="act-route" className="input-control" placeholder="payroll, compliance-risk…" value={form.sourceRoute} onChange={set("sourceRoute")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="act-recommended">Önerilen aksiyon</label>
              <textarea id="act-recommended" className="input-control" rows={2} value={form.recommendedAction} onChange={set("recommendedAction")} />
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={createAction.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> Kaydet
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [x] **Step 3: `ActionsPage.tsx`'e mutasyon UI'larını ekle**

İçe aktarımlar + hook'lar:

```tsx
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { ActionModal } from "./ActionModal";
import { nextActionStatus, nextActionStatusLabel } from "./format";           // format import satırına
import { useDeleteGlobalAction, useSetActionStatus } from "./queries";        // queries import satırına
// component başına:
const { showToast } = useToast();
const setStatus = useSetActionStatus();
const deleteAction = useDeleteGlobalAction();

const advance = async (action: GlobalActionDto) => {
  const next = nextActionStatus(action.status);
  if (!next) return;
  try {
    await setStatus.mutateAsync({ id: action.id!, status: next });
    showToast(`"${action.title}" durumu güncellendi.`, "success");
  } catch (e) {
    showToast(e instanceof ApiError ? e.message : "Durum güncellenemedi.", "error");
  }
};

const remove = async (action: GlobalActionDto) => {
  try {
    await deleteAction.mutateAsync(action.id!);
    showToast(`"${action.title}" silindi.`, "info");
  } catch (e) {
    showToast(e instanceof ApiError ? e.message : "Aksiyon silinemedi.", "error");
  }
};
```

`renderCard` footer'ındaki `toolbar-actions` içeriği (yorum satırı yerine):

```tsx
<div className="toolbar-actions">
  {isMgmt && nextActionStatusLabel(action.status) && (
    <button className="btn btn-primary btn-sm" onClick={() => advance(action)}>
      {nextActionStatusLabel(action.status)}
    </button>
  )}
  {sourcePath && (
    <button className="btn btn-secondary btn-sm" onClick={() => navigate(sourcePath)}>Kaynağa git</button>
  )}
  {isAdmin && (
    <button
      className="btn-icon-sm"
      title="Sil"
      aria-label={`${action.title} aksiyonunu sil`}
      onClick={() => remove(action)}
    >
      <i aria-hidden="true" className="fa-solid fa-trash" />
    </button>
  )}
</div>
```

Header'a (page-header yorumu yerine):

```tsx
{isAdmin && (
  <button className="btn btn-primary" onClick={() => setCreateOpen(true)}>
    <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Aksiyon
  </button>
)}
```

Sayfa sonundaki placeholder:

```tsx
{createOpen && <ActionModal onClose={() => setCreateOpen(false)} />}
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/actions/
git commit -m "feat(frontend): aksiyon mutasyonları — oluşturma, ileri yönlü durum geçişi, silme"
```

---

### Task 4: GlobalSearch gerçek API sonuçları

**Files:**
- Modify: `frontend/src/layout/GlobalSearch.tsx`
- Test: `frontend/src/layout/GlobalSearch.test.tsx` (yeni)

**Interfaces:**
- Consumes: `apiFetch`, `useQuery`, mevcut sayfa-sonucu mantığı.
- Produces: mevcut `GlobalSearch()` genişler — 300ms debounce ile `GET /search?q=`; API sonuçları sayfa sonuçlarının altına eklenir. `SearchResultDto.routeKey` → `appRoutes` path'i; tip ikonları: `personnel→fa-user`, `action→fa-list-check`, `candidate→fa-briefcase`, diğer→`fa-compass`.

Davranış: sayfa sonuçları anlık (mevcut), API sonuçları debounce'lu eklenir; `hint` alanı gösterilir. API hatası sessizce yutulur (arama düşerse sayfa sonuçları çalışmaya devam eder).

- [x] **Step 1: Başarısız testleri yaz** — `GlobalSearch.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { SESSION_KEY } from "../../api/session";
import { GlobalSearch } from "./GlobalSearch";

const results = [
  { type: "personnel", label: "Ahmet Yılmaz", hint: "Senior Developer · Yazılım", routeKey: "personnel", entityId: 3 },
  { type: "action", label: "SGK matrah kontrolü", hint: "Bordro aksiyonu", routeKey: "actions", entityId: 1 },
];

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role: "hr-admin", roleLabel: "X", initials: "XX", employeeId: 5 },
  }));
  stubApi({ "/api/search": results });
});
afterEach(() => vi.unstubAllGlobals());

test("API sonuçları debounce sonrası listelenir", async () => {
  renderPage(
    <AuthProvider>
      <GlobalSearch />
    </AuthProvider>,
  );
  await userEvent.type(screen.getByLabelText("Personel, aksiyon veya sayfa ara"), "ahmet");
  expect(await screen.findByText("Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("Senior Developer · Yazılım")).toBeInTheDocument();
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u).startsWith("/api/search?q=ahmet"));
    expect(hit).toBe(true);
  });
});

test("iki karakterden kısa sorguda API çağrısı yapılmaz", async () => {
  renderPage(
    <AuthProvider>
      <GlobalSearch />
    </AuthProvider>,
  );
  await userEvent.type(screen.getByLabelText("Personel, aksiyon veya sayfa ara"), "a");
  await new Promise((resolve) => setTimeout(resolve, 400));
  expect(vi.mocked(fetch).mock.calls.some(([u]) => String(u).includes("/api/search"))).toBe(false);
});
```

Run: `npm test -- --run src/layout/GlobalSearch.test.tsx` → FAIL.

- [x] **Step 2: `GlobalSearch.tsx`'i genişlet**

Mevcut dosyada şu değişiklikler yapılır (tam parçalar):

```tsx
// import bloğuna eklenir:
import { useQuery } from "@tanstack/react-query";
import { apiFetch } from "../api/client";
import type { components } from "../api/schema";

type SearchResultDto = components["schemas"]["SearchResultDto"];

const TYPE_ICONS: Record<string, string> = {
  personnel: "fa-user", action: "fa-list-check", candidate: "fa-briefcase",
};
```

Component içine (mevcut `items` hesabından önce):

```tsx
const [debouncedQuery, setDebouncedQuery] = useState("");
useEffect(() => {
  const timer = setTimeout(() => setDebouncedQuery(query.trim()), 300);
  return () => clearTimeout(timer);
}, [query]);

const apiResults = useQuery({
  queryKey: ["search", debouncedQuery],
  queryFn: () => apiFetch<SearchResultDto[]>(`/search?q=${encodeURIComponent(debouncedQuery)}`),
  enabled: debouncedQuery.length >= 2,
  retry: false,
});
```

`items` hesabı; sayfa sonuçlarının ardından API sonuçları eklenir (mevcut `items` tanımının yerine):

```tsx
const pageItems: Item[] = !query.trim() || query.trim().length < 2
  ? []
  : appRoutes
      .filter((r) => r.navKey === r.key && r.roles.includes((user?.role ?? "employee") as Role))
      .filter((r) => r.title.toLocaleLowerCase("tr-TR").includes(query.trim().toLocaleLowerCase("tr-TR")))
      .map((r) => ({ label: r.title, hint: "Sayfaya git", icon: navIcons[r.key] || "fa-compass", path: r.path }));

const apiItems: Item[] = (apiResults.data ?? []).map((result) => ({
  label: result.label ?? "",
  hint: result.hint ?? "",
  icon: TYPE_ICONS[result.type ?? ""] ?? "fa-compass",
  path: appRoutes.find((r) => r.key === result.routeKey)?.path ?? "/overview",
}));

const items: Item[] = [...pageItems, ...apiItems];
```

Sonuç butonu `key`'i çakışmasın diye `key={`${item.path}-${item.label}`}` yapılır.

- [x] **Step 3: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS (AppShell testleri de etkilenmemeli).

- [x] **Step 4: Commit**

```bash
git add frontend/src/layout/
git commit -m "feat(frontend): global arama — birleşik /search sonuçları (personel/aksiyon/aday)"
```

---

### Task 5: SettingsPage — şirket profili, bildirimler, güvenlik, abonelik

**Files:**
- Create: `frontend/src/features/settings/queries.ts`
- Create: `frontend/src/features/settings/SettingsPage.tsx`
- Modify: `frontend/src/routes.tsx` (`pageFor`'a `settings`)
- Test: `frontend/src/features/settings/SettingsPage.test.tsx`

**Interfaces:**
- Produces (queries): `SettingsDto`, `useSettings()`, `useUpdateCompany()`, `useUpdateNotifications()`, `useUpdateSecurity()`, `useUploadLogo()` (FormData), `useChangePassword()` (`POST /auth/change-password`).
- Produces: `SettingsPage()` — `settings-layout` + 4 `set-section` (state: `section: "general" | "notif" | "security" | "billing"`).

Davranış: eski `Settings()` DOM'u. Header "Değişiklikleri Kaydet" şirket formunu `PUT company` ile kaydeder. Bildirim toggle'ları değişimde anında `PUT notifications` (bilinçli fark: eskide sahte kaydetme). Güvenlik: şifre değişikliği gerçek `POST /auth/change-password` (yanlış mevcut şifre → ApiError mesajı), 2FA toggle `PUT security`. Abonelik salt-okur (`subscription`). Logo yükleme gerçek `POST company/logo` (FormData); mevcut logo `logoPath` doluysa `GET /api/settings/company/logo` `<img>` ile gösterilir.

- [x] **Step 1: Başarısız testleri yaz** — `SettingsPage.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { SettingsPage } from "./SettingsPage";

const settings = {
  company: { name: "HR Master Teknoloji A.Ş.", website: "www.hrmaster.com", systemEmail: "info@hrmaster.com", phone: "+90 212 555 00 00", headquartersAddress: "Maslak", logoPath: null },
  notifications: { newPersonnelEmail: true, leaveRequestEmail: true, weeklyReportEmail: false },
  security: { twoFactorSmsEnabled: false },
  subscription: { plan: "pro", planName: "HR Master Kurumsal", billingCycle: "Yıllık", price: 12000, renewalDate: "2026-10-12", paymentMethodMasked: "•••• •••• •••• 4582" },
};

beforeEach(() =>
  stubApi({
    "/api/settings": settings,
    "/api/settings/company": settings.company,
    "/api/settings/notifications": { ...settings.notifications, weeklyReportEmail: true },
    "/api/auth/change-password": {},
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <ToastProvider>
      <SettingsPage />
    </ToastProvider>,
  );

test("şirket formu backend verisiyle dolar ve kaydet PUT atar", async () => {
  renderShell();
  const name = await screen.findByLabelText("Şirket Adı");
  expect(name).toHaveValue("HR Master Teknoloji A.Ş.");
  await userEvent.clear(name);
  await userEvent.type(name, "HR Master A.Ş.");
  await userEvent.click(screen.getByRole("button", { name: /Değişiklikleri Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/settings/company" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    expect(JSON.parse(String(put![1]?.body))).toMatchObject({ name: "HR Master A.Ş.", website: "www.hrmaster.com" });
  });
});

test("bildirim toggle değişimi anında PUT notifications atar", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Bildirimler/ }));
  await userEvent.click(screen.getByLabelText("Haftalık Rapor"));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/settings/notifications" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    expect(JSON.parse(String(put![1]?.body))).toMatchObject({ weeklyReportEmail: true, newPersonnelEmail: true });
  });
});

test("şifre değişikliği change-password ucuna gider; eşleşmeyen tekrar hata verir", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Güvenlik & Yetki/ }));
  await userEvent.type(screen.getByLabelText("Mevcut Şifre"), "demo123");
  await userEvent.type(screen.getByLabelText("Yeni Şifre"), "yeni12345");
  await userEvent.type(screen.getByLabelText("Yeni Şifre (Tekrar)"), "farkli");
  await userEvent.click(screen.getByRole("button", { name: "Şifreyi Güncelle" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Şifreler eşleşmiyor, kontrol edin.");
  await userEvent.clear(screen.getByLabelText("Yeni Şifre (Tekrar)"));
  await userEvent.type(screen.getByLabelText("Yeni Şifre (Tekrar)"), "yeni12345");
  await userEvent.click(screen.getByRole("button", { name: "Şifreyi Güncelle" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/auth/change-password" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({ currentPassword: "demo123", newPassword: "yeni12345" });
  });
});

test("abonelik bölümü salt-okur plan bilgisi gösterir", async () => {
  renderShell();
  await screen.findByLabelText("Şirket Adı");
  await userEvent.click(screen.getByRole("button", { name: /Abonelik & Fatura/ }));
  expect(screen.getByText("HR Master Kurumsal")).toBeInTheDocument();
  expect(screen.getByText("•••• •••• •••• 4582")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/settings/SettingsPage.test.tsx` → FAIL.

- [x] **Step 2: `queries.ts` yaz**

```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type SettingsDto = components["schemas"]["SettingsDto"];
export type CompanyProfileDto = components["schemas"]["CompanyProfileDto"];
export type NotificationSettingsDto = components["schemas"]["NotificationSettingsDto"];
export type UpdateCompanyProfileCommand = components["schemas"]["UpdateCompanyProfileCommand"];

export const useSettings = () =>
  useQuery({
    queryKey: ["settings"],
    queryFn: () => apiFetch<SettingsDto>("/settings"),
  });

export const useUpdateCompany = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: UpdateCompanyProfileCommand) =>
      apiFetch<CompanyProfileDto>("/settings/company", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["settings"] }),
  });
};

export const useUpdateNotifications = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: NotificationSettingsDto) =>
      apiFetch<NotificationSettingsDto>("/settings/notifications", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["settings"] }),
  });
};

export const useUpdateSecurity = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: { twoFactorSmsEnabled: boolean }) =>
      apiFetch<{ twoFactorSmsEnabled: boolean }>("/settings/security", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["settings"] }),
  });
};

export const useUploadLogo = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (file: File) => {
      const data = new FormData();
      data.append("file", file);
      return apiFetch<{ logoPath: string }>("/settings/company/logo", { method: "POST", body: data });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["settings"] }),
  });
};

export const useChangePassword = () =>
  useMutation({
    mutationFn: (command: { currentPassword: string; newPassword: string }) =>
      apiFetch<void>("/auth/change-password", {
        method: "POST",
        body: JSON.stringify(command),
      }),
  });
```

- [x] **Step 3: `SettingsPage.tsx` yaz** (eski `Settings()` DOM paritesi; toggle'lar `<label className="switch">` yapısıyla, erişilebilirlik için checkbox'lara `aria-label`)

```tsx
import { useEffect, useRef, useState } from "react";
import { ApiError, apiDownload } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import {
  useChangePassword, useSettings, useUpdateCompany, useUpdateNotifications,
  useUpdateSecurity, useUploadLogo, type NotificationSettingsDto,
} from "./queries";

const SECTIONS: [string, string, string][] = [
  ["general", "fa-building", "Şirket Bilgileri"],
  ["notif", "fa-bell", "Bildirimler"],
  ["security", "fa-shield-halved", "Güvenlik & Yetki"],
  ["billing", "fa-credit-card", "Abonelik & Fatura"],
];

const NOTIF_ROWS: [keyof NotificationSettingsDto & string, string, string][] = [
  ["newPersonnelEmail", "Yeni Personel Kaydı", "Sisteme yeni biri eklendiğinde yöneticilere bildir."],
  ["leaveRequestEmail", "İzin Talepleri", "Personel izin talebi oluşturduğunda anında e-posta gönder."],
  ["weeklyReportEmail", "Haftalık Rapor", "Her pazartesi sabahı özet operasyon raporu gönder."],
];

export function SettingsPage() {
  const { showToast } = useToast();
  const settingsQ = useSettings();
  const updateCompany = useUpdateCompany();
  const updateNotifications = useUpdateNotifications();
  const updateSecurity = useUpdateSecurity();
  const uploadLogo = useUploadLogo();
  const changePassword = useChangePassword();
  const logoInputRef = useRef<HTMLInputElement>(null);

  const [section, setSection] = useState("general");
  const [company, setCompany] = useState<Record<string, string> | null>(null);
  const [passwords, setPasswords] = useState({ current: "", next: "", repeat: "" });
  const [passwordError, setPasswordError] = useState<string | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | null>(null);

  useEffect(() => {
    if (company === null && settingsQ.data?.company) {
      const profile = settingsQ.data.company;
      setCompany({
        name: profile.name ?? "", website: profile.website ?? "",
        systemEmail: profile.systemEmail ?? "", phone: profile.phone ?? "",
        headquartersAddress: profile.headquartersAddress ?? "",
      });
    }
  }, [company, settingsQ.data]);

  // <img src> Bearer gönderemez; korumalı logo apiDownload ile blob URL'e çevrilir.
  const logoPath = settingsQ.data?.company?.logoPath ?? null;
  useEffect(() => {
    let objectUrl: string | null = null;
    if (logoPath) {
      apiDownload("/settings/company/logo")
        .then(({ blob }) => {
          objectUrl = URL.createObjectURL(blob);
          setLogoUrl(objectUrl);
        })
        .catch(() => setLogoUrl(null));
    } else {
      setLogoUrl(null);
    }
    return () => {
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [logoPath]);

  if (settingsQ.isPending || company === null) return <PageLoading />;
  if (settingsQ.isError) return <PageError error={settingsQ.error} />;

  const data = settingsQ.data;
  const notifications = data.notifications ?? {};
  const subscription = data.subscription ?? {};

  const setCompanyField = (key: string) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      setCompany((f) => ({ ...f!, [key]: e.target.value }));

  const saveCompany = async () => {
    try {
      await updateCompany.mutateAsync({
        name: company.name.trim(), website: company.website.trim() || null,
        systemEmail: company.systemEmail.trim() || null, phone: company.phone.trim() || null,
        headquartersAddress: company.headquartersAddress.trim() || null,
      });
      showToast("Ayarlar başarıyla kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Ayarlar kaydedilemedi.", "error");
    }
  };

  const toggleNotification = async (key: keyof NotificationSettingsDto & string) => {
    try {
      await updateNotifications.mutateAsync({
        newPersonnelEmail: notifications.newPersonnelEmail ?? false,
        leaveRequestEmail: notifications.leaveRequestEmail ?? false,
        weeklyReportEmail: notifications.weeklyReportEmail ?? false,
        [key]: !notifications[key],
      });
      showToast("Bildirim tercihi güncellendi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Tercih kaydedilemedi.", "error");
    }
  };

  const toggleTwoFactor = async () => {
    try {
      await updateSecurity.mutateAsync({ twoFactorSmsEnabled: !(data.security?.twoFactorSmsEnabled ?? false) });
      showToast("Güvenlik ayarı güncellendi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Ayar kaydedilemedi.", "error");
    }
  };

  const submitPassword = async () => {
    setPasswordError(null);
    if (!passwords.next || !passwords.repeat) {
      setPasswordError("Yeni şifre alanlarını doldurun.");
      return;
    }
    if (passwords.next !== passwords.repeat) {
      setPasswordError("Şifreler eşleşmiyor, kontrol edin.");
      return;
    }
    try {
      await changePassword.mutateAsync({ currentPassword: passwords.current, newPassword: passwords.next });
      setPasswords({ current: "", next: "", repeat: "" });
      showToast("Şifreniz güncellendi.", "success");
    } catch (e) {
      setPasswordError(e instanceof ApiError ? e.message : "Şifre güncellenemedi.");
    }
  };

  const onLogoPick = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      await uploadLogo.mutateAsync(file);
      showToast("Logo yüklendi.", "success");
    } catch (err) {
      showToast(err instanceof ApiError ? err.message : "Logo yüklenemedi.", "error");
    } finally {
      e.target.value = "";
    }
  };

  return (
    <div id="settings-screen">
      <div className="page-header">
        <div>
          <h2>Sistem Ayarları</h2>
          <p>Şirket profili, bildirimler, güvenlik ve abonelik tercihlerini yönetin.</p>
        </div>
        <button className="btn btn-primary" onClick={saveCompany} disabled={updateCompany.isPending}>
          <i aria-hidden="true" className="fa-solid fa-save" /> Değişiklikleri Kaydet
        </button>
      </div>

      <div className="settings-layout">
        <aside className="settings-sidebar">
          <nav className="set-nav">
            {SECTIONS.map(([key, icon, label]) => (
              <button key={key} className={`set-link ${section === key ? "active" : ""}`} onClick={() => setSection(key)}>
                <i aria-hidden="true" className={`fa-solid ${icon}`} /> {label}
              </button>
            ))}
          </nav>
        </aside>

        <main className="settings-content">
          {section === "general" && (
            <div id="set-general" className="set-section active">
              <div className="set-card">
                <div className="card-head">
                  <h3>Marka & Görünüm</h3>
                  <p>Sistemde görünecek şirket adı ve marka varlıkları.</p>
                </div>
                <div className="form-row">
                  <div className="logo-upload">
                    <div className="current-logo">
                      {logoUrl
                        ? <img src={logoUrl} alt="Şirket logosu" style={{ maxWidth: "100%", maxHeight: "100%" }} />
                        : <i aria-hidden="true" className="fa-solid fa-building" />}
                    </div>
                    <div>
                      <input ref={logoInputRef} type="file" accept="image/png,image/jpeg" className="sr-only" aria-label="Logo dosyası" onChange={onLogoPick} />
                      <button className="btn-outline" onClick={() => logoInputRef.current?.click()} disabled={uploadLogo.isPending}>Logo Yükle</button>
                      <small>PNG, JPG, maksimum 2 MB</small>
                    </div>
                  </div>
                </div>
                <div className="form-grid-2">
                  <div className="input-group">
                    <label htmlFor="set-company-name">Şirket Adı</label>
                    <input id="set-company-name" type="text" className="input-control" value={company.name} onChange={setCompanyField("name")} />
                  </div>
                  <div className="input-group">
                    <label htmlFor="set-company-web">Web Sitesi</label>
                    <input id="set-company-web" type="text" className="input-control" value={company.website} onChange={setCompanyField("website")} />
                  </div>
                </div>
              </div>

              <div className="set-card mt-4">
                <div className="card-head"><h3>İletişim Bilgileri</h3></div>
                <div className="form-grid-2">
                  <div className="input-group">
                    <label htmlFor="set-company-email">E-Posta (Sistem)</label>
                    <input id="set-company-email" type="email" className="input-control" value={company.systemEmail} onChange={setCompanyField("systemEmail")} />
                  </div>
                  <div className="input-group">
                    <label htmlFor="set-company-phone">Telefon</label>
                    <input id="set-company-phone" type="tel" className="input-control" value={company.phone} onChange={setCompanyField("phone")} />
                  </div>
                  <div className="input-group col-span-2">
                    <label htmlFor="set-company-address">Merkez Adres</label>
                    <textarea id="set-company-address" rows={2} className="input-control" value={company.headquartersAddress} onChange={setCompanyField("headquartersAddress")} />
                  </div>
                </div>
              </div>
            </div>
          )}

          {section === "notif" && (
            <div id="set-notif" className="set-section active">
              <div className="set-card">
                <div className="card-head"><h3>E-Posta Bildirimleri</h3></div>
                {NOTIF_ROWS.map(([key, title, description], index) => (
                  <div key={key}>
                    {index > 0 && <div className="divider" />}
                    <div className="toggle-row">
                      <div><strong>{title}</strong><p>{description}</p></div>
                      <label className="switch">
                        <input
                          type="checkbox"
                          aria-label={title}
                          checked={notifications[key] ?? false}
                          onChange={() => toggleNotification(key)}
                        />
                        <span className="slider round" />
                      </label>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {section === "security" && (
            <div id="set-security" className="set-section active">
              <div className="set-card">
                <div className="card-head"><h3>Giriş Güvenliği</h3></div>
                {passwordError && <p className="form-error" role="alert">{passwordError}</p>}
                <div className="form-grid-2">
                  <div className="input-group">
                    <label htmlFor="current-password">Mevcut Şifre</label>
                    <input id="current-password" type="password" placeholder="••••••••" className="input-control" value={passwords.current} onChange={(e) => setPasswords((p) => ({ ...p, current: e.target.value }))} />
                  </div>
                </div>
                <div className="form-grid-2 mt-3">
                  <div className="input-group">
                    <label htmlFor="new-password">Yeni Şifre</label>
                    <input id="new-password" type="password" className="input-control" value={passwords.next} onChange={(e) => setPasswords((p) => ({ ...p, next: e.target.value }))} />
                  </div>
                  <div className="input-group">
                    <label htmlFor="new-password-repeat">Yeni Şifre (Tekrar)</label>
                    <input id="new-password-repeat" type="password" className="input-control" value={passwords.repeat} onChange={(e) => setPasswords((p) => ({ ...p, repeat: e.target.value }))} />
                  </div>
                </div>
                <button className="btn btn-secondary mt-4" onClick={submitPassword} disabled={changePassword.isPending}>Şifreyi Güncelle</button>
              </div>

              <div className="set-card mt-4">
                <div className="card-head"><h3>İki Aşamalı Doğrulama</h3></div>
                <div className="toggle-row">
                  <div><strong>SMS ile doğrulama</strong><p>Giriş yaparken telefonunuza tek kullanımlık kod gönderilir.</p></div>
                  <label className="switch">
                    <input
                      type="checkbox"
                      aria-label="SMS ile doğrulama"
                      checked={data.security?.twoFactorSmsEnabled ?? false}
                      onChange={toggleTwoFactor}
                    />
                    <span className="slider round" />
                  </label>
                </div>
              </div>
            </div>
          )}

          {section === "billing" && (
            <div id="set-billing" className="set-section active">
              <div className="plan-banner">
                <div className="pb-info">
                  <span className="badge-pro">{(subscription.plan ?? "PRO").toLocaleUpperCase("tr-TR")} PLAN</span>
                  <h3>{subscription.planName}</h3>
                  <p>{subscription.billingCycle} ödeme planı aktif. Bir sonraki yenileme: <strong>{subscription.renewalDate}</strong></p>
                </div>
                <div className="pb-price">₺{(subscription.price ?? 0).toLocaleString("tr-TR")}<small>/yıl</small></div>
              </div>

              <div className="set-card mt-4">
                <div className="card-head"><h3>Ödeme Yöntemi</h3></div>
                <div className="cc-preview">
                  <div className="cc-icon"><i aria-hidden="true" className="fa-brands fa-cc-mastercard" /></div>
                  <span>{subscription.paymentMethodMasked}</span>
                  <button className="btn-text" onClick={() => showToast("Ödeme yöntemi değişikliği demo kapsamı dışındadır.", "info")}>Değiştir</button>
                </div>
              </div>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
```

- [x] **Step 4: `routes.tsx` `pageFor`'a ekle**

```tsx
import { SettingsPage } from "./features/settings/SettingsPage";
// pageFor:
  settings: SettingsPage,
```

- [x] **Step 5: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 6: Commit**

```bash
git add frontend/src/features/settings/ frontend/src/routes.tsx
git commit -m "feat(frontend): sistem ayarları — şirket profili, logo, bildirim/2FA toggle, şifre değişikliği, abonelik"
```

---

### Task 6: ManagerPage — Yönetici Konsolu

**Files:**
- Create: `frontend/src/features/manager/ManagerPage.tsx`
- Modify: `frontend/src/routes.tsx` (`pageFor`'a `manager`)
- Test: `frontend/src/features/manager/ManagerPage.test.tsx`

**Interfaces:**
- Consumes: `useOverview` (`../overview/queries`), `usePendingLeaves`/`useDecideLeave` (`../leaves/queries`), `tableToCsvLines`/`downloadCsv` (`../shared/csv`), `chartToken` (`../shared/chartSetup`), `react-chartjs-2` `Line`, `useToast`.
- Produces: `ManagerPage()`.

Davranış: eski `ManagerDashboard()` DOM'u. Gerçek veri: "Şu An İzinli" ← `overview.onLeaveToday`, "Onay Bekliyor" ← `pending.length`, Onay Bekleyenler paneli ← `usePendingLeaves(true)` + `useDecideLeave` (Onayla/Reddet). Bilinçli farklar: trend grafiği, yoğunluk haritası, departman kullanım tablosu ve "Planlanan İzin"/"Kullanım Oranı" KPI'ları **demo statik** (backend'de izin-analitik ucu yok; kartlara `Demo` pill eklenir — bordro pusulası önizleme deseni); header ay/departman seçicileri kaldırıldı (işlevsizdi), CSV butonu gerçek `mini-table` dışa aktarımı.

- [x] **Step 1: Başarısız testleri yaz** — `ManagerPage.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { ManagerPage } from "./ManagerPage";

const overview = {
  activeEmployees: 12, pendingApprovals: 2, openPositions: 1, newApplications: 3,
  inOfficeToday: 9, onLeaveToday: 3, pulseScore: 74, departmentDistribution: [], recruitmentFunnel: {},
};
const pending = [
  { id: 4, employeeId: 3, employeeName: "Ahmet Yılmaz", leaveTypeId: 1, leaveTypeName: "Yıllık İzin", startDate: "2026-08-12", endDate: "2026-08-17", days: 5, status: "pending", description: null },
];

beforeEach(() =>
  stubApi({
    "/api/dashboard/overview": overview,
    "/api/leaves/pending": pending,
    "/api/leaves/4/approve": { ...pending[0], status: "approved" },
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <ToastProvider>
      <ManagerPage />
    </ToastProvider>,
  );

test("KPI'lar gerçek veriyle dolar, onay paneli talepleri listeler", async () => {
  renderShell();
  expect(await screen.findByText("Yönetici Konsolu")).toBeInTheDocument();
  expect(screen.getByText("3 kişi")).toBeInTheDocument();
  expect(screen.getByText("1 talep")).toBeInTheDocument();
  expect(screen.getByText("Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("Yıllık İzin")).toBeInTheDocument();
});

test("Onayla butonu approve ucuna gider", async () => {
  renderShell();
  await screen.findByText("Ahmet Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: "Onayla" }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(
      ([u, i]) => String(u) === "/api/leaves/4/approve" && i?.method === "POST",
    );
    expect(hit).toBe(true);
  });
});

test("bekleyen yoksa panel boş durumu gösterir", async () => {
  stubApi({ "/api/dashboard/overview": overview, "/api/leaves/pending": [] });
  renderShell();
  expect(await screen.findByText("Bekleyen talep yok.")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/manager/ManagerPage.test.tsx` → FAIL.

- [x] **Step 2: `ManagerPage.tsx` yaz**

```tsx
import { Line } from "react-chartjs-2";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { chartToken } from "../shared/chartSetup";
import { downloadCsv, tableToCsvLines } from "../shared/csv";
import { useOverview } from "../overview/queries";
import { useDecideLeave, usePendingLeaves } from "../leaves/queries";

// Demo analitik (backend'de izin-analitik ucu yok; eski mock ile birebir).
const DEMO_TREND = [12, 15, 8, 10, 25, 45, 80, 120, 30, 15, 10, 20];
const DEMO_MONTHS = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];
const DEMO_HEATMAP: [string, string[]][] = [
  ["Yazılım", ["hm-l1", "hm-l2", "hm-l4", "hm-l3", "hm-l1"]],
  ["Satış", ["hm-l2", "hm-l1", "hm-l1", "hm-l4", "hm-l2"]],
  ["İK", ["hm-l1", "hm-l3", "hm-l2", "hm-l1", "hm-l1"]],
];
const DEMO_DEPT_USAGE: [string, string, string, string, number][] = [
  ["Yazılım Ekibi", "42 kişi", "120 gün", "340 gün", 35],
  ["Satış & Pazarlama", "18 kişi", "45 gün", "120 gün", 28],
];

const initialsOf = (name?: string | null): string =>
  (name ?? "")
    .split(" ")
    .filter(Boolean)
    .map((part) => part[0]?.toLocaleUpperCase("tr-TR"))
    .slice(0, 2)
    .join("") || "?";

const formatDateRange = (start?: string, end?: string): string => {
  const fmt = (iso?: string) =>
    iso ? new Date(iso + "T00:00:00").toLocaleDateString("tr-TR", { day: "numeric", month: "long" }) : "";
  return `${fmt(start)} - ${fmt(end)}`;
};

export function ManagerPage() {
  const { showToast } = useToast();
  const overviewQ = useOverview();
  const pendingQ = usePendingLeaves(true);
  const decide = useDecideLeave();

  if (overviewQ.isPending || pendingQ.isPending) return <PageLoading />;
  if (overviewQ.isError) return <PageError error={overviewQ.error} />;
  if (pendingQ.isError) return <PageError error={pendingQ.error} />;

  const overview = overviewQ.data;
  const pending = pendingQ.data;

  const resolve = async (id: number, name: string, approve: boolean) => {
    try {
      await decide.mutateAsync({ id, approve });
      showToast(approve ? `${name} talebi onaylandı.` : `${name} talebi reddedildi.`, approve ? "success" : "info");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "İşlem tamamlanamadı.", "error");
    }
  };

  const exportReport = () => {
    const table = document.querySelector<HTMLTableElement>(".mini-table");
    if (!table) return;
    downloadCsv(tableToCsvLines(table), "departman-kullanim-raporu.csv");
    showToast("Rapor indirildi.", "success");
  };

  return (
    <div id="manager-screen">
      <div className="manager-header page-header">
        <div>
          <h2>Yönetici Konsolu</h2>
          <p>Şirket geneli izin analitikleri, yoğunluk görünümü ve onay işlemleri.</p>
        </div>
        <div className="mh-actions header-actions">
          <button className="btn btn-secondary" onClick={exportReport}>
            <i aria-hidden="true" className="fa-solid fa-cloud-arrow-down" /> Raporu İndir
          </button>
        </div>
      </div>

      <div className="stats-grid-pro">
        <div className="kpi-pro theme-blue">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-solid fa-users-viewfinder" /></div></div>
          <span className="kpi-val">{overview.onLeaveToday ?? 0} kişi</span>
          <span className="kpi-label">Şu An İzinli</span>
        </div>
        <div className="kpi-pro theme-orange">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-regular fa-folder-open" /></div><span className="kpi-trend text-orange">Kritik</span></div>
          <span className="kpi-val">{pending.length} talep</span>
          <span className="kpi-label">Onay Bekliyor</span>
        </div>
        <div className="kpi-pro theme-purple">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-solid fa-briefcase" /></div><span className="kpi-trend">Demo</span></div>
          <span className="kpi-val">142 gün</span>
          <span className="kpi-label">Planlanan İzin</span>
        </div>
        <div className="kpi-pro theme-green">
          <div className="kpi-top"><div className="kpi-icon-box"><i aria-hidden="true" className="fa-solid fa-wallet" /></div><span className="kpi-trend">Demo</span></div>
          <span className="kpi-val">%42</span>
          <span className="kpi-label">İzin Kullanım Oranı</span>
        </div>
      </div>

      <div className="dashboard-grid">
        <div className="chart-section card">
          <div className="section-head">
            <div>
              <h3>İzin Kullanım Trendleri</h3>
              <span>Departman ve ay bazlı görünüm (demo)</span>
            </div>
            <span className="status-pill info">Demo</span>
          </div>
          <div className="chart-container manager-chart">
            <Line
              data={{
                labels: DEMO_MONTHS,
                datasets: [{
                  label: "Toplam izin günü",
                  data: DEMO_TREND,
                  borderColor: chartToken("--primary", "#0f766e"),
                  borderWidth: 2.5,
                  backgroundColor: "rgba(15, 118, 110, 0.12)",
                  fill: true,
                  tension: 0.35,
                  pointBackgroundColor: chartToken("--surface", "#ffffff"),
                  pointBorderColor: chartToken("--primary", "#0f766e"),
                  pointRadius: 4,
                }],
              }}
              options={{
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false }, tooltip: { mode: "index", intersect: false } },
                scales: {
                  y: { grid: { color: chartToken("--line-soft", "#e9efef") }, beginAtZero: true },
                  x: { grid: { display: false } },
                },
              }}
            />
          </div>

          <div className="section-head heat-title">
            <h3>Departman Yoğunluk Haritası</h3>
          </div>
          <div className="heatmap-container">
            {DEMO_HEATMAP.map(([label, cells]) => (
              <div key={label} className="heatmap-row">
                <span className="hm-label">{label}</span>
                <div className="hm-bars">
                  {cells.map((cell, index) => <div key={index} className={`hm-cell ${cell}`} />)}
                </div>
              </div>
            ))}
          </div>
        </div>

        <div className="approval-panel card">
          <div className="panel-head">
            <h3>Onay Bekleyenler</h3>
            <span className="status-pill pending">{pending.length ? `${pending.length} bekleyen` : "Tamamlandı"}</span>
          </div>
          <div className="req-list">
            {pending.map((request) => (
              <div key={request.id} className="req-item">
                <div className="req-user">
                  <div className="req-avatar">{initialsOf(request.employeeName)}</div>
                  <div className="req-meta"><h4>{request.employeeName}</h4><span>{request.description || "İzin talebi"}</span></div>
                </div>
                <div className="req-details">
                  <div><span>İzin Türü</span><strong>{request.leaveTypeName}</strong></div>
                  <div><span>Süre</span><strong>{request.days} gün</strong></div>
                  <div><span>Tarihler</span><strong>{formatDateRange(request.startDate, request.endDate)}</strong></div>
                </div>
                <div className="req-actions">
                  <button className="btn btn-secondary btn-sm" disabled={decide.isPending} onClick={() => resolve(request.id!, request.employeeName ?? "Talep", false)}>Reddet</button>
                  <button className="btn btn-primary btn-sm" disabled={decide.isPending} onClick={() => resolve(request.id!, request.employeeName ?? "Talep", true)}>Onayla</button>
                </div>
              </div>
            ))}
            {pending.length === 0 && <p className="pending-desc">Bekleyen talep yok.</p>}
          </div>
        </div>
      </div>

      <div className="table-section">
        <div className="section-head manager-table-head">
          <h3>Departman Bazlı Kullanım Raporu</h3>
          <span className="status-pill info">Demo</span>
        </div>
        <table className="mini-table">
          <thead><tr><th>Departman</th><th>Toplam Personel</th><th>Kullanılan İzin</th><th>Kalan Hak</th><th>Doluluk</th></tr></thead>
          <tbody>
            {DEMO_DEPT_USAGE.map(([dept, staff, used, remaining, fill]) => (
              <tr key={dept}>
                <td><strong>{dept}</strong></td>
                <td>{staff}</td>
                <td>{used}</td>
                <td>{remaining}</td>
                <td><div className="progress-mini"><div className="p-fill" style={{ width: `${fill}%` }} /></div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
```

- [x] **Step 3: `routes.tsx` `pageFor`'a ekle**

```tsx
import { ManagerPage } from "./features/manager/ManagerPage";
// pageFor:
  manager: ManagerPage,
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS. `npm run build` → hatasız.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/manager/ frontend/src/routes.tsx
git commit -m "feat(frontend): yönetici konsolu — gerçek KPI/onay paneli, demo analitik, CSV raporu"
```

---

### Task 7: Uçtan uca duman testi + parite + günlük + kapanış

**Files:**
- Modify: `docs/gelistirme-gunlugu.md`; gerekirse küçük düzeltmeler.

- [x] **Step 1: Backend + frontend'i başlat** (`cd backend && dotnet run --project src/Ekipzen.API --launch-profile http` + `cd frontend && npm run dev`)

- [x] **Step 2: Duman testi** (Playwright, scratchpad script'i; giriş `demo123`; **tab tıklamalarında `exact: true`** — dilim 6'daki `has-text` alt dizgi tuzağını hatırla)

1. hr-admin → `#/actions`: KPI'lar + kartlar; öncelik/kaynak/sahip filtreleri listeyi daraltır; sekmeler (Açık/Bu Hafta/Tamamlanan) doğru kartları gösterir.
2. "Yeni Aksiyon" → form → kaydet → kartlarda görünür; sidebar rozeti artar.
3. Kartta "Bu haftaya al" → Bu Hafta sekmesine düşer; "Tamamlandı işaretle" → Tamamlanan; silme → kart kaybolur.
4. Denetim İzi sekmesi: gerçek audit kayıtları (önceki işlemler görünür).
5. `#/risk/action-center` → aynı sayfa render olur.
6. Header arama: "ahmet" → personel sonucu; tıkla → `#/personnel`; hr-admin'de aday sonucu da gelir; employee girişinde arama yalnız sayfa+kapsamlı sonuç döner.
7. hr-admin → `#/settings`: şirket adı değiştir → Kaydet → yenilemede kalıcı; logo yükle (png) → görünür; bildirim toggle → kalıcı; şifre değiştir (yanlış mevcut şifre → hata mesajı; doğru → başarı + yeni şifreyle login); 2FA toggle kalıcı; abonelik bölümü dolu.
8. employee → `#/settings` ve `#/manager` → yetki kilidi.
9. manager (`ece.arslan@hrmaster.local`) → `#/manager`: KPI'lar dolu, bekleyen talep varsa Onayla/Reddet çalışır (yoksa çalışan tarafında talep oluşturup dön), "Raporu İndir" CSV indirir.
10. Çalışan → `#/actions`: kartlar salt-okur (ilerletme/silme/Yeni Aksiyon yok, Denetim İzi sekmesi yok).

- [x] **Step 3: Görsel parite** — eski/yeni `#/actions`, `#/settings`, `#/manager` yan yana. Bilinçli farklar: aksiyon kartlarında ilerletme/silme + "Yeni Aksiyon", denetim izinin gerçek veri/rol kısıtı, ayarlarda gerçek kalıcılık + toggle'ların anında kaydı, manager'da ay/departman seçicilerinin kaldırılması + Demo pill'leri + gerçek onay paneli. Diğer farklar DOM/class düzeltmesiyle giderilir.

- [x] **Step 4: Günlük + kapanış commit** — "Şu an neredeyiz" → Dilim 8 (Kapanış: legacy taşıma + README); Dilim 7 kaydı; plan kutuları işaretlenir.

```bash
git add frontend/ docs/
git commit -m "test(frontend): dilim 7 duman testi, parite kontrolü ve günlük güncellemesi"
```

---

## Yürütme notu (paralellik)

Task 1→2→3 sıralıdır. Task 4 (arama), Task 5 (ayarlar), Task 6 (manager) birbirinden ve 2-3'ten bağımsızdır; ancak Task 2/5/6 aynı `routes.tsx` dosyasına dokunur — paralel yürütülecekse rota adımları ana oturumda yapılır.

## Sonraki dilimler

Dilim 8 (Kapanış): eski dosyalar `legacy-frontend/`e taşınır, README ve dokümantasyon güncellenir. Planı bu dilim main'e merge edildikten sonra yazılır.
