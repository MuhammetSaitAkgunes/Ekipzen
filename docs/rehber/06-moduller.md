# 6. Modüller — Derinlemesine

Bu bölüm en çok iş kuralı içeren dört modülü ayrıntılı anlatır. Diğer modüller
(personel, puantaj, ayarlar, dashboard) aynı desenleri izler; kavradıktan sonra
kolayca okunur.

## 6.1 Bordro — projenin tacı

Bordro, "brüt maaştan yasal kesintiler düşülüp net maaşın bulunması"dır. Türkiye
4/a (işçi) kurallarına göre hesaplanır. İki parça vardır: **motor** (saf hesap)
ve **yaşam döngüsü** (dönem yönetimi).

### 6.1.1 Bordro motoru — saf fonksiyon

`backend/src/IKPro.Domain/Services/PayrollEngine.cs`. Girer `PayrollInput`
(brüt, çalışılan gün, fazla mesai, ek ödemeler, önceki kümülatif matrah) +
`PayrollParameters` (SGK oranları, taban/tavan, vergi dilimleri, istisnalar);
çıkar `PayrollCalculation` (saatlik ücret, brüt kazanç, SGK, gelir/damga vergisi,
net ücret, işveren maliyeti, uyarılar).

Hesabın özü (kod, sadeleştirilmiş):

```
saatlik ücret   = brüt / aylık çalışma saati
fazla mesai     = saatlik × fazla mesai saati × çarpan
brüt kazanç     = (brüt × çalışılan gün / varsayılan gün) + fazla mesai + ek ödemeler
SGK matrahı     = clamp(brüt kazanç, SGK taban, SGK tavan)   ← tavan üstü kesilmez
SGK/işsizlik    = SGK matrahı × oran
gelir vergisi   = dilimli hesap (kümülatif matrah üzerinden) − asgari ücret istisnası
damga vergisi   = brüt × oran − damga istisnası
net             = brüt kazanç − tüm kesintiler
işveren maliyeti= brüt kazanç + işveren SGK + işveren işsizlik
```

**Neden bazı detaylar böyle:**
- **Kümülatif matrah + dilimli vergi:** Türkiye'de gelir vergisi artan oranlıdır
  ve yıl içinde birikir. Motor "yıl başından bu yana toplam matrah"a göre dilimi
  bulur; bu yüzden `PreviousTaxBase` girdisi var. Yeni dönem oluşturulurken bu
  değer, aynı yılın önceki onaylı döneminden **devralınır**.
- **SGK tavanı (`clamp`):** Prime esas kazancın bir üst sınırı vardır; onun
  üstündeki maaştan SGK kesilmez. `Math.Min(..., tavan)` bunu sağlar. Tavana
  yaklaşınca "SGK tavan kontrolü" uyarısı üretilir.
- **Neden motor Domain'de ve saf?** Hiçbir DB/HTTP yok → yüzlerce parite testi
  (eski JS çıktısıyla birebir eşleşme) mock'suz yazılabiliyor. Bordro yanlış
  hesaplarsa şirket ceza öder; bu yüzden en çok test edilen yer burasıdır.

### 6.1.2 Bordro yaşam döngüsü — dönem durumları

`Features/Payroll/Periods/`. Bir bordro dönemi şu durumlardan geçer:

```
Draft (oluştur) → Control (kontrol edildi) → Approved (dönem onaylandı) → Closed
```

- **Oluştur** (`CreatePayrollPeriodCommand`): aktif her personel için bir satır
  üretir; kümülatif matrahı önceki dönemden, fazla mesai saatini puantaj
  view'ından, IBAN durumunu profilden çeker. Maaş girdisi İK tarafından doldurulur.
- **Girdi güncelle** (`UpdatePayrollRowCommand`): satırın maaş/ek ödemelerini
  değiştirir; IBAN/puantaj eksikse durumu "Eksik Veri" yapar.
- **Kontrol** (`RunPayrollCheckCommand`): tüm satırları "Onaya Hazır" / "Eksik
  Veri" olarak işaretler; dönem "Control"a geçer.
- **Satır onayı** (`ApprovePayrollRowCommand`): motoru çalıştırır ve sonucu
  `PayrollResult` olarak **dondurur (snapshot)**. Onaylı satır artık canlı
  hesaplanmaz, snapshot'tan okunur.
- **Dönem gönder** (`SubmitPayrollPeriodCommand`): tüm satırlar onaylıysa dönemi
  "Approved" yapar.

> **Neden onayda snapshot?** Onaydan sonra ayar (parametre) değişse bile onaylanmış
> maaş **değişmemeli** — bordro yasal bir belgedir. Snapshot, "o an ne hesaplandıysa
> o kalır" garantisini verir. Onaysız satırlar ise canlı (parametre değişince
> yeniden) hesaplanır.
>
> **Koruma (guard):** `PayrollRowGuards.EnsurePeriodOpen` — onaylanmış/kapatılmış
> dönem üzerinde değişiklik 409 döner. Bu, "geçmiş bordroyu kurcalama" kuralıdır.

