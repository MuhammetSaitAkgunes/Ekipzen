# Kiracı Başına Ayrı Veritabanı — Tasarım

> **Durum:** onaylandı (2026-08-06)
> **Önceki tasarım:** `2026-08-05-kiraci-dosya-bolumleme-design.md` (dosya tarafı; bu iş onun veritabanı karşılığı)

## Problem

Tek bir müşteriyi yedeğinden geri döndüremiyoruz.

Yedek tüm kiracıları kapsayan tek bir veritabanı dosyası. Bir müşteri "dünkü
hâlimize dönün" dediğinde yedekten geri yüklemek **tüm müşterileri** o ana
döndürmek demek — yapılamaz. Gerçekte yapılabilecek olan, yedeği yan bir
veritabanına açıp o kiracının satırlarını 30 tablodan FK sırasına dikkat ederek
elle kopyalamaktır: yarım gün süren, `Id` çakışmasına açık, prova edilmemiş bir
el işi.

İkincil sorunlar aynı kökten:

- **KVKK imhası kanıtlanamıyor.** `TenantPurger` canlı veriyi siler ama yedekteki
  kopya saklama süresi boyunca durur; seçici silme mümkün değil.
- **Kiracı sınırı her yeni kısıtta yeniden düşünülmek zorunda.** 2026-08-06'da
  bulunan tekil indeks hatası (departman adı, TC Kimlik No, bordro dönemi sistem
  genelinde tekildi) tam olarak bu sınıftandı.
- **Tek veritabanı tek hata noktası.** Düşerse tüm müşteriler düşer.

## Kapsam

**Dahil:** platform/kiracı veritabanı ayrımı, yönlendirme dizini, bağlantı
çözümü, kiracı yaşam döngüsü (provizyon/migration/purge), yedekleme ve
nokta-zaman kurtarma, test altyapısı.

**Hariç:** `TenantId` sütunlarının ve global filtrelerin kaldırılması (Yol B —
ayrı ve sonraki bir iş), çok sunuculu dağıtım, bulut geçişi, login akışına
kiracı seçimi eklenmesi.

## Bağlam kararları

| Karar | Seçim | Gerekçe |
| --- | --- | --- |
| Barındırma | Kendi sunucumuzda SQL Server | Verilen karar; nokta-zaman kurtarma FULL+LOG zinciriyle elle kurulacak |
| Identity nerede | Kiracı DB'sinde | Geri yükleme **bütün** olsun: veri ve kullanıcı hesapları birlikte dönsün |
| `TenantId` sütunları | **Korunuyor** | Değişiklik altyapıda kalsın; geri dönüş yolu açık olsun; `.bak` kime ait okunabilsin |
| Müşteri ölçeği | Belirsiz | Script'ler döngüsel yazılır, sonradan paralelleştirilebilir |
| Tek e-posta = tek kiracı | Korunuyor | 2026-08-06 kararı; yeni mimaride dizin bunu zorlar |
| RPO / RTO | **15 dakika / 2 saat** | Log yedeği sıklığını belirler; runbook'taki açık kapanır |

---

## Mimari

### İki katman

**`EkipzenPlatform`** — tek adet. `PlatformDbContext`.

| Tablo / sütun | İçerik | Ne zaman |
| --- | --- | --- |
| `Tenants` | Ad, slug, **Status**, oluşturma zamanı | Faz 1a |
| `TenantDirectory` | Normalize e-posta (PK) → TenantId | Faz 1a |
| `Tenants.DatabaseName` | Kiracının veritabanı adı | **Faz 2** |
| `Subscriptions` | Abonelik ve faturalama | **Faz 2** |

`DatabaseName`, kiracı veritabanları gerçekten ayrılana kadar hiçbir şey ifade
etmez; kullanılmayan bir sütunu erken eklemek yerine ihtiyaç doğduğu fazda
eklenir.

**`Ekipzen_{slug}`** — müşteri başına bir adet. Bugünkü `AppDbContext`, değişmeden.

