# Ekipzen İK Rehberi ve Öğrenme Yolculuğu

Tarih: 24 Eylül 2026

Durum: Ürün yaklaşımı kullanıcıyla netleştirildi ve kabul edildi. Bu belge geliştirme planıdır; aşağıdaki özelliklerin uygulanmış olduğunu göstermez. Teknik isimler ve paket sınırları uygulama sırasında kaynak kod üzerinden kesinleştirilecektir.

## 1. Amaç ve kabul edilen ürün kararları

Firma sahibi veya İK deneyimi olmayan yeni mezun bir çalışan, Ekipzen üzerinde gerçek günlük işleri yaparken süreçleri öğrenebilmeli; karmaşık istisnalar dışında temel görevleri doğru biçimde yürütebilmelidir.

Başarı, ekran turunun bitirilmesiyle değil; kullanıcının ne yaptığını, neden yaptığını, hangi kurala dayandığını, sonucu ve sonraki adımı anlayabilmesiyle ölçülür.

- Rehber uygulamadaki bütün süreçleri kapsar. İlk uygulama diliminin dar olması teslim kapsamını daraltmaz.
- Öğrenme gerçek veriler ve gerçek işlemler üzerinde gerçekleşir.
- Günlük işleri belirlemek ve hatırlatmak rehberin parçasıdır.
- Bilinmeyen seçimler kısa sorular, örnekler ve gerekçelerle açıklanır.
- Ayrıntılı örnekler ve mevzuat dayanakları sunulur.
- Yasal zorunluluk, şirket politikası ve iyi uygulama önerisi ayrılır.
- Kullanıcı hata yaptığında işlemin durumuna uygun düzeltme yolu öğretilir.
- Kritik işlemlerde ön kontrol, karmaşık durumlarda gerekçeli uzman yönlendirmesi bulunur.
- Öğrenme kullanıcı ve süreç bazında takip edilir; yardım deneyime göre azalır ve her zaman yeniden açılabilir.
- Kritik karar noktalarında kısa senaryo soruları ve açıklamalı geri bildirim kullanılır.
- Rehberin azalması yetkiyi artırmaz, işlem kontrollerini kaldırmaz.

## 2. Mevcut uygulamadaki dayanaklar

İnceleme tabanı: `14cd2394174c023527d14775cafde646b99d1394`. Plan hazırlanırken yerel `main` ile `ekipzen` uzak deposundaki `main` bu committeydi.

| Mevcut kaynak | Plan için anlamı |
| --- | --- |
| `frontend/src/features/home/HomePage.tsx` | Ana Sayfa öncelikli işleri ve bekleyen onayları gösteriyor; rehberli iş başlangıcı buraya bağlanabilir. |
| `frontend/src/features/inbox/InboxPage.tsx` | İş Kutusu onay ve aksiyon sekmelerini birleştiriyor; görevlerin ortak giriş noktası olabilir. |
| `frontend/src/features/actions/` ve `backend/src/Ekipzen.Application/Features/Actions/` | Aksiyon listesi, kaynak, sahip, öncelik ve durum altyapısı mevcut. Otomatik görev üretimi ve gerçek işlemden kapanma ayrıca tasarlanmalı. |
| `frontend/src/routes.tsx` ve `frontend/src/layout/AppShell.tsx` | Mevcut roller, gezinme ve ortak uygulama kabuğu rehber erişiminin sınırlarını belirler. |
| `frontend/src/features/personnel/` ve `backend/src/Ekipzen.Application/Features/Employees/` | Personel, içe aktarma, belge, istihdam ve işten çıkış akışları kapsam envanterine alınmalı. |
| `frontend/src/features/payroll/` ve `backend/src/Ekipzen.Application/Features/Payroll/` | Bordro rehberi mevcut hesaplama ve onay kurallarına bağlanmalı. |
| `backend/src/Ekipzen.Infrastructure/Persistence/AppDbContext.cs` ve `backend/src/Ekipzen.Infrastructure/Persistence/PlatformDbContext.cs` | Kullanıcıya ve şirkete ait öğrenme/görev verisi kiracı veri sınırını izlemeli; platform kimlik verisine karıştırılmamalı. |
| `docs/bordro-kanuni-kapsam-ve-isletim.md` | Bordronun destek kapsamı ve inceleme düzeni için başlangıç belgesi; güncel hukuki doğrulamanın yerine geçmez. |

