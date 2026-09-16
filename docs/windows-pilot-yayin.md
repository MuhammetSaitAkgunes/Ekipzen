# Windows / IIS pilot yayın kılavuzu

8 Eylül 2026 — P2-A yazılım ve paket hazırlığı. **Bu belge yayın yapıldığı veya pilot kabulü verildiği anlamına gelmez.** Hedef: mevcut Windows / SQL Server üzerinde tek büro, yaklaşık 10 personel. Sunucu sürümleri henüz bilinmiyor.

Güncel hedef Turhost Windows Profesyonel olarak netleşti. Bu genel IIS kılavuzunu, paylaşımlı hosting'e doğrudan uygulanacak talimat gibi kullanmayın; hesaba özel ön koşullar ve ayrı API düzeni için önce `docs/turhost-pilot-onkontrol.md` belgesini okuyun.

## 1. Önce envanter ve yetki

Sistem yöneticisi hedef sunucuda `scripts/get-windows-server-inventory.ps1` dosyasını PowerShell ile çalıştırır. Script yalnız Windows, IIS, .NET ve kayıtlı SQL instance sürümlerini okur; yazılım kurmaz, SQL'e bağlanmaz, şifre/personel verisi toplamaz. Çıktı hedefin destek durumuyla karşılaştırılmadan yayın yapılmaz. SQL edition bilgisi tek başına lisans hakkını kanıtlamaz.

Hedef model IIS in-process, x64, framework-dependent ASP.NET Core; React ve `/api` aynı HTTPS origin altında. İlgili runtime'ı içeren **güncel .NET 9 Hosting Bundle** ve ANCM V2 gerekir. .NET 9'un destek sonu 10 Kasım 2026; uzun süreli işletim için .NET 10 geçişi bu tarihten önce ayrıca test edilip karara bağlanmalı. Mevcut projenin SDK/framework sürümü bu pakette yükseltilmedi. [Microsoft destek politikası](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core), [IIS barındırma rehberi](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-9.0).

Pilotun dışarıya açılabilmesi için: alan adı ve HTTPS sertifikası; Windows servis/app-pool hesabı; SQL TLS sertifikası; SMTP hesabı/relay; erişimi sınırlandırılmış kalıcı dizinler; yedek ve geri dönüş sorumlusu gerekir. Bu bilgiler veya gerçek sırlar Git'e yazılmaz.

## 2. Yerelde paket üretimi

PowerShell 7, Node.js 24 LTS ve `backend/global.json` ile uyumlu SDK kullanılır. Node 24 seçimi yerel araç zinciriyle CI'ı eşler; Node 20 artık destek dışıdır. [Node.js sürüm tablosu](https://nodejs.org/en/about/previous-releases).

```powershell
pwsh -File scripts/build-windows-pilot.ps1 -ReleaseName pilot-p2-001
```

Script temiz çalışma ağacı ister; `npm ci`, frontend build ve Release/win-x64 backend publish çalıştırır. Yerel geliştirme provasında `-AllowDirty` açıkça verilebilir; manifest bu durumu işaretler ve böyle bir paket müşteriye yayın paketi diye sunulmaz. Script **test koşmaz**; testler yayın kapısının ayrı şartıdır.

Çıktı: `artifacts/<ReleaseName>/site`, örnek yapılandırma, bu kılavuz ve SHA-256 dosya manifesti. Sürüm klasörü zaten varsa işlem durur; mevcut paket silinmez veya ezilmez. Script IIS, servis, registry, SQL veya GitHub üzerinde değişiklik yapmaz.

- `site/wwwroot`: yalnız frontend statik dosyaları; personel evrakları burada değildir.
- `site/web.config`: SDK'nın ürettiği ANCM yapılandırması; in-process ve Production ortamı sabitlenir.
- `site/appsettings.Production.json`: sırları boş, güvenli ayarları zorunlu kılan şablon.
- Development yapılandırması, `App_Data` ve frontend demo giriş bilgisi pakette bulunursa üretim başarısız sayılır.
- `manifest.json`: kaynak commit, dirty bilgisi, Node sürümü, lockfile hash'i ve site dosyalarının hash'leri. Bu manifest kriptografik imza veya test sertifikası değildir; güvenilen teslim kanalıyla paylaşılmalıdır.

## 3. Sürümlerden ayrı veri ve sırlar

Örnek yerleşim; gerçek disk/hedef sistem yöneticisi tarafından doğrulanır:

