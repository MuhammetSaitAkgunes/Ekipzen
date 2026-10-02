# Ekipzen — şirket ve işe alım hazırlığı öğrenme kanıtı

2 Ekim 2026. Kullanıcının onayladığı aktarım dersi paketi `6a60833`, `417df15`, `e7f93df` kaynak dalından `ekipzen` remote'una gönderildi, `main` fast-forward ilerletildi ve gönderildi. Başlangıçta izlenen dosyalarda kullanıcı değişikliği yoktu; bu nedenle geri alma/temizleme yapılmadı. Önceden bulunan yerel tarayıcı çıktıları korundu.

Bu paket [olağan işlem matrisindeki](2026-10-02-ik-rehberi-olagan-islem-matrisi.md) S02–S04 ve R01/R03 bağlantılarını kapatır. Şirket ve işe alım modüllerinin bütün işlemlerinin tamamlandığını belirtmez.

## Gerçek işlem ve içerik bağları

| İşlem | Mevcut API / operation key | Açıkça seçilen kaynak |
| --- | --- | --- |
| Şirket profilini kaydet/düzelt | PUT `/api/settings/company`, `company-profile-update` | `company/company-profile` |
| Departman ekle | POST `/api/departments`, `department-create` | `company/departments` |
| Departman ad/kod düzelt | PUT `/api/departments/{id}`, `department-update` | `company/departments` |
| Pozisyon aç | POST `/api/positions`, `position-create` | `recruitment/candidate-create` |
| Aday oluştur | POST `/api/candidates`, `candidate-create` | `recruitment/candidate-create` |

Yeni ders/adım sayısı yaratılmaz; mevcut on süreç/37 alt adım içindeki üç taslak, gerçek ekran alanları, gerekçe, örnek, sınır ve düzeltme yollarıyla genişletildi. Pozisyon açma aday kaydı önkoşuludur; iki işlem aynı hazırlık dersine bağlanabilse de ayrı kaynak seçimi ve ayrı gerçek işlem kanıtı taşır. Departman eklemenin seçimi düzeltmeye otomatik taşınmaz. Rehber seçimi ve form ön kontrolü kendi başına kayıt yazmaz.

Kaynaklar mevcut merkezî güncel yayın/uzman inceleme kapısından geçer. Eski, adım dışı, eksik/geçersiz kaynak işlemi reddeder. Katalog yayını kilidi tenant işleminden önce alınır; kaynak snapshotı ve iş sonucu tenant transactionı içinde birlikte yazılır. Kanıtın zorla başarısız olması bütün iş yazımını geri alır. Kaynak seçmeyen eski istemci olağan işlemi sürdürebilir; gerçek öğrenme kanıtı edinmez. İçerik taslakları uzman incelemesi ve ayrı yetkili merkezî editör yayını yapılmadan üretimde yayınlanmaz.

## Kayıt belirteci, eşzamanlı değişiklik ve no-op

Şirket profilinin beş alanından ve departmanın ad/kodundan üretilen SHA-256 `recordToken`, okunmuş değerleri temsil eder. Arayüz ilk belirteci saklar; arka planda veri yenilenince kullanıcı taslağı veya belirteci sessizce güncellenmez. Rehberli düzeltmede `expectedRecordToken` zorunludur (eksik 400); verilen belirteç güncel değerleri karşılamıyorsa 409 döner. Aynı ilk belirteçle yarışan farklı değişikliklerden yalnız biri kabul edilir. Kaynak seçmeyen ve belirteç vermeyen eski istemci için zorunlu sürüm denetimi getirilmez.

Bu belirteç genel artan kayıt sürümü değildir; aynı alan değerlerine dönülmüşse yine aynı belirteç oluşur. Profil logosu ayrı API'dir, profil belirtecine veya bu kanıta dahil edilmez. Alan değerleri değişmeyen kayıt yeni kanıt üretmez. Departman düzeltmesi bağlı çalışanları taşımaz. Kullanıcı hata sonrası taslağı ve seçili kaynağı korur. Şirkette açık “taslağı bırak” işlemi güncel profili yeniden sunucudan alır; okuma başarısızsa taslak korunur.

Pozisyon/aday oluşturma bu paketle kalıcı gönderim makbuzu kazanmaz. Ağ yanıtı belirsizse gerçek pozisyon/aday listesi önce kontrol edilir; kaynak kapısı otomatik tekrar tekilliği değildir. Mevcut kapalı/kadrosuz pozisyon kontrolü ve adayın puansız (`null`) / sıfır puan ayrımı korunur. Oluşturmak değerlendirme, işe alma veya dış iletişim değildir.

