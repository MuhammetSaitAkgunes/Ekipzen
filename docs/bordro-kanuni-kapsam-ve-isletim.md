# Bordro Kanuni Kapsamı ve İşletim Standardı

**Sürüm tarihi:** 7 Eylül 2026
**Parametre dönemi:** 1 Ocak 2026 ve sonrası için yürürlük tarihli setler
**Hesap profili:** Yurt içi, özel sektör, standart 5510/4-a çalışanı

## Güvence yaklaşımı

İK Pro bordro motoru yalnız açıkça desteklediği çalışan ve ödeme profilinde
hesap üretir. Bir alan sınıflandırılmamışsa veya gerekli kanıt/referans yoksa
satır hesaplanabilir bir ön izleme gösterebilir; ancak **onaylanamaz**. Özel
statüyü standart çalışan gibi varsayarak sessizce yanlış bordro üretmek yasaktır.

Mevzuat setleri yürürlük tarihiyle sürümlenir. Yalnız `approved` setler hesaba
girer; onaylı set yerinde değiştirilemez. Yeni değerler yeni tarihli taslakta
hazırlanır, resmî HTTPS kaynakları ve uzman inceleme notu olmadan onaylanamaz.
Dönem, açıldığı tarihte kullandığı ayar setine bağlanır; satır onaylandığında
hesap sonucu ayrıca değişmez snapshot olarak saklanır.

## 2026 resmî parametreleri

| Alan | Değer |
| --- | ---: |
| Brüt asgari ücret / aylık SGK alt sınırı | 33.030,00 TL |
| Resmî net asgari ücret kontrolü | 28.075,50 TL |
| Aylık SGK üst sınırı | 297.270,00 TL |
| SGK işçi / işsizlik işçi | %14 / %1 |
| İndirimsiz SGK işveren / işsizlik işveren | %21,75 / %2 |
| Diğer sektör / imalat genel indirim puanı | 2 / 5 |
| Damga vergisi | binde 7,59 (`%0,759`) |
| Nakit yemek SGK / gelir vergisi günlük istisnası | 158,00 / 300,00 TL |
| Engellilik indirimi 1. / 2. / 3. derece | 12.000 / 7.000 / 3.000 TL aylık |
| Normal fazla çalışma | saatlik ücretin %50 zamlısı; `aylık ücret / 225 × 1,5` |
| Yıllık fazla çalışma sınırı | 270 saat |

Ücret geliri vergi tarifesi kümülatif matrahta 190.000, 400.000, 1.500.000 ve
5.300.000 TL eşiklerini; sırasıyla %15, %20, %27, %35 ve %40 oranlarını kullanır.
Dilimlerin tabanı ve taban vergisi sunucuda matematiksel süreklilik kontrolünden
geçer.

Asgari ücret gelir vergisi istisnası sabit bir rakam olarak düşülmez. İlgili
ayın asgari ücret kümülatif matrahı üzerinden tarife ile yeniden hesaplanır;
bu nedenle 2026'da dilim geçişiyle aylık istisna değişir. Çok işverenli çalışanda
istisna yalnız tek/en yüksek ücretli işverende seçilir ve aynı seçim gelir ile
damga vergisi istisnasını birlikte yönetir.

## Hesaplanan ve denetim izi bırakılan kalemler

- Çalışılan/SGK prim gününe göre aylık ücret, SGK taban ve tavan oranlaması.
- Standart fazla çalışma, prim, nakit yol, nakit yemek ve tam tabi diğer ödeme.
- Nakit yemek için fiilî gün ve ayrı SGK/gelir vergisi istisna sınırları.
- Çalışan SGK ve işsizlik kesintileri, kümülatif gelir vergisi, damga vergisi.
- Dereceye göre engellilik indiriminin gelir vergisi matrahından düşülmesi.
- Asgari ücret gelir ve damga vergisi istisnaları.
- İndirimsiz, diğer sektör 2 puan ve imalat 5 puan senaryolarında işveren maliyeti.
- Önceki işveren matrahının çalışan talebi + işveren kabulü referansıyla ilk
  bordroya açılış olarak alınması; sonraki aylarda yalnız onaylı snapshot'tan devir.
- Kuruş hassasiyetinde, `AwayFromZero` yuvarlama ve bordro pusulasında ayar
  sürümü/kaynağı, matrahlar, istisnalar ve indirimlerin gösterimi.

## Satır onayını durduran kapılar

- Desteklenen standart 4/a profilinin seçilmemesi.
- Eksik puantaj, IBAN veya SGK prim günü doğrulaması.
- Tam ay aylık ücretin yürürlükteki asgari ücretin altında olması.
- Yemek yardımının sınıfı/günü eksik olması veya yemek gününün prim gününü aşması.
- Fazla çalışmada standart %50 türü, çalışan onayı ve yıllık 270 saat kontrolünün
  doğrulanmaması.
- Diğer ödemenin tam prime ve vergiye tabi olduğunun doğrulanmaması.
- Engellilik durumunun seçilmemesi; indirim uygulanıyorsa belge referansının veya
  yürürlükteki parametrenin eksik olması.
