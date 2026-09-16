# React Port Dilim 6: İşe Alım (ATS) + Uyum Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eski mock `#/recruitment` (ATS) ve `#/risk/compliance` (Uyum Risk Merkezi) ekranlarını gerçek backend uçlarına bağlı React sayfaları olarak porta et: aday havuzu + detay + pipeline + işe alım dönüşümü ve uyum belgesi CRUD + denetim hazırlığı.

**Architecture:** Dilim 3–5 ile aynı desen — `features/recruitment/` ve `features/compliance/` altında `queries.ts` (TanStack Query + üretilmiş `schema.d.ts` tipleri) + sayfa component'leri; eski DOM/class yapısı birebir korunur, veri mock yerine API'den gelir. `routes.tsx` `pageFor` eşlemesine `recruitment` eklenir, `compliance-risk` yeni operasyonel sayfayla değiştirilir (dashboard'daki salt-okur `ComplianceRiskPage` silinir).

**Tech Stack:** React 18 + TypeScript, TanStack Query, Vitest + RTL (`stubApi`/`renderPage`), mevcut CSS (`styles/recruitment.css`, `styles/main.css` — kopyalar `frontend/src/styles/`te).

## Global Constraints

- Eski DOM paritesi: class adları, id'ler, ikonlar ve Türkçe metinler `components/recruitment.js` ve `components/dashboard.js` (`ComplianceRiskDetail`) ile birebir; bilinçli farklar görev notlarında listelenir.
- Renk/stil hardcode yok; mevcut class'lar kullanılır. Tek CSS eklemesi Task 3'te (`.status-tag.hired`, token'larla).
- Tüm kullanıcı metinleri Türkçe.
- TDD: her görevde önce başarısız test, sonra implementasyon, sonra tüm testler (`npm test -- --run`, `frontend/` içinde).
- API çağrıları yalnız `apiFetch`/`apiDownload` (`src/api/client.ts`) üzerinden; ham `fetch` yok. Hatalar `ApiError` olarak yakalanır, mesaj kullanıcıya gösterilir.
- Tipler `src/api/schema.d.ts`'ten (`components["schemas"][...]`); elle DTO tanımlanmaz.
- Rol denetimi `useAuth()` (`user?.role`) ile; rota kaydı `routes.tsx`'te zaten var (`recruitment` → yalnız `hr-admin`; `compliance-risk` → `hr-admin`+`manager`).

### API sözleşmesi (backend hazır, değişmez)

İşe alım (hepsi yalnız hr-admin; `RecruitmentController`):
- `GET /api/candidates?search=&status=` → `CandidateListItemDto[]` (`id,name,appliedRole,status,score,initials,appliedAtUtc`)
- `GET /api/candidates/{id}` → `CandidateDetailDto` (+`location,experienceYears,summary,skills[],experiences[],notes[],evaluations[],history[]`)
- `POST /api/candidates` (`CreateCandidateCommand`: `name,appliedRole,positionId?,score,location?,experienceYears,summary?,skills[]?,experiences[]?`) → 201 detay
- `PATCH /api/candidates/{id}/status` `{status}` — geçerli: `Yeni|Mülakat|Teklif|Red`; işe alınmış aday → 409
- `POST /api/candidates/{id}/notes` `{noteType,text}` — tür: `Teknik Mülakat|İK Görüşmesi`
- `POST /api/candidates/{id}/evaluations` `{criterion,score,maxScore}` (UI'da kullanılmıyor — eski ekranda ekleme yoktu)
- `POST /api/candidates/{id}/hire` `{departmentId,title?,hireDate?}` → `HireResultDto` (`candidateId,employeeId,employeeName`); zaten işe alınmış/reddedilmiş → 409
- Aday durum kataloğu: `Yeni|Mülakat|Teklif|Red|İşe Alındı` (İşe Alındı yalnız hire ucuyla)

Uyum (`ComplianceController`; okuma hr-admin+manager — manager yalnız ekibini görür; mutasyonlar yalnız hr-admin):
- `GET /api/compliance/documents?status=&level=&search=` → `ComplianceDocumentDto[]` (`id,employeeId,employee,dept,document,owner,dueDate,dueLabel,status,level`)
- `GET /api/compliance/readiness` → `ComplianceReadinessDto` (`totalCount,completedCount,missingCount,dueSoonCount,inReviewCount,ownedCount,documentComplianceScore,readinessScore,readinessRisk,auditChecklist[],recommendedActions[]`)
- `POST /api/compliance/documents` (`CreateComplianceDocumentCommand`: `employeeId,documentName,ownerName?,dueDate?,status,level`) — aynı personelde aynı adlı açık belge → 409
- `PUT /api/compliance/documents/{id}` `{documentName,dueDate,level}`
- `PATCH /api/compliance/documents/{id}/status` `{status}` — aynı duruma geçiş → 409; `Tamamlandı`ya geçişte level `low` olur
- `PATCH /api/compliance/documents/{id}/owner` `{ownerName}`
- Belge durum kataloğu: `Eksik|İncelemede|Süresi Yaklaşıyor|Tamamlandı`; risk: `high|medium|low`; `dueLabel`: `Tamamlandı|Bugün|Gecikti|"N gün"|-`

Seed durumu: uyum belgeleri seed'li; **aday/pozisyon seed'i yok** → İşe Alım boş durumla açılır, "Yeni Aday" ile beslenir (bilinçli fark).

---

### Task 1: İşe alım format yardımcıları + query katmanı

**Files:**
- Create: `frontend/src/features/recruitment/format.ts`
- Create: `frontend/src/features/recruitment/queries.ts`
- Test: `frontend/src/features/recruitment/format.test.ts`

**Interfaces:**
- Consumes: `apiFetch` (`../../api/client`), `components["schemas"]` tipleri.
- Produces:
  - `formatTimeAgo(iso?: string | null): string` — `"Az önce" | "{h}s önce" | "{d}g önce" | "-"`
  - `statusTagClass(status?: string | null): string` — `Yeni→"yeni"`, `Mülakat→"mülakat"`, `Teklif→"teklif"`, `Red→"red"`, `İşe Alındı→"hired"`
  - `scoreClass(score?: number | null): "high" | "mid"` — `>80 → "high"`
  - `PIPELINE_STATUSES: string[]` = `["Yeni","Mülakat","Teklif","Red"]`, `NOTE_TYPES: string[]` = `["Teknik Mülakat","İK Görüşmesi"]`
  - `useCandidates(search: string, status: string)`, `useCandidate(id: number | null)`, `useCreateCandidate()`, `useSetCandidateStatus()` (`{id,status}`), `useAddInterviewNote()` (`{id,noteType,text}`), `useHireCandidate()` (`{id,departmentId,title?,hireDate?}`)
  - Tipler: `CandidateListItemDto`, `CandidateDetailDto`, `InterviewNoteDto`, `HireResultDto`, `CreateCandidateCommand`

- [x] **Step 1: Başarısız testleri yaz** — `format.test.ts`:

```ts
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { formatTimeAgo, scoreClass, statusTagClass } from "./format";

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date("2026-07-16T12:00:00Z"));
});
afterEach(() => vi.useRealTimers());

test("formatTimeAgo saat ve gün etiketleri", () => {
  expect(formatTimeAgo("2026-07-16T11:30:00Z")).toBe("Az önce");
  expect(formatTimeAgo("2026-07-16T10:00:00Z")).toBe("2s önce");
  expect(formatTimeAgo("2026-07-15T09:00:00Z")).toBe("1g önce");
  expect(formatTimeAgo(null)).toBe("-");
  expect(formatTimeAgo("bozuk")).toBe("-");
});

test("statusTagClass eski CSS sınıflarına eşler", () => {
  expect(statusTagClass("Yeni")).toBe("yeni");
  expect(statusTagClass("Mülakat")).toBe("mülakat");
  expect(statusTagClass("Teklif")).toBe("teklif");
  expect(statusTagClass("Red")).toBe("red");
  expect(statusTagClass("İşe Alındı")).toBe("hired");
});

test("scoreClass 80 eşiği", () => {
  expect(scoreClass(92)).toBe("high");
  expect(scoreClass(80)).toBe("mid");
  expect(scoreClass(undefined)).toBe("mid");
});
```

Run: `npm test -- --run src/features/recruitment/format.test.ts` → FAIL.

- [x] **Step 2: `format.ts` yaz**

```ts
export const formatTimeAgo = (iso?: string | null): string => {
  if (!iso) return "-";
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return "-";
  const hours = Math.floor((Date.now() - then) / 3_600_000);
  if (hours < 1) return "Az önce";
  if (hours < 24) return `${hours}s önce`;
  return `${Math.floor(hours / 24)}g önce`;
};

// Eski CSS: .status-tag.yeni/.mülakat/.teklif/.red; "hired" Task 3'te eklenir.
export const statusTagClass = (status?: string | null): string =>
  status === "İşe Alındı" ? "hired" : (status ?? "").toLocaleLowerCase("tr-TR");

export const scoreClass = (score?: number | null): "high" | "mid" =>
  (score ?? 0) > 80 ? "high" : "mid";

export const PIPELINE_STATUSES = ["Yeni", "Mülakat", "Teklif", "Red"];
export const NOTE_TYPES = ["Teknik Mülakat", "İK Görüşmesi"];
```

- [x] **Step 3: `queries.ts` yaz**

```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type CandidateListItemDto = components["schemas"]["CandidateListItemDto"];
export type CandidateDetailDto = components["schemas"]["CandidateDetailDto"];
export type InterviewNoteDto = components["schemas"]["InterviewNoteDto"];
export type HireResultDto = components["schemas"]["HireResultDto"];
export type CreateCandidateCommand = components["schemas"]["CreateCandidateCommand"];

const candidatesPath = (search: string, status: string): string => {
  const params = new URLSearchParams();
  if (search) params.set("search", search);
  if (status) params.set("status", status);
  const query = params.toString();
  return query ? `/candidates?${query}` : "/candidates";
};

export const useCandidates = (search: string, status: string) =>
  useQuery({
    queryKey: ["recruitment", "candidates", search, status],
    queryFn: () => apiFetch<CandidateListItemDto[]>(candidatesPath(search, status)),
  });

export const useCandidate = (id: number | null) =>
  useQuery({
    queryKey: ["recruitment", "candidate", id],
    queryFn: () => apiFetch<CandidateDetailDto>(`/candidates/${id}`),
    enabled: id !== null,
  });

export const useCreateCandidate = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateCandidateCommand) =>
      apiFetch<CandidateDetailDto>("/candidates", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
  });
};

export const useSetCandidateStatus = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiFetch<CandidateDetailDto>(`/candidates/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
  });
};

