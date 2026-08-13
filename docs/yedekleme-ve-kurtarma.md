# Yedekleme ve Kurtarma Runbook'u

> **Neden bu belge en kritik olanı:** İK Pro'daki diğer her hata geri alınabilir.
> Veri kaybı alınamaz. Bir müşterinin özlük dosyaları, bordro geçmişi ve izin
> kayıtları kaybolursa hizmet devam edemez ve KVKK açısından da ihlal doğar.

## Temel ilke

**Yedeğin var olması yeterli değildir; geri yüklenebildiği kanıtlanmalıdır.**

Test edilmemiş yedek, olmayan yedektir. Sektörde en sık görülen felaket, yedeğin
alınmamış olması değil, alınan yedeğin bozuk çıkması ya da geri yükleme
prosedürünün ilk kez felaket anında denenmesidir. Bu yüzden aşağıdaki tatbikat
zorunludur.

## Hedefler (üretim için belirlenmeli)

Bu iki sayı ticari taahhüdünüzü belirler; müşteri sözleşmesine yazılmadan önce
kararlaştırılmalıdır.

**Karar verildi — 2026-08-13.**

| Hedef | Değer | Anlamı |
| --- | --- | --- |
| **RPO** | **1 saat** | Felaket anında en fazla 1 saatlik veri kaybı. Saatlik işlem günlüğü yedeği zorunlu. |
| **RTO — tek müşteri** | **4 saat** | Bir müşterinin verisi bozulduğunda (yanlış silme, veri bozulması) o müşteri 4 saat içinde geri gelir. Diğerleri hiç etkilenmez. |
| **RTO — tüm sistem** | **bir sonraki mesai gününün sonu** | Sunucu/veri merkezi kaybında. |

### Bu sayılar neden böyle — ve neyi taahhüt ETMİYOR

**RPO 1 saat.** Kaybedilen saatte tipik olarak birkaç izin talebi, o sabahın
puantaj girişleri ve personel kartı düzenlemeleri olur. En sinsisi sonuncusudur:
izin ve puantaj yeniden girilir, ama "dün kartta neyi değiştirmiştim" kimse
hatırlamaz. Bordro ise kaynak veriden yeniden koşturulabilir — kayıp değil,
tekrar iştir. Log yedeği küçük olduğu için 1 saat, disk açısından neredeyse
bedava; daha sık almak da mümkündür ve karar ileride sıkılaştırılabilir.

**RTO neden iki tane:** kiracı başına veritabanına geçtikten sonra tek müşteriyi
geri yüklemek, diğerlerine hiç dokunmadan yapılan **küçük** bir iştir (tatbikatta
demo ölçeğinde saniyeler). Tüm sunucuyu geri yüklemek ise müşteri sayısıyla
büyür. İkisine aynı sayıyı vermek, birini abartmak diğerini gizlemek olurdu.

> ⚠️ **"Tüm sistem" için mesai içi taahhüdünün sebebi teknik değil:**
> `RTO = fark etme + karar verme + geri yükleme + doğrulama`. Bugün **fark etme**
> ayağı yok — uyarı webhook'u yapılandırılmadı, zamanlanmış yedek görevi
> kurulmadı, nöbet tutan kimse yok. Gece 03:00'te disk giderse bu, sabah ilk
> müşteri arayınca öğrenilir. Daha sıkı bir RTO ancak şunlar kurulduktan sonra
> taahhüt edilebilir: otomatik uyarı + zamanlanmış yedek + off-site kopya +
> nöbet. Bunlar kurulmadan yazılacak "4 saat" bir taahhüt değil, temenni olurdu.

### RPO'nun teknik ön koşulu: FULL kurtarma modeli

Saatlik log yedeği **yalnız FULL recovery model'de** mümkündür. SIMPLE'da işlem
günlüğü yedeği alınamaz; o veritabanı için RPO sessizce "son tam/fark yedeği"ne
geriler ve `RESTORE … STOPAT` imkânsız olur.

Bu ortama bağlı bir tuzaktı: `CREATE DATABASE` kurtarma modelini sunucunun
`model` veritabanından **miras alır** ve SQL Server Express ile pek çok
sıkılaştırılmış kurulum SIMPLE gelir. İki yerden kapatıldı:

1. **Provizyon** kurtarma modelini açıkça `FULL` yapar (harmanlamayla aynı
   gerekçe: ortam varsayılanına güvenilmez). Testi: `VeritabaniSaglayiciTests`.
