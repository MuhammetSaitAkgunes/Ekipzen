# Employees (Personel) — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Son güncelleme:** 2026-08-28 · **Kapsam:** 13 uç
nokta, `Features/Employees` + `EmployeesController` · **Mevcut test:** 39

**Neden bu alan seçildi:** kapsaması en yüksek alan. Yöntem açısından da bilgi
verici: iyi test edilmiş bir alanda ÖD ne bulur?

**Cevap:** en ağır güvenlik bulgusu burada çıktı. 38 test, **davranışın kendisini**
değil, uçların tek tek doğru cevap verdiğini ölçüyor. "İşten çıkış" bir uç değil
bir **süreç** olduğu için hiçbir testin kapsamına girmemiş.

---

## Özet

| # | Katman | Önem | Bulgu | Nerede |
| --- | --- | --- | --- | --- |
| **E-1** | Mantık | **ÇÖZÜLDÜ** (eski KRİTİK) | Pasif personelin mevcut/yeni oturumu ve refresh token erişimi kesiliyor | `TenantAccessMiddleware`; `IdentityService`; durum komutları; integration testi |
| E-2 | Mantık | **YÜKSEK** | Pasif yönetici ekibin yöneticisi olarak kalıyor — onay kuyruğu sahipsiz | `SetEmployeeStatusCommandHandler`, `BulkDeactivateEmployeesCommandHandler` |
| **E-3** | Mantık | **ÇÖZÜLDÜ 2026-08-31** | Puantaj yalnız personelin etkin çalışma tarih aralığında kabul ediliyor | create/update + integration testleri |
| E-4 | Mantık | ORTA | **Geçersiz TC kabul ediliyor** (`11111111111` → 201); yalnız 11 hane kontrolü var | `EmployeeUpsertModel` → `NationalIdRegex = ^\d{11}$` |
| E-5 | Kod | ORTA | İşten çıkış **tek alanlık bir güncelleme**; süreç olarak ele alınmıyor | `SetEmployeeStatusCommand` |
| E-6 | Kod | DÜŞÜK | `BulkDeactivate` yalnız aktifleri sayıyor, bulunamayan id'leri raporlamıyor | `BulkDeactivateEmployeesCommandHandler` |

**Doğru çalışan, kanıtlanmış davranışlar:**

- **Mükerrer TC engelleniyor** (409, canlı doğrulandı)
- Yetki politikaları doğru dağıtılmış: yazma uçları `HrAdminOnly`, okuma uçları
  `Management` + satır kapsamı; `import/*` yalnız hr-admin
- **Silme ucu yok** — personel yalnız pasife alınıyor (doğru: özlük verisi silinmez)
- `BulkDeactivate` `ExecuteUpdate` yerine change tracker kullanıyor ki **audit
  interceptor ve trigger devrede kalsın** — bilinçli ve doğru bir tercih, yorumda da yazılı
- Evrak/foto yükleme, içe aktarma ve directory kapsamı 38 testle iyi kapsanmış

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

### E-2 · Pasif yönetici ekibin yöneticisi olarak kalıyor — YÜKSEK

Canlı: yönetici pasife alındı → ekibindeki personelin `managerId` değişmedi.

Sonuçları: izin onay kuyruğu **sahipsiz** kalır (o yöneticinin ekibi için karar
verecek kimse yoktur), `ScopeFor` kapsamı pasif bir kişiye bağlı kalır, ve
E-1 ile birleşince **pasif bir yönetici ekibinin taleplerini onaylamaya devam
edebilir**.

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
| 4 | İşlem sınırı | ⚠️ E-5 — işten çıkış çok adımlı bir süreç olmalıyken tek alan güncellemesi |
| 5 | N+1 / kartezyen | ✅ Liste sorgusu projeksiyonlu ve sayfalı |
| 6 | Hata zarfı | ✅ 403/404/409 doğru |
| 7 | Audit izi | ✅ Trigger + interceptor; `BulkDeactivate` bunun için bilinçli olarak change tracker kullanıyor |
| 8 | Test boşluğu | ❌ Aşağıda |

### E-5 · İşten çıkış bir süreç olarak ele alınmamış — ORTA (E-1/E-2'nin kökü)

İşten çıkış erişim kesme, zorunlu etkin ayrılış tarihi ve son ay bordrosu
açısından tamamlandı (`E-1`, Payroll `P-4`). Yeniden aktifleştirme ayrılış
tarihini temizliyor; eski refresh token'ı diriltmiyor.

Bekleyen izin taleplerinin kararı, yöneticilik bağlarının devri ve kıdem/ihbar
hesabı hâlâ tek bir orkestre süreç değil. Bu nedenle `E-5` ve `E-2` kök
bulguları açık kalıyor; `E-3` çalışma tarihi kapısıyla kapandı.

### E-6 · Toplu pasife almada sessiz atlama — DÜŞÜK

`BulkDeactivate` yalnız `Active` olanları sayıyor ve dönüş değeri işlenen
sayısıdır. Var olmayan ya da zaten pasif id'ler sessizce atlanıyor; çağıran
"20 seçtim, 17 işlendi" farkının sebebini göremiyor.

### Test boşluğu

39 test şunları iyi kapsıyor: dizin listeleme/filtreleme, rol matrisi, kapsam
daraltması, TC doğrulama (biçim), mükerrer TC, tam kart CRUD, evrak yükleme/
indirme, foto, içe aktarma (önizleme + hatalı satır + mükerrer), toplu pasife
alma.

**Yeni kapsanan davranış:** pasif personelin mevcut access token'ı, yeni login'i
ve mevcut refresh token'ı reddediliyor; ayrılış tarihi zorunlu kaydediliyor;
reaktivasyonda tarih temizleniyor; puantaj yalnız etkin çalışma aralığında kabul ediliyor.

**Hâlâ test edilmemiş davranışlar:** pasif yöneticinin ekip bağı (E-2), geçersiz
TC algoritması (E-4) ve işten çıkışın kalan yan etkileri (E-5).

**Yöntem açısından not:** bu alanın 38 testi var ve hiçbiri E-1'i yakalamıyor.
Sebep, testlerin **uç nokta** düzeyinde yazılmış olması: her uç tek başına doğru
cevap veriyor. "İşten çıkış" ise uçlar arası bir **süreç**; hiçbir tek ucun
sorumluluğunda olmadığı için kimsenin testine girmemiş. ÖD'nin Katman 1'i tam
olarak bunu yakalamak için var.

---

## Sonraki adım

1. **E-2** — yöneticilik devri ve bekleyen onayların yeni sahibine aktarılması
2. **E-5** — kalan işten çıkış süreci (Leaves ve kıdem/ihbar ile kesişir)
3. **E-4** — TC algoritma doğrulaması
4. **E-6**
