# Ekipzen — olağan işlem kapsam ve bitirme matrisi

2 Ekim 2026. Başlangıç [envanteri](2026-09-24-ik-rehberi-surec-envanteri.md), onaylı [plan](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md), controller/komutlar, `GuidanceOperationEvidence` ve `GetDailyGuidanceTasksQuery` karşılaştırılarak hazırlanmıştır. Bu belge önceki teslimlerin tarihsel açık iş cümleleri yerine **güncel teknik kapanış listesidir**. İnsan/uzman kabulü ayrı kapıdır. Bu paket satırları yerelde uygulanmıştır; main'e senkronizasyon tek ayrı onayla yapılacaktır.

## Durum ve kanıt tanımı

- **Bağlı:** Gerçek yazım açık seçilen güncel merkezî ders sürümüne bağlanır; sahibinin kişisel kanıtı vardır. Kaynaksız eski işlem sonradan damgalanmaz. Bu durum bütün sürecin veya kişinin yeterliliğinin kabulü değildir.
- **Açık:** İşin API'si/ekranı çoğunlukla vardır; aşağıdaki somut öğrenme/görev/düzeltme veya güvenlik bağlantısı henüz tamamlanmamıştır. Yeni API ihtiyacı ilgili satırda ayrıca belirtilir.
- **Okuma:** Liste/önizleme/analiz bir yazım değildir; açılması başarılı iş kanıtı üretmez. Açıklama/alıştırma ve ilgili işin gerçek sonucu ayrı izlenir. Her okuma için yeni mutasyon yaratılmayacak.
- **Karar/dış:** Üründe olmayan yetenek veya şirket/uzman/sağlayıcı kararı. Mevcut yol/sınır açık gösterilir; tamamlandı denmez ve sessizce kaldırılmaz.

Tablodaki dersler mevcut 10 süreç/39 adım tanımına başvurur. Bir adım birden fazla işlem öğretir; örneğin pozisyon hazırlığı ve aday oluşturma aynı `recruitment/candidate-create` açıklamasını, fakat ayrı işlem anahtarları ve kanıtları kullanır. Mutasyon ve GET denetim geçmişi aynı şey değildir. Yönetimin `GET /api/guidance/operations`, çalışanın `GET /api/leaves/learning-operations` ve kişisel veri dışa aktarımı yalnız kişinin kendi kaynaklı kanıtlarını gösterir.

## Şirket hazırlığı

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| S01 başlangıç şablonu | GET `/api/setup/preview`, POST `/api/setup` | `company/initial-setup` **Bağlı**; gerçek eklenenler, token ve no-op korunur. | Eksik başlangıç görevi; kaynak değişirse yeniden önizleme. Şablon politika onayı değildir. |
| S02 şirket profili | PUT `/api/settings/company` | `company/company-profile` **Bağlı, bu paket**; değişen alan adları, ilk token, no-op ve atomik kanıt. | Başlangıç eksikliği mevcut; profilin alan bazlı eksikliği için güvenilir zorunluluk listesi henüz yok. Aynı formdan koşullu düzeltme. |
| S03 departman ekle | POST `/api/departments` | `company/departments` **Bağlı, bu paket**. | Mevcut hazırlık ekranında boş liste açıklanır; aynı isim reddedilir. Çalışan bağlantısı kartta ayrıca yapılır. |
| S04 departman düzelt | PUT `/api/departments/{id}` | `company/departments` **Bağlı, bu paket**; ayrı kaynak, ilk token ve gerçek alan farkı. | Bağlı kişiler taşınmaz; eski token 409, no-op kanıt üretmez. |
| S05 departman silme | GET departments/{id}/deletion-preview, DELETE departments/{id} | `company/departments` **Bağlı**; incelenen ad/kod/çalışan/pozisyon sayısı belirteci, ayrı kaynak, açık teyit ve atomik kalıcı kanıt. | Bağlı çalışan veya pozisyon varsa silme kapalı. Bayat önizleme 409; aynı silme tekrarında yeni kanıt yok. Silinen kimlik geri açılmaz; ad/kod hatasında Düzenle yolu. |
| S06 logo ve bildirim tercihleri | POST settings/company/logo, PUT settings/notifications | `company/company-profile` **Bağlı**; profil yazımından ayrı iki kaynak/ilk belirteç/önizleme/teyit/işlem kanıtı. | Hata seçimi korur; güncel durum açıkça yeniden incelenir. Aynı bildirim tercihi no-op; kayıt e-posta teslimi değildir. Eski logo yalnız commit sonrası silinir. Haftalık rapor desteklenmiyor. |
| S07 rol/ekip erişimi ve hesap hazırlığı | Oturum rolü, personel kartı, işe alım/provizyon, kendi parola işlemi | `company/access-preparation` **Okuma**; hazırlık sırasından erişim rehberine, gerçek personel/işe alım ekranlarına bağlantı ve kaynaklı alıştırma. | Unvan/şifre değişimi rol onayı değildir; hesap hazır, davet teslimi ve gerçek giriş ayrıdır. Rol yönetimi/SMS/davet teslim API yetenekleri eklenmiş sayılmaz. Okuma gerçek işlem kanıtı üretmez. |

