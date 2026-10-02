# Ekipzen — olağan işlem kapsam ve bitirme matrisi

2 Ekim 2026. Başlangıç [envanteri](2026-09-24-ik-rehberi-surec-envanteri.md), onaylı [plan](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md), controller/komutlar, `GuidanceOperationEvidence` ve `GetDailyGuidanceTasksQuery` karşılaştırılarak hazırlanmıştır. Bu belge önceki teslimlerin tarihsel açık iş cümleleri yerine **güncel teknik kapanış listesidir**. İnsan/uzman kabulü ayrı kapıdır. Bu paket satırları yerelde uygulanmıştır; main'e senkronizasyon tek ayrı onayla yapılacaktır.

## Durum ve kanıt tanımı

- **Bağlı:** Gerçek yazım açık seçilen güncel merkezî ders sürümüne bağlanır; sahibinin kişisel kanıtı vardır. Kaynaksız eski işlem sonradan damgalanmaz. Bu durum bütün sürecin veya kişinin yeterliliğinin kabulü değildir.
- **Açık:** İşin API'si/ekranı çoğunlukla vardır; aşağıdaki somut öğrenme/görev/düzeltme veya güvenlik bağlantısı henüz tamamlanmamıştır. Yeni API ihtiyacı ilgili satırda ayrıca belirtilir.
- **Okuma:** Liste/önizleme/analiz bir yazım değildir; açılması başarılı iş kanıtı üretmez. Açıklama/alıştırma ve ilgili işin gerçek sonucu ayrı izlenir. Her okuma için yeni mutasyon yaratılmayacak.
- **Karar/dış:** Üründe olmayan yetenek veya şirket/uzman/sağlayıcı kararı. Mevcut yol/sınır açık gösterilir; tamamlandı denmez ve sessizce kaldırılmaz.

Tablodaki dersler mevcut 10 süreç/37 adım tanımına başvurur. Bir adım birden fazla işlem öğretir; örneğin pozisyon hazırlığı ve aday oluşturma aynı `recruitment/candidate-create` açıklamasını, fakat ayrı işlem anahtarları ve kanıtları kullanır. Mutasyon ve GET denetim geçmişi aynı şey değildir. `GET /api/guidance/operations` ve kişisel veri dışa aktarımı yalnız kişinin kendi kaynaklı kanıtlarını gösterir.

## Şirket hazırlığı

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| S01 başlangıç şablonu | GET `/api/setup/preview`, POST `/api/setup` | `company/initial-setup` **Bağlı**; gerçek eklenenler, token ve no-op korunur. | Eksik başlangıç görevi; kaynak değişirse yeniden önizleme. Şablon politika onayı değildir. |
| S02 şirket profili | PUT `/api/settings/company` | `company/company-profile` **Bağlı, bu paket**; değişen alan adları, ilk token, no-op ve atomik kanıt. | Başlangıç eksikliği mevcut; profilin alan bazlı eksikliği için güvenilir zorunluluk listesi henüz yok. Aynı formdan koşullu düzeltme. |
| S03 departman ekle | POST `/api/departments` | `company/departments` **Bağlı, bu paket**. | Mevcut hazırlık ekranında boş liste açıklanır; aynı isim reddedilir. Çalışan bağlantısı kartta ayrıca yapılır. |
| S04 departman düzelt | PUT `/api/departments/{id}` | `company/departments` **Bağlı, bu paket**; ayrı kaynak, ilk token ve gerçek alan farkı. | Bağlı kişiler taşınmaz; eski token 409, no-op kanıt üretmez. |
| S05 departman silme | DELETE `/api/departments/{id}` | **Açık**; API vardır, rehberli son etki/kanıt ve ekran bağlantısı yok. | Bağlı kayıt koruması mevcut; silme yerine ad/kod düzeltme yolu açıklanır. |
| S06 logo ve bildirim tercihleri | POST `/api/settings/company/logo`, PUT `/api/settings/notifications` | **Açık**; profil kanıtı bu ayrı işlemleri kapsamaz. | Hatalı logo mevcut yükleme yoluyla değiştirilir; gerçek e-posta sonucu tercih kaydı değildir. Haftalık rapor desteklenmiyor. |
| S07 rol/ekip erişimi ve hesap hazırlığı | Hesap daveti/provizyonu, ekip kapsamı, parola | `company/access-preparation` **Karar/dış + açık bağlantı**; gerçek rol ve sorumlu doğrulanır. | Desteklenmeyen SMS 2FA etkin gösterilmez. Yeni rol yönetimi/SMS yeteneği eklenmiş sayılmaz; hesap sorumlusuna yönlendirme ve mevcut davet/provizyon sonucu izlenir. |

