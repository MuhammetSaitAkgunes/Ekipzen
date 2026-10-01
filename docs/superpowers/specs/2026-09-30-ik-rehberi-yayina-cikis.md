# İK rehberi — tam kapsam yayın bitirme listesi

30 Eylül 2026. Kullanıcı kararı: onaylı plandaki tüm süreç ve öğrenme özellikleri tamamlandıktan sonra Turhost'ta ilk canlı yayın yapılacak. Mevcut özellikleri pilot diye erken yayımlama kararı yoktur. Henüz sunucuya publish gönderilmemiştir.

Bu belge [onaylı planı](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md) daraltmaz; kodda mevcut olan parçalar ile yayın öncesi açık teslimleri ayırır. Eski plandaki boş kutular tek başına sıfır ilerleme anlamına gelmez; tamamlandı kararı kod ve doğrulamaya dayanır.

İçerik yayın kararı: tek merkezî katalog; içerik yayınını yalnız Ekipzen ekibi yönetir. İçeriği hazırlayan veya düzenleyen kişi kendi sürümünü onaylayamaz. Onayı başka bir yetkili Ekipzen İK yöneticisi verir; uzman inceleme bilgisi ayrıca kaydedilir. Müşteri şirketindeki İK rolü yayın yetkisi vermez; İK rolü tek başına uzman incelemesinin kanıtı değildir.

## Kodda mevcut ürün parçaları

| Parça | Uygulanan kapsam | Kaynak |
| --- | --- | --- |
| Öğrenme yolculuğu | İşe girişte yer imi ve gerçek kart oluşturma; adayda ilk gerçek inceleme; izin kararında gerçek işlem ve açıklamalı alıştırmalar. Bu üç gerçek işlemde açıkça seçilen merkezî rehber sürümü işlem kanıtıyla korunur; geçmiş işler sonradan damgalanmaz. Bunlar yeterlilik onayı değildir. | [Sürümlü işlem kanıtı](2026-10-01-ik-rehberi-surumlu-islem-kaniti.md); frontend/src/features/guidance/GuidanceJourneyPage.tsx; backend/src/Ekipzen.Application/Features/Guidance/ |
| Günlük işler | Bekleyen izin, uyum belgesi, ücret paketi eksikliği, aday ilk incelemesi, bordro ve ayrılış taslakları ile parasal düzeltmelerin dış işlem kanıtları. | backend/src/Ekipzen.Application/Features/Guidance/GetDailyGuidanceTasksQuery.cs |
| İşe alım | Gerekçeli sürümlü pozisyon/aday düzeltmesi; not ve değerlendirme revizyonu; gerekçeli aşama kararı ve dayanak snapshotları; puansız/sıfır puan ayrımı. | backend/src/Ekipzen.Application/Features/Recruitment/; frontend/src/features/recruitment/ |
| Adaydan çalışan kartına dönüş | Mevcut kalıcı işe alma/provizyon kaydından doğrulanan çalışan bağlantısı. Hesap hazırlığı bekliyor olsa da çalışan kartı açılır; aktif olmayan çalışan için işe giriş rehberi önerilmez. Çelişkili bağlantı tahmin edilmez. | RecruitmentQueries.cs; CandidateDetail.tsx |
| Kritik hata düzeltmeleri | Evrak geri çekme, ücret paketi/dönem düzeltmeleri, telafi incelemeleri, bordro ve ayrılış parasal sürümleri ve ayrı dış ödeme/bildirim kanıtları. | [API eksikleri ve tamamlanan sözleşmeler](2026-09-25-ik-rehberi-backend-api-eksikleri.md) |

## Yayın öncesi açık teslimler

