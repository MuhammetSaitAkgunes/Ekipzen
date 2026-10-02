# T04 — Puantaj, bordro ve ayrılış öğrenme bağlantıları

2 Ekim 2026. T03'ün onaylanan `74bb200`, `08ca6e4`, `b18c516` commitleri `ekipzen` remote'una gönderildi, `main` fast-forward birleştirildi ve gönderildi. Bu paket `b18c516` üzerinden ayrı `codex/ik-rehberi-puantaj-bordro-ayrilis-baglantilari` dalında geliştirilmiştir; kendi Git senkronizasyonu ayrıca onay gerektirir.

## Kapsam ve kişisel öğrenme kanıtı

Sabit 45 satırlık olağan işlem matrisinde A02, B01, B03, B06, B07, O01, O03 ve G03 kapatıldı. Sonuç **36 bağlı işlem, 8 okuma, 1 açık işlem (G02)**. Okuma veya alıştırma gerçek işlem kanıtı değildir.

| İşlem grubu | Yayınlanmış ders kaynağı |
| --- | --- |
| Bordro ayarları ve ayrılış kuralı taslak/onay | `payroll/settings-review` |
| Bordro satırı girdileri ve dönem kontrolü | `payroll/row-review` |
| Ücret telafi dosyası taslak/onay; açık satır ve ilk/sonraki/zincir parasal uygulamalar | `payroll/correction-external` |
| Ayrılış dosyası ve hesap taslağı | `offboarding/preparation`, `settlement-review` |
| Ayrılış parasal düzeltmesini açma/yazma/onay | `offboarding/correction-external` |
| İlk hesap ve revizyon ödeme/bildirim kayıtları | `daily/external-followup` |

İsteğe bağlı `GuidanceContentRevisionId` eski istemcileri korur. Seçilen içerik sürümü doğru süreç/adıma ait, güncel ve yayınlanmış olmalıdır. Gerçek yazım ve kişisel kanıt aynı tenant kilidi/veritabanı işlemi içinde kalır; kanıt yazılamazsa gerçek işlem de geri alınır. Taslak, kontrol, uygulama, onay ve dış kanıt sonuçları öğrenme geçmişinde ayrılır. Yeni yeterlilik veya mevzuat uygunluğu onayı üretilmez.

## Puantaj kaynağı ve bordro incelemesi

Satır girdileri kaydedilirken puantaj, onaylı izin, takvim ve istihdam tarihlerinin kaynak izi tutulur. Daha sonra kaynak değişirse açık/onaysız satır yeniden inceleme gerektirir, onay engellenir ve gerçek kaynaklı günlük görev açılır. Eski form kaynak sürümü nedeniyle 409 alır; girilmiş gün/fazla mesai tutarları otomatik değiştirilmez. Dönem kontrolü bu işi tek başına kapatmaz; kullanıcı girdileri karşılaştırıp yeniden kaydeder.

Eski satırlarda boş inceleme izi geriye dönük yeterlilik sayılmaz. İlk girdi kaydı yeni iz oluşturur. Bu görev açık/onaysız satırlarla sınırlıdır; onaylı dönemleri otomatik yeniden hesaplamaz veya gelişmiş telafi kilitlerini değiştirmez.

## İlk hesabın ayrı dış kanıtları

Tenant kapsamlı ve yalnız aynı şirketin İK yöneticisine açık yeni GET/POST uçları:

- `/api/payroll/periods/{periodId}/rows/{rowId}/external-reconciliation`
- `/api/employees/{employeeId}/offboarding/{caseId}/external-reconciliation`

İlk onaylı dönem/satır veya ilk tamamlanmış ayrılış hesabında ödeme ve bildirim iki ayrı eklemeli kayıt/geçmiş ve günlük görev taşır. Sorumlu atama işi kapatmaz. `completed` gerekli belge/tarih bilgilerini; `not-required` belge referansı ve gerekçeyi ister. En güncel kayıt yalnız kendi türünü kapatır; yeni `pending` kaydı yeniden açar. Kaynak belirteci ve türün son kayıt kimliği eşzamanlı değişiklikleri reddeder. Bankaya veya resmî kuruma gönderim yapılmaz.

Yeni geçerli parasal revizyon ilk hesabın kanıtlarını salt okunur bırakır; mevcut revizyonun ayrı kanıt ve görev akışı kullanılır. Eski onaylı hesaplar, ayrılış tarihi ve pasif personel durumu korunur. Gelişmiş zincir/yıl sınırı kilitleri ve önceki finansal önizleme/onay kuralları değişmez.

## Migration ve sınırlar

`20261002130722_AddFinancialExternalAndTimesheetReview` tenant şemasına yeni dış kanıt tablosunu ve nullable puantaj inceleme izini ekler. Finansal kanıt veya inceleme izi varsa veri kaybına yol açacak `Down` SQL 51000 ile durur. Model kontrolünde bekleyen şema değişikliği yoktur. Üretim migration'ı, yayın ve geri yükleme yapılmadı.

Genel ağ sonucu, tekrar gönderim ve bütün komutların no-op davranışı T06'da incelenecektir; bu paket bütün finansal yazımlara idempotency garantisi vermez. Gerçek uzman incelemesi, ayrı merkezî editör yayını ve deneyimsiz kullanıcı kabulü test verisiyle tamamlanmış sayılmaz.

## Doğrulama

- Backend genel koşum: 349 entegrasyon + 101 birim testi geçti; son sonuç etiketleri ve migration geri alma koruması sonrası T04'ün 5 entegrasyon testi tekrar geçti.
- Yeni kontroller: puantaj kaynak değişikliği/409/onay engeli, ayrı dış kanıt işleri, eşzamanlı kayıt, başka kullanıcı/tenant/rol sınırı, geçmişin korunması, kanıt hatasında atomik geri alma ve migration veri kaybı engeli.
- Frontend genel son koşum: 99 dosya, 589/589 test geçti. İlk koşumdaki yeni panelin yanlış yardım tercihi test verisi düzeltildi; etkilenen 23 test de geçti.
- Backend Release derlemesi sıfır uyarı/hata; frontend TypeScript/Vite derlemesi başarılı. Lint hata vermedi; önceden var olan dört test yardımcısı uyarısı devam ediyor. OpenAPI şeması gerçek API'den üretildi; model/sürüm ve diff kontrolleri yapıldı.

## Kalan sabit iş ve kullanım sınırı

Bizim tarafta **T05 günlük aksiyon bağlantısı ve görev kapanışı, T06 tekrar/yarım işlem/öğrenme seviyeleri, T07 akış doğrulaması ve teknik teslim** kaldı. Kullanıcı T08 Turhost/Visual Studio kurulumunu manuel üstlendi; bu başlık için sağlayıcı bilgisi veya uygulama istenmeyecek, doğrulanmış kurulum sayılmayacak.

Başlangıçta haftalık %21 kalan kullanımdan yaklaşık 14 yüzde puanı geliştirme, 7 puan acil düzeltme rezervi hedeflendi. Paket sonundaki ölçüm %15 kalandır; T05–T07 için yaklaşık 8 puan kalır. Bu hesapta uygulanmış bir kota değildir; tüketim ve başka oturumlar nedeniyle garanti edilemez. Dar dosya okumaları, kısa raporlar ve değişiklik/hata gerektirmedikçe geçen testleri tekrar çalıştırmama uygulanır. Güvenlik/doğrulama veya onaylanan öğrenme kapsamından feragat edilmez.