Mevcut `docs/rehberler/` geliştiricilere yöneliktir. Bu çalışma son kullanıcıya uygulama içinde eşlik eden ayrı bir ürün yeteneğidir. Yukarıdaki liste ilk kaynak haritasıdır; tüm alt işlemlerin doğrulanmış envanteri Paket 1 çıktısı olacaktır.

## 3. Kullanıcının günlük akışı

1. Ana Sayfa, kullanıcının yetkili olduğu gerçek kayıtlardan oluşan günlük işleri gösterir.
2. İş kartı neden oluştuğunu, önceliğini, varsa son tarihini ve gecikmenin etkisini açıklar.
3. Kullanıcı ilgili kayıtta rehberli süreci başlatır veya kaldığı yerden devam eder.
4. Rehber hazırlığı kontrol eder, gerekli seçimleri açıklar ve gerçek işlemi mevcut ekran üzerinden yaptırır.
5. Kritik adımda değişecek kayıtlar, sonuçlar ve düzeltme sınırları gösterilir.
6. Başarılı sunucu işlemi doğrulandıktan sonra iş durumu güncellenir.
7. Kısa sonuç açıklaması ne yapıldığını, gerekçesini ve sonraki adımı özetler.
8. Uygun karar noktasındaki kısa soru ve uygulama kanıtı öğrenme ilerlemesine işlenir.

Önerilen yüzeyler: Ana Sayfa'da günlük işler ve öğrenmeye devam etme alanı; İş Kutusu'nda iş takibi; ilgili ekranda rehber paneli; süreç haritası, terimler ve ilerleme için rehber merkezi. Kesin yerleşim erişilebilir prototip üzerinden doğrulanır.

Rehber form girişini örtmemeli, yazılmış veriyi kaybettirmemeli ve her adımda zorunlu açılır pencere üretmemelidir. Klavye, ekran okuyucu, dar ekran, açık ve koyu tema kullanımında çalışmalıdır.

## 4. Tam kapsam envanteri

| Grup | Kapsanacak işler | Öğrenme sonucu |
| --- | --- | --- |
| Şirket hazırlığı | Şirket bilgileri, organizasyon, departmanlar, kullanıcı daveti, roller, ayarlar | Hangi ayarın hangi süreci etkilediğini ve kimin yetkili olduğunu anlama |
| İşe alım | Pozisyon, aday, değerlendirme, aşamalar, çalışan kaydına geçiş | Aday ile çalışan kayıtlarının ayrımını ve geçiş koşullarını anlama |
| İşe giriş | Çalışan kaydı, istihdam bilgileri, belge ve takip adımları | İşe girişin gerekli hazırlığını ve sırasını uygulama |
| Personel yönetimi | Profil güncelleme, istihdam değişikliği, belge, içe aktarma ve hatalı kayıt düzeltme | Değişikliğin diğer süreçlere etkisini kontrol etme |
| İzin ve onay | Tür seçimi, talep, bakiye, ekip görünümü, onay/ret, iptal/düzeltme, analiz | İzin kararını koşulları ve etkileriyle açıklama |
| Mesai ve puantaj | Günlük kayıt, eksik/hatalı kayıt, fazla çalışma, dönem kontrolü | Bordroya giden girdileri doğrulama |
| Bordro | Ayarlar, dönem, girdiler, hesaplama, önizleme, kontroller, onay, pusula ve izin verilen düzeltmeler | Hesap kalemlerini ve onay önkoşullarını anlama |
| Uyum ve belgeler | Gereklilik, eksik belge, süre takibi ve düzeltici iş | Zorunluluk ile öneriyi ayırma ve eksikliği giderme |
| İşten ayrılış | Dosya, bağlı işler, hesap taslağı, kontroller, tamamlanma ve izin verilen geri alma | Sonuçları, işlem sınırını ve uzman ihtiyacını anlama |
| Günlük yönetim ve analiz | İş Kutusu, aksiyon, sorumluluk, takip, genel durum, riskler, çalışan nabzı ve denetim izi | Sinyali yorumlama, uygun takip işi oluşturma ve sonucunu değerlendirme |

Her satırın alt akışları ve istisnaları kaynak kod ile eşlenir. Çalışan ve yöneticiye ait adımlar da kendi yetkileri içinde kapsanır; firma sahibi olmak kendiliğinden ek sistem yetkisi sağlamaz.

