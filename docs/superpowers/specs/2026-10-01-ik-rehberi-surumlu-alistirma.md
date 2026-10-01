# Sürümlü alıştırmalar ve kaynaklı alt adımlar

1 Ekim 2026. [Onaylı plan](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md) ve [tam kapsam yayın listesi](2026-09-30-ik-rehberi-yayina-cikis.md) geçerlidir. Bu teslim ilk üç akıştaki on alt adımı kapsar; tüm süreç envanteri tamamlanmış sayılmaz.

## Üründeki akış

İşe girişin altı form bölümü, aday oluşturma/ilk inceleme ve izin ön inceleme/karar etkisi ekranlarında “Kaynaklı adım ve sürümlü alıştırma” açılır. Yayımlanmış soru yoksa gerçek görevin ilgili ekranda sürdüğü açıklanır. Soru otomatik yanıtlanmaz. Kullanıcı güncel sürümü açıkça seçince o sürüm kimliği sabitlenir; başka bir yayın açık soruyu ve seçimi değiştirmez. Dayanak bağlantısı aynı sürümü ayrı sekmede açar.

Ekipzen yayın panelinde bu on alt adım için başlangıç taslakları bulunur. Her biri hedef, ön koşul, işlem, gerekçe, örnek/karşı örnek, sonuç, kanıt, sonraki adım, düzeltme ve uzman yönlendirmesi içerir. Metin ve soru aynı içerik gövdesindedir. Soru, seçenekler, doğru seçenek ve açıklama inceleme kapsamına girer. Hazırlayan/düzenleyen kişi kendi sürümünü yayımlayamaz; diğer yetkili yayıncı uzman incelemesini teyit eder. Yayınlanan soru ve cevap değişmez; düzeltme yeni taslak ve bağımsız yayın gerektirir.

Mevcut kısa karar alıştırmaları korunur ve ayrı takip edilir. Eski kod sürümü yeni katalog sürümü olarak yorumlanmaz. Yeni “Kaynaklı sürümlere ait alıştırma geçmişim” yalnız oturum sahibinin bu yeni kayıtlarını gösterir. Geçmiş sürümler korunur, yeni soruya aktarılmaz. Doğru yanıt, okuma beyanı ve gerçek işlem kanıtı birbirinin yerine geçmez; yeterlilik onayı üretilmez.

## Sunucu sözleşmesi

| Uç | Davranış |
| --- | --- |
| `GET /api/guidance/catalog/steps/{processKey}/{stepKey}` | Güncel, role uygun yayımlanmış alt adım alıştırması; yoksa `200 null`. Bilinmeyen adım 404, süreç rolü dışı 403. |
| `GET /api/guidance/catalog/versions/{id}/practice` | Kimliği sabitlenmiş yayımlanmış/geçmiş soru ve kullanıcının kendi son denemesi. Açıklama yalnız kendi denemesi varsa verilir. |
| `POST .../versions/{id}/practice/attempts` | `selectedChoice`, `expectedAttemptCount`, `attemptId` alır. Yalnız güncel içerikte yanıtlanabilir. Başlık, süreç, soru veya doğruluk istemciden alınmaz. |
| `GET /api/guidance/catalog/practice-progress` | Kendi kiracısında kendi kullanıcıya ait içerik sürümü bazlı son deneme ve güncellik. |

İlk izin alıştırmaları İK/ekip yöneticisi; işe giriş ve işe alım alıştırmaları yalnız İK yöneticisine açıktır. İçeriğin hedef rolü ayrıca kontrol edilir. Çalışan role eklenmiş olsa bile yönetici alıştırması API'sine erişemez. İçerik kimliği `{processKey}-{stepKey}` ve tanımlı alt adım eşleşmesi sunucuda doğrulanır; ilgisiz içerik aynı adıma bağlanamaz.

Okur katalog gövdesi `Practice=null` döndürür; doğru yanıt ve açıklama buradan sızmaz. Yayın ekibinin özel editoryal yanıtı tam soru ve doğru seçeneği taşır. Alıştırma yanıtlarında doğru seçenek indeksi verilmez; cevap sonrası doğruluk ve açıklama verilir. Bu mekanizma bir sınav güvenliği veya sertifika sistemi değildir.

Kayıt önce merkezî içerik anahtarını, sonra tenant içindeki kullanıcı+sürüm anahtarını kilitler. Aynı son deneme kimliği, seçim ve beklenen sayaçla tekrar gönderilirse aynı sonuç döner; sayaç artmaz. Aynı kimliğin farklı yanıtı veya başka oturumdaki eski sayaç 409 alır. Daha sonraki denemeden sonra eski isteğin tekrarı da 409 alır. Tam deneme günlüğü tutulmaz; her içerik sürümünde sayaç ve son deneme tutulur. Geçmiş içerik sürümleri ayrı kayıtlarıyla kalır.