```text
D:\Ekipzen\releases\pilot-p2-001\site   uygulama dosyaları, salt okunur
D:\Ekipzen\data\storage                tenant evrak/logo dosyaları
D:\Ekipzen\data\keys                   şifreli Data Protection anahtarları
D:\Ekipzen\data\logs                   dönen uygulama logları
D:\Ekipzen\secrets\                    harici JSON yapılandırma ve PFX
```

App-pool hesabına yalnız yayın klasöründe okuma/çalıştırma; storage, keys ve logs dizinlerinde gereken yazma; sır dosyalarında yalnız okuma verilir. Yönetici/SYSTEM dışında geniş kullanıcı gruplarına erişim verilmez. Dizinler uygulama açılmadan önce oluşturulmalı; junction/symlink ve inherited ACL'ler incelenmelidir. Açılış kapısının yol kontrolü, Windows ACL denetiminin yerine geçmez.

`deployment/windows/configuration.example.json` dosyasını yayın kökünün **dışına**, örneğin `D:\Ekipzen\secrets\ekipzen.production.local.json` konumuna kopyalayıp gerçek değerlerle doldurun. `EKIPZEN_CONFIG_FILE` app-pool ortam değişkeni bu mutlak yolu gösterir. Uygulama bu dosyayı okur; environment/CLI override önceliği korunur. Sırları komut satırı argümanına veya yayın `web.config` dosyasına koymayın. Pilot için ACL-korumalı harici JSON kullanılabilir; bu, şifreli bir secret vault olduğu anlamına gelmez.

Zorunlu kontroller:

- Ayrı, güçlü `Jwt:Secret` ve `Platform:ProvisioningKey`; örnekler reddedilir. Erişim token süresi 1–30 dakika, örnekte 15 dakika.
- `AllowedHosts`: açık host adları; wildcard yok. `App:BaseUrl`: bu hostlardan birine ait HTTPS origin. Davetlerin `localhost` adresine gitmesi önlenir.
- Aynı-origin pilotta `Cors:AllowedOrigins` boş bırakılır. Başka origin ancak açık ihtiyaç varsa tam HTTPS origin olarak eklenir; CORS kimlik doğrulama değildir.
- SQL için `Encrypt=True`, `TrustServerCertificate=False`, `sa` dışı kimlik ve ayrı platform/uygulama katalogları. Sunucu adı TLS sertifikasıyla eşleşmeli. Bağlantı kurulamadığında TLS doğrulaması kapatılarak ilerlenmez.
- `Email:Mode=smtp`, geçerli sunucu/gönderen/port. Outbox üretimde kabul edilmez. SMTP TLS modu mevcut MailKit uygulamasınca StartTLS veya bağlantıda TLS olarak kullanılır.
- Storage/keys ve log yolu yayın klasöründen ayrı, mutlak olmalı. Bu dizinler gerçek sunucuda yedeklemeye dahil edilir.

## 4. Data Protection ve loglar

Özel anahtarlı, geçerli süreli RSA PFX sistem yöneticisi tarafından sağlanır. Bu sertifika TLS sertifikasından bağımsız tutulabilir. PFX ve parolası `DataProtection:CertificatePath` / `CertificatePassword` ile verilir; PFX, JSON sır dosyası ve kalıcı anahtar dizini yayın paketine eklenmez.

Anahtarlar `DataProtection:KeysPath` içinde sertifikayla şifrelenir. Sabit `SetApplicationName("Ekipzen")` sayesinde release dizini değişikliği uygulama kimliğini bozmaz. Başka makineye geri dönüşte **anahtar dosyaları + ilgili PFX + parola** birlikte gerekir. Sertifika döndürme/eskisiyle anahtar çözme ve expiry alarmı ayrıca planlanmalı; çalışan sertifika rastgele değiştirilmemeli. Bu pakette otomatik sertifika rotasyonu yoktur. [Microsoft Data Protection yapılandırması](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-9.0).

Üretim uygulama logu `Logging:FilePath` konumuna yazılır; günlük ve 10 MB eşiğinde döndürülür, 14 dosya saklanır. Bunlar bordro audit kayıtlarının yerine geçmez. IIS request logları ve uygulama loglarının ACL/retention/alarm ayarları hedef sunucuda doğrulanmalı. İstek gövdesi veya token loglama açılmamalı. ANCM `stdoutLogEnabled` varsayılan kapalıdır; yalnız kısa süreli, kontrollü açılış teşhisinde ayrıca ele alınır.

## 5. Bağlantı kurmadan yapılandırma kontrolü