Uygulama dışında yürüyen adım varsa rehber bunu açıkça belirtir. Ekipzen'de kayıt açılması dış bildirimin, ödemenin veya başka bir sistem işleminin tamamlandığı anlamına gelmez. Böyle adımlarda kullanıcı beyanı ile sistemce doğrulanmış sonuç ayrılır.

## 5. Her rehberin içerik sözleşmesi

Her süreç ve adım aşağıdaki alanlarla hazırlanır:

- Sabit kimlik, başlık, sürüm, hedef roller ve öğrenme hedefi.
- Başlama koşulu, önkoşullar, gerekli kayıt/belgeler ve önceki adımlar.
- Yapılacak iş ve sade dille gerekçesi.
- Seçenekler, uygulanma koşulları ve gerekçeli karar soruları.
- Kısa açıklama, ayrıntılı örnek, karşı örnek ve terim açıklamaları.
- Dayanak türü: mevzuat, şirket politikası veya iyi uygulama.
- Sonuç, tamamlanma kanıtı, sonraki adım ve başka sürece etkiler.
- Eksik veri, yetkisizlik, istisna ve uzman yönlendirme koşulları.
- İşlem durumuna göre düzeltme/iptal/geri alma yolu.
- Öğrenmeyi ölçen soru, açıklamalı cevap ve yardım seviyesi.

Örnekler üretim kayıtlarına sahte personel veya sahte işlem eklemez. Kişisel bilgi gerekmeyen açıklamalarda kimliği belirsiz örnekler kullanılır.

## 6. Günlük görevler ve karar desteği

Görevler gerçek kayıt, durum değişikliği, eksiklik veya doğrulanmış süre kuralından üretilir. Her görev bir süreç/kayıt, sorumlu kapsamı ve üretim gerekçesiyle ilişkilidir.

- Aynı tetikleyici tekrar işlendiğinde aynı görev çoğalmaz.
- Gerekçesi ortadan kalkan, düzeltmeyle yeniden gerekli olan ve başka kullanıcı tarafından tamamlanan işler yeniden değerlendirilir.
- Erteleme yasal veya operasyonel son tarihi değiştirmez.
- Eksik veri yüzünden değerlendirilemeyen durum, yapılacak iş yokmuş gibi gösterilmez.
- Tarih hesabında şirket takvimi, saat dilimi ve ilgili kuralın geçerlilik dönemi dikkate alınır.
- İçerik ve görev erişimi sunucuda yetki kontrolünden geçer; bağlantıyı bilmek erişim sağlamaz.

Karar yardımcısı mevcut bilgiyi kullanır, belirleyici bilgileri kullanıcıya doğrulatır ve bilmediği bilgiyi sorar. Gerekçesiz kesin sonuç üretmez. Belirsizlikte hangi bilgi veya uzman değerlendirmesinin eksik olduğunu açıklar.

Onaylı kurallar ve içerik temel alınır. Yapay zekâ entegrasyonu bu planın tamamlanması için zorunlu değildir; ayrıca ele alınırsa mevzuat ve işlem yetkisinin kaynağı olamaz.

## 7. Gerçek işlemler ve hata düzeltme

Her alt işlem için bir düzeltme matrisi hazırlanır: kaydetmeden önce değiştirilebilir, kayıt sonrası düzenlenebilir, iptal edilebilir, ters işlem gerektirir, dış sistemde düzeltme gerektirir veya uzman incelemesine yönlenir.

Genel bir sınırsız geri alma davranışı varsayılmaz. Mevcut API gerekli düzeltmeyi desteklemiyorsa bu açık bir geliştirme bağımlılığıdır; rehber var olmayan bir yeteneği vaat etmez.

Kritik işlemlerde etkilerin özeti ve mevcut onay koşulları korunur. Rehber iş kurallarını ikinci kez uygulayan ayrı bir işlem motoruna dönüşmez; mevcut servisleri kullanır. Tekrarlanan istekler, başarısız kaydetme, ağ kesintisi ve başka kullanıcının kaydı değiştirmesi yanlış tamamlanmaya veya çift işleme yol açmamalıdır.

Uzman yönlendirmesi neden, gerekli uzmanlık, hazırlanacak bilgiler ve beklerken sürdürülebilecek işleri açıklar. Otomatik olarak üçüncü kişilere veri veya mesaj göndermez.