| Teslim | Bitti sayılma ölçütü | Bugünkü durum |
| --- | --- | --- |
| Ortak içerik kataloğu ve inceleme/yayın yaşam döngüsü | Süreç/adım/rol, kaynak türü, örnek, yürürlük, içerik sürümü ve inceleyen bilgisinin sunucuda tanımlanması; onaylı sürümün değişmeden saklanması; taslak/inceleme/onay/kullanımdan kaldırma; yayımlanan içeriğin rehber ekranında okunması. | Teknik katalog/API/okur-yayın ekranı uygulandı; [merkezî içerik sözleşmesi](2026-09-30-ik-rehberi-icerik-katalogu.md). Gerçek içerik ve uzman kabulü ile adım/öğrenme bağlantısı aşağıdaki teslimlerde açık. |
| Tüm süreçlerde öğrenme ve yardım | Şirket hazırlığı, işe alım, işe giriş, personel, izin, mesai/puantaj, bordro, uyum, ayrılış ve günlük yönetimde ortak adım/örnek/karar desteği; kullanıcı/tenant/sürüm bazlı ilerleme ve yardım tercihi; gerçek işlem ile alıştırmanın ayrılması. | Kısmi; on başlangıç taslağı ve süreç/katalog geçişleri, kişisel sürüm bazlı inceleme beyanı uygulandı. [Kaynaklı öğrenme](2026-10-01-ik-rehberi-kaynakli-ogrenme.md) ve [sürümlü alıştırma](2026-10-01-ik-rehberi-surumlu-alistirma.md) üzerine on grupta otuz yedi alt adım taslağı, kişisel sürümlü soru/yanıt, adım düzeyinde rol sınırı ve seçili sürümün ekran içi açıklaması mevcut. [Günlük yönetim ve yardım teslimi](2026-10-01-ik-rehberi-gunluk-yardim.md) üç günlük adımı ve sunucuda saklanan süreç bazlı kişisel ayrıntılı/kısa anlatım tercihini ekler; son teslim uyarlanan anlatımı da sunar. Bu manuel tercih beceri veya iş kanıtı değildir. [Sürümlü gerçek kanıt](2026-10-01-ik-rehberi-surumlu-islem-kaniti.md) çalışan kartı, aday ilk karar ve izin kararına bağlandı. Diğer gerçek işlem bağlantıları ve açık alt işlemler, uzman içerik kabulü/yayın ve kanıta dayalı bağımsız yeterlilik değerlendirmesi açık; kabul edilen anlatım uyarlaması son teslimde uygulandı; adım veya okuma sayısı bütün kapsam kabulü değildir. |
| Günlük iş ve temel işlem bağımlılıkları | Kapsamdaki olağan işleri gerçek kayıttan üretme, neden/sorumlu/gerçek son tarih, tekilleştirme/kapanma/yeniden açılma; temel işlemlerde eksik sunucu önizlemesi ve eşzamanlılık kapılarının tamamlanması. | Kısmi; ayrıntılı API kuyruğu bağlantılı belgede. Karmaşık istisnalarda güvenli uzman yönlendirmesi ilk kullanıcı hedefiyle uyumludur. |
| Kaynaklı içerik ve ürün kabulü | Mevzuat/şirket politikası/iyi uygulama ayrımı; resmî kaynağın ve koşulların uzman incelemesi; firma sahibi/yeni mezun görev denemeleri; klavye/dar ekran/hata/düzeltme ve rol/tenant sınırları; gerçek kanıtlarla tüm envanterin karşılanması. | Açık; açıklama metni veya test sayısı bu kabulün yerine geçmez. |
| Turhost ilk canlı kurulum | Hesaba özel alan adı/HTTPS, .NET çalıştırma ve IIS düzeni, SQL/SMTP/kalıcı dizin/anahtar erişimleri; tek dış /api öneki; tüm katalogların kontrollü migrationı; davet/dosya/oturum ve geri yükleme denemesi. | Açık; yerel publish üretimi hedef sunucu uyumluluğunu veya canlı kurulumunu kanıtlamaz. |

## İş sırası ve bitirme disiplini

1 Ekim ek teslimi: [şirket başlangıcı, puantaj ve uyum](2026-10-01-ik-rehberi-sirket-puantaj-uyum-kaniti.md) yedi gerçek komutta sürümlü kanıtı genişletir. Şablonun gerçekten eklediği kayıtlar/sayılar ve aynı kayda yapılan ayrı düzeltmeler öğrenme yolculuğunda görünür. Bu üç süreç grubunun bütün alt işlemleri tamamlanmış sayılmaz; diğer şirket/personel, bordro/ayrılış bağlantıları, olağan API kapıları ve günlük görev envanteri açık kalır. Bordro açma/satır/dönem onayı ve ayrılış tamamlama/geri alma bağlantıları da tamamlandı; [uyarlanan anlatım teslimi](2026-10-01-ik-rehberi-bordro-ayrilis-uyarlanan-anlatim.md) güncel kişisel alıştırma ve iş kanıtıyla yalnız uzun örneği kısaltır. Sıradaki geliştirme kalan olağan işlem kapıları, diğer alt işlem bağlantıları ve günlük görev kapsamıdır. Tam kapsam yayın koşulu ve uzman/yeni kullanıcı/Turhost kabulü korunur.

