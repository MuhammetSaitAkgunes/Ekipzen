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

Tek kaynak: `routes.tsx`. Hash router kullanılır (`/#/dashboard` gibi). Her route
bir **rol listesi** taşır; kullanıcının rolü uymuyorsa sayfa gizlenir.

```tsx
{ key: "recruitment", path: "/recruitment", ..., roles: ["hr-admin"] },
{ key: "leaves",      path: "/leaves",      ..., roles: ALL },
```

- `RequireAuth` → giriş yoksa `/login`'e yollar.
- `PublicOnly` → giriş/davet sayfaları (girişliyken erişilmez).
- `RouteGate` → rol matrisini uygular.

Public (girişsiz) route'lar: `/login` ve `/accept-invite` (davet kabul) — hepsi bu.
**Kayıt sayfası yoktur:** hesap operatör tarafından açılır (bkz.
[06 — Multi-Tenancy](06-multi-tenancy.md)). `/signup` ve `/register-company` eski
yer imleri için `/login`'e yönlendiren birer kalıntıdır.

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
   aynı refresh'i paylaşır. Refresh de düşerse oturumu temizleyip `/login`'e yollar.
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

Renkler ve boşluklar CSS **token**'larından (`var(--primary)` gibi) gelir; hex
kodu doğrudan yazılmaz. Bu, tema tutarlılığını (açık/koyu) korur. Ayrıntı için
`styles/main.css` ve modül CSS'leri.

## Sonraki Adım

Kimlik doğrulama akışının tamamı → [05 — Kimlik & Yetkilendirme](05-kimlik-ve-yetkilendirme.md).
