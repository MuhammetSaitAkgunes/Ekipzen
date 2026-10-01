# İK rehberi — tam kapsam yayın bitirme listesi

30 Eylül 2026. Kullanıcı kararı: onaylı plandaki tüm süreç ve öğrenme özellikleri tamamlandıktan sonra Turhost'ta ilk canlı yayın yapılacak. Mevcut özellikleri pilot diye erken yayımlama kararı yoktur. Henüz sunucuya publish gönderilmemiştir.

Bu belge [onaylı planı](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md) daraltmaz; kodda mevcut olan parçalar ile yayın öncesi açık teslimleri ayırır. Eski plandaki boş kutular tek başına sıfır ilerleme anlamına gelmez; tamamlandı kararı kod ve doğrulamaya dayanır.

İçerik yayın kararı: tek merkezî katalog; içerik yayınını yalnız Ekipzen ekibi yönetir. İçeriği hazırlayan veya düzenleyen kişi kendi sürümünü onaylayamaz. Onayı başka bir yetkili Ekipzen İK yöneticisi verir; uzman inceleme bilgisi ayrıca kaydedilir. Müşteri şirketindeki İK rolü yayın yetkisi vermez; İK rolü tek başına uzman incelemesinin kanıtı değildir.

## Kodda mevcut ürün parçaları

| Parça | Uygulanan kapsam | Kaynak |
| --- | --- | --- |
| Öğrenme yolculuğu | İşe girişte yer imi ve gerçek kart oluşturma; adayda ilk gerçek inceleme; izin kararında gerçek işlem ve açıklamalı alıştırmalar. Bunlar yeterlilik onayı değildir. | frontend/src/features/guidance/GuidanceJourneyPage.tsx; backend/src/Ekipzen.Application/Features/Guidance/ |
| Günlük işler | Bekleyen izin, uyum belgesi, ücret paketi eksikliği, aday ilk incelemesi, bordro ve ayrılış taslakları ile parasal düzeltmelerin dış işlem kanıtları. | backend/src/Ekipzen.Application/Features/Guidance/GetDailyGuidanceTasksQuery.cs |
| İşe alım | Gerekçeli sürümlü pozisyon/aday düzeltmesi; not ve değerlendirme revizyonu; gerekçeli aşama kararı ve dayanak snapshotları; puansız/sıfır puan ayrımı. | backend/src/Ekipzen.Application/Features/Recruitment/; frontend/src/features/recruitment/ |
| Adaydan çalışan kartına dönüş | Mevcut kalıcı işe alma/provizyon kaydından doğrulanan çalışan bağlantısı. Hesap hazırlığı bekliyor olsa da çalışan kartı açılır; aktif olmayan çalışan için işe giriş rehberi önerilmez. Çelişkili bağlantı tahmin edilmez. | RecruitmentQueries.cs; CandidateDetail.tsx |
| Kritik hata düzeltmeleri | Evrak geri çekme, ücret paketi/dönem düzeltmeleri, telafi incelemeleri, bordro ve ayrılış parasal sürümleri ve ayrı dış ödeme/bildirim kanıtları. | [API eksikleri ve tamamlanan sözleşmeler](2026-09-25-ik-rehberi-backend-api-eksikleri.md) |

## Yayın öncesi açık teslimler

| Teslim | Bitti sayılma ölçütü | Bugünkü durum |
| --- | --- | --- |
| Ortak içerik kataloğu ve inceleme/yayın yaşam döngüsü | Süreç/adım/rol, kaynak türü, örnek, yürürlük, içerik sürümü ve inceleyen bilgisinin sunucuda tanımlanması; onaylı sürümün değişmeden saklanması; taslak/inceleme/onay/kullanımdan kaldırma; yayımlanan içeriğin rehber ekranında okunması. | Teknik katalog/API/okur-yayın ekranı uygulandı; [merkezî içerik sözleşmesi](2026-09-30-ik-rehberi-icerik-katalogu.md). Gerçek içerik ve uzman kabulü ile adım/öğrenme bağlantısı aşağıdaki teslimlerde açık. |
| Tüm süreçlerde öğrenme ve yardım | Şirket hazırlığı, işe alım, işe giriş, personel, izin, mesai/puantaj, bordro, uyum, ayrılış ve günlük yönetimde ortak adım/örnek/karar desteği; kullanıcı/tenant/sürüm bazlı ilerleme ve yardım tercihi; gerçek işlem ile alıştırmanın ayrılması. | Kısmi; on başlangıç taslağı ve süreç/katalog geçişleri, kişisel sürüm bazlı inceleme beyanı uygulandı. [Kaynaklı öğrenme](2026-10-01-ik-rehberi-kaynakli-ogrenme.md) ve [sürümlü alıştırma](2026-10-01-ik-rehberi-surumlu-alistirma.md) üzerine şirket/personel/puantaj eklendi: altı grupta yirmi alt adım taslağı, kişisel sürümlü soru/yanıt, adım düzeyinde rol sınırı ve seçili sürümün ekran içi açıklaması mevcut; [son teslim](2026-10-01-ik-rehberi-sirket-personel-puantaj.md). Bu grupların bütün alt işlemleri tamamlanmış değildir. Kalan alt adımlar, uzman içerik kabulü/yayın, katalog sürümüne bağlı gerçek iş ve yardım tercihi açık; adım veya okuma sayısı bütün kapsam kabulü değildir. |
| Günlük iş ve temel işlem bağımlılıkları | Kapsamdaki olağan işleri gerçek kayıttan üretme, neden/sorumlu/gerçek son tarih, tekilleştirme/kapanma/yeniden açılma; temel işlemlerde eksik sunucu önizlemesi ve eşzamanlılık kapılarının tamamlanması. | Kısmi; ayrıntılı API kuyruğu bağlantılı belgede. Karmaşık istisnalarda güvenli uzman yönlendirmesi ilk kullanıcı hedefiyle uyumludur. |
| Kaynaklı içerik ve ürün kabulü | Mevzuat/şirket politikası/iyi uygulama ayrımı; resmî kaynağın ve koşulların uzman incelemesi; firma sahibi/yeni mezun görev denemeleri; klavye/dar ekran/hata/düzeltme ve rol/tenant sınırları; gerçek kanıtlarla tüm envanterin karşılanması. | Açık; açıklama metni veya test sayısı bu kabulün yerine geçmez. |
| Turhost ilk canlı kurulum | Hesaba özel alan adı/HTTPS, .NET çalıştırma ve IIS düzeni, SQL/SMTP/kalıcı dizin/anahtar erişimleri; tek dış /api öneki; tüm katalogların kontrollü migrationı; davet/dosya/oturum ve geri yükleme denemesi. | Açık; yerel publish üretimi hedef sunucu uyumluluğunu veya canlı kurulumunu kanıtlamaz. |

## İş sırası ve bitirme disiplini

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
