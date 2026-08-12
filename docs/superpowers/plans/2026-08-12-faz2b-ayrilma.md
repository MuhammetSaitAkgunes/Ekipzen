# Faz 2b — Ayrılma

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Her müşterinin verisini kendi veritabanına almak — böylece tek bir müşteri, diğerlerine dokunulmadan ve saniye hassasiyetinde geri yüklenebilsin.

**Architecture:** `Tenants.DatabaseName` her kiracının katalog adını taşır. Provizyon `CREATE DATABASE` yapıp migration'ları uygular; bağlantı çözücünün içi katalog adını oradan üretir — çağıranların hiçbiri değişmez, çünkü ayrılma noktası Faz 1b'de kurulmuştu. Purge `DROP DATABASE`'e iner. Riskli sıra bilinçli: önce devredilen mayınlar temizlenir, sonra tesisat kurulur, en son veri bölünür.

**Tech Stack:** .NET 9, EF Core 9, SQL Server, xUnit + FluentAssertions.

Tasarım: `docs/superpowers/specs/2026-08-06-kiraci-basina-veritabani-design.md`
Önceki fazlar: `2026-08-06-faz1a-platform-katmani.md`, `2026-08-07-faz1b-baglanti-tesisati.md`, `2026-08-12-faz2a-kimlik-kiraci-kapsamina.md`

## Global Constraints

- **Faz başlangıcı: 51 birim + 156 entegrasyon = 207 test, CI yeşil.** Azalmamalı.
- Derleme uyarısız (`-warnaserror`). `await` içermeyen `async` test metodu CS1998 verir.
- Türkçe kod yorumları ve test adları; yorumlar "ne" değil "neden" anlatır.
- **Testler işletim sistemine özgü davranışa yaslanmamalı** — CI Linux, yerel Windows. Bu, bir testi zaten kırdı (dosya kilitleme).
- **Kiracı veritabanları `Turkish_CI_AS` harmanlamasıyla oluşturulur** (ürün kararı: Türkçe alfabetik sıralama doğru olsun, aksan duyarlı kalsın). `CREATE DATABASE` bunu AÇIKÇA verir — sunucu varsayılanına bırakılmaz, çünkü yerel (Turkish) ile CI (Latin1) ayrışıyor.
- Gerçek müşteri verisi yok; demo veri seed'den yeniden üretilir.
- Her görev ayrı commit.

## Test yazımı hakkında

Bu planda testler **birebir kod olarak verilmez**, ne kanıtlamaları gerektiği yazılır. Sebebi: önceki fazlarda plandaki test taslakları iki kez yanlış çıktı (var olmayan uç nokta, var olmayan varlık alanı) ve uygulayıcının döngüsünü boşa harcadı. Uygulayıcı **gerçek sözleşmeyi koddan doğrulayıp** testi ona göre yazar.

---

## Görev sırası ve gerekçesi

Sıra riske göre kurulmuştur; bozmayın:

| # | Görev | Neden bu sırada |
| --- | --- | --- |
| 0 | Self-servis kaydı kaldır | Ayrılmadan sonra anonim kayıt **veritabanı oluşturur**; en başta kapatılmalı |
| 1 | Devredilen mayınları temizle | Bunlar ayrılmadan **önce** düzeltilmezse, ayrılma anında patlarlar ve sebebi ayrılmaya bağlanır |
| 2 | `DatabaseName` + veritabanı sağlayıcı | Kullanılmaz; tek başına doğrulanabilir |
| 3 | Çözücü katalog üretir | Tüm `DatabaseName` boş olduğu için davranış değişmez |
| 4 | Provizyon gerçek DB kurar + test altyapısı | **Yeni** kiracılar ayrılır; mevcutlar paylaşımda kalır |
| 5 | Mevcut kiracılar ayrılır | Herkes ayrılır — geri dönüşün zorlaştığı nokta |
| 6 | Purge `DROP DATABASE` + `Subscriptions` platforma | Ayrılma tamamlanınca anlamlı olur |
| 7 | Migration orkestrasyonu + açılış doğrular + dokümantasyon | Çok veritabanlı işletim |

---

### Görev 0: Self-servis kaydı kaldır

**Neden en başta:** `POST /api/tenants/signup` bugün anonim ve bir **satır** oluşturuyor. Bu fazdan sonra aynı uç bir **veritabanı** oluşturacak — yani internetten herhangi biri, bir script'le SQL Server'a istediği kadar veritabanı kurdurabilir. Kaynak tüketme açığı.

**Karar:** veritabanı oluşturmak otomatik bir yan etki değil, **bilinçli bir eylem** olmalı. Kiracı yalnız platform anahtarıyla (`POST /api/tenants`) açılır — yani operatör satışı kapattığında.

