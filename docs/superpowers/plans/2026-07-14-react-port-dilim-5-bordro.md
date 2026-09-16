# React Port — Dilim 5: Bordro — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bordro ekranını (`/payroll` ailesi) gerçek bordro API'sine bağla: hr-admin için dönem yaşam döngüsü (oluştur → girdi düzenle → kontrol → satır onayı → dönem gönderimi), tekil hesaplama (server-side preview) ve yıla göre versiyonlu bordro ayarları; çalışan için "Bordrolarım" pusula listesi + PDF indirme.

**Architecture:** `src/features/payroll/` altında tek `PayrollPage` kabuğu (3 rota aynı kabuğu farklı `tab` prop'uyla render eder) + sekme bileşenleri (`PeriodTab`, `CalculatorTab`, `SettingsTab`, `MyPayslipsView`) + satır detay paneli (`PayrollDetailPanel`) + `queries.ts`/`format.ts`. Employee rolü kabuk yerine doğrudan `MyPayslipsView` görür (backend policy: periods/preview/settings yalnız hr-admin).

**Tech Stack:** Dilim 1–4 stack'i (yeni bağımlılık yok). PDF indirme dilim 3'teki `apiDownload` ile.

## Global Constraints

- Dilim 1–4 planlarındaki tüm kısıtlar geçerli (CSS'e dokunma, aynı class/DOM, Türkçe metinler birebir, her görev sonunda `cd frontend && npm test -- --run` yeşil).
- Değer kümeleri backend ile birebir: dönem durumu `draft|control|approved|closed`; satır onay durumu **Türkçe** `Ön Hesap|Kontrol|Onaya Hazır|Eksik Veri|Onaylandı`; pill sınıfı eski `payrollStatusClass` haritası (`Onaylandı→approved, Onaya Hazır→info, Kontrol→pending, Eksik Veri→rejected, Ön Hesap→info`, bilinmeyen→pending).
- Para/sayı formatı eski payroll.js ile birebir: `Intl.NumberFormat("tr-TR", {style:"currency", currency:"TRY", maximumFractionDigits:0})` (yuvarlanmış) ve `{minimumFractionDigits:2, maximumFractionDigits:2}`. Testlerde beklenen değerler ICU farklarına takılmamak için `format.ts` yardımcılarıyla üretilir.
- Testlerde gerçek ağ yok (`stubApi`); tipler `schema.d.ts`'ten.

### Veri/davranış eşleme kararları (mock ↔ backend farkları)

| Eski mock davranışı | Karar |
|---|---|
| Tek sabit dönem ("Nisan 2026", localStorage yok) | Dönem seçici: `GET /payroll/periods` (yeni→eski sıralı, ilki otomatik seçilir) + "Yeni Dönem" modalı (`POST /payroll/periods {year, month}`; 409'da ApiError mesajı). Hiç dönem yoksa boş durum kartı |
| Çalışan satırları sabit 5 kişi, maaşlar dolu | Dönem oluşturulunca backend aktif personel için satır üretir (`grossSalary=0`, fazla mesai puantajdan, IBAN profilden) → "Detay" paneline **girdi düzenleme formu** eklenir (`PUT /payroll/periods/{id}/rows/{rowId}`, gövde `PayrollRowInputModel`) |
| Ayarlar localStorage'da | `GET/PUT /payroll/settings?year` (yıla göre versiyonlu). "Varsayılana dön" backend'de reset ucu yok → form son kaydedilmiş (sorgudan gelen) değerlere döner. `minWageGross = sgkBaseMin` gönderilir (mock'taki eşlemenin aynısı); `taxBrackets` gönderilmez (null → backend değiştirmez) |
| Bordro hesabı client-side | Tablo satırları backend'den hesaplı gelir; tekil hesap `POST /payroll/preview` (300ms debounce, personel arama deseni) |
| `controls[]` içinde `detail` + `icon` | Backend yalnız `label/value/level` döner → `detail`/`icon` label'a göre statik haritadan (eski metinler birebir) |
| `steps[]` sabit | Dönem durumu + satır sayımlarından türetilir (deterministik; kurallar `derivePayrollSteps`'te) |
| Header "Onaya gönder" → toast | `POST /payroll/periods/{id}/submit`; 409 (onaysız satır) mesajı error toast'ta |
| "Kontrol edildi" → toast | `POST /payroll/periods/{id}/check`; dönem `control`'a geçer, satırlar Eksik Veri/Onaya Hazır olur |
| Detay panelindeki "Onaya gönder" → toast | `POST .../rows/{rowId}/approve`; 409 mesajı panelde form-error. Onaylı satırda "Pusula İndir" (PDF, `apiDownload`) butonu görünür |
| Çalışan da demo dönem tablosu görürdü | Backend policy gereği çalışan **"Bordrolarım"** görünümü alır: `GET /payroll/my` listesi + kendi pusula PDF'i. hr-admin'e bağlı personel kaydı yoksa `/payroll/my` 403 döner (İK yönetim görünümünü gördüğünden bu ucu çağırmaz) |
| Sekmeler `switchPayrollTab` ile DOM class değiştirir | 3 rota (`payroll`, `payroll-calculator`, `payroll-settings`) aynı `PayrollPage`'i farklı `tab` prop'uyla render eder; sekme butonu `navigate()` çağırır |
| Tekil hesap personel seçimi sabit listeden | En yeni dönemin satırlarından dolar (`usePayrollPeriod`); satır seçilince brüt/prim/yol/yemek/yan hak/önceki matrah forma kopyalanır, gün=`defaultWorkedDays`, fazla mesai=3 saat (eski `fillPayrollScenarioFromEmployee` davranışı). Dönem yoksa yalnız serbest giriş |
| Dönem Durumu KPI metni "Kontrol" | `PERIOD_STATUS_TEXT`: `draft→Taslak, control→Kontrol, approved→Onaylandı, closed→Kapandı` |

---

### Task 1: Bordro format yardımcıları + query katmanı

**Files:**
- Create: `frontend/src/features/payroll/format.ts`, `frontend/src/features/payroll/queries.ts`
- Test: `frontend/src/features/payroll/format.test.ts`

**Interfaces:**
- Produces (`format.ts`):
  - `formatPayrollMoney(v?: number): string` — TRY, 0 kesir, `Math.round`
  - `formatPayrollNumber(v?: number): string` — 2 kesir
  - `payrollStatusClass(status?: string | null): string` — eski harita
  - `PERIOD_STATUS_TEXT: Record<string, string>`
  - `CONTROL_META: Record<string, { detail: string; icon: string }>` — label→(detail, icon); eski `controls[]` metinleri
  - `PayrollStepView = { id: string; label: string; status: "done" | "active" | "pending"; meta: string }`
  - `derivePayrollSteps(status: string | undefined, rows: PayrollRowDto[], controls: PayrollControlDto[]): PayrollStepView[]`
- Produces (`queries.ts`): tipler `PayrollPeriodListItemDto, PayrollPeriodDetailDto, PayrollRowDto, PayrollControlDto, PayrollRowInputModel, PayrollCalculation, PreviewPayrollCommand, PayrollSettingsDto, UpdatePayrollSettingsCommand, MyPayslipDto` + hook'lar (aşağıda).

- [x] **Step 1: Başarısız testleri yaz** — `format.test.ts`:

```ts
import { describe, expect, test } from "vitest";
import {
  CONTROL_META, PERIOD_STATUS_TEXT, derivePayrollSteps, formatPayrollMoney,
  formatPayrollNumber, payrollStatusClass,
} from "./format";

test("para ve sayı formatı eski payroll.js ile aynı", () => {
  // Birebir Intl çıktısı ortama göre değişebilir; davranışı sabitle:
  expect(formatPayrollMoney(118000.4)).toBe(
    new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", maximumFractionDigits: 0 }).format(118000),
  );
  expect(formatPayrollMoney(undefined)).toBe(
    new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY", maximumFractionDigits: 0 }).format(0),
  );
  expect(formatPayrollNumber(1.5)).toBe(
    new Intl.NumberFormat("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(1.5),
  );
});

test("durum pill sınıfları eski haritayla birebir", () => {
  expect(payrollStatusClass("Onaylandı")).toBe("approved");
  expect(payrollStatusClass("Onaya Hazır")).toBe("info");
  expect(payrollStatusClass("Kontrol")).toBe("pending");
  expect(payrollStatusClass("Eksik Veri")).toBe("rejected");
  expect(payrollStatusClass("Ön Hesap")).toBe("info");
  expect(payrollStatusClass("bilinmeyen")).toBe("pending");
  expect(PERIOD_STATUS_TEXT.draft).toBe("Taslak");
  expect(CONTROL_META["Eksik IBAN"].icon).toBe("fa-building-columns");
});

describe("derivePayrollSteps", () => {
  const rows = (approved: number, total: number) =>
    Array.from({ length: total }, (_, i) => ({ approvalStatus: i < approved ? "Onaylandı" : "Kontrol" })) as never[];
  const controls = [{ label: "Eksik IBAN", value: 2, level: "high" }] as never[];

  test("taslak dönemde kontrol adımı aktiftir", () => {
    const steps = derivePayrollSteps("draft", rows(0, 3), controls);
    expect(steps.map((s) => s.status)).toEqual(["done", "done", "active", "pending", "pending"]);
    expect(steps[2].meta).toBe("2 uyarı inceleniyor");
    expect(steps[3].meta).toBe("3 kayıt bekliyor");
  });

  test("kontrol döneminde onay adımı aktiftir", () => {
    const steps = derivePayrollSteps("control", rows(1, 3), controls);
    expect(steps[2].status).toBe("done");
    expect(steps[3].status).toBe("active");
  });

  test("onaylanan dönemde pusula adımı tamamdır", () => {
    const steps = derivePayrollSteps("approved", rows(3, 3), controls);
    expect(steps[3].status).toBe("done");
    expect(steps[4]).toMatchObject({ status: "done", meta: "Pusulalar hazır" });
  });
});
```

Run: `npm test -- --run src/features/payroll` → FAIL.

- [x] **Step 2: `format.ts` yaz**

```ts
import type { PayrollControlDto, PayrollRowDto } from "./queries";

const currency = new Intl.NumberFormat("tr-TR", {
  style: "currency",
  currency: "TRY",
  maximumFractionDigits: 0,
});

const number = new Intl.NumberFormat("tr-TR", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export const formatPayrollMoney = (value?: number): string => currency.format(Math.round(value ?? 0));
export const formatPayrollNumber = (value?: number): string => number.format(value ?? 0);

export const payrollStatusClass = (status?: string | null): string =>
  ({
    "Onaylandı": "approved",
    "Onaya Hazır": "info",
    "Kontrol": "pending",
    "Eksik Veri": "rejected",
    "Ön Hesap": "info",
  })[status ?? ""] ?? "pending";

export const PERIOD_STATUS_TEXT: Record<string, string> = {
  draft: "Taslak",
  control: "Kontrol",
  approved: "Onaylandı",
  closed: "Kapandı",
};

/** Kontrol kartı detay/icon'ları — backend yalnız label/value/level döner (eski payroll.js metinleri). */
export const CONTROL_META: Record<string, { detail: string; icon: string }> = {
  "Eksik Puantaj": { detail: "Fazla mesai ve çalışılan gün kapanışı bekliyor.", icon: "fa-clock" },
  "Eksik IBAN": { detail: "Ödeme listesine girmeden tamamlanmalı.", icon: "fa-building-columns" },
  "SGK Matrah Uyarısı": { detail: "Alt/üst sınır ve ücret dışı ödeme etkisi izleniyor.", icon: "fa-shield-halved" },
  "Vergi Matrahı Uyarısı": { detail: "Kümülatif gelir vergisi dilimi değişen kayıtlar.", icon: "fa-scale-balanced" },
  "Onay Bekleyen": { detail: "Kontrol veya onay aşamasında bekleyen bordrolar.", icon: "fa-file-signature" },
};

export type PayrollStepView = {
  id: string;
  label: string;
  status: "done" | "active" | "pending";
  meta: string;
};

/** Eski steps[] sabitinin dönem durumu + satır sayımlarından türetilmiş hali. */
export const derivePayrollSteps = (
  status: string | undefined,
  rows: PayrollRowDto[],
  controls: PayrollControlDto[],
): PayrollStepView[] => {
  const approved = rows.filter((r) => r.approvalStatus === "Onaylandı").length;
  const waiting = rows.length - approved;
  const totalWarnings = controls.reduce((total, c) => total + (c.value ?? 0), 0);
  const periodDone = status === "approved" || status === "closed";

  return [
    { id: "prep", label: "Hazırlık", status: "done", meta: "Puantaj ve personel verisi" },
    { id: "calc", label: "Hesaplama", status: "done", meta: "Ön hesap oluşturuldu" },
    {
      id: "check", label: "Kontrol",
      status: status === "draft" ? "active" : "done",
      meta: `${totalWarnings} uyarı inceleniyor`,
    },
    {
      id: "approve", label: "Onay",
      status: waiting === 0 && rows.length > 0 ? "done" : status === "control" ? "active" : "pending",
      meta: `${waiting} kayıt bekliyor`,
    },
    {
      id: "slip", label: "Pusula",
      status: periodDone ? "done" : "pending",
      meta: periodDone ? "Pusulalar hazır" : "Onay sonrası yayın",
    },
  ];
};
```

- [x] **Step 3: `queries.ts` yaz**

```ts
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../api/client";
import type { components } from "../../api/schema";

export type PayrollPeriodListItemDto = components["schemas"]["PayrollPeriodListItemDto"];
export type PayrollPeriodDetailDto = components["schemas"]["PayrollPeriodDetailDto"];
export type PayrollRowDto = components["schemas"]["PayrollRowDto"];
export type PayrollControlDto = components["schemas"]["PayrollControlDto"];
export type PayrollRowInputModel = components["schemas"]["PayrollRowInputModel"];
export type PayrollCalculation = components["schemas"]["PayrollCalculation"];
export type PreviewPayrollCommand = components["schemas"]["PreviewPayrollCommand"];
export type PayrollSettingsDto = components["schemas"]["PayrollSettingsDto"];
export type UpdatePayrollSettingsCommand = components["schemas"]["UpdatePayrollSettingsCommand"];
export type MyPayslipDto = components["schemas"]["MyPayslipDto"];

export const usePayrollPeriods = (enabled: boolean) =>
  useQuery({
    queryKey: ["payroll", "periods"],
    queryFn: () => apiFetch<PayrollPeriodListItemDto[]>("/payroll/periods"),
    enabled,
  });

export const usePayrollPeriod = (id: number | null) =>
  useQuery({
    queryKey: ["payroll", "period", id],
    queryFn: () => apiFetch<PayrollPeriodDetailDto>(`/payroll/periods/${id}`),
    enabled: id !== null,
  });

export const useCreatePayrollPeriod = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: { year: number; month: number }) =>
      apiFetch<PayrollPeriodDetailDto>("/payroll/periods", {
        method: "POST",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll"] }),
  });
};

export const useUpdatePayrollRow = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ periodId, rowId, model }: { periodId: number; rowId: number; model: PayrollRowInputModel }) =>
      apiFetch<PayrollRowDto>(`/payroll/periods/${periodId}/rows/${rowId}`, {
        method: "PUT",
        body: JSON.stringify(model),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll"] }),
  });
};

export const useRunPayrollCheck = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (periodId: number) =>
      apiFetch<PayrollPeriodDetailDto>(`/payroll/periods/${periodId}/check`, {
        method: "POST",
        body: JSON.stringify({}),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll"] }),
  });
};

export const useApprovePayrollRow = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ periodId, rowId }: { periodId: number; rowId: number }) =>
      apiFetch<PayrollRowDto>(`/payroll/periods/${periodId}/rows/${rowId}/approve`, {
        method: "POST",
        body: JSON.stringify({}),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll"] }),
  });
};

export const useSubmitPayrollPeriod = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (periodId: number) =>
      apiFetch<PayrollPeriodDetailDto>(`/payroll/periods/${periodId}/submit`, {
        method: "POST",
        body: JSON.stringify({}),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll"] }),
  });
};

export const usePayrollPreview = () =>
  useMutation({
    mutationFn: (command: PreviewPayrollCommand) =>
      apiFetch<PayrollCalculation>("/payroll/preview", {
        method: "POST",
        body: JSON.stringify(command),
      }),
  });

export const usePayrollSettings = (enabled: boolean) =>
  useQuery({
    queryKey: ["payroll", "settings"],
    queryFn: () => apiFetch<PayrollSettingsDto>("/payroll/settings"),
    enabled,
  });

export const useUpdatePayrollSettings = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (command: UpdatePayrollSettingsCommand) =>
      apiFetch<PayrollSettingsDto>("/payroll/settings", {
        method: "PUT",
        body: JSON.stringify(command),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["payroll", "settings"] }),
  });
};

export const useMyPayslips = () =>
  useQuery({ queryKey: ["payroll", "my"], queryFn: () => apiFetch<MyPayslipDto[]>("/payroll/my") });
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/payroll/
git commit -m "feat(frontend): bordro format yardımcıları ve query katmanı"
```

---

### Task 2: PayrollPage kabuğu + Bordrolarım (employee) + rota kaydı

**Files:**
- Create: `frontend/src/features/payroll/PayrollPage.tsx`, `frontend/src/features/payroll/MyPayslipsView.tsx`
- Modify: `frontend/src/routes.tsx` (pageFor: payroll, payroll-calculator, payroll-settings)
- Test: `frontend/src/features/payroll/MyPayslipsView.test.tsx`, `frontend/src/features/payroll/PayrollPage.test.tsx`

**Interfaces:**
- Consumes: Task 1 hook'ları, `useAuth`, `useToast`, `apiDownload`, `PageLoading/PageError`.
- Produces:
  - `PayrollPage({ tab }: { tab: "period" | "calculator" | "settings" })` — hr-admin kabuğu; employee'de `MyPayslipsView` render eder. Sekme içerikleri bu görevde `null` placeholder (Task 3/5/6 doldurur); dönem seçici + "Yeni Dönem" modalı + "Onaya gönder" bu görevde kurulur.
  - `PayrollPeriodPage()`, `PayrollCalculatorPage()`, `PayrollSettingsPage()` — rota sarmalayıcıları.
  - `MyPayslipsView()` — çalışan pusula listesi.

- [x] **Step 1: Başarısız testleri yaz**

`MyPayslipsView.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { formatPayrollMoney } from "./format";
import { MyPayslipsView } from "./MyPayslipsView";

const slips = [
  { periodId: 1, rowId: 11, periodName: "Haziran 2026", grossEarnings: 118000, totalDeductions: 38000, netPay: 80000, approvalStatus: "Onaylandı" },
  { periodId: 2, rowId: 22, periodName: "Mayıs 2026", grossEarnings: 110000, totalDeductions: 35000, netPay: 75000, approvalStatus: "Onaylandı" },
];

beforeEach(() => stubApi({ "/api/payroll/my": slips, "/api/payroll/periods/1/rows/11/payslip": {} }));
afterEach(() => vi.unstubAllGlobals());

const renderView = () =>
  renderPage(
    <ToastProvider>
      <MyPayslipsView />
    </ToastProvider>,
  );

test("pusula listesi backend'den dolar", async () => {
  renderView();
  expect(await screen.findByText("Haziran 2026")).toBeInTheDocument();
  expect(screen.getAllByText(formatPayrollMoney(80000)).length).toBeGreaterThan(0);
  expect(screen.getAllByText("Onaylandı")).toHaveLength(2);
});

test("pusula PDF'i indirme ucuna gider", async () => {
  vi.stubGlobal("URL", { ...URL, createObjectURL: vi.fn(() => "blob:x"), revokeObjectURL: vi.fn() });
  renderView();
  await screen.findByText("Haziran 2026");
  await userEvent.click(screen.getAllByTitle("Pusulayı indir")[0]);
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/1/rows/11/payslip");
    expect(hit).toBe(true);
  });
});

test("boş listede bilgi metni görünür", async () => {
  stubApi({ "/api/payroll/my": [] });
  renderView();
  expect(await screen.findByText("Henüz bordro kaydınız yok.")).toBeInTheDocument();
});
```

`PayrollPage.test.tsx`:

```tsx
import { screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { AuthProvider } from "../../auth/AuthContext";
import { ToastProvider } from "../../layout/ToastProvider";
import { SESSION_KEY } from "../../api/session";
import { PayrollPage } from "./PayrollPage";

const periods = [
  { id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft", employeeCount: 4 },
  { id: 2, name: "Haziran 2026", year: 2026, month: 6, status: "approved", employeeCount: 4 },
];

const setRole = (role: string) =>
  localStorage.setItem(SESSION_KEY, JSON.stringify({
    token: "T", refreshToken: "R",
    user: { id: "u", name: "X", email: "x@x", role, roleLabel: "X", initials: "XX", employeeId: 5 },
  }));

beforeEach(() => {
  localStorage.clear();
  stubApi({ "/api/payroll/periods": periods, "/api/payroll/my": [] });
});
afterEach(() => vi.unstubAllGlobals());

const renderShell = () =>
  renderPage(
    <AuthProvider>
      <ToastProvider>
        <PayrollPage tab="period" />
      </ToastProvider>
    </AuthProvider>,
  );

test("hr-admin sekmeleri ve dönem seçicisini görür, ilk dönem seçilir", async () => {
  setRole("hr-admin");
  renderShell();
  expect(await screen.findByText("Dönem Bordrosu")).toBeInTheDocument();
  expect(screen.getByText("Tekil Hesaplama")).toBeInTheDocument();
  expect(screen.getByText("Bordro Ayarları")).toBeInTheDocument();
  expect(screen.getByLabelText("Bordro dönemi")).toHaveValue("3");
});

test("çalışan sekme kabuğu yerine Bordrolarım görünümünü alır", async () => {
  setRole("employee");
  renderShell();
  expect(await screen.findByText("Henüz bordro kaydınız yok.")).toBeInTheDocument();
  expect(screen.queryByText("Dönem Bordrosu")).not.toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/payroll` → FAIL.

- [x] **Step 2: `MyPayslipsView.tsx` yaz**

```tsx
import { ApiError, apiDownload } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { formatPayrollMoney, payrollStatusClass } from "./format";
import { useMyPayslips } from "./queries";

export function MyPayslipsView() {
  const { showToast } = useToast();
  const slipsQ = useMyPayslips();

  if (slipsQ.isPending) return <PageLoading />;
  if (slipsQ.isError) return <PageError error={slipsQ.error} />;

  const slips = slipsQ.data;

  const handleDownload = async (periodId: number | undefined, rowId: number | undefined, periodName: string) => {
    if (periodId === undefined || rowId === undefined) return;
    try {
      const { blob, fileName } = await apiDownload(`/payroll/periods/${periodId}/rows/${rowId}/payslip`);
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = fileName ?? `bordro-${periodName}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
      showToast("Bordro pusulası indirildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Pusula indirilemedi.", "error");
    }
  };

  return (
    <div id="payroll-screen">
      <div className="page-header">
        <div>
          <h2>Bordro</h2>
          <p>Onaylanan bordro pusulalarınızı görüntüleyin ve indirin.</p>
        </div>
      </div>

      <section className="table-container payroll-table-card">
        <div className="payroll-table-header">
          <div>
            <h4>Bordrolarım</h4>
            <p className="text-muted">Dönem bazında brüt, kesinti ve net ödeme özetiniz.</p>
          </div>
        </div>
        <table className="data-table payroll-table">
          <thead>
            <tr>
              <th>Dönem</th><th>Brüt</th><th>Kesinti</th><th>Net</th><th>Durum</th><th></th>
            </tr>
          </thead>
          <tbody>
            {slips.map((slip) => (
              <tr key={slip.rowId}>
                <td><strong>{slip.periodName}</strong></td>
                <td>{formatPayrollMoney(slip.grossEarnings)}</td>
                <td>{formatPayrollMoney(slip.totalDeductions)}</td>
                <td><strong>{formatPayrollMoney(slip.netPay)}</strong></td>
                <td>
                  <span className={`status-pill ${payrollStatusClass(slip.approvalStatus)}`}>
                    {slip.approvalStatus}
                  </span>
                </td>
                <td className="text-right">
                  <button
                    className="btn btn-secondary btn-sm"
                    title="Pusulayı indir"
                    aria-label={`${slip.periodName} bordro pusulasını indir`}
                    onClick={() => handleDownload(slip.periodId, slip.rowId, slip.periodName ?? "")}
                  >
                    <i aria-hidden="true" className="fa-solid fa-file-pdf" /> PDF
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {slips.length === 0 && <p className="pending-desc">Henüz bordro kaydınız yok.</p>}
      </section>
    </div>
  );
}
```

- [x] **Step 3: `PayrollPage.tsx` yaz** (sekme içerikleri placeholder; Task 3/5/6 doldurur)

```tsx
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ApiError } from "../../api/client";
import { useAuth } from "../../auth/AuthContext";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { MyPayslipsView } from "./MyPayslipsView";
import { useCreatePayrollPeriod, usePayrollPeriods, useSubmitPayrollPeriod } from "./queries";

const TABS = [
  { tab: "period", path: "/payroll", icon: "fa-table-list", label: "Dönem Bordrosu" },
  { tab: "calculator", path: "/payroll/calculator", icon: "fa-calculator", label: "Tekil Hesaplama" },
  { tab: "settings", path: "/payroll/settings", icon: "fa-sliders", label: "Bordro Ayarları" },
] as const;

export function PayrollPage({ tab }: { tab: "period" | "calculator" | "settings" }) {
  const { user } = useAuth();
  const isAdmin = user?.role === "hr-admin";
  if (!isAdmin) return <MyPayslipsView />;
  return <PayrollAdminShell tab={tab} />;
}

function PayrollAdminShell({ tab }: { tab: "period" | "calculator" | "settings" }) {
  const navigate = useNavigate();
  const { showToast } = useToast();
  const periodsQ = usePayrollPeriods(true);
  const submitPeriod = useSubmitPayrollPeriod();
  const [periodId, setPeriodId] = useState<number | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  useEffect(() => {
    if (periodId === null && (periodsQ.data ?? []).length > 0) {
      setPeriodId(periodsQ.data![0].id ?? null);
    }
  }, [periodsQ.data, periodId]);

  if (periodsQ.isPending) return <PageLoading />;
  if (periodsQ.isError) return <PageError error={periodsQ.error} />;

  const periods = periodsQ.data;
  const selected = periods.find((p) => p.id === periodId) ?? null;

  const handleSubmitPeriod = async () => {
    if (periodId === null) {
      showToast("Önce bir bordro dönemi seçin.", "warning");
      return;
    }
    try {
      await submitPeriod.mutateAsync(periodId);
      showToast("Dönem bordrosu onaylandı ve kapatıldı.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Dönem gönderilemedi.", "error");
    }
  };

  return (
    <div id="payroll-screen">
      <div className="page-header">
        <div>
          <h2>Bordro</h2>
          <p>
            {selected
              ? `${selected.name} dönemi için hesaplama, kontrol, onay ve tekil bordro senaryolarını yönetin.`
              : "Bordro dönemi oluşturarak hesaplama, kontrol ve onay akışını başlatın."}
          </p>
        </div>
        <div className="header-actions">
          {tab === "period" && (
            <>
              <label className="sr-only" htmlFor="payroll-period-select">Bordro dönemi</label>
              <select
                id="payroll-period-select"
                className="small-select"
                value={periodId ?? ""}
                onChange={(e) => setPeriodId(Number(e.target.value))}
              >
                {periods.map((p) => (
                  <option key={p.id} value={p.id}>{p.name}</option>
                ))}
              </select>
              <button className="btn btn-secondary" onClick={() => setCreateOpen(true)}>
                <i aria-hidden="true" className="fa-solid fa-plus" /> Yeni Dönem
              </button>
            </>
          )}
          <button className="btn btn-secondary" onClick={() => navigate("/payroll/calculator")}>
            <i aria-hidden="true" className="fa-solid fa-calculator" /> Tekil hesapla
          </button>
          <button className="btn btn-primary" onClick={handleSubmitPeriod} disabled={submitPeriod.isPending}>
            <i aria-hidden="true" className="fa-solid fa-paper-plane" /> Onaya gönder
          </button>
        </div>
      </div>

      <div className="payroll-tabs">
        {TABS.map((item) => (
          <button
            key={item.tab}
            className={`payroll-tab ${tab === item.tab ? "active" : ""}`}
            onClick={() => navigate(item.path)}
          >
            <i aria-hidden="true" className={`fa-solid ${item.icon}`} /> {item.label}
          </button>
        ))}
      </div>

      {/* Sekme içerikleri: Task 3 (dönem), Task 5 (tekil), Task 6 (ayarlar) */}
      {tab === "period" && (
        <section id="payroll-period" className="payroll-tab-content active">{null}</section>
      )}
      {tab === "calculator" && (
        <section id="payroll-calculator" className="payroll-tab-content active">{null}</section>
      )}
      {tab === "settings" && (
        <section id="payroll-settings" className="payroll-tab-content active">{null}</section>
      )}

      {createOpen && (
        <CreatePeriodModal onClose={() => setCreateOpen(false)} onCreated={(id) => setPeriodId(id)} />
      )}
    </div>
  );
}

function CreatePeriodModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: number) => void }) {
  const { showToast } = useToast();
  const createPeriod = useCreatePayrollPeriod();
  const now = new Date();
  const [year, setYear] = useState(String(now.getFullYear()));
  const [month, setMonth] = useState(String(now.getMonth() + 1));
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    setError(null);
    try {
      const period = await createPeriod.mutateAsync({ year: Number(year), month: Number(month) });
      showToast(`${period.name} bordro dönemi oluşturuldu.`, "success");
      if (period.id !== undefined) onCreated(period.id);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Dönem oluşturulamadı.");
    }
  };

  return (
    <div className="modal-overlay" style={{ display: "flex" }}>
      <div className="modal-card scale-in">
        <div className="modal-head">
          <div>
            <h3>Yeni Bordro Dönemi</h3>
            <p>Aktif personel için girdi satırları otomatik oluşturulur.</p>
          </div>
          <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Dönem penceresini kapat">
            <i aria-hidden="true" className="fa-solid fa-xmark" />
          </button>
        </div>
        <div className="modal-body-scroll">
          {error && <p className="form-error" role="alert">{error}</p>}
          <div className="form-grid-2">
            <div className="input-group">
              <label className="input-label" htmlFor="pp-year">Yıl</label>
              <input id="pp-year" type="number" className="input-control" value={year} onChange={(e) => setYear(e.target.value)} />
            </div>
            <div className="input-group">
              <label className="input-label" htmlFor="pp-month">Ay</label>
              <select id="pp-month" className="input-control" value={month} onChange={(e) => setMonth(e.target.value)}>
                {Array.from({ length: 12 }, (_, i) => (
                  <option key={i + 1} value={i + 1}>{i + 1}</option>
                ))}
              </select>
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={onClose}>Vazgeç</button>
          <button className="btn btn-primary" onClick={submit} disabled={createPeriod.isPending}>
            <i aria-hidden="true" className="fa-solid fa-check" /> Oluştur
          </button>
        </div>
      </div>
    </div>
  );
}