Paketin `site` dizininde ve gerçek uygulama hesabının erişim bağlamıyla:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:DOTNET_ENVIRONMENT = 'Production'
$env:EKIPZEN_CONFIG_FILE = 'D:\Ekipzen\secrets\ekipzen.production.local.json'
dotnet Ekipzen.API.dll --verify-production=true
```

Başarısız doğrulama sıfır olmayan çıkış kodu üretir. Bu komut ayarları, kalıcı dizinlerin varlığını ve PFX'in okunabilirliğini kontrol eder; **SQL/SMTP/IIS'e bağlanmaz, şema oluşturmaz/migrate etmez**. Dizinlere gerçek yazma yetkisi ve hizmet bağlantısı ayrıca test edilir. Komutun başarılı olması e-posta teslimi, veritabanı uygunluğu veya TLS uçtan uca doğrulaması değildir.

## 6. Hedef sunucuda ayrı onaylı yayın

1. İlk olarak mevcut uygulama/dizin ve veritabanı hedefleri, yedekler, geri dönüş adımı ve test yayın alanı belirlenir. Mevcut veritabanının üzerine otomatik işlem yapılmaz.
2. Gerekli platform/paylaşılan/tenant şemaları kontrollü yönetim adımıyla hazırlanır. Production açılışı demo seed veya otomatik migration çalıştırmaz. `/health` bağlantı kontrolü, bütün tenant şemalarının uygun olduğunu tek başına kanıtlamaz.
3. IIS'te ayrı x64 app-pool, No Managed Code, yalnız gereken anonymous HTTP girişi ve uygulama içi JWT kimlik doğrulama düzeni hazırlanır. Site kökü yalnız `site` klasörüdür. HTTPS 443 binding ve açık hostname kullanılır.
4. `EKIPZEN_CONFIG_FILE` sadece ilgili app-pool için tanımlanır. İki environment adı da Production olmalı; global makine ayarları başka uygulamaları etkileyecek şekilde değiştirilmez.
5. HTTP→HTTPS, HSTS, güvenlik başlıkları, `/` frontend, `/api` yetki sınırları, bilinmeyen API için 404 ve farklı host/origin reddi doğrulanır. Pilot topolojisi doğrudan IIS in-process; ek proxy/load balancer için ayrıca güvenilen proxy tasarımı gerekir. Her gelen `X-Forwarded-*` başlığına güvenilmez.
6. SMTP ile gerçek yönetici daveti teslim alınır, bağlantı doğru HTTPS hostunu açar, şifre belirlenir ve giriş yapılır. Sonra evrak yükleme/indirme, servis restart ve tekrar giriş denenir.
7. Önceki release'e dönüş ve ayrı ortamda veri/evrak/anahtar geri yükleme kanıtlanır. Uygulama dosyası rollback'i veritabanı migration'ını kendiliğinden geri almaz.

### SQL yetki ayrımı — açık işletim kapısı

Mevcut tenant provizyon/migration akışı aynı uygulama bağlantısı üzerinden DDL yetkisi kullanıyor. `sa` reddi tek başına en az yetkiyi sağlamaz. İlk kurulum/şema yönetimi için gereken yükseltilmiş yetki ile günlük çalışma yetkisi ayrımı ve platform yönetim uçlarının ağ sınırı **P2-B'de hedef sunucuyla birlikte karara bağlanmalı**. Bu paket bu ayrımı çözülmüş diye raporlamaz; runtime hesabına kalıcı `sysadmin` vererek geçilmez.

## 7. Bu paketin kapatmadığı kapılar

- Sunucu sürümü/destek/lisans uygunluğu, gerçek IIS kurulumu ve SQL/SMTP bağlantı kanıtı.
- Gerçek tarayıcıda CSP/CDN/font, davet, dosya ve oturum akışları; otomatik test host'u IIS değildir.
- SQL yönetim yetkisi ayrımı; bağımlılık güvenlik taraması ve destek süresi içinde .NET yükseltmesi.
- Tarayıcı `localStorage` token saklaması bu pakette değiştirilmedi. CSP riski azaltır fakat HttpOnly oturum tasarımının yerine geçmez.
- SMS 2FA ve haftalık rapor desteklenmediği için UI/API bunları etkinleştirmez; yeni bir 2FA veya rapor servisi eklenmedi.
- 40 saatlik haftaya uygun çalışma/izin/bordro kuralları, uzman mutabakatı ve tam geri yükleme kabulü P3/P4/P5 kapsamındadır.

Senkronizasyon onayı Git işlemlerini kapsar; bu kılavuzdaki sunucu adımlarını otomatik olarak yetkilendirmez.