2. **Yedek tatbikatı** her veritabanının FULL olduğunu doğrular; değilse o
   veritabanı için **başarısız** olur ve düzeltme komutunu yazar. Platform ve
   paylaşılan katalog EF tarafından oluşturulduğu için provizyon yolundan
   geçmez — bu kontrol onları da kapsar.

## Yedek planı

| Tür | Sıklık | Saklama | Not |
| --- | --- | --- | --- |
| Tam (FULL) | Günlük | 30 gün | Gece düşük trafikte |
| Fark (DIFFERENTIAL) | 6 saatte bir | 7 gün | Geri yükleme süresini kısaltır |
| İşlem günlüğü (LOG) | **Saatlik** (RPO = 1 saat) | 7 gün | `backup-log.ps1`; FULL recovery model zorunlu (bkz. yukarısı) |

**5. Görevi çalıştıran hesabın SQL Server'da SYSADMIN olması gerekir.**
Görev SYSTEM ile kaydedilir ve SYSTEM sunucuya bağlanabilir — ama varsayılan
olarak **hiçbir kullanıcı veritabanına erişemez**. Sonuç, ilk koşumda:

```
Login failed for user 'NT AUTHORITY\SYSTEM'
(gerçek sebep: hata 916 — "not able to access the database")
```

**Neden veritabanı-başına yetki vermek ÇÖZMEZ:** yedek hesabı, bugün var olmayan
kiracı veritabanlarına da erişmek zorundadır. Her yeni müşteri provizyonlandığında
biri gidip yetki vermezse yedekleme o müşteri için **sessizce** kırılır — yani
yedeklemenin çalışması, kimsenin unutmamasına bağlı hale gelir. Sunucu seviyesinde
yetki bu yüzden gereklidir:

```sql
-- SYSTEM ile çalıştıracaksanız (varsayılan):
ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM];
```

> **Güvenlik dengesi — bilinçli seçin.** Bu, o makinede SYSTEM olarak çalışan
> HER ŞEYE SQL Server üzerinde tam yetki verir. Adanmış bir veritabanı
> sunucusunda bu zaten fiilen böyledir (SYSTEM veri dosyalarına erişebilir) ve
> yedekleme otomasyonu için standart yaklaşımdır. Uygulama ile veritabanının aynı
> makinede olduğu bir kurulumda ise yüzey genişler; o durumda görevi **ayrı bir
> hizmet hesabıyla** çalıştırıp sysadmin'i yalnız ona verin:
>
> ```powershell
> ... register-backup-task.ps1 -TaskUser "ALANADI\ikpro-yedek" ...
> ```
>
> Bu hesap için Görev Zamanlayıcı şifre soracaktır.

> **Üretim notu — script'ler kullanıcı profilinde durmasın.** Geliştirme
> makinesinde depo `C:\Users\<kullanıcı>\OneDrive\...` altında olabilir ve bu
> bugün çalışır. Üretimde olmaz: SYSTEM olarak koşan görev kullanıcı profiline
> ve OneDrive'a bağımlı hale gelir — dosya "yalnız bulutta" işaretlenirse
> (Files On-Demand) görev sessizce başarısız olur, kullanıcı profili silinirse
> yedekleme durur. Script'leri `C:\IKPro\scripts` gibi profil dışı bir yola
> kopyalayın ve görevleri oradan kaydedin.

### Script'leri çalıştırmadan önce — üç tuzak

Bunların üçü de gerçekten yaşandı; buraya yazılmalarının sebebi bu.

**1. `pwsh` değil `powershell`.** `pwsh` PowerShell 7'dir ve Windows'ta
**kurulu gelmez**. Script'ler Windows PowerShell 5.1 ile çalışır:

```
powershell -ExecutionPolicy Bypass -File "scriptsackup-restore-drill.ps1" ...
```

**2. Doğru dizinde olun.** Depoda git worktree'leri varsa (`.claude/worktrees/…`)
onlar farklı bir dalın kopyasıdır ve `scripts/` klasörünü içermeyebilir. Komutları
**ana depo kökünden** çalıştırın.

