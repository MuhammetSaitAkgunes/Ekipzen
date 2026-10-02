# İK rehberi — toplu aktarımın sürümlü dersi ve gerçek işlem kanıtı

2 Ekim 2026. Personel yaşam/ücret paketi main'e alındıktan sonra mevcut `personnel / import-review` adımı ayrıntılı aktarım dersine ve gerçek aktarım makbuzuna bağlandı. Yeni bir süreç veya adım sayısı eklenmedi. Bu teslim teknik kapsamdır; gerçek uzman incelemesi, merkezî yayın ve tüm öğrenme kapsamının kabulü açık kalır.

## Öğrenme ve uygulama

Merkezî yayıncının mevcut `personnel-import-review` taslağı; dosya hazırlığı, şirket/departman/kişi kontrolü, şablonun aktif/belirsiz süreli/tam zamanlı koşulları, 1.000 kişi/5 MB sınırı, satır bazında önizleme, kısmi aktarım, gerçek sonuç ve düzeltme yolunu anlatır. Ayrıntılı örnek 20 satırın 18 geçerli, bir hatalı ve bir mükerrer sonucunu açıklar. Terimler, ön koşul, sonuç doğrulama, sonraki evrak/ücret/günlük işler ve uzman yönlendirmesi vardır. Alıştırma, bağlantı kesilince yeni gönderim açmak yerine kendi kayıtlı sonucunu sorgulamayı ve gerekirse aynı gönderimi kullanmayı sorar.