export const PayrollPeriodPage = () => <PayrollPage tab="period" />;
export const PayrollCalculatorPage = () => <PayrollPage tab="calculator" />;
export const PayrollSettingsPage = () => <PayrollPage tab="settings" />;
```

- [x] **Step 4: `routes.tsx` `pageFor`'a ekle**

```tsx
import { PayrollPeriodPage, PayrollCalculatorPage, PayrollSettingsPage } from "./features/payroll/PayrollPage";
// pageFor içine:
//   payroll: PayrollPeriodPage,
//   "payroll-calculator": PayrollCalculatorPage,
//   "payroll-settings": PayrollSettingsPage,
```

- [x] **Step 5: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 6: Commit**

```bash
git add frontend/src/
git commit -m "feat(frontend): bordro kabuğu — sekmeler, dönem seçici/oluşturma, Bordrolarım (çalışan)"
```

---

### Task 3: Dönem Bordrosu sekmesi (KPI + akış + kontrol merkezi + parametreler + tablo)

**Files:**
- Create: `frontend/src/features/payroll/PeriodTab.tsx`
- Modify: `frontend/src/features/payroll/PayrollPage.tsx` (period placeholder → `<PeriodTab periodId={periodId} />`)
- Test: `frontend/src/features/payroll/PeriodTab.test.tsx`

**Interfaces:**
- Consumes: `usePayrollPeriod`, `useRunPayrollCheck`, `usePayrollSettings`, format yardımcıları, `useToast`.
- Produces: `PeriodTab({ periodId }: { periodId: number | null })`. Satır "Detay" butonu `setDetailRow(row)` state kurar; overlay `{detailRow && null}` placeholder (Task 4 doldurur). `PayrollDetailPanel`'a geçecek prop'lar: `periodId`, `periodName`, `row`, `onClose`.

- [x] **Step 1: Başarısız testleri yaz** — `PeriodTab.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { formatPayrollMoney } from "./format";
import { PeriodTab } from "./PeriodTab";

