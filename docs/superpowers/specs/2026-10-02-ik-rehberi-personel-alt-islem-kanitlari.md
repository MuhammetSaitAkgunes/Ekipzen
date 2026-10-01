# İK rehberi — personel alt işlemleri ve günlük işler

2 Ekim 2026. Toplu aktarım paketi main'e alındıktan sonra, mevcut personel kartı ve evrak işlemleri öğrenme yolculuğuna bağlandı. Bu teslim tüm öğrenme kapsamının veya uzman kabulünün tamamlandığı anlamına gelmez.

## Gerçek işlem ve kaynak bağlantısı

| İşlem | Sunucu komutu | Kullanıcının açıkça seçtiği kaynak |
| --- | --- | --- |
| Kart düzeltme; departman/yönetici dahil | `PUT /api/employees/{id}` | `personnel / record-review` |
| Özlük belgesi yükleme | `POST /api/employees/{id}/documents` | `personnel-entry / tab-evrak` |
| Gerekçeli belge geri çekme | `POST /api/employees/{id}/documents/{documentId}/withdraw` | `personnel-entry / tab-evrak` |

Her gerçek değişiklik `GuidancePersonnelEvidences` içinde oturum sahibi, çalışan/belge numarası, işlem türü, UTC zaman, değişen alan adları ve varsa gerekçeyle saklanır. İlk kayıt değiştirilmez; sonraki işlem ayrı satırdır. Otomatik eski/yeni kişisel değer kopyası tutulmaz; kullanıcı gerekçesi serbest metindir. Rehber seçilmediyse kaynak snapshotı boştur: olağan işlem geçmişte görünür, kişisel öğrenme kanıtı sayılmaz.

Seçilen kaynak güncel merkezî yayın, doğru süreç/adım ve rol kapısından geçer. Kaynak başlığı ve sürümü iş kaydıyla birlikte snapshot olarak saklanır. Daha sonraki yayın geçmiş kanıtı güncel sürüme taşımaz. Okuma, kaynak seçimi, alıştırma, başarısız işlem veya değişmeyen kartı kaydetme gerçek işlem kanıtı üretmez. Mevcut kişisel işlem geçmişi ve veri dışa aktarımı kaynaklı personel kanıtlarını da okur; bu kayıtlar bağımsız yeterlilik/uzman onayı değildir.

## Kartı güvenle düzeltme

İK yöneticisine dönen kart GET yanıtı `recordToken` taşır; salt okur roller bu belirteci almaz. Token, düzenlenebilir kart ve profil değerlerinin deterministik özetidir; fotoğraf ayrı işlem olduğu için dahil değildir. Mevcut frontend formu ilk yüklenen kartı ve tokenını birlikte saklar; arka plan yenilemesi taslağın alanlarını veya tokenını değiştirmez. Genel kaydetme durum bilgisini ve formda düzenlenmeyen acil durum yakınlığını aynı başlangıç kartından korur. Boş TC/profil seçimleri kendiliğinden varsayılan değerle doldurulmaz.

Güncelleme şirket içindeki organizasyon yazım kilidi altında tokenı tekrar karşılaştırır. Eski token 409 döner; kullanıcı taslağı korunur. Güncel kart ayrı sekmede incelenebilir; kullanıcı açıkça “Taslağı bırakıp güncel kartı yükle” seçerse form yeniden hazırlanır. Kaydetme sonrası aynı eski tokenın tekrarı ikinci kanıt üretmez. Genel kart düzeltmesi durum/işten ayrılış/ücret telafisi işlemlerinin kapılarını kaldırmaz.

Rehberli düzeltmede `expectedRecordToken` ve en çok 500 karakterlik gerekçe zorunludur. Eski API istemcilerinin kaynaksız güncelleme sözleşmesi korunur; token göndermeyen eski istemciye bu eski-form koruması uygulanmaz. Güncel frontend token gönderir. Değişmeyen değerler kanıt oluşturmaz.

`GET /api/employees/manager-choices?exceptEmployeeId=...` yalnız şirketin aktif ve giriş hesabı bağlantısı olan çalışanlarını sunar; kartın kendisi hariç tutulabilir. Yönetici atanması her çalışan için zorunlu kabul edilmez. Mevcut sunucu referans/döngü kontrolleri ve kalıcı erişim eşitlemesi sürer. `GET /api/employees/{id}/personnel-history` İK yöneticisine şirket içindeki ilgili kartın son 100 işlemini sunar; kişisel öğrenme listesi yalnız işlem sahibine aittir.

## Belge atomikliği

