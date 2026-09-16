# Turhost Windows Profesyonel — pilot yayın ön kontrolü

8 Eylül 2026. Durum: sağlayıcı araştırması ve uyarlama planı; kurulum veya hosting hesabı doğrulaması değildir.

## Kesinleşen kullanıcı tercihleri

- Önceki “mevcut Windows / SQL Server sunucusu” ifadesi yerine Turhost **Windows Profesyonel** kullanılacak. SQL de Turhost'ta olacak.
- Kullanıcı yalnız normal alan adını açacak; arayüz ve API ayrı yayınlanacak. API'nin gizli veya yalnız tarayıcı UI'ından çağrılabilir olması gibi ek bir güvenlik şartı istenmedi.
- API Visual Studio Folder Publish ile hazırlanıp hosting'deki `api` klasörüne yüklenerek uygulama olarak tanımlanmak isteniyor. Çalıştırma DLL üzerinden olacak.
- Gerçek alan adı, hosting panelindeki uygulama/izin düzeni, SQL bağlantıları ve SMTP bilgileri henüz alınmadı. Bunların sır değerleri bu belgeye veya Git'e yazılmayacak.

## Resmî sayfadan görülenler

Profesyonel paket için 2 CPU core, 8 GB RAM, 10 MSSQL veritabanı, veritabanı başına 1000 MB ve toplam 5000 MB SQL kotası listeleniyor. Altyapı tablosunda Windows Server 2022, IIS 10 ve MSSQL 2019 bulunuyor. Bunlar ürün sayfası bilgileridir; hesaba ayrılmış kaynak/uygulama yetkisi ölçümü değildir. “Limitsiz disk” SQL kotasının limitsiz olduğu anlamına gelmez. [Turhost paket sayfası](https://www.turhost.com/hosting/windows-hosting/).

Aynı sayfanın tanıtımında .NET Core 10, karşılaştırma tablosunda ise eski 2.2–6.0 aralığı yazıyor. Bu nedenle **projede kullanılan .NET 9'un bu hesapta kurulu olduğu sayfadan kesinleştirilemez**. Destek ekibinden yazılı teyit gerekir; otomatik major yükseltme veya güvenlik kontrolünü kapatma yapılmaz.

Turhost'un destek belgesi MSSQL oluşturmayı ve kullanıcı atamayı panel üzerinden anlatıyor. Bu, uygulamanın SQL bağlantısına `CREATE DATABASE` yetkisi verildiğini kanıtlamaz. Belge eski Plesk sürümlerine de atıf yaptığı için panel adımlarını güncel hesap için ayrıca teyit etmek gerekir. [Turhost veritabanı yönetimi](https://destek.turhost.com/veritabani-database-yonetimi/).

## Kodla eşleşmesi gereken yayın düzeni

1. **Kullanıcı deneyimi:** React üretim çıktısı normal alan adının kökünde çalışır; API çağrıları arka planda yapılır. API kökünde bir web sayfası olmaması normaldir. Veriyi koruyan sınır URL'nin bilinmemesi değil, kimlik doğrulama ve yetkilendirmedir.
2. **DLL yayın:** Framework-dependent, `UseAppHost=false`, single-file kapalı. Mevcut Windows paket betiği DLL üretir; Visual Studio profili henüz eklenmedi. Yalnız `Ekipzen.API.dll` değil, gerekli bütün publish çıktısı ve `web.config` yüklenir. Hedef mimari/runtime sağlayıcıyla eşleştirilir. [Microsoft yayınlama rehberi](https://learn.microsoft.com/en-us/dotnet/core/deploying/).
3. **API yolu:** Mevcut controller yollarında `api/` öneki, frontend'de `/api` tabanı var. IIS uygulamasını ayrıca `/api` altına koymak, uyarlama olmadan çift önek oluşturur. Hosting uygulama kökü netleşince dışarıya tek `/api/...` sözleşmesi verecek şekilde uyarlanıp test edilmeli. Frontend kök alan adı deneyimi değişmeyecek.
4. **IIS:** Mevcut paket in-process kullanıyor. Alt uygulamanın uygulama havuzu ve üst `web.config` kalıtımı sağlayıcının izinleriyle uyumlu olmalı; mevcut kök-site paketi incelenmeden alt uygulamaya kopyalanmamalı. [Microsoft IIS alt uygulama rehberi](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-9.0#sub-applications).
5. **Veri ve sırlar:** Evrak, log, Data Protection anahtarları, PFX ve dış yapılandırma yayın sırasında silinmeyen dizinlerde kalmalı. **Yalnız API klasörünün dışında olmak yetmez: sır/anahtar/evrak dizinleri alan adının genel web kökünden de erişilemez olmalı.** Paylaşımlı hosting'de ACL ve mutlak yollar sağlayıcıdan alınır. Önceki kılavuzdaki `D:\Ekipzen\...` yolları gerçek Turhost yolu değildir.
6. **SQL:** Mevcut mimari platform + paylaşılan + bir müşteri kataloğu olmak üzere üç katalog kullanır. İsim/kimlik türetme, otomatik oluşturma, `Turkish_CI_AS`, FULL recovery ve TLS gereksinimleri sağlayıcı yetkileriyle eşleştirilmeli. Paket adedi tek başına bu uyumu sağlamaz.

## SQL yetki ayrımı için önerilen yön — henüz uygulanmadı

Günlük uygulama hesabı yalnız gereken veri işlemlerini yapmalı; katalog oluşturma/silme ve şema değişikliği ayrı, kontrollü yönetim adımları olmalı. Panelde önceden hazırlanmış veritabanlarını doğrulayıp kaydeden kurulum seçeneği, Turhost koşulları teyit edilirse uygun adaydır. Aynı sunucuda/isim şemasında ve aynı kullanıcıyla farklı kataloglara erişim varsayımları açık bağlantı eşlemesiyle değerlendirilir; tenant izolasyonu azaltılmaz.

Bu yalnız bağlantı dizesi değişikliği değildir: kurulum, migration, silme/geri dönüş ve yarım kalmış işlemlerin durumları birlikte ele alınmalı. Runtime hesabına kalıcı `sysadmin` verilmez. Yetki yokken kurulum veya silme başarılıymış gibi raporlanmaz. Sağlayıcı cevabı ve ürün sahibi kararı olmadan bu akış yeniden yazılmayacak.

## Turhost desteğine iletilebilecek teknik sorular

Bu metin hazırlanmıştır; destek talebi otomatik gönderilmemiştir:

> Windows Profesyonel paketimde React arayüzü alan adı kökünde, ASP.NET Core API'yi ayrı `/api` uygulamasında yayınlamak istiyorum. Projem .NET 9 hedefliyor; DLL/framework-dependent yayın kullanacağım. Şunları mevcut hesabım için teyit edebilir misiniz?
>
> 1. Kurulu .NET 9 / ASP.NET Core runtime sürümleri ve ANCM V2 desteği nedir? x64 in-process alt uygulama için ayrı uygulama havuzu sağlanıyor mu?
> 2. Üç ayrı MSSQL kataloğu ve kontrollü şema yönetimi için hangi yetkiler veriliyor? Veritabanı oluşturma yalnız panelden mi? Katalog isimlerinde zorunlu önek ve kullanıcı/katalog eşlemesi kısıtı var mı? Günlük veri kullanıcısı ile şema yöneticisini ayırabiliyor muyum?
> 3. SQL'e `Encrypt=True;TrustServerCertificate=False` ile bağlanabileceğim, sertifikası doğrulanan sunucu adı nedir? `Turkish_CI_AS` harmanlaması destekleniyor mu?
> 4. Web kökü dışında kalıcı, uygulamanın erişebildiği evrak/anahtar/log/sır dizinleri sağlanıyor mu? Uygulamaya özel environment değişkeni ve parolalı PFX dosyasını okuma desteği var mı?
> 5. SMTP için doğrulanan TLS bağlantı bilgileri, gönderim sınırları ve uygulama havuzunun idle/recycle politikası nedir?
> 6. SQL ve dosya yedekleri hangi sıklıkla alınıyor, ne kadar saklanıyor, dışarı alınabiliyor mu; ayrı test kataloğuna geri yükleme nasıl yapılıyor? FULL recovery/log yedeği desteği var mı?

Şifre veya özel anahtarın destek yanıtına/Git'e eklenmesi gerekmez. Yedek ürününün varlığı, bizim uygulamamızın geri yüklenebilirliğinin ölçüldüğü anlamına gelmez.

## Çıkış şartı

Sağlayıcı cevapları → yetki/adres tasarımının kararı → Visual Studio publish profili ve gerekiyorsa alt uygulama uyarlaması → yerel test → ayrı onaylı test yayını → gerçek HTTPS/SQL/SMTP/davet/evrak/restart kabulü. P3 bordro girdileri, P4 geri yükleme ve P5 kullanıcı/uzman kabulü ayrıca açıktır. Git senkronizasyon onayı sunucuya müdahale yetkisi değildir.
