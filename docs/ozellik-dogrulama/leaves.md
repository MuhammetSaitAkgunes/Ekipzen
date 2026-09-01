# Leaves (İzin) — Özellik Doğrulama

**Son doğrulama:** 2026-09-01 · **Kapsam:** izin bakiye/defter sorguları,
talep oluşturma/onay/red/iptal, ekip ve yönetici kapsamı · **Modül testi:** 12

> Mevzuat hükmü müşavirindir. Yazılım; hak ediş politikasını istihdam döneminde
> saklar, gerçekleşen hak/kullanım/iade hareketlerini silmeden muhasebeleştirir.
> Bugünkü 24 günlük şirket politikası yasal hesaplama iddiası değildir.

## Güncel özet

| # | Önem | Bulgu | Durum |
| --- | --- | --- | --- |
| L-1 | YÜKSEK | Yeni yıl bakiyesi/devir oluşmuyordu | ✅ İstihdam dönemi defteri ve yıldönümü hak edişiyle çözüldü |
| L-2 | YÜKSEK | Yıl aşan izin başlangıç yılından düşüyordu | ✅ İş günü kullanımı takvim yılı parçalarına ayrılıyor |
| L-3 | ORTA | Onaylı izin iptal edilemiyordu | ✅ Silme yerine ters kayıtla iade ediliyor |
| L-4 | ORTA | Personel kendisini vekil seçebiliyordu | ✅ Kendisi ve pasif personel reddediliyor |
| L-5 | ORTA | Geçmiş tarihli talep için açık politika yok | ⬜ Ürün/mevzuat kararı bekliyor |
| L-6 | ORTA | Devir alanı anlamsız ve daima sıfırdı | ✅ Önceki tüm defter hareketlerinden hesaplanıyor |
| L-7 | ORTA | Eşzamanlı onayda bakiye aşılabiliyordu | ✅ Personel bazlı veritabanı kilidiyle serileştirildi |
| L-8 | DÜŞÜK | Personel bağı olmayan hr-admin kendi iznini kullanamıyor | ⬜ Bilinçli ürün kararı olarak açık |

## E-5C1 ile kurulan doğruluk modeli

- Bakiye artık çalışan + takvim yılı satırı değil, bir
  `EmployeeEmploymentPeriod` kapsamındaki imzalı `LeaveLedgerEntry`
  hareketlerinin toplamıdır.
- `ServiceAnniversaryEntitlement` çalışma yıldönümünde idempotent oluşur.
  Gelecekteki hak ediş, daha erken tarihli bir talebi finanse edemez.
- `Usage` ve `UsageReversal` geçmişi silmez; düzeltmeler denetlenebilir kalır.
- Bekleyen düşümlü talepler rezervasyon olarak ayrıca hesaba katılır. Oluşturma
  ve onay aynı dağıtık kilit altında tekrar kontrol edilir.
- Yıl sınırını aşan izin, `IWorkingDayCalculator` ile yıl bazında parçalanır.
  Devir ayrı ve kırılgan bir kolon değil, önceki hareketlerin net toplamıdır.
- Yeni işe alınan çalışana peşin 24 gün yazılmaz; ilk hak ediş yıldönümüdür.
- Kontrollü yeniden işe alım yeni bir istihdam dönemi açar. Eski izin defteri ve
  ücret paketi yeni döneme kendiliğinden sızmaz.
- Eski canlı veriler migration sırasında istihdam dönemlerine bağlanır:
  `LeaveBalance` değerleri geçiş açılışına, onaylı kullanımlar ayrı kullanım
  hareketlerine çevrilir. Bağlanamayan talep varsa migration sessiz veri kaybı
  yerine durur.
- Kişisel veri dışa aktarımı artık eski bakiye satırı yerine izin defterini verir.
- React ekranı hak ediş, bekleyen rezervasyon, sıradaki yıldönümü ve hareket
  defterini gösterir; pasif personel için ayrı çalışma dönemi açma akışı vardır.

## Açık kararlar ve sınırlar

### L-5 · Geçmiş tarihli izin

Geçmişe dönük talebin tamamen kapatılması, bordro/puantaj düzeltmesi için belirli
bir pencere tanınması veya yalnız İK rolüne açılması bir ürün ve mevzuat
kararıdır. Bugünkü istihdam dönemi ve kronolojik bakiye kontrolleri tesadüfi
kabulleri azaltır; ancak açık bir tarih alt sınırı hâlâ yoktur.

### L-8 · Personel bağı olmayan İK hesabı

`RequireEmployeeId` personel kaydı olmayan yönetim hesabını kendi izin
uçlarından bilinçli olarak uzak tutar. Gerçek müşteride İK kullanıcısının aynı
zamanda çalışan olup olmayacağı onboarding kararıyla netleşmelidir.

### Hak ediş politikası

İstihdam dönemi `AnnualLeaveEntitlementDays` snapshot'ını taşır ve mevcut
geçiş varsayımı 24 gündür. Kıdem, yaş, sözleşme/iş kolu ve tarih etkili mevzuat
kurallarından otomatik hesap henüz yapılmaz. Bu, uzman onaylı sürümlü kural
motorunun girdisidir; E-5C1 muhasebe altyapısını kurar, hukuki görüş üretmez.

## Otomatik kanıt

- Yıl aşan kullanım ve onaylı iptal iadesi
- Gelecek talepte yıldönümü hak edişinin yalnız bir kez oluşması
- Bekleyen taleplerin bakiyeyi aşamaması
- Çakışma, hafta sonu/resmi tatil, kapsam ve sahiplik kontrolleri
- Kontrollü yeniden işe alımda iki ayrı dönem ve eski bakiye sızıntısının olmaması
- Frontend bakiye/defter görünümü ve pasif personel işe dönüş isteği

## Sıradaki adım

E-5C1 tamamlandıktan sonra otomatik kıdem/ihbar taslak hesaplayıcısına geçilebilir.
Önce uzman onaylı tarih etkili kural sürümü ve beklenen altın senaryolar
tanımlanmalıdır. L-5 ve L-8 bu paketten bağımsız ürün kararları olarak açık kalır.
