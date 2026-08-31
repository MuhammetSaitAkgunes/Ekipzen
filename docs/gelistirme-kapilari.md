# İK Pro — Geliştirme ve Satışa Hazırlık Kapıları

**Son güncelleme:** 2026-08-31

**Güncel çalışma tabanı:** `main@835aac7`; özellik dalı
`codex/gate3-offboarding-closure` (yerel, senkronizasyon onayı bekliyor)

**Kaynak:** özellik doğrulama sicili, Faz 2b mimari incelemesi ve tam test paketi

Bu belge, sıradaki geliştirmeyi yalnız özellik listesiyle değil, müşteriye zarar
verebilecek risklerin kapatılmasıyla yönetir. Bir kapı; kod, otomatik test, bulgu
kaydı ve çıkış kanıtı birlikte tamamlanmadan kapanmış sayılmaz.

## Güncel sıra

| Kapı | İçerik | Durum | Ticari etkisi |
| --- | --- | --- | --- |
| 0 | Personel erişim güvenliği (`E-1`) | ✅ Tamamlandı, `main` ve GitHub senkron | İşten ayrılan kullanıcının şirkete erişimini keser |
| 1 | API doğrulama ve hata sözleşmesi (`X-1`, `C-2`, `N-1`) | ✅ Tamamlandı ve senkronlandı | Entegrasyon hatalarını anlaşılır 400 yanıtlarına çevirir; gerçek 500 alarm gürültüsünü azaltır |
| 2 | Bordro ve parasal doğruluk | ✅ Üç dilim tamamlandı ve senkronlandı | Yanlış/negatif ödeme ve hatalı vergi matrahı riskini kapatır |
| 3 | Personel yaşam döngüsü ve işlem bütünlüğü | 🟡 `E-3`, `R-6`, `E-2`, `E-5A+B` tamamlandı; `E-5C` karar girdileri bekliyor | İşe alım/işten çıkış süreçlerini yarım kalmış veri ve sahipsiz onaylardan korur |
| 4 | İzin, puantaj ve mevzuat kuralları | ⬜ Karar girdileri gerekli | Türkiye mevzuatına uyum ve yıl geçişlerinde operasyon sürekliliği sağlar |
| 5 | Üretim güvenliği ve felaket kurtarma | ⬜ Planlandı | Kesinti ve veri kaybı riskini satış öncesi kabul edilebilir seviyeye indirir |
| 6 | Pilot müşteri ve satış kapısı | ⬜ Kapalı | Ürünün kontrollü ücretli pilotta desteklenebilir olduğunu kanıtlar |

## Kapı 0 — Personel erişim güvenliği

Kapsam: pasifleştirmede bağlı Identity hesabının kapanması, refresh token
iptali, eski access token'ın anında reddi ve reaktivasyonda eski refresh token'ın
dirilmemesi. Çıkış kanıtı: Release build 0 uyarı/0 hata, 66 unit ve 182
integration testi. `E-1` kapandı; işten çıkışın kalan süreç kapsamı Kapı 3'tedir.

## Kapı 1 — API doğrulama ve hata sözleşmesi

Kapsam:

- Actions öncelik/durum alanlarında komut ve filtre doğrulaması.
- Compliance durum/risk alanlarında komut ve filtre doğrulaması.
- Recruitment aday durumu filtresinin aynı sözleşmeye alınması.
- Doğrulayıcıyı aşan `ArgumentException` için ayrıntı sızdırmayan merkezi 400
  güvenlik ağı.
- RFC 7807 `application/problem+json` ve alan bazlı `errors` sözleşmesi.

Çıkış ölçütü: geçersiz enum yollarının tamamı 400; gerçek sunucu hataları 500
olarak kalır; Release build warning-as-error ile yeşil; tüm backend testleri
yeşil. Güncel kanıt: 0 uyarı/0 hata, 66 unit + 190 integration.

## Kapı 2 — Bordro ve parasal doğruluk

İlk dilim `A-2 → P-5 → P-1`: eksik puantaj tamamlanmış sayılamaz; sıfır/negatif
net bordro uyarısız onaylanamaz. İkinci dilim `P-3 → P-2/P-8/P-9`: dönem sırası
ve kümülatif vergi matrahı tek doğruluk kaynağına bağlanır. Son dilim `P-4`,
`P-10`, `P-12`: ay içinde ayrılan personel, sorgu büyümesi ve eşzamanlı onay
koruması.

**İlk dilim tamamlandı (2026-08-28):** puantaj tamlığı artık istemci bayrağı
değil; sunucu işe giriş tarihi, hafta sonu, tatil, puantaj kaydı ve onaylı izin
birleşiminden hesaplıyor. Dönem sonundan sonra işe başlayan aktif çalışan döneme
alınmıyor. Güncelleme, toplu kontrol ve onay yolları puantajı yeniden hesaplıyor;
sıfır brüt veya sıfır/negatif net satır `Eksik Veri` kalıyor ve onayda 409 alıyor.
Kanıt: backend build 0 uyarı/0 hata, 70 unit + 190 integration; frontend
lint/build ve 146 test yeşil. `A-2`, `P-5`, `P-1` kapandı.

