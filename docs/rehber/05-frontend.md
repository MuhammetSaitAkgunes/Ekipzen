# 5. Frontend Mimarisi

Frontend, `frontend/` klasöründe React 18 + TypeScript + Vite ile yazılmıştır.
Bu bölüm "kod nasıl organize, veri nasıl akıyor, yeni ekran nasıl eklenir"
sorularını yanıtlar.

## 5.1 Klasör turu

```
frontend/src/
├── api/         client.ts (fetch sarmalayıcı), session.ts, schema.d.ts (üretilen tipler)
├── auth/        AuthContext, guards (rota koruması), LoginPage
├── layout/      AppShell (sidebar/header), GlobalSearch, ToastProvider
├── features/    her modül burada (queries.ts + sayfalar + modallar)
├── pages/       PlaceholderPage (henüz portlanmamış rota yer tutucusu)
├── shared→features/shared/  csv, chartSetup, PageState (loading/error)
├── styles/      eski uygulamadan BİREBİR kopyalanan CSS (parite)
├── test/        apiStub, renderPage, setup (test altyapısı)
├── routes.tsx   rota + rol matrisi + sayfa eşlemesi
├── App.tsx / main.tsx  uygulama kökü
```

Bir modül (ör. `features/leaves/`) tipik olarak şunları içerir:
- `queries.ts` — o modülün tüm API çağrıları (TanStack Query hook'ları),
- `format.ts` — biçimlendirme/etiket yardımcıları,
- `<Modül>Page.tsx` — ana sayfa,
- `<Modül>Modal.tsx` — form/pencere bileşenleri,
- `*.test.tsx` — Vitest + React Testing Library testleri.

> **Neden "feature-folder" (modül bazlı klasör)?** Alternatif "tip bazlı"
> (`components/`, `hooks/`, `services/`) klasörlemedir. Modül bazlıda bir özelliğe
> dokunmak için tek klasöre bakarsın; ilgili her şey (sorgu, biçim, ekran, test)
> yan yana. Büyük uygulamalarda gezinmesi çok daha kolay.

## 5.2 Veri akışı: `apiFetch` → TanStack Query → bileşen

Frontend'de iki tür durum vardır:
- **Sunucu durumu** (server state) — backend'den gelen veri (personel listesi,
  izin bakiyesi…). Bunu **TanStack Query** yönetir.
- **UI durumu** — modal açık mı, hangi sekme seçili… Bunu `useState` yönetir.

> **Neden TanStack Query?** Sunucu verisini elle `useState` + `useEffect` ile
> çekmek; loading/error/cache/yeniden-çekme/invalidation'ı her sayfada tekrar
> yazmak demektir. TanStack Query bunların hepsini hazır verir. "Mutasyon sonrası
> ilgili listeyi tazele" tek satır (`invalidateQueries`).

Örnek desen (`features/leaves/queries.ts`):

```ts
// Okuma: bakiye kartı
export const useLeaveBalance = () =>
  useQuery({ queryKey: ["leaves","balance"], queryFn: () => apiFetch("/leaves/balance") });

// Yazma: talep oluştur; başarınca izin sorgularını tazele
export const useCreateLeave = () => {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => apiFetch("/leaves", { method:"POST", body: JSON.stringify(body) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["leaves"] }),
  });
};
```

Sayfa bileşeni sadece `const balance = useLeaveBalance()` der; `balance.isPending`,
`balance.isError`, `balance.data` hazır gelir. `shared/PageState.tsx` içindeki
`PageLoading`/`PageError` bileşenleri bu durumları tutarlı gösterir.

## 5.3 API istemcisi: `api/client.ts`

