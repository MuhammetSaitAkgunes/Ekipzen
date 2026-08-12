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

| Hedef | Anlamı | Öneri |
| --- | --- | --- |
| **RPO** (Recovery Point Objective) | En fazla ne kadarlık veri kaybını göze alıyorsunuz? | ≤ 1 saat → saatlik log yedeği gerekir |
| **RTO** (Recovery Time Objective) | Hizmet en geç ne kadar sürede geri gelmeli? | ≤ 4 saat |

> ⬜ **Karar bekliyor:** RPO ve RTO değerleri henüz belirlenmedi. Bunlar
> belirlenmeden yedek sıklığı ve saklama süresi netleşemez.

## Yedek planı

| Tür | Sıklık | Saklama | Not |
| --- | --- | --- | --- |
| Tam (FULL) | Günlük | 30 gün | Gece düşük trafikte |
| Fark (DIFFERENTIAL) | 6 saatte bir | 7 gün | Geri yükleme süresini kısaltır |
| İşlem günlüğü (LOG) | RPO'ya göre (ör. saatlik) | 7 gün | Yalnız FULL recovery model'de |

> **İki veritabanı:** `IKProDb` (kiracı verisi) ve `IKProPlatform` (kiracı
> kimliği ve yönlendirme). İkisi de yedek planına dahildir — platform küçüktür
> ama onsuz hiçbir kullanıcı giriş yapamaz.

**Kritik kural:** Yedekler veritabanı sunucusuyla **aynı diskte tutulmaz.**
Üretimde ayrı fiziksel konum (ayrı sunucu / nesne depolama) zorunludur. Aynı
diskteki yedek, disk arızasında veriyle birlikte gider.

## Tatbikat

`scripts/backup-restore-drill.ps1` yedek alır, **ayrı bir veritabanı adına**
(`<ad>_RestoreDrill`) geri yükler, tablo satır sayılarını kaynakla karşılaştırır
ve kopyayı düşürür. **İki veritabanı da tatbik edilmelidir** — platform küçüktür
ama kiracı kimliğini ve e-posta→kiracı yönlendirmesini tuttuğu için, o olmadan
geri yüklenen kiracı verisine kimse erişemez.

```powershell
# Kiracı verisi
pwsh scripts/backup-restore-drill.ps1 -Database IKProDb -BackupPath C:\yedek

# Platform veritabanı (kiracı kimliği ve yönlendirme)
pwsh scripts/backup-restore-drill.ps1 -Database IKProPlatform -BackupPath C:\yedek
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

Kiracı verisi veritabanı:

```powershell
pwsh scripts/backup-restore-drill.ps1 `
  -Database IKProDb `
  -BackupPath "C:\SQLYedek" `
  -StoragePath "C:\IKPro\App_Data\storage" `
  -OffsitePath "\\yedek-sunucu\ikpro" `
  -LogPath "C:\SQLYedek\tatbikat.jsonl" `
  -AlertWebhookUrl "https://hooks.slack.com/services/..."
```

Platform veritabanı (kiracı kimliği ve yönlendirme — dosya bileşeni yok):

```powershell
pwsh scripts/backup-restore-drill.ps1 `
  -Database IKProPlatform `
  -BackupPath "C:\SQLYedek" `
  -OffsitePath "\\yedek-sunucu\ikpro" `
  -LogPath "C:\SQLYedek\tatbikat.jsonl" `
  -AlertWebhookUrl "https://hooks.slack.com/services/..."
```

**İkisinin de başarılı olması gerekmektedir.** Platform olmadan, geri yüklenen verilere kimse erişemez.

| Bileşen | Parametre | Ne sağlar |
| --- | --- | --- |
| Evrak dosyaları | `-StoragePath` | **Kiracı başına ayrı zip** üretilir (`{db}-tenant-{id}-{damga}.zip`). Tek müşterinin dosyalarını diğerlerine dokunmadan geri yükleyebilir, müşteri ayrıldığında KVKK gereği **yalnız onun yedeğini imha edebilirsiniz**. **Veritabanı tek başına yetmez:** DB yalnız dosya yollarını tutar, dosyalar diskte durur. |
| Off-site kopya | `-OffsitePath` | Yedek ikinci konuma kopyalanır ve boyut olarak doğrulanır. Aynı diskteki yedek, disk arızasında veriyle birlikte gider. |
| Denetim izi | `-LogPath` | Her koşum JSON satırı olarak eklenir (başarı **ve** başarısızlık). |
| Uyarı | `-AlertWebhookUrl` | Başarısızlıkta POST edilir (Slack/Teams uyumlu). Sessiz başarısızlık en tehlikeli durumdur. |