export const useAddInterviewNote = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, noteType, text }: { id: number; noteType: string; text: string }) =>
      apiFetch<InterviewNoteDto>(`/candidates/${id}/notes`, {
        method: "POST",
        body: JSON.stringify({ noteType, text }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["recruitment"] }),
  });
};

export const useHireCandidate = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, departmentId, title, hireDate }: {
      id: number; departmentId: number; title?: string | null; hireDate?: string | null;
    }) =>
      apiFetch<HireResultDto>(`/candidates/${id}/hire`, {
        method: "POST",
        body: JSON.stringify({ departmentId, title: title || null, hireDate: hireDate || null }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["recruitment"] });
      queryClient.invalidateQueries({ queryKey: ["employees"] });
    },
  });
};
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/recruitment/
git commit -m "feat(frontend): işe alım format yardımcıları ve query katmanı"
```

---

### Task 2: RecruitmentPage kabuğu — aday havuzu listesi + arama/filtre + rota kaydı

**Files:**
- Create: `frontend/src/features/recruitment/RecruitmentPage.tsx`
- Modify: `frontend/src/routes.tsx` (`pageFor`'a `recruitment` eklenir)
- Test: `frontend/src/features/recruitment/RecruitmentPage.test.tsx`

**Interfaces:**
- Consumes: `useCandidates`, `formatTimeAgo`, `statusTagClass`, `scoreClass`.
- Produces: `RecruitmentPage()`. Sidebar: server-side arama (300ms debounce) + durum filtre sekmeleri (`Tümü|Yeni|Mülakat`) + aday listesi; ilk aday otomatik seçilir. Detay alanı ve "Yeni Aday" modalı placeholder'dır (Task 3/4 doldurur). İç state: `selectedId: number | null`, `createOpen: boolean` — Task 3/4 bunları kullanır.

Davranış: eski `Recruitment()` DOM'u (`#ats-container > aside.ats-sidebar + main.ats-detail`). Bilinçli farklar: arama/filtre server-side (eskide client-side DOM gizleme), "Yeni Aday" butonu (seed yok), boş durum metni.

- [x] **Step 1: Başarısız testleri yaz** — `RecruitmentPage.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { RecruitmentPage } from "./RecruitmentPage";

const candidates = [
  { id: 5, name: "Burak Yılmaz", appliedRole: "Senior Frontend Developer", status: "Mülakat", score: 92, initials: "BY", appliedAtUtc: "2026-07-16T10:00:00Z" },
  { id: 6, name: "Selin Koç", appliedRole: "UI/UX Designer", status: "Yeni", score: 85, initials: "SK", appliedAtUtc: "2026-07-15T09:00:00Z" },
];

beforeEach(() =>
  stubApi({
    "/api/candidates": candidates,
    "/api/candidates/5": { ...candidates[0], skills: [], experiences: [], notes: [], evaluations: [], history: [] },
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <ToastProvider>
      <RecruitmentPage />
    </ToastProvider>,
  );

test("aday listesi skor ve durum etiketleriyle dolar", async () => {
  renderShell();
  expect(await screen.findByText("Burak Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("%92 uygun")).toBeInTheDocument();
  expect(screen.getByText("Selin Koç")).toBeInTheDocument();
  expect(screen.getByText("Aday Havuzu")).toBeInTheDocument();
});

test("durum filtre sekmesi server-side sorgu atar", async () => {
  renderShell();
  await screen.findByText("Burak Yılmaz");
  await userEvent.click(screen.getByRole("button", { name: "Yeni" }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates?status=Yeni");
    expect(hit).toBe(true);
  });
});

test("arama 300ms debounce ile server-side gider", async () => {
  renderShell();
  await screen.findByText("Burak Yılmaz");
  await userEvent.type(screen.getByLabelText("Aday ara"), "burak");
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/candidates?search=burak");
    expect(hit).toBe(true);
  });
});

test("aday yoksa boş durum ve Yeni Aday butonu görünür", async () => {
  stubApi({ "/api/candidates": [] });
  renderShell();
  expect(await screen.findByText(/Henüz aday yok/)).toBeInTheDocument();
  expect(screen.getByRole("button", { name: /Yeni Aday/ })).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/recruitment/RecruitmentPage.test.tsx` → FAIL.

- [x] **Step 2: `RecruitmentPage.tsx` yaz** (detay + modal placeholder; eski `Recruitment()` DOM paritesi)

```tsx
import { useEffect, useState } from "react";
import { PageError, PageLoading } from "../shared/PageState";
import { formatTimeAgo, scoreClass, statusTagClass } from "./format";
import { useCandidates } from "./queries";

const FILTER_TABS: [string, string][] = [["", "Tümü"], ["Yeni", "Yeni"], ["Mülakat", "Mülakat"]];

export function RecruitmentPage() {
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  // Personel sayfasındaki server-side arama debounce deseni.
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const candidatesQ = useCandidates(debouncedSearch, statusFilter);

  if (candidatesQ.isPending) return <PageLoading />;
  if (candidatesQ.isError) return <PageError error={candidatesQ.error} />;

  const candidates = candidatesQ.data;
  const activeId = selectedId ?? candidates[0]?.id ?? null;

  return (
    <div id="ats-container">
      <aside className="ats-sidebar">
        <div className="sidebar-header">
          <div>
            <h3>Aday Havuzu <span className="badge-count">{candidates.length}</span></h3>
            <p>Aktif pozisyonlara göre sıralandı</p>
          </div>
          <button className="btn btn-primary btn-sm" onClick={() => setCreateOpen(true)}>
            <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Aday
          </button>
          <div className="search-wrap">
            <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
            <label className="sr-only" htmlFor="candidate-search">Aday ara</label>
            <input
              id="candidate-search"
              type="text"
              placeholder="Aday ara"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="filter-tabs">
            {FILTER_TABS.map(([value, label]) => (
              <button
                key={label}
                className={`ft-btn ${statusFilter === value ? "active" : ""}`}
                onClick={() => setStatusFilter(value)}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
        <div className="candidate-list">
          {candidates.map((candidate) => (
            <div
              key={candidate.id}
              className={`candidate-item ${candidate.id === activeId ? "active" : ""}`}
              onClick={() => setSelectedId(candidate.id ?? null)}
            >
              <div className="ci-avatar" aria-hidden="true">{candidate.initials}</div>
              <div className="ci-info">
                <div className="ci-header">
                  <h4>{candidate.name}</h4>
                  <span className="ci-time">{formatTimeAgo(candidate.appliedAtUtc)}</span>
                </div>
                <p>{candidate.appliedRole}</p>
                <div className="ci-meta">
                  <span className={`status-tag ${statusTagClass(candidate.status)}`}>{candidate.status}</span>
                  <span className={`score-text ${scoreClass(candidate.score)}`}>%{candidate.score} uygun</span>
                </div>
              </div>
            </div>
          ))}
          {candidates.length === 0 && (
            <p className="pending-desc">Henüz aday yok. "Yeni Aday" ile ilk adayı ekleyin.</p>
          )}
        </div>
      </aside>

      <main className="ats-detail">
        {/* Aday detayı Task 3'te */}
        {activeId === null && (
          <div className="card"><p className="pending-desc">Görüntülenecek aday yok.</p></div>
        )}
      </main>

      {/* Yeni Aday modalı Task 4'te */}
      {createOpen && null}
    </div>
  );
}
```

