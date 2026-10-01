# İK rehberi — bordro/ayrılış etki önizlemesi ve personel tekrar güvenliği

2 Ekim 2026. Bu teknik teslim öğrenme kapsamından feragat etmez; gerçek uzman veya deneyimsiz kullanıcı kabulü yerine geçmez.

## Bordro başlangıcı

`GET /api/payroll/periods/creation-preview?year=...&month=...` yalnız HrAdmin içindir. Mevcut kronolojik dönem koşullarını ve ay başındaki onaylı parametreyi kullanır. Uygun personeli, işe giriş/ayrılış tarihlerini, istihdam/paket kimliğini, paket sürümünü/kaynağını, ilk ücret ve yan hakları, varsayılan gün sayısını, puantajdan fazla mesaiyi, IBAN/puantaj tamlığını ve önceki matrahı gösterir. Ay başında paket yoksa tutarlar sıfır başlar; bu bir ücret kararı değildir. Kanuni sınıflandırmalar, prim günleri ve istisna kabulleri satırda ayrıca doğrulanır.

Önizleme ile gerçek oluşturma aynı `PayrollPeriodDraftBuilder` fonksiyonunu kullanır. Taslak yalnız bellektedir; GET dönem, satır, sonuç veya rehber kanıtı kaydetmez. `POST /api/payroll/periods` zorunlu `expectedPreviewToken` alır; eksik belirteç 400, etkileyen başlangıç verisi değişmişse 409 döner. Oluşturma kilidi ve serializable işlem içinde kaynak yeniden okunur. Dönem onayı, ödeme veya dış bildirim yapılmaz. Tarih değişimi eski yanıtı geçersiz kılar; hata halinde alanlar korunur, önizleme/teyit kaldırılır.

## Ayrılış ve geri alma

`GET /api/employees/{id}/offboarding/{caseId}/completion-preview` ve `.../reversal-preview` yalnız HrAdmin içindir. Dosya kimliği/sürümü, istihdam ve ücret dönemi etkileri, kayıtlı parasal toplam/kaynak, her çalışan için eski/yeni yönetici, ilgili ekipteki bekleyen izin sayısı, kuyruğa alınacak erişim/rol işlemleri ve engeller döner. Bekleyen izinlere karar verilmez; erişimin tamamlandığı iddia edilmez.

Son `complete` ve `reverse` komutları mevcut `expectedVersion` yanında zorunlu `expectedPreviewToken` ister. Sunucu aynı ayrılış kilidinde kaynakları yeniden okuyup hash ve engelleri kontrol eder; ardından mevcut iş kuralları uygulanır. Hash içinde dosya, istihdam, paket, bekleyen izinler ve organizasyonun yönetici/hesap/durum alanları vardır. Etkilenmeyen organizasyonun kimlik bilgileri istemciye döndürülmez; yalnız hash üretilir. Organizasyon değişikliği muhafazakâr biçimde yeniden inceleme gerektirebilir. GET herhangi bir iş kaydını, erişim kuyruğunu veya kişisel öğrenme kanıtını yazmaz.

Manuel ayrılışta “Taslağı kaydet ve ayrılış etkisini incele” önce görünür biçimde taslağı kaydeder, sonra salt okunur etkiyi alır. Bu aşamada personel aktiftir. Onaylı kural taslağı tekrar yazılmadan incelenir. Son teyit doğrudan incelenen dosya/sürüm/hash ile tamamlar; taslak yeniden kaydedilmez. Geri alma için de ayrı etki incelemesi ve açık teyit gerekir. Engel veya eksik/çelişkili önizleme son işlemi açmaz. Hata halinde yeniden inceleme gerekir.

Mevcut parasal sürüm geçmişi geri almayı kilitler. Geçmiş hesaplar ve tamamlanmış parasal düzeltmeler değiştirilmez. Bu teslim dış ödeme/bildirimi geri almaz veya uzman incelemesini otomatik onaylamaz.

## Personel oluşturma tekrarı

