# Ekipzen Backend

Türkçe İK SaaS frontend'ini (`../`) besleyen **.NET 9 + EF Core 9 + MSSQL** backend'i.
Clean Architecture ile, 13 fazda (Faz 0–12) geliştirildi; tüm veri şekilleri ve iş
kuralları frontend'in kanonik kaynaklarından birebir alındı (`services/mockData.js`,
`routes.js`, `components/payroll.js`, `components/dashboard.js`).

## Mimari

```
Ekipzen.sln
 ├─ src/
 │   ├─ Ekipzen.Domain          → entity, enum, read model, PayrollEngine (bağımlılıksız)
 │   ├─ Ekipzen.Application     → CQRS handler (MediatR), DTO, FluentValidation, servis arayüzleri
 │   ├─ Ekipzen.Infrastructure  → EF Core DbContext + migration + SQL view/trigger/function,
 │   │                          Identity + JWT, dosya deposu, QuestPDF, e-posta outbox, seed
 │   └─ Ekipzen.API             → controller, middleware, DI, Swagger
 └─ tests/
     ├─ Ekipzen.Tests.Unit         → bordro motoru parite testleri (JS çıktılarıyla birebir)
     └─ Ekipzen.Tests.Integration  → uçtan uca API testleri (WebApplicationFactory + gerçek MSSQL)
```

Bağımlılık yönü: `API → Infrastructure → Application → Domain`.

## Çalıştırma

### Docker ile (API + MSSQL)

```bash
docker-compose up -d          # MSSQL 2022 + API ayağa kalkar
```

### Yerel geliştirme

Gereksinim: .NET SDK 9 (`global.json` ile sabit) + erişilebilir bir MSSQL instance'ı.

```bash
dotnet build                                  # temiz derleme
dotnet run --project src/Ekipzen.API           # https://localhost:7001/swagger
```

- Development ortamında migration + demo seed **otomatik** uygulanır (`Program.cs`).
- **İki bağlantı dizesi, N veritabanı.** `ConnectionStrings:PlatformConnection`
  (`EkipzenPlatform` — kiracı kimliği, yönlendirme dizini, refresh token'lar) ve
  `ConnectionStrings:DefaultConnection` (`EkipzenDb` — **paylaşılan** katalog).
  Her kiracının verisi ise kendi kataloğundadır (`Ekipzen_{slug}`); adresi
  `DefaultConnection`'dan yalnız katalog adı değiştirilerek türetilir, yani
  sunucu/kimlik ayarı tek yerde durur. Önek `Tenancy:DatabasePrefix` ile
  değiştirilebilir (entegrasyon testleri bunu kullanır).
  İkisi de `appsettings.Development.json`'da tanımlı (yerel Windows auth); ortam
  değişkeniyle ezilebilirler: `ConnectionStrings__DefaultConnection`,
  `ConnectionStrings__PlatformConnection`.
  **Tuzak:** yalnız birini ezersen diğeri sessizce varsayılan sunucuya bağlanmaya devam
  eder — hata vermeden tutarsız bir kuruluma düşersin, ikisini birlikte ayarla.
- **Sıfırdan başlarken** `EkipzenDb` + `EkipzenPlatform` düşürmek YETMEZ; tüm
  `Ekipzen_*` kiracı veritabanlarını da birlikte düşür. Aksi halde provizyon
  "katalog zaten var" diye reddeder (yarıda kalmış kurulumun artığını sessizce
  yeniden kullanmamak için konulmuş bilinçli koruma).
- Dosya deposu kökü: `Storage:Root` (varsayılan `App_Data/storage`); e-posta outbox'ı
  `{Storage:Root}/outbox` altına JSON olarak düşer (SMTP yerine geliştirme göndericisi).

### Demo kullanıcılar (seed)

| E-posta | Rol | Şifre |
|---|---|---|
| `ik@hrmaster.local` | hr-admin | `demo123` |
| `ece.arslan@hrmaster.local` | manager | `demo123` |
| `ahmet.yilmaz@hrmaster.local` | employee | `demo123` |

Swagger'da **Authorize** butonuna `Bearer {token}` girerek korunan uçlar denenebilir
(token `/api/auth/login`'den alınır).

## Test

```bash
dotnet test                                          # birim + entegrasyon
dotnet test --collect:"XPlat Code Coverage"          # kapsam ölçümü (coverlet)
```

- Entegrasyon testleri gerçek MSSQL'e karşı koşar: `EkipzenDb_Test` veritabanı her koşuda
  sıfırdan kurulur (migration + seed). Bağlantı, test fabrikasında ortam değişkeniyle verilir.
- **Bordro paritesi kritik kabul kriteridir:** `PayrollEngine` çıktıları,
  `components/payroll.js` motorunun gerçek JS çıktılarıyla xUnit'te birebir eşleştirilir.
- Teslim anındaki durum (Faz 12): **78 test yeşil** (9 birim + 69 entegrasyon); satır kapsamı
  (entegrasyon koşusu): API %93, Application %95, Domain %91, Infrastructure %99.

## Kesişen prensipler

- **Yetki kapsamı:** `routes.js` rol matrisi backend policy'si olarak birebir uygulanır —
  hr-admin → tümü; manager → yalnız kendi ekibi; employee → yalnız kendisi.
- **Audit:** kritik tablolarda (Employees, LeaveRequests, PayrollEmployees,
  ComplianceDocuments) SQL trigger + EF interceptor birlikte append-only iz üretir;
  `/api/audit-logs` ile sunulur.
- **SQL nesneleri:** iş-günü fonksiyonu (`fn_WorkingDays`), izin bakiyesi / aylık puantaj /
  bordro dönem özeti / çalışan-departman risk / uyum hazırlık **view**'ları — hepsi
  EF migration'larıyla oluşturulur.
- **Hata zarfı:** ProblemDetails (RFC 7807) + FluentValidation; iş kuralı ihlalleri 409.

## API sözleşmesi

Uç envanteri ve rol matrisi için: [`../raporlar/backend-api-sozlesmesi.md`](../raporlar/backend-api-sozlesmesi.md).
Frontend'in beklediği sözleşme (`services/apiClient.js`) birebir karşılanır:
`POST /api/auth/login`, `GET /api/me`, `GET /api/actions`,
`GET /api/audit-logs`, `PATCH /api/actions/{id}/status`.

> **Uygulamada kayıt (signup) ucu yoktur.** `POST /api/auth/register` kiracı
> sızıntısı yüzünden (anonim kayıt her kullanıcıyı platformdaki en düşük Id'li
> kiracıya bağlıyordu), `POST /api/tenants/signup` ise anonim isteğin veritabanı
> oluşturmasına yol açtığı için kaldırıldı. Yeni şirket yalnız operatör
> tarafından açılır (`POST /api/tenants`, `X-Platform-Key`); mevcut şirkete
> katılım yalnız davetle olur.
