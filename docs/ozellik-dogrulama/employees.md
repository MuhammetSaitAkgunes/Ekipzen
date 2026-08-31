# Employees (Personel) — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Son güncelleme:** 2026-08-31 · **Kapsam:** 15 uç
nokta, `Features/Employees` + `EmployeesController` · **Mevcut test:** 42

**Neden bu alan seçildi:** kapsaması en yüksek alan. Yöntem açısından da bilgi
verici: iyi test edilmiş bir alanda ÖD ne bulur?

**Cevap:** en ağır güvenlik bulgusu burada çıktı. 42 test, **davranışın kendisini**
değil, uçların tek tek doğru cevap verdiğini ölçüyor. "İşten çıkış" bir uç değil
bir **süreç** olduğu için hiçbir testin kapsamına girmemiş.

---

## Özet

| # | Katman | Önem | Bulgu | Nerede |
| --- | --- | --- | --- | --- |
| **E-1** | Mantık | **ÇÖZÜLDÜ** (eski KRİTİK) | Pasif personelin mevcut/yeni oturumu ve refresh token erişimi kesiliyor | `TenantAccessMiddleware`; `IdentityService`; durum komutları; integration testi |
| **E-2** | Mantık | **ÇÖZÜLDÜ 2026-08-31** | Halef zorunlu; ekip/onay kapsamı ve yönetici rolü güvenle devrediliyor | status/offboarding/upsert + Identity provizyonu + integration/UI testleri |
| **E-3** | Mantık | **ÇÖZÜLDÜ 2026-08-31** | Puantaj yalnız personelin etkin çalışma tarih aralığında kabul ediliyor | create/update + integration testleri |
| E-4 | Mantık | ORTA | **Geçersiz TC kabul ediliyor** (`11111111111` → 201); yalnız 11 hane kontrolü var | `EmployeeUpsertModel` → `NationalIdRegex = ^\d{11}$` |
| **E-5** | Kod | **KISMEN ÇÖZÜLDÜ 2026-08-31** | Kalıcı, geri alınabilir süreç ve uzman kaynaklı finansal kapanış tamam; otomatik yasal hesap `E-5C` kararlarını bekliyor | `EmployeeOffboardingWorkflow`; `EmployeeOffboardingCase` |
| **E-6** | Kod | **ÇÖZÜLDÜ 2026-08-31** | Toplu pasifleştirme süreç kapısını aşmasın diye devre dışı; arayüzde yalnız raporlama seçimi kaldı | `EmployeesController`; `PersonnelPage` |

**Doğru çalışan, kanıtlanmış davranışlar:**

- **Mükerrer TC engelleniyor** (409, canlı doğrulandı)
- Yetki politikaları doğru dağıtılmış: yazma uçları `HrAdminOnly`, okuma uçları
  `Management` + satır kapsamı; `import/*` yalnız hr-admin
- **Silme ucu yok** — personel yalnız pasife alınıyor (doğru: özlük verisi silinmez)
- Toplu seçim yalnız CSV raporlaması için korunuyor; toplu pasifleştirme işten
  çıkış vakasının finansal ve organizasyonel kontrollerini aşamıyor
- Evrak/foto yükleme, içe aktarma ve directory kapsamı otomatik testlerle iyi kapsanmış

---

## Katman 1 — Mantık (senaryo yürüyüşü)

### E-1 · İşten çıkarılan personel sisteme giriş yapabiliyor — KRİTİK

Canlı yürütüldü:

```
1) Personel pasife alındı            → 204
2) Aynı personelle giriş denendi     → 200   ← GİRİŞ BAŞARILI
3) /api/me                           → 200
4) /api/payroll/my                   → 200   ← bordro kayıtlarını okuyor
5) /api/leaves/my                    → 200
```

`SetEmployeeStatusCommandHandler` yalnız `Employee.Status` alanını değiştiriyor.
Bağlı Identity kullanıcısına **hiç dokunmuyor**: hesap kilitlenmiyor, refresh
token'ları iptal edilmiyor, rolü alınmıyor. `IdentityService` içinde
`EmployeeStatus` geçen **tek bir satır yok** — yani login personelin aktif olup
olmadığını hiç sormuyor.

**Neden kritik:** İK ürününde "işten çıkış", erişimin kesilmesi gereken andır.
Bugün işten çıkarılan bir personel:

