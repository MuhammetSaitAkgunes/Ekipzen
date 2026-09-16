# KVKK & Kiracı Veri İzolasyonu

> **Amaç:** Ekipzen çok-kiracılı (multi-tenant) SaaS mimarisinde her müşteri
> şirketinin (kiracının) verisinin nasıl ayrıştırıldığını, erişimin nasıl
> denetlendiğini ve KVKK yükümlülüklerinin teknik karşılıklarını **tek yerde**
> belgelemek. Bu doküman hem geliştirici hem de veri sorumlusu/DPO içindir.
>
> **Kapsam durumu:** Mimari izolasyon (veri ayrımı + erişim denetimi) üretime
> hazır ve testlerle kanıtlıdır. Veri **silme/anonimleştirme** ve **ilgili kişi
> hakları** akışları için bugün elle işletilen prosedürler tanımlıdır;
> otomasyon [Bilinen boşluklar](#7-bilinen-boşluklar--yapılacaklar) bölümünde
> yol haritasıyla listelenmiştir. Bu dosya "yapıldı" iddiası değil, mevcut
> durumun dürüst bir haritasıdır.

---

## 1. İzolasyon modeli — kiracı başına veritabanı + `TenantId` ayraç

Ekipzen **kiracı başına ayrı veritabanı** modeli kullanır: her müşteri şirketin
verisi kendi MSSQL kataloğunda durur (`Tenants.DatabaseName`), bağlantı kiracıdan
çözülür. Ayrım böylece bir sorgu kuralına değil, **fiziksel sınıra** dayanır.

Model 2026-08-13'te değişti; gerekçesi kurtarılabilirlikti: ortak veritabanında
bir müşterinin "verimizi dünkü hâline döndürün" talebi ya bütün müşterileri geri
sarmayı ya da satır satır elle onarımı gerektiriyordu. Ayrı katalogda tek
müşteri, diğerlerine hiç dokunmadan geri yüklenebilir.

**`TenantId` sütunu ve global sorgu filtresi KALDIRILMADI** — ikinci savunma
katmanı olarak duruyorlar:

> **Global sorgu filtresi:** EF Core, kiracı-kapsamlı her varlığa otomatik olarak
> `WHERE TenantId = @currentTenant` ekler. Uygulama kodu bu filtreyi elle yazmaz;
> dolayısıyla "filtre eklemeyi unutma" sızıntısı yapısal olarak imkânsızdır.

Bağlantı çözümü yanlış katalogu verse bile filtre veriyi karıştırmaz. İki
bağımsız savunma, tek kusursuz savunmadan iyidir. Ayrıca mekanizma, bir kiracıyı
geçici olarak paylaşılan veritabanında tutmayı (`DatabaseName` boş) mümkün
kılıyor — kademeli taşıma yolu açık kalsın diye.

### Nasıl çalışır

- `ITenantScoped` işaretleyici arayüzü (`int TenantId { get; set; }`) izolasyona
  tabi tüm tipleri işaretler.
- `AppDbContext`, model kurulumunda **reflection** ile `ITenantScoped` uygulayan
  her varlık tipini bulur ve her birine `HasQueryFilter(e => e.TenantId == CurrentTenantId)`
  uygular. Yeni bir varlık `BaseEntity`'den türediği an filtreye otomatik dahil olur.
- Aktif kiracı `ICurrentTenant.TenantId` üzerinden gelir; HTTP isteğinde JWT
  `tenant` claim'inden okunur (bkz. [Bölüm 3](#3-erişim-denetimi)).
- Yeni kayıtlar `AuditableEntityInterceptor` tarafından **kaydetme anında**
  otomatik olarak aktif `TenantId` ile damgalanır (uygulama kodu set etmek
  zorunda değildir).

Kaynak: `Ekipzen.Domain/Common/ITenantScoped.cs`,
`Ekipzen.Infrastructure/Persistence/AppDbContext.cs`,
`.../Interceptors/AuditableEntityInterceptor.cs`.

---

## 2. Katman katman izolasyon

İzolasyon yalnız EF varlıklarında değil, veriye erişen **her yolda** uygulanır.
Bir katmanın atlanması sessiz sızıntı demektir; bu yüzden her biri ayrı ele alındı:

| Katman | Mekanizma | Not |
|---|---|---|
| **Yazılabilir varlıklar** | `BaseEntity : ITenantScoped` + global filtre | Personel, izin, bordro, departman, evrak metadatası vb. |
| **Okuma modelleri / SQL view** | 6 read-model `ITenantScoped`; view'ler `TenantId` taşır | View'ler EF global filtresini **baypas eder**, bu yüzden `TenantId` sütununu kendileri taşır ve keyless entity olarak yine filtrelenir |
| **SQL fonksiyonu** | `fn_WorkingDays(@start, @end, @tenantId)` | Kiracı parametresi zorunlu; tatil günleri kiracıya göre |
| **Audit trigger'ları** | Tetikleyiciler etkilenen satırın `TenantId`'sini `COALESCE(i.TenantId, d.TenantId, 0)` ile kopyalar | Ham SQL EF'i baypas ettiği için trigger'lar `TenantId`'yi elle taşır |
| **Dosya/blob deposu** | **Metadata-kapılı erişim** | Fiziksel dosyalar kategori+GUID ile ortak kökte durur; erişim, dosyaya işaret eden **tenant-scoped DB satırı** üzerinden verilir → yabancı kiracı listeleme/indirme denemesinde **404** alır |
| **Kimlik/kullanıcı** | `ApplicationUser.TenantId`, `RefreshToken.TenantId` | Login pasif kiracıyı reddeder; token kiracı claim'i taşır |

**Dosya izolasyonu hakkında önemli ayrıntı:** Fiziksel dosyalar kiracıya özel
klasörlerde tutulmaz; bunun yerine her indirme/listeleme, tenant-scoped bir
`EmployeeDocument`/`ComplianceDocument` satırından geçer. Yabancı kiracı satırı
göremediği için dosyaya da erişemez (404). Bu, blob depolamada fiziksel ayrım
gerektirmeden mantıksal izolasyon sağlar. (İleride şifreli/kiracı-kapsamlı
fiziksel depolama bir sertleştirme adımıdır — bkz. Bölüm 7.)

Kaynak: `Ekipzen.Domain/ReadModels/*`, view/fonksiyon migration'ları
(`...TenantAwareViews.cs`), `Ekipzen.Infrastructure/Storage/LocalFileStorage.cs`.

### Kanıt: izolasyon testleri

İzolasyon iddiaları test edilir, varsayılmaz. `Ekipzen.Tests.Integration/Tenancy/`:

- `TenantIsolationTests` — Kiracı A, Kiracı B'nin departman/personel verisini görmez.
- `TenantViewIsolationTests` — uyum hazırlık view'i kiracı başına ayrışır (global
  toplam sızmaz).