**İkinci dilim tamamlandı (2026-08-28):** yeni dönem yalnız en son dönem
onaylanmış/kapatılmış ve istenen ay kronolojik olarak daha ileri ise açılıyor.
`PreviousTaxBase` satır girdi sözleşmesinden kaldırıldı; oluşturma, detay,
güncelleme, kontrol ve onay aynı sunucu hesabını kullanıyor. Kaynak yalnız aynı
yıldaki onaylı/kapatılmış önceki dönemlerin onaylı satırlarıdır. Eski istemcinin
matrah göndermesi değeri değiştirmiyor. `P-2`, `P-3`, `P-8`, `P-9` kapandı.
Dönem oluşturma tenant bazlı SQL Server transaction-owned application lock
altında serileştiriliyor; çoklu API instance'ında iki farklı ay eşzamanlı
istendiğinde yalnız biri açılıyor, diğeri kilit sonrasında 409 alıyor.

**Üçüncü dilim tamamlandı (2026-08-28):** personelde zorunlu etkin ayrılış
tarihi tutuluyor; yeniden aktifleştirmede temizleniyor. Ay içinde ayrılan kişi
son bordrosuna giriyor, ayrılıştan sonraki günler puantaj eksiği sayılmıyor ve
sonraki dönemlere taşınmıyor. Eski, tarihi bilinmeyen pasif kayıtlar bilinçli
olarak döneme alınmıyor. Bordro detayında ayarlar/vergi dilimleri ile çalışan
satırları iki açık sorguya ayrıldı; çalışan × vergi dilimi çarpımı kalktı.
Düzenleme, kontrol, satır onayı ve dönem gönderimi aynı dönem-scoped dağıtık
kilidi kullanıyor; eşzamanlı çift onay/gönderimde yalnız biri başarılı oluyor.
`P-4`, `P-10`, `P-12` kapandı. İşten çıkışın yöneticilik devri, izinler ve
kıdem/ihbar gibi kalan yan etkileri Kapı 3 / `E-5` kapsamındadır.
Kanıt: Release backend build 0 uyarı/0 hata; 71 unit + 190 integration;
frontend lint/build + 146 test yeşil.


Çıkış ölçütü: parasal altın senaryolar; eksik puantaj ve negatif net için red
testleri; dönem sırası/kümülatif matrah regresyonları; eşzamanlı onay testi.

## Kapı 3 — Personel yaşam döngüsü ve işlem bütünlüğü

Kapsam: `E-5/E-2/E-3` ile yöneticilik devri, bekleyen talepler, puantaj ve son
bordro; `R-6` ile işe alım + login oluşturmanın atomik veya telafi edilebilir
hale gelmesi; başarısız adımların audit izi ve güvenli tekrar çalıştırılması.

**Kapı 3 / E-3 tamamlandı (2026-08-31):** puantaj oluşturma ve güncelleme,
personelin işe giriş/etkin ayrılış aralığı dışında kayıt kabul etmiyor. Doğrudan
personel güncelleme ucu durum değişimini atlayamıyor; yeni personel yalnız aktif
başlıyor ve tüm aktif/pasif geçişler yaşam döngüsü komutundan geçiyor.

**Kapı 3 / R-6 tamamlandı (2026-08-31):** personel, izin bakiyesi, aday geçmişi,
pozisyon kontenjanı ve kalıcı provizyon işi tek veritabanı commit’inde oluşuyor.
Identity/davet adımı idempotent; geçici hatalar lease + üstel geri çekilmeli
background worker ile tenant bazında yeniden deneniyor. Kalıcı hata ve son hata
aday detayında görünür; İK düzeltilmiş e-postayla güvenli retry yapabiliyor.
İki ardışık SMTP hatası sonrası manuel retry’nin beklemede kaldığı ve worker’ın
üçüncü denemede hesabı tamamladığı uçtan uca kanıtlandı. Kanıt: Release backend
0 uyarı/0 hata, 71 unit + 193 integration; frontend lint/build + 147 test.

