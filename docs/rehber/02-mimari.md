# 2. Mimari

Bu bölüm "kod nerede, katmanlar birbirine nasıl bağlanır, bir istek uçtan uca
ne yaşar" sorularını yanıtlar.

## 2.1 Monorepo

Backend ve frontend **aynı depoda** (monorepo) durur. Ayrı repolar da olabilirdi.

> **Neden monorepo?** Frontend'in tipleri backend'in Swagger şemasından üretiliyor
> (`npm run gen:api`). Backend sözleşmesi değişince frontend tiplerini aynı commit'te
> güncellemek, iki repo arasında sürüm koştururmaktan çok daha kolay. Tek "kaldığımız
> yer" günlüğü, tek dilim planı seti, tek PR akışı.

## 2.2 Backend: Clean Architecture (Temiz Mimari)

Backend dört projeye bölünür. Kural basit ama güçlü: **bağımlılıklar hep içeriye
doğru akar.** Dış katman iç katmanı bilir, tersi asla olmaz.

```
   ┌─────────────────────────────────────────────┐
   │  IKPro.API           (en dış — HTTP/Swagger) │
   │  ┌───────────────────────────────────────┐   │
   │  │  IKPro.Infrastructure  (DB, JWT, PDF)  │   │
   │  │  ┌─────────────────────────────────┐   │   │
   │  │  │  IKPro.Application  (iş mantığı) │   │   │
   │  │  │  ┌───────────────────────────┐  │   │   │
   │  │  │  │  IKPro.Domain (çekirdek)  │  │   │   │
   │  │  │  └───────────────────────────┘  │   │   │
   │  │  └─────────────────────────────────┘   │   │
   │  └───────────────────────────────────────┘   │
   └─────────────────────────────────────────────┘
        bağımlılık yönü:  API → Infra → App → Domain
```

### Katman katman

**IKPro.Domain** — Çekirdek. Varlıklar (`Entities/`), enum'lar (`Enums/`), sabitler
(`Constants/`: roller), okuma modelleri (`ReadModels/`: SQL view karşılıkları) ve
**saf iş kuralı** motorları (`Services/PayrollEngine.cs`). Hiçbir dış paketi
(EF Core, ASP.NET) bilmez — sadece C#. Bu yüzden **test etmesi en kolay** katmandır.

> **Neden bordro motoru Domain'de?** Bordro hesabı, veritabanından veya HTTP'den
> tamamen bağımsız saf bir fonksiyondur: gir "brüt maaş + parametreler", çık
> "net maaş + kesintiler". Onu Domain'e koyunca hiçbir mock'a gerek kalmadan
> yüzlerce parite testi yazabiliyoruz (bkz. `IKPro.Tests.Unit`).

**IKPro.Application** — İş mantığının orkestrası. Her özellik `Features/<Modül>/`
altında. Deseni **CQRS benzeri** (MediatR ile): her işlem bir **Command** (yazma)
veya **Query** (okuma) + bir **Handler**. Örn. `Features/Leaves/Commands/CreateLeaveRequestCommand.cs`.
Katman, veritabanına `IApplicationDbContext` arayüzü (interface) üzerinden erişir —
somut EF Core sınıfını görmez.

> **Neden MediatR / Command-Handler?** Controller'lar ince kalır (yalnız "isteği
> al, komutu gönder, sonucu döndür"). Her iş kuralı tek bir handler'da toplanır,
> tek başına test edilir. Doğrulama, loglama gibi kesişen kaygılar **pipeline
> behavior**'larla (bkz. 2.4) merkezi eklenir.

**IKPro.Infrastructure** — Dış dünya implementasyonları. EF Core `AppDbContext`,
migration'lar ve SQL view/fonksiyonları (`Persistence/`), ASP.NET Identity + JWT
(`Identity/`), QuestPDF pusula üretici (`Pdf/`), e-posta soyutlaması (`Email/`),
dosya deposu (`Storage/`). Application'daki arayüzlerin gerçek karşılıkları buradadır.

**IKPro.API** — En dış kabuk. Controller'lar (`Controllers/`), global hata
yakalayıcı (`Middleware/GlobalExceptionHandler.cs`), `Program.cs` (DI kurulumu,
kimlik doğrulama, Swagger). HTTP'yi bilen tek katman.

### Bağımlılık kuralının pratik faydası

`ScopeFor` (rol kapsamı) Application'da yazılır ama EF Core'un `IQueryable`'ı
üzerinde çalışır — çünkü Application "veritabanı" kavramını bir arayüz olarak
görür, somut motoru değil. Yarın MSSQL yerine PostgreSQL'e geçsek Domain ve
Application **hiç değişmez**; sadece Infrastructure değişir. Test ederken de
gerçek DB yerine sahte bir context takabiliriz.

## 2.3 Bir isteğin uçtan uca yolculuğu

Somut örnek: **çalışan "izin talebi oluştur" düğmesine basıyor.**

