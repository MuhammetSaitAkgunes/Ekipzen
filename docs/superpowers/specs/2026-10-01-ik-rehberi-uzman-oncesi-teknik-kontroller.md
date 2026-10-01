# İK rehberi — uzman incelemesi öncesi teknik kontroller

1 Ekim 2026. Bu teslim onaylı öğrenme kapsamını daraltmaz. Teknik davranışın doğrulanması, içeriğin gerçek uzman tarafından incelenmesi veya bağımsız yeterlilik onayı değildir.

## Kullanıcıya eklenen davranış

- Öğrenme yolculuğunda tüm yetkili süreç/adımlar birlikte açılır. Güncel yayımlanmış içerik için kişisel inceleme beyanı, son sürümlü alıştırma sonucu ve aynı süreç/adım/sürümdeki gerçek işlem kanıtı ayrı gösterilir. Eski sürüm yeniye aktarılmaz; yayımlanmamış içerik açıkça belirtilir. Okuma hatası eksik kanıt sonucu olarak gösterilmez. Liste açılmadan bu ek sorgular yapılmaz.
- Adım açıklaması ayrıca kaçınılacak örnek, terimler, işlem sonucu, sonucu doğrulama ve sonraki adımı gösterir. Uyarlanan anlatım yalnız uzun örneği kapatır; bu alanlar ve hukuki dayanak görünür kalır.
- Günlük liste açık/haftalık aksiyonları, eksik şirket başlangıç kayıtlarını ve taslak bordro ayarlarını da mevcut kayıttan üretir. Aksiyon bağlantısı ilgili sekmeyi açıp kayda odaklanır. Durum değişiminde liste yeniden sorgulanır. Aksiyonun “Bugün” gibi tarih metni otomatik takvim son tarihine dönüştürülmez; taslak ayarın yürürlük tarihi inceleme son tarihi değildir.

## Koşullu gerçek işlem sözleşmeleri

### Bordro satırı

Dönem detayı ve satır cevapları `revision` taşır. `PUT /api/payroll/periods/{periodId}/rows/{rowId}` ve `POST .../approve` artık `expectedRevision` ister; eksik belirteç 400, eski belirteç 409 verir. İstemci eski değeri yazarak eşzamanlı değişikliği silemez.

`GET .../approval-preview` dönem kilidinde gerçek puantaj tamlığı ve önceki onaylı kümülatif matrahı yeniden okur; hesap ve engelleri döndürür. Kayıt, hesap snapshot'ı veya öğrenme kanıtı oluşturmaz. Onay sunucuda aynı hesap dayanaklarını yeniden kontrol eder. Onaylı/kapanmış dönemin korumaları sürer; zaten onaylı satır tekrar onaylanamaz. Dönem gönderimi hâlâ tüm satırların onaylı olmasını denetler; bu teslim dönem açma etki önizlemesi eklemez.

### Uyum belgesi

Belge DTO'su `revision` taşır. Bilgi PUT'u, durum ve sorumlu PATCH'leri `expectedRevision` ister. Belge kilidi altında eski sürüm 409 ile reddedilir. Bilgi PUT'unda `updateOwner: true` ile sorumlu da aynı kayıtta değişir; boş/null sorumlu kaldırılır. `updateOwner: false` mevcut sorumluyu korur. Ortak form yalnız bir PUT gönderir; ad/tarih/risk ve sorumlu için ayrı kısmi başarı yoktur. No-op sürümü veya öğrenme kanıtını artırmaz. Hata halinde girilen değerler korunur ve “Bilgileri düzelt” ile tekrar açılır.

Bu kontrol eski istemciler için sözleşme değişikliğidir. Frontend şeması güncel izole test API'sinden yeniden üretilmiştir; kullanılan revizyon alanları atlanmamalıdır. Mevcut denetim izi korunur; bu teslim uyum belgesine yeni bir eklemeli revizyon arşivi eklemez.

### Şirket başlangıcı