**Kapı 3 / E-2 tamamlandı (2026-08-31):** ekibi bulunan yönetici halef
seçilmeden pasife alınamıyor ve toplu pasifleştirme bu kapıyı aşamıyor. Aktif,
giriş hesabı bağlı bir doğrudan rapor terfi ettirilebiliyor; ekip bağları ve
bekleyen izin onay kapsamı aynı veritabanı commit'inde yeni yöneticiye geçiyor.
Yönetici rolleri kalıcı ve idempotent provizyon işleriyle veriliyor/alınıyor;
ilk doğrudan çalışan ataması otomatik terfi, son çalışanın ayrılması otomatik
rol geri alma üretiyor. HR-admin rolü korunuyor ve eski rolü taşıyan access
token, güncel organizasyon rolüyle eşleşmiyorsa anında reddediliyor. React
arayüzü ayrılış tarihi, ekip/onay etkisi ve uygun halefleri gösteren tekil
işten çıkış akışını sunuyor. Kanıt: backend build 0 uyarı/0 hata, 71 unit +
195 integration; frontend lint/build + 148 test.

**Kapı 3 / E-5A+B tamamlandı (2026-08-31):** İşten çıkış kalıcı ve sürümlü
`Taslak → Tamamlandı → Geri Alındı` vakasına dönüştü. Tamamlama; etkin ayrılış,
çıkış nedeni, halef/ekip devri, dış uzman kaynaklı kullanılmayan izin, kıdem ve
ihbar tutarlarının anlık görüntüsü ile Identity erişim kapatma işini atomik
olarak kaydediyor. Genel durum ve toplu pasifleştirme yolları kapatıldığı için
süreç atlanamıyor. Geri alma yalnız beklenen vaka sürümü ve değişmemiş
organizasyon anlık görüntüsüyle çalışıyor; sapma/stale istek 409 alıyor, geçmiş
vaka silinmiyor. React akışı taslak, sürümlü tamamlama, finansal özet, provizyon
durumu ve gerekçeli geri alma sağlıyor. Uygulama hukuki tutar hesaplamıyor;
kaynağı ve referansı görünen uzman onaylı değerleri saklıyor. Yerel kanıt:
backend hedefli 17 integration testi ve frontend hedefli 6 test; tam paket 71
unit + 195 integration + 149 frontend testi. Release build 0 uyarı/0 hata,
lint ve production build yeşil.

Kapı 3’ün sıradaki karar paketi `E-5C`: otomatik kıdem/ihbar hesabı ancak etkin
ücret ve yan hakların güvenilir kaynağı, doğru izin yıl devri/bakiye modeli ve
uzman onaylı tarih etkili sürümlü kurallar birlikte tanımlandığında açılacak.
Bu girdiler tamamlanana kadar dış uzman finansal anlık görüntüsü tek güvenli
kapanış sözleşmesidir.

Çıkış ölçütü: işe giriş/çıkış uçtan uca süreç testleri, idempotanslık, yarım kalan
işlem için kurtarma yolu ve açık sahiplik kuralları.

## Kapı 4 — İzin, puantaj ve mevzuat

Kapsam: `L-1/L-2` yıl devri ve yıl aşan izin; `A-1/A-3/A-4` gece vardiyası,
haftalık fazla mesai ve erken mesai; `R-3/P-11` gibi mevzuat değerlerinin sabit
koddan sürümlü/yapılandırılabilir kurala taşınması.

Çıkış ölçütü: mali müşavir/uzman tarafından onaylanmış karar kaydı, tarih etkili
kural sürümü ve yıl sınırı/gece vardiyası regresyonları.

## Kapı 5 — Üretim güvenliği ve felaket kurtarma

Kapsam: CORS allowlist, HSTS ve forwarded headers; sağlık/readiness kontrolleri;
`Subscription.Price` SQL precision tanımı; kiracı migration dağıtım kontrolü;
off-site yedek hedefi; veritabanı + evrak dosyası geri yükleme tatbikatı; alarm,
log ve operasyon runbook'ları.

Çıkış ölçütü: üretim benzeri ortamda dağıtım provası, off-site kopya kanıtı,
dosya dahil restore drill, ölçülmüş RPO/RTO ve kritik alarmların tetiklenmesi.

## Kapı 6 — Pilot müşteri ve satış

Kapsam: kontrollü tenant onboarding, paket/abonelik yetkileri, demo verisi,
ürün içi hata ve destek akışı, KVKK sözleşme/dokümantasyonu, kullanım telemetrisi,
pilot kabul senaryoları ve satış demosu.

Çıkış ölçütü: en az bir temsilî müşteri verisiyle UAT; destek ve geri dönüş
sorumluları; fiyat/paket sınırları; kritik/yüksek açıklar için kabul veya kapatma
kararı. Kapı 2 ve Kapı 5 kapanmadan ücretli pilot önerilmez.

## Çalışma kuralı

Her kapı ayrı ve küçük yerel commitlerle ilerler. Geliştirme ve doğrulama
tamamlandığında kaynak/hedef ref, commitler, dosyalar, test sonuçları ve yapılacak
Git işlemleri tek paket olarak raporlanır. Kullanıcının tek açık onayı, yalnız bu
raporlanan feature push + merge + hedef push zincirini yetkilendirir; kapsam
kendiliğinden başka dal, commit, tag veya remote'a genişletilemez.