- elindeki token'la (60 dk) çalışmaya devam eder,
- **yeni token alabilir** — parolası hâlâ geçerli,
- kendi bordro geçmişini, izin kayıtlarını ve profilini okumaya devam eder.

İşveren açısından bu, çıkışı yapılmış bir çalışanın şirket sistemine erişimini
sürdürmesi demektir; KVKK açısından da erişim yetkisinin gereksiz sürdürülmesidir.

> Not: kiracı seviyesindeki erişim kapısı (`ITenantAccessGuard`) çalışıyor —
> dondurulmuş kiracının kullanıcıları giremiyor. Eksik olan **personel
> seviyesinde** karşılığı.

**Çözüm — 2026-08-28:** Tekli ve toplu pasifleştirme artık bağlı Identity
hesabını kapatıyor ve açık refresh token'larını iptal ediyor. Token üretimi aktif
personel koşuluna bağlandı; korumalı isteklerde personel durumu yeniden denetlenerek
pasifleştirmeden önce verilmiş access token da anında reddediliyor. Reaktivasyon
eski refresh token'ı yeniden geçerli kılmıyor. Bu akış
`PassiveEmployee_LosesCurrentSession_LoginAndRefreshAccess` integration testiyle
ve tam backend paketiyle doğrulandı (66 unit + 182 integration).

### E-2 · Yöneticilik ve onay devri — ÇÖZÜLDÜ (2026-08-31)

Eski canlı bulgu: yönetici pasife alındı → ekibindeki personelin `managerId` değişmedi.

Sonuçları: izin onay kuyruğu **sahipsiz** kalır (o yöneticinin ekibi için karar
verecek kimse yoktur), `ScopeFor` kapsamı pasif bir kişiye bağlı kalır, ve
E-1 ile birleşince **pasif bir yönetici ekibinin taleplerini onaylamaya devam
edebilir**.

**Çözüm:** ekibi bulunan yönetici halef seçilmeden veya bulk yoluyla pasife
alınamıyor. Aktif ve giriş hesabı bağlı halef seçildiğinde ekip bağlarıyla
bekleyen izin kapsamı aynı commit'te devrediliyor. Doğrudan rapor terfi ederse
kendi kendisinin yöneticisi olmayacak şekilde ayrılan yöneticinin üstüne
bağlanıyor; daha derin alt ekip döngü riski nedeniyle reddediliyor. İlk ekip
ataması ve son ekip ayrılışı Identity rolünü kalıcı/idempotent işlerle
eşitliyor; HR-admin korunuyor. Eski rolü taşıyan access token anında 401 alıp
güncel rol için refresh'e zorlanıyor. Backend yaşam döngüsü ve React halef
modalı otomatik testlerle kanıtlandı.

### E-3 · Çalışma dönemi dışında puantaj — ÇÖZÜLDÜ (2026-08-31)

Puantaj oluşturma ve güncelleme personelin etkin çalışma aralığını doğruluyor:
işe girişten önce, etkin ayrılıştan sonra veya tarihi bilinmeyen pasif personelde
kayıt 409 ile reddediliyor. Böylece bordro kapsamı ile puantaj verisi aynı
istihdam dönemi kuralını kullanıyor.

### E-4 · TC kimlik algoritması doğrulanmıyor — ORTA

```csharp
private static readonly Regex NationalIdRegex = new(@"^\d{11}$", RegexOptions.Compiled);
```

Yalnız "11 rakam" kontrolü var. Canlı denendi: `11111111111` → **201 kabul**.
Bu değer gerçek TC kimlik algoritmasına göre geçersizdir (tüm haneler aynı
olamaz; ayrıca 10. ve 11. hane sağlama kurallarına uymalı).

Etkisi: SGK bildirimleri ve resmî belgeler geçersiz kimlik numarasıyla üretilir;
hata kurumdan geri döner. Mükerrer TC kontrolü var ama **geçersiz TC** kontrolü yok.

---

## Katman 2 — Pratik kod kalitesi

