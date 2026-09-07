# Payroll (Bordro) — Özellik Doğrulama

**Güncelleme:** 7 Eylül 2026
**Desteklenen profil:** Yurt içi özel sektör standart 5510/4-a

## Sonuç

Bordro eski JavaScript prototipine parite üreten yapıdan çıkarıldı. Hesabın tek
kaynağı C# domain motorudur; frontend yalnız sunucu sonucunu gösterir. 2026
resmî tutarları altın testlerle sabitlenmiş, yürürlük tarihli ayar seti ve
satır onay kapıları kurulmuştur.

Teknik doğrulama “Türkiye'deki bütün bordro statüleri destekleniyor” anlamına
gelmez. Standart profil dışındaki statüler ve sınıflandırılmamış ödeme/indirimler
fail-closed kalır. Ayrıntılı sınır:
[`bordro-kanuni-kapsam-ve-isletim.md`](../bordro-kanuni-kapsam-ve-isletim.md).

## Kapanan yüksek riskler

- Sıfır/negatif net, eksik puantaj, eksik IBAN ve sıfır brüt onaylanamaz.
- Dönemler geri sırada veya önceki dönem kapanmadan açılamaz; yarışlar tenant ve
  dönem kapsamlı veritabanı kilidiyle serileştirilir.
- Ay içinde ayrılan çalışan son bordroya girer; sonraki döneme taşınmaz.
- Kümülatif gelir vergisi matrahı yalnız önceki onaylı dönem snapshot'larından
  türetilir. Önceki işveren açılışı ayrı alan ve talep/kabul referansıyla alınır.
- Dönem tam olarak kullandığı onaylı mevzuat setine bağlıdır; bugünkü ayarlar
  geçmiş dönemin ekranını veya onaylı sonucu değiştirmez.
- SGK taban/tavanı eksik gün oranlaması, 2026 kümülatif asgari ücret istisnası,
  nakit yemek istisnaları, engellilik indirimi ve işveren 2/5 puan maliyetleri
  motor ve pusulada denetlenebilir kırılıma sahiptir.
- Çok işveren seçimi gelir ve damga vergisi asgari ücret istisnasını birlikte
  yönetir.
- Ayar seti `Draft → Approved` akışındadır; kaynak ve uzman notu zorunlu,
  onaylı sürüm değişmezdir.

## Kanıt kapsamı

- Resmî net asgari ücret: 28.075,50 TL.
- Asgari ücret istisnasının Ocak/Haziran/Temmuz/Ağustos-Aralık tarife geçişleri.
- SGK alt/üst sınırı ve eksik gün.
- Nakit yemek 158 TL SGK / 300 TL GV günlük sınırı.
- Engellilik indirimi 1./2./3. derece.
- Çok işveren istisnası, 2/5 puan işveren maliyeti ve kuruş yuvarlama.
- Ayar taslak/onay/değişmezlik, dönem yaşam döngüsü, kümülatif devir, yetki,
  eşzamanlı onay/gönderim ve PDF pusula.
- Frontend ayar, tekil hesap, dönem ve satır detay sözleşmeleri.

## Üretim kabul kapısı

Kod ve otomatik test tamamlanması tek başına üretim sertifikası değildir.
Ücretli pilot öncesinde:

1. [`bordro-mevzuat-dogrulama-talebi.md`](../bordro-mevzuat-dogrulama-talebi.md)
   yetkili bordro uzmanı/SMMM tarafından doldurulmalı.
2. Temsilî anonimleştirilmiş gerçek bordrolar bağımsız sistemle paralel
   hesaplanmalı; açıklanamayan kuruş farkı kalmamalı.
3. Üretim migration'ı temiz ve mevcut veritabanı kopyalarında denenmeli.
4. Desteklenen profil ve kapsam dışı statüler sözleşme/onboarding metninde
   açıkça belirtilmeli.

## Kalan ürün adayları

- Standart profil dışındaki SGDP, stajyer/çırak, teşvikli bölge ve diğer özel
  bordro profillerini ayrı motor profilleriyle genişletmek.
- Ayni/kart yemek-ulaşım, kişisel sigorta/sendika gibi ek matrah sınıfları.
- Fazla çalışma yıllık toplamını puantajdan otomatik takip edip 270 saat
  aşımında uyum vakası açmak; kazanılmış ücreti yine ödemek.
- Uzak gelecek dönem açılışı için operasyonel üst sınır.
