# Bordro, ayrılış ve uyarlanan anlatım

1 Ekim 2026. Kullanıcı, güncel adımın kendi doğru alıştırma yanıtı ile kendi gerçek işlem kanıtı birlikte varsa uzun örneğin kapalı başlamasını onayladı. Tüm öğrenme kapsamı bitmeden yayın yapılmaması kararı geçerlidir.

## Gerçek işlem bağlantıları

| İşlem | İçerik adımı | Kanıtın doğduğu nokta |
| --- | --- | --- |
| Bordro dönemi oluşturma | payroll / period-create | Gerçek taslak dönem ve ilk satırlar aynı kayıtta oluşturulunca |
| Bordro satırı onayı | payroll / approval | Eksik girdi kontrolleri geçilip hesap sonucu ve satır onayı kaydedilince |
| Bordro dönem onayı | payroll / approval | Bütün satırlar onaylıyken dönem Approved durumuna geçince |
| Ayrılışı tamamlama | offboarding / completion | Beklenen vaka sürümüyle gerçek ayrılış kaydedilince |
| Ayrılışı geri alma | offboarding / reversal-review | Beklenen vaka sürümü ve gerekçeyle gerçek geri alma kaydedilince |

Komutlar isteğe bağlı guidanceContentRevisionId alır. Güncel yayın, rol, süreç ve adım mevcut merkezî katalog kilidiyle doğrulanır; kanıt gerçek iş değişikliğiyle aynı SaveChanges içinde saklanır. Kaynak seçimi, taslak kaydetme, hesaplama ve başarısız/tekrarlanan onay tek başına bu kanıtı oluşturmaz. Eski istemcilerin kaynak taşımayan istekleri desteklenir; geçmiş işler sonradan damgalanmaz.

Üç ayrı kiracı kanıt tablosu SQL Server çoklu cascade yolu oluşturmaz. Ayrılış kanıtında vaka sürümü korunur; geri alma ilk tamamlanma kanıtını silmez. Kişisel geçmiş ve veri dışa aktarımı aynı okuyucuyu kullanır. Kullanıcı/tenant kapsamı sunucudan gelir. Kanıt, dış ödeme/bildirim veya erişim kuyruğunun bitmesi anlamına gelmez.

Kiracı migration'ı 20261001125205_AddGuidanceFinancialOperationEvidence; kanıt varsa Down 51000 ile durur. Production otomatik migration/seed davranışı değişmedi. Bordro satır onayı için beklenen istemci satır sürümü ve ayrı sunucu önizlemesi bu teslimde eklenmedi.

## Uyarlanan anlatım

Kişisel süreç tercihi detailed, compact veya adaptive olabilir. Mevcut expectedRevision, kullanıcı izolasyonu ve tekrar/çakışma sözleşmesi korunur; yeni migration gerekmez.

Adaptive yalnız güncel seçili içerikte aynı contentRevisionId, processKey ve stepKey için kullanıcının güncel doğru son alıştırma yanıtı ve güncel gerçek işlem kanıtı birlikte başarıyla okununca uzun örneği kapalı başlatır. Kanıtlar yüklenirken, okunamazken, yeniden doğrulanırken veya eski/başka adımdayken örnek açık kalır. Eski içerik sürümü yeni yayına aktarılmaz. Kullanıcı örneği açabilir veya ayrıntılı anlatımı seçebilir. Ön koşul, işlem, gerekçe, dayanak, düzeltme ve uzman yönlendirmeleri görünür kalır. Otomatik kısaltma yeni kayıt, iş tamamlanması veya yeterlilik onayı üretmez.

## Visual Studio klasör yayını

Ekipzen.API projesindeki TurhostFolder profili Release / win-x64 / framework-dependent üretir, npm ci ve frontend derlemesini çalıştırır, frontend dosyalarını wwwroot'a ekler. IIS web.config Production ortamını taşır. Çıktı artifacts/visual-studio/site klasörüdür; yalnız yerel dosya üretir.

Bu profil React ve API'nin aynı site kökünde çalışacağı düzeni paketler; dış yol /api tek kez kullanılır. API'yi ayrıca /api alt uygulamasına koymak için uygun ayrı yol düzenlemesi ve doğrulama gerekir. Kullanıcının son bilgisi Microsoft .NET hosting ve Visual Studio publish yöntemidir; alan adı, kesin paket/runtime ve hesap düzeni henüz doğrulanmadı.

Visual Studio'da Ekipzen.API → Publish → mevcut TurhostFolder klasör profilini seçin. İlk yükleme öncesi yeni/boş çıktı klasörü kullanın; profil önceki klasörü temizlemez. Son kaynak commitinden yeniden paket üretin. Development yapılandırması, App_Data ve demo giriş bilgileri pakete alınmaz. Gerçek sırlar publish profiline veya Git'e konulmaz; mevcut harici Production yapılandırma sözleşmesi uygulanır.

Yerel paket üretimi canlı kurulum, uzman içerik kabulü veya bütün öğrenme kapsamının bitmesi değildir. Kalan teslimler [yayına çıkış belgesinde](2026-09-30-ik-rehberi-yayina-cikis.md), olağan API eksikleri [API kuyruğunda](2026-09-25-ik-rehberi-backend-api-eksikleri.md) izlenir.

## Doğrulama

Frontend 89 dosyada 530 test; backend 91 birim ve rehber/bordro/personel/Production hosting kapsamındaki 95 gerçek SQL entegrasyon testi geçti. Release --warnaserror sıfır uyarı/hata, frontend üretim derlemesi başarılı. Lint hata yok; değiştirilmeyen test/helper dosyalarında dört mevcut uyarı var. EF Release model kontrolü değişiklik yok dedi; idempotent yeni migration SQL'i üretildi. Visual Studio klasör profili başarıyla paket üretti. Gerçek Turhost yüklemesi, uzman içerik kabulü ve tarayıcı görsel/yeni kullanıcı kabulü yapılmadı.