## İşe alım

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| R01 pozisyon açma | POST `/api/positions` | `recruitment/candidate-create` **Bağlı, bu paket**; adaydan ayrı kanıt. | Kadro başvuru sayısı değildir; oluşturma aday/çalışan/ilan üretmez. Mevcut sürümlü pozisyon düzeltmesiyle telafi. |
| R02 pozisyon düzelt/kapat | PUT `/api/positions/{id}`, corrections | `recruitment/candidate-create` **Bağlı**; ilk sürüm/gerekçe/önce-sonra kaydında ayrı kaynak, no-op/eski sürüm reddi. | Bağlı aday ve geçmiş korunur; kapalı pozisyonda yeni aday/işe alma durur. |
| R03 aday oluştur | POST `/api/candidates` | `recruitment/candidate-create` **Bağlı, bu paket**; puansız durum korunur. | `candidate-review:{id}` gerçek Yeni kayıttan doğar; karar son tarihi uydurulmaz. |
| R04 aday temel bilgi/pozisyon düzelt | PUT `/api/candidates/{id}`, corrections | `recruitment/candidate-create` **Bağlı**; değişmez düzeltme kaydında ayrı kaynak, beklenen sürüm/gerekçe/rol uygunluk teyidi. | Geçmiş/aşama korunur; işe alınmış başvuru kilitlidir, çalışan kartı ayrıdır. |
| R05 aşama kararı | PATCH `/api/candidates/{id}/status` | `recruitment/first-review` **Kısmi bağlı**; ilk Yeni geçişi kaynaklı kanıttır, sonraki geçişler aynı öğrenme kaydına eklenmez. | Zorunlu gerekçe/seçilmiş güncel dayanak snapshotları vardır; Yeni görevi kaynaktan kapanır. Diğer aşamalarda karar/iletişim takibi açık. |
| R06 mülakat notu ekle/düzelt | POST notes ve notes/{id}/revisions | `recruitment/first-review` **Bağlı**; ekleme ve her gerekçeli yeni sürüm ayrı kaynak ve aktör kanıtı. İlk kayıt/eski karar dayanağı değişmez. | Yanlış aday notunun iptal/taşıması **karar/dış bağımlılık**; fiziksel silme yok. Eklemede ağ tekrar tekilliği ve aday beklenen sürümü ayrıca açık. |
| R07 değerlendirme ekle/düzelt | POST evaluations ve evaluations/{id}/revisions | `recruitment/first-review` **Bağlı**; puan/ölçek, düzeltmede iki sürüm ve ayrı kaynak/aktör kanıtı. | Genel puan otomatik hesaplanmaz. Yanlış aday/iptal, ölçüt standardı ve yapılandırılmış dayanak bağımlılıkları açık. Eklemenin genel tekrar/sürüm güvenliği T06'da. |
| R08 işe al ve başarısız hesap hazırlığını tekrar dene | POST hire ve provisioning/retry | **Açık kaynak bağlantısı**; kalıcı aday–çalışan ilişkisi ve provizyon sonucu vardır. | Adaydan doğru çalışan kartına geçilir; hesabın hazırlanması ayrıca doğrulanır. Geri alma/kadro/ücret etkisi bağımlılıkları açık. |
| R09 huni ve başvuru geçmişini incele | GET `/api/recruitment/funnel`, aday geçmişi | **Okuma**; mevcut huni anlık dağılımdır. | Tarihsel dönüşüm/bekleme süresi ve aday iletişim durumu ayrı açık API/yetenek; anlık sayıya geçmiş oran denmez. |