30 İK tablosu (`TenantId` sütunlarıyla), Identity (`Users`, `Roles`,
`UserRoles`, `RefreshTokens`…), okuma modeli view'ları, audit trigger'ları, ve
kendi migration geçmişi.

`Tenants` bugün `AppDbContext`'te ve 15 yerden kullanılıyor (kiracı yönetimi,
kimlik, seed) — hepsi platform context'ine taşınır. `Subscriptions` uygulamada
tek yerden okunuyor (`SettingsHandlers.cs`), taşınması bir ek sorgu maliyeti.

`Roles` her kiracı DB'sinde kendi kopyasını taşır (hr-admin, manager, employee),
DB kurulurken seed edilir. Bugün global oldukları için bu sadeleşmedir.

### Neden Subscriptions platformda

"Kim hangi pakette, kimin yenilemesi geldi" bir platform sorusudur; kiracı
DB'lerinde dursaydı her fatura raporu tüm veritabanlarını gezmek zorunda
kalırdı. Yan faydası: bir müşteriyi geri yüklediğinizde **aboneliği geri
sarılmaz** — doğrusu budur.

### Yönlendirme dizini

```
TenantDirectory( NormalizedEmail PK, TenantId )
```

Login'in tek işi e-postadan kiracıyı bulmak. **Parola doğrulaması kiracı
DB'sinde yapılır; kimlik bilgisi platform DB'sine hiç yazılmaz.**

Dizin **türetilmiş**tir, asıl kaynak değildir. Asıl kayıt kiracı DB'sindeki
`Users` tablosudur. Dizin bozulursa ya da bir geri yüklemeden sonra saparsa,
kiracı DB'si taranarak yeniden kurulur. Bunun için açık bir komut olacak.

"Tek e-posta = tek kiracı" kuralını `NormalizedEmail` birincil anahtarı zorlar —
bugün tek `Users` tablosunun sağladığı garanti, aynı güçle korunur.

### Veritabanı adı saklanır, hesaplanmaz

`Tenants.DatabaseName` provizyonda bir kez üretilir (`Ekipzen_acme`) ve bir daha
değişmez.

Alternatifi tehlikeli: ad slug'dan her seferinde hesaplansaydı, slug değiştiği
gün uygulama var olmayan bir veritabanına bağlanır ve müşterinin verisi
"kaybolmuş" görünürdü. Sunucu adresi yapılandırmadan gelir; çok sunuculu
senaryo bugün yok, gerekirse `Tenants`'a bir sütun eklenir.

---

## İstek akışı

### Bağlantı çözümü

Bağlantı dizesi bugün tek yerden okunuyor (`DependencyInjection.cs:23`) —
ayrılma noktası dar. Yerine kiracıdan katalog adı üreten bir çözücü gelir. Aynı
sunucu, farklı `Database=`; bağlantı dizeleri yalnız katalog adında farklılaşır.

**Kiracı kütüğü** bellekte önbelleklenir: `TenantId → (DatabaseName, Status)`.
Her istekte platform DB'sine gitmek, tek veritabanına dolambaçlı bir dönüş
olurdu. Durum değiştiğinde ilgili kayıt **anında** düşürülür — süre dolması
beklenmez.

> **Fazlara göre:** Faz 1b'de kütük yalnız `Status` tutar ve çözücü herkese aynı
> bağlantıyı döndürür; `DatabaseName` Faz 2'de hem sütun hem kütük alanı olarak
> eklenir. Bu bölüm bitmiş hâli anlatır.

### Login

1. Platform: normalize e-posta → TenantId
2. Kütük: TenantId → DatabaseName, Status
3. Kiracı DB'sine bağlan, parolayı orada doğrula
4. JWT'ye `tenant={id}` yaz

**JWT formatı değişmiyor** — dağıtım anında kimse oturumundan düşmez. Sonraki
her istek JWT'deki kiracıdan doğrudan doğru veritabanına gider.

### Erişim kapısı

