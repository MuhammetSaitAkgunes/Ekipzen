# Leaves (İzin) — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Kapsam:** 9 uç nokta, `Features/Leaves` +
`LeavesController` + `vw_LeaveBalanceSummary` + `fn_WorkingDays` ·
**Mevcut test:** 9

**Neden bu alan üçüncü:** İş Kanunu'na bağlı bakiye/devir mantığı taşıyor,
bordroyu besliyor, ve Recruitment'ta bulunan R-2/R-3 (bakiye yılı, 24 gün
sabiti) doğrudan buraya bağlanıyor.

> Mevzuat hükmü müşavirindir. Burada sınanan: bakiye muhasebesi, yıl sınırı,
> devir, onay/iptal yolları ve düzeltme imkânı.

---

## Özet

| # | Katman | Önem | Bulgu | Nerede |
| --- | --- | --- | --- | --- |
| L-1 | Mantık | **YÜKSEK** | Yıl sonu devri/yeni yıl bakiyesi **hiç oluşmuyor** — her 1 Ocak'ta sistem izin kabul etmez hale geliyor | Devir yazan kod yok; `LeaveBalance` yalnız seed ve `hire`'da üretiliyor |
| L-2 | Mantık | **YÜKSEK** | Yıl sınırını aşan izin **tamamen başlangıç yılından** düşülüyor | `CreateLeaveRequestCommand.Handle` → `request.StartDate.Year` |
| L-3 | Mantık | ORTA | Onaylanmış izin **iptal edilemiyor** — düzeltme yolu yok, bakiye kilitli | `CancelLeaveRequestCommandHandler` |
| L-4 | Mantık | ORTA | İzin talebinde **vekil olarak kendisi** seçilebiliyor | `CreateLeaveRequestCommand.Handle`, vekil kontrolü yalnız varlık bakıyor |
| L-5 | Mantık | ORTA | Geçmiş tarihli talep **doğrulanmıyor**; bugün yalnız bakiye sıfır olduğu için tesadüfen engelleniyor | `CreateLeaveRequestCommandValidator` — tarih alt sınırı yok |
| L-6 | Mantık | ORTA | `CarriedOverDays` hiçbir zaman hesaplanmıyor, her yerde `0` yazılıyor | `AppDbContextInitializer`, `RecruitmentCommands.cs:357` |
| L-7 | Kod | ORTA | Onayda bakiye yarışı (TOCTOU) — eşzamanlılık koruması yok | `DecideLeaveRequestCommandHandler` |
| L-8 | Kod | DÜŞÜK | `RequireEmployeeId` yüzünden hr-admin demo hesabı kendi izin uçlarını kullanamıyor | `LeaveGuards.RequireEmployeeId` |

**Doğru çalışan, kanıtlanmış davranışlar:**

- **Bakiye muhasebesi tasarımı iyi.** `EnsureBalanceAsync` hem onaylı (view) hem
  **bekleyen** talepleri sayıyor; birden çok bekleyen talep toplamda bakiyeyi
  aşamıyor. Onay anında yeniden doğruluyor ve **değerlendirilen talebi
  bekleyenlerden çıkarıyor** — bu ince ve doğru bir ayrıntı.
- Çakışan tarih aralığı engelleniyor (409)
- Hafta sonu/resmi tatile denk gelen aralık reddediliyor (`fn_WorkingDays`)
- Onay gerektirmeyen tipler doğrudan onaylı açılıyor
- Yönetici yalnız kendi ekibini karara bağlayabiliyor (`ScopeFor`)
- **Kimse kendi talebini onaylayamıyor** (hr-admin hariç) — açıkça kodlanmış
- Yalnız bekleyen talep iptal edilebiliyor, ve yalnız sahibi tarafından

---

## Katman 1 — Mantık (senaryo yürüyüşü)

### L-1 · Yıl sonu devri yok — her 1 Ocak'ta izin sistemi duruyor — YÜKSEK

`CarriedOverDays` alanını **yazan tek kod yok**; her yerde sabit `0`. Yeni yıl
için `LeaveBalance` satırı üreten bir iş de yok — satırlar yalnız seed'de ve
`hire` anında oluşuyor.

`EnsureBalanceAsync` bakiye satırı bulamazsa `remaining = 0` kabul ediyor.
Sonuç, canlı olarak doğrulandı:

```
2027 talebi: 409 → "Yetersiz izin bakiyesi: talep 5 gün, kullanılabilir 0 gün"
```

**Etkisi:** 1 Ocak sabahı **hiçbir personel yıllık izin talep edemez**. Birileri
elle her personel için yeni yıl bakiye satırı açana kadar modül kilitli kalır.
Hata mesajı da yanıltıcı: "bakiyeniz yok" der, "yıl açılmamış" demez.

**İkinci etki (mevzuat, müşavir doğrulaması):** kullanılmayan yıllık izin
İş Kanunu'na göre yanmaz; devreder ve çıkışta ücrete dönüşür. Devir hiç
hesaplanmadığı için bu hak sistemde **kayboluyor**.

### L-2 · Yıl sınırını aşan izin tamamen başlangıç yılından düşülüyor — YÜKSEK

Canlı denendi: `2026-12-28 → 2027-01-05`, **7 iş günü**, talep kabul edildi ve
tamamı **2026** bakiyesinden düşüldü.

Sebep: hem oluşturmada hem onayda bakiye yılı `StartDate.Year`'dan alınıyor:

```csharp
await LeaveGuards.EnsureBalanceAsync(
    context, employeeId, request.StartDate.Year, days, ...);
```

