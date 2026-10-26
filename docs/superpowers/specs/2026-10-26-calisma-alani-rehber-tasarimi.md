# Ekipzen çalışma alanı ve rehber tasarımı

Tarih: 26 Ekim 2026. Kapsam: kullanıcının onayladığı giriş sonrası arayüz yenilemesi.

## Amaç ve tamamlanan aşamalar

Deneyimsiz kullanıcı aktif işi, yapılacak işlemi, gerekçeyi/dayanağı ve kontrol/düzeltme yolunu aynı çalışma alanında bulabilmeli. Ekipzen logosu ve mor vurgu korunur. Tanıtım sitesi ve giriş ekranları kapsam dışındadır.

| Aşama | Teslim |
| --- | --- |
| 1. Ortak tasarım ve örnekler | Ortak okunaklı tasarım; rehber başlangıcı, personel formu ve izin karar görünümü |
| 2. Çalışma alanına yayılım | Aşağıdaki tüm modüller; 43 mevcut işlem bölgesinde rehber/form yerleşimi |
| 3. Teknik doğrulama | Otomatik regresyonlar, rol/tema/genişlik taraması ve somut form kontrolleri |

Bu üç uygulama aşaması tamamlandı. Kullanıcılı görsel kabul, insan/uzman değerlendirmesi ve Turhost yayını kullanıcıya ait ayrı teslim faaliyetleridir.

## Uygulanan tasarım sözleşmesi

- Giriş sonrası kökte nötr açık/koyu yüzeyler, 16 px gövde, en az 14 px alan etiketleri, görünür odak, belirgin ana eylem ve okunabilir metin genişliği kullanılır. Eski kök renk/ölçek tanımları ortak tasarım dosyasında birleştirilir.
- Yerleşim gerçek işlem bölgesinin genişliğine göre karar verir. Bölge en az 960 px ise form ve 340 px rehber yan yanadır; daha darsa rehber formun üstünde, ayrıntıları kendi içinde kaydırılan kompakt alandadır. Geniş tablolar kendi alanında kayar.
- Personel bölüm gezinmesi formun üstündedir. Dar ekranda seçilen bölüm yatay şeritte görünür tutulur. Bölüm değişikliği, rehberi küçültme veya ayrı sekmede kaynak açma form taslağını sıfırlamaz.
- İşlem açıklaması açık başlar. Odaklanılan işlemin rehberi öne gelir; başka işlemlerin uzun açıklamaları kapanır. Alt işlem açılması onun rehberini öne alır. Açıkça küçültülmüş rehber yazarken kendiliğinden tekrar açılmaz.
- Açıklama sırası: **Şimdi yap → Neden ve dayanak → Örnek ve düzeltme → Sonucu kontrol et**. Terimler, kaynak künyesi ve sürüm ayrıntıları ayrı açıklamadadır. Küçültmede seçili sürümün ön koşulu, dayanağı, uygulama koşulu ve uzman uyarısı görünür kalır.
- Ayrıntılı/kısa/uyarlanan tercihleri korunur. Uyarlanan örnek kısaltması yalnız aynı güncel içerik sürümünün doğru alıştırması ve kullanıcının kendi gerçek işlem kanıtı birlikte varsa uygulanır; yeterlilik onayı oluşturmaz.
- Ortak rehber merkezi: **Başlangıç / Süreçler ve içerikler / Öğrenme geçmişim**. Başlangıç kayıtlı personel konumuna veya rolün izin verdiği ilk işe yönlendirir. İçerik taraması süreç ve başlıkla filtrelenir; tek seçili açıklama gösterilir.
- Çalışanın kendi okuma geçmişi mevcut yetkili katalog adresinde kalır. Yönetici yolculuğuna yeni yetki verilmez. Merkezî içerik hazırlama/yayın görünümü mevcut editör erişimine tabidir.

## Tek kapsam listesi

| Alan | Uygulanan mevcut ekranlar/işlemler |
| --- | --- |
| Çalışma alanı | Ortak gezinme, sayfa başlıkları, formlar, durumlar, düğmeler, tablolar, odak ve açık/koyu tema |
| İK Rehberi | Başlangıç, süreç haritası, içerik arama/filtre, sürümlü açıklama, öğrenme geçmişi, anlatım tercihleri, yetkili içerik yönetimi |
| Şirket | Başlangıç hazırlığı, şirket bilgileri, departman ekleme/düzeltme/silme |
| Personel | Oluşturma/düzeltme, bölüm gezinmesi, evrak inceleme/geri çekme, toplu aktarım önizlemesi |
| Personel ücret işlemleri | Mevcut ücret/tarih düzeltme, telafi uygulaması ve dış fark kanıtı alanları |
| İşe alım | Pozisyon, aday oluşturma/düzeltme, not/değerlendirme, aşama kararı, işe alma/provizyon |
| İzin | Mevcut tür ayarları, talep, iptal ön kontrolü, yönetici karar/etki/not alanı |
| Puantaj | Günlük kayıt ve ön kontrol |
| Bordro | Ay/dönem, gerçek girdi önizlemesi, satır kontrolü, onay, ayarlar ve ayrılış kuralları |
| Uyum | Belge oluşturma/düzeltme ve durum ön kontrolü |
| İşten ayrılış | Hesap, geri alma ön kontrolü, parasal revizyon ve mevcut ödeme/bildirim kanıtları |
| Günlük işler | İş kutusu, aksiyon oluşturma ve mevcut işlem/sonuç pencereleri |

