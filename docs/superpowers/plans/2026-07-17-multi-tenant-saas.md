# Multi-Tenant SaaS Altyapısı — Uygulama Planı

> **For agentic workers:** Bu bir **program** (çok fazlı), tek dilim değil. Her faz
> kendi içinde test edilebilir ve ayrı ayrı yürütülür. Fazlar sıralıdır; Faz 0
> temeldir. Adımlar checkbox (`- [ ]`) ile izlenir. Yürütme için
> superpowers:subagent-driven-development veya executing-plans kullan.

**Goal:** Ekipzen'i tek-kiracılı (single-tenant) yapıdan **çok-kiracılı (multi-tenant)
SaaS**'a dönüştürmek: birçok müşteri şirket aynı sistemde, verileri birbirinden
tam izole, tek veritabanında.

**Kararlar (kullanıcı onaylı):**
- **İzolasyon:** Ortak DB + ortak şema + her tabloda `TenantId` + EF Core **global
  query filter** ile otomatik izolasyon.
- **Kiracı çözümü:** JWT'ye gömülü `tenant` claim'i; her istekte token'dan okunur.
- **Açılış:** Önce **provizyonlu** (iç uç: şirket + ilk hr-admin oluştur); self-servis
  kayıt sonraki faza bırakılır.

**Architecture:** `TenantId` `BaseEntity`'ye eklenir (tüm 29 varlık miras alır).
`AuditableEntityInterceptor` yeni kayıtlara aktif kiracıyı damgalar. `AppDbContext.
OnModelCreating`'te kiracı-kapsamlı her varlığa global query filter eklenir —
böylece **her sorgu otomatik olarak yalnız aktif kiracının verisini görür**.
Aktif kiracı `ICurrentTenant` ile JWT `tenant` claim'inden gelir.

