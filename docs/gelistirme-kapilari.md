# İK Pro — Geliştirme ve Satışa Hazırlık Kapıları

**Son güncelleme:** 2026-08-28

**Başlangıç çizgisi:** `main@d58181c`

**Kaynak:** özellik doğrulama sicili, Faz 2b mimari incelemesi ve tam test paketi

Bu belge, sıradaki geliştirmeyi yalnız özellik listesiyle değil, müşteriye zarar
verebilecek risklerin kapatılmasıyla yönetir. Bir kapı; kod, otomatik test, bulgu
kaydı ve çıkış kanıtı birlikte tamamlanmadan kapanmış sayılmaz.

## Güncel sıra

| Kapı | İçerik | Durum | Ticari etkisi |
| --- | --- | --- | --- |
| 0 | Personel erişim güvenliği (`E-1`) | ✅ Tamamlandı, `main` ve GitHub senkron | İşten ayrılan kullanıcının şirkete erişimini keser |
| 1 | API doğrulama ve hata sözleşmesi (`X-1`, `C-2`, `N-1`) | ✅ Tamamlandı; senkronizasyon onaylandı | Entegrasyon hatalarını anlaşılır 400 yanıtlarına çevirir; gerçek 500 alarm gürültüsünü azaltır |
| 2 | Bordro ve parasal doğruluk | ⬜ Sırada | Yanlış/negatif ödeme ve hatalı vergi matrahı riskini kapatır |
| 3 | Personel yaşam döngüsü ve işlem bütünlüğü | ⬜ Planlandı | İşe alım/işten çıkış süreçlerini yarım kalmış veri ve sahipsiz onaylardan korur |
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

Çıkış ölçütü: parasal altın senaryolar; eksik puantaj ve negatif net için red
testleri; dönem sırası/kümülatif matrah regresyonları; eşzamanlı onay testi.

## Kapı 3 — Personel yaşam döngüsü ve işlem bütünlüğü

Kapsam: `E-5/E-2/E-3` ile yöneticilik devri, bekleyen talepler, puantaj ve son
bordro; `R-6` ile işe alım + login oluşturmanın atomik veya telafi edilebilir
hale gelmesi; başarısız adımların audit izi ve güvenli tekrar çalıştırılması.

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
