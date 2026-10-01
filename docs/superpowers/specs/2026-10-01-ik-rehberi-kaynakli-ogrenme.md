# Kaynaklı başlangıç içerikleri ve sürüme bağlı inceleme yolculuğu

1 Ekim 2026. Bu teslim onaylı tam kapsam planını daraltmaz. On süreç için birer başlangıç metni, süreç/katalog geçişleri ve kişisel inceleme beyanı sağlar; bütün alt adımların eğitimi veya bağımsız yeterlilik kabulü tamamlanmış sayılmaz.

## Kaynaklı taslakların ürün akışı

Ekipzen yayın panelindeki “On süreç için kaynaklı başlangıç taslakları”, şirket hazırlığı, işe alım, işe giriş, personel, izin, puantaj, bordro, uyum, ayrılış ve günlük yönetim için formu doldurur. Kaydetme ayrıca kullanıcı işlemi gerektirir ve yalnız mevcut taslak oluşturma API'sini kullanır. Kendiliğinden seed, inceleme gönderimi, uzman kaydı veya yayın yapılmaz. Aynı içerik kimliği mevcutsa başlangıç düğmesi kapanır; mevcut yayımlanmış/kaldırılmış kayıttan yeni sürüm hazırlanır.

Her metinde hedef/önkoşul, uygulama işi ve gerekçesi, ayrıntılı anonim örnek, hatalı örnek, terimler, sonuç ve kanıt ayrımı, sonraki adım, düzeltme yolu, uzman ihtiyacı ve uygulanma koşulu vardır. Günlük takip sırası iyi uygulama olarak etiketlenir; bir görev kartı kanuni yükümlülük veya tahmini kanuni süre üretmez. Diğer metinlerin resmî dayanağı, Ekipzen'in kendi ekran/izlenebilirlik yöntemiyle karıştırılmaz. Vergi, tazminat, özel çalışma rejimi veya özel sigortalılık için tek kaynakla tam hukuki hüküm verilmez.

Araştırma tarihi içerik taslağının hazırlandığı tarihi gösterir; uzman inceleme tarihi değildir. Taslaktaki yürürlük başlangıcı yayın öncesinde ayrıca kontrol edilir. Bağlantıların erişilebilirliğini kontrol etmek, değişiklik ve somut uygulanabilirlik konusunda uzman onayının yerine geçmez.

## Araştırılan resmî kaynaklar

