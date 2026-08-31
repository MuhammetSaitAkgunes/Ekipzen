# Bulgular — Birleşik Liste

**Tarih:** 2026-08-13 · **Son güncelleme:** 2026-08-31 · **Kaynak:** 6 ÖD
belgesi, 11 özellik alanı
**Tespit edilen: 49 · Açık: 34 — 0 KRİTİK · 8 YÜKSEK · 18 ORTA · 8 DÜŞÜK**

Bu belge, düzeltme aşamasının tek giriş noktasıdır. Bulguların gerekçeleri,
canlı kanıtları ve kod alıntıları alan belgelerindedir; burada **ne yapılacağı
ve hangi sırayla** var.

| Alan | Bulgu | KRİTİK | YÜKSEK |
| --- | --- | --- | --- |
| [Payroll](payroll.md) | 3 açık / 12 tespit | — | — |
| [Recruitment](recruitment.md) | 10 | — | 4 |
| [Leaves](leaves.md) | 8 | — | 2 |
| [Attendance](attendance.md) | 8 | — | 3 |
| [Employees](employees.md) | 4 açık / 6 tespit | — | — |
| [Kalan alanlar](kalan-alanlar.md) | 2 açık / 5 tespit | — | — |

---

## 1. Bulgular tek tek değil, yedi kök nedenin belirtisi

49 bulgunun büyük kısmı bağımsız hata değil; aynı yapısal eksiğin farklı
modüllerdeki görünümü. Düzeltme planı **bulgu bazlı değil kök neden bazlı**
kurulmalı — yoksa aynı hata bir sonraki modülde yeniden yazılır.

### K1 · İleri yön var, geri yön yok

`R-5` işe alım geri alınamıyor · `L-3` onaylı izin iptal edilemiyor ·
`E-5` işten çıkış bir alan güncellemesi · `A-7` onaylı döneme müdahale serbest

Her modülde "yap" yolu yazılmış, "geri al / düzelt" yolu yazılmamış. Gerçek İK
operasyonunda yanlış giriş kaçınılmazdır; bugün her yanlış giriş **kalıcı**.

### K2 · Takvim kavramı yok

`L-1` yıl devri hiç yok · `L-2` yıl aşan izin başlangıç yılına yazılıyor ·
`R-2` geçmiş tarihli işe alımda cari yıl bakiyesi oluşmuyor ·
`L-6` `CarriedOverDays` her yerde `0` · `P-2` kümülatif matrah devri kopuyor ·
`P-3` dönemler sırasız açılabiliyor

Sistem "şu an"ı biliyor, **zaman içindeki sürekliliği** bilmiyor. En keskin
sonucu L-1: 1 Ocak'ta izin modülü kendiliğinden kilitleniyor.

### K3 · Süreç yok, alan güncellemesi var

`E-5` (kök) → `E-1`, `E-2` ve `E-3` kapandı. `P-4` kapandı.

İşten çıkış artık erişimi kesiyor, etkin tarihi ve son ay bordrosunu koruyor.
Yöneticilik ve bekleyen onay devri korunuyor; kıdem/ihbar hâlâ süreç değil.

### K4 · Doğrulama modül modül elle yazılmış

`X-1` (çapraz) · `C-2` `N-1` doğrulayıcı hiç yok · `E-4` geçersiz TC ·
`R-1` işe alım tarihi · `A-4` ara dinlenme · `A-5` gelecek tarih ·
`P-6` 2099 dönemi · `L-5` geçmiş tarihli talep

Ortak yapı yok; doğrulama hatırlanan yere yazılmış. X-1 bunun en somut kanıtı:
5 modülün 3'ünde enum koruması var, 2'sinde yok ve orada istemci hatası **500**
oluyor. **Bu madde, kullanıcının middleware/katman talebinin tam karşılığı.**

### K5 · Mevzuat sabit kodlanmış

`R-3` 24 gün · `A-3` günlük 480 dk · `A-4` ara dinlenme · `P-11` `* 0.95m` ·
`A-6` 09:00 / 08:45

Değerler koda gömülü, kaynağı yazılı değil. Müşavir güncellemesi geldiğinde
**kod değişikliği + dağıtım** gerektirecek. Yapılandırılabilir hale gelmeli.

### K6 · Eşzamanlılık koruması yok

