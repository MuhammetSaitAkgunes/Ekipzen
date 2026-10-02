# Ekipzen — T03 izin türü, talep ve iptal öğrenmesi

2 Ekim 2026. Sabit olağan kapsamın L01/L02 satırları; yeni kapsam satırı eklenmez. T01/T02'nin onaylı altı commiti sırasıyla ekipzen remote'una/main'e fast-forward senkronize edildi. Bu paketin tabanı `0806d68d85e9ed594cf9723074b3ce2ea27da906`.

## Ürün akışı

İzinlerim ekranı tür/koşul → ön kontrol → gerçek talep → sonuç → gerektiğinde iptal/iade sırasını ve kişisel işlem geçmişini gösterir. Başlangıç şablonunun Yıllık, Mazeret, Raporlu ve Uzaktan kodları kendi belge/politika kontrolü, örneği ve resmî kaynağına bağlanır. Özel/bilinmeyen kodda adından hüküm çıkarılmaz; İK sorumlusuyla koşul doğrulaması istenir. Bayraklar şirketin uygulama davranışıdır, hukuki uygunluk onayı değildir.

Yeni merkezî `leave/request-lifecycle` ile 10 süreç/39 ders taslağı vardır. Çalışan, yönetici ve İK yöneticisi kendi talebini mevcut yetkisiyle oluşturur/iptal eder. Yönetici inceleme/karar dersleri çalışanlara açılmaz. Ders taslağı gerçek uzman ve bağımsız Ekipzen editör yayını olmadan katalogda okura açılmaz. Talep ve iptal ayrı, açık seçilmiş güncel dersle kişisel kanıt üretir. Kaynak seçimi son kontrolü kaldırır; yeniden inceleme gerekir. Kaynaksız geçmiş sonradan damgalanmaz. Okuma/alıştırma/önizleme gerçek işlem veya yeterlilik değildir.

## API ve tutarlılık

- POST leaves/preview salt okur; başarılı sonuç `recordToken` içerir: kişi/dönem/tür/kod/ad, onay-bakiye bayrakları, tarihler, iş günü, vekil kimliği. POST leaves opsiyonel expectedRecordToken/guidanceContentRevisionId alır. Rehberli yazımda belirteç zorunlu; eski belirteç/kaynak 409. Çakışma/dönem/vekil/bakiye/rezervasyon kişi kilidi altında tekrar doğrulanır. Token bakiye rezervasyonu veya gönderim makbuzu değildir.
- GET leaves/{id}/cancellation-preview yalnız kendi talebinde durum/canCancel/releasedReservationDays/refundedDays/recordToken döndürür. POST cancel sorgusunda aynı iki opsiyonel alan vardır. Personel bağı yoksa 403; başka kişinin talebi 403; bağlı personelin başka tenantta olmayan kimliği 404. Kaynaksız eski boş gövdeli iptal çalışır.
- İptal tokeni durum/tarih/tür/dönem ve gerçek defter hareketlerine bağlıdır; onay veya başka değişiklik eski önizlemeyi geçersiz kılar. Aynı kişi kilidiyle eşzamanlı iki iptalden yalnız biri yazılır; diğeri 409.
- Bulunan iade kusuru düzeltildi: değişmiş tatil takviminden yeni gün hesabı yerine kaydedilmiş kullanım hareketi aynı dönem/tarih/günle terslenir. Eski kullanım silinmez; yıl bazlı kaynak anahtarı tekilliği korunur. Tür bayrağı değişse de gerçekleşmiş kullanım iadesi defterden gelir. Puantaj/bordro/ayrılış/dış bildirim otomatik düzeltilmez.
- İş kaydı, kullanım/iade ve kaynaklı kanıt aynı tenant transaction'ındadır. Kanıt hatasında talep/iptal/hak ediş/kullanım/iade geri alınır. Talep bildirimi commit sonrası mevcut yolda yürür; başarılı talep teslim kanıtı değildir.
- GET leaves/learning-operations oturum sahibinin yalnız talep/iptal anahtarlarını döndürür. Genel yönetim kanıt uçlarının yetkisi genişletilmez; başka kullanıcının kanıtı veya izin açıklaması yanıtta yoktur. Kişisel dışa aktarım aynı kanıtları içerir; İK/yönetici genel yolculuğu da kendi kayıtlarını görür.
- Yeni migration gerekmez; mevcut GuidancePreparationEvidences kullanılır. Katalog kilidi tenant/kişi kilidinden öncedir.