## 8. Öğrenme durumu ve mahremiyet

İş akışı ile öğrenme durumu ayrı tutulur:

- İş akışı belirli gerçek kayıt ve süreç örneğiyle ilgilidir.
- Öğrenme ilerlemesi kullanıcı, süreç/beceri ve içerik sürümüyle ilgilidir.
- Başka kişinin tamamladığı iş ilk kullanıcının öğrenme başarısı sayılmaz.
- Ekranı açma veya Sonraki düğmesine basma tek başına başarı değildir.

Önerilen seviyeler: rehberle uygulama, destekle uygulama, bağımsız uygulama. Seviye geçişi doğru uygulama, karar soruları ve yardım ihtiyacı gibi kanıtlara dayanır. Sayısal eşikler pilot gözleminden sonra belirlenir; tek bir işlem tüm modülü öğrenmiş saydırmaz.

Yanlış cevap işi cezalandırıcı bir sınava dönüştürmez; gerekçe ve örnekle öğretir. Kritik işlem koşulları ise öğrenme sorusundan bağımsız olarak uygulanır. Kullanıcı yardımı artırabilir, ara verebilir ve devam edebilir.

İlerleme sunucuda saklanır; oturum veya cihaz değişiminde korunur. İçerik değişirse yalnız etkilenen beceriler için tekrar önerilir; geçmiş kanıt ve kullanılan içerik sürümü korunur.

Öğrenme olayları gereksiz personel verisini kopyalamaz. Kullanıcının cevaplarını yöneticilere açan değerlendirme/puan sıralaması varsayılmaz. Saklama, silme, dışa aktarma ve erişim kuralları mevcut veri yaşam döngüsüyle birlikte tanımlanır.

## 9. Mevzuat ve içerik işletimi

Bu belge somut yasal süre, oran veya hak tanımlamaz. İçerik üretiminde resmî kaynaklar araştırılmalı ve ilgili uzman tarafından incelenmelidir.

Mevzuat içeriği için kaynak bağlantısı, ilgili hüküm, uygulanma koşulları, yürürlük başlangıcı/sonu, son inceleme tarihi, içerik sürümü ve inceleyen kişi/rol kaydedilir. Şirket politikası da kendi sürüm ve geçerlilik bilgisine sahiptir; yasal hakları geçersiz kılan öneri üretmez.

Önerilen içerik yaşam döngüsü: taslak, incelemede, onaylı, kullanımdan kaldırılmış. Onaylı sürüm yerinde değiştirilmez; değişiklik yeni sürüm oluşturur. Güncel doğrulaması olmayan veya çelişkili yönlendirme etkilenen karar adımında kullanıma alınmaz.

Mevzuat değişikliği ilgili süreçleri, soruları, görev kurallarını ve açık iş örneklerini etki analiziyle günceller. Geçmiş işlem için o tarihte kullanılan sürüm korunur. İçerik güncellemesi açık işlemi sessizce farklı bir karar akışına taşımaz.

Uygulamadan önce içerik sahibi, uzman inceleme sorumlusu ve gözden geçirme düzeni atanmalıdır. Atama yapılmamış olması ortak teknik altyapı hazırlığını engellemez; doğrulanmamış hukuki içeriğin yayımlanmasını engeller.

## 10. Teknik yerleşim önerisi

Kesin veri modeli ve API sözleşmeleri Paket 2'de karara bağlanacak; aşağıdaki ayrımlar korunacaktır:

| Parça | Sorumluluk |
| --- | --- |
| Sürümlü süreç/içerik kataloğu | Adımlar, dayanaklar, karar soruları, önkoşullar ve onaylı sürümler |
| Süreç örneği | Gerçek kayıtla bağlı ilerleme, bekleyen adım ve tamamlanma kanıtı |
| Öğrenme kaydı | Kullanıcıya ait beceri ilerlemesi, cevap/uygulama kanıtı ve yardım tercihi |
| Görev değerlendirme | Gerçek veriden görev üretme, tekilleştirme ve durum uzlaştırma |
| Modül bağlantıları | Mevcut ekranlar, sorgular ve iş komutlarıyla entegrasyon |
| Rehber arayüzü | Panel, süreç haritası, örnekler, sorular ve devam etme deneyimi |

