# Ürün Sağlamlığı Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ekipzen'in gerçek kullanımda veri kaybetmeden, sessizce başarısız olmadan ve yavaşlamadan ayakta kalmasını sağlayacak dört somut iyileştirmeyi uygulamak.

**Architecture:** Dört bağımsız iş: (1) uyarı hijyeni + CI'da uyarıları hata sayma, (2) istemci sorgu politikası, (3) rota bazlı kod bölme, (4) yedekleme + geri yükleme tatbikatı. Hiçbiri diğerine bağlı değil; her biri tek başına test edilip commit'lenebilir.

**Tech Stack:** .NET 9, EF Core, React 18 + Vite + TanStack Query, GitHub Actions, SQL Server.

## Global Constraints

- Mevcut testler kırılmayacak: backend 122/122, frontend 141/141 yeşil kalacak.
- Her değişiklik TDD ile: önce kırmızı test, sonra minimum kod.
- Bordro hesap mantığına DOKUNULMAYACAK (mali müşavir doğrulaması bekliyor).
- Yıkıcı veritabanı işlemi yok: geri yükleme tatbikatı yalnız ayrı bir isme yapılır, mevcut veritabanı asla üzerine yazılmaz.
- Her görev ayrı commit.

---

## Uygulama durumu (2026-08-05)

| Görev | Durum | Kanıt |
| --- | --- | --- |
| 1 · Uyarı hijyeni + CI'da uyarı = hata | ✅ Tamam | `dotnet build -t:Rebuild` → 0 uyarı; Release + `-warnaserror` temiz; backend 122/122 |
| 2 · Sorgu retry/tazelik politikası | ✅ Tamam | 4 yeni test; frontend 145/145; tsc + oxlint temiz |
| 3 · Rota bazlı kod bölme | ✅ Tamam | Ana chunk 686,76 → 266,23 kB (gzip 200,76 → 84,70); tarayıcıda 4 rota gezildi |
| 4 · Yedekleme + geri yükleme tatbikatı | ⚠️ Yazıldı, **koşulmadı** | Script ve runbook hazır; tatbikat ortam kesintisi nedeniyle çalıştırılamadı |

**Görev 4 açık kalan tek adım:** tatbikatın gerçekten koşturulup çıkış kodunun 0
döndüğünün görülmesi. Koşulmamış kurtarma scripti, kurtarma garantisi vermez.

```powershell
pwsh scripts/backup-restore-drill.ps1 -Database EkipzenDb -BackupPath $env:TEMP\ekipzen-yedek
```

---

## Önceliklendirme gerekçesi

Sağlamlık ekseninde sıralama (satılabilirlikten farklı):

| # | Madde | Neden bu sırada |
| --- | --- | --- |
| 1 | Yedekleme + geri yükleme tatbikatı | Veri kaybı geri dönüşü olmayan tek hatadır. Yedeğin var olması yetmez; geri yüklenebildiği kanıtlanmalı. |
| 2 | CI'ın gerçekten koşması | Diğer her iyileştirmenin doğrulanma zemini. Koşmayan hat, olmayan hattır. |
| 3 | Uyarı hijyeni → uyarı = hata | Gürültü gerçek uyarıyı gizler. Temizlenince CI kalite tabanını kalıcı yükseltir. |
| 4 | Sorgu politikası (retry/staleTime) | Kullanıcının gördüğü kararlılık: hata sonrası 7 sn bekleme, sekme dönüşünde refetch fırtınası. |
| 5 | Kod bölme | İlk açılış performansı; sağlamlıktan çok deneyim ama ucuz. |