**Kaldırılacaklar:**
- `TenancyController` — `Signup` aksiyonu
- `RegisterTenantCommand.cs` — komut, validator, handler, slug üretimi (dosyanın tamamı)
- Testler: `SelfServiceSignupTests`; `ScheduledCleanupTests` ve `TenantPurgeTests` içinde signup kullanan kurulumlar provizyon yoluna çevrilir
- Frontend: `CompanySignupPage` + testi, `/register-company` rotası, `/signup` yönlendirmesi, giriş sayfasındaki bağlantı
- `schema.d.ts` — üretilmiş; yeniden üretilir ya da ilgili bölüm çıkarılır

**Yan kazanç:** Görev 1(d)'deki "`TenantOnboarding` / `RegisterTenantCommand` sıra kopyası" mayını **kendiliğinden yok olur** — kopya kalkıyor, tek yol kalıyor.

**Giriş sayfası:** şirket kaydı bağlantısı kalkınca ziyaretçiye ne gösterileceğine karar verin. En sade seçenek: bağlantıyı kaldırıp giriş formunu bırakmak. Bir "bizimle iletişime geçin" metni eklenecekse bu bir ürün/pazarlama kararıdır — koda gömmeden önce sorun.

**Regresyon testi:** `POST /api/tenants/signup` artık başarısız olmalı ve arkasında kiracı oluşmamalı. Yorumunda neden kaldırıldığını yazın; biri ileride "kayıt ucu yok, ekleyeyim" demesin.

**Doğrulama:** backend tam suite (kaldırılan testler kadar azalır), frontend `vitest` + `tsc -b` + `oxlint`.

> **✅ TAMAMLANDI (2026-08-12).** Planla iki sapma:
> 1. Plan "404 dönmeli" diyordu; ASP.NET bu yola **405** döndürüyor (iki segmentli yol, var olan bir rota şablonuna başka bir fiille denk düşüyor). Test durum kodunu sabitlemek yerine sözleşmenin ölçülebilir karşılığını doğruluyor: **istek başarısız + kiracı satırı oluşmadı** (`SelfServisKayitKaldirildiTests`). Kodu çakmak sözleşmemizi değil ASP.NET'in yönlendiricisini test etmek olurdu.
> 2. Planda yazmayan iki kaldırma daha gerekti: `"signup"` rate-limit politikası (tek kullanıcısı gitmişti) ve testlerdeki `RateLimiting__SignupPermitPerHour` ayarı.
>
> `cleanup-unverified`'ın **anlamı değişti**: artık "doğrulanmamış self-servis kayıt" değil, "provizyon ortasında kesilip `Provisioning`'de takılı kalmış kiracı" topluyor. Testleri buna göre yeniden yazıldı. Görev 1(d) doğrulandı: `TenantOnboarding.CreateWithAdminAsync`'in tek çağıranı `ProvisionTenantCommand`.
>
> Doğrulama: 51 birim + 153 entegrasyon yeşil, `-warnaserror` temiz; frontend `tsc --noEmit` temiz + 146 test yeşil.

---

### Görev 1: Devredilen mayınları temizle

Önceki fazlar bu dört maddeyi bilinçli olarak 2b'ye bıraktı. Hepsi ayrılma anında gerçek soruna dönüşür; ayrılmadan **önce** kapatılmalı ki bir hata çıktığında sebebi ayrılmaya bağlanmasın.

**a) `EmailExistsAsync` çözülemeyen kiracıda fırlatır**
`IdentityService.EmailExistsAsync` dizinden gelen `TenantId` ile kapsam açıyor. Dizin türetilmiştir ve sapabilir — karşılığı olmayan bir kimliğe işaret edebilir (test altyapısı bunu bilerek üretiyor). Ayrılmadan sonra `Create(...)` çözülemeyen kiracıda **fırlatır** ve metot `bool` döndürmesi gerekirken 500'e dönüşür; provizyon, self-servis kayıt ve işe alım yollarını kırar.
→ Çözülemeyen kiracıda `false` dönmeli. Gerekçeyi yoruma yaz.

**b) `AcceptInviteAsync` durum makinesini atlıyor**
Herhangi bir `Provisioning` kiracıyı, kullanıcı şifre belirler belirlemez `Active` yapıyor. Ayrılmadan sonra bu, **veritabanı henüz kurulmamış bir kiracıyı Active** yapmak demek — kullanıcı girer ve var olmayan katalogla karşılaşır.
→ Yalnız provizyonu **tamamlanmış** kiracı etkinleştirilmeli. Kiracının veritabanı gerçekten hazır değilse durumu değiştirme.