## İşe giriş ve personel yönetimi

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| P01 çalışan oluştur | POST `/api/employees`, kendi oluşturma sonucu | `personnel-entry/tab-is` **Bağlı**; kalıcı gönderim makbuzu/gerçek çalışan kimliği. | Açık sözleşme/çalışma seçimi; belge/iletişim/ücret işleri ayrı. Belirsiz yanıt aynı gönderimle kurtarılır. |
| P02 kart bilgileri ve departman/yönetici bağları | PUT `/api/employees/{id}`, personnel-history | `personnel/record-review` **Bağlı**; ilk token, değişen alanlar, gerekçe. | Eksik iletişim, evrak ve geçersiz atanmış yönetici kaynaktan görev; durum kısayolu yok. |
| P03 evrak yükle/geri çek | POST documents ve documents/{id}/withdraw | `personnel-entry/tab-evrak` **Bağlı**; iş/kanıt atomik, gerekçeli geri çekme. | Aktif evrak yokluğu işi gerçek kayıttan kalkar/yeniden açılır; zorunlu evrak listesi değildir. Fiziksel saklama/silme kararı açık. |
| P04 ücret paketi ekle | POST compensation-packages | `personnel-entry/tab-mali` **Bağlı**; dönem/paket/sürüm bağlamı. | Bugün geçerli ücret hazırlığı işi; eski/ileri tarihli paket görevi kapatmaz. |
| P05 kullanılmamış tutar/kaynak ve güvenli tarih düzelt | PUT compensation-packages/{id}, effective-date + preview | `personnel-entry/tab-mali` **Bağlı**; önizleme/ilk sürüm/gerekçe ve atomik kanıt. | Kullanılmış/kapalı kayıt kilitli; uzman telafi dosyası ayrı. Tarih değişimi bugünkü ücret işini yeniden açabilir. |
| P06 toplu aktarım | POST import/preview, import; GET import/results/{requestId} | `personnel/import-review` **Bağlı**; tek gönderim, ilk kaynak ve gerçek satır/kart sonuçları. | Kart/evrak/ücret işleri doğar; yalnız atlanan eksikler yeni dosyada. TC'siz farklı gönderim tekilliği ve yenileme sonrası dosya kurtarma açık. |
| P07 yeniden işe alma | POST employees/{id}/rehire | `personnel/rehire` **Bağlı**; yeni dönem, eski geçmiş korunur. | Bugünkü yeni dönem ücreti görevi; eski paket/izin taşınmaz. Erişim kuyruğu sonucu ayrı. |
| P08 fotoğraf ve bilinmeyen özlük/uygunluk alanları | Mevcut fotoğraf ve kart API'leri | **Açık açıklama/bağlantı ve karar**. | Fotoğraf kart oluşturma kanıtı değildir. `false` ile değerlendirilmedi ayrımı olmayan alanlar için model kararı gerekir; tahmini sağlık/uygunluk onayı yapılmaz. |

