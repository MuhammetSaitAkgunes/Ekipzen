# T06 — Belirsiz işlem sonucu, tekrar ve öğrenme aşamaları

2 Ekim 2026. Onaylanan T05 `fc49d5f`, `32b49e0`, `21186ea` commitleri `ekipzen` remote’una gönderildi, `main` fast-forward birleştirildi ve gönderildi. Yeni paket `21186ea` üzerinden `codex/ik-rehberi-tekrar-ogrenme-seviyeleri` dalında hazırlanır; kendi Git senkronizasyonu ayrıca onay bekler.

## İşlem sonucu sözleşmesi

Ortak `apiFetch` iş yazımlarında sonucu ayrıştırır. Sunucunun 4xx reddi `rejected`; istemcinin süren veya kontrol edilmemiş aynı yazımı göndermemesi `not-sent`; ağ/iptal, 5xx/408 veya başarılı kodla okunamayan yanıt `unknown` olur. Belirsizlik işlemin geri alındığını göstermez. GET/indirme ve bilinen beş kalıcılaştırmayan POST önizlemesi yazım kanıtı veya belirsiz yazım işareti oluşturmaz. Kimlik uçları ayrı oturum akışında kalır.

Mutasyonlar otomatik yeniden denenmez. 401 sonrasında mevcut tek token yenileme/tekrar korunur; yanıt veya yenileme beklenirken kullanıcı/tenant/rol kapsamı değişirse eski iş yeni kapsamla gönderilmez, eski yenileme yanıtı yeni oturumu ezmez veya silmez. Aynı kapsam, yöntem ve kaynakta süren yazımın ikinci çağrısı istemciden çıkmaz. Belirsiz yazımda gerçek kayıt kontrol edilmeden aynı kaynağa yeni gönderim engellenir.

Çalışma alanındaki ortak uyarı gerçek kaydı ve varsa makbuzu kontrol ettirir. Açık sorguları yeniden okumak tek başına sonuç teyidi değildir; kullanıcı ayrıca ilgili sonucu incelediğini belirtir. Uyarıyı kaldırmak kayıt/ödeme/karar veya kişisel öğrenme kanıtı üretmez. Farklı iş ve kayıtlar gereksiz yere tek bir genel kilitle durdurulmaz.

Uyarı kendi kullanıcı+tenant+rol kapsamında bu tarayıcı sekmesinin `sessionStorage` alanında tutulur; sayfa yenilendiğinde aynı kapsamda tekrar okunur. Yazılamazsa mevcut sekmenin belleğinde koruma sürer, kalıcılık garanti edilmez. Saklananlar yöntem, kaynak yolu, zaman ve varsa mevcut gönderim kimliğidir; form gövdesi, dosya, kişi adı veya token saklanmaz. En fazla 20 çözümlenmemiş kaynak sınırında yeni kaynak yazımı kontrol bekler; eski işaret sessizce atılmaz.

## Yazım ve sürüm matrisi

| İşlem ailesi | Mevcut sunucu sözleşmesi ve sonuç kontrolü | Tekrar sınırı |
| --- | --- | --- |
| Personel oluşturma | `CreationRequestId`, aynı gönderimin makbuzu ve içerik eşitliği; GET creation-results | Aynı kimlik/gövdeyle mevcut sonuç okunur. Yeni kimlik yeni işlem olabilir; eski sonucu kontrol etmeden üretilmez. |
| Toplu personel aktarımı | `ImportRequestId`, aynı dosya/önizleme/rehber bağı ve makbuz; GET import/results | Aynı gönderim korunur; başka dosya/kimlik aynı işlem sayılmaz. Yenilenen sayfada dosya kendiliğinden geri yüklenmez. |
| Yayınlanmış alıştırma | İçerik sürümü, `AttemptId`, beklenen deneme sayacı | Aynı kimlikle mevcut sonuç alınır; yeni yayına veya yeni denemeye otomatik taşınmaz. |
| Şirket başlangıcı ve bordro dönemi | Önizleme belirteci, mevcut kayıt/dönem tekilliği, kaynaklı kanıt | Tamamlanan başlangıcın özel no-op davranışı başka komutlara genellenmez; mevcut kaynak yeniden okunur. |
| Personel, şirket, işe alım, puantaj, izin ve uyum alt yazımları | İlgili DTO/önizleme/iş sürümü ve rol/tenant kapıları; gerekçe/dayanak gereken işlemler kendi sözleşmesini korur | 400/409'daki taslak korunur, güncel kayıt açıkça incelenir; kaynak bağlantısız eski istemcinin isteğe bağlı sürüm alanına otomatik güvence verilmez. |
| Aksiyon yazımları | Kaynak alanlarından hesaplanan Revision; rehberli PUT/PATCH/DELETE sürüm kontrolü, ileri durum kuralı | No-op bilgi kaydı kanıt üretmez; tamamlanan aksiyon yeniden açılmaz. Oluşturmanın ayrı sunucu makbuzu yoktur. |
| Finansal taslak/onay/telafi/parasal sürümler | Kaynak sürümü, önizleme/etki dosyası ve aynı tenant atomik kilitler | Taslak tekrarları yeni sürüm üretebilir. Genel exactly-once/no-op vaadi yok; belirsizlikte hesap ve geçmiş yeniden okunur. Gelişmiş finansal kilitler korunur. |
| Dış ödeme/bildirim kanıtı | Kaynak belirteci, türün son kayıt kimliği ve eklemeli geçmiş | Aynı kanıt/eskimiş form çatışması gerçek sonuçla uzlaştırılır; bankaya/resmî kuruma gönderim yapılmış sayılmaz. |
| Dosya yükleme/geri çekme, silme ve hesap hazırlığı | Kaynak metadata/geçmiş, ilgili sürüm veya önizleme; hesap hazırlığının mevcut iş sonucu | Dosya/iş tekrarının genel makbuz garantisi yok. Eksik/görünmeyen kayıt tüm yükümlülüğün tamamlandığını göstermez. |

