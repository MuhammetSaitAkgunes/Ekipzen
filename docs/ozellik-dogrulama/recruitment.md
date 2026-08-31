# Recruitment (İşe Alım) — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Kapsam:** 10 uç nokta, `Features/Recruitment` +
`RecruitmentController` · **Mevcut test:** 4

**Neden ilk bu alan:** kapsama açısından en zayıf alan (10 uç / 4 test) ve `hire`
akışı **Identity kullanıcısı yaratıyor** — yani buradaki bir hata doğrudan
güvenlik/kimlik hatasına dönüşebilir.

---

## Özet

| # | Katman | Önem | Bulgu | Durum |
| --- | --- | --- | --- | --- |
| R-1 | Mantık | **YÜKSEK** | İşe alım tarihi hiç doğrulanmıyor (1990 ve 2099 kabul edildi) | açık |
| R-2 | Mantık | **YÜKSEK** | Geçmiş tarihli işe alımda personelin **cari yılda izin bakiyesi olmuyor** | açık |
| R-3 | Mantık | **YÜKSEK** | 24 günlük izin hak edişi sabit kodlanmış; mevzuata aykırı görünüyor | dış girdi bekliyor (müşavir) |
| R-4 | Mantık | ORTA | Tek kelimelik isimde soyad boş kalıyor | açık |
| R-5 | Mantık | ORTA | İşe alım **geri alınamıyor**; yanlış işe alım düzeltilemiyor | açık |
| **R-6** | Kod | **ÇÖZÜLDÜ 2026-08-31** | İş verileri atomik; Identity/davet kalıcı, idempotent ve yeniden denenebilir iş | integration + UI testi |
| R-7 | Kod | ORTA | `GetCandidateQuery` 6 koleksiyon `Include` — kartezyen çarpım | açık |
| R-8 | Kod | DÜŞÜK | Pozisyon oluştururken departman iki kez sorgulanıyor | açık |
| R-9 | Kod | ORTA | `notes` ve `evaluations` uçlarının hiç testi yok | açık |
| R-10 | Kod | DÜŞÜK | `AuthorName` denormalize; kullanıcı adı değişince not eski adı taşır | açık |

**Doğru çalışan, kanıtlanmış davranışlar** (bunlar da raporun parçası — neyin
güvenilir olduğunu bilmek, neyin bozuk olduğunu bilmek kadar önemli):

- Pipeline geçişleri (`Yeni → Mülakat → Teklif`) ve geçmiş kaydı
- İşe alınmış aday **tekrar işe alınamıyor** (409)
- İşe alınmış adayın pipeline durumu **değiştirilemiyor** (409)
- Pozisyon kontenjanı düşüyor, bitince ilan kapanıyor
- Funnel sayıları tutarlı
- E-posta ön kontrolü, yazımlar başlamadan çakışmayı yakalıyor
- Tüm controller `HrAdminOnly` — bu alanda satır düzeyi yetki boşluğu **yok**

---

## Katman 1 — Mantık (senaryo yürüyüşü)

Gerçek API üzerinden, demo kiracıda, uçtan uca yürütüldü: pozisyon aç → aday
başvurusu → pipeline → not/değerlendirme → işe al → sonrasını doğrula.

### R-1 · İşe alım tarihi hiç doğrulanmıyor — YÜKSEK

`HireCandidateCommandValidator` `DepartmentId`, `Email` ve `Title` doğruluyor;
**`HireDate` için hiçbir kural yok.**

Denendi ve **ikisi de kabul edildi**:

```
hireDate = 1990-01-01  → 200 OK
hireDate = 2099-12-31  → 200 OK
```

Bu tek başına da sorun ama asıl zararı R-2 ile birleşince veriyor.

### R-2 · Geçmiş tarihli işe alımda cari yılda izin bakiyesi oluşmuyor — YÜKSEK

`hire` yalnız **tek bir** izin bakiyesi açıyor ve yılını `hireDate.Year`'dan
alıyor:

```csharp
context.LeaveBalances.Add(new LeaveBalance {
    Employee = employee, Year = hireDate.Year, EntitledDays = 24, ...
});
```

Veritabanında doğrulandı:

```
personel: Zeynep Aydın | iseGiris=1990-01-01
izin bakiyesi: yil=1990 hakedis=24 devir=0     ← cari yıl (2026) için bakiye YOK
```

**Neden gerçek bir senaryo:** geçmiş tarihli işe alım istisna değil, kuraldır —
sisteme geçerken mevcut personelin aktarılması, geç girilen kayıt, deneme süresi
sonrası resmileştirme. Bu personel **cari yılda hiç izin kullanamaz** ve sebebi
ekranda görünmez; "bakiyesi yok" der.

Gelecek tarihli işe alımda da aynı: `2099` yılına bakiye açıldı.

### R-3 · İzin hak edişi sabit 24 gün — YÜKSEK, müşavir doğrulaması gerekir

`EntitledDays = 24` handler'ın içine gömülü. İki ayrı sorun:

1. **Mevzuat.** 4857 sayılı İş Kanunu md.53'e göre yıllık izin **kıdeme
   bağlıdır** (1–5 yıl: 14 gün, 5–15 yıl: 20 gün, 15+ yıl: 26 gün) ve hak ediş
   **bir yıllık çalışma dolduğunda** başlar. Yeni işe alınan birine, işe giriş
   yılında 24 gün vermek hem **fazla** hem **erken** görünüyor. Fazla verilen
   izin, kullanılmazsa çıkışta **ücrete dönüşür** — yani doğrudan para.
