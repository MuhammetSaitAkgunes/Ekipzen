# İK rehberi — içerik sürümüne bağlı gerçek işlem kanıtı

1 Ekim 2026. [Günlük yönetim ve yardım tercihinin](2026-10-01-ik-rehberi-gunluk-yardim.md) üzerine gerçek işlemi kullanılan merkezî içerik sürümüne bağlayan ilk üç bağlantı uygulanmıştır. [Tam kapsam yayın listesi](2026-09-30-ik-rehberi-yayina-cikis.md) daraltılmaz; diğer süreçler için sürümlü işlem kanıtı açık kalır.

## İşlem bağlantıları

| Gerçek işlem | İçerik anahtarı | Kanıt ve rol |
| --- | --- | --- |
| Çalışan kartı oluşturma | `personnel-entry-tab-is` | Yeni EmployeeCreationEvidence ile aynı SaveChanges; İK yöneticisi |
| Yeni adayın ilk gerçek aşama kararı | `recruitment-first-review` | Yeni CandidateReviewEvidence ile aynı SaveChanges; İK yöneticisi |
| Bekleyen izin talebinde onay veya ret | `leave-decision-effect` | Yeni GuidanceLeaveDecisionEvidence ve karar aynı kiracı transaction'ı; mevcut onay kapsamındaki İK/ekip yöneticisi |

Bu bağlantılar tüm işe girişin, işe alımın veya izin sürecinin tamamlandığını ve kararın doğruluğunu göstermez. Çalışan kartı evrak/ücret/dış bildirim kanıtı değildir; aday kanıtı not, değerlendirme, iletişim veya işe alma kanıtı değildir. Okuma, alıştırma, sunum tercihi ve yeterlilik birbirinden ayrı kalır.

## Ekran davranışı

Üç gerçek işlem ekranındaki kapalı “Gerçek işlemde kullandığım rehber sürümü” bölümü açılana kadar yeni kaynak sorgusu göndermez. Güncel, yayımlanmış ilgili adım açıkça seçilir; seçilen sürümün açıklaması ekran içinde açılır. Alıştırma sorusu bulunması veya yanıtlanması zorunlu değildir. Yeni yayın mevcut seçimi otomatik değiştirmez; yenilemeden sonra kullanıcı güncel sürümü açıkça seçer veya bağlantıyı kaldırır.

Seçim tek başına hiçbir kanıt kaydetmez. Gerçek işlem gövdesinde yalnız isteğe bağlı `guidanceContentRevisionId` gönderilir. Yeni kart oluşturma, aday ilk karar ve izin onay/ret hook'ları başarıda kişisel işlem geçmişini yeniden okutur. Eski sürüm veya iş çakışmasında form, gerekçe/not ve seçilen sürüm korunur. Rehber bağlantısı yoksa mevcut iş kontrolleri ve eski kanıt davranışları devam eder; rehber geçmiş sürümü tahmin etmez.

Öğrenme yolculuğunda “Rehber sürümüne bağlı gerçek işlemlerim” bölümü kullanıcının kendi gerçek kayıt kimliği, işlem sonucu/zamanı ve kullanılan içerik başlığı/sürümünü gösterir. Geçmiş sürüm güncel değilse bunu açıklar; yeni sürümün başarısı saymaz. Çalışan/aday adları, not, kimlik numarası, tutar veya izin açıklaması öğrenme snapshot'ına kopyalanmaz.

## Sunucu ve veri sözleşmesi

