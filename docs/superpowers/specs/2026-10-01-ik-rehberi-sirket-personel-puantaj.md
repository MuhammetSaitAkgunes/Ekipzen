# İK rehberi — şirket, personel ve puantaj alt adımları

1 Ekim 2026. [Sürümlü alıştırma sözleşmesinin](2026-10-01-ik-rehberi-surumlu-alistirma.md) üzerine şirket hazırlığı, personel yönetimi ve puantaj için on yeni alt adım eklenmiştir. Böylece altı süreç grubunda toplam yirmi adım tanımı ve içerik hazırlama taslağı vardır. Bu sayı tüm süreçlerin veya bu altı grubun bütün işlemlerinin tamamlandığı anlamına gelmez. [Tam kapsam yayın listesi](2026-09-30-ik-rehberi-yayina-cikis.md) geçerlidir.

## Ekran ve öğretim kapsamı

| Süreç / ekran | Alt adımlar | Alıştırmaya erişen roller |
| --- | --- | --- |
| Şirket hazırlığı / Ayarlar | Başlangıç kayıtları, şirket profili, departmanlar, erişim sorumluları | İK yöneticisi |
| Personel / personel listesi | Doğru çalışanı bulma ve bilgi kontrolü | İK yöneticisi ve ekip yöneticisi |
| Personel / personel listesi | İçe aktarma önizlemesi ve sonuç kontrolü; ayrı çalışma dönemiyle işe dönüş | İK yöneticisi |
| Puantaj / aylık puantaj ekranı | Giriş önizlemesi, mevcut gün düzeltmesi, aylık özet ve bordro etkisi | İK yöneticisi ve ekip yöneticisi |

Yeni rehber grupları adımları sırayla listeler. Grup açılınca yalnız başlıklar görünür; her alıştırma ayrıca açılana kadar soru isteği gönderilmez. Açıkça seçilen katalog sürümünün ön koşul, işlem, gerekçe, ayrıntılı örnek, hata düzeltme, uzman yönlendirmesi ve dayanak bilgileri alıştırmanın içinde gösterilir. Tam içerik ve kaynak bağlantısı da aynı sürüme gider. Açıklama yüklenemezse hata ve yeniden yükleme düğmesi sunulur; kaynak incelenmiş gibi gösterilmez.

Arka planda yeni sürüm yayımlanması seçili soruyu veya açıklamayı değiştirmez. Güncel sürüme geçiş açık kullanıcı seçimidir. Önceki sürüm açıklaması geçmiş olarak okunabilir; güncel işlem yönlendirmesi sayılmaz. Önceden tanımlı işe giriş, aday ve izin alıştırmaları da bu açıklama sunumunu kullanır.

Şablon metin ve soru, merkezî katalogdaki yetkili Ekipzen içerik ekibine hazırlama girdisidir. Kaydetmek yayımlamaz. Hazırlayan/düzenleyen dışında yayın onayı ve kaydedilmiş uzman incelemesi gerekir. Müşteri şirketinin İK rolü bu yetkiyi sağlamaz. Gerçek uzman kabulü ve yayın bu teslimde yapılmamıştır; okur ekranı yayımlanmamış adımı açıkça bildirir ve taslağı onaylı içerik gibi sunmaz.

## Gerçek iş ve hata düzeltme sınırları

- Başlangıç kurulumu eksik temel kayıtları oluşturur ve mevcut kayıtları korur; bordro taslağı veya şablon yılı hukuki onay değildir. Profil e-postasını kaydetmek SMTP teslimatını doğrulamaz. Güvenlik ekranındaki desteklenmeyen SMS doğrulaması etkin koruma gibi anlatılmaz; rehber rol yetkisi vermez.
- Personel incelemesinde doğru kişi ve kaynak bilgi doğrulanır. Ekip yöneticisi bilgi düzeltmesini İK kullanıcısına iletir. Aktarım önizlemesi kayıt veya rezervasyon değildir; hatalı/mükerrer satırlar, bilinmeyen departman, sözleşme koşulları ve gerçek sonuç ayrıca kontrol edilir. Sonucu belirsiz aktarım kontrolsüz tekrar gönderilmez. İşe dönüş ayrı çalışma dönemi açar; eski ayrılış ve hesap geçmişini silmez. Uygulama dönemi hukuki kıdem/izin haklarını kendiliğinden belirlemez ve dış bildirim göndermez.
- Puantajda gerçek saat ve mola kullanılır; örneğin 09.00–18.00 ve 60 dakika mola için 480 dakika çalışma karşılaştırılır. Önizleme kaydı oluşturmaz. Günlük hesap kanuni fazla çalışma değerlendirmesinin tamamı değildir. Düzeltme aynı mevcut satırdan yapılır; sürüm çakışmasında güncel kayıt yeniden incelenir. Aylık özet veya CSV ödeme/bildirim değildir; önceden açılmış bordronun girdileri ayrıca kontrol edilir.