2. **Yer.** Sayı bir iş kuralı ama komut handler'ının içinde sabit. Şirket yasal
   asgariden fazla vermeyi seçebilir — bu meşrudur, ama o zaman **ayar** olmalı,
   sabit değil.

> Bu bulguyu mevzuat uzmanı doğrulamalıdır; ben hukuki görüş vermiyorum.
> Müşavir görüşü geldiğinde bu madde revize edilecek.

### R-4 · Tek kelimelik isimde soyad boş — ORTA

Ad, boşluktan bölünüyor: son parça soyad, kalanı ad. Tek kelimelik isimde
(`"Cher"`) soyad **boş string** kalıyor — doğrulandı. Yasal belgelerde ve bordro
çıktısında soyadsız personel sorun çıkarır.

### R-5 · İşe alım geri alınamıyor — ORTA

Yanlış aday işe alındığında düzeltme yolu **yok**:

- Aday `Hired`'da kilitli — durum değiştirme 409
- Tekrar işe alma 409
- Personel kaydı, izin bakiyesi ve kullanıcı duruyor
- Pozisyon kontenjanı düşmüş durumda

Operatörün elinde kalan tek çare doğrudan veritabanına müdahale. Bu, Faz 2b'de
provizyon için tespit edilip `resend-invite` ile kapatılan boşluğun aynısı —
burada karşılığı yok.

---

## Katman 2 — Pratik kod kalitesi

| # | Kontrol | Sonuç |
| --- | --- | --- |
| 1 | Kiracı izolasyonu | ✅ Tüm sorgular `IApplicationDbContext` üzerinden, global filtre kapsıyor; ambient context kötüye kullanımı yok |
| 2 | Yetki kapsamı | ✅ Controller tamamı `HrAdminOnly`; satır düzeyi daraltma bu alanda gerekmiyor |
| 3 | Doğrulama | ❌ R-1 (tarih), R-4 (isim) |
| 4 | İşlem sınırı | ✅ İş verisi atomik; dış Identity etkisi telafi edilebilir ve idempotent |
| 5 | N+1 / kartezyen | ❌ R-7, R-8 |
| 6 | Hata zarfı | ✅ 404/409 doğru kullanılmış, mesajlar iç detay sızdırmıyor |
| 7 | Audit izi | ✅ `CandidateHistory` durum değişimi, not ekleme ve işe alımı kaydediyor |
| 8 | Test boşluğu | ❌ R-9 |

### R-6 · İşe alım + login arası yarım işlem — ÇÖZÜLDÜ (2026-08-31)

İşe alımın iş verileri ve `IdentityProvisioningOperation` aynı `SaveChanges`
içinde kalıcılaşıyor. Identity hesabı transaction dışındaki dış etki olarak
idempotent bir işlemciye bırakılıyor; lease, deneme sayısı, son hata, sonraki
deneme zamanı ve tamamlanma zamanı kalıcı tutuluyor.

İlk deneme istek içinde best-effort çalışır. Geçici SMTP/altyapı hatası işe alımı
geri almaz; tenant-izole background worker üstel geri çekilmeyle yeniden dener.
İK aday detayından durumu/hata mesajını görür, gerekirse e-postayı düzeltip
manuel retry başlatır. Tamamlanmış iş yeniden kuyruğa alınmaz. Entegrasyon testi
iki geçici e-posta hatası, manuel retry, otomatik worker iyileşmesi, davet kabulü
ve login zincirini doğrular.

### R-7 · `GetCandidateQuery` kartezyen çarpım üretiyor — ORTA

Tek sorguda **altı koleksiyon** `Include` ediliyor (`Skills`, `Experiences`,
`Notes`, `Evaluations`, `History`, + `Position`) ve projede
`AsSplitQuery`/`QuerySplittingBehavior` **hiç kullanılmıyor** (kod tabanında tek
bir örneği yok).

EF Core bunu tek `JOIN` sorgusuna çevirir; satır sayısı koleksiyonların
**çarpımı** kadar olur. Gerçekçi bir adayda 5 yetenek × 3 deneyim × 10 not ×
5 değerlendirme × 20 geçmiş = **15.000 satır** — tek bir aday detayı için.

### R-8 · Departman iki kez sorgulanıyor — DÜŞÜK

`CreatePositionCommandHandler` önce `AnyAsync` ile varlığı doğruluyor, sonra
ismi için `SingleAsync` çekiyor. Tek sorgu yeterdi.

### R-9 · İki uç noktanın hiç testi yok — ORTA

Mevcut testler şu uçlara dokunuyor: `candidates` (liste/oluştur), `status`,
`hire`, `positions`, `funnel`.

**Hiç dokunulmayanlar:** `POST /candidates/{id}/notes`,
`POST /candidates/{id}/evaluations`. Onda ikisi test edilmemiş.

### R-10 · `AuthorName` denormalize — DÜŞÜK

`InterviewNote.AuthorName`, not eklenirken `currentUser.UserName`'den kopyalanıyor.
Kullanıcının adı sonradan değişirse not eski adı taşımaya devam eder.
**Kasıtlı olabilir** — audit kaydının o anki adı saklaması savunulabilir bir
tercihtir. Karar verilip yorumla sabitlenmeli; bugün belirsiz.

---

## Sonraki adım

`R-6` kapandı. Kalan önerilen sıra:

1. **R-1 + R-2** (tarih doğrulama + bakiye yılı) — birlikte çözülmeli, aynı kök
2. **R-5** (işe alımı geri alma) — artık R-6'nın kalıcı işlem altyapısına dayanabilir
3. **R-9** (eksik testler) — düzeltmelerle birlikte yazılır
4. **R-7, R-4, R-8, R-10** — kalite borcu
5. **R-3** — müşavir görüşü geldiğinde