**c) `AppDbContextInitializer` kök kapsamdan çözülüyor**
Tek kalan elle `Impersonate`. Düzeltme bu dosyada değil `Program.cs`'te: initializer, `ITenantScopeFactory` ile açılan bir kapsamdan çözülmeli.

**d) ~~`TenantOnboarding` / `RegisterTenantCommand` sıra kopyası~~ — Görev 0 ile kapandı.**
Kopyanın kaynağı self-servis kayıttı; o kalkınca tek yol kaldı. Görev 0'dan sonra `TenantOnboarding.CreateWithAdminAsync`'in tek çağıranının `ProvisionTenantCommand` olduğunu **doğrulayın**; değilse burada ele alın.

**Doğrulama:** her madde için davranışı sabitleyen bir test. Tam suite geçmeli.

> **✅ TAMAMLANDI (2026-08-12).**
>
> **(a)** Çözüm try/catch DEĞİL: `EmailExistsAsync` kapsam açmadan önce
> `ITenantRegistry.GetStatusAsync`'e soruyor — null ise kiracı yoktur → `false` +
> uyarı logu. Fark önemli: `catch` altyapı arızasını da yutar ve "kullanıcı yok"
> diye raporlardı; bu ayrım korunuyor (SQL erişilemezse istisna yukarı çıkar).
> Test `EmailExistsAsyncTests` bir birim testi ve ayrılmadan SONRAKİ dünyayı taklit
> ediyor: kapsam fabrikası çözülemeyen kiracıda fırlatıyor, yani metot fabrikaya
> giderse test kırılıyor — değeri fazdan bağımsız.
>
> **(b)** "Yalnız provizyonu tamamlanmış kiracı etkinleştirilsin" yerine
> **etkinleştirme tamamen kaldırıldı**. Görev 0'dan sonra `Provisioning` kiracı
> yaratan meşru bir akış kalmadı; geriye yalnız tehlikeli tarafı kalırdı. Kural
> netleşti: durum geçişleri provizyon yolunun işidir, son kullanıcının hiçbir
> eylemi kiracıyı erişilebilir yapamaz (`KiraciDurumMakinesiTests`).
>
> **(c)** `SeedDefaultTenantAsync` (ve `Impersonate` çağrısı) `AppDbContextInitializer`'dan
> çıkarıldı; kiracı satırını garanti etme işi `PlatformDbInitializer.EnsureDefaultTenantAsync()`'e
> taşındı (platform verisi + seed'in önkoşulu). `Program.cs` initializer'ı artık
> `ITenantScopeFactory.Create(varsayılanKiraciId)` kapsamından çözüyor. `SeedAsync`
> ilk satırında `TenantIdOrThrow()` ile **tel tuzağı** kuruyor: kiracısız kapsamda
> sessizce yanlış yere yazmak yerine patlıyor (`SeedKapsamiTests`).
>
> **(d)** Doğrulandı, ek iş çıkmadı.
>
> Doğrulama: 52 birim + 156 entegrasyon yeşil, `-warnaserror` temiz.

---

### Görev 2: `DatabaseName` ve veritabanı sağlayıcı

**Üretilen:**
- `Tenants.DatabaseName` (`string?`, max 128) — provizyonda bir kez üretilir, bir daha değişmez. **Boş bırakılabilir**: mevcut kiracılar Görev 5'e kadar paylaşılan veritabanında kalır.
- `ITenantDatabaseProvisioner` (Application):
  - `Task<bool> ExistsAsync(string databaseName, CancellationToken)`
  - `Task CreateAsync(string databaseName, CancellationToken)`
  - `Task DropAsync(string databaseName, CancellationToken)`

**Uygulama notları:**
- `CREATE DATABASE` EF migration içinde yapılamaz; `master`'a ham SQL gerekir. Bağlantı, çözücünün kullandığı dizeden katalog adı `master` yapılarak türetilir (`SqlConnectionStringBuilder`).
- **Veritabanı adı ASLA SQL'e enterpole edilmez.** Ad slug'dan üretiliyor ve slug istemciden geliyor — `QUOTENAME` ile kaçırılmalı ya da adın `^[A-Za-z0-9_]+$` desenine uyduğu doğrulanmalı. `CREATE DATABASE` parametreleştirilemez, bu yüzden doğrulama tek savunmadır.
- `CREATE DATABASE [ad] COLLATE Turkish_CI_AS` — harmanlama **açıkça** verilir (bkz. Global Constraints).
- `DropAsync` önce `ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE` yapmalı; aktif bağlantı varken `DROP` başarısız olur. Bağlantı havuzu da temizlenmeli (`SqlConnection.ClearAllPools`).

**Bu görevde kimse sağlayıcıyı kullanmaz.** Testler onu doğrudan çağırır: oluştur → var mı → harmanlaması doğru mu → düşür → yok mu. Ad doğrulamasının kötü girdiyi reddettiği de sınanmalı.

> **✅ TAMAMLANDI (2026-08-12).** Planla bir sapma:
>
> Plan `SqlConnection.ClearAllPools()` diyordu; **yalnız hedef kataloğun havuzu** temizleniyor (`ClearPool`). `ClearAllPools` süreçteki her veritabanının havuzunu boşaltır — üretimde tek bir kiracının silinmesi, o kiracıyla hiç ilgisi olmayan bütün isteklere bağlantı yeniden kurma maliyeti bindirirdi. Havuzlar bağlantı dizesine göre anahtarlandığı için hedefi seçmek mümkün; çözücü ve sağlayıcı aynı türetmeyi kullanıyor, böylece anahtarlar eşleşiyor.
>
> Ek not: `Tenants.DatabaseName` **tekil indeks almadı**. SQL Server'da unique index NULL'ları tek değer sayar; kiracıların çoğu boşken ikinci kiracı eklenemezdi.
>
> Testler: `VeritabaniAdiDogrulamaTests` (birim — enjeksiyon denemeleri dahil, üç metodun da aynı kapıdan geçtiği), `VeritabaniSaglayiciTests` (entegrasyon — oluştur/var/harmanlama/düşür + idempotanslık + açık bağlantı varken düşürme).

---

### Görev 3: Çözücü katalog üretir

`TenantConnectionResolver.ResolveFor(int? tenantId)` içi değişir:

- `tenantId` null → paylaşılan dize (migration, platform işlemleri, login öncesi)
- Kiracının `DatabaseName`'i boş → paylaşılan dize (henüz ayrılmamış kiracı)
- Doluysa → aynı sunucu, katalog adı `DatabaseName`

**Kritik:** çözücü her çağrıda platform veritabanına gidemez — bağlantı çözümü istek başına birden fazla kez çalışır. `ITenantRegistry` zaten kiracı durumunu önbelleklemek için var; **`DatabaseName`'i de oraya ekleyin** ve çözücü kütükten okusun. Kütük kaydı `Invalidate` ile düşüyor; `DatabaseName` değişmediği için ek bir düşürme yolu gerekmez.

**Bu görevde tüm `DatabaseName`'ler boş**, dolayısıyla davranış değişmez ve 207 test geçmeye devam eder. Bunu doğrulayan bir test yazın: `DatabaseName` dolu olan bir kiracı için çözücünün o katalogu döndürdüğünü, boş olan için paylaşılanı döndürdüğünü sınayın (birim testi, gerçek veritabanı gerekmez).

> **✅ TAMAMLANDI (2026-08-12).** Planın değinmediği bir zorunluluk çıktı:
>
> `ResolveFor` **senkrondur** ve öyle kalmak zorunda — çağrıldığı yer `AddDbContext` yapılandırma geri çağrısı, orası async'e çevrilemez. Kütüğün mevcut API'si ise `Task` döndürüyordu. Sync-over-async (`.GetAwaiter().GetResult()`) yerine kütüğe **senkron** bir `GetDatabaseName(int)` eklendi; önbellek ıskası gerçek senkron sorguyla doldurulur (kiracı başına ~5 dakikada bir, sıcak yolda hiç veritabanına gidilmez).
>
> Durum ve katalog adı **tek önbellek kaydında** tutuluyor: ayrı kayıtlar biri düşüp diğeri kalınca kütüğün kendiyle çelişmesine yol açardı. "Kiracı yok" cevabı da önbellekleniyor — aksi halde geçersiz kimlikli istek her seferinde veritabanına giderdi (ucuz bir DoS yüzeyi).
>
> Testler: `BaglantiCozucuTests` (üç yol + katalog dışında hiçbir ayarın değişmediği), `KiraciKutuguTests` (katalog adı okuma, `Invalidate` ile tazelenme, durum+ad aynı kayıttan).

---

### Görev 4: Provizyon gerçek veritabanı kurar

Provizyon altı adıma iner (tasarım belgesindeki sıra):

1. **Platform, tek transaction:** `Tenants` satırı (`Status = Provisioning`, `DatabaseName` üretilmiş) **ve** `TenantDirectory` rezervasyonu
2. `CREATE DATABASE` — **hedef zaten varsa reddet.** Başarısız bir denemeden kalmış veritabanını sessizce yeniden kullanmak, iki müşterinin verisinin karışması demektir
3. Migration'lar uygulanır (yeni veritabanına bağlı bir context ile)
4. Roller seed edilir, admin kullanıcı (şifresiz) oluşturulur
5. Davet e-postası gönderilir
6. `Status = Active`

**Sıra gerekçeleri (yoruma yazın):** e-posta 1. adımda rezerve edilir ki eşzamanlı iki kayıt aynı adresi alamasın; davet en sonda gönderilir ki müşteri çalışmayan bir kuruluma link almasın.

**Yarıda kalırsa** kiracı `Status = Provisioning`'de kalır — erişilemez ama görünür (`GET /api/tenants/stuck` zaten var).

**Veritabanı adı üretimi:** `IKPro_{slug}`. Slug zaten `^[a-z0-9-]+$` doğrulamasından geçiyor, ama tire SQL tanımlayıcısında sorun çıkarabilir — adı üretirken tireyi alt çizgiye çevirin ve sonucu Görev 2'deki desen doğrulamasından geçirin.

**Test altyapısı — bu görevin en büyük parçası:**
- Her provizyon testi artık **gerçek bir veritabanı** kuruyor. `IKProApiFactory` koşum başında `IKPro_test_*` kalıntılarını da düşürmeli (bugün yalnız iki sabit adı düşürüyor).
- Testlerin ürettiği veritabanı adları çakışmamalı ve tanınabilir olmalı.
- Koşum süresi artacak; ölçün ve rapora yazın. Kabul edilebilir bulmuyorsanız durun ve bildirin — şablon veritabanı kopyalama gibi bir optimizasyon ayrı bir iştir.

> **✅ TAMAMLANDI (2026-08-13).**
>
> **Temizlik stratejisi (kullanıcı kararı):** her testten sonra agresif temizlik. `TenancyTestBase` artık `IAsyncLifetime` uyguluyor; provizyonlanan her katalog izleniyor ve test bitince düşürülüyor. Fabrika ayrıca koşum başında `IKPro_test_*` desenli artıkları süpürüyor — bu, koşum ORTASINDA çöken bir süreç içindir; olmasaydı tek bir çökme sonraki tüm koşumları "veritabanı zaten var" ile kırardı. Test slug'ları `test-` önekli üretiliyor ki süpürme deseni gerçek bir kiracıyı asla silemesin.
>
> **Süre:** entegrasyon takımı **33 sn → 93 sn** (167 test). Yaklaşık 2,8 kat. Kabul edildi.
>
> **Planla sapmalar:**
> 1. `TenantOnboarding` statik yardımcıdan **servise** dönüştü — bağımlılık sayısı (7) parametre listesiyle taşınamaz hale geldi.
> 2. Migration'ı Application katmanından çalıştırmak mümkün değil (`AppDbContext` Infrastructure'da). Yeni bir soyutlama gerekti: **`ITenantSchemaInitializer`** — migration + **rol tohumlaması**. Roller kritik: boş bir kiracı veritabanında `AspNetRoles` boştur, `AddToRoleAsync` SESSİZCE düşer ve admin yetkisiz kalır. Planda bu adım "roller seed edilir" diye geçiyordu ama nereye ait olduğu yazmıyordu.
> 3. Adım 1'in tek transaction'ı için `IPlatformDbContext`'e `BeginTransactionAsync` eklendi (rezervasyon, kiracının veritabanı-üretimli kimliğini bilmek zorunda olduğundan iki `SaveChanges` kaçınılmaz).
> 4. **Davet e-postası ayrı bir adıma çıkarılmadı** — ve ~~açık bırakıldı~~ **KAPATILDI (2026-08-13):** `POST /api/tenants/{id}/resend-invite` eklendi. Planın sırasında e-posta 5., `Active` 6. adım; yani e-posta yine `Active`'den önce gidiyor ve bu, bugünkü davranışa göre hiçbir riski azaltmıyor. Riski gerçekten azaltan tek sıra (e-posta `Active`'den SONRA) daha kötü bir arıza üretirdi: e-posta gönderimi patlarsa kiracı Active ama admin'in daveti yok ve provizyon slug çakışması yüzünden yeniden denenemez. **Doğru çözüm bir operatör ucu: "daveti yeniden gönder"** — ve 2026-08-13'te eklendi: `POST /api/tenants/{id}/resend-invite`, platform anahtarlı, şifresi olan hesaba göndermez (409). Böylece e-posta adımının başarısızlığı artık veri silmeyi gerektirmiyor.
>
> **İki mevcut test kırıldı ve ikisi de bilgi taşıyordu:**
> - `KiraciKutuguTests` — provizyonlanan kiracının `DatabaseName`'i artık dolu; test güncellendi.
> - `TenantFileIsolationTests` — "yabancı ID → 404" kurgusu artık **kurulamıyor**: her katalog kendi kimlik dizisini saydığı için 1 numaralı personel her kiracıda var. Bu bir gerileme değil, izolasyonun güçlenmesi (sorgu filtresi değil fiziksel ayrılık). Test asıl güvenceyi ölçecek şekilde yeniden yazıldı: başka kiracının cevabında ötekinin verisi asla görünmez.
>
> Doğrulama: 66 birim + 167 entegrasyon yeşil, `-warnaserror` temiz. Koşum sonrası sunucuda artık katalog yok (elle doğrulandı).