## İşe alım

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| R01 pozisyon açma | POST `/api/positions` | `recruitment/candidate-create` **Bağlı, bu paket**; adaydan ayrı kanıt. | Kadro başvuru sayısı değildir; oluşturma aday/çalışan/ilan üretmez. Mevcut sürümlü pozisyon düzeltmesiyle telafi. |
| R02 pozisyon düzelt/kapat | PUT `/api/positions/{id}`, corrections | `recruitment/candidate-create` **Bağlı**; ilk sürüm/gerekçe/önce-sonra kaydında ayrı kaynak, no-op/eski sürüm reddi. | Bağlı aday ve geçmiş korunur; kapalı pozisyonda yeni aday/işe alma durur. |
| R03 aday oluştur | POST `/api/candidates` | `recruitment/candidate-create` **Bağlı, bu paket**; puansız durum korunur. | `candidate-review:{id}` gerçek Yeni kayıttan doğar; karar son tarihi uydurulmaz. |
| R04 aday temel bilgi/pozisyon düzelt | PUT `/api/candidates/{id}`, corrections | `recruitment/candidate-create` **Bağlı**; değişmez düzeltme kaydında ayrı kaynak, beklenen sürüm/gerekçe/rol uygunluk teyidi. | Geçmiş/aşama korunur; işe alınmış başvuru kilitlidir, çalışan kartı ayrıdır. |
| R05 aşama kararı | PATCH `/api/candidates/{id}/status` | `recruitment/first-review` **Bağlı**; ilk kullanıcı/aday kanıtı korunur, sonraki gerçek kararlar ayrı kaynak/karar/sürüm/gerekçeyle saklanır. | Zorunlu gerekçe/seçilmiş güncel dayanak snapshotları vardır; Yeni görevi kaynaktan kapanır. Aynı aşama veya eski sürüm yeni kanıt üretmez. İletişim/takvim/teklif teslimi T05/API bağımlılığıdır. |
| R06 mülakat notu ekle/düzelt | POST notes ve notes/{id}/revisions | `recruitment/first-review` **Bağlı**; ekleme ve her gerekçeli yeni sürüm ayrı kaynak ve aktör kanıtı. İlk kayıt/eski karar dayanağı değişmez. | Yanlış aday notunun iptal/taşıması **karar/dış bağımlılık**; fiziksel silme yok. Eklemede ağ tekrar tekilliği ve aday beklenen sürümü ayrıca açık. |
| R07 değerlendirme ekle/düzelt | POST evaluations ve evaluations/{id}/revisions | `recruitment/first-review` **Bağlı**; puan/ölçek, düzeltmede iki sürüm ve ayrı kaynak/aktör kanıtı. | Genel puan otomatik hesaplanmaz. Yanlış aday/iptal, ölçüt standardı ve yapılandırılmış dayanak bağımlılıkları açık. Eklemenin genel tekrar/sürüm güvenliği T06'da tamamlandı. |
| R08 işe al ve başarısız hesap hazırlığını tekrar dene | POST hire ve provisioning/retry | `recruitment/hire-preparation` **Bağlı**; bağımsız sürümlü ders/alıştırma, işe almada ilk aday sürümü, ayrı işe alma/yeniden istek kanıtı. | Kalıcı doğru çalışan bağlantısı ve ayrı hesap sonucu; aktif teslimat lease'i yeniden denemeyle kaldırılmaz. Kanıt istek sonucudur; gerçek davet teslimi değildir. Geri alma/kadro/ücret ve genel gönderim makbuzu bağımlılıkları açık. |
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
| P08 personel fotoğrafı | POST/GET employees/{id}/photo; güncel kart | `personnel-entry/tab-kimlik` **Bağlı**; ayrı ilk fotoğraf belirteci/kaynak/önizleme/teyit ve atomik kişisel kanıt. | Yalnız aynı şirketin İK yöneticisi fotoğrafı görüntüler/yükler. Yanlış dosya doğru dosyayla değiştirilir; hata seçimi korur. Kullanıcı kararıyla üç evet/hayır uygunluk alanı aktif UI/DTO/yazımdan kaldırıldı; eski DB değerleri ve tarihsel kayıtlar silinmedi. Fotoğraf uygunluk onayı değildir. |