## İzin, puantaj ve bordro

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| L01 izin türü/bakiye ve talep ön inceleme | GET types/balance/ledger, POST leaves/preview | `leave/review-request` **Okuma + açık tür bağlantısı**. | Tür/şirket politikası kaynakları ve belge koşulları henüz uygulama türleriyle tam eşlenmedi; izin türü bayrağı hukuki uygunluk değildir. |
| L02 izin talebi ve iptal | POST `/api/leaves`, leaves/{id}/cancel | **Açık öğrenme/devam bağlantısı**; gerçek kayıt ve mevcut iptal koşulları vardır. | Talep gerçek bekleyen onay işi doğurur; kullanıcı/rol kapsamı genişletilmez. İptal bakiye/rezervasyon etkisi yeniden doğrulanır. |
| L03 onay/ret | POST leaves/{id}/approve veya reject | `leave/decision-effect` **Bağlı**. | Bekleyen görev gerçek durumdan kapanır; eski/ikinci karar reddedilir, soru gerçek karar değildir. |
| A01 puantaj oluştur/düzelt | POST attendance/preview ve attendance; PUT attendance/{id} | `attendance/entry-review`, entry-correction **Bağlı**. | Tarih aralığı ve ilk satır sürümü korunur. Bordro girdisini otomatik düzeltmez. |
| A02 aylık özet/bordro uzlaştırma | GET puantaj özetleri ve dönem girdileri | `attendance/period-summary` **Okuma + açık uzlaştırma**. | Değişen puantajla mevcut bordro girdisinin tekrar kontrol görevi/kanıtı açık. |
| B01 hesap ayarı ve ayrılış kuralı taslağı/onayı | PUT payroll/settings, POST settings/{id}/approve; termination-calculation-rules | `payroll/settings-review` **Açık kaynak bağlantısı**. | Taslak ayar inceleme görevi mevcut; yürürlük/kaynak/uzman onayı korunur. |
| B02 dönem açma | GET periods/creation-preview, POST periods | `payroll/period-create` **Bağlı**; aynı kaynağa token ve gerçek personel/kaynak girdileri. | Açık dönem görevi; yanlış kaynak/ayda yeniden önizleme. |
| B03 satır düzeltme ve toplu kontrol | PUT periods/{id}/rows/{rowId}, POST periods/{id}/check | `payroll/row-review` **Açık kaynak/sonuç bağlantısı**; sürümlü yazım ve gerçek hesap vardır. | Dönem işi mevcut; ayrıntılı satır eksikleri/puantaj uzlaştırması kapanış matrisi açık. |
| B04 satır ve dönem onayı | POST rows/{id}/approve, periods/{id}/submit | `payroll/approval` **Bağlı**. | Onay sonrasında açık dönem işi kapanır; aynı işlem ödeme/beyan değildir. |
| B05 pusula/geçerli parasal sürüm | GET periods/{id}/rows/{rowId}/payslip, my | `payroll/payslip-review` **Okuma**. | Kullanıcı/kayıt kapsamı korunur; yanlış onay olağan satır düzenlemeyle açılmaz. |
| B06 uzman telafi ve parasal sürümler | compensation remediation-case, approved-impact/preview/apply/chain/revise | `payroll/correction-external` **Açık öğretici bağlantı**; uzman dosyası ve sürümlü parasal akış mevcut. | Dış ödeme/bildirim iki ayrı kanıt/görev. İkinci düzeltmenin zinciri/yıl aşımı ayrı gelişmiş karar; kilit ve uzman yönlendirmesi sürer. |
| B07 onay/kapanış ve olağan dış işlem | Dönem durumu ve mevcut dış kayıt yolları | **Açık kapsam netleştirme**. | Onayı ayrı dönem kapatma veya banka/resmî gönderim gibi göstermeme; olağan dış teyit ve varsa kapanış yaşam döngüsü mevcut yeteneklerle eşlenecek. |