**Bu planın kapsamı: 1, 3, 4, 5.** Madde 2 (CI'ın koşması) `git push` gerektirir — dışa açık işlem, kullanıcı onayına bağlı.

**Kapsam dışı (karar/kimlik bilgisi gerektiriyor):** MFA, Excel içe aktarma, Sentry/APM entegrasyonu, faturalandırma, e-Bildirge. Gerekçeler planın sonunda.

---

## File Structure

| Dosya | Sorumluluk |
| --- | --- |
| `backend/Directory.Build.props` | Derleme çapında uyarı politikası (değiştir) |
| `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs` | EF1002 gerekçeli bastırma (değiştir) |
| `backend/src/Ekipzen.API/Controllers/*.cs` | Bozuk XML yorumları (değiştir) |
| `.github/workflows/ci.yml` | Uyarı = hata adımı (değiştir) |
| `frontend/src/queryClient.ts` | Sorgu varsayılan politikası (oluştur) |
| `frontend/src/queryClient.test.ts` | Politika testi (oluştur) |
| `frontend/src/App.tsx` | Politikayı kullan (değiştir) |
| `frontend/src/routes.tsx` | Rota bazlı lazy import (değiştir) |
| `scripts/backup-restore-drill.ps1` | Yedek al + ayrı isme geri yükle + doğrula (oluştur) |
| `docs/yedekleme-ve-kurtarma.md` | Runbook: RPO/RTO, tatbikat, sorumluluk (oluştur) |

---

### Task 1: Uyarı hijyeni ve CI'da uyarı = hata

**Files:**
- Modify: `backend/src/Ekipzen.Infrastructure/Persistence/TenantPurger.cs`
- Modify: `backend/src/Ekipzen.API/Controllers/*.cs` (CS1570 geçen dosyalar)
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: yok
- Produces: yok (derleme davranışı değişir)

- [ ] **Step 1: Uyarı envanterini çıkar**

Run: `cd backend && dotnet build 2>&1 | grep -oE "warning [A-Z]+[0-9]+" | sort | uniq -c | sort -rn`
Expected: `CS1570` ve `EF1002` satırları görünür; sayıları not edilir.

- [ ] **Step 2: EF1002'yi gerekçeyle bastır**

`TenantPurger.cs` içinde DELETE döngüsünü saran bölgeye:

```csharp
// EF1002 bastırma gerekçesi: tenantId parametre olarak geçiliyor ({0} + object[]),
// enterpole edilen tek şey EF metadata'sından gelen tablo adları — tablo adı SQL'de
// parametreleştirilemez. Kullanıcı girdisi SQL'e girmiyor.
#pragma warning disable EF1002
        // ... mevcut ExecuteSqlRawAsync çağrıları ...
#pragma warning restore EF1002
```

- [ ] **Step 3: CS1570'leri düzelt**

Bozuk XML yorumları `<` ve `&` kaçışsız kullanımından kaynaklanır. Örnek düzeltme:

```csharp
/// <summary>Aksiyonlar — routes.js /actions (tüm roller) &amp; /risk/action-center.</summary>
```

- [ ] **Step 4: Uyarıların sıfırlandığını doğrula**

Run: `cd backend && dotnet build 2>&1 | grep -c "warning"`
Expected: `0`

- [ ] **Step 5: CI'da uyarıyı hata say**

`.github/workflows/ci.yml` backend derleme adımını değiştir:

```yaml
      - run: dotnet build --no-restore --configuration Release -warnaserror
```

- [ ] **Step 6: Testleri koştur**

Run: `cd backend && dotnet build --configuration Release -warnaserror && dotnet test`
Expected: derleme uyarısız, 122/122 geçer.

- [ ] **Step 7: Commit**

```bash
git add backend .github
git commit -m "build: derleme uyarılarını temizle ve CI'da uyarıyı hata say"
```

---

### Task 2: Sorgu varsayılan politikası

**Files:**
- Create: `frontend/src/queryClient.ts`
- Create: `frontend/src/queryClient.test.ts`
- Modify: `frontend/src/App.tsx`

**Interfaces:**
- Consumes: yok
- Produces: `createQueryClient(): QueryClient` — App.tsx ve testler bunu kullanır.

**Politika gerekçesi:** Varsayılan `retry: 3` üstel geri çekilmeyle ~7 saniye sürer; kullanıcı hatayı geç görür. 4xx'te retry anlamsızdır (istek yanlış, tekrarı da yanlış). `staleTime: 0` ise her odaklanmada refetch tetikler.

- [ ] **Step 1: Failing test yaz**

`frontend/src/queryClient.test.ts`:

```ts
import { expect, test } from "vitest";
import { ApiError } from "./api/client";
import { createQueryClient } from "./queryClient";

const retryOf = (client: ReturnType<typeof createQueryClient>) =>
  client.getDefaultOptions().queries?.retry as
    (failureCount: number, error: unknown) => boolean;

test("istemci hatalarında (4xx) yeniden denenmez", () => {
  const retry = retryOf(createQueryClient());
  expect(retry(0, new ApiError(404, "Bulunamadı"))).toBe(false);
  expect(retry(0, new ApiError(403, "Yetkisiz"))).toBe(false);
});

test("sunucu hatalarında sınırlı sayıda yeniden denenir", () => {
  const retry = retryOf(createQueryClient());
  expect(retry(0, new ApiError(500, "Sunucu hatası"))).toBe(true);
  expect(retry(2, new ApiError(500, "Sunucu hatası"))).toBe(false);
});

test("veriler kısa süre taze sayılır (odak değişiminde refetch fırtınası olmaz)", () => {
  const client = createQueryClient();
  expect(client.getDefaultOptions().queries?.staleTime).toBe(30_000);
});
```

- [ ] **Step 2: Testin başarısız olduğunu gör**

Run: `cd frontend && npx vitest run src/queryClient.test.ts`
Expected: FAIL — `Failed to resolve import "./queryClient"`

- [ ] **Step 3: Minimum implementasyon**

`frontend/src/queryClient.ts`:

```ts
import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "./api/client";

/**
 * Sorgu varsayılanları tek yerde. Gerekçeler:
 * - 4xx: istek hatalı; tekrarı da hatalı olur, kullanıcıyı bekletir.
 * - 5xx: geçici olabilir; 2 deneme yeter (varsayılan 3 ~7 sn sürüyordu).
 * - staleTime 30 sn: sekmeye her dönüşte tüm ekranın yeniden yüklenmesini önler.
 */
export const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        retry: (failureCount: number, error: unknown) => {
          if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
          return failureCount < 2;
        },
      },
    },
  });
```

- [ ] **Step 4: Testin geçtiğini gör**

Run: `cd frontend && npx vitest run src/queryClient.test.ts`
Expected: PASS (3 test)

- [ ] **Step 5: App.tsx'i bağla**

`frontend/src/App.tsx` içinde `const queryClient = new QueryClient();` satırını değiştir:

```tsx
import { createQueryClient } from "./queryClient";
const queryClient = createQueryClient();
```

- [ ] **Step 6: Tüm paketi koştur**

Run: `cd frontend && npx vitest run && npx tsc -b && npx oxlint`
Expected: 144/144 geçer, tip ve lint temiz.

- [ ] **Step 7: Commit**

```bash
git add frontend/src/queryClient.ts frontend/src/queryClient.test.ts frontend/src/App.tsx
git commit -m "feat(frontend): sorgu retry ve tazelik politikası"
```

---

### Task 3: Rota bazlı kod bölme

**Files:**
- Modify: `frontend/src/routes.tsx`

**Interfaces:**
- Consumes: yok
- Produces: `buildRouteObjects()` imzası değişmez; sayfalar `React.lazy` ile yüklenir.

**Not:** `routes.tsx` şu an 16 sayfayı eager import ediyor; Chart.js dahil her şey tek bundle'da. Rota elemanları `<Suspense>` ile sarılacak, fallback mevcut `PageLoading`.

- [ ] **Step 1: Mevcut bundle boyutunu ölç (referans)**

Run: `cd frontend && npx vite build 2>&1 | tail -15`
Expected: tek büyük JS chunk; boyutu not et.

- [ ] **Step 2: Sayfaları lazy'ye çevir**

`routes.tsx` içindeki sayfa importlarını değiştir (auth sayfaları hariç — ilk ekran):

```tsx
import { Suspense, lazy } from "react";
const OverviewPage = lazy(() => import("./features/overview/OverviewPage").then(m => ({ default: m.OverviewPage })));
// ... her sayfa için aynı kalıp
```

`GatedPage` içindeki render'ı sar:

```tsx
return (
  <RouteGate route={route}>
    <Suspense fallback={<PageLoading />}>
      <Page />
    </Suspense>
  </RouteGate>
);
```

- [ ] **Step 3: Testlerin hâlâ geçtiğini gör**

Run: `cd frontend && npx vitest run`
Expected: 144/144 geçer (lazy sayfalar Suspense ile çözülür).

- [ ] **Step 4: Bundle'ın bölündüğünü doğrula**

Run: `cd frontend && npx vite build 2>&1 | tail -25`
Expected: birden çok chunk; ana chunk Step 1'dekinden belirgin küçük.

- [ ] **Step 5: Tarayıcıda duman testi**

Giriş yap → `/dashboard` açılır → `/personnel`'e geç → konsolda hata yok.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/routes.tsx
git commit -m "perf(frontend): rota bazlı kod bölme"
```

---

### Task 4: Yedekleme ve geri yükleme tatbikatı

**Files:**
- Create: `scripts/backup-restore-drill.ps1`
- Create: `docs/yedekleme-ve-kurtarma.md`

**Interfaces:**
- Consumes: yok
- Produces: `backup-restore-drill.ps1 -Database <ad> -BackupPath <klasör>` — çıkış kodu 0 = tatbikat başarılı.

**Güvenlik kuralı:** Script geri yüklemeyi **her zaman** `<ad>_RestoreDrill` adına yapar ve bu adın kaynak veritabanıyla aynı olmasını reddeder. Mevcut veri asla üzerine yazılmaz.

- [ ] **Step 1: Script'i yaz**

`scripts/backup-restore-drill.ps1`: tam yedek al → ayrı isme geri yükle → satır sayısı karşılaştır → tatbikat kopyasını düşür. (Tam içerik uygulama sırasında yazılır; sözleşme: aynı ada geri yükleme reddedilir, doğrulama başarısızsa çıkış kodu 1.)

- [ ] **Step 2: Tatbikatı gerçekten koştur**

Run: `pwsh scripts/backup-restore-drill.ps1 -Database EkipzenDb -BackupPath $env:TEMP\ekipzen-backup`
Expected: çıkış kodu 0; "tatbikat başarılı" ve karşılaştırılan tablo satır sayıları yazılır.

- [ ] **Step 3: Koruma kuralını doğrula**

Run: aynı script'i kaynakla aynı hedef adı zorlayacak şekilde çağır.
Expected: hata mesajıyla reddeder, çıkış kodu 1, veritabanına dokunmaz.

- [ ] **Step 4: Runbook'u yaz**

`docs/yedekleme-ve-kurtarma.md`: RPO/RTO hedefleri, yedek sıklığı, saklama süresi, nereye saklanacağı (üretimde ayrı fiziksel konum), tatbikat sıklığı (en az 3 ayda bir), kimin sorumlu olduğu, KVKK notu (yedekler de kişisel veri içerir).

- [ ] **Step 5: Commit**

```bash
git add scripts docs/yedekleme-ve-kurtarma.md
git commit -m "ops: yedekleme + geri yükleme tatbikatı ve runbook"
```

---

## Kapsam dışı bırakılanlar ve gerekçeleri

| Madde | Neden bu planda yok | Ne gerekiyor |
| --- | --- | --- |
| CI'ın GitHub'da koşması | `git push` dışa açık işlem, onayınıza bağlı | "push et" talimatı |
| Sentry/APM entegrasyonu | Harici hesap ve DSN gerektirir | Hesap açılması + DSN |
| MFA | Tasarım kararları var (TOTP mu SMS mi, kurtarma kodu akışı, zorunlu mu opsiyonel mi) | Önce brainstorming |
| Excel içe aktarma | Sütun eşleme, hata raporu, kuru çalıştırma tasarımı gerektirir | Önce brainstorming |
| Faturalandırma | Ödeme sağlayıcı hesabı ve sözleşme | Ticari karar |
| e-Bildirge | Bordro doğruluğuna bağlı | Mali müşavir dönüşü |