## İzin, puantaj ve bordro

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| L01 izin türü/bakiye ve talep ön inceleme | GET types/balance/ledger, POST leaves/preview, GET cancellation-preview | `leave/request-lifecycle` **Okuma**; dört başlangıç kodunun gerçek belge/politika kontrolü, örnek/resmî kaynak; özel kodda sorumlu doğrulaması. | Bayrak hukuki uygunluk değildir. Önizleme kayıt/kanıt üretmez. İş günü/hak ediş, rezervasyon ve gerçek kullanım iadesi ayrıdır; şirket takvimi ve istisnalar uzmanla doğrulanır. |
| L02 izin talebi ve iptal | POST `/api/leaves`, leaves/{id}/cancel, GET learning-operations | `leave/request-lifecycle` **Bağlı**; ayrı seçilen ders/önizleme belirteci, gerçek talep/iptalle atomik kişisel kanıt. | Bekleyen onay işi kaynaktan doğar/kapanır; yönetici yetkisi genişlemez. İptal gerçek kullanım hareketini tersler; takvim değişse de aynı gün iade edilir. 409'da taslak/sürüm korunur, açık yeniden önizleme gerekir. |
| L03 onay/ret | POST leaves/{id}/approve veya reject | `leave/decision-effect` **Bağlı**. | Bekleyen görev gerçek durumdan kapanır; eski/ikinci karar reddedilir, soru gerçek karar değildir. |
| A01 puantaj oluştur/düzelt | POST attendance/preview ve attendance; PUT attendance/{id} | `attendance/entry-review`, entry-correction **Bağlı**. | Tarih aralığı ve ilk satır sürümü korunur. Bordro girdisini otomatik düzeltmez. |
| A02 aylık özet/bordro uzlaştırma | GET puantaj özetleri ve dönem girdileri | `attendance/period-summary` **Okuma**; gerçek uzlaştırma yazımı `payroll/row-review` altında ayrı kanıttır. | Açık satırdaki incelenmiş puantaj/izin/takvim/istihdam izi değişirse uyarı ve kararlı günlük iş açılır. Toplu kontrol kapatmaz; güncel satır girdisini kaydetme kapatır. Tutarlar otomatik değiştirilmez. |
| B01 hesap ayarı ve ayrılış kuralı taslağı/onayı | PUT payroll/settings, POST settings/{id}/approve; termination-calculation-rules | `payroll/settings-review` **Bağlı**; dört ayrı taslak/onay anahtarı, sürüm, kaynak ve kişisel kanıt. | Taslak ayar görevi korunur. Kaydetme hesaplarda kullanımı veya uzman onayını temsil etmez; onaylı set değişmez. İş/kanıt aynı tenant transaction içindedir. |
| B02 dönem açma | GET periods/creation-preview, POST periods | `payroll/period-create` **Bağlı**; aynı kaynağa token ve gerçek personel/kaynak girdileri. | Açık dönem görevi; yanlış kaynak/ayda yeniden önizleme. |
| B03 satır düzeltme ve toplu kontrol | PUT periods/{id}/rows/{rowId}, POST periods/{id}/check | `payroll/row-review` **Bağlı**; satır girdisi ile güncel puantaj uzlaştırması ve toplu kontrolün kanıtları ayrıdır. | Sürüm puantaj/izin/kalendar kaynağını da içerir; bayat form 409. Kaynak değişimi hesap girdisini sessiz değiştirmez. Toplu kontrol onay veya puantaj uzlaştırma teyidi değildir. |
| B04 satır ve dönem onayı | POST rows/{id}/approve, periods/{id}/submit | `payroll/approval` **Bağlı**. | Onay sonrasında açık dönem işi kapanır; aynı işlem ödeme/beyan değildir. |
| B05 pusula/geçerli parasal sürüm | GET periods/{id}/rows/{rowId}/payslip, my | `payroll/payslip-review` **Okuma**. | Kullanıcı/kayıt kapsamı korunur; yanlış onay olağan satır düzenlemeyle açılmaz. |
| B06 uzman telafi ve parasal sürümler | compensation remediation-case, approved-impact/preview/apply/chain/revise | `payroll/correction-external` **Bağlı**; uzman dosyası taslak/onayı, açık satıra uygulama ve ilk/sonraki/zincir parasal uygulamalar ayrı kaynak/sonuç kanıtları üretir. | Mevcut önizleme, sürüm, uzman teyidi ve atomik ay zinciri kilitleri korunur. Sonraki onaylı ay varken ikinci tek-ay düzeltme/yıl aşımı gelişmiş bağımlılık olarak kilitlidir. |
| B07 onay ve olağan dış işlem | Dönem onayı; GET/POST periods/{id}/rows/{rowId}/external-reconciliation | Onay `payroll/approval`; ilk hesap dış kanıtı `daily/external-followup` **Bağlı**. | Onay dönemi Kapandı durumuna geçirmez; kapatma API’si veya banka/resmî gönderim iddiası yoktur. İlk geçerli hesapta ödeme/bildirim iki ayrı günlük iştir; revizyon oluşunca mevcut revizyon takibine geçer. |

