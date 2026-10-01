# İK rehberi — bordro, uyum ve ayrılış alt adımları

1 Ekim 2026. [Şirket/personel/puantaj tesliminin](2026-10-01-ik-rehberi-sirket-personel-puantaj.md) üzerine bordro için altı, uyum için üç, ayrılış için beş alt adım eklenmiştir. Dokuz süreç grubunda toplam otuz dört adım tanımı ve içerik taslağı vardır. Bunlar bütün alt işlemlerin veya onaylı ürün kapsamının tamamlandığı anlamına gelmez; [tam kapsam yayın listesi](2026-09-30-ik-rehberi-yayina-cikis.md) geçerlidir.

## Adımlar ve ekran bağlantıları

| Süreç / ekran | Adımlar, sırasıyla | Alıştırma rolleri |
| --- | --- | --- |
| Bordro / yönetim ekranı | `settings-review`: ayar kaynak/sürümü; `period-create`: doğru dönem; `row-review`: girdi/hesap; `approval`: satır/dönem; `payslip-review`: geçerli pusula; `correction-external`: onaylı hata ve dış işlem farkı | İK yöneticisi |
| Uyum / belge takip ekranı | `record-review`: gerçek belge ve takip kaydı | İK yöneticisi ve ekip yöneticisi |
| Uyum / belge takip ekranı | `status-review`: doğrulanmış durum; `record-correction`: bilgi/sorumlu düzeltmesi | İK yöneticisi |
| Ayrılış / personelin ayrılış penceresi | `preparation`: dosya; `settlement-review`: parasal taslak; `completion`: tamamlama/dış işlem; `reversal-review`: geri alma kapsamı; `correction-external`: parasal sürüm ve ayrı dış kanıtlar | İK yöneticisi |

Kapalı gruplar soru isteği göndermez; adım ve içerik sürümü açık kullanıcı seçimiyle okunur. Seçili sürümün işlem, gerekçe, örnek, düzeltme ve kaynak açıklamaları mevcut ekran içi bileşenle gösterilir. Yeni yayın açık soruyu veya açıklamayı otomatik değiştirmez. Alıştırmanın kaydı gerçek bordro, belge veya ayrılış işlemi göndermez. Yönetici kendi pusulalarını görmeye devam eder; yönetim bordrosu rehberi önerilmez. Ayrılış grubu gerçek dosya bağlamı yüklendiğinde gösterilir.

Şablonlar merkezî katalogdaki yetkili Ekipzen yayın ekibinin hazırlama formuna metin ve soruyu birlikte taşır. Hazırlamak/kaydetmek yayımlamaz. Başka yetkili yayıncı onayı ve kaydedilmiş uzman incelemesi gerekir; müşteri İK rolü yayın yetkisi sağlamaz. Bu teslimde gerçek uzman kabulü veya içerik yayını yapılmamıştır. Yayımlanmamış alıştırma okur ekranında açıkça belirtilir.

## Öğretilen iş ve düzeltme ayrımları

- Bordroda ayar taslağı ve onaylı sürüm, dönem başlangıç girdisi ve güncel kaynak, satır onayı ve dönem onayı, PDF üretimi ve gerçek teslim/ödeme ayrılır. Onaylı ücret hatası ayrı uzman dosyası ve etki incelemesiyle ele alınır. Desteklenen aynı yıl ilk zincir telafisi atomik sürümler; ikinci düzeltmede sonraki onaylı ay varsa kilit sürer. Her ay/sürümün ödeme ve bildirim kanıtları ayrıdır; eski sonuç/kanıt korunur. Hesap farkı gerçek ödeme değildir.
- Uyumda takip etiketi ve gerçek dosya ayrılır; son tarih alanı kanuni süreyi kendi başına kanıtlamaz. Ekip yöneticisi erişebildiği kaydı inceler ve İK'ya iletir; belgeye erişim yetkisi genişletilmez. İncelemede seçimi günlük kartı kaldırabilir; bu, belgenin hazır olduğu anlamına gelmez. Ad/tarih/risk ve sorumlu ayrı isteklerle kaydedildiğinden kısmi sonuç ayrıca okunur; mevcut sorumlu bu ekrandan boşaltılamaz. Yanlış dosyanın erişimi yalnız durum etiketiyle kesilmez.
- Ayrılışta taslak, parasal hesap, tamamlama, dış bildirim ve erişim kuyruğu farklı sonuçlardır. Onaylı kural taslağı bütün hukuki hakları veya ödeme sonucunu onaylamaz. Geri alma çalışma durumu/dönem/devir etkisini taşır; yalnız parasal hatada kullanılmaz. Tamamlanmış hesabın telafisi personeli pasif, ayrılış tarihini sabit tutan yeni parasal sürümdür. Eski hesap ve kanıtlar korunur; yeni sürümün ödeme/bildirim işleri ayrı kapanır. Sorumlu ataması gerçek işlem sonucu değildir.