- Engellilik dışındaki kişisel sigorta/sendika vb. gelir vergisi matrah
  indirimlerinin bulunmadığının doğrulanmaması.
- Kümülatif matrahın doğrulanmaması; önceki işveren bakiyesi varsa çalışan
  talebi/işveren kabulü referansının eksik olması.
- SGK işveren indirimi seçiliyken uygunluk referansının bulunmaması.
- Prim günü olmayan ek ödemenin SGK'nın sonraki iki aya dağıtım incelemesinin
  tamamlanmamış olması; sıfır/negatif net; onaysız mevzuat seti.

## Bilinçli kapsam dışı profiller

Aşağıdakiler standart 4/a motoruna sokulmaz; ayrı tarih etkili profil ve altın
testler geliştirilmeden üretim onayı verilemez:

- Emekli/SGDP, stajyer-çırak, kamu, ev hizmetleri, gemi adamı, yabancı/dar
  mükellef ve diğer özel sigortalılık statüleri.
- Ar-Ge, teknoloji geliştirme bölgesi, serbest bölge ve benzeri gelir vergisi
  teşvik/istisnaları.
- Ayni/kart yemek veya ulaşım ile standart nakit sınıflandırmasından farklı yan haklar.
- Hafta tatili, ulusal bayram/genel tatil ve standart %50 fazla çalışmadan farklı
  çalışma türleri.
- Kişisel sigorta, sendika aidatı veya engellilik dışında vergi matrahını azaltan
  kalemler; bunlar var ise mevcut satır fail-closed kalır.
- Genel 2/5 puan dışında işveren teşvikleri.

Bu sınır “eksik hesabı doğru saymak” değildir: ürün yalnız kanıtlanmış profilini
hesaplar ve kalan profilleri açıkça reddeder. Yeni profil eklemek; resmî kaynak,
uzman incelemesi, yürürlük tarihli parametre, migration, API/arayüz
sınıflandırması ve altın senaryo testlerinin tamamını gerektirir.

## Resmî kaynaklar

- ÇSGB, 2026 asgari ücret: https://www.csgb.gov.tr/tr/poco-pages/asgari-ucret/
- SGK, 2026 prime esas kazanç sınırları: https://www.sgk.gov.tr/Content/Post/2e0c9e1a-2cfe-4456-af10-49d3de0c58ba/Prime-Esas-Kazanc-Miktarlari-2026-01-14-10-35-39
- SGK, işveren prim oranları: https://www.sgk.gov.tr/Content/Post/c7812ea8-5087-413f-aeb5-d3c1d153e11a/Isveren-Prim-Oranlari-2026-01-13-04-52-38
- GİB, 2026 ücret tarifesi: https://usak.gib.gov.tr/vergi-konulari/1_bireysel/11_ucret_geliri/11
- GİB, engellilik indirimi: https://gib.gov.tr/vergi-konulari/1_bireysel/8_engelliler_icin_vergi_avantajlari/8
- GİB, 321 sayılı Tebliğ özeti (işveren değişikliği/kümülatif matrah): https://www.gib.gov.tr/duyuru-arsivi/guncel/616_gelir_vergisi_genel_tebliginde_seri_no%3A_311_degisiklik_yapilmasina_dair_teblig_seri_no%3A_321_30.10.2022_tarihli_resmi_gazetede_yayimlanmistir
- GİB, 2026 ücret geliri rehberi: https://intvrg.gib.gov.tr/hazirbeyan/assets/pdf/DUYURU_UNIVERSAL_2026_2026_Ucret_Geliri.pdf
- ÇSGB, fazla çalışma yönetmeliği: https://www.csgb.gov.tr/Media/gekgpqcm/i%C5%9F-kanununa-ili%C5%9Fkin-fazla-%C3%A7al%C4%B1%C5%9Fma-ve-fazla-s%C3%BCrelerle-%C3%A7al%C4%B1%C5%9Fma-y%C3%B6netmeli%C4%9Fi.pdf

## Her mevzuat güncellemesinde zorunlu kontrol

1. Resmî kaynakların yayımlanma ve yürürlük tarihini doğrula.
2. Yeni tarihli taslak set oluştur; eski onaylı seti değiştirme.
3. Oran, sınır, indirim ve vergi dilimi sürekliliğini iki kişiyle incele.
4. Resmî asgari ücret, dilim geçişi, tavan, eksik gün, yemek, engellilik,
   çok işveren ve işveren maliyeti altın testlerini güncelle.
5. Backend build/test, migration fresh-database testi, OpenAPI üretimi,
   frontend test/build ve PDF üretimini çalıştır.
6. Yetkili bordro uzmanı/SMMM kabul kaydından sonra seti onayla.

Yazılım kontrolleri mevzuat uzmanının üretim sorumluluğunu ortadan kaldırmaz.
Ücretli pilot/üretim öncesinde desteklenen profil için bağımsız uzman kabulü ve
gerçek anonimleştirilmiş bordro paralel hesap karşılaştırması zorunludur.
