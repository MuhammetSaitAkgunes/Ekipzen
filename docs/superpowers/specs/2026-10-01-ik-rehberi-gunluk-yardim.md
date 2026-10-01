# İK rehberi — günlük yönetim ve kişisel anlatım tercihi

1 Ekim 2026. [Bordro/uyum/ayrılış tesliminin](2026-10-01-ik-rehberi-bordro-uyum-ayrilis.md) üzerine günlük yönetimde üç alt adım ve sunucuda saklanan kişisel anlatım tercihi eklenmiştir. On süreç grubunda toplam otuz yedi adım tanımı ve taslağı vardır. Bu, her grubun bütün işlemlerini veya [tam kapsam yayın listesini](2026-09-30-ik-rehberi-yayina-cikis.md) tamamlamaz.

## Günlük yönetim öğretimi

| Adım | Ne öğretilir? | Alıştırma rolü |
| --- | --- | --- |
| `task-review` | Kartın nedeni, gerçek kayıt bağlantısı, desteklenen kapsam ve güvenilir tarih ayrımı | İK yöneticisi ve ekip yöneticisi |
| `task-action` | Gerçek kayıtta işlem, sunucu sonucu ve listeyi yeniden kontrol etme; belirsiz isteği kör tekrar göndermeme | İK yöneticisi ve ekip yöneticisi |
| `external-followup` | Parasal sürümün ödeme/bildirim işlerinde ayrı sorumlu, kanıt ve sonuç; yeni sürümde yeniden açılma | İK yöneticisi |

Günlük işler bileşenine sıralı kaynaklı adım grubu eklenmiştir. Kapalı grup soru isteği göndermez; alıştırma gerçek kaydı değiştirmez. Boş liste kapsanan kaynaklarda bekleyen iş görünmediğini söyler; tüm İK veya yasal yükümlülüklerin bittiği sonucu verilmez. Tarih notu karar son tarihi değildir. Gerçek görev kaynaktan üretilmeye devam eder; yeni görev tablosu veya elle tamamlandı bayrağı eklenmemiştir. Yönetici yalnız mevcut yetkili izin görevleri üzerinde çalışır; yeni öğretim rol sınırını genişletmez.

Bu üç taslak `good-practice` türündedir; dayanak Ekipzen'in kaynak durum ve kanıt takip sözleşmesidir. Günlük kart yeni bir kanuni süre yaratmaz; ilgili işin mevzuat/şirket politikası dayanağı ayrı süreç içeriğinde doğrulanır. Mevcut katalogdaki bağımsız Ekipzen yayıncı ve uzman incelemesi bu soru/metinler için de gerekir. Taslaklar otomatik yayımlanmamıştır.

## Yardım tercihi ve ekran davranışı

Tercih kullanıcı, tenant ve süreç bazındadır. İlk durumda `detailed` (Ayrıntılı anlatım) uygulanır. `compact` (Kısa anlatım), kaynaklı adımın uzun örneğini açılabilir bölüm yapar; örnek her zaman tekrar açılabilir. Ön koşul, gerçek işlem, gerekçe, dayanak/uygulanabilirlik, düzeltme ve uzman yönlendirmesi görünür kalır. Tam katalog metni ve bağımsız işlem onayları daraltılmaz. Soru veya içerik sürümü değişmez.

Öğrenme yolculuğundaki kapalı “Anlatım tercihimi düzenle” bölümü açılınca kendi tercihler okunur; süreç seçimi gerçek rolün erişebildiği ekranlarla sınırlıdır. Seçili kaynaklı adımda aynı süreç tercihi doğrudan düzenlenebilir. Kayıt sunucudan başarılı dönmeden yeni mod uygulanmaz. GET hata/yüklemede ayrıntılı anlatım korunur. Çakışmada eski tercih ezilmez; kullanıcı güncel tercihi yeniden okuyup seçer. Ağ sonucu belirsizse aynı beklenen sürüm ve modla yeniden gönderim, hemen önce başarıyla uygulanmış aynı değişikliği tekrar saymaz.

Bu manuel sunum tercihidir; beceri seviyesi, yardım ihtiyacının kanıta dayalı otomatik azalması, alıştırma başarısı veya gerçek işlem/yeterlilik kanıtı değildir. Bu sonraki öğrenme sözleşmeleri açık kalır.

## API ve kişisel veri sözleşmesi

