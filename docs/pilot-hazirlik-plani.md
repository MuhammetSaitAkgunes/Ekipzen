# Ekipzen — kontrollü pilot hazırlık planı

Tarih: 8 Eylül 2026. Başlangıç analizi: `raporlar/pilot-hazirlik-analizi-2026-09-07.md`.

## Hedef ve sınır

İlk müşteri yaklaşık 10 personelli, gündüz çalışan bir muhasebe bürosu. Kullanıcının satın aldığı Turhost Windows Profesyonel paketinde barındırılacak; SQL de Turhost'ta olacak. Kullanıcı normal alan adını açacak, arayüz ve API ayrı yayınlanacak. Hedef genel satış açılışı değil; tek müşteride destekli, geri alınabilir ve ölçülebilir pilot. İlk bordro, büronun mevcut bordrosuyla paralel hesaplanıp mali müşavir tarafından kalem bazında kabul edilmeden gerçek ödeme veya beyan için esas alınmayacak.

Doğrulanmış çalışma düzeni: yalnız Pazartesi–Cuma, 09.00–18.00, 1 saat yemek molası; günlük net 8, haftalık net 40 saat. Cumartesi çalışma yok. Bu bilgi şimdilik pilot planıdır; uygulamadaki çalışma/fazla mesai kuralı P3 doğrulaması olmadan değiştirilmez.

Öncelik sırası: veri ve hesap doğruluğu → güvenli işletim → günlük kullanım → görsel iyileştirme. Mevzuat veya desteklenmeyen çalışan statüsü kapsam dışına alınarak sessiz hesap yapılmaz; uygunluk doğrulanana kadar o bordro bloke edilir. Puan/yüzde yerine aşağıdaki somut çıkış kapıları kullanılır.

## Paketler ve çıkış kapıları

| Paket | Teslimat | Tamamlanma kanıtı | Tahmini mühendislik süresi |
| --- | --- | --- | --- |
| P1 — Gerçek müşteri başlangıcı | Demo verisiz başlangıç kurulumu; eksik katalogları tamamlama; ilk mevzuat taslağını açma/kaydetme/onaylama | Yeni tenant üzerinden API testleri; tekrar/eşzamanlı kurulum ve izolasyon testleri; boş/hatalı/taslaklı ekran testleri | 1–2 gün |
| P2 — Windows üretim ortamı | IIS + HTTPS yayın paketi; Production yapılandırması; erişim, kalıcı dosya/anahtar dizinleri, SMTP; gerçeği yansıtmayan özelliklerin kapatılması | Temiz yayın ve yeniden başlatma provası; sağlık kontrolü; davet/şifre akışı; yetkisiz erişim testi; doğrulanmış CI sonucu | 2–3 gün |
| P3 — Büro çalışma düzeni ve hesap girdileri | Çalışma haftası/mola/fazla mesai profili; yarım gün tatiller ve izin hakları; bordro sonrası puantaj değişiklik kuralı; geçiş bakiyeleri | Belirlenen profilde puantaj→izin→bordro regresyonları; uzman kontrollü mevzuat seti ve mutabakat senaryoları | 3–5 gün |
| P4 — Yedek ve geri dönüş | Windows zamanlanmış yedek; platform+tenant DB+evrak+anahtarlar; sunucu dışı kopya; hata alarmı; geri dönüş prosedürü | Ayrı test ortamında geri yükleme; dosya indirme/giriş/bordro karşılaştırması; ölçülmüş süre ve kayıp sınırı | 1–2 gün |
| P5 — Pilot kabulü | 10 kişilik kontrollü aktarım; gerçek tarayıcı senaryoları; paralel bordro; kullanıcı eğitimi ve destek kaydı | Büro sorumlusu + mali müşavir + ürün sahibi tarafından yazılı kabul; açık kritik hata olmaması | 2–3 gün + müşteri bekleme süresi |

Toplam yaklaşık 9–15 mühendislik günü; takvim taahhüdü değildir. Sunucu erişimi, uzman değerlendirmesi ve paralel bordro dönemi bekleme süresi ayrıca değerlendirilir. Her paket küçük yerel commitlerle ilerler. Doğrulama sonrası kaynak/hedef refs, commitler, dosyalar ve test sonuçları bildirilir; feature push + main merge + main push için tek paket onayı alınır. Önceki onay yeni pakete taşınmaz.

## P1 — tamamlandı ve senkronize edildi