Mevzuat dayanağı ile ürün mekanikleri ayrıdır. [KVKK'nın resmî genel ilkeler sayfası](https://www.kvkk.gov.tr/Icerik/6606/General-Principles-in-Processing-of-Personal-Data) 2 Ekim 2026'da kontrol edildi; doğruluk/güncellik ve amaçla sınırlılık açıklanır. Önizleme, kısmi aktarım ve makbuz kanuni bildirim veya bütün veri işleme şartlarının sağlandığı anlamına gelmez. Gerçek uzman incelemesi yapılmadı; taslak otomatik yayımlanmaz. Mevcut hazırlayan/yayımlayan ayrılığı ve merkezî Ekipzen yayın yetkisi korunur.

İK yöneticisi son kontrolde güncel ders sürümünü açıkça seçer, açıklamayı ekran içinde veya sürümlü ders/alıştırma bağlantısıyla açar. Kaynak değişince üç teyit yeniden istenir. Yeni dosya önceki önizleme, teyit ve kaynak seçimini bırakır. Seçim veya okuma kayıt oluşturmaz. Ayrıntılı/kısa/uyarlanan anlatımın mevcut kişisel kuralları kullanılmaya devam eder; kaynak seçimi yeterlilik onayı değildir.

## API ve kalıcı kanıt

| Uç | Davranış |
| --- | --- |
| `POST /api/employees/import/preview` | Salt okunur; kaynak damgalamaz ve kanıt üretmez. |
| `POST /api/employees/import` | Multipart `guidanceContentRevisionId` isteğe bağlı; seçilirse güncel yayın, rol, süreç ve adım kontrolü. Mevcut `file`, `importRequestId`, `expectedPreviewToken` korunur. |
| `GET /api/employees/import/results/{requestId}` | Yalnız tenant/oturum sahibinin değişmez sonucu ve ilk ders snapshotı. |
| `GET /api/guidance/me/operations` ve kişisel veri dışa aktarımı | Yalnız sahibinin kaynaklı ve en az bir yeni kişi oluşturmuş aktarımı; her gönderim tek kanıt. |

Mevcut `EmployeeImportReceipts` tablosuna nullable `GuidanceSnapshotJson` eklenir; ayrı kanıt tablosu veya kişi başına öğrenme kaydı oluşturulmaz. Gerçek sonuç `guidanceContent` snapshotını taşır. Kanıtın bağlamı gönderim kimliği, oluşturulan/atlanan sayıları, gerçek kart kimlikleri ve atlanan satır numarası/nedeni/alan sorunlarıdır. Öğrenme görünümüne TC, IBAN veya kişi adları tekrar kopyalanmaz. Olağan kaynaksız makbuz ve sıfır yeni kişi oluşturan sonuç kişisel öğrenme kanıtı sayılmaz; eski makbuz sonradan kaynaklı yapılamaz.

Yeni kayıt güncel merkezî yayın kilidi altında, mevcut tenant aktarım kilidi/transactionıyla uygulanır. Kartlar, çalışma dönemleri, sonuç ve kaynak snapshotı birlikte kalıcılaşır. Makbuz/kanıt kaydı başarısızsa tüm yeni kartlar ve dönemler geri alınır.

## Tekrar ve sonuç kurtarma

Tamamlanan kendi makbuzu varsa yeni yayın geçerliliğinden önce tekrar olarak tanınır. Handler aynı dosya parmak izi, önizleme tokenı ve ilk kaynak kimliğini tekrar doğrular. Aynı gönderim bu üçü değişmeden tekrarlandığında eski sonuç/kart kimlikleri döner; ders sonradan yayından kaldırılmış olsa da tamamlanan işlem yeniden damgalanmaz. Farklı dosya, token veya kaynak 409 alır. Yeni gönderim eski/kaldırılmış kaynakla başlatılamaz.

Arayüz başarılı yanıtın gönderim, satırlar, sayılar, gerçek kart kimlikleri ve seçilmiş ders kimliğini doğrular. Tutarsız yanıtta başarı ilan edilmez. Belirsiz sonuçta dosya, gönderim ve kaynak sabit kalır; önce kendi kayıtlı sonucu sorgulanır veya aynı gönderim tekrar denenir. GET ile kurtarılan sonuç da personel, günlük görev ve kişisel kanıt sorgularını yeniler. Kaynaklı başarı yalnız aktarımın gerçekleştiğini anlatır; bütün personel hazırlığı ve dış bildirimler tamamlanmış sayılmaz.

Mevcut gerçek kayıttan iletişim, evrak ve bugünkü ücret hazırlığı görevleri yeni kartlarda da oluşur. Tek bir aktarım tamamlandı bayrağı eklenmedi. TC'siz kişinin yeni ve farklı gönderimler arasında otomatik tekilliği veya tarayıcı yenilemesi sonrası dosya/form kurtarma bu teslimde uygulanmadı; kullanıcı aynı gönderim ve sonuç kontrolüne yönlendirilir. Hatalı kaydedilmiş kartlar mevcut güvenli kart/ücret/ayrılış düzeltme yollarına gider; yalnız atlanan eksikler yeni dosyada hazırlanır.

## Migration ve doğrulama

Tenant migration `20261002063356_AddEmployeeImportGuidanceSnapshot`, mevcut makbuza tek nullable alan ekler. Kaynak snapshotı taşıyan herhangi bir makbuz varken Down 51000 ile durur; tarihsel kaynak sessizce silinmez. API/frontend/tenant migration birlikte teslim edilir; üretim migrationı veya publish yapılmadı.

Üç yeni SQL senaryosu eşzamanlı tekrar/tek kanıt, kaynak yayından kaldırıldıktan sonraki aynı sonuç, kullanıcı/tenant sınırı, kişisel dışa aktarım, gerçek günlük ücret işleri, hatalı/adım dışı kaynak, olağan eski istemci, sıfır oluşturma, zorla snapshot hatasında atomik geri alma ve Down korumasını kapsar. Dört yeni arayüz senaryosu açık sürüm seçimi/teyitler, belirsiz POST sonrası GET kurtarma/yolculuk yenileme, yeni dosyada seçim sıfırlama ve tutarsız kaynak yanıtını reddetmeyi kapsar.

- Tam SQL entegrasyonu: 327 başarılı; birim: 101 başarılı; frontend: 559 başarılı / 93 dosya.
- Release .NET derlemesi sıfır uyarı/hata; frontend üretim derlemesi başarılı; lint dört önceden bulunan uyarı, yeni uyarı yok; AppDbContext model/migration eşleşiyor; `git diff --check` başarılı.
- Playwright: ders seçimi, açıklama, üç teyit ve gerçek sonuç/kaynak/kart bağlantıları masaüstü ve 390 × 844 dar ekranda prova edildi. Sayfa ve modal genişliği 390; yatay taşma yok. Görüntüler incelendi.

Tarayıcı provasında örnek API yanıtları kullanılır; gerçek Excel okuma ve SQL atomikliği entegrasyon testlerinde sınanır. Örnek yayın bilgisi gerçek uzman kabulü değildir. Deneyimsiz kullanıcının gerçek görev kabulü yapılmadı.

## Kalan teslimler

Sıradaki paket bütün süreçlerin açık olağan işlemlerini API, kaynak sürümü, günlük sinyal ve düzeltme yolu bakımından tek matriste netleştirip ilk açık şirket/işe alım bağlantılarını kapatacaktır. Başka modüllerin alt işlemleri bu teslimle tamamlanmış sayılmaz.

| Kalan grup | Durum |
| --- | --- |
| Olağan işlem/öğrenme/günlük görev ve ağ sonucu matrisi | Kısmi; bağımsız aktarım dersi ve kanıtı teknik olarak tamamlandı, diğer açık alt işlemler sırada. |
| Gerçek uzman incelemesi ve ayrı merkezî editör yayını | Bekliyor. |
| Kanıta dayalı uygulama seviyesi, deneyimsiz kullanıcı ve erişilebilirlik/hata kabulü | Bekliyor; gerçek görev gözlemi ve pilot eşikleri gerekir. |
| Turhost Visual Studio publish, hedef ortam, yedek/geri yükleme ve canlı kabulü | Bekliyor; hesabın alan adı/runtime/SQL/SMTP/kalıcı dizin/anahtar bilgileri doğrulanacak. |

Bu dört grup dört küçük adım veya dört gün vaadi değildir. Önceki finansal telafi/entegrasyon/saklama bağımlılıkları kaldırılmadı; tüm öğrenme kapsamı tamamlanmadan yayın yapılmaması kararı korunur.