Önerilen yeni kod alanları: `frontend/src/features/guidance/` ve `backend/src/Ekipzen.Application/Features/Guidance/`. Bunlar henüz mevcut değildir. Gereken entity, kalıcılık, migration ve API katmanları mevcut proje düzeni izlenerek eklenir. Kullanıcı/şirket ilerlemesi kiracı veritabanında saklanır; ortak genel içeriğin saklama yöntemi Paket 2'de belirlenir.

Görevlerin mevcut aksiyon modelini genişletmesi veya bağlı bir model kullanması; kayıt/sorumlu bağlantısı, tarih alanlarının tipi, tekillik, sorgu yetkileri ve mevcut ekranlarla uyumluluk üzerinden kararlaştırılır. İlk kaynak incelemesi bu kararı tek başına kesinleştirmez.

## 11. Uygulama paketleri

Her paket küçük, incelenebilir commitlere ayrılır. Geliştirme durumu aşağıdaki kutularla takip edilir; hiçbir kutu yalnız dokümantasyon yazıldığı için işaretlenmez.

### Paket 1 — Süreç ve yetenek envanteri

- [ ] Tüm kapsam gruplarını ekran, API, rol ve gerçek alt işlemlerle eşle.
- [ ] Başlama koşulu, önkoşul, karar, sonuç ve süreçler arası bağımlılıkları çıkar.
- [ ] Düzeltme matrisini ve dış sistemde yürüyen adımları belirle.
- [ ] Eksik uygulama yeteneklerini rehberden ayrı bağımlılıklar olarak kaydet.
- [ ] Her sürecin öğrenme hedefini, içerik sahibini ve inceleme ihtiyacını belirle.

Çıkış ölçütü: Bütün süreç gruplarında kapsanan alt işlemler ve eksikler görünürdür; henüz uygulanmayan işlem tamamlanmış gibi sunulmaz.

### Paket 2 — İçerik standardı, prototip ve sözleşmeler

- [ ] Rehber paneli, günlük iş kartı, süreç haritası ve ilerleme görünümünü prototiple.
- [ ] İşe giriş ve izin türü seçimi için ortak şablonla örnek içerik hazırla.
- [ ] Veri modeli, API, sürümleme, yetki ve içerik yayınlama sözleşmelerini belirle.
- [ ] Günlük görev üretimi, tekrar çalıştırma ve tamamlanma kanıtını tanımla.
- [ ] Erişilebilirlik ve deneyimsiz kullanıcıyla anlaşılırlığı kontrol et.

Çıkış ölçütü: Gerçek iş akışı ile öğrenme akışı birbirine karışmadan prototipte izlenebilir; doğrulanacak içerik ve teknik sözleşmeler bellidir.

### Paket 3 — Ortak rehber ve öğrenme altyapısı

- [ ] Onaylı süreç kataloğu, süreç örneği ve kullanıcı ilerlemesini uygula.
- [ ] Rehber paneli, devam etme, yardım seviyesi ve açıklamalı soruları ekle.
- [ ] Başarılı gerçek işlemle adım tamamlanmasını ilişkilendir.
- [ ] Rol/kayıt yetkileri, kiracı izolasyonu ve veri yaşam döngüsünü uygula.
- [ ] İçerik sürümü değişimi ve yarıda kalan iş davranışını doğrula.

Çıkış ölçütü: Oturum yenilense bile doğru kullanıcı ve kayıtta devam edilir; sahte ilerleme veya veri sızıntısı oluşmaz.

### Paket 4 — Günlük iş entegrasyonu

- [ ] Ana Sayfa ve İş Kutusu'na gerekçeli görevler ve rehberli başlangıç ekle.
- [ ] Öncelik, sorumluluk, son tarih ve ilgili kayda yönlendirmeyi uygula.
- [ ] Tekilleştirme, başka kullanıcı tarafından tamamlama ve yeniden açılmayı doğrula.
- [ ] Eksik veri, başarısız değerlendirme ve gecikme durumlarını açıkça göster.

Çıkış ölçütü: Liste gerçek iş durumuyla tutarlıdır; tekrar çalıştırma kopya görev veya yanıltıcı tamamlanma üretmez.

### Paket 5 — İşe girişin uçtan uca doğrulanması

- [ ] Hazırlık, çalışan kaydı, belge ve takip adımlarını tek yolculukta birleştir.
- [ ] Karar açıklamaları, mevzuat/şirket politikası ayrımı ve kaynakları bağla.
- [ ] Hatalı giriş, düzeltme, yarıda bırakma ve başka kullanıcıyla devamı doğrula.
- [ ] Kısa öğrenme soruları ve sonuç açıklamalarını tamamla.