**Tech Stack:** .NET 9, EF Core (global query filters + interceptor), ASP.NET Identity
(JWT claim), MSSQL. Frontend değişimi minimal (tenant token'da taşınır).

## Global Constraints

- **Çapraz-kiracı sızıntısı = kabul edilemez.** Her kiracı-kapsamlı varlık global
  filtreye sahip olmalı; her fazda **izolasyon testi** (Kiracı A, Kiracı B'nin
  verisini asla görmez) yazılır. Bu testler bu programın en kritik çıktısıdır.
- **SQL view'ları global filtreyi baypas eder** (ham SQL). Kiracı-kapsamlı her
  view'ın SQL'ine `TenantId` filtresi eklenmeli VEYA view'dan okuyan handler
  in-app filtrelemeli. Bu, en sık gözden kaçan tuzaktır (Faz 3).
- Kimlik verisi (`ApplicationUser`, `RefreshToken`) de kiracıya bağlı; login akışı
  e-posta → kullanıcı → kullanıcının kiracısı zincirini kurar.
- Mevcut tek-kiracı verisi bir **"default tenant"**e taşınır (backfill migration).
- Türkçe kullanıcı metinleri; her faz sonunda `dotnet test` + gerekiyorsa `npm test` yeşil.

---

## Faz 0 — Kiracılık temeli (veri + izolasyon çekirdeği)

**En kritik faz.** Yanlış yapılırsa her şey sızar. Sonunda: tüm veriye `TenantId`
eklenmiş, global filtre aktif, mevcut veri default tenant'a taşınmış.

### Görevler

- [ ] **T0.1 — `Tenant` varlığı + `TenantId` alanı**
  - Create: `Domain/Entities/Tenancy/Tenant.cs` (`Id`, `Name`, `Slug` (benzersiz,
    ör. "acme"), `IsActive`, `CreatedAtUtc`).
  - Modify: `Domain/Common/BaseEntity.cs` → `public int TenantId { get; set; }`.
    (Tüm 29 varlık otomatik miras alır.)
  - Not: `Tenant`'ın kendisi `BaseEntity`'den **türememeli** (kendi kendine kiracı
    olamaz) — ayrı taban veya düz sınıf.
  - Modify: `Infrastructure/Identity/ApplicationUser.cs` + `RefreshToken.cs` →
    `TenantId` ekle (Identity varlıkları `BaseEntity` değil).

- [ ] **T0.2 — `ICurrentTenant` soyutlaması**
  - Create: `Application/Common/Interfaces/ICurrentTenant.cs` → `int? TenantId { get; }`
    ve `int TenantIdOrThrow()` (kiracısız bağlamda anlamlı hata).
  - Create: `API/Services/CurrentTenant.cs` → JWT `tenant` claim'inden okur
    (`CurrentUser.cs` desenini izle: `HttpContext.User.FindFirstValue("tenant")`).
  - Register: `API/Program.cs` DI'ya ekle (scoped).

- [ ] **T0.3 — JWT `tenant` claim'i**
  - Modify: `Infrastructure/Identity/JwtTokenService.cs` `CreateAccessToken` →
    `claims.Add(new Claim("tenant", user.TenantId.ToString()))`.
  - Modify: `IdentityService` token üreten yerler kullanıcının `TenantId`'sini
    taşıdığından emin ol (login/refresh/register hepsi kullanıcıdan türetir).

- [ ] **T0.4 — Interceptor: TenantId damgalama**
  - Modify: `Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs`
    → `EntityState.Added` olan her `BaseEntity` için `TenantId == 0` ise
    `currentTenant.TenantIdOrThrow()` ata (audit damgalamayla aynı yerde).
    `ICurrentTenant` interceptor'a inject edilir.

- [ ] **T0.5 — Global query filter (izolasyonun kalbi)**
  - Modify: `Infrastructure/Persistence/AppDbContext.cs` `OnModelCreating` →
    kiracı-kapsamlı **her** varlık için:
    `builder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantId)`.
    `_tenantId`, DbContext'e inject edilen `ICurrentTenant`'tan alınır (ctor'da
    bir kez okunup field'a alınır — filter closure'ı sabit değeri yakalar).
  - Reflection ile tüm `BaseEntity` türlerine otomatik uygulamak, elle 36 satır
    yazmaya tercih edilir (unutma riskini sıfırlar). Yardımcı:
    `ApplyTenantFilter<T>()` generic + model tarama.
  - **Dikkat:** `HasQueryFilter` ilişkili include'larda da geçerli; ama raw SQL
    ve view'lar HARİÇ (Faz 3).

- [ ] **T0.6 — Migration + mevcut verinin backfill'i**
  - `dotnet ef migrations add MultiTenancyFoundation`.
  - Migration'a elle: bir "Default" tenant satırı ekle; **tüm mevcut tablolardaki**
    `TenantId`'yi o tenant'ın Id'siyle güncelle (`UPDATE ... SET TenantId = @default`).
    ApplicationUser/RefreshToken dahil.
  - `TenantId` sütunlarına index; sık sorgulanan tablolarda `(TenantId, ...)`
    bileşik index düşün.

- [ ] **T0.7 — İZOLASYON TESTİ (bu fazın kanıtı)**
  - Create: `tests/Ekipzen.Tests.Integration/Tenancy/TenantIsolationTests.cs`.
  - Test altyapısı iki kiracı + her birinde birer kullanıcı/veri kurabilmeli
    (Faz 1'deki provizyon ucuyla ya da seed yardımcısıyla).
  - Doğrula: Kiracı A'nın token'ıyla `/api/employees` çağrısı **yalnız** A'nın
    personellerini döner; B'nin bir kaydının id'siyle `/api/employees/{bId}` → 404
    (global filtre B'yi görünmez yapar, NotFound olur).
  - Run: `dotnet test --filter TenantIsolation` → PASS.

- [ ] **T0.8 — Commit**
  `git commit -m "feat(backend): multi-tenancy temeli — TenantId, global query filter, interceptor, backfill"`

---

## Faz 1 — Kiracı-farkında kimlik + provizyon

Sonunda: yeni kiracı (şirket + ilk hr-admin) oluşturulabiliyor; login doğru
kiracıyı çözüyor; kayıt kiracı içinde.

### Görevler

- [ ] **T1.1 — Kiracı provizyon ucu (iç/platform yetkisi)**
  - Create: `Application/Features/Tenancy/Commands/ProvisionTenantCommand.cs`
    (`CompanyName`, `Slug`, `AdminName`, `AdminEmail`). Handler:
    1) `Tenant` oluştur, 2) o kiracıda ilk `hr-admin` `ApplicationUser`'ı
    (geçici şifre veya davet token'ı; A3/A4 e-posta akışına bağlanır),
    3) sonucu (tenant id/slug + admin e-posta) döndür.
  - **Bu, A1 bulgusunu (prod'da ilk admin oluşturulamıyor) da çözer** — artık her
    kiracının ilk admin'i provizyonla gelir.
  - Yetki: bu uç normal rol sisteminin **dışında** — bir "platform admin" API
    anahtarı / ayrı policy ile korunmalı (kiracılar birbirini provizyonlayamaz).
    Basit MVP: yapılandırmadan gelen bir `X-Platform-Key` başlığı.
  - Controller: `TenancyController` (platform-korumalı).

- [ ] **T1.2 — Login kiracıyı çözer**
  - Modify: `IdentityService.LoginAsync` — e-posta ile kullanıcıyı bul; kullanıcının
    `TenantId`'si token'a gömülür (T0.3 zaten yapıyor). Ek: kiracı pasifse (
    `Tenant.IsActive == false`) login reddedilir.
  - **Dikkat:** E-posta artık kiracı-benzersiz mi, global benzersiz mi? Identity
    varsayılanı global benzersiz e-posta. MVP için **global benzersiz** en basit
    (aynı e-posta iki şirkette olamaz). Kararı belgele; ileride kiracı-bazlı
    benzersizlik gerekirse Identity özelleştirmesi gerekir.

- [ ] **T1.3 — Kayıt (register) kiracı içinde**
  - Modify: `RegisterCommand`/`IdentityService.RegisterAsync` — self-servis kayıt
    henüz yok; register yalnız **mevcut bir kiracı bağlamında** çalışmalı (davet
    akışıyla). Geçici olarak: anonim register'ı **kapat** (provizyon + davet
    dışında kullanıcı oluşturulmaz) veya kiracı bağlamı zorunlu kıl.
  - Not: Bu, Faz 2'deki self-servis kayıtla yeniden ele alınacak.

- [ ] **T1.4 — Interceptor/filter, kimlik yazımlarında da geçerli**
  - `ApplicationUser`/`RefreshToken` global filter'a dahil değilse (Identity ayrı
    DbSet), bu tablolar için de kiracı kontrolünü **elle** ekle: refresh token
    doğrulaması kullanıcının kiracısıyla eşleşmeli; kullanıcı sorguları kiracıya
    filtrelenmeli.