---

### Görev 5: Mevcut kiracılar ayrılır

Bu, geri dönüşün zorlaştığı nokta.

Gerçek müşteri verisi olmadığı için **taşıma script'i yazılmaz**: iki veritabanı da düşürülür, uygulama açılır, demo kiracılar Görev 4'teki gerçek provizyon yolundan yeniden kurulur.

**Seed bunun için değişmeli:** `AppDbContextInitializer` bugün demo kiracıları doğrudan yazıyor. Artık provizyon yolunu çağırmalı — böylece kayıt akışı her geliştirme kurulumunda çalıştırılmış olur.

**Doğrulama (elle, çıktıları rapora):** iki DB düşür → uygulama aç → `IKPro_demo` ve `IKPro_globex` veritabanlarının **fiilen oluştuğunu** ve harmanlamalarının `Turkish_CI_AS` olduğunu sqlcmd ile gör → iki demo kiracıyla giriş yap → birinde personel oluştur, diğerinde görünmediğini doğrula.

> **✅ TAMAMLANDI (2026-08-13).**
>
> **Elle doğrulama çıktıları** (tüm `IKPro*` veritabanları düşürülüp uygulama sıfırdan açıldı):
> - `IKPro_demo` ve `IKPro_globex` **oluştu**, ikisi de `Turkish_CI_AS`.
> - `Tenants`: `demo → IKPro_demo (Active)`, `globex → IKPro_globex (Active)`.
> - Kullanıcılar: demo katalogunda 3 (ik@/ece/ahmet), globex katalogunda 3 (globex-admin/deniz/cem), **paylaşılan `IKProDb`'de 0**. Personel: 4 / 2 / **0**.
> - İki kiracıyla giriş yapıldı; her biri yalnız kendi personelini gördü.
> - Globex'te departman oluşturuldu → `IKPro_globex`'te 1 satır, `IKPro_demo` ve `IKProDb`'de 0. API üzerinden de yalnız globex görüyor.
>
> **Planla sapma — testler kendi katalog önekini kullanıyor.** Plan bunu öngörmüyordu ama şart oldu: demo kiracıların slug'ları sabit (`demo`, `globex`), dolayısıyla test koşumu da `IKPro_demo` üretirdi ve **geliştiricinin çalışan demo ortamını her `dotnet test`'te silerdi**. Katalog adı üretimi statik yardımcıdan `ITenantDatabaseNamer` servisine dönüştü; önek `Tenancy:DatabasePrefix` ile geliyor ve testler `IKPro_test_` kullanıyor.
>
> `PlatformDbInitializer.EnsureDefaultTenantAsync` (Görev 1'de eklenmişti) **kaldırıldı**: varsayılan kiracı artık provizyon yolundan doğuyor, o metot ölü koda dönüşmüştü.
>
> **Kırılan test:** `AuthFlowTests` — demo admin'in baş harfleri `İK` yerine `İY`. Sebep: baş harfler artık üretim kuralından (`DeriveInitials`) geliyor, çünkü hesabı elle yazılmış bir seed satırı değil gerçek provizyon oluşturuyor. Beklenti güncellendi; bu, seed'in ürün kuralını atlamayı bıraktığının kanıtı.
>
> Doğrulama: 66 birim + 167 entegrasyon yeşil, `-warnaserror` temiz.

---

### Görev 6: Purge `DROP DATABASE` ve `Subscriptions` platforma

**Purge:**
1. `Status = Purging` (erişim kesilir)
2. Dosya alanı silinir
3. `DROP DATABASE` (Görev 2'deki sağlayıcı)
4. Platform: dizin satırları, refresh token'lar, abonelik, kiracı satırı

`TenantPurger`'ın FK sıralı silme mantığı ve `TenantScopedTablesInDeleteOrder` **artık gereksiz** — silin. `DROP DATABASE` hiçbir tabloyu kaçırmaz. Mevcut purge testleri korunmalı: kiracı verisi gitmeli, **diğer kiracıya dokunulmamalı**, dosya silme hatası çağırana görünmeli.

`DatabaseName` boş olan bir kiracı (hiç ayrılmamış) purge edilirse eski satır-silme yolu gerekir mi? Görev 5'ten sonra böyle bir kiracı **kalmaz**; kalıyorsa bu bir hatadır — o durumda anlamlı hata fırlatın, sessizce eski yola düşmeyin.

**`Subscriptions`:** kiracı veritabanından platforma taşınır. Gerekçe: "kim hangi pakette, kimin yenilemesi geldi" bir platform sorusudur; kiracı DB'lerinde kalsaydı her fatura raporu tüm veritabanlarını gezerdi. Yan fayda: bir müşteriyi geri yüklemek **aboneliği geri sarmaz** — doğrusu budur. `SettingsHandlers` tek okuma noktası.

> **✅ TAMAMLANDI (2026-08-13).**
>
> **Purge:** `PurgeAsync` artık dosya alanını siliyor, `DROP DATABASE` yapıyor, sonra platformdaki izleri (refresh token, abonelik, dizin, kiracı satırı) temizliyor. `TenantScopedTablesInDeleteOrder` ve `QualifiedTableName` **silindi**; `#pragma warning disable EF1002` de onlarla birlikte gitti.
>
> **Sıra değişti — dosyalar artık ÖNCE siliniyor.** Eski gerekçe ("silme transaction'ı geri alınırsa dosyalar boşuna gitmesin") `DROP DATABASE` geri alınamadığı için düştü. Yeni ölçüt hangi artığın daha kötü olduğu: veritabanı gidip dosyalar kalırsa diskte PII sürer (KVKK riski); dosyalar gidip veritabanı kalırsa kiracı zaten `Purging`'de erişilemez durur ve operatör yeniden çalıştırınca tamamlanır.
>
> **`DatabaseName` boşsa:** `InvalidOperationException` — sessizce eski yola düşmüyoruz (zaten o yol artık yok).
>
> **Planın değinmediği bir düzeltme:** `PurgeUnverifiedAsync` kataloğu HİÇ oluşmamış adayları da "okuyamadım" diye atlıyordu; onlar tam da temizlenmesi gereken enkaz. Artık önce `ExistsAsync` soruluyor — katalog yoksa kaybedilecek veri de yok, silinir; katalog VAR ama okunamıyorsa atlanır.
>
> **`Subscriptions` platforma taşındı.** İki migration (`SubscriptionsToPlatform`, `DropSubscriptionsFromAppDb`) + `TenantId` üzerinde **tekil** indeks (kod birden çok yerde "tek kayıt" varsayıyor). Seed ve purge platforma yazıyor/siliyor.
>
> **Taşımanın bedeli ve karşılığı:** platform context'inde **global kiracı filtresi YOKTUR** — kiracı veritabanında yapısal olarak imkânsız olan "filtreyi unutma" hatası burada mümkün. `GetSettingsQueryHandler` kiracıyı açıkça filtreliyor ve `AbonelikPlatformdaTests` bunu iki kiracıyla sabitliyor (filtre kalksaydı ikisi de aynı planı görürdü).
>
> **Test altyapısı:** `IKProApiFactory` artık koşum SONUNDA da süpürüyor — test host'unun açılışta kurduğu demo kiracıları koleksiyon boyunca yaşamak zorunda ve ancak host kapanınca silinebiliyor. Koşu sonrası sunucuda tek bir test kataloğu kalmıyor (elle doğrulandı).
>
> Doğrulama: 66 birim + 177 entegrasyon yeşil, `-warnaserror` temiz.

---

### Görev 7: Migration orkestrasyonu, açılış doğrulaması, dokümantasyon

**Migration koşucusu:** tüm kiracı veritabanlarını sırayla migrate eden bir komut. Her kiracı için başarı/başarısızlık raporlar; biri bile patlarsa sıfırdan farklı kodla çıkar. Bu, dağıtımın bir adımıdır.

**Açılış migrate etmez, doğrular:** platform veritabanı açılışta migrate olur (küçük, her zaman gerekli). Kiracı veritabanları **edilmez** — açılış süresi müşteri sayısıyla büyümemeli ve yarıda kalan bir dağıtım fark edilmeli. Şeması geride kalan kiracı 503 döner ve log yüksek sesle söyler; eski şemayla sessizce çalışmak veri bozulmasıdır.

**Dokümantasyon:**
- `docs/yedekleme-ve-kurtarma.md`: geri yükleme artık gerçek — `RESTORE DATABASE IKPro_acme … STOPAT` komutunu yaz. "Veritabanı kiracı bazına bölünmüyor" maddesini kapat.
- `docs/rehberler/07-veritabani-ve-migrationlar.md`: iki context değil artık N+1 veritabanı; migration koşucusu ve üretim adımları.
- `docs/rehberler/06-multi-tenancy.md`: izolasyon artık fiziksel.
- Plana "Uygulama sonucu" bölümü.

---

## Faz sonu doğrulama

- [x] Yeni müşteri açıldığında kendi veritabanı oluşuyor, harmanlaması `Turkish_CI_AS` — `ProvizyonVeritabaniKurarTests` + elle `sys.databases` kontrolü
- [x] İki demo kiracı ayrı veritabanlarında; birinde oluşturulan veri diğerinde görünmüyor — elle doğrulandı (Görev 5), `ProvizyonVeritabaniKurarTests`
- [x] Login, refresh, dondurma/çözme, davet kabulü, işe alım çalışıyor — mevcut suite
- [x] Purge veritabanını düşürüyor; diğer kiracıya dokunmuyor — `TenantPurgeTests`
- [x] Yarıda kalan provizyon `Provisioning`'de görünür kalıyor ve erişilemiyor — `PlatformKatmaniTests`, `KiraciDurumMakinesiTests`
- [x] Migration koşucusu tüm kiracıları güncelliyor, başarısızlığı raporluyor — `POST /api/tenants/migrate`, `SemaKapisiTests`
- [x] Açılış kiracı veritabanlarını migrate etmiyor; geride kalanı 503 ile bildiriyor — `TenantSchemaCheckService` + `ITenantSchemaGuard`, `SemaKapisiTests`
- [x] Backend + frontend suite'leri yeşil, derleme uyarısız

---

## Uygulama sonucu

**Faz 2b tamamlandı (2026-08-13).** Her müşterinin verisi kendi veritabanında;
tek bir müşteri, diğerlerine dokunulmadan geri yüklenebilir hale geldi.

### Planla sapmalar ve gerekçeleri

| Nerede | Plan | Yapılan | Neden |
| --- | --- | --- | --- |
| Görev 0 | signup 404 dönmeli | 405 dönüyor; test kodu değil sonucu sabitliyor | Kodu çakmak sözleşmeyi değil ASP.NET yönlendiricisini test etmek olurdu |
| Görev 1(b) | "yalnız tamamlanmış kiracı etkinleşsin" | Etkinleştirme tamamen kaldırıldı | Görev 0'dan sonra meşru tetikleyicisi kalmadı; geriye yalnız zararı kalırdı |
| Görev 2 | `ClearAllPools()` | Yalnız hedef kataloğun havuzu | Süreçteki her veritabanının havuzunu boşaltmak, bir silme yüzünden tüm müşterilere gecikme bindirirdi |
| Görev 3 | — | Kütüğe **senkron** `GetDatabaseName` | `ResolveFor`, `AddDbContext` geri çağrısından çağrılıyor; async'e çevrilemez, sync-over-async da istenmedi |
| Görev 4 | Davet ayrı adım | Ayrılmadı; yerine `resend-invite` ucu | Plandaki sıra riski azaltmıyordu; asıl eksik operatörün kurtarma aracıydı |
| Görev 4 | — | `ITenantSchemaInitializer` (migration + **rol**) | Boş katalogda rol yoksa `AddToRoleAsync` sessizce düşer, admin yetkisiz kalır |
| Görev 5 | — | `ITenantDatabaseNamer` (yapılandırılabilir önek) | Demo slug'ları sabit; testler geliştiricinin `IKPro_demo`'sunu silecekti |
| Görev 6 | Dosyalar en sonda | Dosyalar ÖNCE | `DROP DATABASE` geri alınamaz; eski gerekçe (rollback) düştü, KVKK riski belirleyici oldu |

### Yol boyunca bulunan gerçek hatalar

1. **`cleanup-unverified` koruması sessizce kalkmıştı** — "şifre belirlemiş
   kiracıyı silme" kontrolü ambient bağlantıdan okuyordu; ayrılmadan sonra o
   bağlantı boş paylaşılan veritabanına bakıyor ve "kimsenin şifresi yok" diyordu.
   Korunması gereken kiracılar silinirdi. Koruma hiç test edilmemişti.
2. **Yedekleme script'leri boş bir veritabanını yedekliyor** — hâlâ açık,
   üretim öncesi kritik (aşağıda).

### Kapatılmayan, bilinen açıklar

- ⚠️ **Yedekleme kiracı kataloglarını kapsamıyor.** `scripts/backup-restore-drill.ps1`
  ve `register-backup-task.ps1` tek bir `-Database IKProDb` alıyor; o katalog artık
  boş. Script'ler `IKPro[_]%` desenini keşfedip her kiracı için ayrı yedek almalı.
  **Faz 3'ün ilk maddesi budur.**
- `POST /api/tenants/migrate` bir HTTP ucu; ayrı bir CLI komutu yok. Dağıtım
  script'i `curl -f` ile aynı sonucu alıyor, ama uygulama açılmadan migration
  çalıştırılamıyor (sıra: aç → migrate → müşteriler 503'ten çıkar).

**Sonraki:** Faz 3 — yedekleme ve kurtarma. `RESTORE … STOPAT` prosedürü
`docs/yedekleme-ve-kurtarma.md`'ye yazıldı ama **tatbikatı yapılmadı**.