```
1. [Frontend] LeaveRequestModal → useCreateLeave() mutation
2. [Frontend] apiFetch("/leaves", {method:"POST", body}) 
               → Authorization: Bearer <token> başlığı eklenir
3. [Vite dev proxy] /api/leaves → http://localhost:5053/api/leaves
4. [API] LeavesController.Create(command)  — ince, sadece sender.Send(command)
5. [App pipeline] ValidationBehavior → FluentValidation kurallarını çalıştırır
                  (tarih sırası, açıklama uzunluğu). Hatalıysa burada durur → 400.
6. [App] CreateLeaveRequestCommandHandler:
         - RequireEmployeeId (kullanıcının personel bağı var mı?)
         - iş günü sayısını SQL fonksiyonuyla hesapla
         - çakışma kontrolü, bakiye kontrolü (LeaveGuards.EnsureBalanceAsync)
         - LeaveRequest kaydını ekle, SaveChanges
         - bildirim tetikle (INotificationTrigger)
7. [Infra] AppDbContext EF Core → MSSQL'e INSERT
8. [API] sonuç DTO'su JSON olarak döner (201 Created)
9. [Frontend] TanStack Query ilgili sorguları invalidate eder (liste tazelenir),
              ToastProvider "talep oluşturuldu" bildirimi gösterir
```

Hata olursa (ör. yetersiz bakiye) handler bir `ConflictException` fırlatır →
`GlobalExceptionHandler` bunu **ProblemDetails** (409 + Türkçe mesaj) JSON'una
çevirir → frontend `apiFetch` bunu `ApiError`'a çevirir → toast kırmızı mesaj
gösterir. Bu zincirin tamamı [Bölüm 4.4](04-kimlik-guvenlik-hata.md#44-hata-yönetimi-tek-hattan).

## 2.4 Kesişen kaygılar (cross-cutting)

Bunlar "her istekte tekrar eden ama iş mantığı olmayan" işlerdir; tek yerde çözülür:

- **Doğrulama** — `Common/Behaviors/ValidationBehavior.cs`: her command/query,
  handler'a girmeden önce FluentValidation kurallarından geçer. Handler artık
  "gövde geçerli mi?" diye uğraşmaz, doğrudan iş kuralına odaklanır.
- **Hata → HTTP** — `API/Middleware/GlobalExceptionHandler.cs`: `NotFoundException`
  → 404, `ConflictException` → 409, `ForbiddenAccessException` → 403,
  `ValidationException` → 400 (alan bazlı), `UnauthorizedException` → 401.
  Handler'lar HTTP kodu düşünmez, anlamlı istisna fırlatır.
- **Kimlik** — `ICurrentUser` (Application arayüzü) o anki kullanıcının id/rol/
  employeeId'sini verir; Infrastructure JWT'den doldurur. Handler "istek kimden
  geliyor?" bilgisine buradan erişir.
- **Rol kapsamı** — `ScopeFor(currentUser)` uzantısı: bir `IQueryable`'ı role
  göre daraltır (manager → ekibi). Tek yerde tanımlanıp her sorguda kullanılır.

## 2.5 Frontend mimarisinin özeti

Detayı [Bölüm 5](05-frontend.md); burada sadece yerini görelim:

- **api/** — `client.ts` (fetch sarmalayıcı: token, 401-refresh), `session.ts`
  (localStorage oturum), `schema.d.ts` (backend'den üretilen tipler).
- **auth/** — `AuthContext` (login/register/logout), `guards.tsx` (rota koruması),
  `LoginPage`.
- **layout/** — `AppShell` (sidebar/header), `GlobalSearch` (Ctrl+K), `ToastProvider`.
- **routes.tsx** — rota + rol matrisi + sayfa eşlemesi (tek dosyada).
- **features/<modül>/** — her modülün `queries.ts` (TanStack Query katmanı),
  `format.ts` (biçimlendirme) ve sayfa/modal bileşenleri.
- **test/** — `apiStub`, `renderPage`, `setup` (test altyapısı).
- **styles/** — eski uygulamadan **birebir kopyalanan** CSS (parite için).

## 2.6 Teknoloji seçimleri tek bakışta

| Katman | Teknoloji | Neden (kısa) |
| --- | --- | --- |
| Backend framework | .NET 9 / ASP.NET Core | Güçlü tipleme, kurumsal olgunluk, MSSQL uyumu |
| Mimari | Clean Architecture + MediatR | Test edilebilirlik, ince controller, kesişen kaygı yönetimi |
| Veri | EF Core + MSSQL | ORM üretkenliği + ağır agregasyonlar için SQL view/SP |
| Doğrulama | FluentValidation | Kuralları handler'dan ayırır, pipeline'da merkezi |
| Kimlik | ASP.NET Identity + JWT | Hazır lockout/hash + durumsuz (stateless) token |
| PDF | QuestPDF | Kod-öncelikli, lisans-dostu pusula üretimi |
| Frontend | React 18 + TypeScript + Vite | Yaygın, tip güvenli, hızlı dev sunucusu |
| Sunucu durumu | TanStack Query | Cache + invalidation + loading/error durumları hazır |
| Test | xUnit + Vitest/RTL | Backend entegrasyon + frontend bileşen testleri |

Her birinin uzun gerekçesi [Bölüm 8](08-kararlar-neden.md).

---

Önceki: [← 1. Genel Bakış](01-genel-bakis.md) · Sonraki: [3. Veri Modeli →](03-veri-modeli.md)
