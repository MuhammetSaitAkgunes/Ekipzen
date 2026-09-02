# E-5C2 — İşten Çıkış Hesap Taslağı Güvenlik Sınırı

**Uygulama tarihi:** 2026-09-02  
**Durum:** Yazılım temeli tamam; üretim kuralı için yetkili uzman onayı zorunlu

## Amaç

Kıdem, ihbar ve kullanılmamış izin tutarlarını “uygulamanın verdiği hukuki karar”
gibi sunmadan, yeniden üretilebilir bir hesap taslağına dönüştürmek. Harici uzman
girişi mevcut ve desteklenen alternatif yol olarak korunur.

## Güvenlik zinciri

1. HR-admin, tarih etkili kuralı **taslak** olarak oluşturur.
2. Kaynak ve uzman inceleme referansı kaydedilmeden taslak oluşturulamaz.
3. İnsan onayı kuralı değişmez hale getirir; düzeltme yeni yürürlük tarihli sürümdür.
4. Vaka bazında kıdem ve ihbar hak edişleri ayrı kutularla açıkça teyit edilir.
5. Sunucu, ayrılış tarihinde yürürlükteki son onaylı sürüm ve vaka snapshot'larıyla
   yalnız **hesap taslağı** üretir.
6. Tutarlar, kural kimliği/sürümü, üreten kullanıcı ve zaman damgasıyla vakaya bağlanır.
7. Ayrı “İşten çıkışı tamamla” eylemi nihai insan kontrol sınırıdır.
8. Kural yoksa veya uygunluk teyit edilmediyse sistem hak ediş icat etmez; harici
   uzman yolu kullanılabilir.

## Hesap girdileri

- Açık istihdam döneminin başlangıcı ve vaka ayrılış tarihi
- Sözleşme türü ve ihbar uygulaması
- Ayrılış tarihinde geçerli ücret paketi snapshot'ı
- Aylık brüt, düzenli prim, yol, yemek ve diğer düzenli yan haklar
- Kullanılmamış izin günü
- Tarih etkili uzman onaylı kural: günlük bölen, yıl başına kıdem günü, asgari
  hizmet süresi, kıdem tavanı, ihbar eşikleri/günleri ve giydirilmiş ücrete dahil
  bileşenler

Tutarlar iki ondalık haneye `AwayFromZero` yöntemiyle yuvarlanır. Kıdem ve ihbar
yalnız vaka bazlı açık teyit verilirse hesaplanır; belirli süreli sözleşmede veya
“çalışana ödeme” dışındaki ihbar uygulamalarında ihbar ödemesi sıfırdır.

## Resmî dayanak araştırması

Alanların ve sürümleme ihtiyacının belirlenmesinde aşağıdaki T.C. Çalışma ve
Sosyal Güvenlik Bakanlığı kaynakları kullanıldı:

- [4857 sayılı İş Kanunu](https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf), m.17 ve m.59
- [İş Kanunu sıkça sorulan sorular](https://www.csgb.gov.tr/sikca-sorulan-sorular/calisma-genel-mudurlugu/%C4%B1s-kanunu/)
- [1475 sayılı İş Kanunu — kıdem düzenlemesi](https://www.csgb.gov.tr/Media/fqed1ewm/1475-say%C4%B1l%C4%B1-i%C5%9F-kanunu-k%C4%B1dem-tazminat%C4%B1na-ili%C5%9Fkin-yasal-d%C3%BCzenleme.pdf)
- [Kıdem tazminatı tavan miktarı](https://www.csgb.gov.tr/%C4%B1statistikler/calisma-hayati-%C4%B1statistikleri/kidem-tazminati-tavan-miktari/)

Bu kaynak araştırması yetkili hukuk/mali müşavir görüşü değildir. Uygulama bu
nedenle hiçbir kuralı seed ile onaylı getirmez ve sabit kıdem tavanı önermez.

## Otomatik kanıtlar

- Saf motor testleri: tavan, giydirilmiş ücret, izin, ihbar kademeleri, uygunluk
  teyidi ve belirli süreli sözleşme koruması
- Entegrasyon testi: HR-admin yetkisi, taslak kuralın hesapta kullanılamaması,
  onaylı sürümün değişmezliği, kural/vaka izi ve üretilen tutarlar
- Frontend: üretim derlemesi, lint ve tam Vitest paketi

## Kalan dış bağımlılık

Üretimde kullanılacak ilk kural sürümü yetkili uzman tarafından doldurulup
onaylanmalı; gerçek vaka örnekleri beklenen sonuçlarıyla regresyon testine
dönüştürülmelidir. Bu tamamlanana kadar özellik “satış demosuna hazır, hukuki
üretim kullanımına kapalı” kabul edilir.
