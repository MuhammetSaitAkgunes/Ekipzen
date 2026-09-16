# Ekipzen — Pilot hazırlığı ve kod kalitesi raporu

Tarih: 7 Eylül 2026
İncelenen sürüm: main@309c4dee57bb1c68b99b51a8ea74276e44ec879a
Amaç: İlk gerçek müşteri pilotuna en kısa, ölçülebilir geliştirme yolunu belirlemek.

## 1. Yönetici değerlendirmesi

**Ürün, işlevsel bir demo seviyesini aşmış; güçlü bir uygulama çekirdeğine sahip. Buna rağmen bugün gerçek müşteri operasyonunu doğrudan üzerine taşımaya hazır değil.** Eksiklerin ağırlık merkezi yeni modüller değil: demosuz ilk kurulum, üretim yapılandırması, veri kurtarma, bazı iş kuralları ve pilot kabul kanıtları.

Kodun genel niteliğini **iyi temelli, modüller arasında olgunluğu değişen** olarak değerlendiriyorum. Çekirdek mimarinin yeniden yazılması gerekmiyor. Son bordro, istihdam ve izin çalışmaları işlem bütünlüğünü belirgin biçimde güçlendirmiş. Bunun yanında ilk kullanıcı deneyimi ve işletim hazırlığı aynı seviyeye ulaşmamış.

En hızlı yol: **tek şirket, öneri olarak 10–30 çalışan, sınırlı ve açıkça desteklenen süreçler, yakın operatör desteği.** Personel, evrak, izin ve onay akışları günlük kullanımda denenebilir; bordro ilk aşamada müşterinin mevcut bordrosuyla paralel karşılaştırılır. Gerçek ödeme ve resmî işlem kaynağı olma kararı ayrıca verilir. Bu kapsam öneridir; müşteri büyüklüğü ve çalışma düzeni henüz bilinmiyor.

| Değerlendirme alanı | Bugünkü durum | Pilot açısından anlamı |
| --- | --- | --- |
| Kontrollü demo | Hazır | Ürünün ana akışları gösterilebilir |
| Personel ve süreç çekirdeği | Güçlü | İşe giriş/çıkış, halef, izin defteri ve yetki temeli mevcut |
| Yeni müşterinin ilk kurulumu | Engelli | Demo dışı zorunlu başlangıç verileri ve ilk bordro ayarı akışı eksik |
| Kanuni bordro | Sınırlı profil için geliştirilmiş; kabul bekliyor | Otomatik test başarısı, bağımsız bordro kabulünün yerine geçmiyor |
| Üretim güvenliği ve dağıtım | Kısmi | Geliştirme ayarlarından kontrollü üretim kurulumuna geçiş gerekiyor |
| Yedekleme ve kurtarma | Araçlar var; operasyon kanıtı eksik | Zamanlama, uzak kopya, alarm ve evrak dahil geri dönüş tamamlanmalı |
| Kalite güvencesi | Güçlü yerel regresyon temeli | Boş müşteri, gerçek tarayıcı ve dağıtım senaryoları eklenmeli |
| Ücretli pilot kabulü | Henüz verilmemeli | Aşağıdaki çıkış ölçütleri kapatılmalı |

Tek bir “% hazır” rakamı vermiyorum: veri kurtarma veya ilk müşteri açılışı gibi bir engel, çok sayıda tamamlanmış ekranla telafi edilemez. **Yeni özellik geliştirmeden, hedefli 10–15 mühendislik iş günü içinde kontrollü pilot adayı çıkarılması makul bir planlama tahminidir.** Bu bir teslim taahhüdü değildir; barındırma, müşteri verisi ve karar sahiplerinin hazır olması varsayılmıştır. Uzman kabulü ve gerçek paralel bordro dönemi ayrıca takvime bağlıdır.

## 2. Kanıt ve incelemenin sınırı