- İK yöneticisine özel başlangıç durumu ve açık kurulum işlemi. Şirket adı platformdaki ilgili tenant kaydından gelir.
- Kurulum yalnız eksik kayıtları ekler; müşteri profilini, izin türlerini, tatillerini ve mevzuat setlerini ezmez. Eşzamanlı istekler tenant veritabanı kilidi ve transaction ile sıralanır.
- 2026 başlangıç şablonu demo ve müşteri akışınca ortak kullanılır. Müşteride hiçbir uzman inceleme notu veya onaylayan kimliği otomatik üretilmez. Örnek çalışan, departman, risk skoru, bordro ve abonelik eklenmez.
- Onaylı set yokluğu (404) teknik arıza sayılmaz: mevcut taslak açılır; hiç kayıt yoksa kurulum paneli gösterilir. Gerçek API hatası yükleniyor ekranında gizlenmez.
- Taslak değişiklikleri kaydedilmeden eski kayıt onaylanamaz; inceleme notu olmadan onay butonu açılmaz. Sunucu tarafındaki sürüm ve yetki kontrolleri korunur.
- Kapsam uyarısı: mevcut takvim şablonu yalnız 2026 tam gün tatilleridir. Yarım günler ve işyeri politikası P3 çıkış şartıdır. “Başlangıç kayıtları mevcut” ifadesi mevzuat veya pilot uygunluğu anlamına gelmez.

P1 yerel testleri geçti (91 birim + 207 entegrasyon + 165 frontend). `518ab53` commit’i ile `main` ve `ikpro/main` eşitlendi. Gerçek müşteri sunucusuna yayın veya gerçek personel aktarımı yapılmadı.

## P2 — Turhost Windows Profesyonel üzerinde işletim

Hedef kullanıcı deneyimi: normal alan adında React arayüzü; ayrı API uygulaması arka planda çalışır; SQL Turhost'tadır. Önceki kök IIS uygulaması varsayımı doğrudan Turhost `/api` alt uygulamasına taşınmaz. Adres/yetki/runtime uygunluğu teyit edilip uyarlanmalı; demo Docker Compose üretim kurulumu değildir. Resmî paket araştırması, açık koşullar ve destek soruları: `docs/turhost-pilot-onkontrol.md`.

1. Turhost paneli/destekten hesaba özel runtime, alan adı/TLS, test yayın alanı, dosya ve SQL yetkileri alınır. RDP/sistem yöneticisi yetkisi varsayılmaz. Günlük uygulama ve provision/migration yetkileri ayrıştırılır.
2. Production yapılandırması doğrulanır: demo seed kapalı; izinli origin/host listesi; HTTPS/proxy davranışı; sırların repo dışında ve erişimi kısıtlı tutulması; loglarda hassas veri bulunmaması.
3. Dosyalar ve veri koruma anahtarları sürüm klasörü dışında kalıcı dizinlere alınır. Yeniden yayın ve servis yeniden başlatma bunları kaybettirmemeli.
4. Gerçek SMTP ile davet ve şifre işlemleri test edilir. Dosyaya e-posta bırakılması teslim edilmiş e-posta sayılmaz.
5. İşlevsiz SMS 2FA ve haftalık rapor vaatleri kapatılır veya açıkça kullanılamıyor gösterilir; sahte güvenlik hissi veren toggle bırakılmaz. Şifre politikası ve oturum saklama riskleri karara bağlanır.
6. Bağımlılık/SDK/CI kontrolleri ve .NET destek takvimi ele alınır; yalnız test edilerek sürüm değişir. CI'ın varlığı değil başarılı koşum kaydı kanıttır.

### P2 güncel ayrımı

P2-A yerel üretim hazırlığı: demo içermeyen Windows/IIS paket betiği, dış yapılandırma ve açılış doğrulaması, HTTPS/host/CORS güvenlik sınırları, sertifikayla şifreli kalıcı Data Protection anahtarları, sınırlı dönen loglar, üretimde daha güçlü şifre politikası ve işlevsiz SMS/haftalık rapor seçeneklerinin kapatılması uygulandı. Ayrıntılar ve sınırlar: `docs/windows-pilot-yayin.md`.

P2-A `09c2b34` ile senkronize edildi. Son GitHub CI koşumu frontend'de geçti, backend'de test sonu katalog temizliğinde 1 hata verdi. P2-B'nin ilk yerel alt paketi bu test ortamı yarışma riskini ve bağımlılık uyarılarını ele alır; yeni CI başarısı push sonrasında ayrıca kanıtlanmalı.

P2-B gerçek ortam kabulü açık: .NET 9 ve SQL hesap yetkileri, uygulama havuzu, güvenli kalıcı yollar, SMTP teslimi, alt uygulama adres uyumu ve gerçek tarayıcı/restart provası. Paylaşımlı hosting'de `get-windows-server-inventory.ps1` çalıştırma yetkisi varsayılmaz; sağlayıcı/panel kanıtı kullanılır. SQL yönetim yetkisi ayrımı destek yanıtından sonra karara bağlanacak. Runtime hesabına kalıcı `sysadmin` verilmez. Git senkronizasyon onayı yayın veya migration yetkisi değildir.

## P3 — para hesabından önce veri doğruluğu

Bürodan henüz doğrulanması gereken bilgiler: personelin standart 4/a uygunluğu; brüt/net ücret anlaşması; yemek/yol/prim uygulaması; engellilik/teşvik/özel statüler; önceki işveren ve yıl içi kümülatif matrahlar; işe giriş tarihleri ve açılış izin bakiyeleri. Çalışma günleri, saatler ve mola bilgisi yukarıda doğrulandı.