`L-7` izin onayında bakiye yarışı (TOCTOU). Bordro `P-12` kapandı.

İki yönetici aynı izni aynı anda onaylarsa sonuç tanımsız. Kiracı başına düşük
trafik bunu bugün gizliyor.

### K7 · Sorgu maliyeti

`R-7` 6 koleksiyon `Include`. Bordro `P-10` kapandı.

Aday büyüdükçe kartezyen çarpım riski sürüyor; bordro detayı sağlayıcıdan
bağımsız iki açık sorguya ayrıldı.

---

## 2. Birlikte düzeltilmesi zorunlu zincirler

Bu üç zincirde halkalar tek tek düzeltilirse **hata gizlenir, kaybolmaz**.

```
ZİNCİR A — para yolu
A-2 eksik puantaj "tam" sayılıyor
  └→ P-5 sıfır brüt satır onaylanabiliyor
       └→ P-1 NEGATİF net ücret üretiliyor ve onaylanıyor

Yalnız P-1 düzeltilirse: negatif engellenir ama eksik puantajlı
bordro onaylanmaya devam eder — hata sessizleşir.

ZİNCİR B — izin hakkı
R-2 işe alımda cari yıl bakiyesi oluşmuyor
R-3 hak ediş 24 gün sabit (müşavir)
  └→ L-1 yıl devri yok
       ├→ L-2 yıl aşan izin tek yıla yazılıyor
       └→ L-6 CarriedOverDays hep 0

Devir mekanizması yazılmadan diğerleri anlamsız.

ZİNCİR C — işten çıkış
E-5 çıkış bir süreç değil
  ├→ E-1 ÇÖZÜLDÜ: erişim ve refresh kesiliyor
  ├→ E-2 ÇÖZÜLDÜ: ekip, onay kapsamı ve yönetici rolü halefe devrediliyor
  ├→ E-3 ÇÖZÜLDÜ: puantaj istihdam tarih aralığıyla sınırlı
  └→ P-4 ÇÖZÜLDÜ: ayrılış ayı bordroda korunuyor
```

---

## 3. Önerilen düzeltme sırası

| Dalga | İçerik | Neden bu sırada |
| --- | --- | --- |
| **0 — tamamlandı 2026-08-28** | **E-1** | Mevcut access token, yeni login ve refresh erişimi kesildi; reaktivasyon eski token'ı diriltmiyor. |
| **1 — tamamlandı 2026-08-28** | **X-1 + K4** doğrulama katmanı | Actions, Compliance ve Recruitment enum girdileri validator kapsamında; merkezi 400 güvenlik ağı var. |
| **2 — tamamlandı 2026-08-28** | **ZİNCİR A** (A-2 → P-5 → P-1) | Puantaj sunucu sahipli; sıfır brüt ve sıfır/negatif net onaya kapalı. |
| **2b — tamamlandı 2026-08-28** | **P-3 → P-2/P-8/P-9** | Dönemler tenant bazlı dağıtık kilitle kronolojik/final duruma bağlı; kümülatif matrah sunucu sahipli. |
| **2c — tamamlandı 2026-08-28** | **P-4/P-10/P-12** | Etkin ayrılış tarihi son ay bordrosunu koruyor; detay sorgusu ayrık; dönem mutasyonları dağıtık kilitli. |
| **3 — sürüyor; E-2/E-3/R-6 tamamlandı 2026-08-31** | **R-6 + ZİNCİR C** | İşe alım telafi edilebilir, puantaj istihdam dönemine bağlı, yöneticilik devri güvenli; E-5 sırada. |
| **4** | **ZİNCİR B** (L-1 devri) | 1 Ocak'a kadar zamanı var ama tasarım kararı gerektiriyor (devir tavanı, hak ediş kademesi). |
| **5** | **K1** geri alma yolları (R-5, L-3, A-7) | Ürün kararı gerektirir: kim, hangi süre içinde, hangi ize karşılık geri alabilir. |
| **6** | **K6** eşzamanlılık, **K7** sorgu, kalan ORTA/DÜŞÜK | Bugün müşteriyi vurmuyor; kalite borcu. |
| **—** | **K5** mevzuat (R-3, A-3, A-4, P-11) | **Müşavir girdisi bekliyor.** Bu arada değerler yapılandırılabilir hale getirilebilir — asıl iş bu, sayının kendisi değil. |

