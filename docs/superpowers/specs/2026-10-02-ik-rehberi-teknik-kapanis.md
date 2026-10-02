# T07 — Akış doğrulaması ve teknik kapanış

2 Ekim 2026. Önceki onaylı T06 `b8597c7`, `cd9f2e7`, `4e56c5c` commitleri ekipzen remote’una gönderildi; main fast-forward birleştirildi ve gönderildi. T07 geliştirmesi `codex/ik-rehberi-teknik-teslim` dalında tamamlandı; kendi kesin senkronizasyon paketi ayrıca onay bekler.

## Bulunan ve giderilen teslim sorunları

- Belirsiz işlem uyarısının dar ekranda kenar boşluğu ve satır/link ayrımı düzeltildi. Uzun makbuz metni ve düğmeler panel genişliğinde kırılır; klavye odağı görünür.
- Personel kartına bağlı olmayan yönetici kişisel izin sorgularına düşmez; ekip izinleri açılır ve sınır açıklanır. Kişisel izin derin bağlantısı bu kapsamı aşmaz. Personel kartı bağlı hesapların kişisel görünümü korunur. Bekleyen onaylar bağlantısı doğru iş kutusu sekmesine gider.
- Teslim scriptine `-SourceRef` eklendi: belirtilen commit `git archive` ile ayrı kaynak ağacına çıkarılır. Paket yalnız bu ağacı derler; yerel ek dosyaları/değişiklikleri devralmaz. Kaynak SHA, temiz kaynak ve orijinal çalışma klasörü durumu ayrı manifest alanlarıdır. Eski varsayılan temiz-ağaç ve açık prova davranışı korunur.

## Doğrulama

Frontend son genel koşumu: 104 dosyada 632/632 geçti. Backend genel Release koşumu: 101 birim + 353 entegrasyon testi geçti; gerçek SQL, tüm süreç aileleri ve erişim/sürüm/atomiklik kontrolleri dahildir. İki DbContext model kontrolü temiz. Lint hata vermedi; eski dört test yardımcısı uyarısı değişmedi. Yeni API, üretim backend davranışı veya migration eklenmedi.

Gerçek tarayıcı testleri ayrı `EkipzenT07Browser`, `EkipzenT07BrowserPlatform` ve `Ekipzen_t07_browser_*` yerel kataloglarında yapıldı. Gerçek API’de tek aksiyon oluşturuldu; başarılı yanıt yalnız tarayıcıya 503 olarak kaybedildi. Tekrar denemesi ikinci POST oluşturmadı; kaynakta tek kayıt bulundu, uyarı yenilemede korundu, kayıt kontrolü sonrası klavyeyle kaldırıldı ve yapay öğrenme kanıtı oluşmadı. Personel hesabı izinlerini okuyabildi; personel yönetimi API’si 403 verdi, yönetim öğrenme rotası/menüsü açılmadı.

390 px’de on süreç ekranının gerçek ilk okumaları ve gövde/ana içerik ölçümleri kaydedildi. Geniş uyum tablosu kendi kaydırma alanında kalır; HTML scroll metriği ile bu widget’ın taşması, görünür ana içeriğin taşması diye karıştırılmaz. Masaüstü ve dar ekran görüntüleri yerel doğrulama çıktılarıdır. Teknik otomasyon gerçek kullanıcı/uzman kabulü veya tam erişilebilirlik sertifikası değildir.

Kesin Git commitinden Windows site paketi üretilir; manifest dosya hash’leri, üretim frontend ve yapılandırma/dosya sınırları doğrulanır. Paket ve kontrol sonuçlarının kesin SHA/yol/hash bilgisi senkronizasyon raporunda bulunur. Üretime yayın veya üretim migration yapılmaz. [Teknik teslim kılavuzu](../../ik-rehberi-teknik-teslim.md) kaynak yeniden üretimini ve kullanıcı kabulünü açıklar.

## Son durum

**T01–T07 teknik kapsamı kapalı; bizde kalan olağan teknik geliştirme başlığı 0.** Sabit 45 satır (37 bağlı / 8 okuma) değişmedi. Kullanıcının üstlendiği gerçek insan/uzman/merkezî editör kabulü ve Turhost/Visual Studio yayını yapılmış sayılmaz. İleri API/saklama/finansal karar bağımlılıkları mevcut kayıtlarda korunur; yeni sürekli geliştirme paketi açılmaz.