**3. Yedek klasörüne yazan SİZ değil, SQL Server servis hesabısınız.**
`BACKUP DATABASE` işini `NT Service\MSSQLSERVER` yapar. Yeni açtığınız bir
klasöre (ör. `C:\SQLYedek`) sizin yazma izniniz olması yetmez; servis hesabının
izni yoksa yedek şu hatayla düşer:

```
Cannot open backup device '...'. Operating system error 3 / 5
```

En güvenli yol, SQL Server'ın **kendi varsayılan yedek klasörünün altını**
kullanmaktır (izin zaten vardır):

```sql
SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400));
```

Başka bir klasör (ör. ayrı bir disk) kullanacaksanız izni açıkça verin:

```
icacls "D:\Yedek\IKPro" /grant "NT Service\MSSQLSERVER:(OI)(CI)F"
```

**4. Kayıt için YÖNETİCİ hakkı şart, ve `-WhatIf` KAYIT YAPMAZ.** Görev SYSTEM
hesabıyla çalışacak şekilde kaydedilir; bu yükseltilmiş hak ister. Sıra şudur:

```powershell
# a) Önce ne yapacağını gör — sistemi DEĞİŞTİRMEZ, yönetici gerekmez:
powershell -ExecutionPolicy Bypass -File scripts
egister-backup-task.ps1 -BackupPath "<yol>" -WhatIf

# b) Sonra YÖNETİCİ PowerShell'de, -WhatIf OLMADAN çalıştır:
powershell -ExecutionPolicy Bypass -File scripts
egister-backup-task.ps1 -BackupPath "<yol>"
```

(b) adımı atlanırsa görev **hiç oluşmaz** ve doğrulama komutu
"Sistem belirtilen dosyayı bulamıyor" der — çünkü aranan görev yoktur.
Script yönetici olmadan çalıştırılırsa bunu açıkça söyler ve hiçbir şey yapmaz.

### Script'ler

| Script | Ne yapar | Nasıl zamanlanır |
| --- | --- | --- |
| `backup-restore-drill.ps1` | Tam yedek + geri yükleme tatbikatı + satır doğrulaması, tüm veritabanları için | Günlük (02:00) |
| `backup-log.ps1` | İşlem günlüğü yedeği + saklama temizliği, tüm veritabanları için | **Saatlik** |
| `IKProBackup.psm1` | Ortak keşif/SQL/log mantığı | — |
| `register-backup-task.ps1` | Yukarıdaki iki görevi Görev Zamanlayıcı'ya kaydeder | Bir kez, yönetici olarak |

> **Keşif mantığı neden ortak modülde:** iki script AYNI veritabanı kümesi
> üzerinde çalışmak zorunda. Ayrı ayrı yazılsalardı zamanla ayrışırlardı ve sonuç
> sessiz olurdu — tam yedeği alınan ama günlüğü alınmayan bir müşteri, felaket
> anına kadar fark edilmez.

> **`backup-log.ps1` kırık zinciri kendi onarır.** Log yedeği geçerli bir tam
> yedeğe dayanmadan alınamaz (Msg 4214). Script bunun sebebini tahmin etmeye
> çalışmaz — dener, hata alırsa TABAN tam yedeği alıp bir kez yeniden dener.
> Sebep birden çok olabilir: yeni provizyonlanan kiracı, kurtarma modelinin
> SIMPLE'a alınıp geri döndürülmesi (zincir kırılır ama `msdb`'deki eski tam
> yedek kaydı durur — "tam yedeği var mı?" diye sormak burada YANILTIR), ya da
> veritabanının yedekten geri yüklenmiş olması.

> **Kaç veritabanı?** İki sabit + kiracı sayısı kadar:
> - `IKProPlatform` — kiracı kimliği, yönlendirme dizini, refresh token'lar.
>   Küçüktür ama **onsuz hiç kimse giriş yapamaz**, üstelik hangi kiracının hangi
>   katalogda olduğunu yalnız o bilir. Yedeklenmezse diğer bütün yedekler
>   sahipsiz kalır.
> - `IKPro_{slug}` — **her kiracı için bir tane.** Yedek planı bunları
>   otomatik keşfetmeli (sabit bir liste tutulursa yeni müşteri yedeksiz kalır).
> - `IKProDb` — paylaşılan katalog. Bugün boştur; henüz ayrılmamış kiracılar için
>   duruyor. Boş olsa da plana dahil edilmesi zararsızdır.
>
> **Platform ile kiracı yedekleri BİRBİRİNE BAĞLIDIR.** Bir kiracıyı eski bir
> ana geri yükleyip platformu bugünkü hâlinde bırakırsanız, o kiracıda o tarihten
> sonra oluşturulmuş kullanıcılar dizinde görünür ama katalogda yoktur — giriş
> denemesi kullanıcıyı var olmayan bir hesaba yönlendirir. Geri yükleme sonrası
> `POST /api/tenants/{id}/rebuild-directory` ile dizin, kiracının kendi
> veritabanından yeniden kurulmalıdır.

