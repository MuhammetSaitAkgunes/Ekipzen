# 8. Kararlar — "Neden böyle yaptık?" (ADR)

Bu bölüm, projedeki önemli teknik kararların **gerekçelerini** toplar. Her karar
için "seçenekler → seçim → neden" formatı. Bir karara katılmasan bile, neden
alındığını bilmek yeniden tartışmayı hızlandırır.

## 8.1 Neden Clean Architecture + MediatR?

**Seçenekler:** (a) klasik katmanlı controller→service→repository, (b) Clean
Architecture + CQRS/MediatR.
**Seçim:** (b).
**Neden:** Controller'ları ince tutar; her iş kuralı tek bir handler'da izole
olur ve tek başına test edilir. Doğrulama/loglama gibi kesişen kaygılar pipeline
behavior'la merkezi eklenir. Bağımlılık kuralı (dış→iç) sayesinde Domain ve
Application, veritabanı/HTTP'den bağımsız kalır — test edilebilirlik yüksek.
**Bedeli:** Küçük bir işlem için bile Command+Validator+Handler yazmak "boilerplate"
hissi verir. Kurumsal ölçekte bu bedel, tutarlılık ve test edilebilirlikle fazlasıyla
geri döner.

## 8.2 Neden eski mock uygulama korunuyor?

**Seçim:** React portundan sonra eski mock (`legacy-frontend/`) silinmedi,
korunup taşındı.
**Neden:** Portun ana kalite kriteri **piksel-parite**: her yeni ekran eskisiyle
birebir aynı görünmeli. Eski uygulama, "yeni ekran doğru mu?" karşılaştırması için
canlı referans. Silseydik bu güvenceyi kaybederdik. İçeriği **değiştirilmez**;
sadece statik sunucuyla açılıp yan yana bakılır.

## 8.3 Neden bordro motoru saf ve Domain'de?

**Neden:** Bordro hesabı DB/HTTP'den bağımsız saf bir dönüşümdür. Domain'e
koyunca mock'suz, yüzlerce parite testi (eski JS çıktısıyla eşleşme) yazılabiliyor.
Bordro finansal/yasal risktir; en yüksek test kapsamını hak eder. Saf fonksiyon
= deterministik = güvenilir.

## 8.4 Neden ağır agregasyonlar SQL view/SP'de?

**Seçenekler:** (a) her şeyi EF Core LINQ ile, (b) ağır okuma modellerini SQL
view/fonksiyonuna taşımak.
**Seçim:** (b) — kritik agregasyonlar için.
**Neden:** "Kalan izin", "aylık puantaj toplamı", "uyum hazırlık skoru" gibi
sorgular, tüm kayıtları belleğe çekip C#'ta toplamak yerine veritabanının tek
sorguda yapması gereken işlerdir. View, bu mantığı **tek doğruluk kaynağı** yapar
(hem UI kartı hem iş kuralı aynı formülü kullanır) ve performanslıdır.
**Dikkat:** View "tek doğruluk" olduğunda, formülündeki bir varsayım (ör. yalnız
onaylı izinleri saymak) uygulama katmanında farklı bir ihtiyaçla çakışabilir —
bkz. 8.7 #2. Ders: view'ın **ne saydığını** iyi bil.

## 8.5 Neden tipler backend'den üretiliyor (`gen:api`)?

**Neden:** Frontend-backend sözleşmesini elle senkron tutmak (iki yerde DTO
tanımlamak) klasik hata kaynağıdır. Swagger→TypeScript üretimi, backend değişince
frontend'de **derleme zamanı** uyumsuzluk yakalar. Tek yönlü, otomatik, güvenilir.

## 8.6 Neden demo şifresi `demo123` ve seed veri?