- [x] **Step 3: `routes.tsx` `pageFor`'a ekle**

```tsx
// import bloğuna:
import { RecruitmentPage } from "./features/recruitment/RecruitmentPage";
// pageFor içine (personnel satırının üstüne):
  recruitment: RecruitmentPage,
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/recruitment/ frontend/src/routes.tsx
git commit -m "feat(frontend): işe alım kabuğu — aday havuzu, server-side arama/filtre, rota"
```

---

### Task 3: Aday detayı — sekmeler, not ekleme, pipeline geçişi, İşe Al modalı

**Files:**
- Create: `frontend/src/features/recruitment/CandidateDetail.tsx`
- Modify: `frontend/src/features/recruitment/RecruitmentPage.tsx` (detay placeholder → `<CandidateDetail />`)
- Modify: `frontend/src/styles/recruitment.css` (`.status-tag.hired` eklenir)
- Test: `frontend/src/features/recruitment/CandidateDetail.test.tsx`

**Interfaces:**
- Consumes: `useCandidate`, `useSetCandidateStatus`, `useAddInterviewNote`, `useHireCandidate`, `useDepartments` (`../personnel/queries`), `useToast`, `formatTimeAgo`, `statusTagClass`, `scoreClass`, `PIPELINE_STATUSES`, `NOTE_TYPES`.
- Produces: `CandidateDetail({ id }: { id: number })`.

Davranış: eski detay DOM'u (dh-profile/dh-actions/detail-tabs/tab-content). Bilinçli farklar: durum select'i (eskide pipeline geçişi yoktu), İşe Al gerçek modal (departman/ünvan/tarih → `POST hire`), not ekleme gerçek `POST`, işe alınmış adayda durum select ve İşe Al pasif.

- [x] **Step 1: Başarısız testleri yaz** — `CandidateDetail.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { CandidateDetail } from "./CandidateDetail";

const detail = {
  id: 5, name: "Burak Yılmaz", appliedRole: "Senior Frontend Developer",
  positionId: null, positionTitle: null, status: "Mülakat", score: 92, initials: "BY",
  appliedAtUtc: "2026-07-16T10:00:00Z", location: "İstanbul", experienceYears: 5,
  summary: "React ve modern JavaScript konusunda güçlü aday.",
  skills: [{ id: 1, name: "React.js" }, { id: 2, name: "TypeScript" }],
  experiences: [{ id: 1, title: "Senior Frontend Developer", company: "TechSolutions A.Ş.", period: "2021 - Günümüz", description: "Arayüz geliştirme." }],
  notes: [{ id: 1, authorName: "Ayşe Demir", noteType: "İK Görüşmesi", text: "İletişim becerileri kuvvetli.", createdAtUtc: "2026-07-15T14:30:00Z" }],
  evaluations: [{ id: 1, criterion: "Teknik Yeterlilik", score: 4, maxScore: 5 }],
  history: [{ id: 1, event: "Başvuru alındı", occurredAtUtc: "2026-07-14T09:00:00Z" }],
};
const departments = [{ id: 1, name: "Yazılım", employeeCount: 4 }];

beforeEach(() =>
  stubApi({
    "/api/candidates/5": detail,
    "/api/candidates/5/status": { ...detail, status: "Teklif" },
    "/api/candidates/5/notes": detail.notes[0],
    "/api/candidates/5/hire": { candidateId: 5, employeeId: 9, employeeName: "Burak Yılmaz" },
    "/api/departments": departments,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderDetail = () =>
  renderPage(
    <ToastProvider>
      <CandidateDetail id={5} />
    </ToastProvider>,
  );

test("detay profil, etiketler ve özgeçmiş sekmesiyle açılır", async () => {
  renderDetail();
  expect(await screen.findByRole("heading", { name: "Burak Yılmaz" })).toBeInTheDocument();
  expect(screen.getByText("5 yıl deneyim")).toBeInTheDocument();
  expect(screen.getByText("React.js")).toBeInTheDocument();
  expect(screen.getByText("TechSolutions A.Ş. • 2021 - Günümüz")).toBeInTheDocument();
});

test("mülakat notu ekleme POST notes ucuna gider", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: "Mülakat Notları" }));
  await userEvent.type(screen.getByLabelText("Mülakat notu"), "Teknik derinlik iyi.");
  await userEvent.click(screen.getByRole("button", { name: "Not Ekle" }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/notes" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      noteType: "Teknik Mülakat", text: "Teknik derinlik iyi.",
    });
  });
});

test("pipeline durumu PATCH status ucuna gider", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Pipeline durumu"), "Teklif");
  await waitFor(() => {
    const patched = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/status" && i?.method === "PATCH",
    );
    expect(patched).toBeTruthy();
    expect(JSON.parse(String(patched![1]?.body))).toMatchObject({ status: "Teklif" });
  });
});

test("İşe Al modalı departmanla hire ucuna gider", async () => {
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  await userEvent.click(screen.getByRole("button", { name: /İşe Al/ }));
  await userEvent.selectOptions(await screen.findByLabelText("Departman"), "1");
  await userEvent.click(screen.getByRole("button", { name: /Onayla/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates/5/hire" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({ departmentId: 1 });
  });
});

test("işe alınmış adayda durum select ve İşe Al pasiftir", async () => {
  stubApi({ "/api/candidates/5": { ...detail, status: "İşe Alındı" }, "/api/departments": departments });
  renderDetail();
  await screen.findByRole("heading", { name: "Burak Yılmaz" });
  expect(screen.getByLabelText("Pipeline durumu")).toBeDisabled();
  expect(screen.getByRole("button", { name: /İşe Al/ })).toBeDisabled();
});
```

Run: `npm test -- --run src/features/recruitment/CandidateDetail.test.tsx` → FAIL.

- [x] **Step 2: `CandidateDetail.tsx` yaz**