Yılbaşı tatilini kapsayan izin istisna değil, **en sık kullanılan** izin
aralıklarından biridir. Bugün 2027'ye ait günler 2026 bakiyesini tüketiyor;
2027 bakiyesi (zaten yok — L-1) hiç etkilenmiyor.

### L-3 · Onaylanmış izin iptal edilemiyor — ORTA

Canlı denendi: onaylı talebin iptali **409** — "Yalnız bekleyen talepler iptal
edilebilir."

Personelin planı değişirse ya da yönetici yanlışlıkla onaylarsa **düzeltme yolu
yok**: izin kayıtta kalır, bakiye tüketilmiş sayılır. Tek çare veritabanına elle
müdahale. Bu, Recruitment R-5 ve Payroll'daki aynı örüntünün üçüncü örneği:
**ileri yön var, geri yön yok.**

### L-4 · Vekil olarak kendisi seçilebiliyor — ORTA

Canlı denendi: `substituteEmployeeId = kendi personel kaydı` → **201 kabul**.
Vekil kontrolü yalnız "böyle bir personel var mı" diye bakıyor. İzindeki kişinin
vekili kendisi olamaz; ayrıca vekilin aktif olup olmadığı da kontrol edilmiyor.

### L-5 · Geçmiş tarih doğrulanmıyor — ORTA (tesadüfen engelleniyor)

`2020-05-01` için talep denendi ve reddedildi — **ama tarih yüzünden değil**,
o yıla ait bakiye satırı olmadığı için (`kalan 0`). Doğrulayıcıda tarih alt
sınırı yok.

Bu, projede daha önce görülen "**tesadüfen doğru**" örüntüsünün bir örneği: o
yıl için bakiye satırı bulunan bir personelde geçmişe dönük talep **geçer**.

### L-6 · Devir alanı hiç hesaplanmıyor — ORTA

`CarriedOverDays` DTO'da, view'da ve veri dışa aktarımında var; **yazan yok**.
Ekranda hep `0` görünüyor — yani alan var, anlamı yok. L-1'in parçası ama ayrı
kayda geçiyor çünkü düzeltme ayrı: biri yıl açma işi, diğeri devir hesabı.

---

## Katman 2 — Pratik kod kalitesi

| # | Kontrol | Sonuç |
| --- | --- | --- |
| 1 | Kiracı izolasyonu | ✅ Tüm erişim `IApplicationDbContext` üzerinden; view'ler `TenantId` taşıyor |
| 2 | Yetki kapsamı | ✅ `ScopeFor(currentUser)` merkezî; kendi talebini onaylama engeli açık kodlanmış |
| 3 | Doğrulama | ❌ L-4, L-5 (tarih alt sınırı, vekil kimliği) |
| 4 | İşlem sınırı | ⚠️ L-7 — tek `SaveChanges` var ama eşzamanlılık koruması yok |
| 5 | N+1 / kartezyen | ✅ Sorgular projeksiyonlu (`Select`), koleksiyon `Include` yığını yok |
| 6 | Hata zarfı | ✅ 403/404/409 doğru; bakiye mesajı hesabı açıklıyor (kalan − bekleyen) |
| 7 | Audit izi | ⚠️ `LeaveRequests` audit trigger'ı var; karar alanları (`DecisionByUserId`) satırda tutuluyor |
| 8 | Test boşluğu | ❌ Aşağıda |

### L-7 · Onayda bakiye yarışı — ORTA

`DecideLeaveRequestCommandHandler` onay öncesi bakiyeyi yeniden doğruluyor — bu
doğru bir refleks. Ama iki yönetici **aynı anda** iki farklı talebi onaylarsa
ikisi de kontrolü geçer ve bakiye aşılabilir. Satır/bakiye üzerinde iyimser kilit
yok.

Kod yorumu eşzamanlılığı düşünmüş ("özellikle eşzamanlı onaylarda") ama
mekanizma tek başına yarışı kapatmıyor.

### L-8 · hr-admin demo hesabı izin uçlarını kullanamıyor — DÜŞÜK

`RequireEmployeeId`, personel bağı olmayan kullanıcıyı 403 ile reddediyor.
Bilinçli ve belgeli bir karar; ancak İK yöneticisinin **kendi** izin talebini
girebilmesi gerçek bir ihtiyaç. Ürün kararı olarak netleştirilmeli.

### Test boşluğu

Mevcut 9 test iyi seçilmiş: çakışma, hafta sonu, bakiye aşımı, toplu bekleyen
talep kontrolü, otomatik onaylı tipler, iptal sahipliği.

**Hiç test edilmemiş davranışlar:** yıl geçişi/devir (L-1), yıl sınırını aşan
izin (L-2), onaylı iznin iptali (L-3), vekil kimliği (L-4), geçmiş tarih (L-5),
eşzamanlı onay (L-7), `team` ve `pending` uçlarının kapsam daraltması.

---

## Sonraki adım

Bulgular düzeltilmedi. Önem sırası:

1. **L-1 + L-6** — yıl açma + devir hesabı; birlikte tasarlanmalı, mevzuat girdisi ister
2. **L-2** — yıl sınırını aşan iznin iki yıla bölünmesi
3. **L-3** — onaylı iznin iptali (Recruitment R-5 ve Payroll ile aynı örüntü: geri yön yok)
4. **L-7** — eşzamanlılık
5. **L-4, L-5** — doğrulama boşlukları
6. **L-8** — ürün kararı
