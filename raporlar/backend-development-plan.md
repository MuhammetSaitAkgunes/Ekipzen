# Ekipzen — Backend Geliştirme Planı

> Durum: Onaylandı · Tarih: 2026-07-11 · Hedef: .NET 9 + EF Core + MSSQL backend

## Context (Neden bu çalışma?)

`Ekipzen`, 3 rollü (hr-admin / manager / employee), ~11 modüllü Türkçe bir İK SaaS
**frontend'idir** (vanilla JS, bundler yok). Tüm veri şu an mock: `services/mockData.js`,
component içi literaller ve `localStorage`. `services/apiClient.js` içinde zaten
hedeflenen bir .NET sözleşmesi var (`https://localhost:7001/api`, Bearer token) ama
hiçbir component gerçek API'ye bağlı değil.

Amaç: bu frontend'i besleyecek **gerçek bir backend**'i (.NET 9, EF Core, MSSQL)
sıfırdan, Clean Architecture ile, fazlar halinde geliştirmek.

**Kapsam kararı:** Sadece backend + API geliştirilir; mevcut frontend'e dokunulmaz.
`mockData.js`/`routes.js`/`components/payroll.js` yalnızca **kaynak-of-truth referansı**
olarak kullanılır (dönmesi gereken veri şekilleri, rol matrisi, brüt→net mantığı).

## Teknoloji Yığını

- **Runtime/API:** .NET 9, ASP.NET Core Web API (controller tabanlı)
- **ORM/DB:** EF Core 9 + `Microsoft.EntityFrameworkCore.SqlServer`, **MSSQL**
- **Auth:** ASP.NET Core Identity + JWT Bearer (+ refresh token)
- **Uygulama katmanı:** MediatR (CQRS), FluentValidation, Mapster (DTO map)
- **Loglama:** Serilog (+ request logging), ProblemDetails standart hata
- **PDF:** QuestPDF (bordro pusulası)
- **Dok:** Swashbuckle (Swagger UI + JWT auth), OpenAPI
- **Test:** xUnit + FluentAssertions (birim), WebApplicationFactory (entegrasyon)
- **Container:** Dockerfile (API) + docker-compose (API + MSSQL)

## Mimari (Clean Architecture — ayrı projeler)

```
Ekipzen.sln
 ├─ src/
 │   ├─ Ekipzen.Domain          → entity, enum, value object, domain event (bağımlılıksız)
 │   ├─ Ekipzen.Application      → CQRS handler, DTO, validator, servis arayüzleri
 │   │                           (IPayrollEngine, IFileStorage, IEmailSender, repo/UoW)
 │   ├─ Ekipzen.Infrastructure   → EF DbContext + config + migration, Identity, JWT,
 │   │                           dosya depolama, PDF, e-posta, repo impl, SQL view/SP/trigger
 │   └─ Ekipzen.API              → controller, middleware, DI, Swagger, Program.cs
 └─ tests/
     ├─ Ekipzen.Tests.Unit        → özellikle bordro motoru parite testleri
     └─ Ekipzen.Tests.Integration → uçtan uca API testleri (WebApplicationFactory)
```

Bağımlılık yönü: API → Infrastructure → Application → Domain. Domain hiçbir şeye bağımlı değil.

## Kesişen (cross-cutting) prensipler

- **Yetki kapsamı:** manager → yalnız kendi ekibi; employee → yalnız kendisi; hr-admin → hepsi.
  `routes.js` `roles[]` matrisi backend policy olarak birebir uygulanır (frontend guard kozmetiktir).
- **Audit:** Her mutasyon audit kaydı üretir. Kritik tablolarda **SQL trigger** + uygulama
  seviyesinde actor yakalayan EF `SaveChanges` interceptor birlikte kullanılır.