`Tenants.Status` dört değerlidir:

| Durum | Anlamı | Erişim |
| --- | --- | --- |
| `Provisioning` | Veritabanı kuruluyor | Kapalı |
| `Active` | Normal | Açık |
| `Frozen` | Bakım / geri yükleme | Kapalı |
| `Purging` | Siliniyor | Kapalı |

`Status`, bugünkü `Tenants.IsActive` alanının **yerini alır** — iki durumlu
bayrak dört durumu ifade edemez. Bugün `IsActive=false` iki ayrı anlamda
kullanılıyor: "self-servis kayıt henüz doğrulanmadı" ve "kiracı kapalı".
Yeni modelde birincisi `Provisioning`, ikincisi `Frozen`'dır; ayrım netleşir.
`PurgeUnverifiedAsync`'in "doğrulanmamış kiracı" ölçütü `Provisioning` durumuna
bağlanır.

Kapı **bağlantı çözümünün içindedir.** Yani yalnız login değil, elinde geçerli
access token olan bir istek de geçemez; refresh de geçemez.

Bu, bugünkü bir açığı kapatır: `Tenants.IsActive = false` yeni girişleri
engelliyor ama `RefreshAsync` kiracının durumuna bakmıyor — elinde refresh
token olan biri oturumunu süresiz uzatabiliyor. Hiçbir istek kiracı çözülmeden
veritabanına ulaşamadığı için açık yapısal olarak kapanır.

### HTTP dışı bağlam

Arka plan işleri (ör. `UnverifiedTenantCleanupService`) ve platform işlemleri
JWT'siz çalışır. Bugün `ICurrentTenant.Impersonate(id)` yalnız bir tamsayı
atıyor; yeni mimaride kiracı, DbContext oluşturulurken **bağlantıyı belirliyor.**

Bu bir tuzak doğurur: `Impersonate` context alındıktan sonra çağrılırsa context
zaten yanlış veritabanına bağlanmıştır — **ve bu sessizce olur.** Sıraya
güvenmek yerine sırayı imkânsız kılıyoruz: kiracıya sabitlenmiş bir kapsam
üreten fabrika. Kapsam alınır, context içinden çıkarılır; yanlış sırada kullanma
imkânı yoktur. Testlerdeki `SeedInTenantAsync` zaten bu deseni kullanıyor;
genelleştirilir.

### Hata durumları

| Durum | Yanıt | Not |
| --- | --- | --- |
| Dizinde e-posta yok | 401, genel mesaj | Hesabın varlığı sızdırılmaz |
| Kiracı `Frozen` / `Purging` | 403, anlamlı mesaj | Tek görünür arayüz değişikliği |
| Kiracı DB'si erişilemez | 503 | Hata değil, bilinen durum |
| Geri yükleme sürüyor | 503 | SQL Server bağlantıyı reddeder |

Veritabanı adı ve gerçek sebep **loga** yazılır, istemciye yazılmaz.

### Kabul edilen risk

Platform DB'si yeni bir tek hata noktasıdır: düşerse yeni giriş yapılamaz. İki
hafifletici — platform DB'si çok küçüktür (üç tablo), ve kütük bellekte olduğu
için hâlihazırda giriş yapmış kullanıcılar çalışmaya devam eder.

---

## Yaşam döngüsü

### Provizyon

Bugün bu iş tek transaction: kiracı satırı ve admin kullanıcısı aynı
veritabanına, ya ikisi de ya hiçbiri. Araya `CREATE DATABASE` girince atomiklik
kaybolur — durum makinesiyle telafi edilir.

1. **Platform, tek transaction:** `Tenants` satırı (`Status=Provisioning`,
   `DatabaseName`) **ve** `TenantDirectory` satırı birlikte
2. `CREATE DATABASE`
3. Migration'lar uygulanır
4. Roller seed edilir, admin kullanıcı (şifresiz) kiracı DB'sinde oluşturulur
5. Davet e-postası gönderilir
6. Platform: `Status = Active`

