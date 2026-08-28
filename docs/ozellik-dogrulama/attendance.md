# Attendance (Puantaj) — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Kapsam:** 5 uç nokta, `Features/Attendance` +
`AttendanceController` + `vw_MonthlyAttendanceSummary` · **Mevcut test:** 6

**Neden bu alan dördüncü:** bordroyu besliyor. Fazla mesai saatleri bu modülden
`vw_MonthlyAttendanceSummary` üzerinden bordro dönemine giriyor — yani buradaki
her hata **paraya dönüşüyor**.

---

## Özet

| # | Katman | Önem | Bulgu | Nerede |
| --- | --- | --- | --- | --- |
| A-1 | Mantık | **YÜKSEK** | **Gece vardiyası kaydedilemiyor** (22:00→06:00 reddediliyor) | `AttendanceEntryModelValidator` + `AttendanceCalculator.Compute` |
| A-2 | Mantık | **ÇÖZÜLDÜ 2026-08-28** | Beklenen iş günlerinin tamamı sunucuda puantaj/onaylı izinle karşılanıyor | `PayrollTimesheetCoverage` + tüm bordro onay yolları |
| A-3 | Mantık | **YÜKSEK** | Fazla mesai eşiği **günlük 8 saat sabit**; haftalık 45 saat kuralı uygulanmıyor | `AttendanceCalculator.StandardWorkMinutes = 480` |
| A-4 | Mantık | ORTA | Ara dinlenme serbest — `break=0` fazla mesaiyi şişiriyor | `AttendanceEntryModelValidator` (0–480 arası her değer) |
| A-5 | Mantık | ORTA | **Gelecek tarihli** puantaj girilebiliyor (2030 kabul edildi) | `AttendanceEntryModelValidator` — `WorkDate` üst sınırı yok |
| A-6 | Kod | ORTA | Mesai başlangıcı ve erken eşiği sabit kodlu (09:00 / 08:45) | `AttendanceCalculator.WorkStart`, `EarlyBefore` |
| A-7 | Kod | ORTA | **Onaylanmış bordro dönemine ait puantaj** sonradan değiştirilebiliyor | `UpdateAttendanceEntryCommandHandler` — dönem kilidi yok |
| A-8 | Kod | DÜŞÜK | Puantaj tipi hem istemciden alınıp hem sunucuda türetiliyor — çelişki mümkün | `CreateAttendanceEntryCommandHandler.Apply` |

**Doğru çalışan, kanıtlanmış davranışlar:**

- **Hesaplama sunucuda** yapılıyor; istemcinin gönderdiği süre/durum güvenilmiyor
- Gün başına tek kayıt kuralı hem oluşturmada hem **tarih değiştirmede** korunuyor
- `EmployeeAccessGuard` her iki komutta da çağrılıyor — yönetici yalnız kendi ekibi
- Çıkışsız kayıt `Absent` olarak doğru işaretleniyor
- Aylık özet doğru agregeliyor (canlı doğrulandı: 4 gün / 3 mevcut / 1 devamsız)
- `vw_MonthlyAttendanceSummary` **`TenantId` taşıyor** — kontrol edildi, sızıntı yok

---

## Katman 1 — Mantık (senaryo yürüyüşü)

### A-1 · Gece vardiyası kaydedilemiyor — YÜKSEK

Canlı denendi:

```
22:00 → 06:00  →  400
"Model.CheckOut: Çıkış saati girişten sonra olmalı."
```

Hem doğrulayıcı hem `Compute` `checkOut > checkIn` şartına dayanıyor; gece yarısını
aşan vardiya kavramı **hiç yok**.

**Etkisi:** fabrika, hastane, güvenlik, otelcilik, çağrı merkezi — gece vardiyası
olan her sektörde o personelin puantajı sisteme **hiç girilemez**. Girilemeyince
fazla mesaisi de bordroya ulaşmaz. Üstelik gece çalışması İş Kanunu md.69'a göre
ayrıca sınırlıdır (7,5 saat); sistem bu kavramı hiç tanımıyor.

### A-2 · Aydaki tek kayıt puantajı "tam" sayıyor — YÜKSEK

Bordro dönemi oluşturulurken puantaj tamlığı şöyle belirleniyor:

```csharp
TimesheetComplete = summary is not null && summary.TotalDays > 0
```

Canlı doğrulandı: 21 iş günü olan bir ayda **4 kayıt** (biri devamsız) girildi ve
özet `totalDays = 4` döndü → bordro satırı **"puantaj tam"** sayılır.

`TimesheetComplete`, bordro satırının **onaylanabilmesi için gereken iki
kapıdan biri**. Yani ayın 21 gününden 4'ü girilmiş bir personelin bordrosu
onaylanabiliyor ve eksiklik hiçbir yerde görünmüyor. Kontrol kartında "Eksik
Puantaj" sayacı da bu bayrağa dayandığı için **0 gösterir**.

**Çözüm (2026-08-28):** beklenen iş günleri işe giriş tarihi, hafta sonu ve
tatillerden türetiliyor; puantaj kayıtları ile onaylı izinler birleşik kapsam
olarak sayılıyor. Bayrak API girdi modelinden ve arayüzdeki düzenlenebilir
checkbox'tan kaldırıldı. Dönem oluşturma, güncelleme, kontrol ve onay aynı
hesabı kullanıyor. Eksik puantajı `timesheetComplete=true` göndererek aşma
girişimi entegrasyon testiyle reddediliyor.

