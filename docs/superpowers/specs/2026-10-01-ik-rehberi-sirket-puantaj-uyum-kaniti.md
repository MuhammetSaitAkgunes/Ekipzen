# İK rehberi — şirket, puantaj ve uyum işlem kanıtları

1 Ekim 2026. [İlk üç sürümlü işlem bağlantısının](2026-10-01-ik-rehberi-surumlu-islem-kaniti.md) üzerine yedi gerçek komut bağlandı. [Tam kapsam yayın listesi](2026-09-30-ik-rehberi-yayina-cikis.md) korunur; bu teslim bütün süreçlerin tamamlandığı veya kullanıcı yeterliliğinin onaylandığı anlamına gelmez.

## Gerçek işlemler

| Komut / işlem anahtarı | Yayımlanan içerik anahtarı | Kanıt sınırı |
| --- | --- | --- |
| Başlangıç kayıtları / `tenant-setup` | `company-initial-setup` | Gerçekten eklenen şirket profili, izin türü/tam gün tatil sayıları ve onaysız bordro taslağı |
| Puantaj oluşturma / `attendance-create` | `attendance-entry-review` | Kaydedilen yeni satır |
| Puantaj düzeltme / `attendance-update` | `attendance-entry-correction` | Gerçekten değişen satırın her ayrı düzeltmesi |
| Uyum takip kaydı / `compliance-create` | `compliance-record-review` | Yeni takip kaydı; belge dosyasının varlığı/onayı değildir |
| Uyum bilgileri / `compliance-update` | `compliance-record-correction` | Ad, son tarih veya risk değişikliği |
| Uyum sorumlusu / `compliance-owner` | `compliance-record-correction` | Ayrı istekte gerçekten değişen sorumlu |
| Uyum durumu / `compliance-status` | `compliance-status-review` | Gerçek durum değişimi; belge doğrulaması veya dış bildirim değildir |

Şirket işlemi yalnız başlangıç şablonunu kapsar; profil/departman/erişim düzenlemelerine otomatik kanıt yazılmaz. Puantaj kanıtı fazla mesainin hukuki uygunluğu veya bordro aktarımının tamamlandığı anlamına gelmez.

## Ekran ve kaynak sözleşmesi

Gerçek işlem ekranlarında kapalı kaynak bölümü açılana kadar katalog sorgusu gönderilmez. İlgili güncel yayın açıkça seçilir; açıklama seçilen kimlikten okunur. Seçim, açıklamayı açma ve puantaj sunucu önizlemesi kanıt oluşturmaz. Puantaj önizleme gövdesi kaynak kimliğini taşımaz; yalnız son POST/PUT taşır.

Kurulum ve uyum belge formunda kaynak değişikliği ön kontrol teyidini sıfırlar. Puantajda kaynak değişimi eski önizlemeyi sıfırlar. Uyumda yeni durum işlemi yeni seçimle başlar. Yeni katalog yayını açık formdaki seçimi sessizce değiştirmez. Bayat kaynak reddinde form ve seçilen kimlik korunur.

Uyum belge bilgisi PUT'u ve sorumlu PATCH'i iki ayrı işlem/transaction'dır. Her gerçek değişiklik kendi kanıtını oluşturur. İlk istek kaydedilip ikincisi başarısız olursa ekran kısmi sonucu açıklar ve güncel kaydın yeniden açılmasını ister. Tek atomik form veya iki işlemin başarıyla tamamlandığı iddiası üretilmez. İlk istek no-op ise kanıt oluşturmaz.

## Sunucu ve veri yaşam döngüsü