### 6.1.3 Pusula PDF

`Features/Payroll/Payslips/`. Onaylı bir satırın pusulası QuestPDF ile üretilir.
hr-admin herkesinkini, çalışan yalnız kendisininkini indirebilir (rol kontrolü
handler'da).

## 6.2 İzin — talep, onay, bakiye

`Features/Leaves/`. Akış: çalışan talep oluşturur → (onay gerektiren tipse)
manager/hr-admin onaylar → bakiye view'ı güncellenir.

**Kritik kurallar:**
- **İş günü hesabı** SQL fonksiyonuyla (hafta sonu + resmi tatil düşülür).
- **Çakışma:** aynı tarihe ikinci bekleyen/onaylı talep 409.
- **Bakiye:** yıllık (düşümlü) izin için hak edişi aşan talep 409.
- **Onay yetkisi:** manager yalnız ekibini karara bağlar; kimse (hr-admin hariç)
  kendi talebini onaylayamaz.

> **Öğrenilen ders (Bölüm 8.7 #2):** Bakiye view'ı yalnız **onaylı** izinleri
> "kullanılmış" sayar. Bu yüzden çakışmayan birçok **bekleyen** talep tek tek
> bakiye içinde açılıp toplu onaylanınca bakiye negatife düşebiliyordu. Düzeltme:
> (a) oluştururken bekleyen düşümlü günleri de hesaba kat, (b) onay anında bakiyeyi
> yeniden doğrula. Ortak mantık `LeaveGuards.EnsureBalanceAsync`'te.

## 6.3 Puantaj (Mesai) — bordronun girdisi

`Features/Attendance/`. Canlı pano, aylık puantaj tablosu, manuel giriş, CSV.
Aylık toplamlar (`vw_MonthlyAttendanceSummary`) bordro dönemine fazla mesai
saati olarak **beslenir** — modüller arası en net bağ budur.

## 6.4 İşe Alım — adaydan personele

`Features/Recruitment/`. Aday havuzu → mülakat notları/değerlendirme → pipeline
geçişleri (Yeni→Mülakat→Teklif) → **işe al**.

**"İşe al" bir dönüşümdür** (`HireCandidateCommand`): aday `Candidate`'ten bir
`Employee`'ye çevrilir. Tam provizyon (Bölüm 8.7 #3 düzeltmesinden sonra):
1. `Employee` kaydı (departman, ünvan, işe giriş tarihi),
2. **login hesabı** (`ApplicationUser`, rol employee, geçici şifre) — işe alınan
   kişi sisteme girebilsin,
3. **izin bakiyesi** (giriş yılı için 24 gün) — izin akışına tam katılabilsin,
4. aday "İşe Alındı" olur, pozisyon kontenjanı düşer (biterse ilan kapanır).

> **Neden tam provizyon?** Eskiden hire yalnız `Employee` açıyordu; işe alınan
> kişi login olamıyor, bakiyesi 0 kalıyordu — yarım bir akış. "Kullanıma hazır
> ürün" gözüyle, işe alım tek adımda çalışan bir personel üretmeli.
>
> **Neden e-posta hire'da isteniyor?** Aday kaydında e-posta yok; login hesabı
> için bir e-posta gerekir. Bu yüzden işe alım penceresi "iş e-postası" alır.

## 6.5 Uyum — evrak ve denetim hazırlığı

`Features/Compliance/`. Personel evraklarının durumunu (Eksik/İncelemede/Süresi
Yaklaşıyor/Tamamlandı) ve bir **denetim hazırlık skorunu** takip eder. Okuma
hr-admin + manager (manager yalnız ekibi); mutasyonlar yalnız hr-admin.

Hazırlık skoru bir formülle hesaplanır: tamamlanma oranı + eksik/yaklaşan evrak
cezaları. hr-admin şirket geneli view'ı, manager kendi kapsamından hesaplananı
görür (Bölüm 8.7 #4 düzeltmesi).

## 6.6 Aksiyon Merkezi + Denetim İzi + Arama

`Features/Actions/`. Üç işi bir arada:
- **Global aksiyonlar** — modüller arası "yapılacaklar" (öncelik/kaynak/sahip
  filtreli); durum ileri yönlü `open→week→done` (geri dönüş 409).
- **Denetim izi** (`audit-logs`) — kritik işlemlerin değiştirilemez kaydı (yönetsel).
- **Birleşik arama** (`search`) — personel (rol kapsamlı) + aksiyon + aday.

> **Neden aksiyon durumu tek yönlü?** "Tamamlandı" işaretlenen bir işi tekrar
> "açık"a çekmek denetim izini karıştırır; ileri yönlü akış operasyonel disiplini
> korur.

---

Önceki: [← 5. Frontend](05-frontend.md) · Sonraki: [7. Geliştirme Akışı →](07-gelistirme-akisi.md)