**Kritik kural:** Yedekler veritabanı sunucusuyla **aynı diskte tutulmaz.**
Üretimde ayrı fiziksel konum (ayrı sunucu / nesne depolama) zorunludur. Aynı
diskteki yedek, disk arızasında veriyle birlikte gider.

## Tatbikat

`scripts/backup-restore-drill.ps1` yedek alır, **ayrı bir veritabanı adına**
(`<ad>_RestoreDrill`) geri yükler, tablo satır sayılarını kaynakla karşılaştırır
ve kopyayı düşürür.

**Veritabanı adı VERMEYİN** — script hepsini platformdan keşfeder (platform +
paylaşılan katalog + her müşteri kataloğu). Tek ad vermek yalnız hedefli bir
tatbikat içindir ve **diğer müşterileri yedeksiz bırakır**.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\backup-restore-drill.ps1 `
  -BackupPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro"
```

> **Tuzak — yedek klasörü izni:** `BackupPath`, sizin değil **SQL Server servis
> hesabının** yazabildiği bir klasör olmalıdır. Kullanıcı `%TEMP%` klasörü
> genelde yazılamaz ve şu hatayı verir:
> `Cannot open backup device ... Operating system error 5 (Erişim engellendi)`.
>
> Güvenli varsayılanı sunucudan sorun:
> ```powershell
> sqlcmd -S localhost -E -h -1 -W -Q "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400));"
> ```

- Çıkış kodu **0** → tatbikat başarılı.
- Çıkış kodu **1** → yedek, geri yükleme veya doğrulama adımlarından biri
  başarısız. **Bu bir olaydır**, sıradaki tatbikata ertelenmez.
- `-KeepRestoredCopy` verilirse tatbikat kopyası elle inceleme için bırakılır.

**Güvenlik:** Script geri yüklemeyi asla kaynak veritabanının üzerine yapmaz;
hedef ad kaynakla aynı olursa çalışmayı reddeder. Mevcut veri hiçbir koşulda
üzerine yazılmaz.

**Sıklık:** En az **3 ayda bir**, ayrıca her şema göçünden (migration) sonra.

**Kayıt:** Her tatbikat aşağıdaki tabloya işlenir. Tarih yoksa tatbikat
yapılmamış sayılır.

| Tarih | Yapan | Sonuç | Süre | Not |
| --- | --- | --- | --- | --- |
| 2026-08-05 | geliştirme | ⬜ ilk koşu | | Script devreye alındı |

## Felaket anı — sıra

1. **Durdur:** Uygulamayı kapat; bozuk durum üzerine yazmayı engelle.
2. **Teşhis et:** Veri kaybı mı, bozulma mı, silme mi? Hangi ana kadar sağlam?
3. **Haber ver:** Etkilenen müşterilere bilgi ver (KVKK açısından da gerekebilir).
4. **Geri yükle:** Son sağlam FULL + sonrasındaki DIFF + LOG zinciri.
5. **Doğrula:** Satır sayıları, son işlem tarihleri, kritik ekranlar açılıyor mu.
6. **Aç:** Uygulamayı devreye al.
7. **Yaz:** Olay sonrası not — kök neden ve tekrarını önleyecek adım.

> **Geri yükleme oturumları geri sarmaz.** Refresh token'lar kiracı
> veritabanında değil **platform** veritabanında durur — yönlendirme için:
> elinde yalnız token dizesi olan sunucu, hangi kiracıya bakacağını satırdaki
> `TenantId`'den öğrenir. Bu yüzden bir müşteriyi düne döndürdüğünüzde
> oturumları düne dönmez; kullanıcıları bir kez yeniden giriş yapar. Bilinçli
> bir karardır (oturum veri değil, durumdur) ama müşteriye bildirilecek
> adımlar listesine yazın.

### Tek kiracıyı dondurma (bakım / kısmi geri yükleme öncesi)

