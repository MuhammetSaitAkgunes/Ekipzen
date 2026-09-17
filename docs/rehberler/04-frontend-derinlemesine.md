# 04 — Frontend Derinlemesine

React + TypeScript arayüzünün nasıl kurulduğunu ve backend ile nasıl konuştuğunu anlatır.

## Proje Haritası

```
frontend/src/
├─ main.tsx            # Uygulama giriş noktası
├─ App.tsx             # Router + sağlayıcılar (Query, Auth, Toast)
├─ routes.tsx          # TÜM route tanımları + rol matrisi + navigasyon
├─ api/
│  ├─ client.ts        # apiFetch/apiDownload — merkezi HTTP istemcisi (401→refresh)
│  ├─ session.ts       # Oturum (token/kullanıcı) localStorage yönetimi
│  └─ schema.d.ts      # Backend Swagger'ından ÜRETİLEN tipler (elle düzenleme!)
├─ auth/
│  ├─ AuthContext.tsx  # login/logout + kullanıcı durumu
│  ├─ guards.tsx       # RequireAuth, PublicOnly, RouteGate (rol kontrolü)
│  ├─ LoginPage.tsx, AcceptInvitePage.tsx   # kayıt sayfası YOK (bkz. Yönlendirme)
├─ layout/
│  ├─ AppShell.tsx     # Kenar çubuğu + üst bar (giriş yapılınca sarmalar)
│  └─ GlobalSearch.tsx # Ctrl+K arama
├─ features/<modül>/   # Her modülün sayfaları + query katmanı + testleri
└─ styles/             # CSS (tasarım token'ları; hex hardcode yok)
```

## Yönlendirme (Routing)

Tek kaynak: `routes.tsx`; tarayıcı router'ı kullanılır (`createBrowserRouter`,
`App.tsx`). Landing sayfaları kökte (`/`, `/ozellikler`, ...), uygulama sayfaları
`/app/*` altındadır (`appPath("/dashboard")` → `/app/dashboard`). Her uygulama
route'u bir **rol listesi** taşır; kullanıcının rolü uymuyorsa sayfa gizlenir.

```tsx
{ key: "recruitment", path: "/recruitment", ..., roles: ["hr-admin"] },
{ key: "leaves",      path: "/leaves",      ..., roles: ALL },
```

- `RequireAuth` → giriş yoksa `/giris`'e yollar (`LOGIN_PATH`, `paths.ts`).
- `PublicOnly` → giriş/davet sayfaları (girişliyken erişilmez).
- `RouteGate` → rol matrisini uygular.

Public (girişsiz) route'lar: `/giris` ve `/davet` (davet kabul) — hepsi bu.
**Kayıt sayfası yoktur:** hesap operatör tarafından açılır (bkz.
[06 — Multi-Tenancy](06-multi-tenancy.md)). `/signup` ve `/register-company`
eski yer imleri için `/iletisim`'e (demo talebi) yönlendiren birer kalıntıdır.

Eski hash router döneminden kalan URL'ler (`/#/accept-invite` gibi e-posta linkleri,
yer imleri, eski kök uygulama yolları) `legacyHash.ts` tarafından yeni temiz yollara
çevrilir (bkz. aşağıdaki "Tasarım sistemi ve yönlendirme" bölümü).

## Veri Çekme: TanStack Query

Bileşenler doğrudan `fetch` çağırmaz; her modülün bir **query katmanı** vardır
(`features/<modül>/queries.ts`). Örnek desen:

```ts
// Gerçek örnek: frontend/src/features/payroll/queries.ts
export const useMyPayslips = () =>
  useQuery({ queryKey: ["payroll", "my"], queryFn: () => apiFetch<MyPayslipDto[]>("/payroll/my") });
```

TanStack Query önbellek, yeniden çekme ve yükleniyor/hata durumlarını yönetir.
Bileşen sadece `isPending`/`isError`/`data`'yı kullanır (`features/shared/PageState.tsx`
ortak yükleniyor/hata bileşenlerini sağlar).

## Merkezi API İstemcisi: `api/client.ts`

Tüm istekler `apiFetch` (JSON) ve `apiDownload` (dosya/PDF) üzerinden geçer. Bu
istemci üç işi tek yerde halleder:

1. **JWT ekleme:** Oturumdaki `Authorization: Bearer <token>` başlığını otomatik ekler.
2. **401 → tek-uçuş refresh → tek retry:** Erişim token'ı süresi dolmuşsa, tek bir
   `/auth/refresh` çağrısıyla yeniler ve isteği bir kez tekrarlar. Eşzamanlı 401'ler
   aynı refresh'i paylaşır. Refresh de düşerse (veya retry hâlâ 401 dönerse) oturum
   temizlenir; `RequireAuth` kullanıcıyı `/giris`'e indirir.
3. **Hata normalizasyonu:** Başarısız yanıtları `ApiError`'a çevirir (backend
   `ProblemDetails` başlığını mesaj olarak taşır).

```ts
// Kullanım — bileşenden değil, query katmanından çağrılır:
const data = await apiFetch<DepartmentDto[]>("/departments");
const { blob, fileName } = await apiDownload("/me/data-export");
```

## Kimlik: `AuthContext`

`AuthProvider` login/register/logout sağlar ve kullanıcıyı tutar. Oturum
(`token`, `refreshToken`, `user`) `localStorage`'da saklanır (`api/session.ts`).
Giriş başarılıysa kullanıcı rolüne göre ana sayfaya yönlendirilir
(`roleHomeFor`: employee → `/overview`, diğerleri → `/dashboard`).