## Günlük işler, devam ve sınırlar

Bekleyen talep mevcut yönetici onay işini doğurur; karar/iptal gerçek durumdan kapatır. Yapay talep/iptal görevi açılmaz. İptal bekleyen rezervasyonu kaldırır veya onaylı gerçek kullanımı iade eder. Yanlış tür/tarih için iptal ve ayrı yeni talep yolu açıklanır.

409/ağ hatasında form ve ders korunur; hazır son kontrol kaldırılır. Önizleme açıkça yeniden alınır ve tekrar onay gerekir. Belirsiz ağ sonucunda gerçek liste/defter okunur; otomatik gönderim makbuzu ve yenileme/cihazlar arası form geri yükleme T06'da açıktır. Çalışan dersini ayrıntılı okur; yönetimde mevcut kısa/uyarlanan anlatım korunur. Çalışanın yönetim tercih API'sine çağrı yapılmaz.

Şirketin 24 günlük hak edişi, sabit hafta sonu/tam gün tatil hesabı, önceki hizmet, yarım gün, izin bölünmesi ve özel tür koşulları tam hukuki uygunluk denetimi değildir. Derste somut kontrol/uzman yönlendirmesi vardır; kullanılmış izin/hesap/dış işlem etkisi özel inceleme gerektirir. Hiçbir özellik sessizce feragat edilmiş sayılmaz.

## Araştırma dayanakları

2 Ekim'de resmî kaynaklardan kontrol edildi; insan uzman incelemesi değildir:

- [4857 İş Kanunu](https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf): m.53–56 yıllık izin, ek m.2 mazeret, m.14 uzaktan çalışma.
- [Yıllık Ücretli İzin Yönetmeliği](https://www.csgb.gov.tr/Media/trddpibj/y%C4%B1ll%C4%B1k-%C3%BCcretli-izin-y%C3%B6netmeli%C4%9Fi.pdf): m.6–9 kullanım/talep, m.20 kayıt.
- [SGK hastalık hali](https://www.sgk.gov.tr/Content/Post/e33fffdb-16b9-4722-a9c1-2bd0a2a220d1/Hastalik-Hali-2025-02-27-11-27-26): rapor ve dış SGK koşulları.
- [Uzaktan Çalışma Yönetmeliği](https://www.csgb.gov.tr/Media/cyxd4g3n/uzaktan-%C3%A7al%C4%B1%C5%9Fma-y%C3%B6netmeli%C4%9Fi.pdf): çalışma biçimi/sözleşme.

## Teknik doğrulama ve kapanış

Odaklı SQL: 16 geçti; gerçek rezervasyon/onay/iade, takvim değişikliği, eski önizleme, eşzamanlı iptal, rol/tenant/kişisel kanıt, kaldırılan ders ve kanıt hatasında geri alma. Bütün backend: 344 SQL entegrasyon ve 101 birim testi geçti. Odaklı frontend: 26 geçti; kaynak değişiminde yeniden ön kontrol, 409'da taslak/sürüm korunması ve çalışan okumasında yönetim API'sine gitmeme. Bütün frontend `--maxWorkers=2`: 98 dosya/586 test geçti; son iptal sonuç etiketi sonrası 15 ortak kanıt testi tekrar geçti.

Release derlemesi 0 uyarı/0 hata; frontend derlemesi başarılı, lint yalnız 4 mevcut uyarı. OpenAPI yenilendi, AppDbContext pending-model kontrolü değişiklik yok, diff-check temiz. Tarayıcı prova verisiyle talep ve iptal kaynak seçimi/ön kontrol/409 korunması, 390 ve 1440 genişlikte taşmama, Tab odağının diyalogda kalması ve Escape ile kapanma doğrulandı. Bu prova insan/uzman testi veya canlı API kabulü değildir. Yeni kullanıcı veya gerçek uzman testi yapılmadı; üretime dosya/migration gönderilmedi.

T03 kapanışıyla **45 satır: 29 bağlı, 7 okuma, 9 açık/kısmi**; kalan **T04–T08, toplam 5**. Sıradaki T04 puantaj/bordro/ayrılış bağlantılarıdır. Uzman/kullanıcı/hedef sunucu kabulü ve tam öğrenme kapsamından önce yayın yapmama kararı korunur.