İstemcinin makbuzlu tekrar istisnası yalnız personel oluşturma, aktarım ve yayınlanmış alıştırmadadır. Aynı gönderim kimliği korunarak kullanıcı tekrar sonucu isteyebilir; otomatik gönderim yoktur. Sunucu ayrıca içerik/dosya/sürüm eşitliğini doğrular. Makbuz bulunmaması anlık olarak işlemin kesin yapılmadığı anlamına gelmez.

## Oturum ve öğrenme aşaması

Önbellek kimliği kullanıcı+tenant+roldür. Bu kapsam değişince eski sorgular temizlenir ve eski form bileşenleri yeniden kurulur; yalnız token yenilenmesi aynı kapsamın verisini korur. Başka sekmedeki oturum değişimi native storage olayıyla izlenir.

Kaydedilmiş yer imi, kişisel inceleme beyanı, alıştırma ve gerçek işlem kanıtları sunucudan gelir. Ayrı istemci aynı kullanıcının aynı sürüm kayıtlarını okuyabilir; başka kullanıcı bu kanıtları devralamaz. Form taslağı, seçilmiş dosya ve sekmenin belirsiz işaretleri başka cihaza taşınmaz. Bu sınır yolculuk ekranında açıkça anlatılır.

Güncel adımın görünür aşaması başlangıç, inceleme, alıştırmayı yeniden çalışma, doğru alıştırma, uygulama kaydı veya doğru alıştırma+uygulama birlikte şeklinde türetilir. İnceleme/alıştırma/uygulama yine ayrı gösterilir. Salt okuma adımlarında yapay yazım kanıtı aranmaz. Eski yayın veya başka adımın kanıtı güncel aşamaya aktarılmaz; okuma hatası eksik kanıt veya başarı sayılmaz. Aşama yeni yetki, puan eşiği veya bağımsız yeterlilik onayı değildir. Önceden onaylanan uyarlanan anlatım koşulu korunur.

## Kapanış ve kontrol sahipliği

T06 teknik kapsamı tamamlanır; bizim kalan başlığımız **T07 akış doğrulaması ve teknik teslim**dir. Sabit 45 satır 37 bağlı/8 okumadır; yeni olağan geliştirme başlığı eklenmez. Kullanıcının 2 Ekim açık kararıyla Turhost/Visual Studio yayını, gerçek insan/uzman denemeleri ve kabulü kullanıcıya aittir. Merkezî rehberde içerik hazırlayan dışında yayın onayı ve gerçek uzman inceleme bilgisi kuralları kaldırılmadı. Kullanıcıya ait kontroller yapılmış veya geçmiş sayılmaz.

Doğrulama: API belirsizlik/tekrar, makbuz kimliği, tenant/rol kapsamı, sekme değişimi, depo yazılamaması, salt okunur önizlemeler, uyarı teyidi, öğrenme aşaması ve gerçek sunucu kişisel devamlılık senaryoları çalıştırıldı. Genel ön yüz koşumunda eski kör tekrar beklentisi yeni kayıt kontrolü akışına uyarlandı; testler arası kalıcı sekme işaretlerinin izolasyonu eklendi. Kesin test sayıları ve derleme/lint sonuçları senkronizasyon raporunda kaydedilir. Yeni API/migration yok; genel sunucu idempotency garantisi eklenmedi. Güvenlik ve öğrenme kapsamından tasarruf amacıyla feragat edilmedi; %7 acil düzeltme rezervi hedefi korunur.