- `POST /api/setup` isteğe bağlı `InitializeTenantSetupCommand` gövdesini ve `guidanceContentRevisionId` alanını alır; önceki boş POST desteklenir. Puantajda bu alan ortak `AttendanceEntryModel` içindedir. Uyum oluşturma, düzenleme, durum ve sorumlu gövdelerinde isteğe bağlıdır. Kullanıcı ve tenant kimliği sunucudan gelir.
- Mevcut GuidanceOperationBehavior ilgili süreç/adım, erişim ve güncel yayımlanmış kaynak kontrolünü merkezî katalog kilidinde yapar. Kilit sırası merkezî katalog → tenant işlemidir. Kaynak yoksa mevcut iş akışı sürer; geçmiş işlem sonradan damgalanmaz.
- Yeni üç typed tablo `GuidanceSetupEvidences`, `GuidanceAttendanceEvidences`, `GuidanceComplianceEvidences` tenant filtreli ve kullanıcıya aittir. Küçük içerik snapshot'ı yeni gerçek kayıtla aynı SaveChanges içinde yazılır. Puantaj/uyum kanıtı kaynak FK üzerinden cascade silinir. Ayrı tablolar SQL Server'da çalışan kaydından iki farklı kaynağa giden çoklu cascade yolunu önler. Kurulum kanıtı birden çok kayıt grubunu temsil eder; tek profil veya ayar kaydına FK ile bağlanmaz, tenant veri yaşam döngüsünde kalır.
- Kurulumun tenant kilidinde zaten var olan kayıtlar tekrar eklenmez. Kanıt yalnız en az bir gerçek ekleme varsa oluşur; eklenen sayılar planlanan sayılardan değil handler'ın gerçek eklemelerinden alınır. Bordro seti onaysız kalır.
- Puantaj düzeltmesi mevcut beklenen satır sürümünü kilit altında kontrol eder. Hesaplanan hedef değerler mevcut değerlerle aynıysa kayıt/kanıt/sürüm değişmez. Gerçek farklı değişiklikler ayrı kanıtlardır; bayat sürüm reddi kanıt üretmez.
- Uyum oluşturma personel bazında; diğer üç yazım ortak belge kilidinde seri çalışır. Trim edilmiş aynı ad/tarih/risk veya aynı sorumlu no-op olur. Mevcut aynı durum 409 davranışı korunur. Uyumun eski istemciden kör yazımı için beklenen sürüm sözleşmesi bu teslimde eklenmez; açık API kuyruğundadır.
- Şirket ve uyum yazımları yalnız İK yöneticisindedir. Puantajda mevcut Management + personel ekip kapsamı korunur. Uyum inceleme içeriğine erişim yazma yetkisi vermez.
- `GET /api/guidance/operations` ve kendi veri dışa aktarımı üç yeni tabloyu aynı kişisel sınırla okur. Her kayıt kararlı `evidenceKey` taşır; aynı satırın birden fazla düzeltmesi ayrı görünür. Kurulumda `resourceId` şablon yılıdır ve ekran bunu yıl olarak gösterir; `setupChanges` gerçek eklenen sayıları verir. Diğer kaynaklarda kayıt kimliğidir. Personel adı, sorumlu adı, not, belge içeriği ve parasal tutarlar öğrenme snapshot'ına kopyalanmaz.

## Migration, doğrulama ve kalan işler

Kiracı migration'ı `20261001121120_AddGuidanceProcessOperationEvidence` üç tabloyu oluşturur. Üretimde kontrollü uygulanmalıdır; bu teslimde üretim migration'ı çalıştırılmadı. Down herhangi yeni kanıt varken 51000 ile durur; SQL koruması gerçek tenant veritabanında veri korunarak doğrulandı. Önceki migration'dan idempotent SQL üretildi ve EF model uyumu doğrulandı.

SQL Server entegrasyonunda rehber kataloğu, şirket kurulumu, puantaj ve uyum grupları 40/40; backend birim testleri 91/91 geçti. Eşzamanlı kurulum/no-op, gerçek eklenen sayılar, salt okunur önizleme, yanlış adım, beklenen puantaj sürümü, iki ayrı düzeltme, uyumun bağımsız istekleri, aynı değer/durum, kişi/tenant ve rol sınırı, kaynak silinmesi, kendi dışa aktarımı ve Down koruması kapsanır. Frontend 89 dosyada 520/520 geçti; yeni senaryolar kaynak kimliğinin gerçek istekte taşınması, kaynak seçiminde ön kontrolün yenilenmesi, bayat sürümde formun korunması, önizlemenin ayrılması, kısmi uyum kaydı ve bir satırın ayrı düzeltmelerini kapsar. Release --warnaserror sıfır uyarı/hata ve üretim frontend derlemesi başarılıdır. Lint sıfır hata ve dört mevcut uyarı; npm audit sıfır zafiyet. Tarayıcı görsel kabulü, gerçek uzman yayını ve Turhost yayını yapılmadı.

Sıradaki geliştirme bordro ve ayrılış işlemlerinin sürümlü öğrenme kanıtlarıdır. Diğer gerçek işlem bağlantıları, olağan işlem API kapıları/günlük görev envanteri, kanıta dayalı beceri/yardım uyarlaması, kaynak/uzman/yeni kullanıcı kabulü ve Turhost ilk canlı kurulum açık kalır.