Paylaşılan stil bütün giriş sonrası sayfalara uygulanır; 43 sayısı sayfa veya yeni süreç sayısı değil, rehber ile gerçek işlemin ayrıldığı mevcut JSX bölgesi sayısıdır.

## Korunan veri ve yetki davranışı

- Yeni backend API, migration, bağımlılık veya iş kuralı eklenmedi.
- Süreç/adım kimlikleri, sürüm adresleri ve gerçek işlem isteklerinin gövdeleri korunur.
- Okuma, alıştırma ve gerçek işlem ayrı kalır. Rehberi açma, adım değiştirme veya önizleme gerçek işlem/başarı kanıtı yazmaz. Kişisel konum kaydı mevcut davranışıyla sürebilir.
- İçeriği işlem kaydına bağlama açık kullanıcı seçimidir. Yeni yayın eski seçimi değiştirmez; eski sürüm uyarısı gösterilir. Sunucunun sürüm/önizleme/izin kontrolleri aynı kalır.
- Yükleniyor, yayımlanmamış, eski seçili sürüm, erişim ve yükleme hataları ayrı gösterilir. Rehber hatası form taslağını silmez. Bozuk katalog liste yanıtı çalışma alanını çökertmez.
- Gerçek işlem mevcut kaydetme/önizleme/onay eylemiyle yürür. Bu tasarım uzman içerik onayı veya otomatik mevzuat kabulü sağlamaz.

## Doğrulama ve sınırları

- Tam frontend regresyonu: **105 dosyada 639 test geçti**. Son personel bölüm görünürlüğü düzeltmesi ayrıca **29 personel testiyle** doğrulandı. Son seçili eski sürüm özeti değişikliği ve ilgili rehber regresyonlarında **3 dosyada 30 test geçti**; buna yeni eski-sürüm testi dahildir. Bu ek kontroller tam test toplamına yeniden eklenmez.
- Son TypeScript/Vite üretim derlemesi başarılı. Lint hata vermedi; daha önce mevcut olan 4 uyarı `DocumentsTab.test.tsx` ve `test/intersectionObserver.ts` dosyalarındadır.
- Playwright ile **1440, 1280, 768 ve 390 px**, açık/koyu tema: İK yöneticisinde 11, yöneticide 7, çalışanda 3 sayfa; toplam **168 sayfa/rol/tema/genişlik birleşimi** kontrol edildi. Sayfa taşması ve erişilebilir sayfanın yüklenememesi denetlendi. Bu tarama her ekranın tüm iş dallarının insan testi değildir.
- Başlangıç, personel formu ve izin karar görünümü somut ekranlarla incelendi. Personelde taslağın küçültme/açma ve bölüm değişiminde korunması; yan rehberin gerçek genişliği; dar ekran seçili bölüm görünürlüğü; izin karar alanının rehber yanında kalması; içerik aramasının seçili sürümü koruması; yayımlanmamış içerik ve rehber hatası kontrol edildi.
- Klavye adım seçimi, açık panelden geri dönüş, modaldan ana rehbere dönüş, gizli bölümün rehberi devralmaması, açık sürüm seçimi ve gerçek işlem kanıtından ayrım otomatik testlerle korunur.
- Bazı ekran görüntülerindeki yayımlanmış içerik ve bekleyen izin satırı yalnız otomasyon tarayıcısına verilen, açıkça etiketli yerel tasarım örnekleridir. Sunucuda içerik yayımlanmadı, uzman onayı veya gerçek İK işlemi yapılmadı. Kullanıcının tarayıcısında gerçek katalog durumu gösterilir.
- Yerel Playwright kayıtları `artifacts/` ve `output/playwright/` altında tutulur; ürün kaynaklarına veya Git senkronizasyonuna eklenmez.

## Teslim

Yerel önizleme `http://127.0.0.1:5183/app/guidance` adresindedir. Kullanıcı bu tasarımı çalışan uygulamada inceleyebilir. Yeni tasarımın Git senkronizasyonu, tam commit/ref/dosya listesi içeren ayrı raporla ve depodaki tek açık paket onayıyla yürütülür. Bu çalışma canlıya yayın yapmaz.