Bütün uygulamayı kapatmak yerine **tek bir müşteriyi** dondurmak gerektiğinde
(o kiracının verisi üzerinde çalışılacak, diğerleri kesintisiz devam etmeli),
platform ucu kullanılır — artık elle SQL'e inmeye gerek yok:

```bash
# Dondur (bakım/geri yükleme başlamadan ÖNCE):
curl -X POST https://<api-host>/api/tenants/<tenantId>/freeze \
  -H "X-Platform-Key: <platform-anahtarı>"

# ... bakım/geri yükleme burada yapılır ...

# Çöz (iş bittiğinde):
curl -X POST https://<api-host>/api/tenants/<tenantId>/unfreeze \
  -H "X-Platform-Key: <platform-anahtarı>"
```

**Faz 1a'da bu adım yalnız yeni girişleri kesiyordu** — elinde geçerli bir
access/refresh token'ı olan kullanıcı çalışmaya ve oturumunu uzatmaya devam
edebiliyordu. Faz 1b ile erişim kapısı login, refresh ve yetkili isteğin
**üçüne de** yerleşti; bu uyarı artık geçerli değil. `freeze` ucu kiracı
kütüğünü (`ITenantRegistry`) **anında** düşürür — bir sonraki istekte üç
yolun üçü de kapanır, TTL dolmasını beklemek gerekmez. Aynı şey `unfreeze`
için de geçerlidir: çözüldüğü an login/refresh/yetkili istek tekrar çalışır.

Uç, `Provisioning`/`Purging` durumundaki bir kiracıyı bilerek reddeder (409) —
bu durumlar kendi yaşam döngüsüne aittir, elle dondurulup çözülmez. Zaten
hedef durumdaki bir kiracıyı tekrar dondurmak/çözmek hataya çarpmaz (idempotent
no-op) — operatör yeniden denerse güvenlidir.

### TEK KİRACIYI geri yükleme (artık gerçek)

Her müşteri kendi kataloğunda (`IKPro_{slug}`) olduğu için, bir müşteriyi
**diğerlerine hiç dokunmadan** ve saniye hassasiyetinde geri döndürebilirsiniz.
Ayrılmanın asıl sebebi buydu.

```bash
# 1) O KİRACIYI dondur — geri yükleme sırasında yazma gelmemeli.
curl -X POST https://<api-host>/api/tenants/<tenantId>/freeze   -H "X-Platform-Key: <platform-anahtarı>"
```

```sql
-- 2) Kuyruk log yedeğini al: son yedekten ŞU ANA kadarki işlemleri kurtarır.
--    Bu adım atlanırsa son yedekten sonraki her şey kaybolur.
BACKUP LOG [IKPro_acme] TO DISK = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro\IKPro_acme_kuyruk.trn'
    WITH NORECOVERY;

-- 3) FULL → DIFF → LOG zinciri. NORECOVERY, zincir bitene kadar veritabanını
--    kullanıma AÇMAZ; son adımda STOPAT ile istenen ana kadar ilerlenir.
RESTORE DATABASE [IKPro_acme]
    FROM DISK = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro\IKPro_acme_FULL.bak'
    WITH NORECOVERY, REPLACE;

RESTORE DATABASE [IKPro_acme]
    FROM DISK = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro\IKPro_acme_DIFF.bak'
    WITH NORECOVERY;

-- 4) Hedef ana kadar log uygula. STOPAT'ı yanlış yazmak sessizce fazla/eksik
--    veri getirir — müşteriyle birlikte kararlaştırın ve buraya not düşün.
RESTORE LOG [IKPro_acme]
    FROM DISK = 'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro\IKPro_acme_kuyruk.trn'
    WITH RECOVERY, STOPAT = '2026-08-13T09:45:00';
```

```bash
# 5) Şema kontrolü: geri yüklenen veritabanı ESKİ bir şemada olabilir.
curl https://<api-host>/api/tenants/schema-status   -H "X-Platform-Key: <platform-anahtarı>"

# Geride ise migration adımını çalıştır (yalnız geride olanlara uygular):
curl -f -X POST https://<api-host>/api/tenants/migrate   -H "X-Platform-Key: <platform-anahtarı>"

# 6) DİZİNİ YENİDEN KUR — atlanırsa kullanıcılar giriş yapamaz (aşağıya bakın).
curl -X POST https://<api-host>/api/tenants/<tenantId>/rebuild-directory   -H "X-Platform-Key: <platform-anahtarı>"

# 7) Çöz.
curl -X POST https://<api-host>/api/tenants/<tenantId>/unfreeze   -H "X-Platform-Key: <platform-anahtarı>"
```