## Uyum, ayrılış ve günlük yönetim

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| U01 uyum kaydı, bilgi/sorumlu ve durum | POST/PUT compliance, status/owner | `compliance/record-review`, record-correction, status-review **Bağlı**. | Eksik/son tarihi yaklaşan belge gerçek kayıttan görev; ilk sürüm/atomik bilgi+sorumlu, no-op korunur. Türün gerçek yükümlülük kaynağı uzman içerik işidir. |
| U02 uyum/risk görünümü | GET uyum hazırlığı ve dashboard | **Okuma**. | Sinyal uygunluk onayı değildir; gerçek kayda yönlendirilir. |
| O01 ayrılış dosyası ve parasal taslak | POST offboarding, settlement taslağı | `offboarding/preparation`, settlement-review **Açık kaynak bağlantısı**; gerçek dosya/uzman hesap akışı vardır. | Taslak ayrılış görevi; gerekçe/kaynak/halef ve eksik hesap kontrolleri. |
| O02 tamamlama/geri alma | offboarding/completion-preview, complete, reversal-preview, reverse | `offboarding/completion`, reversal-review **Bağlı**. | Personel/ekip/izin/dönem/erişim etkisi; sabit önizleme/sürüm. Dış ödeme/bildirim geri alınmış sayılmaz. |
| O03 parasal düzeltme ve dış sonuç | settlement-revisions ve external-reconciliation | `offboarding/correction-external` **Açık kaynak bağlantısı**; parasal sürüm/uzman/iki ayrı dış kanıt mevcut. | Düzeltme sürümünde iki görev açılır. Olağan ilk ayrılışın dış sonuç takibi ve doğru günlük kapsam B07 ile birlikte kapanacak. |
| G01 günlük iş, sorumlu ve gerçek kayıt | GET guidance/daily-tasks | `daily/task-review` **Okuma**; mevcut görevler kaynak durumdan tekilleşir/kapanır. | Güvenilir son tarih yoksa tarih üretilmez. Boş liste bütün İK işlerinin tamamlandığı anlamına gelmez. |
| G02 aksiyon oluştur/düzelt/durum/sil | POST/PUT/PATCH/DELETE `/api/actions` | `daily/task-action` **Açık kaynak bağlantısı**. | Açık aksiyon işi mevcut; durum değişince kaynaktan kapanır/yeniden açılır. Metin tarih etiketi hukuki tarih değildir. |
| G03 dış kanıt takip | Parasal revizyon dış kayıtları ve gerçek aksiyonlar | `daily/external-followup` **Okuma + açık yazım bağlantısı**. | Gönderim/ödeme yapılmış gibi tahmin edilmez; sorumlu ataması işi kapatmaz. |
| G04 analiz ve denetim izi | GET dashboard, audit-logs, search | **Okuma**. | Kaynak ve yanlış pozitif açıklaması korunur; yeni kanıt için ekran açma sayısı kullanılmaz. |

## Kalan teknik iş paketlerinin sabit listesi

Bu liste **sekiz çalışma başlığıdır, sekiz commit/gün vaadi değildir**. Bir başlık ancak ilişkilendirilmiş bütün satırların kod/API/arayüz/doğrulama sonucu raporlanınca kapanır. Yeni bulunan kusur mevcut başlığa eklenir; yeni özellik istenirse kapsam değişikliği ayrıca görünür olur.