1. Mevcut aday–çalışan geçişini kapat; genel testleri ve ilk yerel paket üretimini doğrula.
2. Ortak içerik kataloğunu oluştur; kaynak/inceleme ve sürüm sözleşmesini ekranlara bağla.
3. Aynı altyapıyla süreç gruplarını tamamla; temel görevler ve işlem kanıtlarını birlikte genişlet.
4. Kapsam matrisindeki açık olağan işlem kapılarını tamamla; karmaşık istisnayı açık uzman yönlendirmesiyle sınırla.
5. Kaynak/uzman/kullanıcı ve tüm uygulama kontrollerini bitir; onaylı kaynak committen yayın paketini üret ve Turhost'ta doğrula.

Bu sıra yeni bir son tarih veya sabit küçük adım sayısı vaadi değildir. Bir teslim yalnız belge yazılarak bitmiş sayılmaz. Git senkronizasyonu ile sunucuya yayın ayrı işlemlerdir; ilk yayına kadar ürün kapsamı daraltılmaz.

## Yerel paket üretimi ve sunucu sınırı

Mevcut scripts/build-windows-pilot.ps1 betiği Release/win-x64, framework-dependent DLL ve React üretim dosyaları üretir; IIS/SQL/SMTP üzerinde değişiklik yapmaz. İlk deneme prova olarak, manifestte dirtyWorkingTree bilgisi korunarak çalıştırılır. Prova paketi tam kapsam ürün teslimi veya Turhost'a gönderilecek son paket değildir.

30 Eylül'deki ilk yerel prova başarıyla üretildi: backend DLL, frontend üretim dosyaları ve Production ortam değişkenleri taşıyan IIS web.config pakete alındı. Manifestteki 173 dosyanın SHA-256 değerleri doğrulandı; Development yapılandırması ve App_Data bulunmadı. Bu prova çalışma ağacından üretildiği için son kaynak commitinin yayın kanıtı sayılmaz. Teslim öncesinde tamamlanmış ve onaylanmış kaynak sürümünden yeniden paket üretilecek.

Üretimde otomatik migration/seed yoktur. Platform, ortak katalog ve kiracı şemalarının yönetim adımı ayrıca doğrulanır; /health tek başına tüm kiracı şemalarının güncel olduğunu göstermez. Yeni nullable aday puanı varken eski uygulamaya/veritabanına kör geri dönüş yapılmaz. Yayın yolu için docs/turhost-pilot-onkontrol.md ve docs/windows-pilot-yayin.md mevcut teknik başlangıç belgeleridir; hesaba özel gerçek durumu doğrulamak gerekir.

## 1 Ekim — uzman incelemesi öncesi teknik kontrol paketi

[Teslim sözleşmesi](2026-10-01-ik-rehberi-uzman-oncesi-teknik-kontroller.md) tüm süreçlerde kişisel güncel öğrenme görünümünü, ayrıntılı adım alanlarını, açık aksiyon/şirket başlangıcı/taslak ayar günlük işlerini, bordro satırı önizlemesi ve zorunlu sürüm kontrolünü, atomik koşullu uyum düzeltmesini ve ayrıntılı koşullu kurulum önizlemesini ekler. Üç olağan API boşluğu kapandı; dönem açma/ayrılış etki önizlemesi, ağ tekrarı/aktarım, diğer alt işlem kanıtları ve günlük görev matrisi açık kalır. Uzman içeriği, pilot yeterlilik eşikleri ve deneyimsiz kullanıcı kabulü yapılmadı. Tam kapsam yayın koşulu korunur; bu paket yalnız teknik teslimdir.

## 2 Ekim — bordro/ayrılış etkileri ve tekrar güvenliği

[Bu teknik paket](2026-10-02-ik-rehberi-etki-onizleme-tekrar-guvenligi.md) dönem açmanın gerçek personel/kaynak girdilerini, ayrılış tamamlama/geri almanın ekip-dönem-erişim etkilerini ve personel oluşturmanın belirsiz ağ sonucundaki tekrar güvenliğini tamamlar. Önizleme ve teyit aynı kayıt sürümüne bağlanır; salt okunur önizleme öğrenme kanıtı üretmez. Manuel ayrılışta taslak kaydı açık bir aşamadır; son onay personeli pasife alır. Makbuz migration'ı kontrollü tenant şema yönetimine dahildir.