- `TenantFileIsolationTests` — yabancı kiracının evrakı 404.
- `TenantProvisioningTests` — provizyonlanan admin yalnız kendi kiracısını görür;
  pasif kiracıya giriş reddedilir.

Bu testler her fazda çalışır; kiracı izolasyonu ürünün **en kritik güvencesidir**.

---

## 3. Erişim denetimi

Kiracı sınırının yanı sıra kiracı **içinde** rol tabanlı yetkilendirme vardır.

### Kiracı çözümü (tenant resolution)

- Kullanıcı giriş yaptığında JWT'ye `tenant` claim'i yazılır.
- Her istekte `CurrentTenant`, bu claim'den aktif kiracıyı okur ve global filtre
  bu değere göre çalışır. İstemci `TenantId`'yi gönderemez veya değiştiremez —
  yalnız sunucunun imzaladığı token'dan gelir.
- **Pasif kiracı:** `Tenant.IsActive = false` ise login reddedilir (401). Aktif
  oturumlar refresh anında da doğrulanır.

### Roller (kiracı içi)

`hr-admin`, `manager`, `employee` — ASP.NET Core policy katmanında uygulanır.
Örn. işe alım yalnız `hr-admin`; manager yalnız kendi ekibinin uyum belgelerini
ve risk skorlarını görür (`ScopeFor` kapsamı).

### Platform seviyesi (kiracı üstü)

Kiracı oluşturma (`POST /api/tenants`) normal rol sisteminin **dışındadır**; bu
platform operasyonu `X-Platform-Key` başlığıyla korunur ve `auth` rate-limit
politikasına tabidir. Platform anahtarı ve JWT sırrı üretimde **ortam
değişkeninden** gelir; commit'li dev placeholder'lar prod'da fail-fast ile
reddedilir.

Kaynak: `Ekipzen.API/Services/CurrentTenant.cs`,
`Ekipzen.API/Controllers/TenancyController.cs`, `Ekipzen.API/Program.cs`.

---

## 4. Kimlik & güvenli onboarding (davet akışı)

Kiracılar arası güvenli hesap kurulumu için geçici paylaşılan şifreler
kullanılmaz:

- Provizyonlanan `hr-admin` ve işe alınan personel **şifresiz** oluşturulur.
- Sisteme bir davet e-postası + şifre-belirleme token'ı gönderilir.
  Gönderim `Email:Mode` yapılandırmasına göre değişir: varsayılan `outbox`
  (dev/demo, dosyaya yazar); üretimde `smtp` (MailKit, gerçek SMTP — Host/From
  eksikse startup fail-fast).