```tsx
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { useDepartments } from "../personnel/queries";
import { NOTE_TYPES, PIPELINE_STATUSES, formatTimeAgo } from "./format";
import { useAddInterviewNote, useCandidate, useHireCandidate, useSetCandidateStatus } from "./queries";

const TABS: [string, string][] = [
  ["cv", "Özgeçmiş"], ["notes", "Mülakat Notları"], ["eval", "Değerlendirme"], ["history", "Geçmiş"],
];

const initialsOf = (name?: string | null): string =>
  (name ?? "")
    .split(" ")
    .filter(Boolean)
    .map((part) => part[0]?.toLocaleUpperCase("tr-TR"))
    .slice(0, 2)
    .join("") || "İK";

export function CandidateDetail({ id }: { id: number }) {
  const { showToast } = useToast();
  const detailQ = useCandidate(id);
  const setStatus = useSetCandidateStatus();
  const addNote = useAddInterviewNote();
  const [tab, setTab] = useState("cv");
  const [noteText, setNoteText] = useState("");
  const [noteType, setNoteType] = useState(NOTE_TYPES[0]);
  const [hireOpen, setHireOpen] = useState(false);

  if (detailQ.isPending) return <PageLoading />;
  if (detailQ.isError) return <PageError error={detailQ.error} />;

  const candidate = detailQ.data;
  const isHired = candidate.status === "İşe Alındı";

  const changeStatus = async (status: string) => {
    try {
      await setStatus.mutateAsync({ id, status });
      showToast(`${candidate.name} durumu "${status}" olarak güncellendi.`, "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Durum güncellenemedi.", "error");
    }
  };

  const submitNote = async () => {
    const text = noteText.trim();
    if (!text) {
      showToast("Önce not içeriğini yazın.", "warning");
      return;
    }
    try {
      await addNote.mutateAsync({ id, noteType, text });
      setNoteText("");
      showToast("Mülakat notu eklendi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Not eklenemedi.", "error");
    }
  };

  return (
    <>
      <div className="detail-header">
        <div className="dh-profile">
          <div className="dh-avatar-lg">{candidate.initials}</div>
          <div>
            <h2>{candidate.name}</h2>
            <p className="dh-role">{candidate.appliedRole}</p>
            <div className="dh-tags">
              <span className="tag-pill"><i aria-hidden="true" className="fa-solid fa-location-dot" /> {candidate.location || "Belirtilmedi"}</span>
              <span className="tag-pill"><i aria-hidden="true" className="fa-solid fa-briefcase" /> {candidate.experienceYears ?? 0} yıl deneyim</span>
            </div>
          </div>
        </div>
        <div className="dh-actions">
          <div className="match-score"><span className="score-circle">{candidate.score}</span><span className="score-label">AI puanı</span></div>
          <label className="sr-only" htmlFor="candidate-status">Pipeline durumu</label>
          <select
            id="candidate-status"
            className="input-control"
            value={isHired ? "" : candidate.status ?? "Yeni"}
            disabled={isHired || setStatus.isPending}
            onChange={(e) => changeStatus(e.target.value)}
          >
            {isHired && <option value="">İşe Alındı</option>}
            {PIPELINE_STATUSES.map((status) => (
              <option key={status} value={status}>{status}</option>
            ))}
          </select>
          <button className="btn btn-primary" disabled={isHired} onClick={() => setHireOpen(true)}>
            <i aria-hidden="true" className="fa-solid fa-thumbs-up" /> İşe Al
          </button>
        </div>
      </div>

      <div className="detail-tabs">
        {TABS.map(([key, label]) => (
          <button key={key} className={`tab-link ${tab === key ? "active" : ""}`} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}
      </div>

      <div className="detail-content-wrapper">
        {tab === "cv" && (
          <div id="tab-cv" className="tab-content active">
            <div className="content-block">
              <h4><i aria-hidden="true" className="fa-regular fa-file-lines" /> Başvuru Özeti</h4>
              <p className="summary-text">{candidate.summary || "Başvuru özeti girilmedi."}</p>
            </div>
            <div className="content-block">
              <h4><i aria-hidden="true" className="fa-solid fa-wand-magic-sparkles" /> Yetenek Seti</h4>
              <div className="skills-wrap">
                {(candidate.skills ?? []).map((skill) => (
                  <span key={skill.id} className="skill-tag">{skill.name}</span>
                ))}
                {(candidate.skills ?? []).length === 0 && <span className="text-muted">Yetenek girilmedi.</span>}
              </div>
            </div>
            <div className="content-block">
              <h4><i aria-hidden="true" className="fa-solid fa-history" /> İş Deneyimi</h4>
              <div className="timeline">
                {(candidate.experiences ?? []).map((experience) => (
                  <div key={experience.id} className="tl-item">
                    <div className="tl-dot" />
                    <div className="tl-content">
                      <strong>{experience.title}</strong>
                      <span>{experience.company} • {experience.period || "-"}</span>
                      <p>{experience.description}</p>
                    </div>
                  </div>
                ))}
                {(candidate.experiences ?? []).length === 0 && <p className="text-muted">Deneyim girilmedi.</p>}
              </div>
            </div>
          </div>
        )}

        {tab === "notes" && (
          <div id="tab-notes" className="tab-content active">
            <div className="notes-container">
              <div className="add-note-box">
                <label className="sr-only" htmlFor="interview-note">Mülakat notu</label>
                <textarea
                  id="interview-note"
                  placeholder="Mülakat notunuzu buraya girin"
                  value={noteText}
                  onChange={(e) => setNoteText(e.target.value)}
                />
                <div className="note-actions">
                  <label className="sr-only" htmlFor="interview-note-type">Not türü</label>
                  <select id="interview-note-type" value={noteType} onChange={(e) => setNoteType(e.target.value)}>
                    {NOTE_TYPES.map((type) => <option key={type}>{type}</option>)}
                  </select>
                  <button className="btn btn-primary btn-sm" onClick={submitNote} disabled={addNote.isPending}>
                    Not Ekle
                  </button>
                </div>
              </div>
              {(candidate.notes ?? []).map((note) => (
                <div key={note.id} className="note-item">
                  <div className="note-avatar" aria-hidden="true">{initialsOf(note.authorName)}</div>
                  <div className="note-body">
                    <div className="note-header">
                      <strong>{note.authorName} ({note.noteType})</strong> <span>{formatTimeAgo(note.createdAtUtc)}</span>
                    </div>
                    <p>{note.text}</p>
                  </div>
                </div>
              ))}
              {(candidate.notes ?? []).length === 0 && <p className="pending-desc">Henüz mülakat notu yok.</p>}
            </div>
          </div>
        )}

        {tab === "eval" && (
          <div id="tab-eval" className="tab-content active">
            <div className="eval-grid">
              {(candidate.evaluations ?? []).map((evaluation) => (
                <div key={evaluation.id} className="eval-card">
                  <div className="eval-header">
                    <span>{evaluation.criterion}</span>
                    <strong>{Number(evaluation.score).toFixed(1)}/{evaluation.maxScore}</strong>
                  </div>
                  <div className="progress-bg">
                    <div
                      className="progress-fill"
                      style={{ width: `${Math.round(((evaluation.score ?? 0) / (evaluation.maxScore || 1)) * 100)}%` }}
                    />
                  </div>
                </div>
              ))}
              {(candidate.evaluations ?? []).length === 0 && <p className="pending-desc">Henüz değerlendirme yok.</p>}
            </div>
          </div>
        )}

        {tab === "history" && (
          <div id="tab-history" className="tab-content active">
            <div className="history-list">
              {(candidate.history ?? []).map((entry) => (
                <div key={entry.id} className="hist-item">
                  <i aria-hidden="true" className="fa-solid fa-envelope bg-blue" />
                  <div>
                    <strong>{entry.event}</strong>
                    <small>{formatTimeAgo(entry.occurredAtUtc)}</small>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {hireOpen && (
        <HireModal
          candidateId={id}
          candidateName={candidate.name ?? ""}
          appliedRole={candidate.appliedRole ?? ""}
          onClose={() => setHireOpen(false)}
        />
      )}
    </>
  );
}

function HireModal({ candidateId, candidateName, appliedRole, onClose }: {
  candidateId: number; candidateName: string; appliedRole: string; onClose: () => void;
}) {
  const { showToast } = useToast();
  const hire = useHireCandidate();
  const departmentsQ = useDepartments();
  const [departmentId, setDepartmentId] = useState("");
  const [title, setTitle] = useState(appliedRole);
  const [hireDate, setHireDate] = useState(new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    if (!departmentId) {
      setError("Departman seçin.");
      return;
    }
    try {
      const result = await hire.mutateAsync({
        id: candidateId,
        departmentId: Number(departmentId),
        title: title.trim() || null,
        hireDate: hireDate || null,
      });
      showToast(`${result.employeeName} işe alındı — personel kaydı oluşturuldu.`, "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Aday işe alınamadı.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>İşe Al: {candidateName}</h3>
            <p>Aday personel kaydına dönüştürülür; pozisyon kontenjanı güncellenir.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="İşe alım penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="hire-department">Departman</label>
              <select
                id="hire-department"
                className="input-control"
                value={departmentId}
                onChange={(e) => setDepartmentId(e.target.value)}
              >
                <option value="">Seçin</option>
                {(departmentsQ.data ?? []).map((department) => (
                  <option key={department.id} value={department.id}>{department.name}</option>
                ))}
              </select>
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="hire-title">Ünvan</label>
              <input id="hire-title" className="input-control" value={title} onChange={(e) => setTitle(e.target.value)} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="hire-date">İşe giriş tarihi</label>
              <input id="hire-date" type="date" className="input-control" value={hireDate} onChange={(e) => setHireDate(e.target.value)} />
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={hire.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> Onayla
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [x] **Step 3: `RecruitmentPage.tsx`'te bağla** — detay placeholder'ı değiştir:

```tsx
// import { CandidateDetail } from "./CandidateDetail";
<main className="ats-detail">
  {activeId !== null ? (
    <CandidateDetail id={activeId} />
  ) : (
    <div className="card"><p className="pending-desc">Görüntülenecek aday yok.</p></div>
  )}
</main>
```

- [x] **Step 4: `.status-tag.hired` stilini ekle** — `frontend/src/styles/recruitment.css` dosyasında `.status-tag.teklif` bloğunun altına (mevcut token desenini kopyala; dosyadaki `.status-tag.yeni/.mülakat/.teklif/.red` bloklarına bak, aynı yapıyı kullan):

```css
.status-tag.hired {
  background: var(--success-soft, rgba(24, 129, 90, 0.12));
  color: var(--success, #18815a);
}
```

Not: dosyada `--success` benzeri token adları farklıysa (`grep -n "\.status-tag\." frontend/src/styles/recruitment.css` ile bak) aynı dosyada onaylı/success durumunda kullanılan mevcut değişkenleri kullan; yeni hex tanımlama.

- [x] **Step 5: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 6: Commit**

```bash
git add frontend/src/features/recruitment/ frontend/src/styles/recruitment.css
git commit -m "feat(frontend): aday detayı — sekmeler, not ekleme, pipeline geçişi, işe alım modalı"
```

---

### Task 4: Yeni Aday modalı

**Files:**
- Create: `frontend/src/features/recruitment/CandidateModal.tsx`
- Modify: `frontend/src/features/recruitment/RecruitmentPage.tsx` (modal placeholder → `<CandidateModal />`)
- Test: `frontend/src/features/recruitment/CandidateModal.test.tsx`

**Interfaces:**
- Consumes: `useCreateCandidate`, `useToast`, `ApiError`.
- Produces: `CandidateModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: number) => void })`. Yetenekler virgülle ayrılır → `skills: string[]`.

- [x] **Step 1: Başarısız testleri yaz** — `CandidateModal.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { CandidateModal } from "./CandidateModal";

