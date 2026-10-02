# Ekipzen — şirket yardımcı işlemleri ve erişim hazırlığı

2 Ekim 2026. Sınırlandırılmış T01 teslimi **S05, S06, S07 ve P08** satırlarını kapatır. Yeni kapsam satırı eklenmedi. Önceki aday karar/işe alma paketi yerelde üç commit olarak korunur; bu paket onun üzerine geliştirilmiştir. İki dalın Git senkronizasyonu ancak tam olarak raporlanan commitler için tek açık onayla yapılır.

## Kapanış ve kalan işler

Olağan işlem matrisi 45 satırdır: **28 bağlı, 6 okuma, 11 açık/kısmi**. Bu paket açık satırları **15'ten 11'e**, açık teknik başlıkları **7'den 6'ya** indirir. T01 ve T02 teknik olarak tamamlandı. Kalan satırlar L01/L02, A02, B01/B03/B06/B07, O01/O03, G02/G03; kalan başlıklar T03–T08. Çapraz tekrar/öğrenme seviyesi, otomasyon/teslim ve sunucu kapıları ayrı ölçülür. Sıradaki paket T03 izin türü, talep ve iptal yolculuğudur.

Gerçek uzman incelemesi, ayrı merkezî editör yayını ve deneyimsiz kullanıcı kabulü yapılmadı. Test katalog yayını uzman incelemesi değildir. Tam öğrenme kapsamı tamamlanmadan yayın yapılmaması kararı korunur; üretime publish veya migration gönderilmedi.

## Departman silme

- Yalnız hr-admin için GET `departments/{id}/deletion-preview`: ad/kod, çalışan ve pozisyon sayıları, `canDelete`, `recordToken`. İnceleme salt okunurdur; kanıt oluşturmaz.
- DELETE sorgusunda isteğe bağlı `expectedRecordToken` ve `guidanceContentRevisionId`. Rehberli işlemde ilk belirteç zorunludur. Kaynak `company/departments`; bağımsız anahtar `department-delete`.
- `departments` kilidi ve Serializable transaction içinde kayıt/bağlantılar yeniden okunur. Çalışan **veya pozisyon** bağlıysa silme reddedilir; pozisyonun departmanını sessizce null yapmaz. Bayat önizleme 409 olur.
- Silme ve `GuidancePreparationEvidence` aynı transaction içindedir. Kanıt silinen departmana FK ile bağlı değildir; tarihsel gerçek sonuç korunur. Tekrar silme 404, ikinci kanıt yok. Başka şirkette aynı sayısal kimlik bulunabilir; yalnız o şirketin kendi kaydı okunur.
- Arayüz önizleme, açık kaynak ve son teyit sunar. Kaynak değişimi teyidi kaldırır; hata ilk önizlemeyi/kaynağı korur. Güncel etki kullanıcı tarafından açıkça yeniden incelenir. Ad/kod hatasında Düzenle yolu öğretilir; silinen kimliğin geri alınması geliştirilmedi.

## Logo, bildirim ve fotoğraf

- Şirket profilinin mevcut token'ından ayrı `logoRecordToken`, bildirimde `recordToken`, çalışan profilinde `photoRecordToken` vardır. Yalnız ilgili kaynağı korurlar; ilgisiz profil düzeltmesi logo seçimini otomatik geçersiz kılmaz.
- Logo/fotoğraf multipart, bildirim JSON komutu ilk belirteç ve isteğe bağlı güncel ders sürümü alır. Rehberli işlemde belirteç zorunlu, bayat kayıt/kaynak reddedilir. Kaynaksız eski istemciler çalışır.
- Logo ve bildirim ayrı seçilen `company/company-profile`; fotoğraf ayrı `personnel-entry/tab-kimlik` kaynağı kullanır. Anahtarlar `company-logo-upload`, `company-notifications-update`, `employee-photo-upload`. İş kaydı ve sahibiyle sınırlı kanıt aynı transaction içindedir. Eski kaynaksız kayda sonradan damga yoktur.
- Logo/fotoğraf dosyasının eski yolu sadece commit sonrası temizlenir. Bilinen kayıt/kanıt başarısızlığında yeni tamamlanmış dosya temizlenir; eski kayıt/dosya korunur. Temizleme başarısızlığı loglanır, commit edilmiş sonuç kullanıcıya başarısız gösterilmez. Dosya sistemi DB transaction'ına katılmaz: yarım dosya yazımı, commit/ağ belirsizliği ve artıkları uzlaştırma T06/T08 işletim sınırlarında kalır. Genel makbuz/yenileme sonrası dosya kurtarma bu paketin garantisi değildir.
- Aynı bildirim değerleri no-op, yeni kanıt yok. Tercih e-posta gönderimi/teslimi değildir; haftalık rapor kapalıdır. Logo/fotoğraf yükleme farklı saklanan dosya üretir; aynı dosya içerik tekilliği geliştirilmedi.
- Dosya seçimi ve toggle yazmaz. Önizleme, ayrı kaynak, açık teyit gerekir. Kaynak değişimi/hata teyidi kaldırır; taslak ve kaynak korunur. Güncel durum açık yeniden inceleme düğmesiyle okunur; seçili dosya/istenen tercih korunur, ilk belirteç kendiliğinden değiştirilmez. Mobilde eylemler okunabilir biçimde sıralanır.
- Kişisel yolculuk ve veri dışa aktarımı doğru anahtar/sonucu gösterir. Bu kozmetik/tercih işlemlerine yapay günlük görev eklenmez; var olan gerçek hazırlık/personel sinyalleri kullanılır.
- Kullanıcının ayrı açık erişim kararıyla GET `employees/{id}/photo` yalnız **aynı şirketin hr-admin** rolüne açıldı. Controller ve query rolü denetler; tenant veritabanı/dosya alanı isteğin şirketine bağlıdır. Ekip yöneticisi 403 alır. Frontend yalnız hr-admin için oturumlu dosyayı indirip geçici blob URL ile gösterir; başka rol istek göndermez, URL kapanışta temizlenir. Fotoğraf görüntülemek kanıt üretmez. Otomatik incelemenin reddettiği daha geniş Management erişim önerisi uygulanmadı.