- `GET /api/guidance/help-preferences`: Management yetkisindeki oturum sahibinin kayıtlı tercihlerini `{ processKey, mode, revision }` olarak döndürür. Kaydı olmayan süreç istemcide ayrıntılı varsayılır; varsayılan okuma kayıt oluşturmaz.
- `PUT /api/guidance/help-preferences/{processKey}`: `{ mode, expectedRevision }` alır. Süreç katalogdaki on anahtardan biri; mod `detailed`, `compact` veya son teslimle eklenen `adaptive`; beklenen sürüm zorunlu ve başlangıçta sıfırdır. Kullanıcı/tenant kimliği istemciden alınmaz. Yönetici bir sunum tercihi kaydederek başka sürecin gerçek işlem yetkisini kazanmaz; çalışan bu yönetim tercihi API'sine erişemez.
- Kullanıcı/süreç kilidinde mevcut sürüm yeniden okunur. Değişen mod sürümü bir artırır. Aynı mevcut mod sürümü artırmaz. Mevcut sürüm beklenenden bir fazla ve mod aynıysa önceki isteğin tekrarına aynı sonuç verilir; diğer bayat istekler 409 alır. Eksik sürüm, bilinmeyen süreç ve geçersiz mod 400; çalışan rolü 403 alır.
- `GuidanceHelpPreferences` AppDbContext kişisel tenant tablosudur; `(TenantId, UserId, ProcessKey)` benzersizdir. Başkasının tercihi okunmaz veya değişmez. `/api/me/data-export` yalnız oturum sahibinin `guidanceHelpPreferences` listesini de içerir. Tercih, okuma, sürümlü alıştırma ve gerçek işlem kanıtı ayrıdır.

## Migration ve yayın sınırı

Kiracı migration'ı `20261001104632_AddGuidanceHelpPreferences` yalnız tercih tablosu ve benzersiz indeksini ekler; platform katalog şeması değişmez. Üretimde bütün kiracı kataloglarına kontrollü uygulanmalıdır; otomatik üretim migration/seed veya Turhost yayını yapılmamıştır. Önceki migration'dan yeni migration'a idempotent SQL yerel doğrulama için üretildi. Modelde bekleyen değişiklik yoktur. Down, tablo doluyken 51000 ile durur; tercihleri korumadan kör geri dönüş yapılmaz.

## Doğrulama ve kalanlar

- Frontend genel testleri: 88 dosyada 507/507 başarılı. Kısa anlatımda örnek açma, kritik metinlerin görünürlüğü, kişisel tercih isteği, çakışmada yeniden okuma, sunucudan yeniden açma/farklı kullanıcı, hata durumunda ayrıntılı varsayım ve rol kapsamı doğrulanır. Günlük yönetim sırası ve yöneticiden dış kanıt adımının saklanması ayrıca kontrol edilir.
- SQL Server katalog/kişisel dışa aktarım entegrasyonu 12/12 başarılı. Üç günlük adımın yayın/yanıt/rol kontrolleri; yardım tercihinde kişisel izolasyon, aynı isteğin tekrarı, bayat sürüm, ayrıntılı moda dönüş, yanlış girdi, yönetici/çalışan sınırı ve kişisel dışa aktarım doğrulanır. Tercih kaydı okuma/alıştırma kanıtı üretmez.
- Backend birim testleri 91/91; Release `--warnaserror` sıfır uyarı/hata. Frontend üretim derlemesi başarılı; lint hata vermedi, dört mevcut test dosyası/helper uyarısı sürüyor. `npm audit --audit-level=high`: sıfır zafiyet. EF model–migration uyumu ve idempotent SQL üretimi başarılı.

Bu teslimde tarayıcı görsel kabulü, gerçek uzman yayını veya canlı sunucu testi yapılmadı. Sıradaki geliştirme katalog sürümüne bağlı gerçek işlem kanıtıdır. Diğer açık olağan işlem API'leri, günlük görev envanterinin genişlemesi, kanıta dayalı beceri/yardım uyarlaması, uzman/yeni kullanıcı kabulü ve Turhost ilk kurulum tamamlanacaktır. Alt adım sayısı bütün kapsamın kabulü değildir.

## 1 Ekim ek teslimi

[Uyarlanan anlatım](2026-10-01-ik-rehberi-bordro-ayrilis-uyarlanan-anlatim.md) kullanıcı kararıyla uygulandı. Aynı güncel adım/sürümde kişisel doğru alıştırma ve gerçek iş kanıtı birlikte varsa yalnız uzun örnek kapalı başlar; kritik açıklamalar görünür kalır. Önceki paragraftaki uyarlama açıklığı bu sunum kuralı için kapanmıştır; bağımsız yeterlilik kabulü oluşturulmadı.