- Pilotun beş günlük/40 saatlik haftası kullanıcı tarafından doğrulandı. Mevcut günlük 480 dakika eşiğinin bu profile rastlaması, haftalık fazla çalışma ve ücret kurallarının da doğru olduğu anlamına gelmez; bu kurallar ayrıca doğrulanır.
- Yarım gün tatiller, hizmet süresi/yaşa bağlı izin hakları ve bakiye geçişleri resmî kaynaklarla ve uzmanla doğrulanır. Başlangıçta herkese demo bakiyesi verilmez.
- Bordro onayı sonrası puantaj/izin/ücret girdisinin değişmesi, mevcut onaylı sonucu sessizce geçersizleştirememeli. Düzeltme/açma kuralı ve audit kanıtı tanımlanır.
- İlk dönem için yıl içi geçmiş matrah ve istisna birikimi açıklanmış olmalı; eksik geçmiş “0” diye varsayılmaz.
- Asgari ücret, farklı vergi dilimi, ay içinde giriş/çıkış, eksik gün, yemek/yol, fazla mesai ve uygun özel durumlar için bağımsız beklenen sonuçlar saklanır. Hesap motorunun kendi sonucu kendisine referans yapılmaz.
- Her tutar için kuruş düzeyinde fark listesi çıkarılır. Açıklanamayan fark veya desteklenmeyen yasal durum varsa bordro kabulü yapılmaz.

## P4 — yedek, geri yükleme ve destek

Mevcut Windows yedek scriptleri temel alınır. Zamanlama, saklama süresi, SQL hesap yetkisi, şifreli sunucu dışı kopya ve hata bildirim alıcısı ürün sahibiyle belirlenir. Ön hedef en fazla 24 saat veri kaybı ve 4 saat içinde geri dönüş; ancak ölçülüp onaylanmadan garanti verilmez. Bordro onayı ve sürüm geçişi öncesinde ayrıca geri dönüş noktası alınır.

Yalnız `.bak` dosyasının varlığı yeterli değildir. Platform yönlendirmesi, tenant verisi, personel evrakları ve gerekli anahtarlar birlikte değerlendirilir. Gerçek ortamın üzerine geri yükleme denenmez. Ayrı katalog/dizinde geri yükleme, uygulama girişi, personel sayısı, evrak hash/indirme ve onaylı bordro sonuçları doğrulanır. Restore ile abonelik veya tenant kimliği yanlışlıkla geri sarılmamalı.

## P5 — pilot kabul kontrol listesi

- [ ] Gerçek müşteriye ait verisiz şirket kuruldu, yönetici daveti teslim edildi ve kabul edildi.
- [ ] 10 personelin kimliği, işe giriş tarihi, ücret paketi, izin ve matrah açılışları çift kontrol edildi; hassas veriler Git'e konulmadı.
- [ ] Yönetici/çalışan yetkileri, izin talebi ve onayı, gündüz puantajı, evrak yükleme/indirme gerçek tarayıcıyla geçti.
- [ ] İlgili ayın resmî parametreleri mali müşavirce onaylandı; paralel bordro kalemlerinde açıklanamayan fark yok.
- [ ] Desteklenmeyen yasal durumlar hesap başlamadan açıkça durduruluyor.
- [ ] HTTPS, e-posta, kalıcı depolama, loglar ve yedek alarmı yayın ortamında doğrulandı.
- [ ] Ayrı ortamda geri yükleme ve önceki uygulama sürümüne dönüş provası geçti.
- [ ] Açık kritik/yüksek veri güvenliği, veri kaybı veya hesap hatası yok.
- [ ] Büro sorumlusu kullanım kabulü; mali müşavir hesap kabulü; ürün sahibi pilot açılış onayı verdi.

Pilot sırasında haftalık kullanım ve sorun kaydı tutulur: başarılı giriş/davet, izin tamamlama, puantaj düzeltme, hesap farkı, destek süresi ve yedek sonucu. Bu bir otomasyon kurulumu talebi değildir; bu dosya işletim planıdır. İlk gerçek bordro dönemi sorunsuz tamamlanmadan genel satışa hazır beyanı verilmez.

## Çalışma ilkesi ve sonraki teslim

Mevcut katmanlı/modüler monolit korunur. İş kuralları Application/Domain, veri erişimi Infrastructure, yetki ve sözleşme API sınırında kalır. Bu küçük pilot için mikroservis veya gereksiz yeniden yazım yok. Testler önce etkilenen alanda, ardından paket sonunda bütünleşik çalıştırılır; aynı değişmemiş taramalar tekrarlanmaz.

Sıradaki teslim P2-B'nin CI/bağımlılık alt paketi ve tek Git senkronizasyon onayıdır. Ardından sağlayıcı cevaplarıyla SQL runtime/provizyon ve API yayın tasarımı karara bağlanır. P3 yerel çalışması bu dış bilgiler beklenirken ayrıca planlanabilir; P2/P3/P4/P5 kanıtları olmadan pilot açılmış sayılmaz.