- **Hata/validasyon:** ProblemDetails + FluentValidation; tutarlı hata zarfı.
- **Sayfalama/filtre:** liste uçlarında server-side filter + pagination.
- **DB yapıları (dengeli kullanım):**
  - **View:** izin bakiyesi, aylık puantaj özeti, bordro dönem özeti, departman risk agregasyonu, uyum hazırlık skoru
  - **Trigger:** kritik tablolarda (Employee, LeaveRequest, PayrollEmployee, ComplianceDocument) audit + izin bakiyesi güncelleme
  - **Stored procedure / function:** risk skorlama agregasyonu, bordro dönem toplu hesap, iş-günü/kıdem hesap fonksiyonları, rapor/CSV export

## Fazlar

### Faz 0 — İskelet & altyapı
Solution + 4 kaynak + 2 test projesi; Central Package Management, `.editorconfig`,
`Directory.Build.props`. `Program.cs`, `appsettings`, Serilog, global exception
middleware + ProblemDetails, Swagger (JWT butonlu), health checks. Dockerfile +
docker-compose (API + MSSQL). Boş migration ile "çalışıyor" doğrulaması.

### Faz 1 — Veri modeli & DB temeli
Tüm modüllerin Domain entity'leri + enum kataloğu (bkz. Ek A). EF `DbContext` + Fluent
API konfigürasyonları, MSSQL. Identity entegrasyonu (`ApplicationUser` ↔ `Employee` bağı).
İlk migration. `mockData.js` şekillerinden **seed** (departman, personel, demo kullanıcılar).
Audit altyapısı (AuditLog entity + interceptor + ilk trigger'lar).

### Faz 2 — Auth & yetkilendirme
Identity + JWT: `login`, `register`, `me`, `refresh`, `logout`, `change-password`.
Rol policy'leri (hr-admin / manager / employee) → route matrisi. Lockout, parola politikası,
2FA toggle. `apiClient.js` sözleşmesindeki `/auth/login|register`, `/me` birebir karşılanır.

### Faz 3 — Personel & Departman
Employee tam kart CRUD (kimlik/iletişim/iş/mali/özlük-sağlık/evrak grupları), directory
list/search/filter, bulk-deactivate, status. Department CRUD. **IFileStorage** (yerel disk):
foto + özlük belge yükleme/listeleme. DTO + validasyon.

### Faz 4 — İzin & Onay
`LeaveType`, `LeaveRequest`, `LeaveBalance`, `Holiday`. Talep oluştur/listele/iptal,
**iş-günü hesap** (tatil tablosu + SQL function). Manager onay kuyruğu, approve/reject
(+audit +bildirim). Takım izin widget'ı. İzin bakiyesi **SQL view** + güncelleme trigger'ı.

### Faz 5 — Puantaj / Mesai
`AttendanceEntry` (giriş/çıkış), `TimesheetEntry`, canlı yoklama panosu, aylık özet.
Manuel giriş + satır düzenleme. Aylık agregasyon **view/SP** → fazla mesai bordroya beslenir.

### Faz 6 — Bordro (en kritik)
`PayrollPeriod`, `PayrollEmployee` (girdi), `PayrollSettings` (döneme göre versiyonlu),
`IncomeTaxBracket`. **`IPayrollEngine`** — C# domain servisi olarak `components/payroll.js`
brüt→net mantığının **birebir** kopyası (SGK taban clamp, artan oranlı gelir vergisi,
asgari ücret istisnaları, damga vergisi, işveren maliyeti, uyarı bayrakları). Uçlar: dönem
listesi, tekil hesap/önizleme, check, approve, dönem submit, settings CRUD. **QuestPDF**
bordro pusulası. Dönem özeti **view/SP**. Motor için yoğun **parite birim testleri**
(JS çıktılarıyla eşleşme).

### Faz 7 — İşe Alım (ATS)
`Candidate`, `Position`, `InterviewNote`, `Evaluation`, `CandidateHistory`. Pipeline
aşama geçişleri (Yeni→Mülakat→Teklif→Red), hire→Employee'ye dönüştür, not/değerlendirme
ekleme, funnel verisi.

### Faz 8 — Analitik & Risk (Dashboard)
`EmployeeRiskMetric` okuma-modeli; ağır agregasyon **SQL view/SP** ile, risk skor formülü
C#/SP. Uçlar: dashboard metrics, overview KPI, attrition/burnout/manager-load/
employee-voice/compliance detay. Departman risk, talent capacity, risk trend serisi.
Metrikler role göre kapsamlanır.

### Faz 9 — Uyum & Belge
`ComplianceDocument`, deadlines, audit checklist, hazırlık skoru. Durum iş akışı
(Eksik/İncelemede/Süresi Yaklaşıyor/Tamamlandı), owner atama. Hazırlık skoru **view**.

### Faz 10 — Aksiyon Merkezi & Audit
`GlobalAction` CRUD + status geçişleri (open→week→done) + filtre (priority/source/owner).
`AuditLog` sunum ucu (append-only; trigger/interceptor ile modüller-arası dolar).
Açık-aksiyon rozet sayacı, birleşik **search** ucu.

### Faz 11 — Ayarlar & Bildirim & Abonelik
`CompanyProfile`, `NotificationSettings`, `SecuritySettings`/2FA, `Subscription`/`Billing`.
Logo yükleme. Bildirim tetikleyicileri (**IEmailSender** soyutlaması) toggle'lara uyar.

### Faz 12 — Test, dokümantasyon, teslim
Entegrasyon testleri (WebApplicationFactory), kapsam ölçümü. Swagger cilası, README,
API sözleşme dokümanı. docker-compose + seed doğrulaması. `apiClient.js` sözleşmesiyle
son hizalama kontrolü.

## Kritik referans dosyalar (değiştirilmez, kaynak olarak okunur)

- `services/apiClient.js` — hedef API sözleşmesi (base URL, endpoint placeholder'ları)
- `services/mockData.js` — kanonik veri şekilleri (seed ve DTO kaynağı)
- `routes.js` — rol→özellik erişim matrisi (backend policy spec'i)
- `components/payroll.js` — brüt→net motor mantığı (birebir replike edilecek)
- `components/dashboard.js` `getDashboardMetrics()` — risk/uyum/voice şekilleri

## Ek A — Enum kataloğu (frontend'den birebir)

Role: `hr-admin|manager|employee` · Employee status: `active|passive` · Leave type:
`Yıllık|Mazeret|Raporlu|Uzaktan` · Leave status: `approved|pending|rejected` · Attendance:
`ontime|late|absent|early` · Timesheet type: `Tam|Mesai|Rapor` / status: `ok|late|overtime|absent`
· Payroll approval: `Kontrol|Onaya Hazır|Eksik Veri|Onaylandı|Ön Hesap` · Candidate:
`Yeni|Mülakat|Teklif|Red` · Action priority: `high|medium|low` / status: `open|week|done`
· Compliance doc: `Eksik|İncelemede|Süresi Yaklaşıyor|Tamamlandı` · Risk level: `high|medium|low`.

## Doğrulama (nasıl test edilir)

1. `docker-compose up -d` → MSSQL ayağa kalkar; `dotnet ef database update` migration uygular.
2. `dotnet build` temiz derlenir; `dotnet run --project src/Ekipzen.API` → Swagger `/swagger` açılır.
3. Auth akışı: `/api/auth/login` ile JWT alınır, Swagger "Authorize" ile korunan uç denenir.
4. Her faz sonunda ilgili uçlar Swagger'dan manuel + entegrasyon testleriyle doğrulanır.
5. **Bordro paritesi:** `components/payroll.js` içindeki bilinen örnek girdiler için
   `IPayrollEngine` çıktısı (net, SGK, vergi, işveren maliyeti) JS çıktısıyla xUnit'te
   birebir eşleştirilir — kritik kabul kriteri.
6. `dotnet test` → birim + entegrasyon testleri yeşil.
7. Seed sonrası `mockData.js` şekilleriyle dönen JSON'ların alan-alan uyumu kontrol edilir.

## Notlar

- Tek şirketli (single-tenant) varsayımı. Multi-tenant kapsam dışı.
- Canlı yoklama için SignalR yerine polling (basitlik); ihtiyaç olursa sonra eklenir.
- Para birimi TRY; Türk 4/a bordro terminolojisi (SGK, damga/gelir vergisi, BES, KVKK, İSG) korunur.