const row = (over: object) => ({
  id: 1, employeeId: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım",
  grossSalary: 118000, workedDays: 30, overtimeHours: 12, premiumPay: 3500,
  roadAllowance: 1200, mealAllowance: 1800, benefitPay: 3200, specialDeductions: 1800,
  previousTaxBase: 312000, ibanComplete: true, timesheetComplete: true,
  approvalStatus: "Kontrol", notes: null,
  hourlyRate: 524, overtimePay: 9440, baseGross: 118000, grossEarnings: 137140,
  sgkBase: 137140, sgkEmployee: 19200, unemploymentEmployee: 1371, incomeTaxBase: 116569,
  incomeTax: 25000, stampTax: 790, totalDeductions: 48161, netPay: 88979,
  employerSgk: 28114, employerUnemployment: 2743, employerCost: 167997, warnings: ["Kontrol bekliyor"],
  ...over,
});
const detail = {
  id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft",
  rows: [row({}), row({ id: 2, employeeId: 4, name: "Selin Koç", department: "Tasarım", approvalStatus: "Onaylandı", warnings: [] })],
  totals: { gross: 274280, net: 177958, employerCost: 335994, deductions: 96322 },
  controls: [
    { label: "Eksik Puantaj", value: 0, level: "high" },
    { label: "Eksik IBAN", value: 1, level: "high" },
    { label: "SGK Matrah Uyarısı", value: 0, level: "medium" },
    { label: "Vergi Matrahı Uyarısı", value: 0, level: "medium" },
    { label: "Onay Bekleyen", value: 1, level: "low" },
  ],
};
const settings = {
  year: 2026, overtimeMultiplier: 1.5, monthlyWorkingHours: 225, defaultWorkedDays: 30,
  sgkEmployeeRate: 14, unemploymentEmployeeRate: 1, sgkEmployerRate: 20.5, unemploymentEmployerRate: 2,
  stampTaxRate: 0.759, sgkBaseMin: 33030, sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211, monthlyMinWageStampTaxExemption: 250.7,
  minWageGross: 33030, taxBrackets: [],
};

