# İK rehberi — yeniden işe alma ve ücret alt işlemleri

2 Ekim 2026. Personel kartı/evrak paketi main'e alındıktan sonra dört mevcut güvenli işlem öğrenme yolculuğuna bağlandı. Bu teslim tüm öğrenme kapsamının, uzman incelemesinin veya canlı yayın kabulünün tamamlandığı anlamına gelmez.

## İşlem ve seçilen kaynak

| Gerçek işlem | API | Güncel merkezî kaynak |
| --- | --- | --- |
| Yeni çalışma dönemiyle yeniden işe alma | `POST /api/employees/{id}/rehire` | `personnel / rehire` |
| Yürürlük tarihli ücret paketi ekleme | `POST /api/employees/{id}/compensation-packages` | `personnel-entry / tab-mali` |
| Kullanılmamış paketin tutar/kaynak düzeltmesi | `PUT /api/employees/{id}/compensation-packages/{packageId}` | `personnel-entry / tab-mali` |
| Son paketin güvenli tarih düzeltmesi | `PUT /api/employees/{id}/compensation-packages/{packageId}/effective-date` | `personnel-entry / tab-mali` |

İK yöneticisi dört işlemde rehber sürümünü ayrı ve açık seçer. Genel kart düzeltmesinin kaynak seçimi bu işlemlere otomatik taşınmaz. API gövdelerindeki `guidanceContentRevisionId` isteğe bağlıdır; seçildiğinde mevcut merkezî yayın/süreç/adım/rol kapıları uygulanır. Eski kaynaksız API istemcileri çalışmaya devam eder. Olağan işlemler de denetim geçmişine eklenir, fakat kaynak snapshotı olmadığı için kişisel öğrenme kanıtı sayılmaz.

Mevcut `GuidancePersonnelEvidences` arşivine çalışma dönemi, ücret paketi, ilişkili önceki paket ve önce/sonra sürüm kimlikleri eklenir. Bu kimlikler tarihsel bağlamdır; yeni bir aktif ilişki veya parasal hesap değildir. Değişen alan adları ve varsa gerekçe saklanır. Ücret değerleri öğrenme arşivine tekrar kopyalanmaz; mevcut finansal düzeltme geçmişi kendi eski/yeni değerlerini korur. Şirket içindeki personel geçmişi son 100 olayı gösterir; kişisel yolculuk ve veri dışa aktarımı yalnız işlem sahibinin kaynaklı kanıtlarını okur. Eski kaynak sürümü güncel öğrenme kanıtına aktarılmaz.

Okuma, alıştırma, kaynak seçimi, ön kontrol/önizleme, hatalı kaynak, değişmeyen ücret düzeltmesi, eski sürüm veya reddedilen tekrar kanıt üretmez. İş kaydı ile kanıt aynı transaction içinde yazılır; kanıt yazma hatasında dönem, ücret paketi, düzeltme geçmişi ve erişim eşitleme isteği geri alınır.

## Personel durumu ve işe dönüş

Genel aktif/pasif kısayolu eklenmedi. Pasife alma mevcut ayrılış tamamlama akışındadır; uygun geri alma mevcut ayrılış geri alma akışındadır. İkisinin önceki öğrenme bağlantıları korunur. Yeniden işe alma yalnız pasif kartta, açık çalışma dönemi yokken ve önceki dönemin bitişinden sonraki tarihle mümkündür.

Form, yeni sözleşme türünü varsayılan kabul etmez; kullanıcı seçer. Karar gerekçesi en çok 500 karakterdir ve güncel arayüzde zorunludur; API tarafında rehberli işlemde zorunlu, olağan eski istemcide isteğe bağlıdır. Kullanıcı önce etkileri görür, sonra yeni dönemi başlatır. Kaydedilmemiş kart değişikliği varsa işe dönüş ön kontrolü durur; kart düzeltmesi ayrı tamamlanır veya taslak bırakılır. İşlemden hemen önce kontroller tekrar yapılır.

Başarıda personel aktif olur, yeni çalışma dönemi açılır ve kalıcı erişim eşitleme isteği eklenir. Eski çalışma dönemi, tamamlanmış ayrılış ve eski ücret/izin geçmişi korunur; ücret veya izin bakiyesi yeni döneme otomatik taşınmaz. Erişim kuyruğuna eklemek hesabın eşitlenmiş olduğu anlamına gelmez. Başarı sonrası yeni kart açıkça yeniden yüklenir; kart okunamazsa kaydın gerçekleştiği ve yeniden işlem öncesi kontrol gerektiği gösterilir. Sunucu hatasında işe dönüş taslağı ve seçilmiş kaynak korunur.

Eşzamanlı aynı işe dönüşte biri başarılı, diğeri aktif kart/açık dönem nedeniyle 409 olur; ikinci dönem veya kanıt oluşmaz. Bu koruma tüm komutlar için kalıcı gönderim makbuzu veya genel ağ sonucu kurtarma desteği sayılmaz.

## Ücret işlemleri ve düzeltme

Paket ekleme mevcut kişi/tarih/tutar/kaynak ön kontrolünü kullanır; seçilmiş rehber sürümü yalnız son onayda gönderilir. Gerçek paket ve açık dönem kimliği, yeni sürüm ve varsa sınırı değişen önceki paket kimliği kanıtta korunur. Aynı başlangıç tarihli ikinci kayıt reddedilir. Bu teslim yeni paket eklemenin mevcut tarih aralığı kurallarını genişletmez.

Tutar/kaynak düzeltmesi yalnız kullanılmamış pakette mümkündür. Mevcut bordro/ayrılış ve kapalı dönem kilitleri, uzman telafi akışı, gerekçe ve beklenen sürüm kontrolleri korunur. Form ilk açılan ücret değerlerini ve sürümünü birlikte sabit tutar. Arka plan sorgusu yeni sürümü getirirse eski taslağın öncesi değişmez ve uyarı gösterilir; eski beklenen sürüm sunucuda 409 alır. Hata taslağı ve kaynak seçimini silmez.