> **6. adım neden zorunlu:** yönlendirme dizini (e-posta → kiracı) PLATFORM
> veritabanındadır ve geri yüklemeyle **geri sarılmaz**. Geri yüklediğiniz ana
> göre:
> - O tarihten SONRA açılan kullanıcılar dizinde vardır ama kiracı kataloğunda
>   yoktur → login onları var olmayan bir hesaba yönlendirir.
> - O tarihten önce silinmiş kullanıcılar katalogda geri gelir ama dizinde
>   yoktur → hiç giriş yapamazlar.
>
> `rebuild-directory` dizini kiracının KENDİ veritabanından yeniden kurar ve iki
> sapmayı da kapatır. Başka kiracıya ait çakışan e-postalar atlanır ve sonuçta
> raporlanır — tek çakışma yüzünden kurtarma yarıda kalmaz.

> **Abonelik geri sarılmaz — ve bu doğrudur.** `Subscriptions` platform
> veritabanındadır. Müşterinin İK verisini düne döndürmek, dün iptal edilmiş bir
> aboneliği canlandırmamalı; plan ve ödeme geçmişi ayrı bir ticari gerçektir.

## KVKK notu

**Yedekler de kişisel veri içerir.** Bu şu sonuçları doğurur:

- Yedekler şifrelenmiş olmalı ve erişim yetkisi sınırlı tutulmalıdır.
- Saklama süresi sonunda **gerçekten imha** edilmelidir.
- Bir kullanıcı silme talebinde bulunduğunda (unutulma hakkı), yedeklerdeki
  kopyanın saklama süresi dolunca kendiliğinden düşeceği kayıt altına alınmalı;
  yedekten seçici silme genelde mümkün değildir.
- Kiracı purge işlemi (`TenantPurger`) canlı veriyi siler; **yedeklerdeki kopya
  saklama süresi boyunca durmaya devam eder.** Aydınlatma metninde belirtilmeli.

## Sorumluluk

⬜ **Atama bekliyor:** Yedeklerin alındığını kimin, hangi sıklıkla kontrol
edeceği ve tatbikatı kimin koşacağı belirlenmelidir. Sahibi olmayan yedek
politikası uygulanmaz.

## Tam kurulum (dört bileşen birlikte)

Tek komut; veritabanı adı verilmez, hepsi keşfedilir:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\backup-restore-drill.ps1 `
  -BackupPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro" `
  -StoragePath "C:\IKPro\App_Data\storage" `
  -OffsitePath "\\yedek-sunucu\ikpro" `
  -LogPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro\tatbikat.jsonl" `
  -AlertWebhookUrl "https://hooks.slack.com/services/..."
```

| Bileşen | Parametre | Ne sağlar |
| --- | --- | --- |
| Evrak dosyaları | `-StoragePath` | **Kiracı başına ayrı zip** üretilir (`{db}-tenant-{id}-{damga}.zip`). Tek müşterinin dosyalarını diğerlerine dokunmadan geri yükleyebilir, müşteri ayrıldığında KVKK gereği **yalnız onun yedeğini imha edebilirsiniz**. **Veritabanı tek başına yetmez:** DB yalnız dosya yollarını tutar, dosyalar diskte durur. |
| Off-site kopya | `-OffsitePath` | Yedek ikinci konuma kopyalanır ve boyut olarak doğrulanır. Aynı diskteki yedek, disk arızasında veriyle birlikte gider. |
| Denetim izi | `-LogPath` | Her koşum JSON satırı olarak eklenir (başarı **ve** başarısızlık). |
| Uyarı | `-AlertWebhookUrl` | Başarısızlıkta POST edilir (Slack/Teams uyumlu). Sessiz başarısızlık en tehlikeli durumdur. |

## Otomatik zamanlama

Tek komut **iki görevi birden** kaydeder: günlük tam yedek + tatbikat, ve
**saatlik işlem günlüğü**. İkincisi isteğe bağlı değildir — RPO 1 saat yalnız
onunla gerçek olur.

```powershell
# 1) Önce ne yapacağını gör (sistemi değiştirmez):
powershell -ExecutionPolicy Bypass -File scripts\register-backup-task.ps1 `
  -BackupPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro" `
  -LogPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro\tatbikat.jsonl" -WhatIf