`POST /api/employees` artık boş olmayan GUID `creationRequestId` ister (eksik/boş: 400). Şirket ve oturum sahibi kapsamında bu kimlikle kilit alınır. İstek gövdesinin hash'i, oluşturulan çalışan/istihdam ve ilk kişisel kanıt aynı SQL işleminde saklanır. Aynı kullanıcı/kimlik/gövde tekrarında mevcut çalışan döner; ikinci çalışan, istihdam, kanıt, rol kuyruğu veya yeni personel bildirimi oluşturulmaz. Aynı kimlikle farklı gövde 409 verir. İlk başarısız kayıtta makbuz oluşmaz; aynı kimlikle düzeltilmiş form denenebilir.

`GET /api/employees/creation-results/{requestId}` yalnız aynı şirketin aynı kullanıcısının sonucunu açar; bulunamayan sonuç null/204'tür. Başka İK yöneticisinin makbuzu görünmez. GUID yetkilendirmenin yerine geçmez. İstek hash'i ham personel/sağlık alanları yerine hash olarak saklanır. Mevcut kaynak kanıtının çalışanla cascade yaşam döngüsü korunur.

Form açık kaldığı sürece aynı gönderim kimliği kullanılır. Ağ hatası sonrası alanlar korunur; tekrar veya “Önceki kayıt sonucunu kontrol et” sunulur. Sayfayı kapatıp yeni form açınca kimliğin taşındığı iddia edilmez; belirsiz sonucu önce mevcut formdan çözmek gerekir. Rehber sürümü hâlâ güncel kaynak kapısından geçer; sonradan emekli edilmiş kaynakla tekrar reddedilirse sonuç sorgusu mevcut kaydı bulabilir.

`AddEmployeeCreationRequestSafety` migration'ı eski kanıtlara nullable kimlik/hash alanları ve şirket/kullanıcı/kimlik için filtreli benzersiz indeks ekler. Yeni makbuzlar varken Down 51000 ile durur; kör geri dönüş tekrar korumasını silemez. Mevcut şema yönetiminde platform/tenant ayrımı ve kontrollü migration koşulları sürer; üretime migration çalıştırılmadı.

## Doğrulama ve kalan kapsam

314 gerçek SQL entegrasyon testi, 91 birim testi ve 91 dosyada 542 frontend testi geçti. SQL senaryoları eşzamanlı aynı gönderim, farklı gövde, kullanıcı kapsamı, tek kanıt/istihdam, geri alma migration koruması, önizlemenin yazmaması, değişen bordro uygunluğu, değişen ayrılış/geri alma kaynağı ve mevcut finansal süreçleri kapsar. Release derlemesi 0 uyarı/0 hata; frontend üretim derlemesi başarılı. Lint 0 hata ve önceki dört uyarı. EF model/migration farkı yok.

Playwright provası yalnız taklit API/örnek oturumla 1440 ve 390 pikselde bordro açma ve ayrılış etki ekranlarını doğruladı. Mobil belge genişliği 390; modal içeriğinde yatay taşma yok. Son onay teyitsiz kapalıydı. Bu prova gerçek SQL tarayıcı kabulü, uzman incelemesi veya yeni kullanıcı denemesi değildir.

| Kalan iş grubu | Durum / sonraki kapsam |
| --- | --- |
| Toplu personel aktarımı | Satır bazında önizleme, hata ve gerçek sonuç; sıradaki teknik paket. |
| Diğer olağan alt işlemler | Sürümlü gerçek işlem bağlantıları, eksik bilgi günlük görevleri ve düzeltme matrisi açık. 37 ders tanımı tüm alt işlemlerin bittiğini göstermez. |
| Uygulama seviyeleri | Rehberle/destekle/bağımsız uygulama için kanıta dayalı kabul açık; sayısal eşikler plan gereği pilot gözleminden sonra belirlenir. |
| Merkezî içerik ve kullanıcı kabulü | Gerçek uzman incelemesi, hazırlayan dışında merkezî editör yayını ve deneyimsiz kullanıcı denemesi bekler. |
| Turhost ilk yayın | Hedef .NET/IIS/SQL/SMTP/kalıcı anahtar-dizinler, kontrollü migration, son paketin kurulumu ve geri yükleme denemesi açık. Visual Studio publish hazırlığı hedef ortam doğrulaması sayılmaz. |

Tam öğrenme kapsamı tamamlanmadan yayın yapılmaması kararı yürürlüktedir. Karmaşık telafi/entegrasyon/saklama bağımlılıkları API kuyruğunda ayrıca izlenir. Bu teslim tüm uzman öncesi geliştirmenin bittiği iddiası taşımaz.