beforeEach(() =>
  stubApi({
    "/api/candidates": { id: 7, name: "Zeynep Aksoy", appliedRole: "QA Engineer", status: "Yeni", score: 70, initials: "ZA" },
  }),
);
afterEach(() => vi.unstubAllGlobals());

test("form doldurulup kaydedilince POST candidates tam gövdeyle gider", async () => {
  const onCreated = vi.fn();
  renderPage(
    <ToastProvider>
      <CandidateModal onClose={() => {}} onCreated={onCreated} />
    </ToastProvider>,
  );
  await userEvent.type(screen.getByLabelText("Ad Soyad"), "Zeynep Aksoy");
  await userEvent.type(screen.getByLabelText("Başvurulan pozisyon"), "QA Engineer");
  await userEvent.clear(screen.getByLabelText("AI puanı (0-100)"));
  await userEvent.type(screen.getByLabelText("AI puanı (0-100)"), "70");
  await userEvent.type(screen.getByLabelText("Yetenekler (virgülle)"), "Playwright, API testi");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/candidates" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      name: "Zeynep Aksoy", appliedRole: "QA Engineer", score: 70,
      skills: ["Playwright", "API testi"],
    });
    expect(onCreated).toHaveBeenCalledWith(7);
  });
});

test("ad boşsa form hatası gösterilir, istek atılmaz", async () => {
  renderPage(
    <ToastProvider>
      <CandidateModal onClose={() => {}} onCreated={() => {}} />
    </ToastProvider>,
  );
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Ad Soyad ve pozisyon zorunludur.");
  expect(vi.mocked(fetch).mock.calls.some(([, i]) => i?.method === "POST")).toBe(false);
});
```

Run: `npm test -- --run src/features/recruitment/CandidateModal.test.tsx` → FAIL.

- [x] **Step 2: `CandidateModal.tsx` yaz**

```tsx
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useCreateCandidate } from "./queries";