- Kullanıcı `POST /api/auth/accept-invite` (veya `/#/accept-invite` sayfası) ile
  kendi şifresini belirler; token geçersiz/süresi geçmişse reddedilir.

Bu, bir kiracının başka bir kiracının kullanıcısı için hesap ele geçirmesini
önler ve KVKK "uygun güvenlik önlemleri" ilkesine hizmet eder.

Kaynak: `Ekipzen.Application/Features/Auth/AcceptInvite/`,
`Ekipzen.Infrastructure/Identity/IdentityService.cs`, `frontend/src/auth/AcceptInvitePage.tsx`.

---

## 5. Denetim izi (audit) & izlenebilirlik

- Hassas tablolar için veritabanı **audit trigger'ları** değişiklikleri
  `AuditLogs`'a yazar; her kayıt etkilenen satırın `TenantId`'sini taşır (kiracı
  başına denetim izi).
- Denetim kayıtları da `ITenantScoped`/global filtreye tabidir → bir kiracının
  denetçisi yalnız kendi kiracısının izini görür.

Bu, KVKW ilgili kişi başvurularında "bu veri ne zaman, kim tarafından değişti"
sorusuna kiracı-kapsamlı yanıt verebilmeyi hedefler.

---

## 6. KVKK ilkeleriyle eşleme

| KVKK ilkesi / yükümlülük | Teknik karşılık (mevcut) |
|---|---|
| **Amaçla sınırlılık / erişim denetimi** | Kiracı global filtresi + rol policy'leri + JWT tenant claim. Kiracı ve rol dışına veri gitmez. |
| **Veri güvenliği (uygun önlemler)** | Şifresiz davet akışı, refresh token rotasyonu, rate limiting, sırların env'den gelmesi, HTTPS (deployment sorumluluğu). |
| **Veri sorumlusu / veri işleyen ayrımı** | Her müşteri şirketi kendi verisinin **veri sorumlusudur**; Ekipzen işletmecisi **veri işleyendir**. Kiracı ayrımı bu sınırın teknik zeminidir. |
| **Hesap verebilirlik / izlenebilirlik** | Kiracı-kapsamlı audit log (Bölüm 5). |
| **Saklama süresi / silme** | Kiracı pasifleştirme + **otomatik kalıcı silme** (`DELETE /api/tenants/{id}`, confirm-slug) ve doğrulanmamış kiracı temizliği mevcut; anonimleştirme varyantı yol haritasında (Bölüm 7). |
| **İlgili kişi hakları (erişim, düzeltme, silme, taşınabilirlik)** | Erişim/düzeltme uygulama içinden; **kendi veri dışa aktarımı** (`GET /api/me/data-export`, JSON) ve **kiracı silme** (`DELETE /api/tenants/{id}`) mevcut; anonimleştirme varyantı yol haritasında (Bölüm 7). |

> **Sorumluluk sınırı:** Bu doküman teknik izolasyonu belgeler; bir müşteriyle
> yapılacak KVKK/veri işleme sözleşmesi (DPA), aydınlatma metni ve VERBİS
> yükümlülükleri **hukuki** süreçlerdir ve bu dosyanın kapsamı dışındadır.

---

## 7. Bilinen boşluklar & yapılacaklar

Dürüstlük için, bugün **otomatik olmayan** veya eksik olan maddeler:

1. **Kiracı verisi silme (otomasyon MEVCUT).**
   Kalıcı silme artık tek işlemle yapılır: `DELETE /api/tenants/{id}?confirmSlug=`
   (platform-key korumalı; `confirmSlug` hedef kiracının slug'ıyla eşleşmezse
   reddedilir — yanlış-id koruması). `ITenantPurger`, silinecek tabloları EF model
   metadata'sından (ITenantScoped + PK'lı) türetir ve FK-güvenli sırada tek
   transaction'da siler: tüm kiracı-kapsamlı satırlar + kullanıcılar + refresh
   token'lar + audit + fiziksel evrak dosyaları + kiracı satırı. Yeni bir
   kiracı-kapsamlı tablo eklendiğinde otomatik kapsanır (unutma sızıntısı yok).
   Fiziksel dosyalar DB commit'inden sonra silinir; bir dosya silinemezse işlem
   yarıda kesilmez ve hata sessizce yutulmaz — elle temizlik için loglanır
   (artık PII riski görünür kalır).
   - **Doğrulanmamış kiracı temizliği:** `POST /api/tenants/cleanup-unverified?olderThanDays=`
     — pasif + eski + hiç şifre belirlememiş (davet hiç kabul edilmemiş, self-servis)
     kiracıları toplu siler; askıya alınmış (şifreli kullanıcısı olan) kiracılar
     korunur. Ayrıca **uygulama-içi zamanlayıcı** (`UnverifiedTenantCleanupService`,
     `BackgroundService`) bu temizliği dış cron'a gerek kalmadan periyodik çalıştırır
     — `Cleanup:UnverifiedTenants` ile yapılandırılır (`Enabled`/`IntervalHours`/
     `OlderThanDays`); yıkıcı olduğu için **varsayılan kapalı**, üretimde açılır.
   - **Halen yapılacak:** *anonimleştirme* varyantı (silme yerine PII maskeleyip
     istatistiği koruma) ve silme işleminin ayrı bir denetim kaydına (audit)
     yazılması.
