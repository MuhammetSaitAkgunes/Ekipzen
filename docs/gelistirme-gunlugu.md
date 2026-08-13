# İK Pro — Geliştirme Günlüğü

> **Amaç:** Geliştirme kesilirse en son nerede kalındığını bu dosyadan anlamak.
> Her oturum sonunda buraya tarihli bir kayıt eklenir; en üstteki "Şu an neredeyiz"
> bölümü daima güncel tutulur.

## Şu an neredeyiz

- **Aktif iş:** **Faz 2b — kiracı başına ayrı veritabanı.** Dal
  `claude/faz2b-ayrilma`, plan `docs/superpowers/plans/2026-08-12-faz2b-ayrilma.md`.
  **FAZ 2b TAMAMLANDI (Görev 0–7).** Her kiracı kendi veritabanında
  (`IKPro_demo`, `IKPro_globex`); paylaşılan `IKProDb` boş ama duruyor. Purge
  `DROP DATABASE`, abonelikler platformda, migration ayrı bir dağıtım adımı.
  **Sırada Faz 3 var: yedekleme ve kurtarma** — ve ona başlamadan önce
  kullanıcıyla konuşulacak.
- **⚠️ Dağıtım sırası değişti:** üretimde artık üç adım var — (1) `IKProDb`
  migrate, (2) `IKProPlatform` migrate, (3) **`POST /api/tenants/migrate`**
  (kiracı katalogları). 3. adım atlanırsa müşteriler 503 alır (kasıtlı) ve durum
  açılış logunda + `GET /api/tenants/schema-status`'ta görünür.