## Uyum, ayrılış ve günlük yönetim

| ID / işlem | API veya kayıt | Ders / teknik durum | Günlük sinyal ve düzeltme |
| --- | --- | --- | --- |
| U01 uyum kaydı, bilgi/sorumlu ve durum | POST/PUT compliance, status/owner | `compliance/record-review`, record-correction, status-review **Bağlı**. | Eksik/son tarihi yaklaşan belge gerçek kayıttan görev; ilk sürüm/atomik bilgi+sorumlu, no-op korunur. Türün gerçek yükümlülük kaynağı uzman içerik işidir. |
| U02 uyum/risk görünümü | GET uyum hazırlığı ve dashboard | **Okuma**. | Sinyal uygunluk onayı değildir; gerçek kayda yönlendirilir. |
| O01 ayrılış dosyası ve parasal taslak | PUT offboarding-draft; POST calculation-draft | `offboarding/preparation`, `settlement-review` **Bağlı**; ayrı taslak kaydı ve onaylı kuraldan parasal taslak kanıtı. | Kişi/tarih/halef/kaynak ve mevcut sürüm kapıları korunur. Taslak personeli pasifleştirmez veya dış bildirim yapmaz. |
| O02 tamamlama/geri alma | offboarding/completion-preview, complete, reversal-preview, reverse | `offboarding/completion`, reversal-review **Bağlı**. | Personel/ekip/izin/dönem/erişim etkisi; sabit önizleme/sürüm. Dış ödeme/bildirim geri alınmış sayılmaz. |
| O03 parasal düzeltme ve dış sonuç | settlement-revisions ve external-reconciliation; ilk hesap GET/POST offboarding/{case}/external-reconciliation | Parasal taslak açma/yazma/onay `offboarding/correction-external`; ilk ve revizyon dış kanıtları `daily/external-followup` **Bağlı**. | Eski hesap, pasif personel ve tarih korunur. İlk tamamlanmış hesap da iki ayrı kanıt/görev taşır; yeni geçerli parasal sürümde eski kanıt salt okunur olur ve revizyon işleri açılır. |
| G01 günlük iş, sorumlu ve gerçek kayıt | GET guidance/daily-tasks | `daily/task-review` **Okuma**; mevcut görevler kaynak durumdan tekilleşir/kapanır. | Güvenilir son tarih yoksa tarih üretilmez. Boş liste bütün İK işlerinin tamamlandığı anlamına gelmez. |
| G02 aksiyon oluştur/düzelt/durum/sil | POST/PUT/PATCH/DELETE `/api/actions` | `daily/task-action` **Bağlı**; oluşturma, bilgi düzeltme, durum ve silme ayrı gerçek işlem kanıtlarıdır. | Kararlı ID ve güncel sorumlu kaynaktan okunur; başka yönetici tamamlayınca herkeste kapanır. Mevcut ileri durum kuralı korunur: tamamlanan aksiyon yeniden açılmaz. Bilgi düzeltme durum değiştirmez; silme kaynak İK işlemini tamamlamaz. Vade etiketi hukuki tarih değildir. |
| G03 dış kanıt takip | İlk hesap ve parasal revizyon dış kayıtları | `daily/external-followup` **Bağlı**; ayrı gerçek ödeme/bildirim yazımlarında kaynak sürümü ve kişisel kanıt. | Sorumlu atama kapatmaz; tamamlandı/gerekçeli gerekmiyor yalnız kendi türünü kapatır. Yeni pending kaydı yeniden açar, önceki kanıt korunur. GET ve alıştırma kanıtı işlem kanıtı değildir. |
| G04 analiz ve denetim izi | GET dashboard, audit-logs, search | **Okuma**. | Kaynak ve yanlış pozitif açıklaması korunur; yeni kanıt için ekran açma sayısı kullanılmaz. |

