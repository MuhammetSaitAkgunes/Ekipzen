# İK rehberi — teknik teslim ve kabul sahipliği

2 Ekim 2026. Sabit olağan işlem kapsamındaki T01–T07 teknik geliştirmesi tamamlandı: 45 satırın 37’si işlem öğrenmesine bağlı, 8’i okuma/inceleme kapsamındadır. Gerçek uzman ve kullanıcı kabulü teknik testlerin yerine geçmez; bu kontroller ve Turhost/Visual Studio yayını kullanıcıya aittir.

## Teslim dosyalarını tanıma

Son senkronizasyon raporunda kesin kaynak SHA’sı ve yerel paket yolu bulunur. `manifest.json` içindeki `commit`, `sourceExportedFromGit: true`, `dirtyWorkingTree: false` ve dosya SHA-256 değerleri paketin kaynaklarını tanımlar. `originalWorkspaceDirty` kullanıcının çalışma klasöründeki ek dosyaları bildirir; kaynak Git arşivinden alındığında bunlar derlenmez.

`site/` frontend ve .NET API çıktısını birlikte içerir. `wwwroot/index.html` ve derlenmiş varlıklar pakette olmalıdır. Development yapılandırması ve App_Data yayımlanmaz; temel ayarlar gerçek sırlarla doldurulmuş sayılmaz. Windows paketi `win-x64`, .NET 9, framework-dependent üretilir. Hosting hesabı uygunluğu ve gerçek sunucu ayarları kullanıcı tarafından yönetilir; bu dosya ortam kabulü değildir.

Paket üretimi canlıya dosya göndermez, SQL migration veya servis/IIS değişikliği yapmaz. Mevcut bir paket klasörünü ezmez. Üretim verisini kaynak arşivi veya site klasörüne kopyalamayın.

## Aynı kaynaktan yeniden üretme

Depo kökünde, rapordaki tam SHA ve yeni bir sürüm adıyla:

```powershell
pwsh -NoProfile -File scripts/build-windows-pilot.ps1 -ReleaseName yeni-teknik-teslim -SourceRef RAPORDAKI_TAM_COMMIT_SHA
```

Bu seçenek takip edilen kaynakları belirtilen committen ayrı bir arşiv ağacına çıkarır. Yerel değişiklikler, ek kaynak dosyaları, mevcut node_modules ve çalışma zamanı verileri taşınmaz. `-SourceRef` ile `-AllowDirty` birlikte kullanılamaz. Sürümün ve kaynak arşivinin mevcut olması işlemi durdurur. Manifest dosya hash’leri kontrol edilmeden başka paket aynı sürüm diye sunulmaz.

Visual Studio ile publish alınırken derlenmiş frontend’in de taşınması gerekir. API projesinde mevcut `BuildFrontendForPublish=true` hedefi npm ci/build çalıştırıp `wwwroot` dosyalarını publish çıktısına ekler. Sunucuya gönderim ve hesaba özgü ayarlar kullanıcıya aittir.

## Teknik doğrulama özeti

| Katman | Kanıt |
| --- | --- |
| Frontend | Son genel koşumda 104 dosya, 632 test geçti; yönetici/personel bağlantısı, belirsiz sonuç, kişisel sürüm ve oturum sınırları dahil. |
| Backend | 101 birim ve 353 gerçek SQL/HTTP entegrasyon testi geçti; süreç işlemleri, rol/tenant, kaynak/sürüm, düzeltme, katalog ve Production hosting testleri dahil. |
| Veri modeli | AppDbContext ve PlatformDbContext için bekleyen model değişikliği yok. |
| Derleme | Kaynak arşivinden frontend ve Release/win-x64 backend publish; üretim dosyası ve SHA-256 kontrolü. |
| Lint | Hata yok; önceden bulunan 4 test yardımcısı uyarısı ayrı kayıtlı. |
| Gerçek tarayıcı | Ayrı test kataloglarında gerçek giriş, kaynak okumaları, tek gerçek yazım + kayıp yanıt/ikinci gönderim engeli/yenileme/klavye teyidi; çalışan erişim sınırı. 390 px’de on ekranın gövde/ana içerik ölçümü, geniş tabloların kendi kaydırma alanı ve masaüstü/dar ekran görsel kontrolü. |

Tarayıcıda yanıt kaybı senaryosu, gerçek başarılı API yanıtının yalnız tarayıcıya 503 olarak verilmesiyle sınandı. Sunucuda tek kayıt oluştu; ikinci yazım gitmedi; kontrol uyarısının kaldırılması öğrenme kanıtı üretmedi. Test katalogları üretim/gerçek uzman kabulü değildir. Bu kontroller kapsamlı erişilebilirlik veya bütün cihaz/browser sertifikası anlamına gelmez.

## Yayın öncesi kullanıcıya kalanlar

1. Gerçek İK/mevzuat uzmanı süreç içeriklerini, örnekleri ve kaynaklarını doğrular. Uzman inceleme bilgisi gerçek kayda dayanır; otomasyonun test yayınları uzman onayı sayılmaz.
2. İçeriği hazırlayan kişi dışında yetkili merkezî editör yayın onayı verir. Firma yöneticisi diğer firmaların veya merkezî kataloğun içeriğini kendiliğinden değiştiremez.
3. Deneyimsiz kullanıcı gerçek görevleri, neden/dayanak ve hata düzeltmesini insan gözetiminde dener; bağımsız yeterlilik kararı yalnız ziyaret/alıştırma/işlem sayısından çıkarılmaz.
4. Kullanıcı Visual Studio/Turhost yayınını, gerçek ortam ve veri geçişi/geri dönüş kabulünü yürütür.

Bu dört kabul bitmeden yayın tamamlanmış sayılmaz. [Olağan kapsam matrisi](superpowers/specs/2026-10-02-ik-rehberi-olagan-islem-matrisi.md) ve [API bağımlılıkları](superpowers/specs/2026-09-25-ik-rehberi-backend-api-eksikleri.md) gelişmiş finansal kilitleri, tarihsel huni, iletişim/entegrasyon ve saklama kararlarını korur. Bunlar teknik kapanışı sürekli yeni olağan geliştirme başlığına çevirmeden ayrı gelecek işler olarak izlenir; kapsamdan feragat değildir.