Bu raporda kaynak kod, migrationlar, CI tanımı, test düzeni, özellik sicilleri, yedekleme araçları ve güncel paket güvenlik duyuruları incelendi.

- Yerel main ile GitHub main, **309c4de** commit’inde eşleşiyor.
- Son test edilen feature sürümü **748fa71** ile main’in dosya ağacı aynı: `git diff --quiet 748fa71 309c4de` sonucu 0.
- Önceki geliştirme turunda aynı dosya ağacı için **91 birim + 201 entegrasyon + 155 frontend = 447 test** başarılıydı; backend derleme, frontend lint/build ve dört bordro migration’ıyla SQL Server yükseltmesi de başarılıydı. Bu analizde aynı testleri tekrar koşturmadım.
- Bu turda yeni olarak npm ve NuGet zafiyet sorguları çalıştırıldı.
- GitHub bağlayıcısında bu commit için workflow/status listeleri boş döndü. **Son commit’in uzak CI başarısı doğrulanmış sayılmadı.** Yerel test sonucu ile GitHub sonucu ayrıdır.
- Canlı müşteri ortamı, tarayıcı senaryosu, yük testi ve yeni bir geri yükleme tatbikatı bu analizde yürütülmedi. Koddan belirlenen davranışlar aşağıda bu nitelikleriyle sunulmuştur.
- Eski özellik sicilinin toplam açık bulgu sayıları güncel değil. Rapor, eski toplamları bugünün sayısı olarak kullanmıyor.

## 3. Kod kalitesi

### Korunması gereken güçlü yönler

**Katmanlı modüler monolit.** API → Application → Domain ayrımı ve Infrastructure uygulamaları anlaşılır. MediatR, FluentValidation ve ortak hata sözleşmesi kullanılıyor. Domain dış paket bağımlılığı taşımıyor. Application katmanı EF Core ve ClosedXML kullanıyor; dolayısıyla mimari tamamen altyapıdan bağımsız değil, ancak pratik sınırları olan bir Clean Architecture uygulaması. Pilot için mikroservis dönüşümünün getirisi yok.

**Tenant ve erişim temeli.** Müşteri başına SQL kataloğu, ayrı platform veritabanı, tenant filtreleri, rol/satır erişim denetimleri ve tenant önekli dosya depolama mevcut. Dosya yolu sınır kontrolü ve personel belge erişim korumaları açık. İzolasyon ve yetki testleri önemli bir varlık; yine de bu rapor bir penetrasyon testi değildir.

**İşlem bütünlüğü.** Bordro dönem/satır kilitleri, eşzamanlı izin onayı koruması, kalıcı ve tekrar denenebilir kimlik provizyon işleri, sürüm kontrollü işten çıkış ve değişmez hesap snapshot’ları iyi mühendislik kararları. Bunlar pilotta en pahalı olabilecek çift işlem ve yarım kalmış süreç risklerini azaltıyor.

**Tip ve test altyapısı.** C# nullable açık; API şemasından frontend tipleri üretiliyor. Entegrasyon testleri gerçek SQL Server üzerinde çalışıyor. CI tanımında backend uyarıları hata sayılıyor. Test sayısından daha değerli olan, dönem sırası, eşzamanlılık ve tenant izolasyonu gibi davranışların sınanması.

### Kaliteyi sınırlayan noktalar

