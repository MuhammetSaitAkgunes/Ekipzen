# İK rehberi — toplu personel aktarımı teknik teslimi

Tarih: 2 Ekim 2026. Bu paket, onaylı plandaki aktarım önizleme ve hata sonucu boşluğunu kapatır. Gerçek uzman kontrolü, içerik yayını veya tüm öğrenme kapsamının tamamlandığı anlamına gelmez.

## Kullanıcıya sunulan davranış

- Excel satır numarası, kişi, unvan, departman, işe giriş ve planlanan işlem birlikte gösterilir. Oluşturulacak, hatalı ve mevcut TC nedeniyle atlanacak satırlar ayrı filtrelenir; 25 satırlık sayfalama vardır. Mobilde tüm alanlar satır kartları halinde görünür.
- Önizleme hiçbir kayıt açmaz. Gerçek aktarım sonucunda oluşturulan personel kimlikleri ve çalışan kartı bağlantıları döner; atlanan satırın nedeni korunur. Önizleme/sonuç raporuna TC, IBAN, telefon ve e-posta değerleri taşınmaz. TC/IBAN/telefon/e-posta hata açıklamaları bu alanların değerlerini tekrar etmez.
- Geçerli satırlar ve aktarım sonucu tek SQL transaction'ında kaydedilir; hatalı/mükerrer satırlar açık kısmi aktarım tercihiyle atlanır. Mevcut çalışan kartları değiştirilmez. Sonuç kaydı başarısızsa personel ve istihdam kayıtları da geri alınır.
- Şablonda sözleşme/çalışma şekli alanı yoktur: aktif, belirsiz süreli ve tam zamanlı kayıt oluşturulur. Önceki pasif aktarma davranışı kaldırıldı; olağan personel oluşturma kuralıyla aynı hale geldi. Diğer sözleşme/çalışma biçimleri tekli kayıt akışından yapılır; geçmiş ayrılış tahmin edilmez.
- Bilinmeyen veya aynı normalize adla birden fazla eşleşen departman için tahmin/otomatik oluşturma yapılmaz. Ad/soyad/unvan, telefon ve e-posta alanlarında veritabanı sınırları önizlemede kontrol edilir. Bozuk dosya, tekrarlı başlık ve çok sayfalı dosya açık sorun olarak dönülür. Tek sayfa, en fazla 1.000 dolu satır ve 5 MB istek sınırı korunur.
- Şablon indirme oturumlu API indirmesiyle yapılır; doğrudan bağlantının Bearer oturumunu taşımama sorunu kapandı.

## API ve tekrar güvenliği

Önizleme, dosyanın SHA-256 parmak izi ile satır sonuçları ve gerçek kayıt modellerine bağlı `previewToken` döner. `POST /api/employees/import` artık multipart `file`, boş olmayan `importRequestId` ve 64 karakterli `expectedPreviewToken` ister. Eski doğrudan dosya gönderimi 400 ile reddedilir. Dosya veya ilgili departman/TC kaynağı değişirse 409 döner; kayıt oluşturulmaz ve kullanıcı yeniden önizler.

`EmployeeImportReceipts` tenant/kullanıcı/gönderim için benzersiz kalıcı sonuç tutar. Tenant kapsamlı `employee-import` kilidi ve serializable transaction, aynı gönderimin eşzamanlı tekrarını güvenli kılar. Kayıtlı sonuç kaynaklar yeniden yorumlanmadan önce bulunur: aynı dosya ve belirteç aynı çalışan kimliklerini döner. Aynı kimlikle başka dosya veya belirteç 409 verir. `GET /api/employees/import/results/{requestId}` yalnız oturum sahibinin kendi şirketindeki sonucunu döner; sonuç yoksa boş yanıt olabilir. API'ler yalnız İK yöneticisine açıktır.

