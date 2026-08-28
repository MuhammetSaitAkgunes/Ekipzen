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
| P-1 | Mantık | **YÜKSEK** | **Negatif net ücret** üretilebiliyor ve onaylanabiliyor | **çözüldü 2026-08-28** |
| P-2 | Mantık | **YÜKSEK** | Sonradan onaylanan dönem, önce açılmış dönemin **kümülatif GV matrahına yansımıyor** | **çözüldü 2026-08-28** |
| P-3 | Mantık | **YÜKSEK** | Dönemler **sırasız** açılabiliyor (Aralık'tan sonra Kasım) | **çözüldü 2026-08-28** |
| P-4 | Mantık | **YÜKSEK** | Ay içinde işten ayrılan personel bordroya **hiç girmiyor** | **çözüldü 2026-08-28** |
| P-5 | Mantık | ORTA | Sıfır brüt maaşlı satır onaylanabiliyor, uyarı yok | **çözüldü 2026-08-28** |
| P-6 | Mantık | ORTA | 2099 gibi uzak gelecek dönemi açılabiliyor | açık |
| P-7 | Mantık | ORTA | Maaş her dönem elle giriliyor; önceki dönemden devir yok | açık |
| P-8 | Kod | ORTA | `PreviousTaxBase` dönem **oluşturulurken** dondurulup bir daha güncellenmiyor | **çözüldü 2026-08-28** |
| P-9 | Kod | ORTA | Kümülatif devir filtresi `Result != null`, yorumu "onaylı" diyor — ölçüt uyuşmuyor | **çözüldü 2026-08-28** |
| P-10 | Kod | ORTA | `BuildDetailAsync` iki koleksiyon `Include` — kartezyen çarpım | **çözüldü 2026-08-28** |
| P-11 | Kod | DÜŞÜK | SGK kontrol kartında `* 0.95m` sihirli sayısı, gerekçesi yok | açık |
| P-12 | Kod | ORTA | Onay/gönderim yollarında eşzamanlılık koruması yok | **çözüldü 2026-08-28** |

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

**Çözüm (2026-08-28):** motor negatif/sıfır net için açık uyarı üretiyor;
merkezi `PayrollRowReadiness` neti `<= 0` olan satırı hem kontrol durumunda
`Eksik Veri` bırakıyor hem onay anında 409 ile durduruyor. Formülün mevzuat
doğruluğu hâlâ mali müşavir kapsamındadır; kapanan bulgu onay açığıdır.

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

**Çözüm (2026-08-28):** daha ileri dönem, en son dönem onaylanmadan açılamıyor;
kümülatif matrah ayrıca dönem oluşturma, canlı detay, satır güncelleme, kontrol
ve onay anında sunucuda yeniden türetiliyor. Böylece önceki dönem sonucu sonradan
değişse bile onaysız satır eski snapshot'a bağlı kalmıyor. İstemci matrahı
değiştiremiyor.

### P-3 · Dönemler sırasız açılabiliyor — YÜKSEK

`2030/12` açıldıktan sonra `2030/11` açılabildi (201). Doğrulayıcı yalnız
yıl (2000–2100) ve ay (1–12) aralığına bakıyor; **önceki dönemin durumu
sorgulanmıyor.**

Tek başına da hatalı ama asıl zararı P-2'yi mümkün kılması.

**Çözüm (2026-08-28):** `PayrollPeriodSequence`, en son dönemden önceki/eşit
bir ayı ve en son dönem `approved/closed` olmadan daha ileri bir ayı 409 ile
reddediyor. Ay atlamaya izin veriliyor; kronolojik geri dönüşe izin verilmiyor.
Bu davranış dönem yaşam döngüsü entegrasyon testinde kanıtlandı.
Aynı tenant için dönem oluşturmanın tamamı `sp_getapplock` tabanlı, transaction
sahipli dağıtık kilit altında çalışıyor. İki farklı ileri ay eşzamanlı istendiğinde
entegrasyon testi tam bir `201` ve bir `409` görüyor. `P-12` yalnız satır onayı ve
dönem gönderiminin eşzamanlılığı için açık kalıyor.


### P-4 · Ay içinde ayrılan personel bordroya hiç girmiyor — YÜKSEK

Dönem oluşturulurken satırlar yalnız aktif personel için üretiliyor:

```csharp
.Where(e => e.Status == EmployeeStatus.Active)
```

15 Kasım'da işten ayrılan bir personel, Kasım bordrosu 20 Kasım'da açıldığında
**listede hiç yer almaz** — çalıştığı 15 günün ücreti, kıdem/ihbar ve SGK
bildirimi bordroya girmez. Ayrılan personelin son ay bordrosu, İK'nın en kritik
işlemlerinden biridir.

**Çözüm (2026-08-28):** `Employee.TerminationDate` etkin son çalışma gününü
saklıyor; pasife alırken zorunlu, yeniden aktifleştirmede temizleniyor. Dönem
seçimi işe giriş/ayrılış aralığına göre yapılıyor. Ayrılıştan sonraki günler
puantaj eksiği sayılmıyor; çalışan son ay bordrosuna giriyor, sonraki döneme
taşınmıyor. Ayrılış tarihi bilinmeyen eski pasif kayıtlar yanlışlıkla bordroya
alınmıyor. Kalan tam işten çıkış süreci `E-5` / Kapı 3 kapsamındadır.

### P-5 · Sıfır brüt maaşlı satır onaylanabiliyor — ORTA

`GrossSalary` doğrulaması yalnız `>= 0`. Sıfır maaş, veri girilmemiş satırın
işaretidir; onaylanabilmesi "unutulan personel" hatasını sessizleştirir.
(Dönem oluşturulurken tüm satırlar zaten `GrossSalary = 0` ile doğuyor.)

**Çözüm (2026-08-28):** brüt maaşı `<= 0` olan satır `Eksik Veri` kalır,
"Brüt maaş sıfır" uyarısı üretir ve onayda 409 alır. Gerekçeli istisna akışı
ayrıca tasarlanana kadar otomatik onaya bilinçli olarak kapalıdır.

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
| 3 | Doğrulama | ⚠️ P-6 açık; P-3 ve P-5 kapalı |
| 4 | İşlem sınırı | ✅ Dönem mutasyonları tenant bazlı transaction-owned application lock ile serileştiriliyor |

**Çözüm (2026-08-28):** `PayrollCumulativeTaxBase`, değeri onaysız satırlar
için her kritik yolda yeniden türetiyor. `PayrollRowInputModel`, OpenAPI tipi ve
düzenleme formu artık `PreviousTaxBase` kabul etmiyor; gösterim salt-okur.
| 5 | N+1 / kartezyen | ✅ Dönem/ayar ve çalışan satırları iki açık sorguda; koleksiyon çarpımı yok |
| 6 | Hata zarfı | ✅ 404/409 doğru; mesajlar iç detay sızdırmıyor |
| 7 | Audit izi | ⚠️ `PayrollEmployees` audit trigger'ı var; **dönem durum geçişleri** ayrı iz bırakmıyor |
| 8 | Test boşluğu | ❌ Aşağıya bakınız |

### P-8 · `PreviousTaxBase` dondurulmuş snapshot — ORTA (P-2'nin kökü)

Değer dönem **oluşturulurken** hesaplanıp satıra yazılıyor. Sonradan:
önceki dönem onaylanırsa, düzeltilirse ya da yeni bir dönem araya girerse

**Çözüm (2026-08-28):** sorgu artık hem satırın `ApprovalStatus.Approved`
olmasını hem de ait olduğu dönemin `Approved/Closed` olmasını açıkça istiyor;
`Result != null` yalnız sonuç varlığı güvenlik koşulu olarak kaldı.
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

**Çözüm (2026-08-28):** uygulama katmanına relational-provider bağımlılığı
eklemek yerine dönem + vergi dilimleri ve çalışan + sonuç satırları iki açık
EF sorgusuna ayrıldı. Böylece sorgu maliyeti çarpım yerine toplamsal büyüyor.

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


**Çözüm (2026-08-28):** düzenleme, toplu kontrol, satır onayı ve dönem gönderimi
aynı `payroll-period:{id}` dağıtık kilidini kullanıyor. Çoklu API instance'ında
çift satır onayı ve çift dönem gönderimi serileştiriliyor; entegrasyon testi her
iki senaryoda bir başarılı yanıt ve bir 409 görüyor.
### Test boşluğu

Otomatik testler parite/asgari ücret, dönem yaşam döngüsü, kümülatif matrah,
dönem sırası, negatif/sıfır net kapıları, ayar sürümleme, yetki, pusula PDF ve
çalışanın kendi bordrolarını kapsıyor.

**Açık test boşluğu:** ayar güncellemesinin onaylı snapshot'a etkisizliğinin
ayrı bir regresyon senaryosuyla kanıtlanması.

---

## Sonraki adım

Kapı 2'nin zorunlu üç dilimi tamamlandı. Kalan bordro adayları:

1. **P-7** — maaş devri ürün kararı
2. **P-6** — dönem üst sınırı
3. **P-11** — açıklanmamış SGK kontrol eşiği; müşavir girdisi gerekli
4. **Kapı 3 / E-5** — yöneticilik, izin ve kıdem/ihbar yan etkileri