- **⏸️ Faz 3 GEÇİCİ OLARAK RAFA KALDIRILDI (2026-08-13, kullanıcı kararı).**
  Önce birkaç revizyon yapılacak; Faz 3'ün kalan maddesi sonraya bırakıldı.

  **Faz 3'te tamamlananlar:** yedekleme tüm kiracı kataloglarını kapsıyor
  (keşif platform tablosundan), RPO/RTO belirlendi (RPO 1 saat; RTO tek müşteri
  4 saat, tüm sistem mesai içi), saatlik log yedeği script'i yazıldı,
  **zamanlanmış görevler kuruldu ve SYSTEM olarak başarıyla koştu**, üç tatbikat
  gerçekten çalıştırıldı (tam yedek/geri yükleme, nokta-zaman `STOPAT`, ve
  uygulama katmanı `freeze → geri yükle → rebuild-directory → unfreeze`).

  **⚠️ Bilinçli olarak ertelenen tek madde — off-site kopya.** Yedekler şu an
  SQL Server'ın kendi klasöründe, yani **kaynakla aynı diskte**. Disk arızasında
  veriyle birlikte giderler; bugünkü kurulum disk kaybına karşı koruma
  SAĞLAMIYOR. `-OffsitePath` parametresi hazır ve kopyayı boyut olarak
  doğruluyor; eksik olan yalnız gerçek bir hedef (ağ paylaşımı / nesne depolama)
  ve SYSTEM hesabının oraya erişiminin doğrulanması. Üretime çıkmadan önce
  kapatılmalı.

  Ayrıca bir sonraki tatbikatta: **evrak dosyalarının** (`-StoragePath` zip'leri)
  geri yüklenmesi hiç denenmedi — tatbikatlar veritabanı ve uygulama katmanını
  kapsadı, dosya katmanını değil.
- **⚠️ Üretim öncesi kritik açık:** yedekleme script'leri hâlâ tek bir
  `-Database IKProDb` alıyor ve o katalog artık **boş**. Yani şu anda alınan
  yedekler gerçek müşteri verisini içermiyor. Script'ler `IKPro[_]%` desenini
  keşfedip her kiracı için ayrı yedek almalı — bkz. `docs/yedekleme-ve-kurtarma.md`.
- **Geliştirme kurulumunu sıfırlarken:** artık `IKProDb` + `IKProPlatform`
  yetmez, **tüm `IKPro_*` kiracı veritabanları da birlikte düşürülmeli**. Aksi
  halde provizyon "katalog zaten var" diye reddeder (bilinçli koruma).
- **Kayıt (signup) politikası — kalıcı karar:** Uygulamada **hiçbir** self-servis
  kayıt ucu yok. Yeni şirket yalnız operatör tarafından `POST /api/tenants`
  (`X-Platform-Key`) ile açılır; katılımın tek yolu davettir. Gerekçe:
  kiracı başına DB'de "kaydol" bir veritabanı oluşturur.
- **Son tamamlanan:** **T5.2 davet/şifre-belirleme akışı.** Provizyonlanan
  hr-admin ve işe alınan personel artık `demo123` yerine **şifresiz** oluşturulur;
  davet e-postası (outbox stub) şifre-belirleme token'ı gönderir. Yeni uçlar:
  `POST /api/auth/accept-invite` (anonim) + frontend `/#/accept-invite` sayfası
  (token'la şifre belirle → otomatik giriş). Identity'ye `AddDefaultTokenProviders()`
  eklendi (reset-token üretimi için). 79 entegrasyon + 9 birim (backend) ve 134
  birim (frontend) test + her iki build yeşil.
- **Multi-tenancy özeti:** Ortak DB + `TenantId` ayraç + EF global sorgu filtresi
  (`ITenantScoped` üzerinden reflection'la, "filtre unutma" sızıntısı imkânsız);
  kiracı JWT `tenant` claim'inden çözülür; SQL view'ler + `fn_WorkingDays` +
  audit trigger'lar TenantId taşır; dosya deposu kiracı-kapsamlı. Provizyon
  `POST /api/tenants` platform anahtarıyla (`X-Platform-Key`) korunur. İzolasyon
  7 tenancy testiyle kanıtlı.
- **Son tamamlanan:** Kod-inceleme (senior-backend + code-review) sonrası üç
  düzeltme, TDD ile. **#2:** `UnverifiedTenantCleanupService` — `IntervalHours ≤ 0`
  yapılandırması `PeriodicTimer` ctor'unu fırlatıp BackgroundService `StopHost`
  ile tüm host'u çökertiyordu; guard eklendi (loglanıp devre dışı, host ayakta).
  **#3:** self-servis signup slug'ı kontrol-sonra-ekle yarışında unique index
  ihlaliyle 500 dönebiliyordu; retry (DbUpdateException'da bırak-yeniden dene,
  yeniden denemede rastgele ekli slug) eklendi — 4 eşzamanlı kayıt testiyle
  kilitlendi. **#4:** `TenantPurger` dosya silmede biri fırlarsa purge'ü yarıda
  kesip 500 dönüyor + PII dosya artığı bırakıyordu; her dosya ayrı try/catch +
  loud log, işlem tamamlanır. 19 birim + 95 entegrasyon yeşil. Plan:
  `docs/superpowers/plans/2026-07-20-inceleme-fix-2-3-4.md`.
- **Önceki tamamlanan:** Doğrulanmamış kiracı zamanlanmış temizlik —
  `UnverifiedTenantCleanupService` (`BackgroundService`), `cleanup-unverified`'ı
  uygulama-içi periyodik çalıştırır (dış cron gerektirmez). Mevcut
  `CleanupUnverifiedTenantsCommand`'i yeniden kullanır (yeni silme mantığı yok);
  tek geçiş `RunOnceAsync`'e ayrıldı (deterministik test, timer flakiness yok).
  `Cleanup:UnverifiedTenants` ile yapılandırılır; **varsayılan kapalı** (yıkıcı
  işlem, opt-in). 19 birim + 91 entegrasyon (+1) yeşil. Plan:
  `docs/superpowers/plans/2026-07-20-dogrulanmamis-kiraci-zamanlanmis-temizlik.md`.
- **Önceki tamamlanan:** İlgili kişi verisi dışa aktarımı — `GET /api/me/data-export`:
  oturum açmış herhangi bir kullanıcı kendi verisini tek istekle JSON paketi
  olarak indirir (hesap + varsa bağlı personel/profil + izin + puantaj + uyum
  belgeleri + bordro listesi metadata). Yalnız kendi `EmployeeId`'sine bağlı
  kayıtlar dahil edilir; tenant izolasyonu + sorgu daraltması çifte güvence
  (test: başka çalışanın adı asla pakette yok; `EmployeeId`'siz kullanıcı
  crash almadan `employee: null` alır). `ICurrentUser`'a `Email` eklendi (JWT
  `email` claim'inden — tek implementasyon, güvenle genişletildi). Frontend
  üst menüde "Verilerimi indir" ikonu (`apiDownload` deseni, bordro pusulası
  indirmeyle aynı). 19 birim + 90 entegrasyon (+2), 136 birim (frontend) yeşil.
  Plan: `docs/superpowers/plans/2026-07-20-ilgili-kisi-veri-disa-aktarim.md`.
- **Önceki tamamlanan:** SMTP gerçek e-posta göndericisi — `IEmailSender`'ın MailKit
  tabanlı `MailKitSmtpEmailSender` implementasyonu eklendi. Seçim `Email:Mode`
  yapılandırmasıyla yapılır: varsayılan `outbox` (dev/demo/test — davranış
  DEĞİŞMEDİ, davet testleri hâlâ dosyadan token okuyor); `smtp` → `SmtpOptions`
  (Host/Port/User/Password/From/FromName/UseStartTls) bağlanır ve `ValidateOnStart()`
  ile fail-fast doğrulanır (Host veya From eksikse başlangıçta patlar — JWT sır
  kontrolüyle aynı ilke). MailKit 4.16.0 seçildi (4.8.0'da CVE-2026-41319 StartTls/SASL
  indirgeme açığı vardı, restore uyarısıyla yakalandı). `appsettings.json`'a örnek
  `Email`/`Smtp` bloğu eklendi (şifre boş — env'den gelir). 19 birim (+4 SmtpOptions)
  + 88 entegrasyon (+1 seçim testi) yeşil. Plan:
  `docs/superpowers/plans/2026-07-20-smtp-eposta-gonderici.md`.
- **Önceki tamamlanan:** Kiracı purge — `ITenantPurger` bir kiracının TÜM verisini
  (ITenantScoped tablolar EF metadata'sından FK-güvenli sırada + kullanıcılar +
  refresh token + audit + fiziksel evrak + kiracı satırı) tek transaction'da
  siler. Uçlar: `DELETE /api/tenants/{id}?confirmSlug=` (platform-key + slug
  onayı) ve `POST /api/tenants/cleanup-unverified?olderThanDays=` (pasif +
  doğrulanmamış kiracıları toplu siler; askıya alınmışları korur). 15 birim +
  87 entegrasyon yeşil. Not: takvim 2026-07-20'ye ilerlediği için tarih-kırılgan
  bir leaves testi (`LeaveLifecycle` takım widget'ı) düzeltildi — bugüne göreli
  izinle test edilecek şekilde. Plan:
  `docs/superpowers/plans/2026-07-20-kiraci-purge-temizlik.md`.
- **Önceki tamamlanan:** Self-servis kayıt — public `POST /api/tenants/signup`
  (platform anahtarı yok, ayrı `signup` rate-limit'i), sunucuda slug türetme,
  kiracı **pasif** başlar ve ilk admin davet e-postasını kabul edince
  **etkinleşir** (e-posta doğrulama kapısı). Frontend `/#/register-company`
  sayfası + login bağlantısı. 15 birim + 82 entegrasyon (backend), 136 birim
  (frontend), her iki build yeşil. Plan:
  `docs/superpowers/plans/2026-07-17-self-servis-kiraci-kaydi.md`.
- **Sıradaki adaylar:** kiracı verisi **anonimleştirme** varyantı (silme yerine
  PII maskeleme), kiracı bazlı yedekleme/geri yükleme, fiziksel dosya at-rest
  şifreleme. Detay: `docs/kvkk-veri-izolasyonu.md` Bölüm 7.
- **Referanslar:**
  - Tasarım dokümanı: `docs/superpowers/specs/2026-07-13-react-frontend-port-design.md`
  - Dilim 1 planı (tamamlandı): `docs/superpowers/plans/2026-07-13-react-port-dilim-1-iskelet.md`
  - Dilim 2 planı (tamamlandı): `docs/superpowers/plans/2026-07-14-react-port-dilim-2-overview-dashboard.md`
  - Dilim 3 planı (tamamlandı): `docs/superpowers/plans/2026-07-14-react-port-dilim-3-personel.md`
  - Dilim 4 planı (tamamlandı): `docs/superpowers/plans/2026-07-14-react-port-dilim-4-izin-puantaj.md`
  - Dilim 5 planı (tamamlandı): `docs/superpowers/plans/2026-07-14-react-port-dilim-5-bordro.md`
  - Dilim 6 planı (tamamlandı): `docs/superpowers/plans/2026-07-16-react-port-dilim-6-isealim-uyum.md`
  - Dilim 7 planı (tamamlandı): `docs/superpowers/plans/2026-07-16-react-port-dilim-7-aksiyonlar-arama-ayarlar.md`
  - Dilim 8 planı (tamamlandı): `docs/superpowers/plans/2026-07-16-react-port-dilim-8-kapanis.md`
  - Backend faz planı: `raporlar/backend-development-plan.md`
- **Çalıştırma komutları:**
  - Backend: `cd backend && dotnet run --project src/IKPro.API --launch-profile http` → `http://localhost:5053`
  - Frontend: `cd frontend && npm run dev` → `http://localhost:5173`
  - Testler: `cd frontend && npm test -- --run`
  - Eski mock uygulama (parite referansı): `npx serve -l 4173 legacy-frontend`

---

## Kayıtlar (yeni → eski)

### 2026-08-13 — Faz 3 (6): uygulama katmanı kurtarma tatbikatı — 3 eksik bulundu

Belgede yazan tam prosedür (`freeze → geri yükle → rebuild-directory →
unfreeze`) çalışan bir uygulamayla, gerçek bir kiracı üzerinde denendi.

**Senaryo:** `IKPro_globex`'te tam yedek + kurtarma noktası; sonra bir aday işe
alındı (kiracı kataloğunda personel + kullanıcı, PLATFORM'da dizin kaydı);
ardından "kaza" olarak tüm personel silindi.

**Prosedür üç yerde eksikti:**

1. **`freeze` yerinde geri yükleme için yetmiyor.** Uygulama isteklerini kesiyor
   ama bağlantı havuzunu kapatmıyor; ilk geri yükleme denemesi "Exclusive access
   could not be obtained" ile düştü. `SET SINGLE_USER WITH ROLLBACK IMMEDIATE`
   şart — ve sonunda `SET MULTI_USER` da, çünkü geri yükleme bunu kendi yapmıyor.
2. **Saatlik log yedeği kurtarmanın ORTASINDA araya girdi.** Zamanlanmış görev
   11:00'de `BACKUP LOG` aldı; benim kuyruk yedeğim artık tam yedeğe oturmadı
   (`Msg 4305`). Çözüm prosedüre yazıldı: ya görevi kurtarma boyunca durdurun,
   ya da araya giren log'ları `msdb`'den okuyup zincire ekleyin (ikincisi daha
   güvenli — hiçbir işlem atlanmaz). Prosedür artık dosya adı tahmin etmek
   yerine zinciri `msdb`'den okumayı söylüyor.
3. **Hedef an tam yedeğin anına eşitse log uygulanmaz** (`Msg 4335`).

**`rebuild-directory`'nin neden zorunlu olduğu somutlaştı.** Geri yüklemeden
sonra dizinde `YENI.PERSONEL@GLOBEX.LOCAL` duruyordu — kurtarma noktasından
sonra işe alınmıştı, yani kiracı kataloğunda artık yok ama platformda var. Bu
öksüz kayıt login'i var olmayan bir hesaba yönlendirir. `rebuild-directory`
dizini kiracının kendi veritabanından yeniden kurup temizledi; o e-postayla
giriş artık doğru şekilde 401 dönüyor.

**Yalıtım kanıtlandı:** tatbikat boyunca demo kiracısı hiç etkilenmedi —
donduğunda diğer kiracı 200 döndü, kurtarma bitince globex hizmete döndü.
Ayrılmanın vaadi buydu ve çalıştığı görüldü.

### 2026-08-13 — Faz 3 (5): zamanlanmış görevler kuruldu, SQL yetkisi verildi

Görevler kaydedildi (`IKPro-YedekTatbikati` + `-Log`) ama ilk koşum başarısız
oldu. Log dosyası sebebi söyledi: `Login failed for user 'NT AUTHORITY\SYSTEM'`.

**Teşhis yanıltıcı mesajın ardındaydı.** `EXECUTE AS` ile bakıldı: SYSTEM login'i
VAR ve `CONNECT SQL` yetkisi de var; gerçek sebep **hata 916** — hiçbir kullanıcı
veritabanına erişemiyor. sqlcmd bunu "Login failed" diye gösteriyor.

**Veritabanı-başına yetki neden çözüm değil:** yedek hesabı bugün var olmayan
kiracı veritabanlarına da erişmek zorunda. Her yeni müşteride biri yetki vermezse
yedekleme o müşteri için SESSİZCE kırılır — yani yedeklemenin çalışması
"kimsenin unutmaması"na bağlı hale gelir. Sunucu seviyesinde yetki gerekli.

**Karar (kullanıcı onayıyla):** `NT AUTHORITY\SYSTEM` sysadmin yapıldı.
Alternatifi (ayrı hizmet hesabı) belgeye yazıldı ve script'e `-TaskUser`
parametresi eklendi; uygulama ile veritabanı aynı makinede olduğu için güvenlik
dengesi açıkça not edildi.

Ek olarak script kayıttan sonra "SON ADIM" uyarısını yazıyor (sysadmin verilmeden
görevler çalışır ama başarısız olur) ve belgeye üretim notu eklendi: script'ler
kullanıcı profilinde/OneDrive'da durmamalı — SYSTEM görevi profil ve bulut
senkronuna bağımlı hale gelir.

### 2026-08-13 — Faz 3 (4): operatör komutlarındaki üç tuzak düzeltildi

Kullanıcı belgedeki kurulum komutunu çalıştırdı ve patladı. Üç ayrı hata vardı,
üçü de dokümantasyondan geliyordu:

1. **`pwsh` bu makinede kurulu değil.** PowerShell 7 Windows'ta kurulu gelmez;
   script'ler Windows PowerShell 5.1 ile çalışıyor. Belgedeki ve script
   yardımlarındaki tüm `pwsh` örnekleri `powershell -File` oldu.
2. **Komut worktree dizininden çalıştırıldı.** `.claude/worktrees/…` farklı bir
   dalın kopyası ve `scripts/` klasörünü hiç içermiyor. Belgeye "ana depo
   kökünden çalıştırın" notu eklendi.
3. **`C:\SQLYedek` yolu çalışmaz.** Yedeği alan sizin hesabınız değil, SQL
   Server servis hesabı (`NT Service\MSSQLSERVER`). Yeni açılan bir klasöre
   servisin yazma izni olmaz ve yedek "Operating system error 3/5" ile düşer.
   Tüm örnekler SQL Server'ın kendi yedek klasörünün altını kullanacak şekilde
   değiştirildi; başka klasör için `icacls` komutu yazıldı.

Ayrıca belgede **eskimiş örnekler** vardı: `-Database IKProDb` /
`-Database IKProPlatform` ile iki ayrı çağrı ve iki ayrı zamanlanmış görev
(`-Db`, `-Platform`). Bu artık yanlış — script hepsini platformdan keşfediyor ve
tek `register` çağrısı iki görevi (günlük + saatlik) birden kuruyor. Ad vermek
diğer müşterileri yedeksiz bırakır; örnekler bunu öğretiyordu.

Belgeye "Script'leri çalıştırmadan önce — üç tuzak" bölümü eklendi ve
dokümantasyondaki komut `-WhatIf` ile bire bir denenerek doğrulandı.

### 2026-08-13 — Faz 3 (3): saatlik log yedeği — RPO'yu gerçek yapan parça

RPO 1 saat kararı verilmişti ama sistemde yalnız elle çalıştırılan tam yedek
vardı; yani taahhüt kâğıt üstündeydi. `backup-log.ps1` yazıldı.

- **Ortak modül `IKProBackup.psm1`.** Keşif/SQL/log mantığı tek yere alındı ve
  `backup-restore-drill.ps1` de ona taşındı. Gerekçe: iki script AYNI veritabanı
  kümesinde çalışmak zorunda; ayrı ayrı yazılsalardı zamanla ayrışırlardı ve
  sonuç sessiz olurdu — tam yedeği alınan ama günlüğü alınmayan müşteri, felaket
  anına kadar fark edilmez. (Modülü yazıp drill'i taşımamak, tam da uyardığım
  duruma düşmek olurdu.)
- **Saklama temizliği:** süresi dolan `.trn` dosyaları siliniyor. `.bak`
  dosyalarına DOKUNULMUYOR — bir log zinciri dayandığı tam yedek olmadan işe
  yaramaz; yanlışlıkla temeli silmek, elde binlerce günlük olup hiçbirini
  kullanamamak demektir.
- **`register-backup-task.ps1` artık İKİ görev kaydediyor** (günlük tam yedek +
  saatlik günlük). Saatlik olan isteğe bağlı değil: seçenek sunmak, RPO'yu
  sessizce 24 saate çıkaran ayarın atlanmasına davetiye olurdu.

**Yol boyunca bir varsayımım yanlış çıktı ve düzeltildi.** Script önce "bu
veritabanının tam yedeği var mı?" diye `msdb`'ye soruyordu. `IKPro_globex` bu
kontrolü GEÇTİ ama `BACKUP LOG` yine de patladı (Msg 4214): kurtarma modelini
SIMPLE'a alıp FULL'e döndürmek **log zincirini kırıyor**, oysa `msdb`'deki eski
tam yedek kaydı yerinde duruyor. Sebebi tahmin etmek yerine duruma tepki verecek
şekilde yeniden yazıldı: dene → 4214 alırsan TABAN tam yedeği al → bir kez
yeniden dene. Bu, sebebi ne olursa olsun (yeni kiracı, model dönüşü, geri
yükleme) çalışır.

**Uçtan uca kanıtlandı:** `IKPro_globex`'e işaret satırı eklendi, script'in
ÜRETTİĞİ dosyalardan (TABAN.bak + iki .trn) ayrı bir ada geri yükleme yapıldı ve
işaret satırı geri geldi. Yani otomasyonun bıraktığı dosyalar gerçekten
kullanılabilir bir zincir oluşturuyor — elle hazırlanmış dosyalar değil.

Tüm tatbikat artıkları (kopya veritabanları, işaret satırları, yedek dosyaları)
temizlendi.

### 2026-08-13 — Faz 3 (2): RPO/RTO kararı + kurtarma modeli tuzağı

**Karar:** RPO **1 saat**; RTO **tek müşteri 4 saat**, **tüm sistem bir sonraki
mesai gününün sonu**.

- **RPO 1 saat:** kaybedilen saatte tipik olarak birkaç izin talebi, o sabahın
  puantajı ve personel kartı düzenlemeleri olur. En sinsisi sonuncusu — izin ve
  puantaj yeniden girilir ama "dün kartta neyi değiştirmiştim" kimse hatırlamaz.
  Bordro kaynak veriden yeniden koşturulabilir, yani kayıp değil tekrar iştir.
- **RTO neden iki tane:** kiracı başına veritabanından sonra tek müşteriyi geri
  yüklemek diğerlerine dokunmadan yapılan küçük bir iş; tüm sunucu ise müşteri
  sayısıyla büyür. Tek sayı vermek birini abartıp diğerini gizlemek olurdu.
- **"Tüm sistem" neden mesai içi:** `RTO = fark etme + karar + geri yükleme +
  doğrulama` ve bugün **fark etme ayağı yok** (uyarı webhook'u, zamanlanmış görev
  ve nöbet kurulmadı). Daha sıkı bir sayı taahhüt değil temenni olurdu.

**Kararın açtığı gizli tuzak bulundu ve kapatıldı.** Saatlik log yedeği yalnız
FULL kurtarma modelinde mümkün; SIMPLE'da işlem günlüğü yedeği ALINAMAZ ve
`RESTORE … STOPAT` imkânsız olur. Oysa `CREATE DATABASE` kurtarma modelini
sunucunun `model` veritabanından **miras alıyordu** — bu makinede FULL olduğu
için tatbikat çalışmıştı, ama SQL Server Express ve pek çok sıkılaştırılmış
kurulum SIMPLE gelir. Yani RPO taahhüdü başka bir sunucuda **sessizce** geçersiz
olurdu; belirti ancak felaket anında çıkardı. Harmanlama sorununun birebir aynısı.

İki yerden kapatıldı:
1. Provizyon `ALTER DATABASE … SET RECOVERY FULL` ile modeli açıkça sabitliyor
   (`VeritabaniSaglayiciTests` doğruluyor).
2. Yedek tatbikatı her veritabanının FULL olduğunu kontrol ediyor; değilse o
   veritabanı için başarısız oluyor ve düzeltme komutunu yazıyor. Platform ve
   paylaşılan katalog EF tarafından oluşturulduğu için provizyon yolundan
   geçmez — bu kontrol onları da kapsar.

Kontrolün gerçekten çalıştığı **kanıtlandı**: `IKPro_globex` geçici olarak
SIMPLE'a alındı, tatbikat onu yakalayıp çıkış kodu 1 döndürdü, sonra FULL'e
geri alındı.

**Dürüst not:** RPO 1 saat şu an **kâğıt üstündedir** — saatlik log yedeği henüz
zamanlanmadı. `register-backup-task.ps1` tam yedeği zamanlıyor; log yedeği için
ayrı bir saatlik görev gerekiyor ve yazılmadı. Belgeye açık madde olarak eklendi.

### 2026-08-13 — Faz 3 (1): yedekleme tüm kiracıları kapsıyor + tatbikat koşturuldu

Faz 2b'nin bıraktığı **üretim öncesi kritik açık** kapandı: yedekleme script'leri
tek bir `-Database IKProDb` alıyordu ve o katalog artık boş — yani alınan
yedekler gerçek müşteri verisini içermiyordu.

**`backup-restore-drill.ps1` çoklu veritabanına çevrildi.** Yedeklenecekler
**platform veritabanından keşfediliyor**: `IKProPlatform` + `IKProDb` + her
`Tenants.DatabaseName`. `-Database` artık zorunlu değil; verilirse yalnız o
katalog tatbik edilir (hedefli tatbikat).

- **Keşif kaynağı neden platform tablosu, `sys.databases` deseni değil:** desen,
  silinmiş bir müşteriden kalan artık katalogu da yedekler ve gerçek müşteri
  listesiyle hiç doğrulanmaz. Tersi durum — platformda yazılı olup sunucuda
  bulunmayan katalog — tatbikatı **durdurur**; sessizce atlamak felaket anında
  "yedeği hiç yokmuş" sürprizine dönüşürdü.
- Bir veritabanının tatbikatı patlarsa diğerleri denenmeye devam eder, ama çıkış
  kodu 1 olur. Kısmi başarı başarı değildir: yedeklenmemiş tek müşteri bile
  telafi edilemez.
- `register-backup-task.ps1` de artık ad almadan kaydediliyor (varsayılan: hepsi).

**İKİ TATBİKAT DA GERÇEKTEN KOŞTURULDU** — bu belgenin kendi ilkesi gereği
("test edilmemiş yedek, olmayan yedektir"):

1. **Otomatik tatbikat:** 4 veritabanı (platform 5 tablo/22 satır, paylaşılan
   36/18, demo 36/146, globex 36/42) yedeklendi, ayrı ada geri yüklendi, satır
   sayıları doğrulandı, kopyalar düşürüldü. Çıkış kodu 0.
2. **Nokta-zaman geri yükleme (`RESTORE … STOPAT`)** — belgede vaat edilen ama
   hiç sınanmamış olan iddia. `IKPro_demo`'ya işaret satırları eklenip kesme
   noktası belirlendi; geri yüklenen kopyada kesme noktasından ÖNCEKİ kayıt var,
   SONRAKİ yok. Saniye hassasiyetinde geri dönüş çalışıyor.

Tatbikat kayıtları `docs/yedekleme-ve-kurtarma.md` içinde tablolarıyla duruyor;
bir sonraki tatbikatta yapılması gerekenler de orada listeli (gerçek boyutta
veri, off-site kopya doğrulaması, `freeze → geri yükle → rebuild-directory →
unfreeze` zincirinin uçtan uca denenmesi).

### 2026-08-13 — Faz 2b / Görev 7: migration orkestrasyonu — FAZ TAMAMLANDI

Fazın son ve en çok işletim etkisi olan kararı: **kiracı veritabanları açılışta
migrate EDİLMEZ.**

**Neden:** açılışta migrate etmek açılış süresini müşteri sayısıyla büyütür (200
müşteride uygulama dakikalarca kapalı) ve daha kötüsü, tek bir kiracının
veritabanı erişilemez olduğunda uygulama **hiç açılmaz** — bir müşterinin sorunu
bütün müşterileri offline yapar. Ayrılmanın amacı tam tersiydi.

**Bedeli:** dağıtım adımı atlanabilir. Üç yerden birden görünür kılındı ki sessiz
kalmasın:

1. **Açılış logu** — `TenantSchemaCheckService` şemaları kontrol eder (migrate
   etmez, açılışı da bloklamaz) ve geride kalanları `LogError` ile yazar.
2. **`GET /api/tenants/schema-status`** — hangi kiracı neyin gerisinde, hata
   alanı dahil. Okunamayan kiracı listeden düşmez; tam tersine görülmesi gereken
   satır odur.
3. **503** — şeması geride kalan kiracının hem istekleri hem login'i reddedilir.
   Kontrol `TenantAccessGuard` içine kondu: o metot token üretimi (login/refresh)
   ile yetkili isteğin ORTAK yolu, yani tek kontrol iki darboğazı da kapatıyor.
   Ayrı bir mekanizma kurmak birini kaçırma riski taşırdı.

**503, 403 değil:** kimlik ve yetki doğru, kiracı da kapalı değil — eksik olan
dağıtımın bir adımı. 503 "şu anda değil, birazdan" der; 403 "senin hakkın yok"
diyerek kullanıcıyı desteğe yönlendirirdi.

**Neden eski şemayla çalıştırmıyoruz:** uygulama kodu artık var olmayan sütunları
sorguluyor. En iyi ihtimalle her istek anlaşılmaz bir SQL hatasıyla düşer; daha
kötüsü kısmen çalışan bir yazma yolu veriyi tutarsız bırakır. Açıkça reddetmek,
sessizce bozmaktan iyidir.

**Yeni uçlar:** `POST /api/tenants/migrate` (bir kiracı patlarsa diğerleri
denenmeye devam eder ama uç **500** döner — `curl -f` ile dağıtım script'i durur)
ve `GET /api/tenants/schema-status`.

**Önbellek ömürleri asimetrik:** "güncel" 30 dk (şema kendiliğinden eskimez,
ancak dağıtımla eskir ve dağıtım süreci yeniden başlatır), "geride" 30 sn (biri
veritabanını elle migrate ederse kimse haber vermez; kiracı yarım saat boyunca
503'te kalmasın). Koşucu bitince zaten açıkça düşürülüyor.

**Dokümantasyon:** `yedekleme-ve-kurtarma.md`'ye **gerçek tek-kiracı geri yükleme
prosedürü** yazıldı (kuyruk log → FULL → DIFF → `RESTORE … STOPAT` → şema
kontrolü → **dizin yeniden kurma** → çöz). Dizin adımı zorunlu: yönlendirme
dizini platformdadır ve geri yüklemeyle geri sarılmaz, atlanırsa kullanıcılar
giriş yapamaz. `07-veritabani-ve-migrationlar.md` artık "iki context, N+2
veritabanı" gerçeğini ve üç adımlı dağıtım sırasını anlatıyor.

**Doğrulama:** 66 birim + 181 entegrasyon yeşil, `-warnaserror` temiz.

### 2026-08-13 — Faz 2b / Görev 6: purge `DROP DATABASE`, abonelikler platformda

**Purge artık veritabanını düşürüyor.** `PurgeAsync` sırası: dosya alanı →
`DROP DATABASE` → platformdaki izler (refresh token, abonelik, dizin, kiracı
satırı). `TenantScopedTablesInDeleteOrder` ve `QualifiedTableName` silindi;
onlarla birlikte `#pragma warning disable EF1002` de gitti.

- **Neden daha güvenli:** eski yol silinecek tabloları EF metadata'sından
  türetiyordu; o listeye girmeyen her şey (view olmayan yeni bir tablo,
  Identity'nin yan tabloları) sessizce geride kalırdı. `DROP DATABASE` hiçbir
  şey kaçırmaz.
- **Dosyalar artık ÖNCE siliniyor.** Eski gerekçe ("transaction geri alınırsa
  dosyalar boşuna gitmesin") `DROP DATABASE` geri alınamadığı için düştü. Yeni
  ölçüt hangi artığın daha kötü olduğu: veritabanı gidip dosyalar kalırsa diskte
  PII sürer (KVKK riski); tersi durumda kiracı `Purging`'de erişilemez durur ve
  operatör yeniden çalıştırınca tamamlanır.
- `DatabaseName` boşsa `InvalidOperationException` — sessizce eski yola düşmek yok.
- **Planın değinmediği düzeltme:** `PurgeUnverifiedAsync`, kataloğu HİÇ oluşmamış
  adayları da "okuyamadım" diye atlıyordu; oysa onlar tam da temizlenmesi gereken
  enkaz. Artık önce `ExistsAsync` soruluyor: katalog yoksa kaybedilecek veri de
  yok, silinir; katalog VAR ama okunamıyorsa atlanır.
- Yeni testler: katalog gerçekten düşüyor mu, purge sonrası aynı slug yeniden
  kullanılabiliyor mu (provizyon var olan katalogu reddettiği için bu, boş kabuk
  bırakılmadığının sert kanıtı).

**`Subscriptions` platform veritabanına taşındı.** İki migration + `TenantId`
üzerinde **tekil** indeks (kod birden çok yerde "kiracı başına tek kayıt"
varsayıyor; ikinci satır sessizce yanlış planı gösterirdi).

- **Neden:** "kim hangi pakette, kimin yenilemesi geldi" kiracı-üstü bir sorudur;
  kiracı veritabanlarında kalsaydı her fatura raporu bütün katalogları gezerdi.
  Ayrıca bir müşteriyi yedekten geri yüklemek **aboneliği geri sarmamalı** — plan
  ve ödeme geçmişi, İK verisinden bağımsız bir ticari gerçek.
- **Bedeli ve karşılığı:** platform context'inde global kiracı filtresi YOKTUR;
  kiracı veritabanında yapısal olarak imkânsız olan "filtreyi unutma" hatası
  burada mümkün. `GetSettingsQueryHandler` kiracıyı açıkça filtreliyor ve
  `AbonelikPlatformdaTests` bunu iki kiracıyla sabitliyor — filtre kalksaydı
  ikisi de aynı planı görürdü.
- Purge aboneliği de siliyor. Bu, "geri yükleme aboneliği geri sarmaz" ilkesiyle
  çelişmez: orada kiracı yaşamaya devam eder, burada tamamen yok edilir.

**Test altyapısı:** `IKProApiFactory` artık koşum SONUNDA da süpürüyor — test
host'unun açılışta kurduğu demo kiracıları koleksiyon boyunca yaşamak zorunda ve
ancak host kapanınca silinebiliyor. Koşu sonrası sunucuda tek bir test kataloğu
kalmadığı elle doğrulandı. Ayrıca `ProvisionRawAsync` cevabı okuduktan sonra
gövdenin tazesini geri koyuyor; yoksa izleme eklemek ilgisiz testleri bozuyordu.

**Doğrulama:** 66 birim + 177 entegrasyon yeşil, `-warnaserror` temiz.

### 2026-08-13 — Ayrılma sonrası temizlik + davet yeniden gönderme

Görev 0–5 boyunca birikmiş geçmişe dönük eksikler kapatıldı, ölü kod silindi ve
Görev 4'te bilinçli olarak açık bırakılan boşluk dolduruldu.

**Sessiz bir veri kaybı hatası bulundu ve düzeltildi.**
`TenantPurger.PurgeUnverifiedAsync`, "şifre belirlemiş kullanıcısı olan kiracıyı
silme" korumasını AMBIENT bağlantı üzerinden yapıyordu. Ayrılmadan sonra o
bağlantı **boş** paylaşılan veritabanına bakıyor → "hiç kimsenin şifresi yok"
diyor → koruma sessizce kalkıyordu. Yani `cleanup-unverified`, tam da korunması
gereken kiracıları silerdi. Kontrol artık her kiracının KENDİ kataloğunda
yapılıyor; kullanıcıları okunamayan aday (katalog hiç oluşmamış olabilir)
silinmiyor, loglanıp atlanıyor — emin olmadan veri silmiyoruz.
Regresyon testi: `CleanupUnverified_SifreBelirlemisKiraciyiKorur`. Bu koruma
daha önce HİÇ test edilmemişti; mevcut test aday bile olmayan (Active) bir
kiracıyla çalışıyordu.

**`TenantPurger` artık ambient `AppDbContext` almıyor.** Son kullanıcısı da
gidince constructor'dan kaldırıldı — yanlış kullanım artık derleme seviyesinde
imkânsız.

**Yeni: `POST /api/tenants/{id}/resend-invite`** (platform anahtarlı). Görev
4'ün bilinen açığıydı: provizyonun 5. adımı e-posta göndermektir ve o adım
başarısız olduğunda geriye kullanıcısı oluşmuş ama girişi imkânsız bir kiracı
kalıyordu; operatörün tek çaresi kiracıyı silip baştan kurmaktı — bir e-posta
gitmedi diye veri silmek. Şifresi olan hesaba gönderilmez (409): davet hesabı
ilk kez kurmak içindir, etkin hesabın parolasını operatöre sıfırlatmak ayrı ve
bilinçli tasarlanması gereken bir akıştır. 4 test.

**Silinenler:** `AppDbContextInitializer.SeedRolesAsync` (roller artık
`ITenantSchemaInitializer`'ın işi — aynı kural iki yerde durmasın) ve onunla
birlikte `RoleManager` bağımlılığı; üç test sınıfında tekrarlanan
`PlatformClient()` kopyaları (base'e taşındı).

**Güncellenen dokümanlar** (hepsi ayrılmadan sonra yanlış kalmıştı):
`kvkk-veri-izolasyonu.md` izolasyon modeli, `yedekleme-ve-kurtarma.md` katalog
listesi + üretim öncesi açıklar, `06-multi-tenancy.md` izolasyon tablosu +
operatör araçları, `05-kimlik-ve-yetkilendirme.md`, `07-veritabani`,
`08-testler.md` (koşum süresi ve test öneki), `backend/README.md`. Ayrıca
kodda kalan "Faz 1b/Faz 2'de olacak" yorumları bugünkü gerçeğe çevrildi.

`frontend/src/api/schema.d.ts` **gerçekten yeniden üretildi** (elle değil):
yalnız 84 satır eklendi, hiç silinmedi — Görev 0'daki elle düzenlemenin doğru
olduğunu da doğruluyor.

**Doğrulama:** 66 birim + 172 entegrasyon yeşil, `-warnaserror` temiz; frontend
`tsc` + `oxlint` temiz.

### 2026-08-13 — Faz 2b / Görev 5: ayrılma tamamlandı

Gerçek müşteri verisi olmadığı için **taşıma script'i yazılmadı**: tüm
veritabanları düşürüldü, uygulama açıldı, demo kiracılar Görev 4'teki gerçek
provizyon yolundan yeniden kuruldu.

- **Seed artık kestirme kullanmıyor.** `DemoTenantSeeder` demo kiracıları
  `TenantOnboarding` üzerinden oluşturuyor; `AppDbContextInitializer` yalnız
  kiracının İÇİNİ dolduruyor. Böylece kayıt akışı her geliştirme kurulumunda
  baştan sona çalıştırılmış oluyor — provizyonu bozan bir değişiklik yalnız
  testlerde değil, uygulamayı açan herkesin karşısında patlıyor.
- **Demo admin'leri provizyondan geliyor** (şifresiz + davet); seed yalnız
  şifrelerini veriyor (`DemoSifresiVerAsync`), hesabı yeniden yaratmıyor.
- **Paylaşılan `IKProDb` duruyor ama boş.** Kiracı bağlamı olmayan her şey
  (sağlık kontrolü, henüz ayrılmamış kiracılar) oraya bağlanıyor; şeması
  açılışta migrate ediliyor, içinde veri yok.
- `PlatformDbInitializer.EnsureDefaultTenantAsync` kaldırıldı — varsayılan
  kiracı artık provizyon yolundan doğduğu için ölü koda dönüşmüştü.

**Planın öngörmediği bir tuzak çıktı ve kapatıldı:** demo kiracıların slug'ları
sabit olduğu için test koşumu da `IKPro_demo` üretiyordu — yani `dotnet test`
her çalıştığında geliştiricinin demo ortamını silecekti. Katalog adı üretimi
statik yardımcıdan **`ITenantDatabaseNamer`** servisine dönüştü; önek
`Tenancy:DatabasePrefix` ile geliyor ve testler `IKPro_test_` kullanıyor.

**Kırılan test:** `AuthFlowTests` — demo admin'in baş harfleri `İK` yerine `İY`.
Sebep: baş harfler artık üretim kuralından (`DeriveInitials`) geliyor, çünkü
hesabı elle yazılmış bir seed satırı değil gerçek provizyon oluşturuyor.
Beklenti güncellendi; bu, seed'in ürün kuralını atlamayı bıraktığının kanıtı.

**Elle doğrulama (tüm `IKPro*` düşürülüp uygulama sıfırdan açıldı):**
- `IKPro_demo` + `IKPro_globex` oluştu, ikisi de `Turkish_CI_AS`.
- Kullanıcı/personel: demo 3/4, globex 3/2, **paylaşılan `IKProDb` 0/0**.
- İki kiracıyla giriş yapıldı, her biri yalnız kendi personelini gördü.
- Globex'te departman oluşturuldu → satır fiziksel olarak `IKPro_globex`'te;
  `IKPro_demo` ve `IKProDb`'de yok.

**Doğrulama:** 66 birim + 167 entegrasyon yeşil, `-warnaserror` temiz.

### 2026-08-13 — Faz 2b / Görev 4: provizyon gerçek veritabanı kuruyor

**Bu, fazın dönüm noktası.** `POST /api/tenants` artık altı adım işletiyor:
(1) platform tek transaction'da kiracı satırı + e-posta rezervasyonu,
(2) `CREATE DATABASE`, (3) migration'lar, (4) roller, (5) admin kullanıcı +
davet, (6) `Status = Active`. Yarıda kalırsa kiracı `Provisioning`'de kalır —
erişilemez ama görünür (`GET /api/tenants/stuck`).

- **Katalog adı:** `IKPro_{slug}`, slug'daki tireler alt çizgiye çevrilerek.
  Çakışma üretmez: slug deseni alt çizgiye izin vermediği için iki farklı slug
  aynı ada indirgenemez.
- **Var olan katalog REDDEDİLİR.** Yarıda kalmış bir kurulumdan artakalan
  veritabanını sessizce yeniden kullanmak, iki müşterinin verisinin aynı yerde
  toplanması demek olurdu — bu ürünün yapabileceği en pahalı hata.
- **Yeni soyutlama `ITenantSchemaInitializer`** (migration + rol tohumlaması).
  Roller şema hazırlığının parçası, isteğe bağlı bir seed adımı değil: boş bir
  kiracı veritabanında `AspNetRoles` boştur ve `AddToRoleAsync` SESSİZCE düşer —
  kullanıcı oluşur, giriş yapar, hiçbir şey göremez. Ayrı bir test bunu sabitliyor.
- `TenantOnboarding` statik yardımcıdan **servise** dönüştü (7 bağımlılık).
- `IPlatformDbContext.BeginTransactionAsync` eklendi: rezervasyon kiracının
  veritabanı-üretimli kimliğini bilmek zorunda olduğundan iki `SaveChanges`
  kaçınılmaz; transaction olmasaydı rezervasyon çakışınca geriye slug'ı işgal
  eden sahipsiz bir kiracı satırı kalırdı.

**Test altyapısı — kullanıcı kararı: agresif temizlik (A seçeneği).**
`TenancyTestBase` artık `IAsyncLifetime`; her testin açtığı katalog test bitince
düşürülüyor. Fabrika koşum başında `IKPro_test_*` artıklarını da süpürüyor (koşum
ortasında çöken süreç için; olmasaydı tek çökme sonraki tüm koşumları kırardı).
Test slug'ları `test-` önekli üretiliyor ki süpürme deseni gerçek bir kiracıyı
asla silemesin.

**Süre: entegrasyon takımı 33 sn → 93 sn** (167 test, ~2,8 kat). Bilinçli ödenen
maliyet. Koşum sonrası sunucuda artık katalog kalmadığı elle doğrulandı.

**Kırılan iki test, ikisi de bilgi taşıyordu:**
- `KiraciKutuguTests` — `DatabaseName` artık dolu; güncellendi.
- `TenantFileIsolationTests` — "yabancı ID → 404" kurgusu artık **kurulamıyor**:
  her katalog kendi kimlik dizisini saydığından 1 numaralı personel her kiracıda
  var. Gerileme değil, izolasyonun güçlenmesi — erişim artık sorgu filtresine
  değil fiziksel ayrılığa dayanıyor. Test asıl güvenceyi ölçecek şekilde
  yeniden yazıldı.

**Bilinen açık:** davet e-postası hâlâ `Status = Active`'den önce gidiyor. Planın
sırası da böyleydi ve bu sıra bugünkü riski azaltmıyor; e-postayı Active'den
sonraya almak ise daha kötü bir arıza üretirdi (Active kiracı + davetsiz admin +
slug çakışması yüzünden yeniden denenemeyen provizyon). Doğru çözüm bir operatör
ucu: **"daveti yeniden gönder"**. Görev 7'ye ya da ayrı bir işe bırakıldı.

**Doğrulama:** 66 birim + 167 entegrasyon yeşil, `-warnaserror` temiz.

### 2026-08-12 — Faz 2b / Görev 2+3: katalog tesisatı (davranış değişmedi)

İkisi de bilinçli olarak "ölü" görevler: üretilen yetenekleri henüz kimse
çağırmıyor, tüm `DatabaseName` alanları boş, dolayısıyla uygulama davranışı
hiç değişmedi. Amaç, Görev 4'te bir hata çıktığında "tesisat mı, provizyon akışı
mı" sorusunu ortadan kaldırmak.

**Görev 2 — `DatabaseName` + veritabanı sağlayıcı**

- `Tenants.DatabaseName` (`nvarchar(128)`, **nullable**) + platform migration'ı
  `TenantDatabaseName`. Boş olması hata değil, geçişin tasarımı: boş ad
  "paylaşılan veritabanında" demek, böylece ayrılma kiracı kiracı yapılabiliyor.
  **Tekil indeks YOK** — SQL Server'da unique index NULL'ları tek değer sayar,
  kiracıların çoğu boşken ikinci kiracı eklenemezdi.
- `ITenantDatabaseProvisioner` + `SqlServerTenantDatabaseProvisioner`:
  `ExistsAsync` / `CreateAsync` / `DropAsync`. Bağlantı, paylaşılan dizeden
  katalog `master` yapılarak türetilir — kiracı DB'leri aynı sunucuda doğduğu
  için ayrı bir bağlantı ayarı iki yerde tutulan ve zamanla ayrışan bir gerçek
  yaratırdı.
- **Ad doğrulaması tek savunmadır:** `CREATE/DROP DATABASE` parametre kabul
  etmez, ad SQL metnine gömülmek zorunda; ad ise slug'dan türetiliyor ve slug
  istemciden geliyor. Yalnız `[A-Za-z0-9_]`, 1–128 karakter kabul ediliyor ve
  üç metot da aynı kapıdan geçiyor (testi bunu ayrı ayrı doğruluyor).
- Harmanlama **açıkça** veriliyor: `COLLATE Turkish_CI_AS`. Sunucu varsayılanına
  bırakılsaydı yerel (Turkish) ile CI (Latin1) arasında sıralama ve eşitlik
  davranışı ayrışırdı.
- **Planla sapma:** plan `SqlConnection.ClearAllPools()` diyordu; yalnız hedef
  kataloğun havuzu temizleniyor (`ClearPool`). `ClearAllPools` süreçteki her
  veritabanının havuzunu boşaltır — üretimde bir kiracının silinmesi, onunla
  hiç ilgisi olmayan bütün isteklere bağlantı yeniden kurma maliyeti bindirirdi.

**Görev 3 — çözücü katalog üretir**

- `TenantConnectionResolver`: kiracı yok → paylaşılan; katalog adı boş → 
  paylaşılan; dolu → aynı sunucu, o katalog. Katalog dışında hiçbir ayar
  değişmiyor (testle sabitlendi).
- **Planın değinmediği zorunluluk:** `ResolveFor` senkron ve öyle kalmak zorunda —
  çağrıldığı yer `AddDbContext` yapılandırma geri çağrısı, async'e çevrilemez.
  Kütüğün API'si ise `Task` döndürüyordu. Sync-over-async yerine kütüğe senkron
  `GetDatabaseName(int)` eklendi; önbellek ıskası gerçek senkron sorguyla
  doldurulur (kiracı başına ~5 dakikada bir; sıcak yolda veritabanına gidilmez).
- Durum ve katalog adı **tek önbellek kaydında**: ayrı kayıtlar biri düşüp
  diğeri kalınca kütüğün kendiyle çelişmesine yol açardı. "Kiracı yok" cevabı da
  önbellekleniyor — aksi halde geçersiz kimlikli istek her seferinde veritabanına
  giderdi (ucuz bir DoS yüzeyi).

**Doğrulama:** 66 birim + 162 entegrasyon yeşil, `-warnaserror` temiz.

**Not — bir kez görülen, tekrarlanamayan kırılma:** Görev 2 sonrası tam takım bir
kez toplu çöktü (160/160, fixture seviyesinde, 515 ms). Aynı komut hemen sonra ve
o günden beri hep yeşil; kök neden kanıtlanamadı. Şüpheli `ClearAllPools`'un
süreç genelinde havuzları boşaltmasıydı ve o kod zaten yukarıdaki gerekçeyle
daraltıldı. Tekrarlarsa ilk bakılacak yer burasıdır.

### 2026-08-12 — Faz 2b / Görev 1: devralınan üç mayın temizlendi

Önceki fazların bilinçli olarak 2b'ye bıraktığı üç madde; hepsi ayrılma anında
gerçek soruna dönüşüyordu, ayrılmadan **önce** kapatıldı ki bir hata çıktığında
sebebi ayrılmaya bağlanmasın. Üçü de önce kırmızı test, sonra düzeltme.

- **(a) `EmailExistsAsync` sapmış dizinde patlıyordu.** Dizin türetilmiştir ve
  karşılığı olmayan bir kiracıya işaret edebilir; ayrılmadan sonra o kimlikle
  kapsam açmak fırlatır — oysa metot `bool` sözleşmesinde ve provizyon/işe
  alım/davet yollarının ön kontrolü. Çözüm **try/catch değil**: kapsam açmadan
  önce kütüğe soruluyor (`ITenantRegistry.GetStatusAsync`), null ise kiracı
  yoktur → `false` + uyarı logu. `catch` kullanmak altyapı arızasını da yutup
  "kullanıcı yok" diye raporlardı; bu ayrım korundu.
- **(b) Davet kabulü kiracıyı etkinleştiriyordu.** Self-servis kaydın e-posta
  doğrulama kapısıydı; o akış kalkınca geriye yalnız zararı kaldı —
  `Provisioning`'de takılı bir kiracı "veritabanı henüz kurulmadı" demek ve onu
  bir son kullanıcının şifre belirlemesiyle açmak, kullanıcıyı var olmayan bir
  kataloğa sokar. Etkinleştirme tamamen kaldırıldı. Kural: **durum geçişleri
  provizyon yolunun işidir.**
- **(c) Seed kök kapsamdan çözülüyordu.** `AppDbContextInitializer` içeride
  `Impersonate` çağırıyordu ama `AppDbContext`'in bağlantısı sınıf DI tarafından
  kurulurken çoktan belirleniyordu — yani ayrılmadan sonra seed **sessizce yanlış
  veritabanına** yazardı. Varsayılan kiracıyı garanti etme işi
  `PlatformDbInitializer.EnsureDefaultTenantAsync()`'e taşındı; `Program.cs`
  initializer'ı `ITenantScopeFactory.Create(...)` kapsamından çözüyor. `SeedAsync`
  artık ilk satırında `TenantIdOrThrow()` ile **tel tuzağı** kuruyor.
- **(d) `TenantOnboarding` sıra kopyası:** Görev 0 ile kapanmıştı, doğrulandı —
  tek çağıran `ProvisionTenantCommand`.
- **Yeni testler:** `EmailExistsAsyncTests` (birim; kapsam fabrikası çözülemeyen
  kiracıda fırlatıyor, yani metot fabrikaya giderse kırılır — değeri fazdan
  bağımsız), `KiraciDurumMakinesiTests`, `SeedKapsamiTests`.
- **Doğrulama:** 52 birim + 156 entegrasyon yeşil, `-warnaserror` temiz.

### 2026-08-12 — Faz 2b / Görev 0: self-servis kayıt tamamen kaldırıldı

- **Neden:** Kiracı başına ayrı veritabanına geçiyoruz. O geçişten sonra
  "kaydol" bir satır değil bir **veritabanı** oluşturmak demek. Anonim bir uç
  bunu tetikleyebilseydi internetten herhangi biri bir script'le SQL Server'a
  istediği kadar veritabanı kurdurabilir; disk, yedekleme penceresi ve lisans
  maliyeti saldırganın eline geçerdi. Rate limit bunu yavaşlatır, çözmez —
  maliyet kalıcı, istek anlıktır. Veritabanı oluşturmak otomatik bir yan etki
  değil, operatörün bilinçli eylemi olmalı.
- **Kaldırılanlar:** `POST /api/tenants/signup` aksiyonu, `RegisterTenantCommand`
  (+ validator/handler ve kendi slug-retry döngüsü), `SelfServiceSignupTests`,
  `"signup"` rate-limit politikası ve `RateLimiting:SignupPermitPerHour` ayarı,
  frontend `CompanySignupPage` (+ testi) ve `/register-company` rotası, giriş
  sayfasındaki "Şirketinizi kaydedin" bağlantısı. `/signup` ve
  `/register-company` eski yer imleri için `/login`'e yönlendiriyor.
- **Kalan tek onboarding yolu:** `POST /api/tenants` (`X-Platform-Key`) → kiracı
  **Active** başlar, ilk hr-admin şifresiz oluşur ve davet e-postasıyla
  `/accept-invite`'a gider. `TenantOnboarding.CreateWithAdminAsync`'in tek
  çağıranı artık `ProvisionTenantCommand`.
- **Yan etki — temizlik servisinin anlamı değişti:** `cleanup-unverified` artık
  "doğrulanmamış self-servis kayıtları" değil, **provizyon ortasında kesilmiş,
  `Provisioning`'de takılı kalmış** kiracıları topluyor. `ScheduledCleanupTests`
  ve `TenantPurgeTests` kurulumları buna göre yeniden yazıldı (signup çağırmak
  yerine provizyonlayıp durumu `Provisioning`'e alıyorlar).
- **Regresyon testi:** `SelfServisKayitKaldirildiTests` — istek başarısız olmalı
  ve **arkasında kiracı satırı oluşmamalı**. Durum kodu bilerek sabitlenmedi:
  ASP.NET bu yola 404 değil **405** döndürüyor; kodu teste çakmak sözleşmemizi
  değil yönlendiriciyi test etmek olurdu.
- **Doğrulama:** backend 51 birim + 153 entegrasyon yeşil, `-warnaserror` build
  temiz; frontend `tsc --noEmit` temiz, 146 test (44 dosya) yeşil.

### 2026-07-20 — Geliştirici rehberleri (docs/rehberler/)

- Yeni başlayan (junior dahil) bir geliştiricinin projeye A–Z hakim olması için
  12 dosyalık rehber seti yazıldı: `docs/rehberler/` (README indeks + 00–10).
  Konular: genel bakış, ortam kurulumu, Clean Architecture, backend derinlemesine,
  frontend derinlemesine, kimlik & yetkilendirme, multi-tenancy, veritabanı &
  migration, testler, **adım adım yeni özellik ekleme** (uçtan uca örnek), sözlük & SSS.
- Gerçek kod dosyalarına referans verir, "neden böyle?" sorusunu yanıtlar; mermaid
  diyagramları (katmanlar, istek yolculuğu, davet akışı, TDD döngüsü) içerir.
  Kök README dokümantasyon bölümü bu seti işaret eder.

### 2026-07-20 — Kod-inceleme fix'leri (#2 host çökmesi, #3 slug yarışı, #4 purge dosya artığı)

- **Bağlam:** `/senior-backend` + `/code-review` ile projenin kod kalitesi ve
  satışa hazırlığı analiz edildi; 5 bulgu + üretim-hazırlık boşlukları raporlandı.
  Kullanıcı kararıyla çökme/veri-bütünlüğü olan #2/#3/#4 düzeltildi (CORS/CI/prod
  migration gibi ops maddeleri ayrı bırakıldı).
- **#2 — `IntervalHours ≤ 0` host'u çökertiyordu:** `UnverifiedTenantCleanupService.ExecuteAsync`'te
  `PeriodicTimer` ctor'u try/catch dışındaydı; `Enabled=true`+`IntervalHours=0` →
  ctor fırlatır → BackgroundService varsayılanı `StopHost` → API hiç başlamaz.
  Guard eklendi (hata logla, dön). Test: `IHostedService.StartAsync` fırlatmıyor.
- **#3 — slug kontrol-sonra-ekle yarışı → 500:** `RegisterTenantCommandHandler`
  slug'ı `AnyAsync` ile kontrol edip ekliyordu; eşzamanlı iki kayıt aynı slug'ı
  hesaplayıp ikincisi unique index'i ihlal eder → yakalanmamış `DbUpdateException`.
  Kiracı yazımı sınırlı (5) retry döngüsüne alındı; `DbUpdateException`'da
  `context.Tenants.Remove` (Added→Detached) + yeniden denemede rastgele ekli slug.
  Register artık `TenantOnboarding` yerine kendi retry'li akışını kullanır
  (provizyon helper'ı değişmedi). Test: aynı ad + 4 eşzamanlı signup → hepsi 201,
  slug'lar benzersiz (3× tekrar koşuldu, kararlı).
- **#4 — purge fiziksel dosya artığı (KVKK):** `TenantPurger` DB commit sonrası
  dosyaları döngüde siliyordu; biri fırlarsa (kilit/kaçış-yolu) döngü kalanları
  silmeden çıkıp metod fırlıyordu (DB silinmiş ama diskte PII kalır, çağırana 500).
  `ILogger` inject edildi; her dosya ayrı try/catch + loud log, sonda başarısız
  sayısı uyarı. Testler: gerçek dosyalı purge dosyayı siler; kök-dışı kaçış yollu
  dosya purge'ü yarıda kesmez, kiracı yine de silinir.
- **Doğrulama:** 19 birim + 95 entegrasyon (+4) yeşil.

### 2026-07-20 — Doğrulanmamış kiracı zamanlanmış temizlik

- **`UnverifiedTenantCleanupService`** (`BackgroundService`, API katmanı):
  doğrulanmamış (hiç etkinleşmemiş) eski self-servis kiracıların uygulama-içi
  periyodik temizliği — dış cron/scheduler gerektirmez. `IServiceScopeFactory`
  ile scope açıp mevcut `CleanupUnverifiedTenantsCommand`'i `ISender` üzerinden
  çağırır (silme mantığı tekrarlanmadı, DRY). `ExecuteAsync` `PeriodicTimer` ile
  döngüler; bir geçiş hatası döngüyü öldürmez (loglanıp sonraki tetiklemede
  yeniden denenir).
- **Test edilebilirlik:** tek geçiş `RunOnceAsync` metoduna ayrıldı → entegrasyon
  testi timer'a bağımlı olmadan deterministik (eski doğrulanmamış kiracı silinir,
  aktif kiracı korunur).
- **Yapılandırma:** `Cleanup:UnverifiedTenants` (`Enabled`/`IntervalHours`/
  `OlderThanDays`). Yıkıcı işlem olduğu için **varsayılan `Enabled=false`** —
  dev/test'te sessiz (mevcut testleri etkilemez), üretimde bilinçli açılır.
  `appsettings.json`'a açıklamalı örnek blok eklendi.
- **Doğrulama:** 1 yeni entegrasyon testi; 19 birim + 91 entegrasyon yeşil.
- KVKK dokümanı Bölüm 7.1 güncellendi ("cron ile tetiklenebilir" → "uygulama-içi
  zamanlayıcı mevcut").

### 2026-07-20 — İlgili kişi verisi dışa aktarımı (KVKK taşınabilirlik)

- **`GET /api/me/data-export`** (`GetMyDataExportQuery`): oturum açmış herhangi
  bir kullanıcı (rolden bağımsız) kendi verisini tek istekle indirir. Paket:
  hesap bilgisi (id/ad/e-posta/roller), varsa bağlı personel kaydı + profil
  (unvan/departman/işe giriş + doğum tarihi/cinsiyet/medeni durum/telefon/
  kişisel e-posta/adres/IBAN/banka), izin talepleri + bakiyeleri, puantaj
  kayıtları, uyum belgeleri (metadata), bordro pusulası listesi (metadata).
  JSON, girintili, `ikpro-verilerim-{tarih}.json` adıyla indirilir (mevcut
  bordro pusulası indirme deseniyle aynı `(byte[], fileName)` → `File(...)`).
- **İzolasyon çift güvence:** tüm alt sorgular `EmployeeId == currentUser.EmployeeId`
  ile daraltılır; ayrıca EF tenant global filtresi devrede. `EmployeeId`'si
  olmayan kullanıcı (ör. bağlı personeli olmayan hr-admin — seed'de
  `ik@hrmaster.local`) crash almadan `employee: null` + boş listelerle export alır.
- **Yan değişiklik:** `ICurrentUser`'a `Email` eklendi (JWT `email` claim'inden
  okunur — daha önce yalnız `UserName`, kullanıcının görünen adını taşıyordu,
  export'ta e-posta alanı için ayrıca gerekliydi). Tek implementasyon
  (`CurrentUser`) olduğundan güvenle genişletildi.
- **Frontend:** üst menüde "Verilerimi indir" ikonu (logout'un yanında, tüm
  roller için görünür), `apiDownload("/me/data-export")` ile tarayıcı indirmesi
  tetikler.
- **Doğrulama:** 2 yeni entegrasyon testi (kendi verisi döner + başka çalışanın
  adı asla pakette yok; `EmployeeId`'siz kullanıcı crash almaz) — 19 birim + 90
  entegrasyon (backend), 136 birim (frontend), her iki build yeşil.
- KVKK dokümanı Bölüm 6 (İlgili kişi hakları satırı) ve Bölüm 7.2 "mevcut"
  olarak güncellendi.

### 2026-07-20 — SMTP gerçek e-posta göndericisi

- **`MailKitSmtpEmailSender`** (`IEmailSender` implementasyonu, MailKit): MimeMessage
  kurup `SmtpClient` ile bağlanır (StartTls veya SslOnConnect — `Smtp:UseStartTls`),
  gerekiyorsa authenticate olur, gönderir. `IEmailSender` soyutlaması hiç değişmedi —
  davet akışı (T5.2) kodunda tek satır değişiklik yok.
- **Seçim mekanizması** (`DependencyInjection.cs`): `Email:Mode` config değeri
  okunur. Varsayılan (yapılandırılmamış veya `outbox`) → mevcut `FileOutboxEmailSender`
  (dev/demo/**test davranışı birebir korunur** — davet testleri hâlâ outbox
  dosyasından `DAVET-KODU:` okuyor). `smtp` → `SmtpOptions` bağlanır ve
  `.ValidateOnStart()` ile **fail-fast**: `Smtp:Host` veya `Smtp:From` boşsa
  uygulama başlangıçta patlar (JWT sır kontrolüyle aynı "üretimde yanlış
  yapılandırma sessizce geçmesin" ilkesi).
- **Güvenlik notu:** MailKit ilk denemede 4.8.0 ile geldi; `dotnet restore`
  NU1902 uyarısıyla CVE-2026-41319'u (StartTls sırasında SASL mekanizması
  indirgeme) yakaladı. GitHub advisory'sinden yamalı sürüm (4.16.0) doğrulanıp
  yükseltildi; restore artık temiz.
- **Yapılandırma:** `appsettings.json`'a örnek `Email`/`Smtp` bloğu eklendi
  (şifre alanı boş bırakıldı — `Smtp__Password` env'den gelmeli, commit'e
  yazılmadı).
- **Doğrulama:** 19 birim (+4 `SmtpOptionsTests`: Host/From eksik → exception,
  tam yapılandırma → sorunsuz, varsayılan Port=587/StartTls=true) + 88
  entegrasyon (+1 `EmailSenderSelectionTests`: varsayılan modda DI'nin
  `FileOutboxEmailSender` çözdüğünü doğrular) yeşil.
- KVKK dokümanı Bölüm 4 ve 7.4 güncellendi ("prod'da SMTP" → "mevcut, MailKit").

### 2026-07-20 — Kiracı purge & doğrulanmamış kiracı temizliği

- **`ITenantPurger`** (Infrastructure `TenantPurger`): bir kiracının tüm verisini
  siler. Silinecek tablolar EF model metadata'sından türetilir (`ITenantScoped` +
  PK'lı = keyless view'ler hariç) ve **FK bağımlılığına göre çocuk→ebeveyn** sırada
  (metadata topo-sort) `DELETE FROM [tablo] WHERE TenantId=@id` çalıştırılır; ardından
  refresh token → kullanıcı (UserRoles cascade) → kiracı satırı; hepsi tek transaction.
  Fiziksel evrak dosyaları (yalnız `EmployeeDocument.FilePath`) commit sonrası silinir.
  Tablo adları metadata'dan çözülür (Identity tabloları `Users`/`UserRoles`… olarak
  yeniden adlandırılmış). Yeni kiracı-kapsamlı tablo otomatik kapsanır.
- **`PurgeTenantCommand`** (confirm-slug güvenlik kapısı) + **`CleanupUnverifiedTenantsCommand`**
  (pasif + eski + hiç şifre belirlememiş kiracılar; askıya alınmışları korur; mantık
  `ITenantPurger.PurgeUnverifiedAsync`'te — Clean Architecture için handler Application'da).
- **Uçlar** (platform-key korumalı, DRY `PlatformKeyValid()`): `DELETE /api/tenants/{id}?confirmSlug=`,
  `POST /api/tenants/cleanup-unverified?olderThanDays=`.
- **Doğrulama:** 5 yeni purge testi (izolasyon: A silinir B korunur; yanlış slug 409;
  anahtarsız 401; doğru silme; cleanup doğrulanmamışı siler aktifi korur). 15 birim +
  87 entegrasyon yeşil.
- **Yan bulgu (purge dışı):** takvim 2026-07-20'ye ilerleyince `LeavesTests.LeaveLifecycle`
  takım-widget assertion'ı düştü — test 07-13…07-17 sabit tarihli izni ileri-bakışlı
  14 günlük pencerede (`EndDate >= bugün`) arıyordu. Temiz main'de de düşüyordu (regresyon
  değil). Takım kısmı **bugüne göreli** onaylı izinle test edilecek şekilde düzeltildi;
  Days=4/bakiye (sabit Temmuz tatiline bağlı) assertion'ları korundu.

### 2026-07-17 — Self-servis kiracı kaydı

- **Public kayıt ucu** `POST /api/tenants/signup` (`RegisterTenantCommand`):
  platform anahtarı gerektirmez, ayrı ve daha sıkı `signup` rate-limit
  politikasıyla korunur (saatlik; kötüye kullanım önleme). Slug **sunucuda**
  şirket adından türetilir (`TenantSlug`, Türkçe transliterasyon) ve çakışırsa
  `-2/-3…` ile benzersizleştirilir.
- **E-posta doğrulama kapısı:** self-servis kiracı **pasif** (`IsActive=false`)
  oluşturulur; ilk hr-admin davet e-postasını kabul edince (`accept-invite`)
  kiracı **etkinleşir**. Doğrulanmadan giriş reddedilir. Provizyon (platform-key)
  yolu değişmedi — kiracı aktif başlar.
- **DRY:** provizyon ve self-servis ortak `TenantOnboarding.CreateWithAdminAsync`
  yardımcısını paylaşır; `ProvisionTenantCommand` bu yardımcıya taşındı
  (davranış aynı, mevcut testler yeşil).
- **Frontend:** `CompanySignupPage` (`/#/register-company`) — şirket+admin formu
  → başarı ekranı ("doğrulama e-postası gönderildi"); login sayfasına "Şirketinizi
  kaydedin" bağlantısı. `auth-alt`/`auth-link` stilleri token'larla eklendi.
- **Doğrulama:** 15 birim + 82 entegrasyon (backend, +6 slug +3 signup), 136
  birim (frontend, +2), her iki build yeşil. TDD (önce başarısız test).

### 2026-07-17 — Multi-tenancy Faz 5 / T5.4: KVKK & veri izolasyonu dokümanı

- `docs/kvkk-veri-izolasyonu.md` oluşturuldu. Uygulamaya sadık (overclaim yok):
  ortak DB + `TenantId` + reflection'lı EF global filtre modeli; katman katman
  izolasyon tablosu (varlık/view/SQL fonksiyon/audit trigger/**metadata-kapılı
  dosya**/kimlik); izolasyon testleriyle kanıt; erişim denetimi (JWT tenant
  claim + roller + platform-key); davet akışı; audit izi; **KVKK ilke eşleme
  tablosu**; bilinen boşluklar (silme/anonimleştirme otomasyonu yok — elle
  prosedür, veri dışa aktarımı yok, blob şifreleme yok, SMTP yok) + üretim
  öncesi operasyonel kontrol listesi.
- Faz 5 (dolayısıyla Faz 0–5 programı) tamam; multi-tenant dalı main'e merge +
  push edildi (`53ce480`). Kalanlar bu programın "sonraki adımları"dır.

### 2026-07-17 — Multi-tenancy Faz 5 / T5.2: Davet akışı

- **Davet/şifre-belirleme akışı** geçici `demo123` şifresinin yerini aldı.
  `IdentityService.CreateInvitedUserAsync` kullanıcıyı **şifresiz** oluşturur,
  rol atar, `GeneratePasswordResetTokenAsync` ile davet token'ı üretir ve
  `SendInviteEmailAsync` ile outbox'a (FileOutboxEmailSender) davet e-postası
  bırakır. `CreateTenantAdminAsync` (companyName parametresi eklendi) ve
  `CreateEmployeeLoginAsync` bu yardımcıyı çağırır.
- **Yeni uç:** `AcceptInviteCommand` (+validator +handler) →
  `POST /api/auth/accept-invite` (anonim, rate-limit "auth"). Token'la
  `ResetPasswordAsync` çağırıp ilk şifreyi belirler; geçersiz token →
  `ValidationException`.
- **Frontend:** `AcceptInvitePage` (`/#/accept-invite?email=&token=`) —
  şifre belirle → `accept-invite` → otomatik `login` → rol ana sayfası. Route
  `PublicOnly` altında. Eksik/geçersiz bağlantıda uyarı.
- **Kritik düzeltme:** `AddIdentityCore` zincirine `.AddDefaultTokenProviders()`
  eklendi — yoksa `GeneratePasswordResetTokenAsync` "no token provider" ile
  patlıyordu (provizyon/hire 500 dönüyordu).
- **Test altyapısı:** `IKProApiFactory.StorageRoot` dışa açıldı;
  `TenancyTestBase`e davet yardımcıları eklendi (`ReadInviteTokenAsync` outbox
  JSON'undan `DAVET-KODU:` token'ını çeker, `AcceptInviteAsync`,
  `ProvisionAndActivateAsync`). Provizyon/hire sonrası login yapan testler
  (TenantProvisioning/Isolation/View/File + Recruitment) davet→kabul→giriş
  akışına güncellendi.
- **Doğrulama:** 79 entegrasyon + 9 birim (backend), 134 birim (frontend),
  her iki build yeşil. AcceptInvitePage için 3 birim test (token eksik,
  başarılı akış, şifre uyuşmazlığı).

### 2026-07-16 — Dilim 8: Kapanış

- Eski mock frontend (`index.html`, `main.js`, `routes.js`, `components/`,
  `styles/`, `services/`) içeriği değişmeden `legacy-frontend/` altına taşındı;
  kısa README ile statik çalıştırma tarifi eklendi (`npx serve -l 4173
  legacy-frontend`). Göreli yollar sayesinde yeni konumdan aynen çalışıyor.
- Kök `README.md` yeniden yazıldı: monorepo yapısı, kurulum/çalıştırma, demo
  kullanıcılar, modül listesi, dokümantasyon işaretçileri ("üretim değil demo"
  uyarısıyla). Eski mock-dönemi README anlatımı kaldırıldı.
- Kök `.gitignore`: `backend/src/IKPro.API/App_Data/` (çalışma zamanı
  yükleme deposu) ignore edildi.
- Günlükteki "kökteki index.html" referansları `legacy-frontend/` olarak
  düzeltildi.
- Doğrulama: 128 birim test + build yeşil; duman — yeni uygulamada
  dashboard/bordro/aksiyonlar açılıyor, legacy uygulama yeni konumdan login
  ekranıyla açılıyor. Not: 4173 portunda önceki oturumdan kalan sunucu süreci
  dizin listelemesi döndürüyordu; süreç kapatılıp yeniden doğrulandı.
- **React portu bu dilimle tamamlandı (8/8).**

### 2026-07-16 — Dilim 7: Aksiyon Merkezi + Global Arama + Ayarlar + Yönetici Konsolu

- 7 görev TDD ile tamamlandı; 128 birim test + build yeşil.
- `src/features/actions/`: `format.ts` (öncelik/durum etiketleri + ileri yönlü
  `open→week→done` geçiş yardımcıları) + `queries.ts` (liste/audit/CRUD/status),
  `ActionsPage` (KPI'lar listeden, server-side öncelik/kaynak/sahip filtreleri,
  Açık/Bu Hafta/Tamamlanan sekmeleri, Denetim İzi gerçek `audit-logs` verisiyle
  yalnız hr-admin+manager; kartlarda durum ilerletme MGMT + silme hr-admin,
  "Kaynağa git" `sourceRoute`tan), `ActionModal` (Yeni Aksiyon, hr-admin);
  `/risk/action-center` aynı sayfaya bağlandı (eski routes.js paritesi).
- `layout/GlobalSearch`: 300ms debounce ile gerçek `GET /search` sonuçları
  (personel rol kapsamlı + aksiyon + aday) sayfa sonuçlarının altına eklenir;
  API hatası sessizce yutulur.
- `src/features/settings/`: `SettingsPage` — şirket profili (PUT + header
  "Değişiklikleri Kaydet"), logo yükleme (FormData) + korumalı logo
  `apiDownload`→blob URL ile gösterim (`<img src>` Bearer gönderemez),
  bildirim toggle'ları değişimde anında PUT, şifre değişikliği gerçek
  `POST /auth/change-password` (yanlış mevcut şifre → backend mesajı),
  2FA toggle, abonelik salt-okur.
- `src/features/manager/`: `ManagerPage` — "Şu An İzinli" (`overview.onLeaveToday`)
  ve "Onay Bekliyor" gerçek; Onay Bekleyenler paneli `usePendingLeaves` +
  `useDecideLeave`; trend grafiği/ısı haritası/departman tablosu ve 2 KPI
  **demo** (backend'de izin-analitik ucu yok, Demo pill'li); CSV dışa aktarım
  gerçek (`downloadCsv` uzantıyı kendisi ekler — çift `.csv.csv` düzeltildi).
- **Kararlar / bilinçli farklar:** aksiyon mutasyonları + "Yeni Aksiyon" eskide
  yoktu; Denetim İzi employee'ye kapalı (uç Management); manager'daki işlevsiz
  ay/departman seçicileri kaldırıldı; ayarlar gerçek kalıcı (eskide sahte).
- Duman: 10 kontrol geçti. Playwright notları: gizli switch checkbox'ları
  (`.switch` CSS) `force`/`evaluate(el.click())` ister; dilim 1 rota testi
  settings placeholder beklentisi gerçek sayfaya güncellendi.
- Parite: `#/actions`, `#/settings`, `#/manager` eskiyle yan yana birebir;
  tüm farklar plandaki bilinçli farklar. Kod düzeltmesi gerekmedi.

### 2026-07-16 — Dilim 6: İşe Alım (ATS) + Uyum

- 7 görev TDD ile tamamlandı; 107 birim test + build yeşil.
- `src/features/recruitment/`: `format.ts` (`formatTimeAgo`, `statusTagClass`,
  `scoreClass`) + `queries.ts` (aday listesi/detay, oluşturma, durum PATCH,
  not POST, hire POST — hire personel query'lerini de invalidate eder),
  `RecruitmentPage` (ATS yerleşimi, server-side arama 300ms debounce +
  Tümü/Yeni/Mülakat filtre sekmeleri, boş durum + "Yeni Aday"),
  `CandidateDetail` (Özgeçmiş/Mülakat Notları/Değerlendirme/Geçmiş sekmeleri,
  not ekleme, pipeline durum select'i, İşe Al modalı — departman/ünvan/tarih →
  personel kaydı), `CandidateModal` (yeni aday formu).
- `src/features/compliance/`: `queries.ts` (belgeler + readiness + 4 mutasyon),
  `CompliancePage` (KPI'lar readiness'tan, durum/risk/arama filtreleri,
  hr-admin'de satır içi durum select + düzenleme + "Yeni Belge",
  manager salt-okur; Yaklaşan Son Tarihler dueDate'li belgelerden türetilir),
  `DocumentModal` (oluşturma POST / düzenleme PUT + sorumlu değişince owner
  PATCH). Dashboard'daki salt-okur `ComplianceRiskPage` silindi; rota
  `/risk/compliance` yeni sayfaya bağlandı (`useComplianceRisk` özet kartı
  için `dashboard/queries`te kaldı).
- **Kararlar / bilinçli farklar:** aday seed'i yok → boş durum + "Yeni Aday"
  (eskide statik 4 aday); pipeline durum select'i ve uyum filtre/mutasyon
  UI'ları eskide yoktu; pozisyon/funnel uçları UI'da kullanılmadı (eski
  ekranda yoktu — YAGNI); değerlendirme sekmesi salt görüntüleme (ekleme
  eskide de yoktu); `.status-tag.hired` ve `.compliance-toolbar` stilleri
  token'larla `main.css`e eklendi.
- Duman: 11 kontrol geçti — aday oluştur/otomatik seçim, arama+filtre, not +
  geçmiş kaydı, 2 pipeline geçişi, işe alım (personelde görünür, select/buton
  pasif) + reddedilen adayda 409, manager recruitment kilidi, uyum tablo +
  filtre, Tamamlandı→risk Düşük + aynı durum 409 toast'ı, yeni belge +
  mükerrer 409, düzenleme (PUT+owner PATCH), manager salt-okur uyum.
  Not: Playwright `has-text` büyük/küçük harf duyarsız alt dizgi eşleşmesi
  yüzünden "Geçmiş" tıklaması "Özgeçmiş"e gidiyordu — script `exact: true`
  ile düzeltildi (uygulama hatası değildi).
- Parite: `#/recruitment` ve `#/risk/compliance` eski uygulamayla yan yana
  birebir; tüm farklar plandaki bilinçli farklar. Tek düzeltme: uyum filtre
  çubuğu yatay düzen CSS'i.

### 2026-07-16 — Dilim 5: Bordro

- 7 görev TDD ile tamamlandı; 88 birim test + build yeşil.
- `src/features/payroll/`: `format.ts` + `queries.ts` (dönem/satır/preview/ayar
  uçları), `PayrollPage` (sekmeler, dönem seçici + "Yeni Dönem", dönem onaya
  gönderme, çalışan rolünde `MyPayslipsView`), `PeriodTab` (KPI, akış, kontrol
  merkezi, parametreler, çalışan tablosu), `PayrollDetailPanel` (özet +
  kazanç/kesinti/matrah kartları + pusula önizleme + **yeni** girdi düzenleme
  formu; Kaydet `PUT` row, Onaya gönder `POST` approve, onaylı satırda Pusula
  İndir PDF), `CalculatorTab` (300ms debounce ile `POST /payroll/preview`,
  personel seçiminde formu satırdan doldurma), `SettingsTab` (12 alan,
  `PUT /payroll/settings`, varsayılana dön).
- **Kararlar / bilinçli farklar:** dönem seçici + "Yeni Dönem" eskide yoktu;
  detay panelinde girdi formu (eskide salt-okur); çalışan görünümü "Bordrolarım"
  (eskide demo tablo); ayar metni "yıla göre saklanır" (eskide tarayıcıda).
  Brüt 0 satırlarda backend SGK taban/istisna hesabı nedeniyle net negatif
  görünebilir — backend davranışı, girdi girilince normalleşir.
- Test düzeltmeleri: detay testinde isim başlık rolüyle sorgulanıyor (isim
  pusula önizlemede de geçiyor); calculator testi son preview POST'una bakıyor
  (ilk debounce brüt=0 ile yarışıyordu).
- Duman: 10 kontrol geçti — dönem oluşturma, girdi kaydetme, kontrol, 409
  "eksik veri" mesajı sonrası IBAN/puantaj işaretleyip satır onayı (4/4),
  dönem onayı + pusula PDF indirme, tekil hesaplama (backend sonucu ₺77.383),
  ayar kaydet + yenilemede kalıcılık, çalışan Bordrolarım + PDF + settings
  yetki kilidi.
- Parite: üç sekme + detay paneli eski uygulamayla yan yana birebir; tüm
  farklar plandaki bilinçli farklar. Kod düzeltmesi gerekmedi.

### 2026-07-14 — Dilim 4: İzin & Onay + Puantaj

- 6 görev TDD ile tamamlandı; 64 birim test + build yeşil.
- `src/features/leaves/`: `LeavesPage` (bakiye kartları `GET /leaves/balance`,
  hareket tablosu + pending iptal, "Ofiste Kimler Yok?" `GET /leaves/team`),
  `LeaveRequestModal` (türler `GET /leaves/types`tan dinamik, takvim günü süre
  ön izlemesi, MGMT'de yerine bakacak kişi listesi, gerçek `POST /leaves`),
  `format.ts`, `queries.ts`.
- `src/features/attendance/`: `AttendancePage` (canlı pano `GET /attendance/live`,
  özet şeridi `GET /attendance/summary` toplamları, gerçek yıl/ay değiştirici,
  aylık puantaj tablosu + CSV), `AttendanceEntryModal` (manuel giriş `POST`,
  satır düzenleme `PUT`), `format.ts`, `queries.ts`.
- Overview'a MGMT rollerine gerçek "Bekleyen Aksiyonlar" kartı eklendi
  (`GET /leaves/pending` + onay/red → `["leaves"]` + dashboard invalidation).
- CSV yardımcıları `features/shared/csv.ts`'e taşındı (personel + puantaj ortak).
- **Kararlar:** kullanılan toplam tek pill (tür kırılımı backend'de yok);
  "Önemli Günler" (doğum günü) eklenmedi (backend'de yok); manuel giriş modalı
  yeni (eskide ghost kart işlevsizdi); kesin izin gün sayısını backend hesaplar
  (UI yalnız takvim günü ön izler). hr-admin/manager hesabına bağlı personel
  kaydı olmadığından `#/leaves` bu rollerde backend'in 403 mesajını gösterir
  ("Hesabınıza bağlı personel kaydı yok...") — bilinçli backend davranışı.
- Duman: 16 kontrol geçti — çalışan talep oluştur/iptal, hr-admin onayı sonrası
  çalışan tarafında "Onaylandı", ekip yokluk widget'ı, canlı pano, ay değiştirici,
  manuel giriş → satır → düzenleme, CSV, çalışan `#/attendance` yetki ekranı.
  Not: backend puantaj seed'i yok; satırlar manuel giriş/gerçek check-in ile oluşur.
- Parite: `#/leaves` (+modal) ve `#/attendance` (iki sekme) yan yana birebir;
  tüm farklar veri kaynaklı (gerçek ay etiketi, gerçek personel) veya plandaki
  bilinçli farklar. Kod düzeltmesi gerekmedi.

### 2026-07-14 — Dilim 3: Personel + Departman

- 6 görev TDD ile tamamlandı; 49 birim test + build yeşil.
- Altyapı: `apiFetch`'e FormData desteği (Content-Type basılmıyor) + `apiDownload`
  (Bearer + 401-refresh + Content-Disposition dosya adı çözümü) — sonraki dilimlerde
  bordro pusulası vb. indirmeler de bunu kullanacak.
- `src/features/personnel/`: `PersonnelPage` (server-side arama 300ms debounce +
  departman/durum filtreleri, toplu seçim + `bulk-deactivate`, CSV dışa aktarma),
  `PersonnelModal` (6 sekme, gerçek POST/PUT, ProblemDetails → form-error, foto
  yükleme), `DocumentsTab` (evrak listesi + yükleme + indirme), `csv.ts`, `queries.ts`.
- **Kararlar:** TC listede maskeli (`nationalIdMasked`); mutasyon eylemleri yalnız
  hr-admin, manager kartı salt-okur; "Yakınlık / Telefon" tek input →
  `emergencyContactPhone`; foto önizleme ikonu kalır (backend foto servis ucu yok);
  sayfalama UI'sı yok (`pageSize=50`, seed küçük); departman CRUD ekranı yok
  (eskide de yoktu).
- Duman: 15 kontrol geçti — CRUD, 409 çakışma mesajı, evrak yükle/indir (backend
  yalnız pdf/jpg/png/doc kabul ediyor), foto, toplu pasife alma, CSV, rol kısıtları.
- Parite düzeltmeleri: "Fotoğraf Yükle" span'e çevrildi (buton çerçevesi
  görünüyordu). Eski uygulamanın bulk-bar/boş-durum görünürlük davranışı birebir
  korunuyor.

### 2026-07-14 — Dilim 2: Overview + Risk Merkezi + 5 risk detayı

- Plan yazıldı (`2026-07-14-react-port-dilim-2-overview-dashboard.md`) ve 7 görev
  TDD ile uygulandı; 33 birim test + `npm run build` yeşil.
- Sayfalar: `OverviewPage`, `RiskCenterPage` (5 paralel sorgu), `AttritionDetailPage`,
  `BurnoutDetailPage`, `ManagerLoadPage`, `EmployeeVoicePage`, `ComplianceRiskPage` —
  hepsi `src/features/` altında, `routes.tsx` `pageFor`'a kayıtlı.
- Altyapı: chart.js 4 + react-chartjs-2 5 (renkler `chartToken` ile CSS token'larından),
  `PageLoading/PageError`, test tarafında `stubApi` (path bazlı fetch stub) ve
  Vitest alias'lı `chartStub` (jsdom'da canvas yok).
- **Veri eşleme kararları** (mock ↔ backend farkları planın tablosunda): ısı haritası
  sürücü metni sayılardan türetilir; dashboard aksiyonları `GET /api/actions`'tan;
  Overview 4. KPI "Bugün İzinli"; Overview alt grid (onay kartları + doğum günleri)
  Dilim 4'e ertelendi; nabız "Riskli Ekipler" `level != low` departmanlardan türetilir;
  uyum sayfasının denetim çeklisti Dilim 6'ya bırakıldı (backend'de yok).
- Duman testi: 15 Playwright kontrolü geçti (grafikler, tablolar, rol geçişi,
  yetki ekranı, API kesintisinde PageError). Bulunan tek gerçek fark düzeltildi:
  Overview "Talepleri incele" `<button>` yerine eskisi gibi `<a>` (`Link`) oldu.
- Not: rol değiştirici testinde yarış var — değişimin bitmesi kullanıcı etiketinden
  beklenmeli (duman script'lerinde `user-profile strong` kontrolü).

### 2026-07-14 — Dilim 1 kapanışı: duman testi, parite, merge

- Task 7 tamamlandı: 15 kontrollük Playwright duman testi **hepsi geçti**
  (hr-admin girişi → dashboard; rol değiştirici → çalışan → `#/overview`;
  çalışanken `#/settings` → "Bu alan için yetki gerekli"; hatalı şifre →
  ProblemDetails mesajı; tema + daraltılmış menü yenilemede kalıcı; Ctrl+K →
  "izin" → İzinlerim → Enter; çıkış → login).
- Görsel parite: eski mock (4173) ile React portu (5173) login + shell ekran
  görüntüleri birebir; kod düzeltmesi gerekmedi. Tek bilinçli fark login metni
  (gerçek backend oturumu açıklaması) ve içerik alanındaki placeholder
  (dashboard Dilim 2'de portlanacak).
- `feature/react-port-dilim-1` main'e merge edildi (fast-forward + uzaktaki
  README commit'i `93e6ce1` merge edildi), merge sonrası 14/14 test yeşil,
  dal silindi.
- Durum: frontend iskeleti main'de; 5 test dosyası / 14 test.

### 2026-07-13 — Dilim 1: iskelet (Task 1–6)

- **Task 1:** Vite 6 + React 18.3 + TS strict iskeleti; 11 CSS dosyası
  değiştirilmeden `frontend/src/styles/`e kopyalandı; Vitest + RTL kuruldu;
  `/api` → `http://localhost:5053` proxy. (`bc11de9`, temizlik `a8c50ab`)
- **Task 2:** `npm run gen:api` — backend swagger'ından `src/api/schema.d.ts`
  tip üretimi; backend'de şema adı çakışması düzeltildi (`1743fd4`, `37b7048`).
- **Task 3 (TDD):** `session.ts` (`ikpro-session` localStorage) + `client.ts`
  fetch wrapper: Bearer, ProblemDetails → ApiError, 401'de tek-uçuş refresh +
  retry, refresh düşerse oturum temizle + `#/login`. (`13e130f`)
- **Task 4 (TDD):** `AuthContext` + `LoginPage` (login/signup) — gerçek API'ye
  bağlı, eski `AuthScreen` DOM paritesi; signup'a şifre alanı eklendi (backend
  zorunlu kılıyor). Review düzeltmeleri: sekme çubuğu + çift-form DOM paritesi.
  (`7ff6345`, `70a5839`)
- **Task 5 (TDD):** `routes.tsx` — eski `routes.js` ile birebir 17 korumalı
  rota, rol matrisi (`hr-admin`/`manager`/`employee`), `roleHomeFor`;
  `guards.tsx` — `RequireAuth`, `PublicOnly`, `RouteGate` (yetkisiz rol
  redirect edilmez, kilit ekranı görür); hash router. Review düzeltmesi:
  sayfa component'leri JSX olarak render (hook güvenliği). (`27ff298`,
  `10da700`, `71120a4`)
- **Task 6:** `AppShell` (sidebar/header/tema/rozet/rol değiştirici),
  `GlobalSearch` (Ctrl+K, bu dilimde yalnız sayfa sonuçları), `ToastProvider`.
  Review düzeltmeleri: nav DOM düzleştirme, toast timer temizliği, arama
  eşiği; build'i kıran tip hataları giderildi. (`8a21129`, `1029a81`,
  `27c18c8`)

### 2026-07-13 — Spec + Dilim 1 planı

- React'e dönüşüm tasarım dokümanı yazıldı:
  `docs/superpowers/specs/2026-07-13-react-frontend-port-design.md` (`9cf48f1`).
- Dilim 1 (iskelet) uygulama planı yazıldı:
  `docs/superpowers/plans/2026-07-13-react-port-dilim-1-iskelet.md` (`8054b26`).
- Dilimleme: 1 iskelet · 2 Overview+Dashboard · 3 Personel · 4 İzin+Puantaj ·
  5 Bordro · 6 İşe Alım+Uyum · 7 Aksiyonlar+Arama+Ayarlar · 8 kapanış.
  Her dilimin planı bir önceki bittikten sonra yazılır; sayfa portları
  `routes.tsx`'teki `pageFor` eşlemesine gerçek component'lerini kaydeder.

### Öncesi — Backend

- `backend/` altında .NET 9 Clean Architecture (EF Core + MSSQL, CPM);
  ayrıntı ve faz planı: `raporlar/backend-development-plan.md`.
- Eski frontend: mock uygulama (dokunulmaz — parite referansı); Dilim 8'de
  `legacy-frontend/` altına taşındı.