Mevcut gerçek işlem API'leri değiştirilmemiştir. [API kuyruğundaki](2026-09-25-ik-rehberi-backend-api-eksikleri.md) ayrıntılı kurulum farkı, aktarım satır sonucu/idempotensi ve puantaj–bordro uzlaştırma ihtiyaçları açık kalır. Rehber bu ihtiyaçları mevcutmuş gibi çağırmaz veya tamamlandı saymaz.

## Kaynak ve inceleme sınırı

Kaynaklar 1 Ekim 2026 tarihinde resmî sayfalardan kontrol edilmiştir; şirket koşullarına uygulanabilirlik ve metnin nihai doğruluğu yayın öncesinde uzman tarafından incelenmelidir.

- Şirket profili için veri sorumlusu tanımı: 6698 m.3; [KVKK — Veri Sorumlusu Kimdir?](https://www.kvkk.gov.tr/Icerik/2032/Veri-Sorumlusu-Kimdir). Profil alanı ve e-posta teslimat kontrolü ürün uygulamasıdır.
- Departman/personel doğruluğu ve amaçla sınırlılık: 6698 m.4; [KVKK — Genel ilkeler](https://www.kvkk.gov.tr/Icerik/6606/General-Principles-in-Processing-of-Personal-Data). Ekran, aktarım ve düzeltme yöntemi kanuni zorunluluk gibi sunulmaz.
- Erişim hazırlığında güvenlik tedbirleri: 6698 m.12; [KVKK — Veri güvenliğine ilişkin yükümlülükler](https://www.kvkk.gov.tr/Icerik/2040/Veri-Guvenligine-Iliskin-Yukumlulukler). Belirli bir SMS veya ekran çözümüne kanuni onay sonucu verilmez.
- İşe dönüşte sözleşme/çalışma koşulları: 4857 m.8; [ÇSGB — İş Kanunu](https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf). Yeni dönem modeli ürün davranışıdır; hakların değerlendirilmesi ayrıca yapılır.
- Puantajda fazla çalışma, süre ve mola ayrımı: 4857 m.41, 63, 68; [ÇSGB — İş Kanunu soruları](https://csgb.gov.tr/sikca-sorulan-sorular/calisma-genel-mudurlugu/%C4%B1s-kanunu/). Günlük dakika hesabı nihai hukuki hak veya ödeme kararı değildir.
- Şirket başlangıç kurulumu taslağı `good-practice` türündedir; ürün iş akışı kaynak olarak yazılmıştır. Diğer derslerde hukuki dayanak ile ürün kontrollerinin ayrımı metinde belirtilir.

## Teknik sözleşme ve doğrulama

`GuidancePublishedSteps` artık yeni on adımı da tanır. Ekip yöneticisinin erişimi süreç yerine adım düzeyinde sınırlandırılmıştır: izin adımları, personel incelemesi ve üç puantaj adımı. Çalışan bu yönetim alıştırmalarına erişemez. Soru okuma, sürüm okuma ve yanıt kaydetme aynı sunucu kuralını kullanır; içerik hedef kitlesini genişletmek işlem rol sınırını kaldırmaz. Bilinmeyen adım 404, yetkisiz rol 403, hatalı süreç/adım bağı 409 döner.

Merkezî değişmez içerik, kişisel kullanıcı/tenant/sürüm ilerlemesi, doğru yanıtın ilk okumada gizlenmesi, güncel sürüme yazma, idempotent deneme ve dışa aktarım sözleşmeleri korunur. Okuma beyanı, alıştırma ve gerçek işlem kanıtı ayrı kalır. Yeni tablo, migration veya veri seed'i yoktur.

Bu teslimde doğrulananlar:

- Frontend genel testleri: 87 dosyada 493/493 başarılı. Yeni grup testleri sıralamayı, isteğin ancak adım açılınca yapılmasını, otomatik gerçek işlem gönderilmemesini ve rol filtrelerini doğrular. Seçili açıklamanın yeni yayınla değişmemesi mevcut sürüm testine eklenmiştir.
- SQL Server kullanan katalog/dışa aktarım entegrasyonu: 10/10 başarılı. Yeni test on adımda yayın, soru/yanıt, rol sınırı, kişisel geçmiş, dışa aktarım ve yanlış bağ kontrollerini gerçek API üzerinden çalıştırır. Farklı kişinin ilerlemesi devralınmaz; alıştırma çalışan oluşturma veya okuma kanıtı üretmez.
- Backend birim testleri: 91/91 başarılı; Release derleme `--warnaserror` ile sıfır uyarı/hata.
- Frontend üretim derlemesi başarılı; lint hata vermedi, mevcut test yardımcıları uyarıları sürüyor. `npm audit --audit-level=high`: sıfır zafiyet.

Bu testler uzman içerik kabulü, tüm olağan İK işlerinin tamamlanması, kullanıcı kabulü veya Turhost kurulumu değildir. Sonraki içerik paketi bordro, uyum/belgeler ve işten ayrılış alt adımlarını genişletecektir; günlük yönetim, diğer açık alt işlemler, sürümlü gerçek iş kanıtı ve yardım tercihi ayrıca tamamlanacaktır.