### A-3 · Fazla mesai eşiği günlük 8 saat sabit — YÜKSEK

```csharp
public const int StandardWorkMinutes = 480;      // 8 saat
var overtime = Math.Max(0, worked - StandardWorkMinutes);
```

İki sorun:

1. **Yapılandırılamaz.** Şirketin haftalık çalışma düzeni (5×9, 6×7,5, vardiya)
   değişkendir; eşik sabit.
2. **Mevzuat (müşavir doğrulaması gerekir).** İş Kanunu md.63'e göre fazla
   çalışma **haftalık 45 saati** aşan süredir. Günlük 8 saat eşiği, Cumartesi
   çalışan bir şirkette (6 × 7,5 = 45) her günü normal saymalıyken **hiç fazla
   mesai üretmez**; 5 günde 9'ar saat çalışan bir şirkette ise haftalık 45'i
   aşmadığı hâlde **her gün 1 saat fazla mesai** yazar ve bordroya fazla ödeme
   olarak gider.

### A-4 · Ara dinlenme serbest, fazla mesai şişiyor — ORTA

Canlı karşılaştırma, aynı 09:00–19:00 aralığı için:

```
break = 0   →  çalışılan 600 dk, fazla mesai 120 dk
break = 60  →  çalışılan 540 dk, fazla mesai  60 dk
```

Doğrulayıcı 0–480 arası her değeri kabul ediyor. İş Kanunu md.68 ara dinlenmeyi
çalışma süresine göre **zorunlu** kılar (7,5 saatin üzerinde en az 1 saat).
Sıfır ara dinlenme girilerek fazla mesai iki katına çıkarılabiliyor — ve bu
doğrudan ödemeye yansıyor.

### A-5 · Gelecek tarihli puantaj girilebiliyor — ORTA

`2030-01-15` için 12 saatlik kayıt **201** ile kabul edildi (180 dk fazla mesai).
`WorkDate` için üst sınır doğrulaması yok.

---

## Katman 2 — Pratik kod kalitesi

| # | Kontrol | Sonuç |
| --- | --- | --- |
| 1 | Kiracı izolasyonu | ✅ Kayıtlar global filtreli; **view `TenantId` taşıyor** (canlı tanım kontrol edildi) |
| 2 | Yetki kapsamı | ✅ `EmployeeAccessGuard` her iki komutta da çağrılıyor |
| 3 | Doğrulama | ❌ A-4, A-5; kısmen A-1 |
| 4 | İşlem sınırı | ✅ Tek varlık, tek `SaveChanges` — çok adımlı yazma yok |
| 5 | N+1 / kartezyen | ✅ Sorgular projeksiyonlu, agregasyon view'da |
| 6 | Hata zarfı | ✅ 400/403/404/409 doğru |
| 7 | Audit izi | ✅ `AttendanceRecords` audit trigger kapsamında |
| 8 | Test boşluğu | ❌ Aşağıda |

### A-7 · Onaylanmış bordronun puantajı sonradan değişebiliyor — ORTA

`UpdateAttendanceEntryCommandHandler` yalnız gün-teklik kuralına bakıyor; o
tarihin ait olduğu bordro döneminin **durumunu sorgulamıyor**. Kasım bordrosu
onaylanıp kapandıktan sonra Kasım puantajı düzenlenebiliyor.

Onaylı bordro snapshot olduğu için **tutar değişmiyor** (bu doğru tasarım — bkz.
Payroll ÖD), ama puantaj ile ödenmiş bordro arasında **kalıcı tutarsızlık**
oluşuyor ve denetimde bu fark açıklanamaz.

### A-8 · Tip hem istemciden hem sunucudan — DÜŞÜK

```csharp
record.Type = string.IsNullOrEmpty(model.Type)
    ? (overtime > 0 ? TimesheetType.Overtime : TimesheetType.Full)
    : AttendanceMappings.ParseType(model.Type);
```

İstemci `Type = "Tam"` gönderirken 3 saat fazla mesai varsa kayıt "Tam" olarak
saklanıyor — hesaplanan veriyle çelişiyor. Ya tip tamamen türetilmeli ya da
çelişki doğrulanmalı.

### Test boşluğu

Mevcut 6 test: hesaplama, gün-teklik (oluşturma ve güncelleme), canlı pano,
aylık özet agregasyonu, kapsam daraltması. **İyi seçilmiş bir set.**

**Hiç test edilmemiş davranışlar:** gece vardiyası (A-1), puantaj tamlığının
bordroya etkisi (A-2), ara dinlenme sınırları (A-4), gelecek tarih (A-5),
onaylı döneme müdahale (A-7), tip çelişkisi (A-8).

---

## Sonraki adım

1. **A-2** — bordro onayını açan kapı; en düşük maliyetli, en yüksek etkili düzeltme
2. **A-1** — gece vardiyası; veri modeli etkisi var (çıkış tarihi ayrı tutulmalı)
3. **A-3 + A-4** — mesai eşiği ve ara dinlenme; müşavir girdisiyle birlikte, yapılandırılabilir hale
4. **A-7** — dönem kilidi
5. **A-5, A-6, A-8** — doğrulama ve kalite borcu