**E-posta 1. adımda rezerve edilir.** Dizin PK'sı çakışmayı yakalar; eşzamanlı
iki kayıt aynı e-postayı alamaz ve bugünkü 409 davranışı korunur.

**Davet en sonda gönderilir.** Erken gönderilip sonraki adım patlarsa müşteri
çalışmayan bir kuruluma davet linki almış olur.

**Hedef veritabanı zaten varsa provizyon reddedilir.** Başarısız bir denemeden
kalmış DB'yi sessizce yeniden kullanmak, iki müşterinin verisinin karışması
demektir.

### Yarıda kalan provizyon

Kiracı `Status=Provisioning` durumunda kalır — görünür bir durum, sessiz enkaz
değil. Bu durumdaki kiracı hiçbir isteği kabul etmez ve müşteriye davet
gitmemiştir.

Bekleyenleri listeleyen bir komut olacak; operatör **yeniden dener** ya da
**geri alır**. Yeniden deneme güvenlidir (migration'lar uygulanmışları atlar,
kullanıcı oluşturma idempotenttir). Geri alma güvenlidir: `Provisioning`
durumundaki kiracıda tanım gereği müşteri verisi yoktur.

### Migration orkestrasyonu

Kiracı DB'leri **uygulama açılışında migrate edilmez.** Üç sebeple: açılış
süresi müşteri sayısıyla büyür; ortada patlarsa bir kısmı güncel bir kısmı eski
kalır ve uygulama yarı ayakta olur; birden fazla örnek aynı DB'de yarışır.

- **Platform DB** açılışta migrate olur — küçük, her zaman gerekli.
- **Kiracı DB'leri** dağıtımın ayrı bir adımı olarak, tek tek dolaşan bir
  komutla. Her kiracı için başarı/başarısızlık raporlanır; biri bile patlarsa
  komut sıfırdan farklı kodla çıkar.
- **Açılış migrate etmez, doğrular.** Şeması geride kalan kiracı 503 döner ve
  log yüksek sesle söyler. Eski şemayla sessizce çalışmak veri bozulmasıdır.

Yan fayda: **kanarya.** Önce tek müşteri migrate edilip kontrol edilebilir,
sonra kalanlara yayılır. Tek veritabanında bu imkânsızdı.

Demo kiracılar artık gerçek provizyon yolundan geçerek kurulur; böylece kayıt
akışı her geliştirme kurulumunda çalıştırılmış olur.

### Purge

1. `Status = Purging` → erişim anında kesilir
2. Dosya alanı silinir (`DeleteTenantSpaceAsync` — mevcut)
3. `DROP DATABASE`
4. Platform: dizin satırı, abonelik, kiracı satırı silinir

`TenantPurger`'ın FK sıralı silme mantığı **gereksizleşir** — `DROP DATABASE`
hiçbir tabloyu kaçırmaz. Yaklaşık 100 satır hassas kod emekli olur.

`DROP` için aktif bağlantı kalmamalı: önce tek kullanıcı moduna alınır ve
bağlantı havuzu temizlenir. `DROP` başarısız olursa durum `Purging`'de kalır ve
operatör uyarılır — **yarım silinmiş bir kiracı asla erişilebilir bırakılmaz.**

KVKK: DROP'tan sonra veri yalnız o müşterinin kendi yedek dosyalarında kalır ve
onlar tek tek imha edilebilir. Bugün mümkün olmayan şey buydu.

---

## Yedekleme ve kurtarma

### Plan

Her kiracı DB'si **FULL recovery** modunda olmak zorundadır; nokta-zaman
kurtarma ancak log yedekleriyle mümkündür.

| Tür | Sıklık | Ne sağlar |
| --- | --- | --- |
| FULL | Günlük | Zincirin başlangıcı |
| DIFF | 6 saatte bir | Geri yükleme süresini kısaltır |
| LOG | 15 dakikada bir (RPO) | Nokta-zaman hassasiyeti |