## Otomatik zamanlama

Kiracı verisi veritabanı:

```powershell
# 1) Önce ne yapacağını gör (sistemi değiştirmez):
pwsh scripts/register-backup-task.ps1 -Database IKProDb -BackupPath "C:\SQLYedek" `
  -TaskName "IKPro-YedekTatbikati-Db" -WhatIf

# 2) YÖNETİCİ PowerShell'de kaydet:
pwsh scripts/register-backup-task.ps1 -Database IKProDb -BackupPath "C:\SQLYedek" `
  -TaskName "IKPro-YedekTatbikati-Db" `
  -StoragePath "C:\IKPro\App_Data\storage" -OffsitePath "\\yedek-sunucu\ikpro" `
  -LogPath "C:\SQLYedek\tatbikat.jsonl"
```

Platform veritabanı (kiracı kimliği ve yönlendirme):

```powershell
# 1) Önce ne yapacağını gör (sistemi değiştirmez):
pwsh scripts/register-backup-task.ps1 -Database IKProPlatform -BackupPath "C:\SQLYedek" `
  -TaskName "IKPro-YedekTatbikati-Platform" -WhatIf

# 2) YÖNETİCİ PowerShell'de kaydet:
pwsh scripts/register-backup-task.ps1 -Database IKProPlatform -BackupPath "C:\SQLYedek" `
  -TaskName "IKPro-YedekTatbikati-Platform" `
  -OffsitePath "\\yedek-sunucu\ikpro" -LogPath "C:\SQLYedek\tatbikat.jsonl"
```

Zamanlanmış görevleri hemen tetikleyip doğrula — **her iki görev de başarılı dönmelidir:**

```powershell
# 3) Kiracı verisi zamanlaması:
Start-ScheduledTask -TaskName IKPro-YedekTatbikati-Db
Get-ScheduledTaskInfo -TaskName IKPro-YedekTatbikati-Db | Select LastRunTime, LastTaskResult

# 4) Platform zamanlaması:
Start-ScheduledTask -TaskName IKPro-YedekTatbikati-Platform
Get-ScheduledTaskInfo -TaskName IKPro-YedekTatbikati-Platform | Select LastRunTime, LastTaskResult
```

`LastTaskResult` **0** = başarılı. Görevler SYSTEM hesabıyla kaydedilir, çünkü SQL
Server yedek dizinine yazma izni kullanıcı hesabında genelde yoktur.

**Neden ayrı görev adları:** Script `Register-ScheduledTask ... -Force` ile çağırıyor
(satır ~97). Aynı ad kullanılırsa `-Force`, ikinci çağrı birinci görevi sessizce siler.
Operatör bu adımları izlerse kiracı verisi zamanlaması kaybolur.

Kaldırmak için her iki görevi de silin:

```powershell
Unregister-ScheduledTask -TaskName IKPro-YedekTatbikati-Db -Confirm:$false
Unregister-ScheduledTask -TaskName IKPro-YedekTatbikati-Platform -Confirm:$false
```

> **Not:** Zamanlanmış görev bu depoda **kaydedilmedi** — sistemde kalıcı
> yapılandırma oluşturduğu ve yönetici hakkı gerektirdiği için bilinçli olarak
> operatöre bırakıldı. Yukarıdaki komut çalıştırılana kadar yedekleme ELLE
> çalışır durumdadır.

## Kalan eksikler

- ⬜ **RPO/RTO belirlenmedi** — ticari karar; sözleşmeye yazılmadan önce netleşmeli.
- ⬜ **Zamanlanmış görev kurulmadı** — yukarıdaki komut operatör tarafından
  çalıştırılmalı.
- ⬜ **Off-site hedefi seçilmedi** — `-OffsitePath` için gerçek bir ağ paylaşımı
  veya nesne depolama belirlenmeli; SYSTEM hesabının o hedefe erişebildiği
  doğrulanmalı.
- ⬜ **Log dosyası kimse tarafından izlenmiyor** — webhook kurulana kadar
  `tatbikat.jsonl` dosyasına düzenli bakılmalı.
- 🔄 **Kiracı bazına bölme sürüyor** — Faz 1a tamamlandı: kiracı kimliği
  `IKProPlatform` veritabanına ayrıldı. Kiracı VERİSİ hâlâ tek `IKProDb`
  içindedir; tek müşteriyi geri yükleme yeteneği Faz 2'de gelir.
  Tasarım: `docs/superpowers/specs/2026-08-06-kiraci-basina-veritabani-design.md`
