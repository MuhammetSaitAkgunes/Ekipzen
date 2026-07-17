# 7. Geliştirme Akışı

Bu bölüm elini kirletmen için: kurulum, çalıştırma, test, ve "yeni bir özellik
nasıl eklenir" reçetesi.

## 7.1 Önkoşullar

- **.NET 9 SDK** (backend)
- **Node.js 20+** (frontend)
- **MSSQL** — LocalDB / SQL Express yeterli (bağlantı `backend/.../appsettings*.json`)

## 7.2 Çalıştırma

İki terminal:

```bash
# 1) Backend — http://localhost:5053  (Swagger: /swagger)
cd backend
dotnet run --project src/IKPro.API --launch-profile http

# 2) Frontend — http://localhost:5173  (/api → :5053 proxy'li)
cd frontend
npm install       # ilk sefer
npm run dev
```

Tarayıcı: `http://localhost:5173` → demo hesapla giriş (`ik@hrmaster.local` /
`demo123`).

> **Neden proxy?** Frontend `/api/...` çağırır; Vite dev sunucusu bunu
> `http://localhost:5053`'e yönlendirir (`vite.config`). Böylece CORS derdi olmaz
> ve üretimde aynı origin altında sunulabilir.

Eski mock uygulamayı görmek istersen (parite referansı):
```bash
npx serve -l 4173 legacy-frontend    # http://localhost:4173
```

## 7.3 Test

```bash
cd frontend && npm test -- --run     # Vitest + RTL (birim/bileşen)
cd backend  && dotnet test           # xUnit (birim + entegrasyon)
```

- **Frontend testleri** `fetch`'i stub'lar; backend gerekmez.
- **Backend entegrasyon testleri** `WebApplicationFactory` ile API'yi bellek-içi
  ayağa kaldırır; gerçek seed'li bir test DB'sine karşı çalışır. Bu yüzden bir
  test collection'ı **paylaşımlı DB** kullanır — testler birbirinin durumunu
  kirletmemeli (ör. oluşturulan kayıtları `finally`'de temizle).

## 7.4 Tip üretimi (frontend ↔ backend sözleşmesi)

Backend'de bir DTO değiştiğinde:

```bash
# backend çalışırken:
cd frontend && npm run gen:api       # Swagger → src/api/schema.d.ts
```

Sonra `tsc` uyumsuzlukları derleme zamanında yakalar. **Backend sözleşmesi
değiştiren her PR'da bunu çalıştır.**

## 7.5 Veritabanı migration'ları

Şema değişikliği (yeni tablo/kolon):

```bash
cd backend
dotnet ef migrations add <AçıklayıcıAd> --project src/IKPro.Infrastructure --startup-project src/IKPro.API
```

SQL view/fonksiyonu değişikliği migration'ın `Up/Down` metotlarına ham SQL olarak
yazılır (örnekler: `*SqlObjects.cs`, `*View.cs`). Uygulama açılışta migration'ları
uygular.

## 7.6 Kod konvansiyonları

- **Türkçe** kullanıcı metinleri ve yorumlar; İngilizce kod tanımlayıcıları.
- **Renk/stil hardcode yok** — CSS token'ları kullan (petrol/evergreen + IBM Plex).
- **Parite** — bir ekranı değiştirirken eski mock ile görsel/DOM uyumunu koru;
  bilinçli farkları belgele.
- **Backend:** iş kuralı handler'da (controller'da değil); HTTP kodu değil anlamlı
  istisna fırlat; sorgularda `ScopeFor` ile rol kapsamı.
- **Frontend:** ağ trafiği yalnız `apiFetch`/`apiDownload`; sunucu durumu TanStack
  Query; tipler `schema.d.ts`'ten (elle DTO yazma).

## 7.7 Git akışı

- `main` korumalı hedef daldır. Her iş için ayrı dal (ör. `feature/...`, `fix/...`).
- Commit mesajları anlamlı ve konu-bazlı (`feat(...)`, `fix(...)`, `test(...)`,
  `docs(...)`). Küçük ve odaklı commit'ler tercih edilir.
- İş bitince: testler + build yeşil → `main`'e fast-forward merge → push.
- Her dilim/iş sonunda `docs/gelistirme-gunlugu.md`'deki "Şu an neredeyiz" güncellenir.

## 7.8 Reçete: Backend'e yeni bir uç eklemek

Örnek: "bir personelin unvanını güncelleyen" bir uç. Adımlar (TDD ile):

1. **Test yaz** (`tests/IKPro.Tests.Integration/...`): uca istek at, beklenen
   sonucu/yetki davranışını doğrula. (Başarısız olmalı.)
2. **Command + Validator + Handler** (`Application/Features/Employees/...`):
   ```csharp
   public sealed record UpdateTitleCommand(int Id, string Title) : IRequest<...>;
   // Validator: Title boş değil, uzunluk sınırı
   // Handler: kaydı bul (ScopeFor!), güncelle, SaveChanges, DTO döndür
   ```
3. **DTO/eşleme** varsa `...Dtos.cs`'e ekle.
4. **Controller** ucunu ekle (ince: `sender.Send(command)`), doğru policy ile.
5. `dotnet test` → yeşil. Gerekirse `npm run gen:api` (frontend kullanacaksa).

## 7.9 Reçete: Frontend'e yeni bir ekran/sorgu eklemek

1. **queries.ts**'e hook ekle: `useX = () => useQuery/useMutation(apiFetch(...))`.
   Tipleri `schema.d.ts`'ten al.
2. **Test yaz** (`*.test.tsx`): `renderPage` + `apiStub` ile; doğru uca doğru
   gövdenin gittiğini / doğru render'ı doğrula.
3. **Bileşen** yaz: `isPending`/`isError` için `PageLoading`/`PageError`; başarı/
   hata için `showToast`. Mevcut CSS class'larını kullan (parite).
4. **routes.tsx**'e (yeni rota ise) ekle: `roles` matrisi + `pageFor` eşlemesi.
5. `npm test -- --run` yeşil; `npm run build` hatasız.

## 7.10 Reçete: Kod incelemesi / kalite

`/code-review` (varsa) veya elle: değişen diff'i "kullanıma hazır ürün" gözüyle
gözden geçir — güvenlik (girdiye güvenme, yetki), iş kuralı boşlukları (durum
geçişleri, kapsam sızıntısı), kesişen kaygılar. Bulguları önceliklendir (kritik
→ kalite) ve TDD ile düzelt. Gerçek bir örnek: Bölüm 8.7'deki 6 düzeltme bu
süreçten çıktı.

---

Önceki: [← 6. Modüller](06-moduller.md) · Sonraki: [8. Kararlar →](08-kararlar-neden.md)