# 2) YÖNETİCİ PowerShell'de kaydet (-WhatIf'i kaldırın):
powershell -ExecutionPolicy Bypass -File scripts\register-backup-task.ps1 `
  -BackupPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro" `
  -LogPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\IKPro\tatbikat.jsonl" `
  -StoragePath "C:\IKPro\App_Data\storage" `
  -OffsitePath "\\yedek-sunucu\ikpro"
```

Görevleri hemen tetikleyip doğrulayın — **ikisi de 0 dönmelidir:**

```powershell
Start-ScheduledTask -TaskName IKPro-YedekTatbikati
Get-ScheduledTaskInfo -TaskName IKPro-YedekTatbikati | Select LastRunTime, LastTaskResult

Start-ScheduledTask -TaskName IKPro-YedekTatbikati-Log
Get-ScheduledTaskInfo -TaskName IKPro-YedekTatbikati-Log | Select LastRunTime, LastTaskResult
```

`LastTaskResult` **0** = başarılı. Görevler SYSTEM hesabıyla kaydedilir, çünkü SQL
Server yedek dizinine yazma izni kullanıcı hesabında genelde yoktur.

**Görev adları:** günlük görev `IKPro-YedekTatbikati`, saatlik görev
`<aynı ad>-Log`. Tek bir `register` çağrısı ikisini birden kurar; ayrı ayrı
kaydetmeye çalışmayın — script `-Force` kullandığı için aynı adla ikinci bir
çağrı birinciyi sessizce siler.

Kaldırmak için her iki görevi de silin:

```powershell
Unregister-ScheduledTask -TaskName IKPro-YedekTatbikati -Confirm:$false
Unregister-ScheduledTask -TaskName IKPro-YedekTatbikati-Log -Confirm:$false
```

> **Not:** Zamanlanmış görev bu depoda **kaydedilmedi** — sistemde kalıcı
> yapılandırma oluşturduğu ve yönetici hakkı gerektirdiği için bilinçli olarak
> operatöre bırakıldı. Yukarıdaki komut çalıştırılana kadar yedekleme ELLE
> çalışır durumdadır.

## Tatbikat kaydı — 2026-08-13

**İki tatbikat da GERÇEKTEN çalıştırıldı.** Bu belgede yazan ilke gereği: test
edilmemiş yedek, olmayan yedektir.

### 1) Otomatik tatbikat (yedek → geri yükle → satır sayısı doğrula)

```
powershell -File scripts\backup-restore-drill.ps1 -BackupPath <yedek-klasörü> -LogPath <log>
```

| Veritabanı | Tablo | Satır | Sonuç |
| --- | --- | --- | --- |
| `IKProPlatform` | 5 | 22 | ✅ |
| `IKProDb` (paylaşılan) | 36 | 18 | ✅ |
| `IKPro_demo` | 36 | 146 | ✅ |
| `IKPro_globex` | 36 | 42 | ✅ |

Çıkış kodu **0**; 4 veritabanının tamamı yedeklendi, ayrı bir ada geri yüklendi,
satır sayıları kaynakla karşılaştırıldı ve tatbikat kopyaları düşürüldü. Toplam
süre ~4 sn (demo ölçeği).

> `IKProDb`'nin 36 tablosu var ama yalnız 18 satırı: katalog boş, şeması güncel.
> Beklenen durum — kiracı bağlamı olmayan işler oraya bağlanıyor.

### 2) Nokta-zaman geri yükleme (`RESTORE … STOPAT`) — asıl kanıt

Yukarıdaki tatbikat "yedek geri yüklenebiliyor" der; bu belgede vaat edilen ise
**"tek müşteriyi istediğiniz saniyeye döndürebilirsiniz"**. O iddia ayrıca sınandı:

1. `IKPro_demo` FULL yedeği alındı.
2. `TATBIKAT-A` departmanı eklendi → `02:26:07`
3. **kesme noktası** → `02:26:10`
4. `TATBIKAT-B` departmanı eklendi → `02:26:13`
5. Kuyruk log yedeği alındı.
6. `RESTORE … NORECOVERY` + `RESTORE LOG … WITH RECOVERY, STOPAT = '02:26:10'`