Platform DB'si de aynı plana dahildir — küçüktür ama onsuz kimse giriş yapamaz.

**Maliyet notu:** toplam yedek *boyutu* bugünkünden farklı olmaz (aynı veri,
farklı dosyalara bölünmüş). Ama dosya ve iş sayısı çarpılır — 40 müşteri ×
15 dakikalık log yedeği ≈ günde ~3.800 dosya. Bunları üreten, doğrulayan ve
saklama süresi dolunca silen bir iş şarttır.

### Geri yükleme

1. `Status = Frozen` → erişim anında kesilir
2. `RESTORE DATABASE ... STOPAT '<an>'`
3. Dosya arşivi `storage/tenant-{id}/` üzerine açılır
4. **Yönlendirme dizini o kiracı için yeniden kurulur**
5. Doğrulama, sonra `Status = Active`

Dördüncü adım zorunludur: platform DB'si geri sarılmadığı için dizin bugünkü
hâlindedir. Geri yükleme anından sonra eklenmiş bir kullanıcı dizinde vardır ama
geri yüklenmiş kiracı DB'sinde yoktur. Dizin türetilmiş olduğu için sapma
kalıcı olamaz.

### Tatbikat

Bugünkü tatbikat "yedek geri yükleniyor mu, satır sayıları tutuyor mu" diye
bakıyor. Yeni tatbikat **nokta-zaman hassasiyetini** kanıtlamalıdır:

1. Bir kiracı seçilir (her koşumda sırayla)
2. Zaman damgası → belirteç bir değişiklik → ikinci zaman damgası
3. Yan bir DB'ye, iki damganın **arasına** geri yüklenir
4. Belirteç değişikliğin **olmadığı** doğrulanır
5. Yan DB düşürülür

İddia "yedek açılıyor"dan "istediğim ana dönebiliyorum"a yükselir.

---

## Test

Bugün 111 entegrasyon testi tek paylaşılan veritabanında koşuyor; suite'te 16
provizyon çağrısı var. Her biri artık gerçek bir veritabanı kuracak: 16 DB ×
migration ≈ koşuma 1–2 dakika ekler. Backend job'ı 93 saniyeden ~3 dakikaya
çıkar. **Kabul ediliyor.**

Şablon veritabanı kopyalayarak hızlandırma mümkündür ama **şimdi yapılmaz** —
gerçekten sorun olursa yapılır.

Fabrika, koşu başında `Ekipzen_test_*` kalıntılarını da düşürmelidir. Bugün tek
test veritabanını düşürüyor; biriken kalıntılar bir süre sonra koşuları
açıklanamaz şekilde bozar.

**Eklenecek testler**

- Dondurulmuş kiracıda login, refresh ve normal isteğin **üçünün de** reddi
- Yarıda kalan provizyonun `Provisioning`'de kalması, yeniden denenebilmesi ve
  geri alınabilmesi
- Var olan veritabanına provizyonun reddi
- Dizin yeniden kurmanın aynı sonucu üretmesi
- Purge'ün veritabanını ve platform satırlarını birlikte silmesi
- Migration koşucusunun kiracı başına başarısızlığı raporlaması ve sıfırdan
  farklı kodla çıkması

---

## Geçiş

| Faz | İçerik | Geri dönüş |
| --- | --- | --- |
| 1a · Platform katmanı | Platform DB, `Tenants` taşınması, `TenantStatus`, yönlendirme dizini, dizin yeniden kurma, provizyon durum makinesi, çapraz-DB purge. | Mümkün |
| 1b · Bağlantı tesisatı | Bağlantı çözücü, kiracı kütüğü, durum kapısı, kiracıya sabitlenmiş kapsam fabrikası. Çözücü **herkese aynı veritabanını** döndürür — hiçbir şey değişmemiş gibi çalışır. | Mümkün |
| 2a · Kimlik hazırlığı | Refresh token'lar platform DB'sine taşınır; kullanıcı işlemleri kiracıya sabitlenmiş kapsamda çalışır. Veri **bölünmez**, davranış değişmez. | Mümkün |
| 2b · Ayrılma | Provizyon gerçek DB kurar; `Tenants.DatabaseName`; çözücü katalog üretir; purge `DROP DATABASE` yapar; `Subscriptions` platforma taşınır; demo kiracılar yeni yoldan yeniden oluşturulur. | Zor — asıl eşik |
| 3 · Yedekleme | Tatbikat, yedek planı, migration koşucusu. | — |
| 4 · Temizlik (isteğe bağlı) | Yol B: `TenantId` ve filtrelerin kaldırılması. Üretimde oturduktan sonra, ayrı karar. | — |

