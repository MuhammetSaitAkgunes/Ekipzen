# T05 — Günlük aksiyon ve görev kapsamının kapanışı

2 Ekim 2026. Onaylanan T04 commitleri `c967361`, `ea0c1f9`, `c82c21b` `ekipzen` remote’una gönderildi, `main` fast-forward birleştirildi ve gönderildi. Yeni paket `c82c21b` üzerinden `codex/ik-rehberi-gunluk-aksiyon-kapanisi` dalında hazırlanır; kendi senkronizasyonu ayrı onay bekler.

## Ürün sonucu

G02 kapandı: sabit 45 olağan satırın **37’si bağlı, 8’i okuma**; açık işlem bağlantısı yok. Bu, bütün öğrenme kabulünün tamamlandığı anlamına gelmez. T06 ve T07 bizim kalan iki teknik başlığımızdır; Turhost/Visual Studio T08 kurulumunu kullanıcı manuel üstlendi. Gerçek uzman, farklı merkezî editör yayını ve deneyimsiz kullanıcı kabulü ayrı bekler.

Manuel aksiyon oluşturma, bilgi düzeltme, durum ilerletme ve silme `daily/task-action` dersinin güncel, yayınlanmış sürümüne bağlanabilir. Öğrenme geçmişi işlemleri ayrı Türkçe başlık ve kaynak ID ile gösterir. Kaydı okumak, bağlantıya tıklamak veya alıştırma kanıtı gerçek işlem değildir.

Yeni **Bilgileri düzelt** akışı mevcut PUT API’sini açar. Başlık, kaynak, sorumlu, ekran bağlantısı, vade etiketi, öncelik ve önerilen iş düzeltilebilir; durum korunur. Hata halinde taslak kaybolmaz. Güncel kayıt kullanıcı tarafından yeniden okunur, bütün takip alanlarıyla form karşılaştırılır ve işlem tekrar teyit edilir. Durum/silme ekranı da gerçek kaynak ve etki teyidi gösterir. Kaynak dersi değiştirmek önceki teyidi düşürür.

Tamamlanan aksiyonun yeniden açılmasına ilişkin eski ileri yönlü iş kuralı değiştirilmedi. Hatalı tamamlama ilgili gerçek İK kaydının desteklenen düzeltme yolunda incelenir; gerekiyorsa yeni takip kaydı açılır. Silme yalnız takip kaydını kaldırır; gerçek bordro/izin/uyum kaydını veya önceki kişisel öğrenme kanıtını silmez.

## API ve atomiklik

- POST komutunda ve PUT/PATCH gövdesinde isteğe bağlı `GuidanceContentRevisionId`; DELETE query’sinde aynı alan. Bağlantısız eski istemciler korunur.
- `GlobalActionDto.Revision`, kimlik/takip alanları/durum ve denetim zamanlarından hesaplanır; yeni veritabanı kolonu veya migration yok.
- PUT/PATCH gövdesi ve DELETE query’si `ExpectedRevision` kabul eder. Rehberli değişikliklerde gerekli; gönderildiği her çağrıda doğrulanır. Aynı tenant `global-actions` kilidiyle eski/eşzamanlı form 409 alır.
- Gerçek kayıt ve kişisel rehber kanıtı aynı veritabanı işlemi içindedir. Kanıt yazımı başarısızsa gerçek kayıt da geri alınır. Değişiklik olmayan PUT yeni kanıt üretmez; aynı veya geriye durum geçişi eski 409 davranışını korur.
- Yetki sınırı aynı: oluşturma/bilgi düzeltme/silme İK yöneticisi, durum İK yöneticisi veya yönetici; çalışan yalnız okur. Tenant filtresi ve kişisel kanıt sahipliği korunur.

## Günlük görevlerin gerçek kapsamı

Aksiyon görevi `action-followup:{id}` anahtarıyla güncel kaynak/sorumlu/durumdan her okumada üretilir. Açık veya haftalık durum aynı işi taşır; başka yetkili kullanıcı tamamlayınca diğer kullanıcılarda da kapanır. Sahip adı bir hesap ataması veya bildirim gönderimi değildir. `Due` metni DateOnly/yasal son tarihe çevrilmez. Ekranda Bugün/Gecikti sayaçları yalnız açık takip etiketlerini sayar; tamamlanan sayaç bütün listedir, uydurulmuş haftalık süre değildir.

Diğer kaynaklar mevcut kaynak durumuna göre davranır: izin kararı, uyum değişikliği/yeniden eksikliği, şirket/personel hazırlığı, puantaj incelemesi, bordro ve ayrılış dosyaları, aday incelemesi, geçerli finansal sürümlerin ayrı ödeme/bildirim kanıtları. Bir kaydın işlemini tamamlamak diğer süreçleri veya dış yükümlülükleri tamamlamaz; her olaya yapay görev eklenmez. Güvenilir kayıtlı tarih dışında son tarih üretilmez. Boş liste yalnız desteklenen kaynakların güncel durumudur.

## Doğrulama ve kalan sınırlar

17 backend entegrasyon testi (yeni üç senaryo + mevcut aksiyon/günlük görev testleri) geçti: okuma/no-op kanıt ayrımı, eski form, eşzamanlı tek yazım, başka kullanıcıyla görev kapanışı, tenant/rol sınırı, silme sonrası tarihsel kanıt, yanlış ders reddi ve kanıt hatasında gerçek yazımın atomik geri alınması.

38 ilgili frontend testi geçti: oluşturma kaynak sürümü, 409 sonrası taslak/yeniden inceleme, durum ve silmede kaynak seçimi/teyit, mevcut rol/kayıt odağı, ortak günlük görev ve öğrenme bileşenleri. Backend Release ve frontend TypeScript/Vite derlemeleri başarılı; lint hata yok, önceden bulunan dört test yardımcısı uyarısı devam eder. OpenAPI şeması gerçek API’den güncellendi.

Genel oluşturma yeniden gönderim makbuzu/idempotency eklenmedi; bağlantısız eski istemcinin gönderilmeyen sürüm alanına otomatik eşzamanlılık garantisi atfedilmez. Genel ağ sonucu/yarım işlem/öğrenme seviyesi T06, süreçler arası otomasyon/klavye/dar ekran ve kesin committen teslim T07 kapsamındadır. Kullanım rezervi hedefi %7 olarak korunur; tüketim garantisi veya hesaba uygulanmış kota değildir. Kapanan kapsamı yeni özelliklerle genişletmek yerine kalan iki başlık sırayla tamamlanır.