| Süreç | Ana kaynak | İnceleme konusu |
| --- | --- | --- |
| Şirket | [KVKK — Veri Sorumlusu Kimdir](https://www.kvkk.gov.tr/Icerik/2032/Veri-Sorumlusu-Kimdir) | Amaç ve işleme yöntemini belirleyen sorumlunun ayrılması |
| İşe alım | [KVKK — Kişisel Veriler](https://www.kvkk.gov.tr/Icerik/2050/Kisisel-Veriler) | İşleme şartının belirlenmesi; otomatik rıza varsaymama |
| İşe giriş / ayrılış | [SGK — İşverenin Yükümlülükleri](https://www.sgk.gov.tr/Content/Post/d9d838d8-6585-40f5-bbcc-47bd43c59bb4/Isverenin-Yukumlulukleri-2022-05-15-06-17-29) | Olağan 4/a dış bildirimlerinin uygulama kaydından ayrılması; istisnaların ayrıca incelenmesi |
| Personel | [KVKK — General Principles in Processing of Personal Data](https://www.kvkk.gov.tr/Icerik/6606/General-Principles-in-Processing-of-Personal-Data) | Doğruluk/güncellik ve ölçülülük |
| İzin | [ÇSGB — Yıllık Ücretli İzin Yönetmeliği](https://csgb.gov.tr/Media/trddpibj/y%C4%B1ll%C4%B1k-%C3%BCcretli-izin-y%C3%B6netmeli%C4%9Fi.pdf) | m.20 kayıt ve m.9 koşullar; farklı izin türlerini ayırma |
| Puantaj | [ÇSGB — İş Kanunu SSS](https://csgb.gov.tr/sikca-sorulan-sorular/calisma-genel-mudurlugu/%C4%B1s-kanunu/) | Çalışma düzeni/fazla çalışma; ham saati tek başına hüküm saymama |
| Bordro / uyum | [ÇSGB — 4857 sayılı İş Kanunu](https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf) | m.37 pusula ve m.75 özlük/gizlilik; hesap veya belge etiketini dış kanıt saymama |
| Günlük yönetim | [KVKK — Aydınlatma Yükümlülüğü](https://www.kvkk.gov.tr/Icerik/2033/Aydinlatma-Yukumlulugu-) | Bağlantılı veri toplama yükümlülüğü; takip sırası ayrıca Ekipzen iyi uygulaması |

Metinler kaynakların kopyası değildir; uygulamanın olağan işlemlerine ilişkin başlangıç öğreticisidir. Resmî kaynak araştırması 1 Ekim'de yapıldı. Taslakların tamamı gerçek uzman incelemesi ve farklı yetkili Ekipzen yöneticisinin onayından sonra okur kataloğuna açılır.

## Süreçten içeriğe ve tekrar sürece

Süreç haritası ve ilgili ayarlar, aday, işe giriş, personel, izin talebi, puantaj, bordro, uyum, ayrılış ve günlük iş ekranları kataloğun `?process=` filtresine bağlanır. Okur açık içerik sürümünü `?version=` ile korur. Filtre değişimi kullanıcının açık seçimiyle sürüm seçimini kaldırır. Güncel içerikten ilgili süreç ekranına dönüş yalnız rolün erişimine uygun bağlantıyı sunar; çalışan/ekip yöneticisine İK yöneticisi ekranı önerilmez. Geçmiş içerik otomatik işlem başlatmaz.

Bu bağlantı ilk kapsamda süreç düzeyindedir. Personel formundaki altı adımın mevcut alıştırmaları ve diğer karar alıştırmalarının ContentVersion değerleri katalog sürümüyle eşitlenmez. Adım/alıştırma ve gerçek işlem snapshotlarının hangi içerik sürümüyle yapıldığını izleme ayrı teslim olarak kalır.

## Kişisel inceleme beyanı

`GET /api/guidance/catalog/readings`, kullanıcının kendi kiracısındaki kendi inceleme beyanlarını döndürür. `POST /api/guidance/catalog/versions/{id}/readings` gövdesinde `confirmed=true` zorunludur. Ekranı açmak, linke gitmek veya metni görüntülemek kayıt oluşturmaz. Güncel olmayan, süresi dolmuş, kaldırılmış, hedef role uygun olmayan veya taslak içerik için yeni kayıt alınmaz.

GuidanceContentReading, AppDbContext'te tenant filtreli kişisel veridir. Platform sürüm kimliğini, sabit içerik/süreç kimliğini, sürüm numarasını, başlık snapshotını ve ilk beyanın UTC zamanını saklar. Ayrı veritabanları arasında FK kurulmaz. Sunucu metadatası kullanılır; istemci başlık/süreç/versiyon veremez. Tenant+kullanıcı+sürüm kimliği benzersizdir. Tekrar veya eşzamanlı gönderim aynı kaydı döndürür ve ilk inceleme tarihini güncellemez.

Kayıt sırasında yayın/kaldırma ile yarışma merkezî içerik anahtarı kilidiyle, kişisel tekrarlar tenant veritabanı kilidiyle korunur. Kilit sırası platform → tenant'tır. Güncellik sorgusu yalnız ilgili içerik anahtarlarının sürümlerini okur; tek sürüm işleminde bütün kataloğu transaction içinde kilitlemez. Kişisel kayıt tenant transactionında kaydedilir; kiracı ve platform arasında dağıtık transaction veya ortak öğrenme tablosu oluşturulmaz.

Yeni sürüm yayımlandığında eski inceleme beyanı eski kimlikte kalır ve güncel olmadığı gösterilir; yeni metin incelendi diye taşınmaz. İki sürüm ayrı kayıt olabilir. İçerik kaldırıldığında geçmiş korunur ve yeni beyan engellenir. Okur kataloğu ve öğrenme yolculuğu bu kayıtları, gerçek işlem/karar alıştırmalarından ayrı bölümde gösterir. Beyan, anlama sınavı veya bağımsız yeterlilik onayı değildir.

Kendi veri dışa aktarımı `guidanceContentReadings` koleksiyonunu içerir. Başka kullanıcı veya şirketin beyanı sorgulanamaz. Yeni tenant migrationı kontrollü kurulum adımıdır; platform migrationı bunun yerine geçmez. Down, inceleme kaydı varken geçmişin silinmesini engeller; otomatik üretim migrationı eklenmedi.

## Açık teslimler

Gerçek uzman kabulü/yayın, süreçlerin bütün alt adımlarını açıklayan içerik envanteri, sürümlü alıştırma/gerçek iş bağlantıları, yardım tercihi, günlük iş/API kapsamı ve novice görev denemeleri hâlâ açıktır. Bu başlangıç metinleri veya okuma sayısı tam kapsam canlı yayın kabulü değildir.