Arayüz belirsiz ağ yanıtında dosya/gönderim kimliğini korur; kayıtlı sonuç sorgulanabilir veya aynı gönderim güvenle tekrar denenebilir. Eksik/tutarsız 200 yanıtı başarı olarak gösterilmez. Sonuç çözülene kadar modal kapatma ve dosya değiştirme durur. Tarayıcı yenilemesi veya sayfadan ayrılma sonrası form/dosyanın otomatik geri yüklenmesi bu paket kapsamında değildir; ekrandaki uyarı formun açık tutulmasını ister. Aynı dosya TC'siz satırlarla yeni bir gönderim olarak açılırsa kişi tekilliği garantisi yoktur. Sonuç, hatalı satırları düzeltip yalnız eksikleri yeni dosyada yüklemeyi açıkça anlatır.

`AddEmployeeImportReceipt` migration'ı yalnız tenant şemasına sonuç tablosu ekler. Makbuz varken Down 51000 ile durur; tekrar koruması sessizce silinemez. Üretimde migration çalıştırılmadı.

## Doğrulama

317 gerçek SQL entegrasyon testi, 99 birim testi ve 91 dosyada 544 frontend testi geçti. Son kullanıcı kapsamı ek kontrolü için üç SQL güvenlik testi; son modal/mobil düzeninden sonra 10 aktarım ekran testi ayrıca geçti. Release derlemesi 0 uyarı/0 hata; frontend üretim derlemesi başarılı. Lint 0 hata ve önceden bulunan dört uyarı. EF model/migration farkı yok; diff kontrolü temiz.

SQL senaryoları aynı TC'siz gönderimin eşzamanlı tekrarını, aynı kimliğe farklı dosya/belirtecin reddini, başka kullanıcı/şirketin sonuca erişememesini, kaynak değişikliğinin uygulamayı durdurmasını, önizlemenin yazmamasını, gerçek Excel satırlarının korunmasını, migration geri alma korumasını ve sonuç yazma hatasında atomik geri almayı doğruladı. Mevcut TC atlama, kısmi aktarım, şablon uyumu ve yönetici rolünün reddi korunur.

Playwright yalnız örnek oturum ve taklit API ile 1440/390 pikselde ekranı doğruladı. Modal dikey kaydırılabilir; mobil belge/modal/tablo yatay taşması yok. Son kontrol onaysız kapalı başlar. Şablon indirme Bearer taşıdı ve beklenen dosya adını kullandı. Bu prova gerçek SQL tarayıcı kabulü, gerçek uzman incelemesi veya deneyimsiz kullanıcı denemesi değildir.

## Kalan kapsam ve sıradaki paket

| İş grubu | Durum / sonraki adım |
| --- | --- |
| Toplu aktarım teknik güvenliği | Bu pakette tamamlandı; bağımsız sürümlü aktarım dersi/kişisel öğrenme kanıtı tamamlandı sayılmaz. |
| Diğer olağan alt işlemler | Sıradaki paket personel kartı düzeltme, departman/yönetici ataması ve özlük belge işlemlerinin mevcut sürümlü derslerle gerçek kanıt bağlantıları ile günlük görev/düzeltme matrisidir. Diğer modüllerin alt işlemleri ayrıca açık kalır. 37 ders tanımı tam işlem kapsamı değildir. |
| Uygulama seviyeleri | Rehberle/destekle/bağımsız uygulama için kanıt kabulü açık; sayısal yeterlilik eşikleri pilot gözleminden sonra belirlenir. |
| Merkezî içerik ve kullanıcı kabulü | Gerçek uzman incelemesi, hazırlayan dışında merkezî editör yayını ve deneyimsiz kullanıcı denemesi bekler. Aktarım makbuzu uzman onayı veya sürümlü rehber kanıtı olarak damgalanmaz. |
| Turhost ilk yayın | Hedef .NET/IIS/SQL/SMTP/kalıcı anahtar-dizinler, kontrollü migration, Visual Studio publish kurulumu ve geri yükleme denemesi açık. |

Tam öğrenme kapsamı tamamlanmadan yayın yapılmaması kararı korunur. Karmaşık telafi/entegrasyon/saklama bağımlılıkları API kuyruğunda ayrıca izlenir. Bu teknik teslim tüm uzman öncesi geliştirmenin bittiği iddiasını taşımaz.