**Sonuç:**

```
CANLI  IKPro_demo      : TATBIKAT-A, TATBIKAT-B   (6 departman)
GERİ YÜKLENEN (STOPAT) : TATBIKAT-A               (5 departman)
```

Kesme noktasından **önceki** kayıt geldi, **sonraki** gelmedi. Saniye
hassasiyetinde geri dönüş çalışıyor.

**Tatbikatın gerçek prosedürden tek farkı:** geri yükleme, canlı katalogun
üzerine değil ayrı bir ada (`IKPro_demo_PitrDrill`) yapıldı. Gerçek kurtarmada
hedef canlı katalogdur ve öncesinde `freeze`, sonrasında `rebuild-directory` +
`unfreeze` adımları gelir (yukarıdaki prosedüre bakın). Tatbikat kopyası ve
işaret satırları sonrasında temizlendi.

### Bir sonraki tatbikatta yapılması gerekenler

- Gerçek boyutta veriyle koşturun: demo ölçeğinde 4 saniye süren işlem, RTO
  hedefini sınamaz.
- `-OffsitePath` ile ikinci kopyayı da doğrulayın.
- `freeze` → geri yükle → `rebuild-directory` → `unfreeze` zincirini **uçtan uca**
  deneyin; bu tatbikat yalnız veritabanı katmanını kapsadı.

## Kalan eksikler

- ✅ **RPO/RTO belirlendi (2026-08-13):** RPO 1 saat; RTO tek müşteri 4 saat,
  tüm sistem bir sonraki mesai gününün sonu. Gerekçeler ve teknik ön koşul
  (FULL kurtarma modeli) yukarıda.
- ✅ **Saatlik log yedeği YAZILDI ve KOŞTURULDU (2026-08-13).** `backup-log.ps1`
  tüm veritabanlarının işlem günlüğünü alır, süresi dolan `.trn` dosyalarını
  temizler ve kırık zinciri kendi kendine onarır. `register-backup-task.ps1`
  artık **iki görevi birden** kaydeder (günlük tam yedek + saatlik günlük);
  saatlik olan isteğe bağlı değildir — RPO 1 saat yalnız onunla gerçek olur.
- ⬜ **Zamanlanmış görevler HENÜZ KAYDEDİLMEDİ** — script `-WhatIf` ile denendi
  ama gerçek kayıt yönetici hakkı ve operatör kararı gerektiriyor:
  `powershell -File scripts\register-backup-task.ps1 -BackupPath "<yol>" -LogPath "<yol>\yedek.jsonl"`.
  **Bu yapılmadan RPO/RTO taahhütleri kâğıt üstündedir.**
- ⬜ **Off-site hedefi seçilmedi** — `-OffsitePath` için gerçek bir ağ paylaşımı
  veya nesne depolama belirlenmeli; SYSTEM hesabının o hedefe erişebildiği
  doğrulanmalı.
- ⬜ **Log dosyası kimse tarafından izlenmiyor** — webhook kurulana kadar
  `tatbikat.jsonl` dosyasına düzenli bakılmalı.
- ✅ **Kiracı bazına bölme TAMAMLANDI (2026-08-13).** Her kiracının verisi kendi
  kataloğunda (`IKPro_{slug}`); tek müşteri diğerlerine dokunulmadan geri
  yüklenebiliyor.
- ✅ **Yedek planı artık TÜM katalogları kapsıyor (2026-08-13).**
  `backup-restore-drill.ps1` yedeklenecekleri **platform veritabanından
  keşfediyor**: `IKProPlatform` + `IKProDb` + her `Tenants.DatabaseName`.
  `-Database` artık zorunlu değil; verilirse yalnız o katalog tatbik edilir
  (hedefli tatbikat). Platformda kayıtlı olup sunucuda bulunmayan bir katalog
  tatbikatı **durdurur** — sessizce atlamak "yedeği hiç yokmuş" sürprizine
  dönüşürdü. Zamanlanmış görev de artık ad almadan kaydediliyor.
- ✅ **Tatbikat gerçekten koşturuldu (2026-08-13).** Aşağıya bakın.
- ⬜ **Off-site hedefi ve zamanlanmış görev hâlâ kurulmadı** — script hazır,
  operatör kararı bekliyor (`-OffsitePath`, `register-backup-task.ps1`).
