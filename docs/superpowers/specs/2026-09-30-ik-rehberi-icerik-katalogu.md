# Merkezî rehber içerik kataloğu

30 Eylül 2026. Kullanıcı kararı: tek merkezî katalog; içerik yayınını yalnız Ekipzen ekibi yönetir. Bir sürümü hazırlayan veya düzenleyen kişi yayımlayamaz; başka bir yetkili İK yöneticisi onaylar, uzman incelemesi kaydedilir.

## Yetki ve veri sınırı

GuidanceContentRevision ve ekleme şeklinde tutulan GuidanceContentEvent, PlatformDbContext'te ortak ürün verisidir. Müşteri personeli, form taslağı, iş kanıtı veya öğrenme cevabı bu kataloğa taşınmaz. Kiracı veritabanlarının şeması değişmez. Merkezî katalog için ayrı platform migrationı uygulanır; müşteri kiracısını migrate etmek bunun yerine geçmez.

Sunucu operatörü GuidanceEditorial:TenantId ile Ekipzen ekip kiracısını, GuidanceEditorial:UserIds dizisiyle izin verilen hesap kimliklerini tanımlar. Kullanıcı kimliği, aktif ekip kiracısı ve hr-admin rolü birlikte aranır. Varsayılan TenantId=0/boş liste erişimi kapatır. Müşteri veya ekip şirketinde hr-admin olmak tek başına içerik yetkisi vermez. Yönetim ekranı bu yetkiyi API'den öğrenir; asıl kontrol komut/sorgu işleyicisindedir.

Bu sunucu ayarı bir parola veya platform API anahtarı değildir; uygulama formundan değiştirilemez. Canlı kurulumda gerçek ekip hesabı ve en az iki ayrı yetkili kullanıcı doğrulanmalıdır. Aynı kişinin farklı hesap kullanmasını kimlik listesi tek başına tespit etmez; hesap ataması Ekipzen ekibinin işletim sorumluluğudur.

## İçerik ve durum sözleşmesi

İçerik kimliği sabittir; süreç onaylı on gruptan seçilir. Her sürüm hedef roller, öğrenme hedefi, önkoşullar, yapılacak iş/gerekçe, ayrıntılı örnek/karşı örnek, terimler, sonuç/kanıt, sonraki adım, düzeltme yolu, uzman ihtiyacı, kaynak türü/referansı/hükmü/uygulanma koşulu ve yürürlük aralığı taşır. İlk kapsam her içerik kaydında bir ana kaynak referansı kullanır. Şirket politikası türü merkezî bir örnektir; belirli müşterinin yürürlükteki politikası olarak sunulmaz.

Taslak eksik alanlarla kaydedilebilir; incelemeye gönderilirken öğretim ve dayanak alanları tamamlanmalıdır. Mevzuat türü HTTPS üzerinden resmî .gov.tr bağlantısı ister. Bu biçim/alan kontrolü, kaynağın güncelliği veya hükmün uygulanabilirliği hakkında otomatik hukuki doğrulama değildir; uzman incelemesi ayrıca gereklidir.

Durumlar: draft → in-review → published → retired. İncelemedeki sürüm gerekçeyle draft durumuna dönebilir. İncelemede ve yayımlanmış metin yerinde düzenlenemez. Yayımlanmış/kaldırılmış kayıttan yeni kayıt kimliği ve artan ContentVersion ile taslak açılır; önceki metin ve inceleme bilgisi korunur. Bir içerik kimliğinde tek açık taslak/inceleme vardır. Yeni sürüm temel aldığı BasedOnId'yi kaydeder; başka süreç veya başka kimlik temel alınamaz.

Her taslak düzenleyen kullanıcı hazırlayanlar listesine eklenir. Yayın komutu bu listenin tamamını kontrol eder; yalnız ilk oluşturanı kontrol edip ortak yazarın kendini onaylamasına izin vermez. Yayın için uzman adı, uzmanlık/rol, özel inceleme referansı, inceleme tarihi, yeniden inceleme tarihi ve bu sürümün dayanaklarla birlikte incelendiği açık teyit gerekir. İnceleme tarihi son metin düzenlemesinden önce veya gelecekte olamaz; yeniden inceleme süresi geçmiş onay kabul edilmez.

Güncelleme ve durum işlemleri ExpectedRevision ister. Aynı içerik kimliği tüm API instance'ları için platform veritabanında transaction sahipli sp_getapplock ile korunur. Metin/durum/uzman bilgisi ve aktör-zaman-gerekçe olayı aynı transactionda kaydedilir. Eski sürümle tekrar işlem 409 alır; istemci otomatik onay tekrarına geçmez.

## Okur ve geçmiş sürümler

GET /api/guidance/catalog: oturum rolüne uygun güncel içerik. Son yayımlanmış veya kaldırılmış kaydın yürürlük başlangıcı ve ContentVersion sırası esas alınır. Türkiye saat diliminde bugünkü tarih kullanılır. Gelecekte yürürlüğe girecek içerik erkenden güncel olmaz. Son geçerli sürüm kaldırılmış, yürürlük sonu geçmiş veya uzman inceleme süresi aşılmışsa eski yayıma sessizce geri dönülmez.

GET /api/guidance/catalog/versions/{id}: rolüne uygun yayımlanmış/kaldırılmış sürümün metnini korur; güncel değilse IsCurrent=false döner. Taslak/incelemedeki kayıt okur yolunda 404'tür. Ekran açık sürüm kimliğini korur ve güncel olmayan sürümü işlem rehberi olarak kullanmama uyarısı gösterir. Okur yanıtında hazırlayan kullanıcı kimlikleri, özel uzman belge referansı ve iç inceleme olayları bulunmaz.

GET .../access yayın ekibi yetkisini, GET/POST .../editorial yönetim listesi/oluşturmayı; PUT .../editorial/{id} taslak düzenlemeyi; POST .../editorial/{id}/submit|return|publish|retire durum işlemlerini sunar. Yönetim ekranında yazılmış ancak kaydedilmemiş değişiklikler incelemeye gönderilemez. Çakışmada form girişi korunur; güncel kayıt kullanıcı isteğiyle yeniden yüklenir. Arka plan yenilemesi açık taslağı sessizce değiştirmez.

Rehber içerikleri sayfası bütün rollere açıktır; yayın paneli yalnız Ekipzen ekibine görünür. Okumak yer imi, alıştırma, gerçek iş veya yeterlilik kanıtı üretmez. Mevcut üç karar alıştırması bu yeni metin sürümlerine kendiliğinden bağlanmaz; süreç ekranlarına ve öğrenme sürümlerine bağlantı sonraki teslimdir.

## Yayın işletimi ve açık işler

Üretimde otomatik migration/seed eklenmedi. AddCentralGuidanceContentCatalog platform migrationı, ekip yetki ayarı ve gerçek iki hesabın yayın denemesi canlı kontrol listesine dahildir. Migration Down, herhangi bir içerik kaydı varken içerik ve inceleme geçmişini silmeye izin vermez. Katalogda otomatik onaylı hukuki içerik veya uydurulmuş uzman kaydı tohumlanmaz.

Tam süreç metinlerinin hazırlanması, resmî kaynakların araştırılması, gerçek uzman incelemesi, süreç/adım ekranlarına bağlanması ve öğrenme kanıtlarının içerik sürümüne ilişkilendirilmesi açıktır. Teknik katalog teslimi, tam kapsam rehberin veya ilk canlı yayın kabulünün bitmesi değildir.