Mevcut gerçek iş API'leri ve hesap kuralları değiştirilmemiştir. Ayrıntılı dönem açılış önizlemesi, genel bordro satırında beklenen sürümlü kaydetme/onay, uyum alanları/sorumluda atomik sürümlü düzeltme ve ayrılışın salt-okur sunucu etki önizlemesi [API kuyruğunda](2026-09-25-ik-rehberi-backend-api-eksikleri.md) açık kalır. İçerik bu sınırları açıklar; istemcinin yeniden okumasını tam eşzamanlılık garantisi veya ekran teyidini mevcut olmayan sunucu önizlemesi saymaz.

## Resmî kaynak ve inceleme sınırı

Kaynaklar 1 Ekim 2026 tarihinde kontrol edilmiştir. Nihai uygulanabilirlik, şirket koşulları ve içerik/soru doğruluğu yayın öncesinde bağımsız uzman incelemesine tabidir.

- Ücret pusulasında dönem ve ekleme/kesinti kalemleri için 4857 m.37; özlük kayıtları ve gizlilik için m.75; sözleşme sona ermesinde kullanılmamış yıllık izin ücreti için m.59 kullanılır. Bunlar bütün vergi/SGK, kıdem veya ihbar haklarını tek başına karşılamaz. [ÇSGB — İş Kanunu](https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf).
- Olağan 4/a ayrılış bildiriminin sona erme tarihini izleyen on gün içinde verilmesi ayrı dış işlem olarak açıklanır; kapsam/istisna ve düzeltme koşulları ayrıca incelenir. Uygulama dış bildirge göndermez. [SGK — İşverenin Yükümlülükleri](https://www.sgk.gov.tr/Content/Post/d9d838d8-6585-40f5-bbcc-47bd43c59bb4/Isverenin-Yukumlulukleri-2022-05-15-06-17-29).
- Ayar kontrolü, dönem açma sırası, ayrılış hazırlığı ve geri alma kapıları `good-practice` türünde ürün iş akışı olarak tanımlanır. Diğer derslerde mevzuatın dayanağı ile ürünün onay/sürüm/kanıt davranışı metinde ayrılır. Taslaklarda otomatik hukuki uygunluk, dış ödeme veya bildirim sonucu verilmez.

## Teknik sözleşme ve doğrulama

`GuidancePublishedSteps` on dört yeni adımı tanır. Yönetici yalnız uyum `record-review` adımına ek erişim alır; uyum değişikliği, yönetim bordrosu ve ayrılış alıştırmaları İK rolündedir. Çalışan bu yönetim alıştırmalarına erişemez. Hedef kitleyi genişletmek sunucu işlem rolünü kaldırmaz. Katalog sürümü, kişisel kullanıcı/tenant ilerlemesi, güncel sürüme yanıt, idempotensi, bağımsız yayın incelemesi ve dışa aktarım sözleşmeleri korunur. Yeni tablo, migration veya seed yoktur.

- Frontend genel testleri: 87 dosyada 500/500 başarılı. Üç yeni grubun sırası/lazy isteği/otomatik işlem yapmaması, yönetici sınırı ve üç editoryal taslağın metin+soru hazırlığı doğrulanır. Son karşı örnek metin düzenlemesinden sonra katalog testleri 13/13 ve üretim derlemesi ayrıca geçti.
- SQL Server katalog/kişisel dışa aktarım entegrasyonu: 11/11 başarılı. Adım rol testi önceki on ve yeni on dört adım grubunda gerçek yayın, soru okuma, sürüm okuma, yanıt, kişisel geçmiş, dışa aktarım ve hatalı bağ kontrollerini çalıştırır; başka kişinin ilerlemesi veya gerçek işlem/okuma kanıtı üretilmez.
- Backend birim testleri 91/91; Release `--warnaserror` derlemesi sıfır uyarı/hata. Frontend TypeScript/Vite üretim derlemesi başarılı; lint hata vermedi, dört mevcut test yardımcı/dosya uyarısı sürüyor. `npm audit --audit-level=high`: sıfır zafiyet.

Bu teslimde tarayıcı görsel kabulü, gerçek uzman yayın onayı veya Turhost testi yapılmadı. Sonraki paket günlük yönetim alt adımları ve yardım tercihini ele alacak. Katalog sürümüne bağlı gerçek iş kanıtı, diğer açık olağan işlem kapıları, uzman/yeni kullanıcı kabulü ve Turhost ilk kurulum teslimleri ayrıca tamamlanacaktır.