| # | Kontrol | Sonuç |
| --- | --- | --- |
| 1 | Kiracı izolasyonu | ✅ Global filtre; evrak erişimi kiracı satırından geçiyor |
| 2 | Yetki kapsamı | ✅ Politikalar doğru dağıtılmış; `ScopeFor` okuma uçlarında |
| 3 | Doğrulama | ❌ E-4 |
| 4 | İşlem sınırı | ✅ Vaka, iş verisi ve kalıcı Identity niyeti tek işlem sınırında |
| 5 | N+1 / kartezyen | ✅ Liste sorgusu projeksiyonlu ve sayfalı |
| 6 | Hata zarfı | ✅ 403/404/409 doğru |
| 7 | Audit izi | ✅ Trigger + interceptor + silinmeyen, sürümlü işten çıkış vakası |
| 8 | Test boşluğu | ❌ Aşağıda |

### E-5 · İşten çıkış kapanışı — KISMEN ÇÖZÜLDÜ (2026-08-31)

`E-5A+B` ile işten çıkış kalıcı ve sürümlü `Taslak → Tamamlandı → Geri Alındı`
vakasına dönüştü. Taslak; ayrılış/çıkış bilgisi, halef, kullanılmayan izin,
kıdem/ihbar, dış uzman referansı ve notları saklıyor. Tamamlama; vaka sürümü,
personel durumu, etkin ayrılış, ekip devri ve Identity erişim kapatma niyetini
tek işlemde kaydediyor. Geri alma; organizasyon anlık görüntüsü değişmişse 409
veriyor; değişmemişse ekip, durum ve erişim niyetini telafi ediyor. Geçmiş vaka
silinmediği için karar ve tersine çevirme gerekçesi izlenebilir.

Uygulama hukuki tutar hesaplamıyor. Kullanılmayan izin, kıdem ve ihbar değerleri
kaynağı/referansı görünen dış uzman onaylı finansal anlık görüntü olarak
saklanıyor. `E-5C`; etkin ücret/yan hak kaynağı, izin yıl devri doğruluğu ve
uzman onaylı tarih etkili sürümlü kurallar tanımlandıktan sonra otomatik hesap
seçeneğini değerlendirecek.

### E-6 · Toplu pasife alma kapısı — ÇÖZÜLDÜ (2026-08-31)

Toplu pasifleştirme ucu geçerli yükte 409 döndürüyor ve arayüz bu eylemi
sunmuyor. Böylece bulunamayan kimliklerin sessiz atlanması kadar önemli olan
halef, finansal kapanış ve geri alma kontrollerinin topluca aşılması da
engellendi. Toplu seçim yalnız CSV raporlamasında kullanılıyor.

### Test boşluğu

41 test şunları iyi kapsıyor: dizin listeleme/filtreleme, rol matrisi, kapsam
daraltması, TC doğrulama (biçim), mükerrer TC, tam kart CRUD, evrak yükleme/
indirme, foto, içe aktarma (önizleme + hatalı satır + mükerrer), toplu pasife
alma.

**Yeni kapsanan davranış:** pasif personelin mevcut access token'ı, yeni login'i
ve mevcut refresh token'ı reddediliyor; ayrılış tarihi zorunlu kaydediliyor;
reaktivasyonda tarih temizleniyor; puantaj yalnız etkin çalışma aralığında kabul
ediliyor; halef, ekip/onay devri, rol terfi/geri alma ve eski rol tokenı doğrulanıyor.

**Yeni kapsanan davranış:** taslak kaydı, sürümlü tamamlama/geri alma, finansal
anlık görüntü toplamı, Identity erişim kapatma/açma, stale sürüm reddi,
organizasyon sapmasında güvenli 409 ve onarım sonrası ekip/rol geri yükleme.

**Hâlâ test edilmemiş davranış:** geçersiz TC algoritması (E-4). `E-5C`
otomatik yasal hesap değildir; ürün ve uzman karar girdisi bekleyen yeni dilimdir.

**Yöntem açısından not:** ilk incelemede bu alanın 38 testi vardı ve hiçbiri E-1'i yakalamıyordu.
Sebep, testlerin **uç nokta** düzeyinde yazılmış olması: her uç tek başına doğru
cevap veriyor. "İşten çıkış" ise uçlar arası bir **süreç**; hiçbir tek ucun
sorumluluğunda olmadığı için kimsenin testine girmemiş. ÖD'nin Katman 1'i tam
olarak bunu yakalamak için var.

---

## Sonraki adım

1. **E-5C karar paketi** — etkin ücret/yan hak, izin devri ve sürümlü uzman kuralları
2. **E-4** — TC algoritma doğrulaması