**Neden:** Bu bir **demo/öğrenme** ürünü. Sabit demo hesaplar + seed veri, testlerin
ve duman kontrollerinin deterministik olmasını sağlar ("Ahmet'in bakiyesi 24 gün"
gibi varsayımlar seed'den gelir). İşe alım login'i de geçici `demo123` kullanır.
**Üretim uyarısı:** Bu kalıp üretimde **kabul edilemez**; gerçek üründe davet/
şifre-sıfırlama akışı gerekir. Kod yorumlarında bu açıkça belirtilmiştir.

## 8.7 Kod inceleme düzeltmeleri (1–6)

Port bittikten sonra "kullanıma hazır ürün" gözüyle bir inceleme yapıldı ve 6
bulgu giderildi. Her biri iyi bir öğrenme vakası:

### #1 — Anonim kayıt yetki yükseltmesi (KRİTİK güvenlik)
**Sorun:** `/api/auth/register` anonimdi ama istemciden `role` alıyordu → herkes
`{"role":"hr-admin"}` ile yönetici olabilirdi.
**Düzeltme:** Anonim kayıt istemci rolünü yok sayar, her zaman `employee`.
**Ders:** Girdiye asla güvenme; özellikle rol/yetki alanlarına.

### #2 — İzin bakiyesi çoklu talepte aşılabiliyor (iş kuralı)
**Sorun:** Bakiye view'ı yalnız onaylı izinleri sayar; çakışmayan çok sayıda
bekleyen talep tek tek geçip toplu onaylanınca bakiye negatife düşer; onay anında
yeniden kontrol yoktu.
**Düzeltme:** Oluştururken bekleyen düşümlü günleri de düş; onayda yeniden doğrula
(`LeaveGuards.EnsureBalanceAsync`).
**Ders:** "Tek doğruluk" bir view, ihtiyacının tamamını karşılamayabilir; durum
geçişlerinde (onay) değişmezleri yeniden doğrula.

### #3 — İşe alınan aday yarım kuruluyordu (iş kuralı / eksik akış)
**Sorun:** Hire yalnız `Employee` açıyordu; login yok, bakiye 0 → kişi sisteme
giremiyor, izin akışına katılamıyordu.
**Düzeltme:** Tam provizyon — login (`ApplicationUser`) + 24 günlük `LeaveBalance`;
hire e-posta alır.
**Ders:** Bir "dönüşüm" işlemi, hedef varlığın **çalışır** olması için gereken her
şeyi kurmalı; yarım bırakma.

### #4 — Uyum hazırlık skoru kapsamsızdı (yetki/tutarsızlık)
**Sorun:** Belge tablosu role göre daraltılırken hazırlık skoru şirket geneli
view'dan dönüyordu → manager, ekip tablosuyla şirket geneli KPI'yı yan yana
görüyordu.
**Düzeltme:** Skoru da aynı kapsamdan hesapla (`ComplianceReadinessAggregator`).
**Ders:** Aynı ekrandaki iki veri parçasının kapsamı **tutarlı** olmalı.

### #5 — apiFetch 401-retry sağlamlaştırma (dayanıklılık)
**Sorun:** Refresh sonrası retry de 401 dönerse ham hata fırlıyor, oturum
temizlenmiyordu; FormData retry davranışı belirsizdi.
**Düzeltme:** Retry de 401'de oturumu temizle + login'e yönlendir; FormData
retry gövde bütünlüğü regresyon testiyle kilitlendi.
**Ders:** Hata yollarının **tüm dallarını** düşün; "mutlu yol" yetmez.

### #6 — Aksiyon merkezi çift sorgu (verimlilik/sadelik)
**Sorun:** Filtre seçenekleri için ikinci bir (filtresiz) sorgu açılıyordu.
**Düzeltme:** Küçük veri kümesi → tek sorgu + client-side filtreleme.
**Ders:** Veri küçükse client-side filtre, hem ağ isteğini hem karmaşıklığı azaltır.

> Bu düzeltmelerin uygulama planı `docs/gelistirme-gunlugu.md` ve fix commit'lerinde;
> çalışma dalı `fix/review-findings-1-6` main'e merge edildi.

## 8.8 Neden TDD ve bu kadar test?

**Neden:** Port dilimleri ve düzeltmeler TDD ile yürütüldü: önce başarısız test,
sonra kod. 131 frontend + 80 backend test, "bir değişiklik başka yeri kırdı mı?"
sorusunu saniyelerde yanıtlar. Bu, büyük bir kod tabanında **güvenle değiştirme**
özgürlüğüdür. Testler ayrıca yaşayan dokümantasyondur — bir davranışın nasıl
çalıştığını en net testinden okursun.

## 8.9 Bilinçli kapsam-dışılar (YAGNI)

Şunlar **bilinçli olarak** eklenmedi (gerektiğinde ayrı iş): Tailwind, SSR/Next.js,
i18n altyapısı, Storybook, Playwright E2E paketi. Ayrıca yönetici konsolundaki
bazı analitikler (izin trend grafiği, yoğunluk haritası) backend'de karşılığı
olmadığı için **demo** olarak işaretlidir. "İhtiyaç olmadan yapma" (YAGNI) ilkesi:
her satır bakım maliyetidir.

---

Önceki: [← 7. Geliştirme Akışı](07-gelistirme-akisi.md) · Sonraki: [9. Sözlük & Onboarding →](09-sozluk-onboarding.md)