export function CandidateModal({ onClose, onCreated }: {
  onClose: () => void; onCreated: (id: number) => void;
}) {
  const { showToast } = useToast();
  const createCandidate = useCreateCandidate();
  const [form, setForm] = useState({
    name: "", appliedRole: "", score: "70", location: "", experienceYears: "0", summary: "", skills: "",
  });
  const [error, setError] = useState<string | null>(null);

  const set = (key: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
      setForm((f) => ({ ...f, [key]: e.target.value }));

  const submit = async () => {
    setError(null);
    if (!form.name.trim() || !form.appliedRole.trim()) {
      setError("Ad Soyad ve pozisyon zorunludur.");
      return;
    }
    try {
      const candidate = await createCandidate.mutateAsync({
        name: form.name.trim(),
        appliedRole: form.appliedRole.trim(),
        score: Math.max(0, Math.min(100, Math.round(Number(form.score) || 0))),
        location: form.location.trim() || null,
        experienceYears: Math.max(0, Math.round(Number(form.experienceYears) || 0)),
        summary: form.summary.trim() || null,
        skills: form.skills.split(",").map((skill) => skill.trim()).filter(Boolean),
      });
      showToast(`${candidate.name} aday havuzuna eklendi.`, "success");
      if (candidate.id !== undefined) onCreated(candidate.id);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Aday eklenemedi.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>Yeni Aday</h3>
            <p>Aday havuzuna manuel kayıt ekleyin.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Aday penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="cand-name">Ad Soyad</label>
              <input id="cand-name" className="input-control" value={form.name} onChange={set("name")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-role">Başvurulan pozisyon</label>
              <input id="cand-role" className="input-control" value={form.appliedRole} onChange={set("appliedRole")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-score">AI puanı (0-100)</label>
              <input id="cand-score" type="number" className="input-control" value={form.score} onChange={set("score")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-exp">Deneyim (yıl)</label>
              <input id="cand-exp" type="number" className="input-control" value={form.experienceYears} onChange={set("experienceYears")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-location">Lokasyon</label>
              <input id="cand-location" className="input-control" value={form.location} onChange={set("location")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="cand-skills">Yetenekler (virgülle)</label>
              <input id="cand-skills" className="input-control" value={form.skills} onChange={set("skills")} />
            </div>
            <div className="input-group col-span-2">
              <label className="input-label" htmlFor="cand-summary">Başvuru özeti</label>
              <textarea id="cand-summary" className="input-control" rows={3} value={form.summary} onChange={set("summary")} />
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={createCandidate.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> Kaydet
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [x] **Step 3: `RecruitmentPage.tsx`'te bağla** — modal placeholder'ı değiştir:

```tsx
// import { CandidateModal } from "./CandidateModal";
{createOpen && (
  <CandidateModal onClose={() => setCreateOpen(false)} onCreated={(id) => setSelectedId(id)} />
)}
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/recruitment/
git commit -m "feat(frontend): yeni aday modalı — havuza gerçek POST ile kayıt"
```

---

### Task 5: Uyum sayfası (okuma) — belgeler + denetim hazırlığı + rota değişimi

**Files:**
- Create: `frontend/src/features/compliance/queries.ts`
- Create: `frontend/src/features/compliance/CompliancePage.tsx`
- Modify: `frontend/src/routes.tsx` (`compliance-risk` → `CompliancePage`)
- Delete: `frontend/src/features/dashboard/ComplianceRiskPage.tsx`, `frontend/src/features/dashboard/ComplianceRiskPage.test.tsx`
- Test: `frontend/src/features/compliance/CompliancePage.test.tsx`

**Interfaces:**
- Consumes: `apiFetch`, `getLevelText` (`../dashboard/format`), `BackToRisk` (`../dashboard/BackToRisk`), `useAuth`.
- Produces:
  - `queries.ts`: `ComplianceDocumentDto`, `ComplianceReadinessDto`, `ComplianceFilters = { search: string; status: string; level: string }`, `useComplianceDocuments(filters: ComplianceFilters)`, `useComplianceReadiness()`, `useCreateComplianceDocument()`, `useUpdateComplianceDocument()` (`{id, documentName, dueDate, level}`), `useSetComplianceStatus()` (`{id, status}`), `useAssignComplianceOwner()` (`{id, ownerName}`), `COMPLIANCE_STATUSES = ["Eksik","İncelemede","Süresi Yaklaşıyor","Tamamlandı"]`, `RISK_LEVELS = ["high","medium","low"]`
  - `CompliancePage()` ve `compliancePillClass(status)` — Task 6 mutasyon UI'larını (buton/select/modal state) bu sayfaya ekler; bu görev yalnız okuma görünümünü üretir.
- Not: `features/dashboard/queries.ts`teki `useComplianceRisk` (RiskCenterPage özet kartı) **silinmez**; yalnız detay sayfası taşınır.

Davranış: eski `ComplianceRiskDetail` DOM'u. Veri kaynağı değişir: KPI'lar `GET /compliance/readiness`ten (Evrak Uyum Skoru=`documentComplianceScore`, Eksik Evrak=`missingCount`, Süresi Yaklaşan=`dueSoonCount`, Denetim Riski=`readinessRisk`), tablo `GET /compliance/documents`tan. Bilinçli farklar: durum/risk/arama filtreleri (server-side), "Yaklaşan Son Tarihler" paneli belgelerden türetilir (tamamlanmamış + `dueDate`li ilk 5), Denetim Hazırlığı + Önerilen Aksiyonlar readiness'tan gelir.

- [x] **Step 1: Başarısız testleri yaz** — `CompliancePage.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { CompliancePage } from "./CompliancePage";

const documents = [
  { id: 1, employeeId: 3, employee: "Ahmet Yılmaz", dept: "Yazılım", document: "İş Sözleşmesi", owner: "Ayşe Demir", dueDate: "2026-07-20", dueLabel: "4 gün", status: "Süresi Yaklaşıyor", level: "high" },
  { id: 2, employeeId: 4, employee: "Ece Arslan", dept: "Yazılım", document: "SGK Girişi", owner: null, dueDate: null, dueLabel: "-", status: "Eksik", level: "medium" },
];
const readiness = {
  totalCount: 12, completedCount: 7, missingCount: 3, dueSoonCount: 2, inReviewCount: 1, ownedCount: 9,
  documentComplianceScore: 82, readinessScore: 74, readinessRisk: "Orta",
  auditChecklist: [{ label: "Zorunlu evrak tamamlama", value: 78, level: "medium" }],
  recommendedActions: ["Eksik evraklar için sorumlu atayın."],
};

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: 5 },
  }));

beforeEach(() => {
  localStorage.clear();
  setRole("hr-admin");
  stubApi({
    "/api/compliance/documents": documents,
    "/api/compliance/readiness": readiness,
  });
});
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <CompliancePage />
      </ToastProvider>
    </AuthProvider>,
  );

// "İş Sözleşmesi" ve "4 gün" hem tabloda hem Yaklaşan Son Tarihler panelinde
// geçer → tekil getByText çoklu eşleşmeyle düşer; getAllByText/findAllByText kullanılır.
test("KPI'lar readiness'tan, tablo belgelerden dolar", async () => {
  renderShell();
  expect(await screen.findByText("Uyum, Evrak ve Denetim Risk Merkezi")).toBeInTheDocument();
  expect((await screen.findAllByText("İş Sözleşmesi")).length).toBeGreaterThan(0);
  expect(screen.getByText("82")).toBeInTheDocument();
  // "Orta" KPI'da, risk filtre seçeneğinde ve risk rozetinde geçer → getAllByText.
  expect(screen.getAllByText("Orta").length).toBeGreaterThan(0);
  expect(screen.getByText("Eksik evraklar için sorumlu atayın.")).toBeInTheDocument();
});

test("durum filtresi server-side sorgu atar", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  await userEvent.selectOptions(screen.getByLabelText("Durum filtresi"), "Eksik");
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/compliance/documents?status=Eksik");
    expect(hit).toBe(true);
  });
});

test("yaklaşan son tarihler paneli dueDate'li belgelerden türetilir", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  expect(screen.getByText("Yaklaşan Son Tarihler")).toBeInTheDocument();
  expect(screen.getAllByText("4 gün").length).toBeGreaterThan(1);
});
```

Run: `npm test -- --run src/features/compliance/CompliancePage.test.tsx` → FAIL.

- [x] **Step 2: `queries.ts` yaz**

```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type ComplianceDocumentDto = components["schemas"]["ComplianceDocumentDto"];
export type ComplianceReadinessDto = components["schemas"]["ComplianceReadinessDto"];
export type CreateComplianceDocumentCommand = components["schemas"]["CreateComplianceDocumentCommand"];

export type ComplianceFilters = { search: string; status: string; level: string };

export const COMPLIANCE_STATUSES = ["Eksik", "İncelemede", "Süresi Yaklaşıyor", "Tamamlandı"];
export const RISK_LEVELS = ["high", "medium", "low"];

const documentsPath = (filters: ComplianceFilters): string => {
  const params = new URLSearchParams();
  if (filters.status) params.set("status", filters.status);
  if (filters.level) params.set("level", filters.level);
  if (filters.search) params.set("search", filters.search);
  const query = params.toString();
  return query ? `/compliance/documents?${query}` : "/compliance/documents";
};

export const useComplianceDocuments = (filters: ComplianceFilters) =>
  useQuery({
    queryKey: ["compliance", "documents", filters],
    queryFn: () => apiFetch<ComplianceDocumentDto[]>(documentsPath(filters)),
  });

export const useComplianceReadiness = () =>
  useQuery({
    queryKey: ["compliance", "readiness"],
    queryFn: () => apiFetch<ComplianceReadinessDto>("/compliance/readiness"),
  });

export const useCreateComplianceDocument = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: CreateComplianceDocumentCommand) =>
      apiFetch<ComplianceDocumentDto>("/compliance/documents", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["compliance"] }),
  });
};

export const useUpdateComplianceDocument = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, documentName, dueDate, level }: {
      id: number; documentName: string; dueDate: string | null; level: string;
    }) =>
      apiFetch<ComplianceDocumentDto>(`/compliance/documents/${id}`, {
        method: "PUT",
        body: JSON.stringify({ documentName, dueDate, level }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["compliance"] }),
  });
};

export const useSetComplianceStatus = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, status }: { id: number; status: string }) =>
      apiFetch<ComplianceDocumentDto>(`/compliance/documents/${id}/status`, {
        method: "PATCH",
        body: JSON.stringify({ status }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["compliance"] }),
  });
};

export const useAssignComplianceOwner = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ownerName }: { id: number; ownerName: string }) =>
      apiFetch<ComplianceDocumentDto>(`/compliance/documents/${id}/owner`, {
        method: "PATCH",
        body: JSON.stringify({ ownerName }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["compliance"] }),
  });
};
```

- [x] **Step 3: `CompliancePage.tsx` yaz** (okuma görünümü; mutasyon butonları Task 6'da eklenir — `documentModal` state ve `isAdmin` bu görevde tanımlanır, UI Task 6)

```tsx
import { useEffect, useState } from "react";
import { PageError, PageLoading } from "../shared/PageState";
import { BackToRisk } from "../dashboard/BackToRisk";
import { getLevelText } from "../dashboard/format";
import {
  COMPLIANCE_STATUSES, RISK_LEVELS, useComplianceDocuments, useComplianceReadiness,
} from "./queries";

export const compliancePillClass = (status?: string | null): string =>
  status === "Tamamlandı" ? "approved" : status === "Eksik" ? "rejected" : "pending";

export function CompliancePage() {
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [levelFilter, setLevelFilter] = useState("");

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const documentsQ = useComplianceDocuments({ search: debouncedSearch, status: statusFilter, level: levelFilter });
  const readinessQ = useComplianceReadiness();

  if (documentsQ.isPending || readinessQ.isPending) return <PageLoading />;
  if (documentsQ.isError) return <PageError error={documentsQ.error} />;
  if (readinessQ.isError) return <PageError error={readinessQ.error} />;

  const documents = documentsQ.data;
  const readiness = readinessQ.data;
  const deadlines = documents
    .filter((doc) => doc.status !== "Tamamlandı" && doc.dueDate)
    .sort((a, b) => String(a.dueDate).localeCompare(String(b.dueDate)))
    .slice(0, 5);

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
        <div className="stat-box"><span className="sb-label">Evrak Uyum Skoru</span><strong className="sb-val">{readiness.documentComplianceScore ?? 0}<small>/100</small></strong></div>
        <div className="stat-box"><span className="sb-label">Eksik Evrak</span><strong className="sb-val text-red">{readiness.missingCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Süresi Yaklaşan</span><strong className="sb-val text-orange">{readiness.dueSoonCount ?? 0}</strong></div>
        <div className="stat-box"><span className="sb-label">Denetim Riski</span><strong className="sb-val text-orange">{readiness.readinessRisk}</strong></div>
      </div>

      <div className="toolbar-actions compliance-toolbar">
        <div className="search-wrap">
          <i aria-hidden="true" className="fa-solid fa-magnifying-glass" />
          <label className="sr-only" htmlFor="comp-search">Evrak veya personel ara</label>
          <input
            id="comp-search"
            type="text"
            placeholder="Evrak veya personel ara"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>
        <label className="sr-only" htmlFor="comp-status">Durum filtresi</label>
        <select id="comp-status" className="input-control" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}>
          <option value="">Tüm durumlar</option>
          {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
        </select>
        <label className="sr-only" htmlFor="comp-level">Risk filtresi</label>
        <select id="comp-level" className="input-control" value={levelFilter} onChange={(e) => setLevelFilter(e.target.value)}>
          <option value="">Tüm riskler</option>
          {RISK_LEVELS.map((level) => <option key={level} value={level}>{getLevelText(level)}</option>)}
        </select>
        {/* "Yeni Belge" butonu Task 6'da (yalnız hr-admin) */}
      </div>

      <div className="compliance-layout">
        <div className="table-container">
          <table className="detail-table data-table">
            <thead>
              <tr><th>Personel</th><th>Departman</th><th>Evrak</th><th>Sorumlu</th><th>Son Tarih</th><th>Durum</th><th>Risk</th></tr>
            </thead>
            <tbody>
              {documents.map((doc) => (
                <tr key={doc.id}>
                  <td><strong>{doc.employee}</strong></td>
                  <td>{doc.dept}</td>
                  <td>{doc.document}</td>
                  <td>{doc.owner || "-"}</td>
                  <td>{doc.dueLabel}</td>
                  <td><span className={`status-pill ${compliancePillClass(doc.status)}`}>{doc.status}</span></td>
                  <td><span className={`risk-badge ${doc.level ?? ""}`}>{getLevelText(doc.level)}</span></td>
                </tr>
              ))}
              {documents.length === 0 && (
                <tr><td colSpan={7}><p className="pending-desc">Filtreye uyan belge yok.</p></td></tr>
              )}
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
            {deadlines.map((doc) => (
              <div key={doc.id} className={`deadline-item ${doc.level ?? ""}`}>
                <div>
                  <strong>{doc.document}</strong>
                  <span>{doc.employee} · {doc.owner || "Sorumlu yok"}</span>
                </div>
                <em>{doc.dueLabel}</em>
              </div>
            ))}
            {deadlines.length === 0 && <p className="pending-desc">Yaklaşan son tarih yok.</p>}
          </div>
        </aside>
      </div>

      <div className="detail-support-grid">
        <section className="card">
          <div className="card-header-clean"><h4>Denetim Hazırlığı</h4><span className="status-pill pending">{readiness.readinessScore ?? 0}/100</span></div>
          <div className="audit-readiness-list">
            {(readiness.auditChecklist ?? []).map((item) => (
              <div key={item.label} className={`audit-readiness-item ${item.level ?? ""}`}>
                <div className="capacity-top"><span>{item.label}</span><strong>{item.value}%</strong></div>
                <div className="progress-bar"><div className="fill" style={{ width: `${item.value}%` }} /></div>
              </div>
            ))}
          </div>
        </section>
        <section className="card">
          <div className="card-header-clean"><h4>Önerilen Aksiyonlar</h4></div>
          <div className="signal-list">
            {(readiness.recommendedActions ?? []).map((action) => (
              <div key={action} className="signal-note action">
                <i aria-hidden="true" className="fa-solid fa-check" /><span>{action}</span>
              </div>
            ))}
          </div>
        </section>
      </div>

      {/* Belge modalı Task 6'da */}
    </div>
  );
}
```

Not: `useAuth`/`isAdmin`, `documentModal` state'i ve mutasyon UI'ları bu görevde **yok** — hepsini Task 6 ekler. Test dosyasının `AuthProvider` + `setRole` sarmalaması Task 6'daki rol testleri için şimdiden hazırdır (Task 5'te zararsızdır).

- [x] **Step 4: `routes.tsx` değiştir + eski sayfayı sil**

```tsx
// routes.tsx: ComplianceRiskPage importunu kaldır, yerine:
import { CompliancePage } from "./features/compliance/CompliancePage";
// pageFor:
  "compliance-risk": CompliancePage,
```

```bash
git rm frontend/src/features/dashboard/ComplianceRiskPage.tsx frontend/src/features/dashboard/ComplianceRiskPage.test.tsx
```

- [x] **Step 5: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS (eski `ComplianceRiskPage.test` silindiği için sayı düşer, kalanlar yeşil).

- [x] **Step 6: Commit**

```bash
git add frontend/src/features/compliance/ frontend/src/routes.tsx
git commit -m "feat(frontend): uyum sayfası — belge listesi, filtreler, denetim hazırlığı; rota compliance uçlarına taşındı"
```

---

### Task 6: Uyum mutasyonları — Yeni Belge, durum geçişi, sorumlu atama, düzenleme

**Files:**
- Create: `frontend/src/features/compliance/DocumentModal.tsx`
- Modify: `frontend/src/features/compliance/CompliancePage.tsx` (hr-admin eylem sütunu + "Yeni Belge" + modal bağlama)
- Test: `frontend/src/features/compliance/DocumentModal.test.tsx` (+ `CompliancePage.test.tsx`e 2 test eklenir)

**Interfaces:**
- Consumes: `useCreateComplianceDocument`, `useUpdateComplianceDocument`, `useAssignComplianceOwner`, `useSetComplianceStatus`, `useEmployees` (`../personnel/queries`), `useToast`, `COMPLIANCE_STATUSES`, `RISK_LEVELS`, `getLevelText`.
- Produces: `DocumentModal({ document, onClose }: { document: ComplianceDocumentDto | null; onClose: () => void })` — `document === null` → oluşturma modu (`POST`), dolu → düzenleme modu (`PUT` + sorumlu değiştiyse `PATCH owner`).

Davranış (hepsi yalnız hr-admin; manager salt-okur):
- Tabloda Durum hücresi hr-admin'de select olur (`PATCH status`; 409 → toast error).
- Tabloya "İşlem" sütunu eklenir: kalem butonu → düzenleme modalı.
- Toolbar'a "Yeni Belge" butonu (oluşturma modalı; 409 mükerrer → form-error).

- [x] **Step 1: Başarısız testleri yaz** — `DocumentModal.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { DocumentModal } from "./DocumentModal";

const employees = {
  items: [{ id: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım", status: "Aktif" }],
  totalCount: 1, page: 1, pageSize: 100,
};
const existing = {
  id: 1, employeeId: 3, employee: "Ahmet Yılmaz", dept: "Yazılım", document: "İş Sözleşmesi",
  owner: "Ayşe Demir", dueDate: "2026-07-20", dueLabel: "4 gün", status: "Eksik", level: "high",
};

beforeEach(() =>
  stubApi({
    "/api/employees": employees,
    "/api/compliance/documents": existing,
    "/api/compliance/documents/1": existing,
    "/api/compliance/documents/1/owner": existing,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderModal = (doc: typeof existing | null) =>
  renderPage(
    <ToastProvider>
      <DocumentModal document={doc} onClose={() => {}} />
    </ToastProvider>,
  );

test("oluşturma modu POST documents ucuna tam gövdeyle gider", async () => {
  renderModal(null);
  // Personel seçenekleri async yüklenir; select'e dokunmadan önce option beklenir.
  await screen.findByRole("option", { name: "Ahmet Yılmaz" });
  await userEvent.selectOptions(screen.getByLabelText("Personel"), "3");
  await userEvent.type(screen.getByLabelText("Belge adı"), "Sağlık Raporu");
  await userEvent.type(screen.getByLabelText("Sorumlu"), "Ayşe Demir");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    expect(JSON.parse(String(posted![1]?.body))).toMatchObject({
      employeeId: 3, documentName: "Sağlık Raporu", ownerName: "Ayşe Demir", level: "medium", status: "Eksik",
    });
  });
});

test("düzenleme modu PUT atar, sorumlu değişince owner PATCH'i de gider", async () => {
  renderModal(existing);
  const name = await screen.findByLabelText("Belge adı");
  expect(name).toHaveValue("İş Sözleşmesi");
  await userEvent.clear(screen.getByLabelText("Sorumlu"));
  await userEvent.type(screen.getByLabelText("Sorumlu"), "Ece Arslan");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents/1" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    expect(JSON.parse(String(put![1]?.body))).toMatchObject({ documentName: "İş Sözleşmesi", level: "high" });
    const owner = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents/1/owner" && i?.method === "PATCH",
    );
    expect(owner).toBeTruthy();
    expect(JSON.parse(String(owner![1]?.body))).toMatchObject({ ownerName: "Ece Arslan" });
  });
});
```

`CompliancePage.test.tsx`e eklenecek testler:

```tsx
test("hr-admin durum select'i PATCH status atar", async () => {
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  await userEvent.selectOptions(screen.getAllByLabelText("Belge durumu")[0], "Tamamlandı");
  await waitFor(() => {
    const patched = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/compliance/documents/1/status" && i?.method === "PATCH",
    );
    expect(patched).toBeTruthy();
    expect(JSON.parse(String(patched![1]?.body))).toMatchObject({ status: "Tamamlandı" });
  });
});

test("manager salt-okur: Yeni Belge ve durum select'i yok", async () => {
  setRole("manager");
  renderShell();
  await screen.findAllByText("İş Sözleşmesi");
  expect(screen.queryByRole("button", { name: /Yeni Belge/ })).not.toBeInTheDocument();
  expect(screen.queryAllByLabelText("Belge durumu")).toHaveLength(0);
});
```

(`beforeEach` stub'ına `"/api/compliance/documents/1/status": documents[0]` eklenir.)

Run: `npm test -- --run src/features/compliance/` → FAIL.

- [x] **Step 2: `DocumentModal.tsx` yaz**

```tsx
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { useEmployees } from "../personnel/queries";
import { getLevelText } from "../dashboard/format";
import {
  COMPLIANCE_STATUSES, RISK_LEVELS,
  useAssignComplianceOwner, useCreateComplianceDocument, useUpdateComplianceDocument,
  type ComplianceDocumentDto,
} from "./queries";

export function DocumentModal({ document: doc, onClose }: {
  document: ComplianceDocumentDto | null; onClose: () => void;
}) {
  const { showToast } = useToast();
  const isEdit = doc !== null;
  const createDocument = useCreateComplianceDocument();
  const updateDocument = useUpdateComplianceDocument();
  const assignOwner = useAssignComplianceOwner();
  const employeesQ = useEmployees({ search: "", departmentId: "", status: "" });
  const [form, setForm] = useState({
    employeeId: doc ? String(doc.employeeId ?? "") : "",
    documentName: doc?.document ?? "",
    ownerName: doc?.owner ?? "",
    dueDate: doc?.dueDate ?? "",
    status: doc?.status ?? "Eksik",
    level: doc?.level ?? "medium",
  });
  const [error, setError] = useState<string | null>(null);

  const set = (key: keyof typeof form) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) =>
      setForm((f) => ({ ...f, [key]: e.target.value }));

  const submit = async () => {
    setError(null);
    if (!form.documentName.trim() || (!isEdit && !form.employeeId)) {
      setError("Personel ve belge adı zorunludur.");
      return;
    }
    try {
      if (isEdit) {
        await updateDocument.mutateAsync({
          id: doc.id!,
          documentName: form.documentName.trim(),
          dueDate: form.dueDate || null,
          level: form.level ?? "medium",
        });
        const newOwner = form.ownerName.trim();
        if (newOwner && newOwner !== (doc.owner ?? "")) {
          await assignOwner.mutateAsync({ id: doc.id!, ownerName: newOwner });
        }
        showToast("Belge güncellendi.", "success");
      } else {
        await createDocument.mutateAsync({
          employeeId: Number(form.employeeId),
          documentName: form.documentName.trim(),
          ownerName: form.ownerName.trim() || null,
          dueDate: form.dueDate || null,
          status: form.status,
          level: form.level,
        });
        showToast("Uyum belgesi oluşturuldu.", "success");
      }
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Belge kaydedilemedi.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>{isEdit ? `Belgeyi Düzenle: ${doc.document}` : "Yeni Uyum Belgesi"}</h3>
            <p>{isEdit ? `${doc.employee} · ${doc.dept}` : "Personel için takip edilecek belge kaydı açın."}</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Belge penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            {!isEdit && (
              <div className="input-group">
                <label className="input-label" htmlFor="doc-employee">Personel</label>
                <select id="doc-employee" className="input-control" value={form.employeeId} onChange={set("employeeId")}>
                  <option value="">Seçin</option>
                  {(employeesQ.data?.items ?? []).map((employee) => (
                    <option key={employee.id} value={employee.id}>{employee.name}</option>
                  ))}
                </select>
              </div>
            )}
            <div className="input-group">
              <label className="input-label" htmlFor="doc-name">Belge adı</label>
              <input id="doc-name" className="input-control" value={form.documentName} onChange={set("documentName")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="doc-owner">Sorumlu</label>
              <input id="doc-owner" className="input-control" value={form.ownerName} onChange={set("ownerName")} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="doc-due">Son tarih</label>
              <input id="doc-due" type="date" className="input-control" value={form.dueDate ?? ""} onChange={set("dueDate")} />
            </div>
            {!isEdit && (
              <div className="input-group">
                <label className="input-label" htmlFor="doc-status">Durum</label>
                <select id="doc-status" className="input-control" value={form.status ?? "Eksik"} onChange={set("status")}>
                  {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </div>
            )}
            <div className="input-group">
              <label className="input-label" htmlFor="doc-level">Risk seviyesi</label>
              <select id="doc-level" className="input-control" value={form.level ?? "medium"} onChange={set("level")}>
                {RISK_LEVELS.map((level) => <option key={level} value={level}>{getLevelText(level)}</option>)}
              </select>
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button
            className="btn btn-primary"
            onClick={submit}
            disabled={createDocument.isPending || updateDocument.isPending || assignOwner.isPending}
          >
            <i aria-hidden="true" className="fa-solid fa-check" /> Kaydet
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [x] **Step 3: `CompliancePage.tsx`'e mutasyon UI'larını ekle**

İçe aktarımlar + state (Task 5 notu gereği bu görevde eklenir):

```tsx
import { ApiError } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { DocumentModal } from "./DocumentModal";
// queries import satırına eklenir: useSetComplianceStatus, type ComplianceDocumentDto
// component başına:
const { user } = useAuth();
const isAdmin = user?.role === "hr-admin";
const { showToast } = useToast();
const setDocumentStatus = useSetComplianceStatus();
const [documentModal, setDocumentModal] = useState<
  { mode: "create" } | { mode: "edit"; document: ComplianceDocumentDto } | null
>(null);

const changeStatus = async (doc: ComplianceDocumentDto, status: string) => {
  try {
    await setDocumentStatus.mutateAsync({ id: doc.id!, status });
    showToast(`${doc.document} durumu "${status}" yapıldı.`, "success");
  } catch (e) {
    showToast(e instanceof ApiError ? e.message : "Durum güncellenemedi.", "error");
  }
};
```

Toolbar sonuna (risk filtresinden sonra):

```tsx
{isAdmin && (
  <button className="btn btn-primary" onClick={() => setDocumentModal({ mode: "create" })}>
    <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Belge
  </button>
)}
```

Tablo başlığına `Risk`ten sonra: `{isAdmin && <th>İşlem</th>}`. Satırda Durum hücresi:

```tsx
<td>
  {isAdmin ? (
    <>
      <label className="sr-only" htmlFor={`doc-status-${doc.id}`}>Belge durumu</label>
      <select
        id={`doc-status-${doc.id}`}
        className="input-control input-sm"
        value={doc.status ?? "Eksik"}
        onChange={(e) => changeStatus(doc, e.target.value)}
      >
        {COMPLIANCE_STATUSES.map((status) => <option key={status} value={status}>{status}</option>)}
      </select>
    </>
  ) : (
    <span className={`status-pill ${compliancePillClass(doc.status)}`}>{doc.status}</span>
  )}
</td>
```

Satır sonuna:

```tsx
{isAdmin && (
  <td>
    <button
      className="btn-icon-sm"
      title="Düzenle"
      aria-label={`${doc.document} belgesini düzenle`}
      onClick={() => setDocumentModal({ mode: "edit", document: doc })}
    >
      <i aria-hidden="true" className="fa-solid fa-pen" />
    </button>
  </td>
)}
```

Boş satır `colSpan` değeri `isAdmin ? 8 : 7` yapılır. Sayfa sonuna (kapanış `</div>` öncesine, "Belge modalı Task 6'da" yorumunun yerine) eklenir:

```tsx
{documentModal && (
  <DocumentModal
    document={documentModal.mode === "edit" ? documentModal.document : null}
    onClose={() => setDocumentModal(null)}
  />
)}
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS. `npm run build` → hatasız.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/compliance/
git commit -m "feat(frontend): uyum belge mutasyonları — oluşturma, durum geçişi, sorumlu atama, düzenleme"
```

---

### Task 7: Uçtan uca duman testi + parite + günlük + kapanış

**Files:**
- Modify: `docs/gelistirme-gunlugu.md`; gerekirse küçük düzeltmeler.

- [x] **Step 1: Backend + frontend'i başlat** (önceki dilimlerle aynı: `cd backend && dotnet run --project src/Ekipzen.API --launch-profile http` + `cd frontend && npm run dev`)

- [x] **Step 2: Duman testi** (Playwright, scratchpad script'i; giriş: `ik@hrmaster.local` / `demo123`)

1. hr-admin → `#/recruitment`: boş durum ("Henüz aday yok") + "Yeni Aday" → form doldur (ad, pozisyon, puan, yetenekler) → kaydet → listede görünür, otomatik seçilir, detay dolar.
2. İkinci aday ekle → arama kutusuna ad yaz → 300ms sonra liste daralır; "Yeni" filtre sekmesi → yalnız yeni adaylar.
3. Detayda not ekle (tür seç + metin) → not listede görünür; Geçmiş sekmesinde "notu eklendi" kaydı.
4. Pipeline select: Yeni → Mülakat → Teklif; her geçişte durum etiketi güncellenir; Geçmiş'e kayıt düşer.
5. "İşe Al" → departman seç → Onayla → toast "personel kaydı oluşturuldu"; `#/personnel`de yeni personel görünür; adayda select + İşe Al pasif; Red durumundaki adayda İşe Al → 409 mesajı modalda.
6. manager/employee → `#/recruitment` → yetki kilidi ekranı.
7. hr-admin → `#/risk/compliance`: KPI'lar + seed'li belge tablosu; durum filtresi `Eksik` → tablo daralır; arama çalışır.
8. Satır durum select'i `Tamamlandı` → risk `Düşük`e döner (level=low kuralı); aynı durumu tekrar seçme → 409 toast'ı.
9. "Yeni Belge" → personel + ad + son tarih → kaydet → tabloda `dueLabel` ("N gün"); aynı ada ikinci belge → 409 form hatası.
10. Kalem → düzenleme modalı: ad/tarih/risk değiştir + sorumlu değiştir → kaydet → tablo güncellenir (PUT + owner PATCH).
11. manager (`ece.arslan@hrmaster.local`) → `#/risk/compliance`: tablo salt-okur (select/Yeni Belge/İşlem yok), yalnız ekip kayıtları.

- [x] **Step 3: Görsel parite** — eski/yeni `#/recruitment` ve `#/risk/compliance` yan yana. Bilinçli farklar: "Yeni Aday"/"Yeni Belge" butonları, server-side arama/filtreler, uyum tablosunda durum select'i + İşlem sütunu (hr-admin), pipeline durum select'i, gerçek veri (seed'siz aday havuzu). Diğer farklar DOM/class düzeltmesiyle giderilir.

- [x] **Step 4: Günlük + kapanış commit** — "Şu an neredeyiz" → Dilim 7 (Aksiyonlar + Arama + Ayarlar); Dilim 6 kaydı; plan kutuları işaretlenir.

```bash
git add frontend/ docs/
git commit -m "test(frontend): dilim 6 duman testi, parite kontrolü ve günlük güncellemesi"
```

---

## Yürütme notu (paralellik)

Task 1 ve 2 sıralı ön koşuldur (Task 3/4 RecruitmentPage'e bağlanır). Task 5+6 (Uyum) Task 1–4'ten bağımsızdır; ancak Task 2 ve 5 aynı `routes.tsx` dosyasına dokunur — paralel yürütülecekse rota adımları ana oturumda yapılır.

## Sonraki dilimler

Dilim 7 (Aksiyonlar + Global Arama + Ayarlar) planı bu dilim main'e merge edildikten sonra yazılır. Dilim 8 kapanış/temizlik.