beforeEach(() =>
  stubApi({
    "/api/payroll/periods/3": detail,
    "/api/payroll/periods/3/check": detail,
    "/api/payroll/settings": settings,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderTab = () =>
  renderPage(
    <ToastProvider>
      <PeriodTab periodId={3} />
    </ToastProvider>,
  );

test("KPI şeridi dönem toplamlarıyla dolar", async () => {
  renderTab();
  expect(await screen.findByText("Taslak")).toBeInTheDocument();
  expect(screen.getByText("Toplam Brüt").closest(".stat-box")).toHaveTextContent(formatPayrollMoney(274280));
  expect(screen.getByText("Çalışan").closest(".stat-box")).toHaveTextContent("1 onaylandı");
});

test("kontrol merkezi backend kartlarını statik detaylarla gösterir", async () => {
  renderTab();
  expect(await screen.findByText("Eksik IBAN")).toBeInTheDocument();
  expect(screen.getByText("Ödeme listesine girmeden tamamlanmalı.")).toBeInTheDocument();
});

test("kontrol edildi butonu check ucuna gider", async () => {
  renderTab();
  await screen.findByText("Eksik IBAN");
  await userEvent.click(screen.getByRole("button", { name: /Kontrol edildi/ }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/3/check");
    expect(hit).toBe(true);
  });
});

test("tablo satırları ve departman filtresi çalışır", async () => {
  renderTab();
  expect(await screen.findByText("Ahmet Yılmaz")).toBeInTheDocument();
  await userEvent.selectOptions(screen.getByLabelText("Departman filtresi"), "Tasarım");
  expect(screen.queryByText("Ahmet Yılmaz")).not.toBeInTheDocument();
  expect(screen.getByText("Selin Koç")).toBeInTheDocument();
});

test("dönem seçili değilse boş durum görünür", async () => {
  renderPage(
    <ToastProvider>
      <PeriodTab periodId={null} />
    </ToastProvider>,
  );
  expect(await screen.findByText(/Henüz bordro dönemi yok/)).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/payroll/PeriodTab.test.tsx` → FAIL.

- [x] **Step 2: `PeriodTab.tsx` yaz** (eski `renderPayrollPeriodTab` + `renderPayrollParameters` DOM paritesi)

```tsx
import { useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import {
  CONTROL_META, PERIOD_STATUS_TEXT, derivePayrollSteps, formatPayrollMoney,
  formatPayrollNumber, payrollStatusClass,
} from "./format";
import { usePayrollPeriod, usePayrollSettings, useRunPayrollCheck, type PayrollRowDto } from "./queries";

export function PeriodTab({ periodId }: { periodId: number | null }) {
  const { showToast } = useToast();
  const detailQ = usePayrollPeriod(periodId);
  const settingsQ = usePayrollSettings(true);
  const runCheck = useRunPayrollCheck();
  const [deptFilter, setDeptFilter] = useState("");
  const [selectedStep, setSelectedStep] = useState<string | null>(null);
  const [detailRow, setDetailRow] = useState<PayrollRowDto | null>(null);

  if (periodId === null) {
    return (
      <div className="card">
        <p className="pending-desc">Henüz bordro dönemi yok. "Yeni Dönem" ile ilk dönemi oluşturun.</p>
      </div>
    );
  }

  if (detailQ.isPending || settingsQ.isPending) return <PageLoading />;
  if (detailQ.isError) return <PageError error={detailQ.error} />;
  if (settingsQ.isError) return <PageError error={settingsQ.error} />;

  const detail = detailQ.data;
  const parameters = settingsQ.data;
  const rows = detail.rows ?? [];
  const controls = detail.controls ?? [];
  const totals = detail.totals ?? {};
  const approvedCount = rows.filter((r) => r.approvalStatus === "Onaylandı").length;
  const steps = derivePayrollSteps(detail.status ?? undefined, rows, controls);
  const departments = [...new Set(rows.map((r) => r.department ?? ""))].filter(Boolean);
  const visibleRows = deptFilter ? rows.filter((r) => r.department === deptFilter) : rows;

  const handleCheck = async () => {
    try {
      await runCheck.mutateAsync(periodId);
      showToast("Kontrol listesi işaretlendi; uyarılar denetim izine kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Kontrol çalıştırılamadı.", "error");
    }
  };

  return (
    <>
      <div className="payroll-kpi-grid">
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Dönem Durumu</span>
          <strong className="sb-val">{PERIOD_STATUS_TEXT[detail.status ?? ""] ?? detail.status}</strong>
          <small>{detail.name} bordrosu</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Çalışan</span>
          <strong className="sb-val">{rows.length}</strong>
          <small>{approvedCount} onaylandı</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Toplam Brüt</span>
          <strong className="sb-val">{formatPayrollMoney(totals.gross)}</strong>
          <small>Ek ödemeler dahil</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">Toplam Net</span>
          <strong className="sb-val">{formatPayrollMoney(totals.net)}</strong>
          <small>Ödeme önizlemesi</small>
        </div>
        <div className="stat-box payroll-kpi">
          <span className="sb-label">İşveren Maliyeti</span>
          <strong className="sb-val">{formatPayrollMoney(totals.employerCost)}</strong>
          <small>Prim işveren payı dahil</small>
        </div>
      </div>

      <section className="card payroll-flow-card">
        <div className="card-header-clean">
          <div>
            <h4>Bordro Akışı</h4>
            <p className="text-muted">Dönem kapanışı için adım adım operasyon takibi.</p>
          </div>
        </div>
        <div className="payroll-flow">
          {steps.map((step) => (
            <button
              key={step.id}
              className={`payroll-step ${step.status} ${selectedStep === step.id ? "selected" : ""}`}
              onClick={() => setSelectedStep(step.id)}
            >
              <span>{step.label}</span>
              <strong>{step.status === "done" ? "Tamam" : step.status === "active" ? "Aktif" : "Bekliyor"}</strong>
              <small>{step.meta}</small>
            </button>
          ))}
        </div>
      </section>

      <div className="payroll-main-grid">
        <section className="card payroll-control-card" data-active-step={selectedStep ?? undefined}>
          <div className="card-header-clean">
            <div>
              <h4>Kontrol Merkezi</h4>
              <p className="text-muted">Eksik veri, matrah ve onay uyarıları.</p>
            </div>
            <button className="btn btn-secondary btn-sm" onClick={handleCheck} disabled={runCheck.isPending}>
              <i aria-hidden="true" className="fa-solid fa-check-double" /> Kontrol edildi
            </button>
          </div>
          <div className="payroll-control-grid">
            {controls.map((control) => {
              const meta = CONTROL_META[control.label ?? ""] ?? { detail: "", icon: "fa-circle-info" };
              return (
                <div key={control.label} className={`payroll-control ${control.level ?? ""}`}>
                  <div className="payroll-control-icon"><i aria-hidden="true" className={`fa-solid ${meta.icon}`} /></div>
                  <div>
                    <span>{control.label}</span>
                    <strong>{control.value}</strong>
                    <p>{meta.detail}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        <div className="card payroll-parameters">
          <div className="card-header-clean">
            <div>
              <h4>{parameters.year} Bordro Parametreleri</h4>
              <p className="text-muted">Demo ön hesap için kullanılan Türkiye 4/a parametreleri.</p>
            </div>
            <span className="status-pill info">Ön hesap</span>
          </div>
          <div className="parameter-grid">
            <div><span>Fazla mesai çarpanı</span><strong>{formatPayrollNumber(parameters.overtimeMultiplier)}</strong></div>
            <div><span>Aylık çalışma saati</span><strong>{formatPayrollNumber(parameters.monthlyWorkingHours)}</strong></div>
            <div><span>SGK PEK alt sınır</span><strong>{formatPayrollMoney(parameters.sgkBaseMin)}</strong></div>
            <div><span>SGK PEK üst sınır</span><strong>{formatPayrollMoney(parameters.sgkBaseMax)}</strong></div>
            <div><span>SGK işçi / işsizlik</span><strong>%{formatPayrollNumber(parameters.sgkEmployeeRate)} + %{formatPayrollNumber(parameters.unemploymentEmployeeRate)}</strong></div>
            <div><span>Damga vergisi</span><strong>‰{formatPayrollNumber(parameters.stampTaxRate)}</strong></div>
          </div>
          <div className="payroll-note">
            <i aria-hidden="true" className="fa-solid fa-circle-info" />
            <span>Bu ekran bordro deneyimi ve kontrol akışı için demo hesap sunar; üretim kullanımında mevzuat ve müşavir doğrulaması gerekir.</span>
          </div>
        </div>
      </div>

      <section className="table-container payroll-table-card">
        <div className="payroll-table-header">
          <div>
            <h4>Çalışan Bordro Tablosu</h4>
            <p className="text-muted">Satıra tıklayarak bordro pusulası ve hesap detayını açın.</p>
          </div>
          <div className="toolbar-actions">
            <label className="sr-only" htmlFor="payroll-dept-filter">Departman filtresi</label>
            <select
              id="payroll-dept-filter"
              className="small-select"
              value={deptFilter}
              onChange={(e) => setDeptFilter(e.target.value)}
            >
              <option value="">Tüm departmanlar</option>
              {departments.map((dept) => (
                <option key={dept} value={dept}>{dept}</option>
              ))}
            </select>
          </div>
        </div>
        <table className="data-table payroll-table">
          <thead>
            <tr>
              <th>Personel</th><th>Departman</th><th>Brüt</th><th>Fazla Mesai</th><th>Ek Ödeme</th><th>Kesinti</th><th>SGK Matrahı</th><th>GV Matrahı</th><th>Net</th><th>Durum</th><th></th>
            </tr>
          </thead>
          <tbody>
            {visibleRows.map((r) => (
              <tr key={r.id} data-dept={r.department ?? ""}>
                <td><strong>{r.name}</strong><small>{r.title}</small></td>
                <td>{r.department}</td>
                <td>{formatPayrollMoney(r.grossEarnings)}</td>
                <td>{formatPayrollMoney(r.overtimePay)}<small>{r.overtimeHours ?? 0} saat</small></td>
                <td>{formatPayrollMoney((r.premiumPay ?? 0) + (r.roadAllowance ?? 0) + (r.mealAllowance ?? 0) + (r.benefitPay ?? 0))}</td>
                <td>{formatPayrollMoney(r.totalDeductions)}</td>
                <td>{formatPayrollMoney(r.sgkBase)}</td>
                <td>{formatPayrollMoney(r.incomeTaxBase)}</td>
                <td><strong>{formatPayrollMoney(r.netPay)}</strong></td>
                <td><span className={`status-pill ${payrollStatusClass(r.approvalStatus)}`}>{r.approvalStatus}</span></td>
                <td>
                  <button className="btn btn-secondary btn-sm" onClick={() => setDetailRow(r)}>Detay</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* Detay paneli Task 4'te */}
      <div id="payroll-detail-overlay" className={`payroll-detail-overlay ${detailRow ? "active" : ""}`}>
        {detailRow && null}
      </div>
    </>
  );
}
```

- [x] **Step 3: `PayrollPage.tsx`'te bağla** — period placeholder'ı değiştir:

```tsx
// import { PeriodTab } from "./PeriodTab";
{tab === "period" && (
  <section id="payroll-period" className="payroll-tab-content active">
    <PeriodTab periodId={periodId} />
  </section>
)}
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/payroll/
git commit -m "feat(frontend): dönem bordrosu sekmesi — KPI, akış, kontrol merkezi, parametreler, tablo"
```

---

### Task 4: Bordro satır detay paneli (görüntüle + düzenle + onayla + PDF)

**Files:**
- Create: `frontend/src/features/payroll/PayrollDetailPanel.tsx`
- Modify: `frontend/src/features/payroll/PeriodTab.tsx` (overlay placeholder → panel)
- Test: `frontend/src/features/payroll/PayrollDetailPanel.test.tsx`

**Interfaces:**
- Consumes: `useUpdatePayrollRow`, `useApprovePayrollRow`, `apiDownload`, format yardımcıları, `useToast`.
- Produces: `PayrollDetailPanel({ periodId, periodName, row, onClose }: { periodId: number; periodName: string; row: PayrollRowDto; onClose: () => void })`.

Davranış: eski `renderPayrollDetail` DOM'u (özet + kazanç/kesinti/matrah kartları + slip önizleme) + **yeni** "Girdiler" düzenleme formu (onaylı satırda gizli). Footer: Kapat · Kaydet (`PUT` row) · Onaya gönder (`POST` approve) · onaylı satırda "Pusula İndir" (PDF).

- [x] **Step 1: Başarısız testleri yaz** — `PayrollDetailPanel.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { PayrollDetailPanel } from "./PayrollDetailPanel";

const baseRow = {
  id: 7, employeeId: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım",
  grossSalary: 118000, workedDays: 30, overtimeHours: 12, premiumPay: 3500,
  roadAllowance: 1200, mealAllowance: 1800, benefitPay: 3200, specialDeductions: 1800,
  previousTaxBase: 312000, ibanComplete: true, timesheetComplete: true,
  approvalStatus: "Kontrol", notes: "SGK tavanına yakın kazanç.",
  hourlyRate: 524, overtimePay: 9440, baseGross: 118000, grossEarnings: 137140,
  sgkBase: 137140, sgkEmployee: 19200, unemploymentEmployee: 1371, incomeTaxBase: 116569,
  incomeTax: 25000, stampTax: 790, totalDeductions: 48161, netPay: 88979,
  employerSgk: 28114, employerUnemployment: 2743, employerCost: 167997, warnings: ["Kontrol bekliyor"],
};

beforeEach(() =>
  stubApi({
    "/api/payroll/periods/3/rows/7": { ...baseRow },
    "/api/payroll/periods/3/rows/7/approve": { ...baseRow, approvalStatus: "Onaylandı" },
    "/api/payroll/periods/3/rows/7/payslip": {},
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderPanel = (row = baseRow) =>
  renderPage(
    <ToastProvider>
      <PayrollDetailPanel periodId={3} periodName="Temmuz 2026" row={row} onClose={() => {}} />
    </ToastProvider>,
  );

test("panel satır bilgileri ve uyarılarıyla açılır", async () => {
  renderPanel();
  expect(screen.getByText("Ahmet Yılmaz")).toBeInTheDocument();
  expect(screen.getByText("Kontrol bekliyor")).toBeInTheDocument();
  expect(screen.getByText("Bordro Pusulası Önizleme")).toBeInTheDocument();
});

test("girdi kaydetme PUT row ucuna doğru gövdeyle gider", async () => {
  renderPanel();
  const gross = screen.getByLabelText("Brüt ücret");
  await userEvent.clear(gross);
  await userEvent.type(gross, "120000");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/payroll/periods/3/rows/7" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    const body = JSON.parse(String(put![1]?.body));
    expect(body).toMatchObject({ grossSalary: 120000, workedDays: 30, overtimeHours: 12 });
  });
});

test("onaya gönder approve ucuna gider", async () => {
  renderPanel();
  await userEvent.click(screen.getByRole("button", { name: /Onaya gönder/ }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/3/rows/7/approve");
    expect(hit).toBe(true);
  });
});

test("onaylı satırda düzenleme formu gizli, PDF butonu görünür", async () => {
  vi.stubGlobal("URL", { ...URL, createObjectURL: vi.fn(() => "blob:x"), revokeObjectURL: vi.fn() });
  renderPanel({ ...baseRow, approvalStatus: "Onaylandı" });
  expect(screen.queryByLabelText("Brüt ücret")).not.toBeInTheDocument();
  await userEvent.click(screen.getByRole("button", { name: /Pusula İndir/ }));
  await waitFor(() => {
    const hit = vi.mocked(fetch).mock.calls.some(([u]) => String(u) === "/api/payroll/periods/3/rows/7/payslip");
    expect(hit).toBe(true);
  });
});
```

Run: `npm test -- --run src/features/payroll/PayrollDetailPanel.test.tsx` → FAIL.

- [x] **Step 2: `PayrollDetailPanel.tsx` yaz**

```tsx
import { useState } from "react";
import { ApiError, apiDownload } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { formatPayrollMoney, payrollStatusClass } from "./format";
import { useApprovePayrollRow, useUpdatePayrollRow, type PayrollRowDto } from "./queries";

const Line = ({ label, value, tone = "" }: { label: string; value?: number; tone?: string }) => (
  <div className={`payroll-line ${tone}`}>
    <span>{label}</span>
    <strong>{formatPayrollMoney(value)}</strong>
  </div>
);

export function PayrollDetailPanel({ periodId, periodName, row, onClose }: {
  periodId: number;
  periodName: string;
  row: PayrollRowDto;
  onClose: () => void;
}) {
  const { showToast } = useToast();
  const updateRow = useUpdatePayrollRow();
  const approveRow = useApprovePayrollRow();
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState({
    grossSalary: String(row.grossSalary ?? 0),
    workedDays: String(row.workedDays ?? 30),
    overtimeHours: String(row.overtimeHours ?? 0),
    premiumPay: String(row.premiumPay ?? 0),
    roadAllowance: String(row.roadAllowance ?? 0),
    mealAllowance: String(row.mealAllowance ?? 0),
    benefitPay: String(row.benefitPay ?? 0),
    specialDeductions: String(row.specialDeductions ?? 0),
    previousTaxBase: String(row.previousTaxBase ?? 0),
    ibanComplete: row.ibanComplete ?? true,
    timesheetComplete: row.timesheetComplete ?? true,
    notes: row.notes ?? "",
  });

  const rowId = row.id!;
  const isApproved = row.approvalStatus === "Onaylandı";
  const parse = (value: string) => {
    const parsed = Number(String(value).replace(",", "."));
    return Number.isFinite(parsed) ? parsed : 0;
  };
  const set = (key: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({
      ...f,
      [key]: e.target.type === "checkbox" ? e.target.checked : e.target.value,
    }));

  const save = async () => {
    setError(null);
    try {
      await updateRow.mutateAsync({
        periodId,
        rowId,
        model: {
          grossSalary: parse(form.grossSalary),
          workedDays: Math.round(parse(form.workedDays)),
          overtimeHours: Math.round(parse(form.overtimeHours)),
          premiumPay: parse(form.premiumPay),
          roadAllowance: parse(form.roadAllowance),
          mealAllowance: parse(form.mealAllowance),
          benefitPay: parse(form.benefitPay),
          specialDeductions: parse(form.specialDeductions),
          previousTaxBase: parse(form.previousTaxBase),
          ibanComplete: form.ibanComplete,
          timesheetComplete: form.timesheetComplete,
          notes: form.notes || null,
        },
      });
      showToast(`${row.name} bordro girdileri kaydedildi.`, "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Girdiler kaydedilemedi.");
    }
  };

  const approve = async () => {
    setError(null);
    try {
      await approveRow.mutateAsync({ periodId, rowId });
      showToast(`${row.name} bordrosu onaylandı.`, "success");
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Satır onaylanamadı.");
    }
  };

  const downloadSlip = async () => {
    try {
      const { blob, fileName } = await apiDownload(`/payroll/periods/${periodId}/rows/${rowId}/payslip`);
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = fileName ?? `bordro-${periodName}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
      showToast("Bordro pusulası indirildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Pusula indirilemedi.", "error");
    }
  };

  const inputFields: [keyof typeof form, string][] = [
    ["grossSalary", "Brüt ücret"],
    ["workedDays", "Çalışılan gün"],
    ["overtimeHours", "Fazla mesai saati"],
    ["premiumPay", "Prim"],
    ["roadAllowance", "Yol yardımı"],
    ["mealAllowance", "Yemek yardımı"],
    ["benefitPay", "Yan hak / ek ödeme"],
    ["specialDeductions", "Özel kesinti"],
    ["previousTaxBase", "Önceki GV matrahı"],
  ];

  return (
    <div className="payroll-detail-panel">
      <div className="payroll-detail-head">
        <div>
          <span className={`status-pill ${payrollStatusClass(row.approvalStatus)}`}>{row.approvalStatus}</span>
          <h3>{row.name}</h3>
          <p>{row.title} · {row.department} · {periodName}</p>
        </div>
        <button className="btn-icon-sm" onClick={onClose} title="Kapat" aria-label="Bordro detayını kapat">
          <i aria-hidden="true" className="fa-solid fa-xmark" />
        </button>
      </div>

      {error && <p className="form-error" role="alert">{error}</p>}

      <div className="payroll-detail-summary">
        <div><span>Brüt Kazanç</span><strong>{formatPayrollMoney(row.grossEarnings)}</strong></div>
        <div><span>Toplam Kesinti</span><strong>{formatPayrollMoney(row.totalDeductions)}</strong></div>
        <div><span>Net Ücret</span><strong>{formatPayrollMoney(row.netPay)}</strong></div>
        <div><span>İşveren Maliyeti</span><strong>{formatPayrollMoney(row.employerCost)}</strong></div>
      </div>

      {!isApproved && (
        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Girdiler</h4></div>
          <div className="form-grid">
            {inputFields.map(([key, label]) => (
              <div key={key} className="input-group col-3">
                <label className="input-label" htmlFor={`pd-${key}`}>{label}</label>
                <input
                  id={`pd-${key}`}
                  type="number"
                  className="input-control"
                  value={String(form[key])}
                  onChange={set(key)}
                />
              </div>
            ))}
            <div className="input-group col-3">
              <label className="input-label" htmlFor="pd-notes">Not</label>
              <input
                id="pd-notes"
                type="text"
                className="input-control"
                value={form.notes}
                onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
              />
            </div>
          </div>
          <div className="toolbar-actions">
            <label className="leg-item">
              <input type="checkbox" checked={form.ibanComplete} onChange={set("ibanComplete")} /> IBAN tamam
            </label>
            <label className="leg-item">
              <input type="checkbox" checked={form.timesheetComplete} onChange={set("timesheetComplete")} /> Puantaj tamam
            </label>
          </div>
        </section>
      )}

      <div className="payroll-detail-grid">
        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Kazançlar</h4></div>
          <Line label="Brüt ücret" value={row.baseGross} />
          <Line label="Fazla mesai" value={row.overtimePay} />
          <Line label="Prim" value={row.premiumPay} />
          <Line label="Yol yardımı" value={row.roadAllowance} />
          <Line label="Yemek yardımı" value={row.mealAllowance} />
          <Line label="Yan hak / ek ödeme" value={row.benefitPay} />
          <div className="payroll-note compact">
            <i aria-hidden="true" className="fa-solid fa-circle-info" />
            <span>{row.notes || "Not yok."}</span>
          </div>
        </section>

        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Kesintiler</h4></div>
          <Line label="SGK işçi payı" value={row.sgkEmployee} tone="deduction" />
          <Line label="İşsizlik işçi payı" value={row.unemploymentEmployee} tone="deduction" />
          <Line label="Gelir vergisi" value={row.incomeTax} tone="deduction" />
          <Line label="Damga vergisi" value={row.stampTax} tone="deduction" />
          <Line label="Özel kesintiler" value={row.specialDeductions} tone="deduction" />
        </section>

        <section className="card payroll-detail-section">
          <div className="card-header-clean"><h4>Vergi / SGK Matrahları</h4></div>
          <Line label="Saatlik ücret" value={row.hourlyRate} />
          <Line label="SGK matrahı" value={row.sgkBase} />
          <Line label="Gelir vergisi matrahı" value={row.incomeTaxBase} />
          <Line label="Önceki kümülatif matrah" value={row.previousTaxBase} />
          <Line label="Dönem sonu kümülatif" value={(row.previousTaxBase ?? 0) + (row.incomeTaxBase ?? 0)} />
          <div className="payroll-warning-stack">
            {(row.warnings ?? []).length > 0
              ? (row.warnings ?? []).map((warning) => (
                  <span key={warning} className="status-pill pending">{warning}</span>
                ))
              : <span className="status-pill approved">Uyarı yok</span>}
          </div>
        </section>

        <section className="card payroll-slip-preview">
          <div className="card-header-clean">
            <h4>Bordro Pusulası Önizleme</h4>
            <span className="status-pill info">Demo</span>
          </div>
          <div className="slip-paper">
            <div className="slip-brand"><strong>Ekipzen</strong><span>{periodName} Bordro Pusulası</span></div>
            <div className="slip-row"><span>Personel</span><strong>{row.name}</strong></div>
            <div className="slip-row"><span>Çalışılan gün</span><strong>{row.workedDays}</strong></div>
            <div className="slip-row"><span>Fazla mesai</span><strong>{row.overtimeHours ?? 0} saat · {formatPayrollMoney(row.overtimePay)}</strong></div>
            <div className="slip-row"><span>Brüt kazanç</span><strong>{formatPayrollMoney(row.grossEarnings)}</strong></div>
            <div className="slip-row"><span>Yasal kesintiler</span><strong>{formatPayrollMoney((row.totalDeductions ?? 0) - (row.specialDeductions ?? 0))}</strong></div>
            <div className="slip-row"><span>Özel kesintiler</span><strong>{formatPayrollMoney(row.specialDeductions)}</strong></div>
            <div className="slip-row total"><span>Net ödenecek</span><strong>{formatPayrollMoney(row.netPay)}</strong></div>
          </div>
        </section>
      </div>

      <div className="payroll-detail-footer">
        <button className="btn btn-secondary" onClick={onClose}>Kapat</button>
        {isApproved ? (
          <button className="btn btn-primary" onClick={downloadSlip}>
            <i aria-hidden="true" className="fa-solid fa-file-pdf" /> Pusula İndir
          </button>
        ) : (
          <>
            <button className="btn btn-secondary" onClick={save} disabled={updateRow.isPending}>
              <i aria-hidden="true" className="fa-solid fa-floppy-disk" /> Kaydet
            </button>
            <button className="btn btn-primary" onClick={approve} disabled={approveRow.isPending}>
              <i aria-hidden="true" className="fa-solid fa-paper-plane" /> Onaya gönder
            </button>
          </>
        )}
      </div>
    </div>
  );
}
```

- [x] **Step 3: `PeriodTab.tsx`'te bağla** — overlay placeholder'ı değiştir:

```tsx
// import { PayrollDetailPanel } from "./PayrollDetailPanel";
<div id="payroll-detail-overlay" className={`payroll-detail-overlay ${detailRow ? "active" : ""}`}>
  {detailRow && (
    <PayrollDetailPanel
      periodId={periodId}
      periodName={detail.name ?? ""}
      row={detailRow}
      onClose={() => setDetailRow(null)}
    />
  )}
</div>
```

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/payroll/
git commit -m "feat(frontend): bordro satır detay paneli — girdi düzenleme, onay, pusula PDF"
```

---

### Task 5: Tekil Hesaplama sekmesi (server-side preview)

**Files:**
- Create: `frontend/src/features/payroll/CalculatorTab.tsx`
- Modify: `frontend/src/features/payroll/PayrollPage.tsx` (calculator placeholder → `<CalculatorTab />`)
- Test: `frontend/src/features/payroll/CalculatorTab.test.tsx`

**Interfaces:**
- Consumes: `usePayrollPreview`, `usePayrollSettings`, `usePayrollPeriods`, `usePayrollPeriod`.
- Produces: `CalculatorTab()`. Form değişikliğinden 300ms sonra `POST /payroll/preview`; personel seçimi en yeni dönemin satırlarından form doldurur.

- [x] **Step 1: Başarısız testleri yaz** — `CalculatorTab.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { formatPayrollMoney } from "./format";
import { CalculatorTab } from "./CalculatorTab";

const settings = {
  year: 2026, overtimeMultiplier: 1.5, monthlyWorkingHours: 225, defaultWorkedDays: 30,
  sgkEmployeeRate: 14, unemploymentEmployeeRate: 1, sgkEmployerRate: 20.5, unemploymentEmployerRate: 2,
  stampTaxRate: 0.759, sgkBaseMin: 33030, sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211, monthlyMinWageStampTaxExemption: 250.7,
  minWageGross: 33030, taxBrackets: [],
};
const periods = [{ id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft", employeeCount: 1 }];
const detail = {
  id: 3, name: "Temmuz 2026", year: 2026, month: 7, status: "draft",
  rows: [{
    id: 1, employeeId: 3, name: "Ahmet Yılmaz", title: "Dev", department: "Yazılım",
    grossSalary: 118000, workedDays: 30, overtimeHours: 12, premiumPay: 3500,
    roadAllowance: 1200, mealAllowance: 1800, benefitPay: 3200, specialDeductions: 1800,
    previousTaxBase: 312000, ibanComplete: true, timesheetComplete: true,
    approvalStatus: "Kontrol", notes: null,
    hourlyRate: 0, overtimePay: 0, baseGross: 0, grossEarnings: 0, sgkBase: 0,
    sgkEmployee: 0, unemploymentEmployee: 0, incomeTaxBase: 0, incomeTax: 0, stampTax: 0,
    totalDeductions: 0, netPay: 0, employerSgk: 0, employerUnemployment: 0, employerCost: 0, warnings: [],
  }],
  totals: { gross: 0, net: 0, employerCost: 0, deductions: 0 },
  controls: [],
};
const calculation = {
  hourlyRate: 524.44, overtimePay: 2360, additionalPay: 6200, baseGross: 118000,
  grossEarnings: 126560, sgkBase: 126560, sgkEmployee: 17718, unemploymentEmployee: 1265,
  incomeTaxBase: 107577, incomeTax: 21000, stampTax: 710, totalDeductions: 40693,
  netPay: 85867, employerSgk: 25944, employerUnemployment: 2531, employerCost: 155035, warnings: [],
};

beforeEach(() =>
  stubApi({
    "/api/payroll/settings": settings,
    "/api/payroll/periods": periods,
    "/api/payroll/periods/3": detail,
    "/api/payroll/preview": calculation,
  }),
);
afterEach(() => vi.unstubAllGlobals());

const renderTab = () =>
  renderPage(
    <ToastProvider>
      <CalculatorTab />
    </ToastProvider>,
  );

test("personel seçilince form dolar ve preview backend'e gider", async () => {
  renderTab();
  await screen.findByText("Tekil Hesaplama");
  await userEvent.selectOptions(await screen.findByLabelText("Personel"), "1");
  expect(screen.getByLabelText("Brüt ücret")).toHaveValue(118000);
  await waitFor(() => {
    const posted = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/payroll/preview" && i?.method === "POST",
    );
    expect(posted).toBeTruthy();
    const body = JSON.parse(String(posted![1]?.body));
    expect(body).toMatchObject({ grossSalary: 118000, workedDays: 30, overtimeHours: 3 });
  });
});

test("sonuç panelinde net ödeme görünür", async () => {
  renderTab();
  await screen.findByText("Tekil Hesaplama");
  expect(await screen.findByText(formatPayrollMoney(85867))).toBeInTheDocument();
  expect(screen.getByText("Bu hesap dönem bordrosuna işlenmedi.")).toBeInTheDocument();
});
```

Run: `npm test -- --run src/features/payroll/CalculatorTab.test.tsx` → FAIL.

- [x] **Step 2: `CalculatorTab.tsx` yaz** (eski `renderPayrollCalculatorTab` + `renderScenarioResult` DOM paritesi)

```tsx
import { useEffect, useState } from "react";
import { PageError, PageLoading } from "../shared/PageState";
import { formatPayrollMoney, formatPayrollNumber } from "./format";
import {
  usePayrollPeriod, usePayrollPeriods, usePayrollPreview, usePayrollSettings,
} from "./queries";

const parseNumber = (value: string, fallback = 0): number => {
  const parsed = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : fallback;
};

type ScenarioForm = {
  gross: string; days: string; overtimeHours: string; overtimeMultiplier: string;
  premium: string; road: string; meal: string; benefit: string; deductions: string; taxBase: string;
};

export function CalculatorTab() {
  const settingsQ = usePayrollSettings(true);
  const periodsQ = usePayrollPeriods(true);
  const latestPeriodId = periodsQ.data?.[0]?.id ?? null;
  const detailQ = usePayrollPeriod(latestPeriodId);
  const preview = usePayrollPreview();

  const [employeeRowId, setEmployeeRowId] = useState("");
  const [form, setForm] = useState<ScenarioForm | null>(null);

  const settings = settingsQ.data;

  // İlk form değerleri ayarlardan gelir.
  useEffect(() => {
    if (form === null && settings) {
      setForm({
        gross: "0", days: String(settings.defaultWorkedDays ?? 30), overtimeHours: "3",
        overtimeMultiplier: String(settings.overtimeMultiplier ?? 1.5),
        premium: "0", road: "0", meal: "0", benefit: "0", deductions: "0", taxBase: "0",
      });
    }
  }, [form, settings]);

  // Form değişikliğinden 300ms sonra server-side ön hesap (personel arama debounce deseni).
  useEffect(() => {
    if (!form) return;
    const timer = setTimeout(() => {
      preview.mutate({
        grossSalary: parseNumber(form.gross),
        workedDays: Math.round(parseNumber(form.days, 30)),
        overtimeHours: parseNumber(form.overtimeHours),
        overtimeMultiplier: parseNumber(form.overtimeMultiplier, 1.5),
        premiumPay: parseNumber(form.premium),
        roadAllowance: parseNumber(form.road),
        mealAllowance: parseNumber(form.meal),
        benefitPay: parseNumber(form.benefit),
        specialDeductions: parseNumber(form.deductions),
        previousTaxBase: parseNumber(form.taxBase),
      });
    }, 300);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [form]);

  if (settingsQ.isPending || periodsQ.isPending || form === null) return <PageLoading />;
  if (settingsQ.isError) return <PageError error={settingsQ.error} />;
  if (periodsQ.isError) return <PageError error={periodsQ.error} />;

  const rows = detailQ.data?.rows ?? [];
  const selectedRow = rows.find((r) => String(r.id) === employeeRowId);
  const result = preview.data;

  const fillFromRow = (rowId: string) => {
    setEmployeeRowId(rowId);
    const row = rows.find((r) => String(r.id) === rowId);
    if (!row) return;
    setForm({
      gross: String(row.grossSalary ?? 0),
      days: String(settings!.defaultWorkedDays ?? 30),
      overtimeHours: "3",
      overtimeMultiplier: String(settings!.overtimeMultiplier ?? 1.5),
      premium: String(row.premiumPay ?? 0),
      road: String(row.roadAllowance ?? 0),
      meal: String(row.mealAllowance ?? 0),
      benefit: String(row.benefitPay ?? 0),
      deductions: "0",
      taxBase: String(row.previousTaxBase ?? 0),
    });
  };

  const setField = (key: keyof ScenarioForm) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f!, [key]: e.target.value }));

  const fields: [keyof ScenarioForm, string, string][] = [
    ["gross", "Brüt ücret", "1"],
    ["days", "Çalışılan gün", "1"],
    ["overtimeHours", "Fazla mesai saati", "1"],
    ["overtimeMultiplier", "Fazla mesai çarpanı", "0.1"],
    ["premium", "Prim", "1"],
    ["road", "Yol yardımı", "1"],
    ["meal", "Yemek yardımı", "1"],
    ["benefit", "Yan hak / ek ödeme", "1"],
    ["deductions", "Özel kesinti", "1"],
    ["taxBase", "Önceki GV matrahı", "1"],
  ];

  return (
    <div className="payroll-calculator-grid">
      <section className="card payroll-calculator-form">
        <div className="card-header-clean">
          <div>
            <h4>Tekil Hesaplama</h4>
            <p className="text-muted">Personel seçin, fazla mesai ve kazanç kalemlerini girerek ön hesap alın.</p>
          </div>
          <span className="status-pill info">Senaryo</span>
        </div>
        <div className="form-grid">
          <div className="input-group col-6">
            <label className="input-label" htmlFor="scenario-employee">Personel</label>
            <select
              id="scenario-employee"
              className="input-control"
              value={employeeRowId}
              onChange={(e) => fillFromRow(e.target.value)}
            >
              <option value="">Serbest giriş</option>
              {rows.map((r) => (
                <option key={r.id} value={r.id}>{r.name} - {r.title}</option>
              ))}
            </select>
          </div>
          {fields.map(([key, label, step]) => (
            <div key={key} className="input-group col-3">
              <label className="input-label" htmlFor={`scenario-${key}`}>{label}</label>
              <input
                id={`scenario-${key}`}
                className="input-control"
                type="number"
                step={step}
                value={form[key]}
                onChange={setField(key)}
              />
            </div>
          ))}
        </div>
        <div className="payroll-note">
          <i aria-hidden="true" className="fa-solid fa-circle-info" />
          <span>Fazla mesai tutarı, brüt ücret / {formatPayrollNumber(settings!.monthlyWorkingHours)} saat üzerinden çarpanla hesaplanır.</span>
        </div>
      </section>

      <aside className="card payroll-scenario-result" id="payroll-scenario-result">
        {result ? (
          <>
            <div className="scenario-result-head">
              <div>
                <span className="status-pill info">Ön hesap</span>
                <h4>{selectedRow?.name ?? "Tekil Hesaplama"}</h4>
                <p>Bu hesap dönem bordrosuna işlenmedi.</p>
              </div>
              <strong>{formatPayrollMoney(result.netPay)}</strong>
            </div>
            <div className="scenario-highlight-grid">
              <div><span>Saatlik ücret</span><strong>{formatPayrollMoney(result.hourlyRate)}</strong></div>
              <div>
                <span>Fazla mesai</span><strong>{formatPayrollMoney(result.overtimePay)}</strong>
                <small>{form.overtimeHours} saat × {formatPayrollNumber(parseNumber(form.overtimeMultiplier, 1.5))}</small>
              </div>
              <div><span>Toplam brüt</span><strong>{formatPayrollMoney(result.grossEarnings)}</strong></div>
              <div><span>İşveren maliyeti</span><strong>{formatPayrollMoney(result.employerCost)}</strong></div>
            </div>
            <div className="scenario-breakdown-grid">
              <section>
                <h5>Kazançlar</h5>
                <div className="payroll-line"><span>Brüt ücret</span><strong>{formatPayrollMoney(result.baseGross)}</strong></div>
                <div className="payroll-line"><span>Fazla mesai</span><strong>{formatPayrollMoney(result.overtimePay)}</strong></div>
                <div className="payroll-line"><span>Prim</span><strong>{formatPayrollMoney(parseNumber(form.premium))}</strong></div>
                <div className="payroll-line"><span>Yol yardımı</span><strong>{formatPayrollMoney(parseNumber(form.road))}</strong></div>
                <div className="payroll-line"><span>Yemek yardımı</span><strong>{formatPayrollMoney(parseNumber(form.meal))}</strong></div>
                <div className="payroll-line"><span>Yan hak / ek ödeme</span><strong>{formatPayrollMoney(parseNumber(form.benefit))}</strong></div>
              </section>
              <section>
                <h5>Kesintiler</h5>
                <div className="payroll-line deduction"><span>SGK işçi payı</span><strong>{formatPayrollMoney(result.sgkEmployee)}</strong></div>
                <div className="payroll-line deduction"><span>İşsizlik işçi payı</span><strong>{formatPayrollMoney(result.unemploymentEmployee)}</strong></div>
                <div className="payroll-line deduction"><span>Gelir vergisi</span><strong>{formatPayrollMoney(result.incomeTax)}</strong></div>
                <div className="payroll-line deduction"><span>Damga vergisi</span><strong>{formatPayrollMoney(result.stampTax)}</strong></div>
                <div className="payroll-line deduction"><span>Özel kesinti</span><strong>{formatPayrollMoney(parseNumber(form.deductions))}</strong></div>
              </section>
              <section>
                <h5>Matrahlar</h5>
                <div className="payroll-line"><span>SGK matrahı</span><strong>{formatPayrollMoney(result.sgkBase)}</strong></div>
                <div className="payroll-line"><span>Gelir vergisi matrahı</span><strong>{formatPayrollMoney(result.incomeTaxBase)}</strong></div>
                <div className="payroll-line"><span>Önceki kümülatif</span><strong>{formatPayrollMoney(parseNumber(form.taxBase))}</strong></div>
                <div className="payroll-line"><span>Dönem sonu kümülatif</span><strong>{formatPayrollMoney(parseNumber(form.taxBase) + (result.incomeTaxBase ?? 0))}</strong></div>
              </section>
            </div>
          </>
        ) : (
          <p className="pending-desc">Hesaplanıyor...</p>
        )}
      </aside>
    </div>
  );
}
```

- [x] **Step 3: `PayrollPage.tsx`'te bağla** — calculator placeholder'ı `<CalculatorTab />` yap + import.

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/payroll/
git commit -m "feat(frontend): tekil bordro hesaplama — server-side preview, debounce, senaryo paneli"
```

---

### Task 6: Bordro Ayarları sekmesi

**Files:**
- Create: `frontend/src/features/payroll/SettingsTab.tsx`
- Modify: `frontend/src/features/payroll/PayrollPage.tsx` (settings placeholder → `<SettingsTab />`)
- Test: `frontend/src/features/payroll/SettingsTab.test.tsx`

**Interfaces:**
- Consumes: `usePayrollSettings`, `useUpdatePayrollSettings`, `useToast`.
- Produces: `SettingsTab()`. 12 alan (eski sıra/etiket/step birebir); Kaydet → `PUT /payroll/settings` (`minWageGross = sgkBaseMin`, `taxBrackets` gönderilmez); "Varsayılana dön" → form son kaydedilen (sorgudaki) değerlere döner.

- [x] **Step 1: Başarısız testleri yaz** — `SettingsTab.test.tsx`:

```tsx
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, test, vi } from "vitest";
import { stubApi } from "../../test/apiStub";
import { renderPage } from "../../test/renderPage";
import { ToastProvider } from "../../layout/ToastProvider";
import { SettingsTab } from "./SettingsTab";

const settings = {
  year: 2026, overtimeMultiplier: 1.5, monthlyWorkingHours: 225, defaultWorkedDays: 30,
  sgkEmployeeRate: 14, unemploymentEmployeeRate: 1, sgkEmployerRate: 20.5, unemploymentEmployerRate: 2,
  stampTaxRate: 0.759, sgkBaseMin: 33030, sgkBaseMax: 297270,
  monthlyMinWageIncomeTaxExemption: 4211, monthlyMinWageStampTaxExemption: 250.7,
  minWageGross: 33030, taxBrackets: [],
};

beforeEach(() => stubApi({ "/api/payroll/settings": settings }));
afterEach(() => vi.unstubAllGlobals());

const renderTab = () =>
  renderPage(
    <ToastProvider>
      <SettingsTab />
    </ToastProvider>,
  );

test("alanlar backend ayarlarıyla dolar", async () => {
  renderTab();
  expect(await screen.findByLabelText("Fazla mesai çarpanı")).toHaveValue(1.5);
  expect(screen.getByLabelText("SGK PEK alt sınırı")).toHaveValue(33030);
});

test("kaydet PUT settings ucuna tam komutla gider", async () => {
  renderTab();
  const multiplier = await screen.findByLabelText("Fazla mesai çarpanı");
  await userEvent.clear(multiplier);
  await userEvent.type(multiplier, "2");
  await userEvent.click(screen.getByRole("button", { name: /Kaydet/ }));
  await waitFor(() => {
    const put = vi.mocked(fetch).mock.calls.find(
      ([u, i]) => String(u) === "/api/payroll/settings" && i?.method === "PUT",
    );
    expect(put).toBeTruthy();
    const body = JSON.parse(String(put![1]?.body));
    expect(body).toMatchObject({ year: 2026, overtimeMultiplier: 2, sgkBaseMin: 33030, minWageGross: 33030 });
    expect(body.taxBrackets).toBeUndefined();
  });
  expect(await screen.findByText(/Ayarlar kaydedildi/)).toBeInTheDocument();
});

test("varsayılana dön formu sorgu değerlerine geri alır", async () => {
  renderTab();
  const multiplier = await screen.findByLabelText("Fazla mesai çarpanı");
  await userEvent.clear(multiplier);
  await userEvent.type(multiplier, "3");
  await userEvent.click(screen.getByRole("button", { name: /Varsayılana dön/ }));
  expect(screen.getByLabelText("Fazla mesai çarpanı")).toHaveValue(1.5);
});
```

Run: `npm test -- --run src/features/payroll/SettingsTab.test.tsx` → FAIL.

- [x] **Step 2: `SettingsTab.tsx` yaz** (eski `renderPayrollSettingsTab` DOM paritesi)

```tsx
import { useEffect, useState } from "react";
import { ApiError } from "../../api/client";
import { useToast } from "../../layout/ToastProvider";
import { PageError, PageLoading } from "../shared/PageState";
import { usePayrollSettings, useUpdatePayrollSettings, type PayrollSettingsDto } from "./queries";

const FIELDS: [keyof PayrollSettingsDto & string, string, string][] = [
  ["overtimeMultiplier", "Fazla mesai çarpanı", "0.1"],
  ["monthlyWorkingHours", "Aylık çalışma saati", "1"],
  ["defaultWorkedDays", "Varsayılan çalışılan gün", "1"],
  ["sgkEmployeeRate", "SGK işçi oranı (%)", "0.01"],
  ["unemploymentEmployeeRate", "İşsizlik işçi oranı (%)", "0.01"],
  ["sgkEmployerRate", "SGK işveren oranı (%)", "0.01"],
  ["unemploymentEmployerRate", "İşsizlik işveren oranı (%)", "0.01"],
  ["stampTaxRate", "Damga vergisi oranı (%)", "0.001"],
  ["sgkBaseMin", "SGK PEK alt sınırı", "1"],
  ["sgkBaseMax", "SGK PEK üst sınırı", "1"],
  ["monthlyMinWageIncomeTaxExemption", "Asgari ücret GV istisnası", "0.01"],
  ["monthlyMinWageStampTaxExemption", "Asgari ücret damga istisnası", "0.01"],
];

const toForm = (data: PayrollSettingsDto): Record<string, string> =>
  Object.fromEntries(FIELDS.map(([key]) => [key, String(data[key] ?? 0)]));

const parseNumber = (value: string): number => {
  const parsed = Number(String(value ?? "").replace(",", "."));
  return Number.isFinite(parsed) ? parsed : 0;
};

export function SettingsTab() {
  const { showToast } = useToast();
  const settingsQ = usePayrollSettings(true);
  const updateSettings = useUpdatePayrollSettings();
  const [form, setForm] = useState<Record<string, string> | null>(null);
  const [feedback, setFeedback] = useState("");

  useEffect(() => {
    if (form === null && settingsQ.data) setForm(toForm(settingsQ.data));
  }, [form, settingsQ.data]);

  if (settingsQ.isPending || form === null) return <PageLoading />;
  if (settingsQ.isError) return <PageError error={settingsQ.error} />;

  const data = settingsQ.data;

  const save = async () => {
    try {
      await updateSettings.mutateAsync({
        year: data.year ?? new Date().getFullYear(),
        overtimeMultiplier: parseNumber(form.overtimeMultiplier),
        monthlyWorkingHours: parseNumber(form.monthlyWorkingHours),
        defaultWorkedDays: Math.round(parseNumber(form.defaultWorkedDays)),
        sgkEmployeeRate: parseNumber(form.sgkEmployeeRate),
        unemploymentEmployeeRate: parseNumber(form.unemploymentEmployeeRate),
        sgkEmployerRate: parseNumber(form.sgkEmployerRate),
        unemploymentEmployerRate: parseNumber(form.unemploymentEmployerRate),
        stampTaxRate: parseNumber(form.stampTaxRate),
        sgkBaseMin: parseNumber(form.sgkBaseMin),
        sgkBaseMax: parseNumber(form.sgkBaseMax),
        monthlyMinWageIncomeTaxExemption: parseNumber(form.monthlyMinWageIncomeTaxExemption),
        monthlyMinWageStampTaxExemption: parseNumber(form.monthlyMinWageStampTaxExemption),
        minWageGross: parseNumber(form.sgkBaseMin),
      });
      setFeedback("Ayarlar kaydedildi. Dönem bordrosu ve tekil hesaplama bu varsayılanları kullanacak.");
      showToast("Bordro ayarları kaydedildi.", "success");
    } catch (e) {
      showToast(e instanceof ApiError ? e.message : "Ayarlar kaydedilemedi.", "error");
    }
  };

  const reset = () => {
    setForm(toForm(data));
    setFeedback("");
    showToast("Bordro ayarları varsayılana döndürüldü.", "info");
  };

  return (
    <section className="card payroll-settings-card">
      <div className="card-header-clean">
        <div>
          <h4>Bordro Ayarları</h4>
          <p className="text-muted">Şirket varsayılanlarını düzenleyin. Ayarlar yıla göre saklanır.</p>
        </div>
        <div className="toolbar-actions">
          <button className="btn btn-secondary" onClick={reset}>
            <i aria-hidden="true" className="fa-solid fa-rotate-left" /> Varsayılana dön
          </button>
          <button className="btn btn-primary" onClick={save} disabled={updateSettings.isPending}>
            <i aria-hidden="true" className="fa-solid fa-floppy-disk" /> Kaydet
          </button>
        </div>
      </div>
      <div className="payroll-settings-grid">
        {FIELDS.map(([key, label, step]) => (
          <div key={key} className="input-group">
            <label className="input-label" htmlFor={`setting-${key}`}>{label}</label>
            <input
              id={`setting-${key}`}
              className="input-control"
              type="number"
              step={step}
              value={form[key]}
              onChange={(e) => setForm((f) => ({ ...f!, [key]: e.target.value }))}
            />
          </div>
        ))}
      </div>
      <div className="payroll-settings-feedback" aria-live="polite">{feedback}</div>
      <div className="payroll-note">
        <i aria-hidden="true" className="fa-solid fa-circle-info" />
        <span>Bu değerler dönem bordrosu ve tekil hesaplama ön hesabında kullanılır. Üretim öncesi mevzuat ve müşavir doğrulaması gerekir.</span>
      </div>
    </section>
  );
}
```

- [x] **Step 3: `PayrollPage.tsx`'te bağla** — settings placeholder'ı `<SettingsTab />` yap + import.

- [x] **Step 4: Testleri doğrula** — Run: `npm test -- --run` → tümü PASS. `npm run build` → hatasız.

- [x] **Step 5: Commit**

```bash
git add frontend/src/features/payroll/
git commit -m "feat(frontend): bordro ayarları sekmesi — yıla göre versiyonlu GET/PUT"
```

---

### Task 7: Uçtan uca duman testi + parite + günlük + kapanış

**Files:**
- Modify: `docs/gelistirme-gunlugu.md`; gerekirse küçük düzeltmeler.

- [x] **Step 1: Backend + frontend'i başlat** (önceki dilimlerle aynı: `dotnet run --project src/Ekipzen.API --launch-profile http` + `npm run dev`)

- [x] **Step 2: Duman testi** (Playwright, scratchpad script'i)

1. hr-admin → `#/payroll`: dönem yoksa boş durum; "Yeni Dönem" → bu ay → oluşur, seçilir; KPI'lar `Taslak`, satırlar aktif personelle dolu (brüt 0).
2. Tabloda "Detay" → girdi formunda brüt ücret gir (ör. 100000) → Kaydet → tablo yenilenir, net > 0.
3. "Kontrol edildi" → satır durumları `Onaya Hazır`/`Eksik Veri` olur; dönem `Kontrol`.
4. Eksik verili satırda "Onaya gönder" → 409 mesajı panelde. IBAN/puantaj işaretle + kaydet → onayla → `Onaylandı`.
5. Tüm satırları onayla → header "Onaya gönder" → dönem `Onaylandı`; onaylı satır detayında "Pusula İndir" → PDF iner.
6. `#/payroll/calculator`: personel seç → form dolar → sonuç paneli backend hesabıyla dolar; brüt değiştir → sonuç güncellenir.
7. `#/payroll/settings`: çarpanı değiştir → Kaydet → feedback metni; sayfa yenile → değer kalıcı.
8. Çalışan (`ahmet.yilmaz@hrmaster.local`) → `#/payroll`: "Bordrolarım" listesi (onaylanan dönem görünür) → PDF iner. `#/payroll/settings` → yetki ekranı.

- [x] **Step 3: Görsel parite** — eski/yeni `#/payroll` (üç sekme + detay paneli) yan yana. Bilinçli farklar: dönem seçici + "Yeni Dönem" (eskide yoktu), detay panelinde girdi formu (eskide salt-okur), çalışan görünümü "Bordrolarım" (eskide demo tablo), gerçek dönem adı. Diğer farklar DOM/class düzeltmesiyle giderilir.

- [x] **Step 4: Günlük + kapanış commit** — "Şu an neredeyiz" → Dilim 6 (İşe Alım + Uyum); Dilim 5 kaydı; plan kutuları işaretlenir.

```bash
git add frontend/ docs/
git commit -m "test(frontend): dilim 5 duman testi, parite kontrolü ve günlük güncellemesi"
```

---

## Yürütme notu (paralellik)

Task 1 ve 2 sıralı ön koşuldur. Task 3+4 (dönem+detay) ile Task 5 (tekil) ve Task 6 (ayarlar) birbirinden bağımsızdır; ancak üçü de `PayrollPage.tsx`'e placeholder değişikliğiyle dokunur — paralel yürütülecekse bağlama adımları ana oturumda yapılır.

## Sonraki dilimler

Dilim 6 (İşe Alım + Uyum) planı bu dilim main'e merge edildikten sonra yazılır.