## Tarihsel kişisel kanıt ve günlük iş

Yeni tenant `GuidancePreparationEvidences` kaydı kullanıcı, operation key, tarihsel kaynak türü/kimliği, değişen alan adları ve seçilmiş kaynak snapshotını taşır. Şirket/aday kişisel değerleri veya önce/sonra adları ikinci bir kanıt deposuna kopyalanmaz. Kaynak nesneye canlı FK bağlanmaz; nesne silinse bile geçmişte gerçekleşmiş öğrenme işlemi korunur. Bu referans güncel kaydın var olduğunu veya kullanıcının yetkin olduğunu kanıtlamaz. Kaynak içeriği yayından kalkarsa tarihsel kanıt kalır, güncel işaretini kaybeder.

Kişisel `guidance/operations` ve `me/data-export` yalnız oturum sahibinin kanıtını döndürür; tenant filtresi ve mevcut rol yetkileri korunur. Yolculuk işlem türünü, gerçek kayıt kimliğini ve alan adlarını gösterir. Okuma, alıştırma ve gerçek uygulama ayrı kalır; otomatik yeterlilik/onay üretilmez.

Yeni adayın `candidate-review:{id}` günlük görevi mevcut gerçek Yeni durumundan oluşur; bilinmeyen son tarih uydurulmaz. Şirket/departman/pozisyon kaydının her birine yapay görev eklenmez. Profil alanlarının hangi şirket politikasına göre zorunlu olduğu veya sonraki aday aşamalarının iletişim görevleri bu pakette kapanmadı; matriste açıktır.

## Migration ve doğrulama

Tenant migration `20261002074120_AddGuidancePreparationEvidence` yeni tablo/index ekler. Kanıt varsa Down 51000 ile durur; tarihsel veri sessizce silinmez. API, tenant migration ve frontend birlikte teslim edilir; üretim migrationı/publish yapılmadı.

- SQL entegrasyonu: **330 başarılı**, birim: **101 başarılı**.
- Frontend: **564 başarılı / 94 dosya**. Yoğun paralel ilk son çalıştırmada eski `PersonnelModal` testinin 5 saniyelik sınırı doldu; süre/test değişmeden `--maxWorkers=2` ile bütün paket başarılı çalıştı.
- Yeni üç SQL senaryosu: rol/kullanıcı/tenant ve dışa aktarım sınırı, no-op/eksik token/eski kaynak, aynı ilk belirteçle yarışma, gerçek aday görevi, yayından kalkan kaynak, migration Down ve beş iş yazımında zorla kanıt hatasının atomik geri alınması.
- Yeni beş UI senaryosu: son kayıtta doğru kaynak/belirteç, hata sonrası taslak ve seçim, departman kaynaklarının ayrılığı, ön kontrolde yazım olmaması, adayda null puan ve başarısız işlem sonrası formun korunması.
- Release .NET derlemesi sıfır uyarı/hata, frontend üretim derlemesi başarılı; lint dört önceden bulunan uyarı, yeni hata/uyarı yok. AppDbContext model/migration uyumlu.
- Playwright: şirket, departman, pozisyon ve aday kaynak seçimi/açıklaması masaüstü ve 390 × 844 dar ekranda görüntülendi. Dört dar görünümde sayfa genişliği 390; yatay taşma yok. Görseller incelendi; aday kaynak düğmesinin odak erişimi kontrol edildi. Bu kontrol tam erişilebilirlik kabulü değildir. İlk aday provasındaki eksik alıştırma test yanıtı düzeltilip doğru API sözleşmesiyle yeniden çalışıldı; son konsolda uygulama hatası yok.
- `git diff --check` başarılı.

Tarayıcı provası örnek API ve test yayın verisi kullanır; gerçek uzman yayını veya deneyimsiz kullanıcı kabulü değildir. Gerçek veritabanı işlemleri SQL testlerinde doğrulandı. Son ekran/taşma ve Git kontrolünün ayrıntıları senkronizasyon raporunda kaydedilir.

## Sıradaki teslim

T02: pozisyon ve aday düzeltmeleri, ardından not/değerlendirme ekleme ve revizyonlarının doğru kaynak/gerçek sonuç bağları. İşe alımın kalan karar/işe alma bağlantıları aynı başlıkta izlenir. Bütün teknik kalan işler ve dış kararlar tek [T01–T08 matrisinde](2026-10-02-ik-rehberi-olagan-islem-matrisi.md) tutulur; önceki entegrasyon/saklama/telafi bağımlılıklarından feragat edilmez. Tüm öğrenme kapsamı tamamlanmadan yayına çıkmama kararı korunur.