## Sayısal kapanış günlüğü

Toplam olağan kapsam **45 satır**dır; satır sayısı kalan iş sayısı değildir. Okuma satırları için yapay mutasyon/kanıt oluşturulmaz.

| Kontrol noktası | Bağlı | Okuma | Açık/kısmi | Açık teknik başlık |
| --- | ---: | ---: | ---: | ---: |
| İlk kapsam matrisi | 19 | 5 | 21 | 8 |
| İşe alım düzeltmeleri sonrası | 23 | 5 | 17 | 8 |
| Aday karar/işe alma sonrası | 25 | 5 | 15 | 7 |
| Şirket yardımcı işlemleri sonrası | 28 | 6 | 11 | 6 |
| İzin türü/talep/iptal sonrası | **29** | **7** | **9** | **5** |
| Puantaj/bordro/ayrılış sonrası | **36** | **8** | **1** | **3** |
| Günlük aksiyon kapanışı sonrası | **37** | **8** | **0** | **2** |
| Tekrar/yarım işlem/öğrenme aşamaları sonrası | **37** | **8** | **0** | **1** |

T04, **A02, B01, B03, B06, B07, O01, O03 ve G03** açıklarını kapatır. A02 okuma kapsamındadır; mutasyonu B03 altında kendi doğru dersine bağlanır. **G02 T05 ile kapandı; olağan işlem bağlantısı açık satırı kalmadı.** T06/T07 çapraz güvenlik ve teknik teslim kapıları da tamamlandı; kullanıcı T08 kurulumunu manuel üstlenmiştir. İnsan/uzman kabulü ayrıca izlenir.

## Kalan teknik iş paketlerinin sabit listesi

Bu liste **sekiz çalışma başlığıdır, sekiz commit/gün vaadi değildir**. Bir başlık ancak ilişkilendirilmiş bütün satırların kod/API/arayüz/doğrulama sonucu raporlanınca kapanır. Yeni bulunan kusur mevcut başlığa eklenir; yeni özellik istenirse kapsam değişikliği ayrıca görünür olur.