Çıkış ölçütü: Deneyimsiz kişi gerçek işe giriş görevini rehberle yürütebilir; uygulama içi kayıt ile dış yükümlülüğün farkını anlayabilir. Bu paket tüm ürünün tamamlandığı anlamına gelmez.

### Paket 6 — Kapsamdaki bütün süreçlere yayılım

- [ ] Şirket hazırlığı, işe alım ve personel yönetimini tamamla.
- [ ] İzin, onay ve mesai/puantaj akışlarını tamamla.
- [ ] Bordro, uyum/belge ve işten ayrılış akışlarını tamamla.
- [ ] Analiz, günlük yönetim, aksiyon ve denetim izi akışlarını tamamla.
- [ ] Her grupta karar desteği, düzeltme, öğrenme ve kaynak kapsamını denetle.

Çıkış ölçütü: Paket 1 envanterindeki her alt işlem için rehber ve doğrulama kanıtı vardır; herhangi bir grup sessizce sonraya bırakılmaz.

### Paket 7 — Pilot ve teslim doğrulaması

- [ ] Firma sahibi ve deneyimsiz yeni mezun profilleriyle temsilî görevleri gözlemle.
- [ ] Doğru tamamlama, gerekçeyi açıklama, hata düzeltme ve uzman ihtiyacını fark etmeyi değerlendir.
- [ ] Yardım seviyelerinin azalmasını ve tekrar açılmasını gözlem sonuçlarına göre ayarla.
- [ ] İçerik doğrulamasını, erişilebilirliği, teknik kontrolleri ve kapsam matrisini tamamla.
- [ ] Mevzuat değişikliği ve içerik güncelleme sürecini örnek değişiklikle doğrula.

Çıkış ölçütü: Açık kritik hata veya doğrulanmamış aktif hukuki yönlendirme yoktur; tüm süreç kapsamı ve kullanıcı sonuçları kanıtlanmıştır.

## 12. Doğrulama yaklaşımı

Testler gerçek risk ve davranışa odaklanır:

- Yetkisiz kullanıcı ve başka kiracı kaydıyla rehber/görev/ilerleme erişiminin reddi.
- Ağ hatası, çift istek ve eşzamanlı güncellemede yanlış tamamlanmanın önlenmesi.
- Tekrarlanan görev değerlendirmesinde tekillik; iptal ve düzeltmede doğru yeniden değerlendirme.
- İçerik sürümü ve işlem tarihine uygun karar; güncelliği doğrulanmamış yönlendirmede güvenli durma.
- Kritik işlem kontrolünün rehber kapalı veya kullanıcı deneyimli olsa da uygulanması.
- Oturum/cihaz değişiminde devam; kullanıcılar arasında öğrenme kanıtının karışmaması.
- Kayıtlı form verisi, klavye odağı, ekran okuyucu ve dar ekran davranışı.

Her paket etkilenen frontend/backend testleriyle doğrulanır. Ortak altyapı ve son entegrasyonda frontend derleme/lint ile ilgili backend testleri çalıştırılır. Dokümantasyon paketinde bağlantı, kapsam ve Git fark kontrolü yeterlidir; çalıştırılmayan uygulama testleri geçmiş gibi raporlanmaz.

Pilot ölçümleri: doğru tamamlanan görevler, tekrar hata, gerekli yardım, gerekçeyi açıklayabilme, hatayı düzeltebilme ve uygun uzman yönlendirmesi. Sadece hız veya rehber görüntülenme sayısı başarı göstergesi kabul edilmez.

## 13. Git ve hafıza iş akışı

Geliştirme küçük, incelenebilir yerel commitlerle ilerler. Senkronizasyon öncesi uzak `ekipzen` referansları yeniden kontrol edilir. Her parti için kaynak/hedef referansları, commitler, dosyalar, doğrulamalar ve yapılacak Git işlemleri bildirilerek tek açık onay alınır. Onay başka commit veya dalı kapsamaz.

Project Memory geçici plan veya iş listesi kopyası olarak kullanılmaz. Kalıcı karar uygulanıp kanonik checkout'a birleştirildikten ve doğrulandıktan sonra ilgili mevcut hafıza kaydı gerekiyorsa güncellenir.