`GET /api/setup/preview` salt okunur: gerçekten eklenecek şirket adını, eksik izin türlerinin kod/isim/bakiye/onay alanlarını, eksik tam gün tatil tarih/isimlerini ve onaysız bordro taslağını gösterir. `POST /api/setup` artık `expectedPreviewToken` ister. Eksik belirteç 400; eklenecek fark değişmişse 409. Başlangıç zaten tamamlanmışsa belirteçli tekrar no-op döner, ikinci kanıt üretmez. Mevcut kayıtlar üzerine yazılmaz; eklenecek şirket adı doğrulanan önizlemeden alınır. Şablon veya taslak gerçek uzman onayı sayılmaz.

## Test verisi yaşam döngüsü

Yeni SQL testleri kendi geçici şirketini oluşturur; bordro testi sıralamaya bağlı demo dönem kullanmaz. Test temizliği yalnız `Ekipzen_test_` önekli, o testin oluşturduğu katalogları hedefler. Katalogla birlikte platform kaydı mevcut purge servisiyle kaldırılır; sonraki toplu migration testine silinmiş veritabanı bırakılmaz. Bu değişiklik yalnız test altyapısındadır.

## Açık kapsam ve uzman kabulü

| Kalan teslim | Durum |
| --- | --- |
| Bordro dönemi açma ve ayrılış tamamlama/geri alma için ayrıntılı etki önizlemeleri | Açık teknik teslim; mevcut son işlem sunucu kontrolleri korunuyor. |
| Çalışan oluşturmanın belirsiz ağ sonucu için istek kimliğiyle tekilleştirilmesi; toplu aktarımın satır bazlı önizleme/sonucu | Açık teknik teslim. |
| Diğer olağan alt işlemlerin sürümlü gerçek iş bağlantıları, eksik bilgi görevleri ve düzeltme matrisi | Açık teknik teslim; 37 adım tanımı bütün işlemlerin uygulandığı anlamına gelmez. |
| Rehberle/destekle/bağımsız uygulama seviyelerinin kanıta dayalı kabulü | Açık; planın öngördüğü sayısal eşikler pilot gözleminden sonra belirlenir. Uyarlanan anlatım yeterlilik onayı vermez. |
| Merkezî kaynak içeriği ve soruların gerçek uzman incelemesi, bağımsız editör yayını | İnsan incelemesi bekler; otomatik yayın yapılmaz. |
| Deneyimsiz kullanıcıyla görev, hata/düzeltme, rol ve erişilebilirlik kabulü | Açık; örnek verili tarayıcı provası bu kabulün yerine geçmez. |
| Turhost hedef ortamı, kontrollü migration, dış yapılandırma ve geri yükleme denemesi | Açık; canlıya dosya gönderilmedi. |

Karmaşık telafi istisnaları, resmî kurum/banka otomatik entegrasyonu ve saklama politikası API kuyruğunda ayrıca izlenir. Tam kapsam yayına çıkış koşulu yürürlüktedir; bu belge bütün uzman öncesi işlerin bittiği iddiası taşımaz.


## Doğrulama

Son kaynakta 311 SQL entegrasyon testi, 91 birim testi ve 90 dosyada 534 frontend testi başarılıdır. Backend Release derlemesi uyarı hata kabul edilerek 0 uyarı/0 hata ile tamamlandı. Frontend üretim derlemesi başarılı; lint 0 hata ve önceden mevcut dört uyarıyla tamamlandı. EF mevcut migration sonrası model farkı olmadığını doğruladı.

Playwright ile yalnız taklit API/örnek oturum verisi kullanılan yerel öğrenme ekranı 1440 ve 390 piksel genişliklerde incelendi; 390 pikselde belge genişliği de 390 idi. Tüm süreç bölümü klavyeden açılıp kapandı. Bu prova gerçek SQL uçtan uca tarayıcı kabulü veya deneyimsiz kullanıcı denemesi değildir; uzman içeriği yayımlanmadı ve Turhost'a paket gönderilmedi.