2. **İlgili kişi verisi dışa aktarımı (taşınabilirlik) — MEVCUT.**
   `GET /api/me/data-export` — oturum açmış herhangi bir kullanıcı (rolden
   bağımsız) kendi verisini tek istekle makine-okunur JSON paketi olarak indirir:
   hesap bilgisi, (varsa) bağlı personel kaydı + profil (iletişim/banka/özlük),
   izin talepleri ve bakiyeleri, puantaj kayıtları, uyum belgeleri, bordro
   pusulası listesi. Yalnız **kendi** `EmployeeId`'sine bağlı kayıtlar dahil
   edilir — başka kullanıcının verisi asla sızmaz (tenant izolasyonu + sorgu
   daraltması çifte güvence; entegrasyon testiyle kanıtlı). Frontend'de üst
   menüde "Verilerimi indir" ikonu. Fiziksel evrak dosyalarının (PDF vb.)
   pakete gömülmesi kapsam dışı — yalnız metadata döner (bkz. madde 3'teki
   fiziksel dosya durumu).
3. **Fiziksel dosya şifreleme / kiracı-kapsamlı fiziksel ayrım.** Bugün erişim
   metadata ile kapılı; blob'lar ortak kökte şifresiz. **Yapılacak:** at-rest
   şifreleme ve/veya kiracı-önekli depolama.
4. **Gerçek e-posta (SMTP) — MEVCUT.** `IEmailSender`'ın MailKit tabanlı SMTP
   implementasyonu eklendi; `Email:Mode=smtp` + `Smtp:Host`/`Smtp:From` (env'den)
   ile etkinleşir. Varsayılan hâlâ `outbox` (dev/demo). Üretime geçerken yalnız
   yapılandırma değişir, kod değişmez.
5. **Yedekleme/geri yükleme kiracı granülaritesi — ALTYAPISI HAZIR.** Her kiracı
   kendi kataloğunda olduğu için tek-kiracı yedeği/geri yüklemesi artık teknik
   olarak mümkün. Prosedürün kendisi (dondur → geri yükle → çöz, oturumların
   geçersizleşmesi dahil) Faz 3'te yazılacak; bkz. `docs/yedekleme-ve-kurtarma.md`.

Bu maddeler `docs/superpowers/plans/2026-07-17-multi-tenant-saas.md` "Sonraki
adımlar" ve MVP sertleştirme kapsamıyla uyumludur.

---

## 8. Operasyonel kontrol listesi (üretim öncesi)

- [ ] `Jwt:Secret`, connection string ve `Platform:ProvisioningKey` ortam
      değişkeninden geliyor; dev placeholder yok (prod fail-fast doğrular).
- [ ] `Email:Mode=smtp` + `Smtp:Host`/`Smtp:From` yapılandırılmış; `Smtp:Password`
      ortam değişkeninden geliyor (appsettings'e yazılmadı).
- [ ] HTTPS zorunlu; token'lar yalnız TLS üzerinden.
- [ ] Yeni eklenen her veri tablosu `BaseEntity`'den türüyor (otomatik kiracı
      filtresi) **veya** bilinçli olarak kiracı-üstü ve gözden geçirilmiş.
- [ ] Yeni SQL view/fonksiyon/trigger `TenantId` taşıyor.
- [ ] İzolasyon testleri (`Tenancy/`) yeşil; yeni modül için izolasyon testi eklendi.
- [ ] Müşteriyle DPA imzalı; aydınlatma metni ve VERBİS kaydı tamam (hukuki).
- [ ] Silme/anonimleştirme prosedürü (Bölüm 7.1) müşteri sözleşmesindeki saklama
      süresiyle eşleştirildi.

---

*Son güncelleme: 2026-07-20 — doğrulanmamış kiracı temizliği için uygulama-içi
zamanlayıcı eklendi (Bölüm 7.1). Aynı gün önceki güncellemeler: ilgili kişi verisi
dışa aktarımı (Bölüm 7.2), SMTP gerçek e-posta göndericisi (Bölüm 7.4), kiracı
purge (Bölüm 7.1). Uygulama değiştikçe bu dosya da güncellenmelidir; özellikle
Bölüm 7'deki maddeler tamamlandıkça Bölüm 6 tablosuna taşınır.*