| Alan | Bulgu | Öncelik |
| --- | --- | --- |
| Frontend tip güvencesi | tsconfig dosyalarında strict/strictNullChecks etkin değil; API tipi üretimi tek başına null durumlarını güvenceye almıyor | Önce kritik form/boş veri akışları; kademeli strict geçiş |
| İlk kullanım testleri | Demo verisiyle çalışan testler temiz müşteri deneyimini temsil etmiyor | Pilot öncesi |
| UI boş/hata durumu | Bordro ayarlarında ilk onaylı kayıt yokken hata görünmeden yüklenme döngüsü oluşabiliyor | Pilot öncesi |
| Kod yoğunluğu | Offboarding iş akışı 669, IdentityService 576, PersonnelModal 516 boş olmayan satır civarında | İlgili iş değişirken böl; toplu refactor yapma |
| Veri erişimi | Aday detayında çoklu koleksiyon Include zinciri var; veri büyüdüğünde sorgu çoğalması riski | ATS pilotta açılırsa ölç ve düzelt |
| Operasyonel görünürlük | Personel/izin bildirimlerinde catch blokları hatayı loglamadan yutuyor | Pilot bildirimleri kullanılacaksa düzelt |
| Doküman güvenilirliği | Kapı belgesinin üst bilgisi eski branch’i, sicil eski test/bulgu toplamlarını gösteriyor | Tek güncel pilot listesiyle toparla |
| CI kapsamı | Frontend tsc/lint/test var; Vite üretim build’i, tarayıcı kabulü ve paket audit kapısı yok | Pilot öncesi temel CI genişletmesi |

Kod kalitesine ilişkin karar: **Mimariyi koru; boş veri, hata, sürüm yükseltme ve işletim akışlarını güçlendir.** Bugünkü en yüksek getiri geniş refactor değil, kullanıcıyı durduran somut boşlukları kapatmaktır.

## 4. Öncelikli bulgular ve en küçük çözüm

P0 = İlgili pilot kullanımını başlatmadan kapatılmalı. P1 = Sınırlı pilotta açık kapsam/manuel süreçle yönetilebilir.

### P0-1 — Demosuz müşteri başlangıcı tamam değil

TenantOnboarding, veritabanı ve rolleri oluşturduktan sonra admin davetiyle tenant’ı aktif yapıyor. TenantSchemaInitializer yalnız migration ve rolleri hazırlıyor. İzin türlerini ve tatil takvimini ekleyen yol AppDbContextInitializer içindeki demo seed’i. Bunları üretim onboarding’ine taşıyan bir katalog başlangıcı bulunmadı.

**Etkisi:** Yeni müşteri giriş yapabilse de izin türleri boş kalabilir; iş günü hesabında tatiller eksik olur. Yeni tenant açılabiliyor olması, İK operasyonuna hazır olduğu anlamına gelmez.

**En küçük çözüm:** Demo çalışanlarından ayrılmış, tekrar çalıştırılabilir başlangıç kataloğu; şirket profili, izin türleri, desteklenen takvim ve ilk mevzuat taslağı için açık kurulum adımı. Mevzuat taslağı otomatik uzman onayı almış gibi işaretlenmemeli.

**Kabul:** Sıfır tenant → davet → giriş → personel → izin talebi/onayı → ilk mevzuat taslağı. Demo seed çağrısı olmadan tüm yol tamamlanmalı.

Kanıt: [TenantOnboarding](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Application/Features/Tenancy/TenantOnboarding.cs:89>), [TenantSchemaInitializer](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Infrastructure/Tenancy/TenantSchemaInitializer.cs:23>), [Demo katalogları](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Infrastructure/Persistence/AppDbContextInitializer.cs:396>).

### P0-2 — İlk onaylı mevzuat seti yoksa ayar ekranı kilitleniyor

Resolver, onaylı set bulamazsa 404 veriyor. SettingsTab formu yalnız başarılı settingsQ.data ile kuruyor; `form === null` kontrolü hata kontrolünden önce yüklenme ekranını döndürüyor. Taslak listesi gelse bile bu dönüş nedeniyle gösterilemiyor.

Ayrıca son migration mevcut PayrollSettings kayıtlarını bilinçli olarak Draft yapıyor. Bu doğru güvenlik sınırı, onaylı seti kalmayan yükseltilmiş tenant’larda da aynı ekran engeline yol açıyor. Kod akışından yüksek güvenle belirlenmiştir; bu turda tarayıcı üzerinden yeniden üretilmemiştir.