- [ ] **T1.5 — Testler**
  - Provizyon → yeni kiracıda admin login olabiliyor.
  - Kiracı A admin'i, Kiracı B'nin personelini/iznini **göremiyor** (T0.7'yi genişlet).
  - Pasif kiracı login reddi.

- [ ] **T1.6 — Commit**
  `git commit -m "feat(backend): kiracı provizyon + kiracı-farkında login/kayıt"`

---

## Faz 2 — SQL view/read-model izolasyonu (kritik tuzak)

Global query filter **yalnız EF varlıklarına** uygulanır. Ham SQL view'ları ve
read-model'ler onu baypas eder → **sessiz çapraz-kiracı sızıntısı riski**.

### Görevler

- [ ] **T2.1 — View'ları kiracı-farkında yap**
  - Modify (migration): `vw_LeaveBalanceSummary`, `vw_MonthlyAttendanceSummary`,
    `vw_PayrollPeriodSummary`, `vw_ComplianceReadiness`, analitik view'lar ve iş-günü
    fonksiyonu — hepsine `TenantId` sütunu ve/veya WHERE filtresi ekle. Read-model
    sınıflarına (`Domain/ReadModels/*`) `TenantId` ekle.
  - Bu view'lardan okuyan **her handler**, `.Where(x => x.TenantId == currentTenant...)`
    ile filtrelemeli (view EF varlığı değilse global filter yok!). Alternatif:
    read-model'i de kiracı-filtreli bir keyless entity yapıp `HasQueryFilter` uygula
    (EF Core keyless entity'lerde query filter destekler) — tercih edilen, çünkü
    "unutma" riskini kapatır.

- [ ] **T2.2 — İzolasyon testi: view/read-model**
  - Kiracı A ve B'de izin/puantaj/uyum verisi kur; A'nın bakiye/hazırlık
    sorgularının B'yi **hiç** yansıtmadığını doğrula.

- [ ] **T2.3 — Commit**
  `git commit -m "feat(backend): SQL view/read-model kiracı izolasyonu"`

---

## Faz 3 — Seed, test altyapısı, dosya deposu izolasyonu

- [ ] **T3.1 — Seed çok-kiracılı**
  - `AppDbContextInitializer` — demo için **2 kiracı** (ör. "Acme" + "Globex") ve
    her birinde kendi demo kullanıcıları/verisi ekle. Bu, izolasyonun demo'da
    görünür olmasını sağlar.

- [ ] **T3.2 — Test altyapısı kiracı yardımcıları**
  - `EkipzenApiFactory`/collection'a: "kiracı oluştur + o kiracıda authed client"
    yardımcısı. Mevcut testler default/tek kiracıda çalışmaya devam etmeli (backfill
    sayesinde), ama yeni testler çoklu kiracı kurabilmeli.
  - **Mevcut 71 entegrasyon testini gözden geçir:** paylaşımlı DB + kiracı filtresi
    etkileşimi (bir testin kiracısı diğerininkini görmemeli; ama aynı kiracıda
    paylaşımlı durum devam eder). Gerekirse testleri tek default kiracıya sabitle.

- [ ] **T3.3 — Dosya deposu kiracı-ayrık (C1 ile birleşir)**
  - Evrak/foto yolları kiracıya göre ayrılmalı (`storage/<tenantId>/...`); bir
    kiracı diğerinin dosyasına id tahminiyle erişememeli (download handler kiracı
    kontrolü). İdealde object storage'a taşınırken (C1) kiracı prefix'i uygulanır.

- [ ] **T3.4 — Commit**
  `git commit -m "test(backend): çok-kiracılı seed + test altyapısı + dosya izolasyonu"`

---

## Faz 4 — Frontend kiracı farkındalığı (minimal)

JWT-claim modeli sayesinde frontend'de büyük değişiklik yok; veri zaten token'la
kiracıya bağlı. Yapılacaklar küçük:

- [ ] **T4.1 — Kiracı bağlamını göster**
  - AppShell header'ında aktif şirket adını göster (token'dan/`/me`'den kiracı adı).
    `GET /api/me` yanıtına kiracı adı/slug eklenir.
- [ ] **T4.2 — (Opsiyonel) Platform/provizyon ekranı**
  - İç kullanım için basit "yeni kiracı oluştur" formu (platform-key korumalı).
    Erken MVP'de curl/Swagger ile de yapılabilir; UI ertelenebilir.
- [ ] **T4.3 — Testler + commit**

---

## Faz 5 — Sertleştirme (MVP operasyon önkoşullarıyla kesişim)

Multi-tenancy, önceki MVP değerlendirmesindeki bazı maddeleri artık **zorunlu**
kılar. Bunlar ayrı ele alınabilir ama multi-tenant'sız anlamsızdır:

- [ ] **T5.1 — Sırlar env'den** (A2): JWT secret + connection string yapılandırmadan/
  env'den; committed dev secret prod'da override edilir. Platform-key de env'den.
- [ ] **T5.2 — Gerçek e-posta** (A3) + **davet/şifre-belirleme akışı** (A4): provizyon
  ve işe-alım artık geçici şifre yerine davet e-postası + token ile şifre belirleme
  kullanmalı (kiracılar arası güvenli onboarding).
- [ ] **T5.3 — Rate limiting** (C3): login/refresh/provizyon uçlarına.
- [x] **T5.4 — KVKK/veri izolasyonu belgeleri** (A5): kiracı verisinin ayrımı, silme
  (kiracı kapatma → veri anonimleştirme/silme), erişim denetimi.
  → `docs/kvkk-veri-izolasyonu.md` (izolasyon katmanları, erişim denetimi, davet
  akışı, KVKK eşlemesi, bilinen boşluklar + operasyonel kontrol listesi).

> Not: T5 kalemleri isteğe göre ayrı bir "MVP sertleştirme" planına da bölünebilir.
> Multi-tenancy çekirdeği Faz 0–3'tür; Faz 4–5 onu satılabilir yapar.

---

## Doğrulama (program geneli)

1. **İzolasyon testleri** (her fazda) — Kiracı A asla Kiracı B'nin verisini görmez:
   personel, izin, bordro, uyum, view/read-model, dosya. Bu, en önemli güvence.
2. `dotnet test` (tüm paket) + `npm test -- --run` yeşil; `dotnet build`/`npm run build` hatasız.
3. **Uçtan uca duman:** iki kiracı provizyonla → her birinde admin login → her admin
   yalnız kendi şirketini görür → çapraz id denemesi 404.
4. Mevcut tek-kiracı davranışı bozulmadı (backfill + default tenant ile geriye uyum).

## Risk notları

- **En büyük risk:** bir kiracı-kapsamlı varlığa global filtre eklemeyi unutmak →
  sessiz sızıntı. Azaltma: reflection ile otomatik filtre uygulama (T0.5) + izolasyon
  testleri (T0.7, T2.2).
- **İkinci risk:** view/read-model baypası (Faz 2). Azaltma: keyless entity + query filter.
- **Performans:** `TenantId` filtreleri her sorguya girer; ilgili index'ler (T0.6) şart.

## Sonraki adımlar (bu programdan sonra)

- Self-servis kiracı kaydı (public "şirketini oluştur" akışı + kötüye kullanım önleme).
- Faturalandırma/abonelik kiracıya bağlama (mevcut `Subscription` kiracı başına).
- İleride kurumsal müşteri için DB-per-tenant seçeneği (mimari buna açık bırakıldı).