Yükleme ve geri çekme aynı çalışanın evrak kilidini kullanır. İş kaydı ve işlem kanıtı tek transaction içindedir. Kanıt yazılamazsa kart, yeni evrak kaydı veya geri çekme durumu geri alınır. Başarısız yüklemenin yalnız yeni fiziksel dosyası temizlenmeye çalışılır; temizleme başarısızsa depo uzlaştırması için uyarı kaydedilir. Depo ile SQL arasında dağıtık transaction yoktur.

Geri çekmede gerekçe zorunludur; eşzamanlı ikinci istek 409 alır. Fiziksel dosya korunur, normal liste/indirme dışlanır. Dosya önizlemesi rehber kimliğini açık seçtirir; yeni dosya veya geri çekme hedefi önceki işlemin seçimini devralmaz. Belge sorguları yenilenirken kart taslağı sıfırlanmaz. Yükleme için GUID gönderim makbuzu bu teslimde yoktur; belirsiz ağ sonucunda yeniden yükleme öncesi liste kontrolü gerekir.

## Günlük iş kaynakları

Yalnız İK yöneticisi için, işe giriş tarihi gelecekte olmayan aktif çalışanların güncel kayıtları okunur. Ayrı “tamamlandı” bayrağı tutulmaz; görevler mutasyon sonrası yeniden hesaplanır.

| İş | Kaynak | Davranış |
| --- | --- | --- |
| İletişim kontrolü | Cep telefonu ve kişisel e-posta ikisi de boş | İletişim sekmesine gider; gerçek kanal kaydedilince kalkar. |
| Evrak envanteri kontrolü | Geri çekilmemiş evrak yok | Evrak sekmesine gider; ilk aktif evrakla kalkar, son evrak geri çekilince yeniden oluşur. |
| Yönetici bağlantısı kontrolü | Atanmış yönetici pasif, bulunamıyor veya giriş hesabı bağlantısı yok | İş sekmesine gider; hiç yönetici atanmaması tek başına görev değildir. |

Bu sinyaller hukuken zorunlu belge/iletişim listesi, tüm evrakın tamamlanması veya hukuki son tarih değildir. Şirket gereksinimi, veri işleme amacı ve gerçek belge içeriği yetkili kişiyle doğrulanır. Üç görevde `dueDate` boştur; işe giriş tarihi son tarih diye gösterilmez.

## Şema ve doğrulama

Tenant migration: `20261001221114_AddGuidancePersonnelEvidence`. Kayıt varken Down 51000 ile durur; geçmiş sessizce silinmez. API ve frontend birlikte teslim edilir; canlı migration/publish yapılmadı.

- Dört yeni SQL senaryosu: değişmeyen kart, gerekçe, eşzamanlı/stale düzeltme, kaynaklı belge yükleme/geri çekme, görev yeniden açılması, kişisel ve şirket sınırları, olağan işlemin öğrenme sayılmaması, yönetici seçenekleri, kanıt yazma hatasında üç işlemin geri alınması ve migration Down koruması.
- İki yeni birim testi: ayrı fotoğraf/no-op ve hassas değer yerine değişen alan adları.
- Beş yeni arayüz testi: kaynak/gerekçe/organizasyon gövdesi, arka plan yenilemesi ve taslak, multipart kaynak, geri çekme kaynağı, sürüm/alan/gerekçe yolculuğu.
- Tam SQL: 321 başarılı; birim: 101 başarılı; frontend: 549 başarılı. Son belge seçim sıfırlamasından sonra ilgili 12 test yeniden başarılı.
- Release .NET derlemesi sıfır uyarı/hata; frontend üretim derlemesi başarılı; lint dört önceden bulunan uyarı, yeni uyarı yok; EF model/migration eşleşiyor.
- Playwright test verisiyle masaüstü ve 390 piksel mobil prova; içerik ve sayfa genişliği 390, yatay taşma yok. Bu gerçek uzman yayını veya canlı kabul değildir.

## Sıradaki teslim ve açık kapsam

Personel durum/yeniden işe alma ve ücret paketi alt işlemlerinin mevcut güvenli API'lerine öğrenme ve günlük görev bağları eklenecek; ardından kalan modül alt işlemleri aynı somut matriste kapatılacak. Toplu aktarımın ayrı sürümlü öğretici/kişisel kanıt bağlantısı, genel komutların belirsiz ağ sonucu/önizleme kapıları, bağımsız uygulama seviyesi kabulü, gerçek uzman/editör/yeni kullanıcı kabulü ve Turhost kurulum/yedek/geri dönüş kabulü açık. Önceki finansal telafi, entegrasyon ve saklama bağımlılıkları kaldırılmadı. Tüm öğrenme kapsamı tamamlanmadan canlı yayın yapılmaması kararı korunuyor.