**En küçük çözüm:** Yüklenme/hata/boş durumlarını ayır; onaylı set olmadan ilk taslak oluşturma ve mevcut taslağı açma/onaylama yolu sun.

**Kabul:** Boş set, yalnız taslak, gelecekte onaylı set, normal onaylı set ve API hatası senaryoları. Yükseltme sonrası operatör SQL müdahalesi olmadan ayarı tamamlayabilmeli.

Kanıt: [SettingsTab: erken dönüş](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/frontend/src/features/payroll/SettingsTab.tsx:81>), [Onaylı set çözümü](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Application/Features/Payroll/Settings/PayrollSettingsHandlers.cs:14>), [Migration: Draft geçişi](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Infrastructure/Persistence/Migrations/20260907074322_AddStatutoryPayrollEngine.cs:205>).

### P0-3 — Mevcut dağıtım örneği gerçek müşteri kurulumu için yeterli değil

Program tüm ortamlarda AllowAnyOrigin kullanıyor. Uygulamada açık HSTS/güvenilir proxy yapılandırması bulunmuyor; bunlar seçilen barındırma katmanında da doğrulanmış değil. IP tabanlı hız sınırlaması proxy arkasında gerçek istemci IP’siyle sınanmalı. CORS açıklığı tek başına yetkisiz veri okunduğunun kanıtı değildir.

Compose Development modunda, geliştirme parolalarıyla ve SQL Developer edition ile geliyor. API konteynerine dosya kalıcılığı için volume tanımlanmamış; LocalFileStorage varsayılanı konteyner içindeki App_Data/storage. Konteyner yeniden oluşturulursa kalıcı bağlanmamış evraklar kaybolabilir.

**En küçük çözüm:** Tek seçilmiş pilot topolojisi; Production ayarları, HTTPS, tanımlı origin/host/proxy, ayrı sırlar, kalıcı evrak ve gerekli Data Protection anahtar deposu, SMTP, sürümlü yayın artefaktı ve migration sırası. SQL hesabının uygulama/operasyon yetkileri açıkça sınırlandırılmalı.

**Kabul:** Üretim benzeri ortamda yeni kurulum ve yeniden yayın; eski evrakı indirme, davet kabulü, doğru IP/rate-limit, yetkisiz origin ve DB kesintisi kontrolleri.