| Başlık | Bağlı satırlar / kapanış ölçütü |
| --- | --- |
| T01 şirket yardımcı işlemleri ve erişim hazırlığı | S05–S07, P08: mevcut güvenli işlem/düzeltme ve desteklenmeyen yeteneğin açık anlatımı; uygun kaynak/kanıt; gerçek erişim/provizyon sonucu. |
| T02 işe alımın kalan alt işlemleri | R02, R04–R08: pozisyon/aday düzeltmeleri, bütün aşama kararları, not/değerlendirme ekleme/revizyonu, işe alma/provizyon için ayrı doğru kaynak ve sonuç. R09'un anlık/tarihsel sınırı açık tutulur. |
| T03 izin türü ve talep/iptal yolculuğu | L01–L02: gerçek tür/koşul kaynağı eşlemesi, rol kapsamlı talep/iptal etkisi ve kanıt. Yayın için uzman içeriği beklenir. |
| T04 puantaj/bordro/ayrılış kalan olağan bağlantılar | A02, B01/B03/B06/B07, O01/O03, G03: gerçek kaynak/önizleme/sonuç, günlük eksik ve düzeltme/external zinciri. Gelişmiş finansal bağımlılığı olağan işlem gibi genişletmez. |
| T05 günlük aksiyon ve görev kapsamının kapanışı | G02 ve tablodaki görev açıkları: gerçek tetikleyici/sorumlu/kararlı anahtar/güvenilir tarih/başka kişiyle kapanma/yeniden açılma; kaynaklı mutasyon. Her kayda yapay görev eklenmez. |
| T06 yarım iş/tekrar ve öğrenme seviyeleri | Bütün yazımlarda açık ağ sonucu ve sürüm matrisi; mevcut oluşturma/aktarım makbuzlarına diğer komutlarda otomatik garanti atfetmeme; oturum/cihaz devamı, kişisel kanıt/alıştırma/uygulama seviyesi modeli. Pilot eşikleri insan kabulünde karara bağlanır. |
| T07 bütün akış otomasyonu ve teknik teslim | Süreçler arası gerçek kayıt akışları, rol/tenant sınırı, hata/düzeltme, klavye/dar ekran; gerekli derleme/test/model kontrolleri ve kesin kaynak commitinden paket üretimi. İnsan/uzman testi değildir. |
| T08 Turhost kurulum ve geri yükleme | Hedef .NET/IIS/SQL yetki uyumu, Visual Studio profil, tek dış API yolu, SMTP, kalıcı evrak/anahtar/sır dizinleri, kontrollü üç katalog migrationı, yedek ve geri yükleme/restart provasının kanıtı. Sağlayıcı bilgileri gerektirir. |

S02–S04 ve R01/R03 ilk hazırlık paketinde, R02/R04/R06/R07 [işe alım düzeltme paketinde](2026-10-02-ik-rehberi-ise-alim-duzeltme-kanitlari.md) teknik olarak bağlandı. T02 sırayla R05'in sonraki aşama kararları, ardından R08 işe alma/provizyon sonuçlarına devam eder. T02 ve bütün şirket/işe alım modülü tamamlandı ilan edilmez; günlük iletişim ve tekrar güvenliği kendi başlıklarında korunur.

## Kaldırılmayan karar ve entegrasyon bağımlılıkları

- Belge/not/değerlendirme/kanıt saklama ve fiziksel silme; özlükte bilinmiyor ayrımı; yanlış aday kaydı iptal/taşıma/birleştirme ve mükerrer kontrolü.
- Genel aday puanının yapılandırılmış dayanağı, toplam onaylı/kalan kadro ayrımı, tarihsel huni ve aday iletişim durumunun API sözleşmeleri.
- TC'siz kişilerin farklı aktarım gönderimleri arasında tekilliği; tarayıcı yenilemesi sonrası dosya/form geri yükleme.
- İkinci parasal düzeltmenin sonraki onaylı aylara zinciri, yıl sınırı aşan telafi; banka/SGK/resmî gönderim veya otomatik doğrulama ve kanıt dosyası yükleme.

Bunlar **feragat değildir**. İlk olağan süreç hedefini güvenli kilit/uzman veya dış sorumlu yönlendirmesiyle karşılayanlar açık sınırlama olarak kalabilir; doğrudan geliştirme gerekenler T01–T06 kapsamında kararlaştırılır. Bu belge hepsinin ilk yayında otomatik entegrasyon yapılacağına dair yeni taahhüt vermez. Güncel [API bağımlılık kaydı](2026-09-25-ik-rehberi-backend-api-eksikleri.md) ayrıntılı sözleşme kaynağıdır.

Gerçek uzman incelemesi ve ayrı merkezî editör yayını, deneyimsiz kullanıcı görev gözlemi/yeterlilik eşikleri ve hedef sunucu kabulü ayrıca açık. Tüm öğrenme kapsamı tamamlanmadan yayın yapılmaması kararı korunur. Ders taslağı, test sayısı veya bu matrisin yazılması kabulün yerine geçmez.