Tüm ağ trafiği tek kapıdan geçer — asla ham `fetch` kullanılmaz. `apiFetch`:
- `Authorization: Bearer <token>` başlığını ekler (varsa),
- JSON gövdelere `Content-Type` ekler (FormData'ya eklemez — dosya yükleme için),
- 401'de **sessiz refresh + tekrar** (bkz. 4.1),
- hatayı `ApiError`'a çevirir, 204'te `null` döner.

`apiDownload` ise ikili (PDF/evrak) indirmeler için aynı token/refresh mantığıyla
`blob` + dosya adı döndürür.

> **Neden tek istemci?** Token ekleme, refresh, hata çevirme gibi kesişen kaygılar
> tek yerde. Yeni bir uç eklerken bunları düşünmezsin — "bedava" gelir.

## 5.4 Tipler backend'den üretilir: `gen:api`

`frontend/src/api/schema.d.ts` **elle yazılmaz**; backend Swagger'ından üretilir:

```bash
npm run gen:api    # backend çalışırken: Swagger → schema.d.ts
```

Sorgu katmanı bu tipleri kullanır: `components["schemas"]["LeaveRequestDto"]`.

> **Neden otomatik üretim?** Backend DTO'su değişince (alan eklendi/silindi),
> `gen:api` çalıştırınca frontend tipleri **derleme zamanında** uyumsuzluğu yakalar.
> Elle senkron tutmak hata kaynağıdır. Örnek: işe alım düzeltmesinde hire gövdesine
> `email` eklendi → `gen:api` → `schema.d.ts` güncellendi → tsc yeşil.

## 5.5 Kimlik ve rota koruması

- `auth/AuthContext.tsx` — `login`, `register`, `logout` ve o anki `user`.
- `auth/guards.tsx` — `RequireAuth` (giriş yoksa /login), `RouteGate` (rol yetmezse
  "yetki gerekli" ekranı; **yönlendirmez**, kilit gösterir).
- `routes.tsx` — tek dosyada: rota listesi + her rotanın `roles`'ı + `pageFor`
  (rota→bileşen eşlemesi). Eski `routes.js` ile **birebir**.

## 5.6 Layout ve global özellikler

- `layout/AppShell.tsx` — sol menü (rol bazlı görünürlük), üst bar, tema, açık-
  aksiyon rozeti (`GET /actions/badge`).
- `layout/GlobalSearch.tsx` — Ctrl+K arama; sayfa sonuçları (anlık) + gerçek
  `GET /search` sonuçları (300ms debounce; personel/aksiyon/aday, rol kapsamlı).
- `layout/ToastProvider.tsx` — başarı/hata bildirimleri; her sayfa `showToast(...)`.

## 5.7 Tasarım: token'lar ve parite

CSS `frontend/src/styles/` altında ve eski uygulamadan **birebir kopyalanmıştır**.
Renkler **hardcode edilmez**, CSS değişkenlerinden (token) gelir: petrol/evergreen
paleti + IBM Plex tipografisi.

> **Neden "parite"?** React portunun ana kuralı: her ekran eski mock uygulamayla
> **piksel-piksel aynı** görünmeli. Bu yüzden CSS kopyalandı, class adları/DOM
> yapısı korundu. Bilinçli farklar (ör. eskisinde olmayan "Yeni Dönem" düğmesi)
> her dilim planında açıkça listelenir. Doğrulama: eski/yeni ekran yan yana
> ekran görüntüsü karşılaştırması.

## 5.8 Test yaklaşımı

- **Araç:** Vitest + React Testing Library (RTL).
- **Altyapı:** `test/renderPage.tsx` (QueryClient + Router sarmalar),
  `test/apiStub.ts` (`fetch`'i stub'lar — yol→yanıt eşlemesi).
- **Ne test edilir:** rol bazlı görünürlük, form doğrulama, doğru uca doğru
  gövdenin gittiği (mutasyonlar), boş/hata durumları.

Örnek bir tuzak (gerçek): RTL'de `getByText("Ahmet Yılmaz")` aynı metin birden
çok yerde geçince (panel başlığı + pusula önizleme) patlar; `getByRole("heading",
{name})` veya `getAllByText` kullanılır. Bu tür incelikler test dosyalarında
yorumlanmıştır.

> **Neden bu kadar test?** Port TDD ile yürütüldü: önce başarısız test, sonra
> kod. 131 frontend + 80 backend test, bir değişikliğin başka bir yeri kırıp
> kırmadığını saniyeler içinde söyler. Bu, "güvenle değiştirebilme" özgürlüğüdür.

---

Önceki: [← 4. Kimlik & Güvenlik](04-kimlik-guvenlik-hata.md) · Sonraki: [6. Modüller →](06-moduller.md)