| Başlık | Bağlı satırlar / kapanış ölçütü |
| --- | --- |
| T01 şirket yardımcı işlemleri ve erişim hazırlığı — **tamamlandı** | S05–S07, P08: önizlemeli silme, ayrı logo/bildirim/fotoğraf kanıtları, rol/ekip/hesap hazırlığı açıklamaları ve gerçek bağlantılar. Üç uygunluk alanı kullanıcı kararıyla kaldırıldı; yeni rol/SMS/teslim yeteneği iddiası yok. |
| T02 işe alımın kalan alt işlemleri — **tamamlandı** | R02, R04–R08: pozisyon/aday düzeltmeleri, bütün aşama kararları, not/değerlendirme ekleme/revizyonu, işe alma/provizyon için ayrı doğru kaynak ve sonuç. R09'un anlık/tarihsel sınırı açık tutulur. |
| T03 izin türü ve talep/iptal yolculuğu — **tamamlandı** | L01–L02: dört başlangıç kodunun kaynak/koşul kontrolü ve özel kod yönlendirmesi; rol kapsamlı ayrı talep/iptal dersi, önizleme belirteci, gerçek kullanım iadesi ve kişisel kanıt. Yayın için uzman içeriği beklenir. |
| T04 puantaj/bordro/ayrılış kalan olağan bağlantılar — **tamamlandı** | A02, B01/B03/B06/B07, O01/O03, G03: gerçek kaynak/önizleme/sonuç, günlük eksik ve düzeltme/external zinciri. Gelişmiş finansal bağımlılığı olağan işlem gibi genişletmez. |
| T05 günlük aksiyon ve görev kapsamının kapanışı — **tamamlandı** | G02 ve tablodaki görev açıkları: gerçek tetikleyici/sorumlu/kararlı anahtar/güvenilir tarih/başka kişiyle kapanma/yeniden açılma; kaynaklı mutasyon. Her kayda yapay görev eklenmez. |
| T06 yarım iş/tekrar ve öğrenme seviyeleri — **tamamlandı** | Bütün yazımlarda açık ağ sonucu ve sürüm matrisi; mevcut oluşturma/aktarım makbuzlarına diğer komutlarda otomatik garanti atfetmeme; oturum/cihaz devamı, kişisel kanıt/alıştırma/uygulama seviyesi modeli. Pilot eşikleri insan kabulünde karara bağlanır. |
| T07 bütün akış otomasyonu ve teknik teslim — **tamamlandı** | Süreçler arası gerçek kayıt akışları, rol/tenant sınırı, hata/düzeltme, klavye/dar ekran; gerekli derleme/test/model kontrolleri ve kesin kaynak commitinden paket üretimi. İnsan/uzman testi değildir. |
| T08 Turhost kurulum ve geri yükleme — **kullanıcı manuel üstlendi** | 2 Ekim açık kullanıcı kararı: bu kısımda geliştirme veya sağlayıcı bilgi talebi yapılmayacak. Kurulum tamamlandı ya da doğrulandı sayılmaz; yayın/ortam kabulü kullanıcının sorumluluğundadır. |

S02–S04 ve R01/R03 ilk hazırlık paketinde, R02/R04/R06/R07 [işe alım düzeltme paketinde](2026-10-02-ik-rehberi-ise-alim-duzeltme-kanitlari.md) bağlandı. R05/R08 [aday karar ve işe alma](2026-10-02-ik-rehberi-aday-karar-ise-alma-ogrenme.md) ile T02'yi; S05–S07/P08 [şirket yardımcı işlemleri](2026-10-02-ik-rehberi-sirket-yardimci-islemler.md) ile T01'i kapattı. L01/L02 [izin yolculuğu](2026-10-02-ik-rehberi-izin-talep-iptal-ogrenme.md) ile T03'ü kapatır. T04 de [finansal öğrenme bağlantıları](2026-10-02-ik-rehberi-puantaj-bordro-ayrilis-baglantilari.md) ile kapandı. G02 [günlük aksiyon kapanışı](2026-10-02-ik-rehberi-gunluk-aksiyon-kapanisi.md) ile T05’i kapattı. T06 [belirsiz işlem ve öğrenme aşaması sözleşmesi](2026-10-02-ik-rehberi-tekrar-ogrenme-seviyeleri.md) ile kapandı. T07 [teknik kapanış](2026-10-02-ik-rehberi-teknik-kapanis.md) ile kapandı. Bizde kalan olağan teknik geliştirme başlığı **0**. T08’i kullanıcı manuel üstlenmiştir. Günlük iletişim ve R09 tarihsel huni kayıtlı API bağımlılıklarıdır; desteklenmeyen işlemlerde sınır ve uzman yönlendirmesi korunur. Yayın kabulü tamamlandı sayılmaz.