---

## 4. Tam liste

Önem: **KRİTİK** güvenlik/veri kaybı · **YÜKSEK** müşteriye zarar ·
ORTA veri/kullanım bozukluğu · DÜŞÜK kalite borcu.
Kök: yukarıdaki K1–K7. Konum ve kanıt için alan belgesine bakınız.

| # | Alan | Önem | Kök | Bulgu |
| --- | --- | --- | --- | --- |
| **E-1** | Employees | **ÇÖZÜLDÜ 2026-08-28** | K3 | Pasifleştirmede mevcut/yeni oturum ve refresh erişimi kesiliyor; kalıcı süreç kapsamı E-5'te sürüyor |
| **P-1** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | — | Sıfır/negatif net uyarılıyor, `Eksik Veri` kalıyor ve onayda 409 alıyor |
| **P-2** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | K2 | Kümülatif matrah kritik yolların tamamında onaylı önceki dönemlerden türetiliyor |
| **P-3** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | K2 | Geri dönem ve son dönem final olmadan ileri dönem açılması 409 |
| **P-4** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | K3 | Etkin ayrılış tarihiyle son ay bordroya giriyor; sonraki ay ve tarih bilinmeyen eski pasifler dışarıda |
| R-1 | Recruitment | **YÜKSEK** | K4 | İşe alım tarihi hiç doğrulanmıyor (1990 ve 2099 kabul) |
| R-2 | Recruitment | **YÜKSEK** | K2 | Geçmiş tarihli işe alımda cari yıl izin bakiyesi oluşmuyor |
| R-3 | Recruitment | **YÜKSEK** | K5 | 24 günlük hak ediş sabit kodlu — mevzuata aykırı görünüyor |
| **R-6** | Recruitment | **ÇÖZÜLDÜ 2026-08-31** | — | İş verisi atomik; Identity/davet kalıcı, idempotent, otomatik ve manuel retry destekli |
| L-1 | Leaves | **YÜKSEK** | K2 | Yıl devri hiç yok — her 1 Ocak'ta modül kilitleniyor |
| L-2 | Leaves | **YÜKSEK** | K2 | Yıl sınırını aşan izin tamamen başlangıç yılından düşülüyor |
| A-1 | Attendance | **YÜKSEK** | — | Gece vardiyası kaydedilemiyor (22:00→06:00 reddediliyor) |
| **A-2** | Attendance | **ÇÖZÜLDÜ 2026-08-28** | — | Beklenen iş günleri puantaj/onaylı izinle sunucuda hesaplanıyor; istemci bayrağı yok |
| A-3 | Attendance | **YÜKSEK** | K5 | Fazla mesai eşiği günlük 8 saat sabit; haftalık 45 saat yok |
| **E-2** | Employees | **ÇÖZÜLDÜ 2026-08-31** | K3 | Halef zorunlu; ekip/onay kapsamı ve dayanıklı yönetici rolü atomik niyetle devrediliyor |
| **X-1** | Çapraz | **ÇÖZÜLDÜ 2026-08-28** | K4 | Geçersiz enum alan bazlı 400; merkezi güvenlik ağı ayrıntı sızdırmayan 400 döndürüyor |
| **P-5** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | — | Sıfır brüt uyarılıyor, `Eksik Veri` kalıyor ve onayda 409 alıyor |
| P-6 | Payroll | ORTA | K4 | 2099 gibi uzak gelecek dönemi açılabiliyor |
| P-7 | Payroll | ORTA | K2 | Maaş her dönem elle giriliyor; önceki dönemden devir yok |
| **P-8** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | K2 | `PreviousTaxBase` istemciden kaldırıldı ve onaysız satırlarda canlı türetiliyor |
| **P-9** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | — | Satır ve dönem onay durumu açıkça filtreleniyor |
| **P-10** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | K7 | Ayar/vergi dilimleri ve çalışan satırları ayrı sorgulanıyor; kartezyen çarpım yok |
| **P-12** | Payroll | **ÇÖZÜLDÜ 2026-08-28** | K6 | Tüm dönem mutasyonları ortak dağıtık kilitte; çift onay/gönderimde tek başarı |
| R-4 | Recruitment | ORTA | — | Tek kelimelik isimde soyad boş kalıyor |
| R-5 | Recruitment | ORTA | K1 | İşe alım geri alınamıyor |
| R-7 | Recruitment | ORTA | K7 | `GetCandidateQuery` 6 koleksiyon `Include` — kartezyen |
| R-9 | Recruitment | ORTA | — | `notes` ve `evaluations` uçlarının hiç testi yok |
| L-3 | Leaves | ORTA | K1 | Onaylanmış izin iptal edilemiyor — bakiye kilitli |
| L-4 | Leaves | ORTA | K4 | İzin talebinde vekil olarak kendisi seçilebiliyor |
| L-5 | Leaves | ORTA | K4 | Geçmiş tarihli talep doğrulanmıyor (bugün tesadüfen engelleniyor) |
| L-6 | Leaves | ORTA | K2 | `CarriedOverDays` hiç hesaplanmıyor, her yerde `0` |
| L-7 | Leaves | ORTA | K6 | İzin onayında bakiye yarışı (TOCTOU) |
| A-4 | Attendance | ORTA | K5 | Ara dinlenme serbest — `break=0` fazla mesaiyi ikiye katlıyor |
| A-5 | Attendance | ORTA | K4 | Gelecek tarihli puantaj girilebiliyor (2030 kabul) |
| A-6 | Attendance | ORTA | K5 | Mesai başlangıcı ve erken eşiği sabit kodlu (09:00 / 08:45) |
| A-7 | Attendance | ORTA | K1 | Onaylanmış bordro dönemine ait puantaj sonradan değişebiliyor |
| **E-3** | Employees | **ÇÖZÜLDÜ 2026-08-31** | K3 | Puantaj yalnız işe giriş–etkin ayrılış tarih aralığında kabul ediliyor |
| E-4 | Employees | ORTA | K4 | Geçersiz TC kabul ediliyor — yalnız `^\d{11}$` kontrolü |
| E-5 | Employees | ORTA | K3 | İşten çıkış tek alanlık güncelleme, süreç değil (**E-1'in kökü**) |
| D-1 | Departments | ORTA | — | Açık pozisyonu olan departman silinebiliyor; pozisyon sahipsiz kalıyor |
| C-1 | Compliance | ORTA | K2 | `DueDate` hiç kullanılmıyor; süresi geçen belge risk sayılmıyor |
| C-2 | Compliance | **ÇÖZÜLDÜ 2026-08-28** | K4 | Durum/seviye komutları ve filtreleri FluentValidation kapsamında |
| N-1 | Actions | **ÇÖZÜLDÜ 2026-08-28** | K4 | Öncelik/durum komutları ve filtreleri FluentValidation kapsamında |
| P-11 | Payroll | DÜŞÜK | K5 | SGK kontrol kartında `* 0.95m` sihirli sayısı |
| R-8 | Recruitment | DÜŞÜK | — | Pozisyon oluştururken departman iki kez sorgulanıyor |
| R-10 | Recruitment | DÜŞÜK | — | `AuthorName` denormalize; kullanıcı adı değişince not eski adı taşır |
| L-8 | Leaves | DÜŞÜK | — | `RequireEmployeeId` yüzünden hr-admin kendi izin uçlarını kullanamıyor |
| A-8 | Attendance | DÜŞÜK | — | Puantaj tipi hem istemciden alınıp hem sunucuda türetiliyor |
| E-6 | Employees | DÜŞÜK | — | `BulkDeactivate` bulunamayan id'leri sessizce atlıyor |

---

## 5. ÖD'nin kendisi hakkında iki gözlem

**Test sayısı kalite göstergesi değil.** En iyi kapsanan alan Employees'ti
(38 test) ve **tek KRİTİK bulgu oradan çıktı**. Testler uç nokta düzeyinde
yazıldığı için her uç tek başına doğru cevap veriyor; "işten çıkış" uçlar arası
bir süreç olduğu için kimsenin sorumluluğuna girmemiş.

**Bulguların çoğu okumayla değil çalıştırmayla bulundu.** E-1, P-1, L-1, A-1,
X-1 — hepsi canlı API'ye istek atılarak yakalandı. Kod okuması bunların hiçbirini
vermezdi; her biri "kod doğru görünüyor ama davranış yanlış" sınıfında. Katman
1'in çalıştırmayı zorunlu kılması bu yüzden pazarlık konusu değil.