## Erişim hazırlığı ve kaldırılan alanlar

S07 okuma satırı olarak kapanır: şirket hazırlığından erişim rehberine geçiş, gerçek oturum rolü, personel kartı departman/yönetici bağlantısı, işe alım/hesap hazırlığı ve kendi parola işlemi ayrı açıklanır. Unvan yetki değildir; hesap hazır, davet teslimi ve gerçek giriş birbirinin kanıtı değildir. Kaynaklı erişim alıştırması mevcut merkezî yayınla açılır; rehberi okumak gerçek işlem kanıtı üretmez. Genel rol atama/SMS/davet teslim API'si eklenmedi.

Kullanıcı açık kararıyla **yüksekte çalışma, gece çalışma, ağır yük kaldırma** alanları aktif UI, okuma DTO'su, yazım modeli, profil uygulama ve kart alan karşılaştırmasından kaldırıldı. Sağlık notu ve beden alanları korunur. Domain/DB'deki eski boolean kolonları/değerleri ve tarihsel kayıtlar silinmez; kart güncellemesi bunları false'a çevirmez. Bu pakette yeni migration yoktur. Eski verinin fiziksel saklama/temizleme kararı ayrı kalır; kaldırma yeni bir tıbbi uygunluk akışı değildir.

Mevcut 10 süreç/38 ders korunur. Şirket profili, departman, erişim hazırlığı ve kimlik dersleri uygulanan davranışla güncellendi. Yeni hukuki hüküm icat edilmedi; ürün davranışı ile mevcut dayanakların ayrımı ve uzman yayın kapısı korunur.

## Doğrulama

- Release solution: uyarılar hata sayılarak **0 uyarı / 0 hata**.
- Tam backend: **340 SQL entegrasyon + 101 birim** testi başarılı. Ardından son onaylı fotoğraf GET eklemesiyle **dört ilgili SQL senaryosu yeniden başarılı**: görüntülenen gerçek baytlar/tür, ekip yöneticisine 403 ve başka tenant erişim reddi dahil. Yeni dört senaryo: silme bağımlılıkları/bayat önizleme/tekrar/rol/tenant/tarihsel kanıt; bildirim no-op/yanlış kaynak/yarış; kanıt hatasında dört işin rollback'i ve eski dosyanın korunması/yeni dosyanın temizlenmesi; kaldırılan alanların API'de yokluğu ve eski değerlerin kart düzeltmesinde korunması.
- Tam frontend: **581 test / 97 dosya** başarılı. Kaynak/ilk belirteç, seçimde yazmama, açık teyit, kaynak değişiminde teyit sıfırlama, 409 taslak koruma, engellenen silme, gerçek erişim bağlantıları ve üç alanın gönderilmemesi doğrulandı. Son iki senaryo hr-admin için oturumlu fotoğraf/URL görüntüleme ve ekip yöneticisinin indirme isteği göndermemesidir.
- Frontend üretim derlemesi başarılı; lint yalnız mevcut dört uyarıyı taşır. OpenAPI izolasyonlu test hostundan yenilendi. EF AppDbContext: son migration sonrası model değişikliği yok.
- Tarayıcı kontrolü gerçek frontend ve kontrollü test API'siyle masaüstü 1440, mobil 390×844; silme/logo/bildirim/erişim/fotoğraf ekranları ve klavye anahtarı. Test API'si gerçek uzman, kullanıcı veya üretim kabulü değildir.