Faz 1 iki plana bölündü: 1a kiracı **kimliğini** ayırır, 1b kiracıdan
**bağlantıya** giden yolu kurar. Tek planda toplamak, 1a sonundaki doğrulama
noktasını kaybettirirdi.

`Subscriptions` Faz 2'ye alındı: kiracı veritabanları gerçekten ayrılana kadar
merkezî olmasının faydası yok, taşımanın riski ise bugünden alınmış olurdu.

**Faz 2 neden ikiye bölündü:** `UserManager`, `AppDbContext`'e bağlıdır ve o da
bağlantısını aktif kiracıdan alır. Veritabanları ayrıldığı anda login kırılır —
kullanıcıyı bulmak için hangi katalogda arayacağını bilmen gerekir, ama kiracıyı
bulmak için kullanıcıyı bulman gerekir. Kimlik katmanının bu düğümü çözmesi
(2a) veri bölünmeden yapılabilir ve tek başına doğrulanabilir; ayrılmanın (2b)
ön koşuludur.

**Refresh yönlendirme kararı (2a):** Elinde yalnız token dizesi olan sunucu hangi
kiracı veritabanına bakacağını bilemez — dizin burada yardım edemez. Karar:
**refresh token'lar platform veritabanına taşınır.** Satır zaten `TenantId`
taşıdığı için yönlendirmeyi o verir ve token biçimi değişmez. Bedeli:
`RefreshTokens.UserId → Users.Id` yabancı anahtarı düşer (çapraz-DB FK mümkün
değil) ve bir kiracıyı geri yüklemek **oturumları geri sarmaz** — ki bu doğru
davranıştır, oturum veri değil durumdur.

Faz 1, riskli tesisatı veri taşımasından ayırır ve tek başına doğrulanabilir
kılar. Gerçek müşteri verisi olmadığı için Faz 2'de taşıma işi yoktur — geçişi
bugün yapmanın en büyük avantajı budur.

**Her faz kendi uygulama planını alır.** Üçünü tek plana sıkıştırmak, Faz 1'in
sonundaki doğrulama noktasını — bu tasarımın en değerli güvenlik ağını —
anlamsız kılardı. Plan sırası: önce Faz 1, tamamlanıp doğrulandıktan sonra
Faz 2, sonra Faz 3.

---

## Riskler

| Risk | Hafifletme |
| --- | --- |
| Platform DB tek hata noktası | Çok küçük; kütük sayesinde mevcut oturumlar etkilenmez |
| Yanlış sırada `Impersonate` → sessizce yanlış DB | Kapsam fabrikası; sıra yapısal olarak imkânsız |
| Dizin ile kiracı DB'sinin sapması | Dizin türetilmiş; yeniden kurma komutu; geri yüklemenin zorunlu adımı |
| Yarıda kalan provizyon | `Provisioning` durumu görünür; yeniden dene / geri al komutları |
| Yedek dosya sayısının patlaması | Saklama ve temizlik işi baştan planlanır |
| Migration'ın bazı kiracılarda başarısız olması | Koşucu tek tek raporlar, sıfırdan farklı kodla çıkar; açılış doğrular ve 503 verir |
| Test veritabanı kalıntıları | Fabrika koşu başında `Ekipzen_test_*` düşürür |