- Çalışan oluşturma gövdesi, aday aşama PATCH gövdesi ve izin approve/reject gövdesi `guidanceContentRevisionId` alanını isteğe bağlı alır. Kullanıcı/tenant kimliği, içerik başlığı/sürümü veya “başarılı işlem” beyanı istemciden alınmaz.
- `IGuidanceOperationCommand` yalnız tanımlı üç komutta vardır. Validation pipeline'dan sonra GuidanceOperationBehavior ilgili sabit süreç/adım/işlem rolünü doğrular. Merkezî katalog kilidinde seçilen sürümün yayımlanmış, erişilebilir ve güncel olduğu yeniden kontrol edilir; başka süreç/adım veya eski/kaldırılmış/süresi dolmuş sürüm işlemden önce reddedilir. Negatif/sıfır, bulunmayan veya geçersiz seçim uygun 400/404/409 cevabı verir; kaldırma/yayın işlemiyle aynı katalog kilidi kullanılır. Gönderilmeyen/null sürüm rehbersiz mevcut iş akışını sürdürür.
- Kilit sırası merkezî katalog → işin kiracı kilididir. İzin ve adayın mevcut kiracı transaction'ları korunur. Çalışan oluşturmanın mevcut tek SaveChanges işlemi yeni kart ve kanıtı birlikte saklar. Yeni bir bağımsız tamamlandı POST'u veya geçmiş kanıtı yeniden damgalayan uç yoktur.
- İstek kapsamındaki GuidanceOperationContext yalnız sunucuda doğrulanan `{ ContentRevisionId, ContentKey, ProcessKey, StepKey, ContentVersion, Title }` snapshot'ını yeni kanıta damgalar; nested rehbersiz komut önceki bağlamı devralmaz. Handler bitince bağlam geri alınır.
- EmployeeCreationEvidence ve CandidateReviewEvidence nullable GuidanceSnapshotJson taşır. Eski satırlar null kalır. Adayın aynı kullanıcı için mevcut ilk inceleme kanıtı sonraki yeniden incelemeyle ezilmez; değişmeyen aşama yeni kanıt üretmez. Rehberle ileri aşama kararı ilk inceleme diye bağlanamaz.
- GuidanceLeaveDecisionEvidence tenant/kullanıcı/talep bazında benzersizdir. İlk kararın Approved/Rejected sonucu snapshot ile birlikte değişmeden korunur. Karar başka kullanıcıya öğrenme başarısı olarak yazılmaz.
- `GET /api/guidance/operations` Management rolündeki oturum sahibinin kayıtlarını döndürür; kullanıcı seçici ve yazma ucu yoktur. EF tenant filtresi ve UserId sınırı uygulanır. IsCurrent yalnız bugünkü içerik durumudur; geçmiş snapshot veya kanıtın doğruluğunu değiştirmez. `/api/me/data-export` kendi `guidanceOperations` listesini de içerir.
- Kaynak personel/aday/talep fiziksel olarak silinirse ilgili kanıt FK cascade ile silinir; saklama/silme politikası bu teslimde yeniden tanımlanmaz. Merkezî içerik ile tenant arasında fiziksel FK yoktur; değişmez yayın kimliği ve küçük snapshot korunur.

Bu özellik genel iş komutlarına yeni bir idempotensi protokolü eklemez. Adayın beklenen sürümü ve izin kararının bekleyen durum koşulu mükerrer kararı engeller. Çalışan oluşturmanın belirsiz ağ sonucunda yeniden gönderimini ayrı bir istek kimliğiyle koruma açık API bağımlılığıdır; kanıt kaydı bu eksikliği gizlemez. Ağ sonucu belirsiz olduğunda gerçek kaydı doğrulamadan yeni çalışan isteği kör tekrarlanmamalıdır.

## Migration ve yayın

Kiracı migration'ı `20261001112117_AddGuidanceOperationEvidence` iki eski kanıt tablosuna nullable snapshot ekler ve izin karar kanıtı tablosunu oluşturur. Eski işlemler yeni öğrenme kanıtına dönüştürülmez. Üretimde bütün kiracı kataloglarına kontrollü uygulanmalıdır; otomatik migration/seed yoktur.

Down, yeni izin kanıtı veya eski tablolarda dolu snapshot varken 51000 ile durur. Bu SQL koruması gerçek tenant veritabanında kanıtı koruyarak doğrulandı; tüm geri alma veya üretim migration'ı çalıştırılmadı. Önceki migration'dan idempotent SQL yerel olarak üretildi; modelde bekleyen değişiklik yoktur. Uzman içerik yayını, tarayıcı görsel kabulü veya Turhost yayını yapılmadı.

## Doğrulama ve sonraki işler

- Frontend: 89 dosyada 513/513 test. Kaynağın açık seçimi, kapalı panelde sorgu olmaması, yeni yayında eski seçimin korunması, yanlış/eksik kaynağın seçilmemesi, tarihsel yolculuk, üç gerçek isteğin içerik kimliği ve form/not korunması doğrulanır.
- SQL Server entegrasyonu: rehber katalog/kişisel dışa aktarım, eski rehber, işe alım ve izin gruplarında 46/46 test. Kişisel/tenant izolasyonu, eski/yanlış/kaldırılmış/süresi dolmuş içerik, no-op, eşzamanlı aday/izin kararı, eski kanıtın korunması, kaynak silinmesi ve Down koruması doğrulanır.
- Backend 91/91 birim testi; Release --warnaserror sıfır uyarı/hata. Frontend üretim derlemesi başarılı; lint sıfır hata ve dört mevcut uyarı. npm audit sıfır zafiyet. EF model uyumu, idempotent SQL üretimi ve Git fark kontrolü başarılı.

Sıradaki teslim şirket hazırlığı, puantaj ve uyum işlemlerine aynı sürümlü kanıt bağlantısıdır. Diğer süreç bağlantıları, açık olağan işlem API'leri/günlük görev envanteri, beceri/yardım uyarlaması, kaynak/uzman/yeni kullanıcı kabulü ve Turhost ilk canlı kurulum açık kalır.