## Kaldırılmayan karar ve entegrasyon bağımlılıkları

- Belge/not/değerlendirme/kanıt saklama ve fiziksel silme; aktif ekranda kaldırılan üç uygunluk alanının eski verisinin saklama/temizleme kararı; yanlış aday kaydı iptal/taşıma/birleştirme ve mükerrer kontrolü.
- Genel aday puanının yapılandırılmış dayanağı, toplam onaylı/kalan kadro ayrımı, tarihsel huni ve aday iletişim durumunun API sözleşmeleri.
- TC'siz kişilerin farklı aktarım gönderimleri arasında tekilliği; tarayıcı yenilemesi sonrası dosya/form geri yükleme.
- İkinci parasal düzeltmenin sonraki onaylı aylara zinciri, yıl sınırı aşan telafi; banka/SGK/resmî gönderim veya otomatik doğrulama ve kanıt dosyası yükleme.

Bunlar **feragat değildir**. İlk olağan süreç hedefini güvenli kilit/uzman veya dış sorumlu yönlendirmesiyle karşılayanlar açık sınırlama olarak kalabilir; doğrudan geliştirme gerekenler T01–T06 kapsamında kararlaştırılır. Bu belge hepsinin ilk yayında otomatik entegrasyon yapılacağına dair yeni taahhüt vermez. Güncel [API bağımlılık kaydı](2026-09-25-ik-rehberi-backend-api-eksikleri.md) ayrıntılı sözleşme kaynağıdır.

Gerçek uzman incelemesi, ayrı merkezî editör yayını, deneyimsiz kullanıcı görev gözlemi/yeterlilik eşikleri ve hedef sunucu kabulü ayrıca açık. 2 Ekim kullanıcı kararı: Turhost yayını ve gerçek insan/uzman kontrolü kullanıcı sorumluluğunda; tamamlanmış sayılmaz, yayın kuralları kaldırılmaz. Tüm öğrenme kapsamı tamamlanmadan yayın yapılmaması kararı korunur. Ders taslağı, test sayısı veya bu matrisin yazılması kabulün yerine geçmez.

## Kullanım bütçesi ve bitirme sınırı

2 Ekim başlangıç ölçümü haftalık %21 kalan kullanımdır. T04–T07 için toplam yaklaşık 14 yüzde puanı hedeflenir; yaklaşık 7 puan deneme sonrası acil düzeltme rezervidir. Bu tahsis uygulanan bir hesap limiti değildir: tüketim işe ve diğer hesap oturumlarına göre değişir. Paket sonunda kullanım ölçülür, ilgili dosyalar dar okunur, bağımsız kontroller birlikte çalıştırılır, başarısızlık/yeni değişiklik yoksa geçen testler yeniden koşturulmaz. Sabit 45 satır veya kapanan T01–T07 yeniden açılıp sürekli yeni geliştirme paketi yaratılmaz; gerçek hata mevcut başlıkta takip edilir. Güvenlik veya doğrulama gereksinimi tasarruf adına kaldırılmaz.

## T07 teknik kapanışı — 2 Ekim

Son genel koşum: frontend 104 dosya/632 test; backend 101 birim ve 353 entegrasyon testi geçti. İki model kontrolü temiz, kaynak commitinden derleme ve SHA-256 manifestiyle teslim hazırlanır. Dar ekran/klavye ve gerçek API kayıt-kayıp yanıt-kontrol akışı doğrulandı; personel kartı olmayan yönetici izin ekranı giriş sorunu düzeltildi. Kesin paket/SHA bilgisi senkronizasyon raporundadır. T01–T07 kapalı; uzman/insan/merkezî editör kabulü ve yayın kullanıcıya aittir, tamamlanmış sayılmaz.