Yeni yayın, kaldırma, inceleme süresinin dolması veya yürürlük dışında kalma yeni yanıtı engeller. Son sürüm kaldırılınca önceki yayına otomatik dönülmez. Arayüz 409/ağ hatasında seçimi korur; başarısız ağ isteğini aynı deneme kimliğiyle tekrar gönderir. Kullanıcı açıkça kayıt yeniler ve gerekirse tekrar seçim yapar; başka sürüme otomatik POST yapılmaz.

## Veri ve dağıtım

`GuidancePublishedPracticeProgress` tenant filtreli kişisel kayıttır. Kullanıcı, platform içerik kimliği, sabit anahtar/süreç/adım, sürüm, başlık snapshotı, sayaç, son seçim/doğruluk, son UTC zaman ve son deneme kimliğini saklar. Tenant+kullanıcı+sürüm kimliği benzersizdir. Platforma çapraz veritabanı FK kurulmaz; gerçek çalışan/aday verisi kopyalanmaz.

`/api/me/data-export` içinde `guidancePublishedPractices` koleksiyonu kendi kayıtlarını içerir; zaman `Z` ile UTC olarak dışa aktarılır. Tenant migrationı `20261001081932_AddGuidancePublishedPractices` kontrollü kurulum adımıdır. Down, kayıt varsa SQL 51000 ile durur; geçmişin kör silinmesini önler. Platform model değişikliği yoktur; yeni soru alanları mevcut JSON gövdede opsiyoneldir. Eski içerikler okunmaya devam eder. Üretimde otomatik migration/seed eklenmedi.

## Araştırma ve inceleme sınırı

Resmî kaynaklar 1 Ekim 2026'da tekrar okundu:

- [KVKK — kişisel veri işleme ilkeleri](https://www.kvkk.gov.tr/Icerik/6606/General-Principles-in-Processing-of-Personal-Data): doğruluk, amaçla bağlantı ve ölçülülük.
- [ÇSGB — 4857 sayılı İş Kanunu](https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf): m.8 sözleşme/çalışma koşulları, m.75 özlük dosyası ve gizlilik. Departman/davet/IBAN ekran kontrolleri bu maddelerin birebir emri olarak sunulmaz; Ekipzen uygulama örnekleridir.
- [ÇSGB — Yıllık Ücretli İzin Yönetmeliği](https://csgb.gov.tr/Media/trddpibj/y%C4%B1ll%C4%B1k-%C3%BCcretli-izin-y%C3%B6netmeli%C4%9Fi.pdf): mevcut izin başlangıç kaynağı; farklı izin türlerine aynı koşul uygulanmaz. Sunucu rezervasyon/defter davranışı ayrıca ürün davranışıdır.

Taslaklar gerçek uzman incelemesi yapılmış gibi işaretlenmez. Soru seçeneği hukuki uzman kararı yerine geçmez. Gerçek inceleme referansı ve bağımsız yayın onayı alınmadan okura açılmaz.

## Kalan ürün teslimleri

Sıradaki geliştirme: diğer süreçlerin alt adım envanterini aynı altyapıya taşıma ve yardım düzeyi tercihini kişisel yolculuğa ekleme. Açık kalan genel kapsam: sürüme bağlı gerçek iş/kanıt ilişkisi; bütün envanterin açıklama/karar/düzeltme kapsamı; günlük iş/API kapıları; gerçek uzman yayın kabulü ve firma sahibi/yeni mezun görev denemeleri; Turhost kurulumu ve tamamlanmış kaynak sürümünden yayın. Bu on adım, tüm ürün için tamamlanma yüzdesi veya canlı kabulü değildir.

## Doğrulama

- Genel backend koşumu: 91 birim ve 293 entegrasyon testi geçti. Ardından eklenen izin yönetici/çalışan sınırı testiyle son katalog/dışa aktarım koşumu 9/9 geçti; genel koşum bu son testi içermiyordu.
- Frontend genel koşumu: 86 dosyada 490/490 test geçti. Yeni yayın sırasında açık sorunun sabit kalması, ağ tekrarında aynı deneme kimliği, eski sürümün ayrı geçmişi ve taslak sorunun incelemeye taşınması doğrulandı.
- Backend Release derlemesi: 0 uyarı/0 hata; frontend üretim derlemesi başarılı. Lint 0 hata/önceden var olan 4 uyarı, npm audit 0 açık.
- AppDbContext ve PlatformDbContext için bekleyen model farkı yok. İdempotent tenant SQL scripti üretildi. Kayıt varken Down koruması gerçek test SQL veritabanında 51000 ile durdu ve kayıt korunmuş olarak doğrulandı.

Bu teknik kontroller gerçek uzman incelemesi, gözlemlenmiş novice kullanıcı kabulü veya Turhost canlı kurulum kanıtı değildir.
