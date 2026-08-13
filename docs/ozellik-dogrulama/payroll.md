# Payroll (Bordro) — Özellik Doğrulama

**Tarih:** 2026-08-13 · **Kapsam:** 13 uç nokta, `Features/Payroll` +
`PayrollController` + `Domain/Services/PayrollEngine` · **Mevcut test:** 12

**Neden bu alan ikinci:** iş riski en yüksek modül. Yanlış bordro para, SGK ve
vergi demektir; hata aylar sonra fark edilir ve fatura üreticiye çıkar.

> **Kapsam sınırı — okumadan geçmeyin.** Bu ÖD, **hesaplama formüllerinin
> mevzuata uygunluğunu doğrulamaz**; o, mali müşavirin işidir ve beklemektedir.
> Burada sınanan şey müşavirin göremeyeceği katman: **durum makinesi, dönem
> sırası, kümülatif devir, onay kapıları, yetki ve veri bütünlüğü.**
> Mevcut 12 test de formülü değil, C# motorunun `payroll.js` ile **paritesini**
> ölçüyor — iki uygulama aynı varsayımı paylaşıyorsa ikisi de yanlış olabilir.

---

## Özet

| # | Katman | Önem | Bulgu | Durum |
| --- | --- | --- | --- | --- |
| P-1 | Mantık | **YÜKSEK** | **Negatif net ücret** üretilebiliyor ve onaylanabiliyor | açık |
| P-2 | Mantık | **YÜKSEK** | Sonradan onaylanan dönem, önce açılmış dönemin **kümülatif GV matrahına yansımıyor** | açık |
| P-3 | Mantık | **YÜKSEK** | Dönemler **sırasız** açılabiliyor (Aralık'tan sonra Kasım) | açık |
| P-4 | Mantık | **YÜKSEK** | Ay içinde işten ayrılan personel bordroya **hiç girmiyor** | açık |
| P-5 | Mantık | ORTA | Sıfır brüt maaşlı satır onaylanabiliyor, uyarı yok | açık |
| P-6 | Mantık | ORTA | 2099 gibi uzak gelecek dönemi açılabiliyor | açık |
| P-7 | Mantık | ORTA | Maaş her dönem elle giriliyor; önceki dönemden devir yok | açık |
| P-8 | Kod | ORTA | `PreviousTaxBase` dönem **oluşturulurken** dondurulup bir daha güncellenmiyor | açık |
| P-9 | Kod | ORTA | Kümülatif devir filtresi `Result != null`, yorumu "onaylı" diyor — ölçüt uyuşmuyor | açık |
| P-10 | Kod | ORTA | `BuildDetailAsync` iki koleksiyon `Include` — kartezyen çarpım | açık |
| P-11 | Kod | DÜŞÜK | SGK kontrol kartında `* 0.95m` sihirli sayısı, gerekçesi yok | açık |
| P-12 | Kod | ORTA | Onay/gönderim yollarında eşzamanlılık koruması yok | açık |

**Doğru çalışan, kanıtlanmış davranışlar:**

- Onaylı satır **tekrar onaylanamıyor** (409)
- Onaylı satır **düzenlenemiyor** (409)
- Onaylanmış/kapatılmış dönemde değişiklik engelleniyor (`EnsurePeriodOpen`)
- Eksik IBAN/puantajlı satır onaylanamıyor
- Onay anında sonuç **kalıcı snapshot**'a yazılıyor (`PayrollResult`) — sonradan
  parametre değişse bile onaylı bordro değişmiyor. **Bu doğru ve önemli bir tasarım.**
- Dönem parametreleri, dönemin ilk gününde yürürlükteki sete bağlanıyor
- Aynı yıl/ay için ikinci dönem açılamıyor (409)

---

## Katman 1 — Mantık (senaryo yürüyüşü)

### P-1 · Negatif net ücret üretiliyor ve onaylanabiliyor — YÜKSEK

Brüt maaşı 0 olan bir satırda motor şunu üretti:

```
brut maas            0.00
brut kazanc          0.00
SGK matrahi      33030.00      ← kazanç 0 iken taban matrah uygulanıyor
SGK isci          4624.20
issizlik           330.30
toplam kesinti    4954.50
NET ODEME        -4954.50      ← personel şirkete BORÇLU görünüyor
isveren maliyet   7431.75      ← hiç kazanmamış personel için işveren maliyeti
uyarilar          ['Puantaj eksik', 'IBAN eksik']   ← negatif netle ilgili UYARI YOK
```

Ve IBAN/puantaj tamamlanmış bir satırda **onay 200 döndü** — yani negatif net
ücretli bordro satırı sisteme onaylı olarak girebiliyor.

İki ayrı sorun var:

1. **Kontrol açığı (kesin):** net ücretin negatif çıkması her koşulda anormaldir.
   Onay kapısı bunu geçirmemeli, en azından açık bir uyarı üretmeli. Bugün
   sessizce geçiyor.
2. **Hesap sorusu (müşavir doğrulaması gerekir):** kazanç sıfırken SGK taban
   matrahının uygulanması. Ay boyu ücretsiz izindeki bir personelde eksik gün
   bildirimi söz konusudur; taban matrahtan prim tahakkuku doğru görünmüyor.
   Ancak formül kararı müşavirindir — burada **kontrol açığı** kayda geçiyor,
   formül hükmü değil.

### P-2 · Kümülatif gelir vergisi matrahı devri kopuyor — YÜKSEK

Senaryo (gerçekten çalıştırıldı):

1. `2030/12` dönemi açıldı
2. `2030/11` dönemi açıldı
3. Kasım'a 100.000 brüt girildi, GV matrahı **85.000** çıktı, satır **onaylandı**
4. Aralık dönemi okundu → aynı personelin `previousTaxBase` = **0.00**

Aralık, Kasım'ın 85.000'lik matrahını **görmüyor**. Gelir vergisi Türkiye'de
kümülatif matraha göre dilimlenir; devir kopunca çalışan alt dilimde kalır ve
**vergi eksik hesaplanır**. Fark, yıl sonunda kuruma karşı sorumluluk doğurur.

Kök neden P-8 ile aynı: `PreviousTaxBase` dönem **oluşturulurken** bir kez
hesaplanıp satıra yazılıyor; sonradan onaylanan önceki dönemler bu değeri
güncellemiyor.

### P-3 · Dönemler sırasız açılabiliyor — YÜKSEK

`2030/12` açıldıktan sonra `2030/11` açılabildi (201). Doğrulayıcı yalnız
yıl (2000–2100) ve ay (1–12) aralığına bakıyor; **önceki dönemin durumu
sorgulanmıyor.**

Tek başına da hatalı ama asıl zararı P-2'yi mümkün kılması.

### P-4 · Ay içinde ayrılan personel bordroya hiç girmiyor — YÜKSEK

Dönem oluşturulurken satırlar yalnız aktif personel için üretiliyor:

```csharp
.Where(e => e.Status == EmployeeStatus.Active)
```

15 Kasım'da işten ayrılan bir personel, Kasım bordrosu 20 Kasım'da açıldığında
**listede hiç yer almaz** — çalıştığı 15 günün ücreti, kıdem/ihbar ve SGK
bildirimi bordroya girmez. Ayrılan personelin son ay bordrosu, İK'nın en kritik
işlemlerinden biridir.

### P-5 · Sıfır brüt maaşlı satır onaylanabiliyor — ORTA

`GrossSalary` doğrulaması yalnız `>= 0`. Sıfır maaş, veri girilmemiş satırın
işaretidir; onaylanabilmesi "unutulan personel" hatasını sessizleştirir.
(Dönem oluşturulurken tüm satırlar zaten `GrossSalary = 0` ile doğuyor.)

### P-6 · Uzak gelecek dönemi açılabiliyor — ORTA

`2099/01` dönemi açıldı (201). Aralık 2000–2100 çok geniş.

### P-7 · Maaş her dönem sıfırdan giriliyor — ORTA

Yeni dönemin her satırı `GrossSalary = 0` ile doğuyor; önceki dönemden devir yok.
200 kişilik bir şirkette her ay 200 maaşın elle girilmesi gerekir — hem
sürdürülemez hem de **unutulan satır sıfır maaşla onaylanabilir** (P-5).

---

## Katman 2 — Pratik kod kalitesi

| # | Kontrol | Sonuç |
| --- | --- | --- |
| 1 | Kiracı izolasyonu | ✅ Tüm erişim `IApplicationDbContext` üzerinden; global filtre kapsıyor |
| 2 | Yetki kapsamı | ✅ Dönem yönetimi `HrAdminOnly`; `payroll/my` `PayrollAccess` + kendi kaydı |
| 3 | Doğrulama | ❌ P-5, P-6, P-3 |
| 4 | İşlem sınırı | ⚠️ Tek `SaveChanges` kullanılıyor (iyi), ama **eşzamanlılık koruması yok** — P-12 |
| 5 | N+1 / kartezyen | ❌ P-10 |
| 6 | Hata zarfı | ✅ 404/409 doğru; mesajlar iç detay sızdırmıyor |
| 7 | Audit izi | ⚠️ `PayrollEmployees` audit trigger'ı var; **dönem durum geçişleri** ayrı iz bırakmıyor |
| 8 | Test boşluğu | ❌ Aşağıya bakınız |

### P-8 · `PreviousTaxBase` dondurulmuş snapshot — ORTA (P-2'nin kökü)

Değer dönem **oluşturulurken** hesaplanıp satıra yazılıyor. Sonradan:
önceki dönem onaylanırsa, düzeltilirse ya da yeni bir dönem araya girerse
**güncellenmiyor**. Doğrusu ya okuma anında türetmek ya da önceki dönem
onaylandığında sonraki dönemleri yeniden hesaplamaktır.

### P-9 · Devir filtresi ile yorumu uyuşmuyor — ORTA

Yorum "aynı yılın **EN SON ONAYLI** döneminin kümülatifi" diyor; kod ise
`pe.Result != null` filtreliyor. `Result` yalnız onayda yazılıyorsa ikisi bugün
denk düşer — ama ölçüt **niyeti ifade etmiyor**. İleride `Result` başka bir
yolda da yazılırsa devir sessizce yanlışlanır. Ölçüt `ApprovalStatus == Approved`
olmalı.

### P-10 · Kartezyen çarpım — ORTA

`BuildDetailAsync` aynı sorguda iki koleksiyon `Include` ediyor
(`Settings.TaxBrackets` + `Employees`) ve projede `AsSplitQuery` hiç
kullanılmıyor. 200 personel × 7 vergi dilimi = **1.400 satır**, her dönem
görüntülemede.

### P-11 · Açıklanmamış sihirli sayı — DÜŞÜK

```csharp
r.SgkBase <= parameters.SgkBaseMin * 0.95m
```

`0.95`'in gerekçesi yazılı değil. Eşik kuralları bordroda denetlenebilir olmalı.

### P-12 · Eşzamanlılık koruması yok — ORTA

`approve` ve `submit` yollarında satır/dönem üzerinde iyimser kilit
(`RowVersion`/`concurrency token`) yok. İki İK kullanıcısı aynı anda çalışırsa:
son yazan kazanır, ve dönem "gönderildi" sayılırken bir satır arada değişmiş
olabilir. Bordro, son yazanın kazanmasının kabul edilebilir olmadığı bir alandır.

### Test boşluğu

Mevcut 12 test şunları kapsıyor: parite senaryosu, asgari ücret neti, dönem
yaşam döngüsü, ayar sürümleme, yetki kapıları.

**Hiç test edilmemiş davranışlar:** kümülatif matrah devri (P-2), dönem sırası
(P-3), ayrılan personelin bordrosu (P-4), negatif/sıfır net (P-1, P-5),
eşzamanlı onay (P-12), `payslip` PDF ucu, `settings` güncelleme sonrası
onaylı dönemin değişmediği.

---

## Sonraki adım

Bulgular **düzeltilmedi** (akış kararı: önce tüm alanların raporu, düzeltme en
sonda). Önem sırası:

1. **P-1** — negatif net ücret onay kapısından geçmemeli
2. **P-2 + P-3 + P-8 + P-9** — aynı kök: kümülatif devir ve dönem sırası
3. **P-4** — ayrılan personelin bordrosu
4. **P-12** — eşzamanlılık
5. **P-5, P-7** — sıfır maaş / maaş devri (birlikte tasarlanmalı)
6. **P-6, P-10, P-11** — kalite borcu