Sıradaki paket toplu aktarımın satır bazında önizleme ve hata sonuçlarıdır. Diğer olağan alt işlemlerin öğrenme bağlantıları/günlük görevleri, pilot sonrası uygulama seviyeleri, gerçek uzman/editör yayını, deneyimsiz kullanıcı ve Turhost kabulü açık kalır. Tam kapsam yayın koşulu daraltılmadı; canlıya dosya veya migration gönderilmedi.

## 2 Ekim 2026 — toplu personel aktarımı teknik güvenliği

[Teslim sözleşmesi](2026-10-02-ik-rehberi-toplu-personel-aktarimi.md) satır bazında önizleme, gerçek çalışan kimlikli sonuç, önizlemeye bağlı atomik kayıt ve tenant/kullanıcı/gönderim kapsamlı kalıcı tekrar korumasını ekler. Aktarım API'si multipart `importRequestId` ve `expectedPreviewToken` ister; kaynak değişikliği 409 ile uygulamayı durdurur. Kendi sonuç sorgusu ve oturumlu şablon indirme vardır. Pasif personel oluşturma boşluğu kapandı; aktif/belirsiz süreli/tam zamanlı şablon varsayımları görünürdür. Hatalı/mükerrer satırlar açık tercihle atlanır; mevcut çalışan değişmez.

Bu paket aktarım makbuzunu sürümlü rehber kanıtı gibi damgalamaz. Diğer personel alt işlemlerinin sürümlü bağlantıları, eksik bilgi günlük görevleri ve düzeltme matrisi sıradaki kapsamdır. Tarayıcı yenilemesi sonrası dosya/form kurtarma, TC'siz kişinin yeni gönderimler arasında tekilliği ve bağımsız aktarım dersi/kanıtı tamamlandı sayılmaz. Pilot yeterlilik kabulü, gerçek uzman/editör yayını, deneyimsiz kullanıcı ve Turhost kabulü açık kalır. Tüm öğrenme kapsamı yayın koşulu korunur; üretime dosya veya migration gönderilmedi.


## 2 Ekim 2026 — personel kartı ve evrak alt işlemleri

[Teslim sözleşmesi](2026-10-02-ik-rehberi-personel-alt-islem-kanitlari.md) kart düzeltme, departman/yönetici seçimi, belge yükleme ve gerekçeli geri çekmeyi açıkça seçilen güncel rehber sürümüne bağlar. Gerçek değişen alan adları/gerekçe korunur; eski kişisel değerler otomatik kopyalanmaz. İK yöneticisine kart belirteci, şirket içi yönetici adayları ve son 100 işlem geçmişi uçları sunulur. Güncel form eski kart belirteciyle 409 alır; arka plan yenilemesi taslağı değiştirmez. Kart, evrak ve kanıt aynı transaction içindedir. Kaynaksız işlem öğrenme kanıtı sayılmaz.

İletişim bilgisi yokluğu, aktif evrak yokluğu ve geçersiz atanmış yönetici bağlantısından üç günlük inceleme işi üretilir; gerçek kayıt düzeldiğinde kalkar. Son aktif evrak geri çekildiğinde iş yeniden oluşur. Bu sinyaller zorunlu kişisel bilgi/belge listesi, hukuki son tarih veya bütün evrakın tamamlanması kararı değildir. Normal yöneticisiz çalışan için atama zorunlu tutulmaz. Tenant migration/API/frontend birlikte teslim edilir; üretime publish veya migration gönderilmedi.

Sıradaki kapsam: personel durum/yeniden işe alma ve ücret paketi alt işlemlerinin öğrenme/günlük görev bağları; ardından diğer açık modül alt işlemleri. Toplu aktarımın bağımsız sürümlü dersi/kanıtı, genel ağ sonucu güvenliği, uygulama seviyesi kabulü, gerçek uzman/editör/yeni kullanıcı kabulü ve Turhost ortam kabulü açık. Önceki finansal telafi/entegrasyon/saklama bağımlılıkları kaldırılmadı; tüm öğrenme kapsamı yayın koşulu korunur.