Developer edition gerçek üretim hizmeti için lisanslanmış değildir; pilotun gerçek operasyon kullanımına uygun SQL lisansı seçilmelidir. [Microsoft lisans rehberi](https://www.microsoft.com/licensing/guidance/SQL). Proxy davranışı seçilen topolojiyle doğrulanmalıdır. [Microsoft proxy rehberi](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Kanıt: [Program](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.API/Program.cs:174>), [Compose](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/docker-compose.yml>), [Dosya deposu](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Infrastructure/Storage/LocalFileStorage.cs:25>).

### P0-4 — Yedek araçları mevcut; gerçek işletim güvencesi henüz kanıtlı değil

DB keşfi, tam/log yedek, ayrı kataloğa restore, evrak arşivi ve uzak kopya seçenekleri mevcut. Dolayısıyla bu alan sıfırdan yazılmayacak. Ancak son kayıtlar zamanlanmış görevlerin kurulmadığını, uzak hedefin seçilmediğini ve logların izlenmediğini söylüyor. Önceki uygulama/DB tatbikatı evrak geri yüklemesini kapsamamış. Bu raporda hedef sunucu işletimi incelenmedi.

**En küçük çözüm:** Mevcut araçları hedef ortamda kur; platform + müşteri DB’leri + evrak + saatlik log zinciri için uzak kopya ve alarm üret.

**Kabul:** Ayrı ortama geri dönüş, giriş, personel/izin verisi, bir bordro PDF’i ve yüklenmiş evrakın açılması. Tanımlı RPO 1 saat / tek müşteri RTO 4 saat hedefleri gerçek ölçümle doğrulanmalı; alarm gerçekten operatöre ulaşmalı.

Kanıt: [Kurtarma kayıtları](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/docs/yedekleme-ve-kurtarma.md:601>), [Tatbikat aracı](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/scripts/backup-restore-drill.ps1>), [Zamanlama aracı](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/scripts/register-backup-task.ps1>).

### P0-5 — Ayar ekranında gerçekleşmeyen güvenlik vaadi var

“SMS ile doğrulama” seçeneği girişte tek kullanımlık kod gönderildiğini söylüyor. Backend yalnız TwoFactorSmsEnabled tercih alanını kaydediyor; incelenen LoginAsync parola doğrulamasından sonra doğrudan token üretiyor. Aynı biçimde haftalık rapor seçeneği bulunuyor, ancak haftalık gönderim işi bulunmadı.

**En küçük çözüm:** Pilot görünümünde uygulanmayan seçenekleri kapat/gizle; yalnız saklanan ayarı etkin güvenlik kontrolü gibi sunma. Gerçek MFA gerekiyorsa ayrı uçtan uca iş olarak tamamla. Parola politikası da mevcut demo uyumlu altı karakter seviyesinden pilot kimlik standardına taşınmalı.

**Kabul:** Kullanıcı arayüzündeki her açık güvenlik/bildirim vaadi gerçek davranışla eşleşmeli.

Kanıt: [SMS vaadi](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/frontend/src/features/settings/SettingsPage.tsx:277>), [Tercih kaydı](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Application/Features/Settings/SettingsHandlers.cs:126>), [Login akışı](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Infrastructure/Identity/IdentityService.cs:90>).

### P0-6 — Bordro motoru, puantaj girdisinin doğruluğunu tek başına garanti etmiyor

AttendanceCalculator günlük 480 dakika üstünü mesai sayıyor; haftalık çalışma düzeni hesabı yok. 5 gün × 9 saat girdisi bu algoritmada haftada 5 saat mesai üretir; bunun hangi hukuki çalışma türüne karşılık geldiği algoritmada modellenmiyor. Gece yarısını aşan vardiya reddediliyor. Bordro onayı sonrası puantajı değiştirmeyi engelleyen dönem kontrolü de attendance yazma yollarında yok.

Bordro motorundaki teyit alanları yararlı, fakat “fazla çalışma kontrol edildi” kutusu otomatik haftalık hesaplama veya bağımsız belge incelemesi değildir.

**En küçük çözüm:** Pilot şirketin çalışma düzenini belirle; desteklenen mesai sınıfını doğrulanmış girdiyle besle; onaylı dönem puantajını kilitle veya izlenebilir düzeltme sürecine bağla. Gece/hafta tatili/genel tatil gibi desteklenmeyen profiller pilotta işleme alınmamalı.

**Kabul:** Puantaj → bordro girdi mutabakatı, dönem kilidi ve müşteri çalışma düzenine uygun sınır senaryoları.

Kanıt: [Puantaj hesabı](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Application/Features/Attendance/AttendanceDtos.cs:92>), [Puantaj yazma yolları](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Application/Features/Attendance/Commands/AttendanceEntryCommands.cs:119>), [Bordro teyitleri](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Application/Features/Payroll/Periods/PayrollRowReadiness.cs:44>).

### P0-7 — Finansal pilot için bağımsız kabul ve izin politikası gerekiyor

Bordro standart yurt içi özel sektör 4/a profiliyle sınırlandırılmış. SGDP, özel teşvikler, bazı yan haklar ve farklı çalışma türleri kapsam dışı. İzin defteri güçlü olmakla birlikte istihdam döneminin 24 günlük varsayılan politikası müşterinin tüm kıdem/yaş/sözleşme durumları için otomatik hukuki doğrulama anlamına gelmiyor. Demo tatil takvimi tam gün 2026 kayıtlarıyla sınırlı; yarım gün ve sonraki yıl yolu ayrıca ele alınmalı.

**En küçük çözüm:** Pilot personel profillerini ele; şirket izin politikasını kaydet; mevzuat/ücret girdilerini uzmanla doğrula. Bir gerçek bordro dönemini müşterinin onaylı referans bordrosuyla kalem kalem karşılaştır. İşten çıkışta mevcut uzman onaylı finansal snapshot akışı kullanılabilir.

**Kabul:** Her farkın açıklanması; açıklanamayan parasal fark kalmaması; matrah/vergi/istisna/prim/net/işveren maliyeti eşleşmesi ve yazılı uzman kabulü. Desteklenmeyen profiller için onay kapısı kapalı kalmalı.

Kanıt: [Kanuni kapsam](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/docs/bordro-kanuni-kapsam-ve-isletim.md>), [İstihdam izin politikası](<C:/Users/Lenovo/OneDrive/Masaüstü/İK Pro UI/İK Pro/backend/src/Ekipzen.Domain/Entities/Organization/EmployeeEmployment.cs:16>).

### P1 — Pilot kapsamında yönetilecek diğer eksikler

- **Risk analitiği:** Skorlar SQL’de hesaplanıyor; temel EmployeeMetricSnapshot verisini yazan yol incelemede demo seed’iyle sınırlı bulundu. Gerçek müşteri için sürdürülebilir veri besleme yolu kanıtlanmadan otomatik tahmin ürünü olarak satılmamalı. İlk pilotta operasyonel raporlar kullanılabilir.
- **Personel/ATS doğrulaması:** TC alanı yalnız 11 rakam kontrolünde; işe alım tarihi için açık iş kuralı yok. Veri girişinde bunlar görünür kontrol edilmeli. ATS ilk pilotta açılmayacaksa geniş ATS geliştirmesi ertelenebilir.
- **Abonelik:** Abonelik kaydı/görünümü var; otomatik tahsilat, paket limitleri ve ürün yetkilendirmesi tamamlanmış kabul edilmemeli. Tek müşteri için sözleşmeyle belirlenmiş sabit kapsam ve manuel faturalama yeterli olabilir.
- **Lisans:** QuestPDF Community seçilmiş. Şirketin kullanım ve gelir koşullarına uygunluğu lansman öncesi kontrol edilmeli; kod seçimi tek başına uygunluğu kanıtlamaz. [QuestPDF lisans koşulları](https://www.questpdf.com/license/community.html).
- **KVKK ve destek:** Teknik izolasyon var; müşteriyle uygulanacak saklama, erişim, destek ve veri teslim/silme sorumluları pilot sözleşmesinde netleştirilmeli. İmzalı müşteri belgeleri bu incelemede görülmedi.

## 5. Bağımlılık ve yayın zinciri

| Bu turda yapılan kontrol | Sonuç | Yorum |
| --- | --- | --- |
| npm audit — tüm bağımlılıklar | 7 yüksek, 1 orta; toplam 8 paket kaydı | Duyuru/yol sayısıyla gerçek sömürülebilir açık sayısı aynı değil |
| npm audit — dev hariç | 2 yüksek paket kaydı | react-router ve react-router-dom aynı kök duyurudan etkileniyor |
| NuGet — transitive dahil, mevcut çözülmüş paketler | 6 projede kayıtlı zafiyet bildirilmedi | Kaynak kodun veya runtime’ın tüm risklerinin tarandığı anlamına gelmez |
| GitHub Actions tanımı | Var | Son main koşusunun başarısı bu turda doğrulanamadı |
| CI Node sürümü | 20 | Destek dışı; yereldeki Node 24 ile eşitlenmeli |
| .NET | net9.0 | Destek sürüyor; yükseltme takvimi yakın |

React Router duyurusu **unstable RSC API’leri** için geçerli. Uygulama createHashRouter kullanan istemci React SPA’sı; bu yolun kullanımı bulunmadı. Bu nedenle mevcut uygulamada kanıtlanmış CSRF sömürüsü iddia etmiyorum. Yine de uygun yama sürümüne kontrollü güncelleme, düşük maliyetli bakım işidir. Diğer kayıtlar geliştirme araçları zincirinde; güncelleme ve tekrar audit yapılmalı, körlemesine major/force güncellemesi uygulanmamalı. [Resmî GitHub duyurusu](https://github.com/advisories/GHSA-qwww-vcr4-c8h2).

.NET 9 desteği **10 Kasım 2026** tarihinde bitiyor; bugün destek dışı değil. İlk kısa pilot için güncel 9.0 servis sürümü kullanılabilir, .NET 10 LTS geçişi bu tarihten önce planlanmalı. CI’daki Node 20 ise artık EOL. [Microsoft destek politikası](https://dotnet.microsoft.com/en-us/platform/support/policy), [Node sürüm durumu](https://nodejs.org/en/about/previous-releases).

## 6. Pilota en kısa geliştirme sırası

Aşağıdaki süreler tek etkin mühendislik hattı, hazır karar sahipleri ve sınırlı müşteri profili varsayımıyla tahmindir. DevOps/uzman/müşteri bekleme süreleri mühendislik süresine dahil değildir.

| Paket | Somut çıktı | Tahmini iş | Geçiş koşulu |
| --- | --- | ---: | --- |
| 1 — İlk müşteri çalışabilsin | Demosuz katalog kurulumu, ilk mevzuat taslağı, ayar ekranı boş/hata düzeltmesi, mevcut Draft kayıtları kurtarma yolu | 2–3 gün | Sıfır tenant ile ilk izin ve bordro taslağı |
| 2 — Güvenli pilot ortamı | Production yapılandırması, kalıcı evrak/anahtarlar, SMTP, CORS/proxy, çalışmayan ayar vaatlerinin kapatılması, bağımlılık/CI düzeni | 3–4 gün | Yeniden yayında veri korunması; gerçek TLS/davet/yetki kontrolleri |
| 3 — Parasal girdiler ve kurtarma | Pilot çalışma/izin politikası, dönem kilidi, girdi mutabakatı; mevcut yedek araçlarının kurulması ve evrak dahil restore | 3–5 gün | Puantaj-bordro izi, çalışan yedek/alarm ve ölçülmüş geri dönüş |
| 4 — Pilot kabulü | Boş müşteri ve rollerle tarayıcı senaryoları, veri aktarımı, temsilî yük, kullanıcı eğitimi ve hata düzeltmeleri | 2–3 gün | Kullanıcı kabul kaydı ve yayın kararı |
| Paralel iş | SMMM örnek bordro incelemesi, müşteri/sunucu/sözleşme kararları | Dış takvime bağlı | Finansal kullanım için bağımsız kabul |

Toplam mühendislik bandı **10–15 iş günü**. Kararlar hazırsa yaklaşık **2–3 çalışma haftasında pilot adayı** hedeflenebilir. Tüm destek dışı bordro profilleri, gece vardiyası ve kapsamlı otomatik tahsilat istenirse bu bandın yeniden tahmin edilmesi gerekir.

Önerilen pilot kapsamı:

- Tek müşteri; başlangıçta 10–30 çalışan; tanımlı gündüz çalışma düzeni.
- Personel/ücret geçmişi, evrak, izin ve onay, manuel doğrulanmış puantaj.
- Bordroda yalnız desteklenen profiller ve mevcut referansla paralel hesap.
- Yeni AI/risk besleme hattı, özel teşvikler, SGDP, gece vardiyası ve otomatik tahsilat kapsam genişlemesine bırakılır.
- Kapsam dışı işlem yalnız sözlü uyarıyla yönetilmez; erişim/işlem kapısı ve kullanıcıya açık durum uygulanır.
- Müşteri mevcut bordro ve kritik operasyon yedeğini, pilot kabulü tamamlanana kadar korur.

Bu sıralama mevcut geliştirme kapılarını hedefe göre yeniden düzenler: **önce ilk müşteri ve işletim boşlukları; ardından o müşterinin kullandığı kurallar.** Tüm pazar profillerini tamamlamayı ilk pilotun önüne koymaz.

## 7. “Pilot açılabilir” kararı için ölçülebilir kabul

1. Demo seed olmadan kurulmuş müşteriyle davet, giriş, personel aktarımı, evrak ve izin/onay akışları tamamlanır.
2. HR-admin, manager ve employee erişimleri; ikinci kontrol tenant’ı üzerinden tenant sınırı; işten çıkış sonrası eski oturumun reddi üretim benzeri ortamda doğrulanır.
3. Onaylı ayarı olmayan ve migration ile yalnız Draft kalmış müşteride uygulama ilk kuruluma yönlendirir; sonsuz yüklenme olmaz.
4. Pilotun kullandığı çalışma/tatil/izin politikası kayıtlıdır; onaylı bordronun puantajına kontrolsüz müdahale edilemez.
5. Başarısız SMTP/provizyon, eski tenant şeması, veritabanı kesintisi ve yedek hatası operatöre görünürdür.
6. Uzak yedekten başka ortama dönüşte DB ve evrak birlikte doğrulanır; RPO/RTO ölçümü kaydedilir.
7. Aynı yayın artefaktı için CI, üretim build’i ve temel tarayıcı senaryoları başarılıdır. Pilota uygun örnek yükte gecikme ve hata oranı ölçülür; eşikler yük testi başlamadan belirlenir.
8. Sorumlu müşteri kullanıcısı ve destek sahibi belirlenir; açık P0 kalmaz. Finansal kullanım için ayrıca bir paralel bordro dönemi ve uzman kabulü tamamlanır.

Pilot başarısını özellik sayısıyla değil şu çıktılarla takip edin: İK yöneticisinin geliştirici müdahalesi olmadan tamamladığı işlemler, veri aktarımının mutabakatı, izin/onayda hata sayısı, bordroda açıklanamayan fark sayısı, destek müdahalesi sıklığı ve kullanıcıların mevcut yönteme kıyasla kazandığı süre. Başlangıç ölçümü alınmadan tasarruf yüzdesi vaat edilmemeli.

## 8. Ticari olarak en doğru ilk vaat

İlk pilotun güçlü satış vaadi **“personel, evrak, izin/onay ve ücret geçmişini tek yerde; değişiklikleri izlenebilir ve erişimleri kontrollü biçimde yönetmek”** olmalı. Bordro için vaat, açık destek profili içinde doğrulanmış hesap ve kontrol izi üzerinden kurulmalı.

“Sınırsız mevzuat kapsamı”, “SMS MFA aktif”, “canlı veriden otomatik ayrılma/tükenmişlik tahmini” ve “tam otomatik abonelik” bugünkü kodla desteklenen genel vaatler değil. Bu ifadeleri düzeltmek, bu özelliklerin hepsini ilk pilot öncesi geliştirmekten daha hızlı ve dürüst bir ürün kararıdır.

**İlk geliştirme önerisi:** P0-1 ve P0-2’yi tek, küçük bir “demosuz ilk müşteri kurulumu” paketi olarak ele almak. Mevcut müşteri yükseltmesindeki ayar ekranı engelini önce kapatmak, ardından aynı kabul senaryosunu sıfır tenant’ta geçirmek.

Bu rapor yalnız analiz ve planlama çıktısıdır. Uygulama kodu, çalışma ayarları ve GitHub referansları değiştirilmedi. Geliştirme seçildiğinde her paket tamamlanıp doğrulanır; repository kuralına göre bildirilen commit/push/merge kapsamı için tek senkronizasyon onayı alınır.