Tarih düzeltmesi mevcut salt okunur sunucu önizlemesini kullanır. Yalnız açık dönemin son paketi ve etkisiz güvenli aralık düzeltilebilir; paket/tarih aralığında bordro veya ayrılış varsa kilitlenir. Kaynak, tarih veya gerekçe değişirse önceki önizleme bırakılır. Son kayıtta önizlemedeki paket ve önceki paket sürümleri gönderilir; sunucu aynı sınırları tekrar kontrol eder. Paketin tarihleri ve sürümü güncellenir; önceki/yeni tarih mevcut düzeltme geçmişinde korunur ve yeni işlem kanıtı eklenir.

## Gerçek kayıttan günlük ücret hazırlığı

Mevcut `employee-compensation:{employeeId}` anahtarı korunur; ayrıca bir tamamlandı bayrağı yoktur. İşe giriş tarihi gelecekte olmayan aktif çalışan ve başlamış açık dönem için bugün yürürlükte paket yoksa görev oluşur. Önceki kapanmış dönemin paketi ve ileri tarihli yeni paket bu görevi kapatmaz. Yeniden işe alınmış çalışanda neden metni eski ücretin taşınmadığını açıklar. Bağlantı ilgili kartın Mali Bilgiler adımını açar; kaynak belge/tutar/tarih kontrolü ister.

Bugün geçerli paket kaydedildiğinde görev kalkar. Son paketin tarihi geleceğe düzeltilip bugün için paket kalmazsa görev yeniden açılır; güvenli tarih tekrar bugüne alındığında kalkar. Ücret değişikliği ve işe dönüşten sonra günlük liste, kişisel kanıt ve personel geçmişi yeniden sorgulanır. Görev hukuki son tarih veya ücret kararının doğruluğu değildir; `dueDate` boştur. Pasif kişiye otomatik işe dönüş kararı veya zorunlu yeniden işe alma görevi üretilmez.

## Migration ve doğrulama

Tenant migration: `20261001231122_AddPersonnelEmploymentEvidence`, mevcut tabloya beş nullable alan ekler. Bu alanlarla bağlam taşıyan kanıt varken Down 51000 ile durur; tarihsel kaynak ve sürüm bilgisi sessizce kaybolmaz. API ve frontend birlikte teslim edilir; üretimde migration/publish yapılmadı.

- Üç yeni SQL senaryosu: kaynak/gerekçe kapısı, eşzamanlı işe dönüş, eski dönem/ayrılış/ücret/izin korunması, bugünkü ve ileri tarihli ücret görevleri, eşzamanlı tutar düzeltmesi/no-op, önizlemenin kanıt üretmemesi, sürümlü tarih düzeltmesi/görev yeniden açılması, kişi sınırı, dört işlemde zorla kanıt hatasında transaction geri alma ve migration Down koruması.
- Altı yeni arayüz senaryosu: ayrı kaynak ve son onay gövdeleri, ön kontrolün kayıt yapmaması, işe dönüş hatasında taslağın korunması, kirli kartla işe dönüşün durması, ücret arka plan yenilemesinde eski değer/sürüm korunması, tarih kaynağı değişiminde önizlemenin bırakılması ve dönem/paket/sürüm yolculuğu.
- Tam SQL: 324 başarılı; birim: 101 başarılı; frontend: 555 başarılı / 93 dosya.
- Release .NET derlemesi sıfır uyarı/hata; frontend üretim derlemesi başarılı; lint dört önceden bulunan uyarı, yeni uyarı yok; AppDbContext model/migration eşleşiyor; `git diff --check` başarılı.
- Playwright yerel örnek verisiyle işe dönüş ve ücret ön kontrolü; işe dönüşte 390 × 844 dar ekran provasının sayfa/içerik genişliği 390, yatay taşma yok. Bu uzman yayını veya deneyimsiz kullanıcının gerçek görev kabulü değildir.

## Kalan teslimler

Sıradaki paket toplu aktarımın bağımsız sürümlü dersi ve kişisel kanıt bağlantısıdır; mevcut önizleme/atomik kayıt/kalıcı makbuz korunacak. Sonraki açık olağan alt işlemler gerçek API, kaynak sürümü, günlük sinyal ve düzeltme matrisi üzerinden kapatılacak. Tüm modüllerin tamamlandığı ilan edilmedi.

| Kalan grup | Durum |
| --- | --- |
| Olağan alt işlem/öğrenme/günlük görev ve ağ sonucu matrisi | Kısmi; toplu aktarımın ayrı dersi/kanıtı ve diğer açık modül işlemleri sırada. |
| Gerçek uzman incelemesi ve ayrı merkezî editör yayını | Bekliyor; teknik test kaynakları gerçek yayın veya uzman onayı değildir. |
| Kanıta dayalı uygulama seviyesi, deneyimsiz kullanıcı, erişilebilirlik ve hata/düzeltme kabulü | Bekliyor; pilot eşikleri ve gerçek görev gözlemi gerekir. |
| Turhost Visual Studio publish, hedef ortam, yedek/geri yükleme ve canlı kabulü | Bekliyor; alan adı/runtime/SQL/SMTP/kalıcı dizin/anahtar ve kurulum doğrulanacak. |

Bu dört grup dört küçük adım veya dört gün vaadi değildir. Önceki finansal telafi, dış entegrasyon ve saklama bağımlılıkları kaldırılmadı. Tüm öğrenme kapsamı tamamlanmadan canlı yayın yapılmaması kararı korunur.