## Tipler: Elle Yazma, Üret

`api/schema.d.ts` backend Swagger'ından **otomatik üretilir**:

```bash
# Backend çalışırken:
cd frontend && npm run gen:api
```

Backend'de bir DTO değişince bu komutu çalıştır — frontend tipleri güncellenir,
uyumsuzluk derleme anında yakalanır. **`schema.d.ts`'i elle düzenleme.**

## Stil / Tasarım Sistemi

Renkler ve boşluklar CSS **token**'larından gelir; hex kodu doğrudan yazılmaz
(istisna: statik `public/favicon.svg`). Bu, tema tutarlılığını (açık/koyu) korur.
Ayrıntı için `styles/main.css`, `styles/tokens.css` ve modül CSS'leri; katman
sırası bir sonraki bölümde.

## Tasarım sistemi ve yönlendirme (2026-09)

**Token katmanları** (üç seviye, alttan üste):

1. `styles/tokens.css` — ham renk **ramp**'leri (`--indigo-700: #4338ca`,
   `--slate-900`, `--emerald-600` gibi; öneksiz) ve **amaç token'ları**
   (`--ez-*` önekli: `--ez-bg`, `--ez-surface`, `--ez-text`, `--ez-primary`,
   `--ez-border`, `--ez-nav-bg`, ...). `--ez-` öneki zorunludur çünkü `--text`,
   `--surface` gibi öneksiz adlar eski `main.css` değişkenleriyle çakışır.
2. `styles/main.css` bu `--ez-*` token'larına **alias** tanımlar (`--primary:
   var(--ez-primary)`, `--surface: var(--ez-surface)`, `--line-soft:
   var(--ez-border-soft)` gibi) — modüllerin eski kısa adları kullanmaya devam
   etmesi içindir.
3. **Yeni yazılan kod her zaman `--ez-*` token'larını kullanır**, alias'ları
   değil. Grafik renkleri (`chartToken`, `features/shared/chartSetup.ts`)
   `getComputedStyle` ile bu token'ları çalışma zamanında okur; modül
   yüklenirken (component dışı, üst seviye sabitler gibi) hesaplanan renkler
   DOM stilleri henüz uygulanmamış olabileceğinden sabit hex kullanır (örnek:
   `features/overview/OverviewCharts.tsx` içindeki `DEPT_COLORS`).

**Paylaşılan bileşenler** `frontend/src/ui/` altındadır (`Button`, `Card`,
`Field`, `Logo`, `controls`, `usePageMeta`) ve CSS sınıfları `ez-` önekini
taşır (`ez-btn`, `ez-card`, `ez-logo`, ...). `Logo` bileşeni `variant`
(`"full" | "mark"`) ve `tone` (`"dark" | "light"`) prop'larını alır; uygulama
kabuğunda (`layout/AppShell.tsx`) daraltılmış sidebar hâlâ `.sidebar-collapsed
.ez-logo` seçicisiyle 32px logo işaretini gösterir.

**Yönlendirme:** tarayıcı router'ı kullanılır; landing sayfaları kökte (`/`,
`/ozellikler`, `/fiyatlandirma`, ...), uygulama sayfaları `/app/*` altındadır
(`paths.ts`: `APP_BASE`, `appPath`, `LOGIN_PATH` = `/giris`, `INVITE_PATH` =
`/davet`). `legacyHash.ts` eski hash router URL'lerini (`#/dashboard`,
`#/accept-invite?...`) ve eski kök uygulama yollarını (`/dashboard`,
`/overview`, ...) yeni temiz yollara çevirir; bu, e-postalardaki eski davet
linklerinin ve tarayıcı yer imlerinin kırılmamasını sağlar.

**Landing içeriği** `frontend/src/marketing/content.ts` dosyasından
düzenlenir (nav linkleri, bölüm metinleri, fiyatlandırma paketleri, KVKK
metni gibi); sayfa bileşenleri (`marketing/sections.tsx`,
`MarketingLayout.tsx`) bu veriyi render eder — metin değişikliği için
component kodunu değiştirmeye gerek yoktur.

**Demo talebi akışı:** `/iletisim` sayfasındaki form `POST
/api/public/demo-requests`'e gönderilir (kimlik doğrulama gerektirmez, kayıt
oluşturmaz — yalnız `DemoRequests:NotifyEmail` adresine bildirim e-postası
üretir). Uç, başarıda `204 No Content` döner (spec'teki 202'den bilinçli
sapma); form isteği aşırı sıklıkta yapılırsa oran sınırlama politikası 429
döner.

**Statik varlıklar / hosting:** production'da backend `PilotHosting.
MapSpaFallback` (`backend/src/Ekipzen.API/Hosting/PilotHosting.cs`) `/api` ve
`/health` dışındaki, uzantısız tüm yolları `index.html`'e düşürür — bu,
`/ozellikler` gibi temiz landing yollarının ve `/app/dashboard` gibi SPA
yollarının doğrudan URL ile açılabilmesini sağlar. `frontend/public/`
altındaki `favicon.svg`, `robots.txt` ve `sitemap.xml` build sırasında
`frontend/dist/` köküne kopyalanır.

## Sonraki Adım

Kimlik doğrulama akışının tamamı → [05 — Kimlik & Yetkilendirme](05-kimlik-ve-yetkilendirme.md).
