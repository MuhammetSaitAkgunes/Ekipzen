# Ekipzen — aday kararları ve işe alma öğrenmesi

2 Ekim 2026. Kullanıcının onayladığı `ba4ea65`, `ea643fd`, `6a29702` commitleri belirtilen kaynak dalına push edildi, `main` üzerine fast-forward birleştirildi ve `main` push edildi. Bu teslim o birleşimden başlar; yeni dalın senkronizasyonu ayrı tek Git onayı gerektirir.

## Sıralı kapsam

1. R05: İlk Yeni kararı dışında Mülakat/Teklif/Red/Yeni arasındaki gerçek aday aşama kararları, açık seçilmiş güncel `recruitment/first-review` kaynağına bağlanır.
2. R08: İşe alma ve mevcut giriş hesabı hazırlığını tekrar isteme, ayrı `recruitment/hire-preparation` dersiyle öğretilir ve iki kişisel işlem kanıtıyla izlenir.

10 süreç korunur; bağımsız işe alma/hesap hazırlığı dersiyle alt adım sayısı 38 olur. Ders taslaktır; gerçek uzman ve ayrı merkezî editör yayımlamadıkça kaynaklı alıştırma/işlem için kullanılabilir yayın değildir. Test yayınları uzman incelemesi sayılmaz.

## Karar geçmişi

- İlk kullanıcı/aday `CandidateReviewEvidence` davranışı ve kaydı korunur. İlk gerçek Yeni kararında yeni ilk kanıt oluşturulduysa ikinci kaynaklı karar kanıtı yazılmaz.
- Sonraki gerçek karar veya daha önce ilk kanıtı bulunan kişinin yeniden Yeni'den geçişi, yeni `CandidateStageDecision` satırına nullable kaynak snapshotı yazar. Anahtar `candidate-stage-decision`; aktör, karar zamanı, aday sürüm aralığı ve gerekçe kendi mevcut karar satırından okunur.
- Eski kaynaksız satır damgalanmaz. İlk kanıt ezilmez; işe alınmış başvuru, eski aday/dayanak sürümü reddedilir. Güncel sürümle aynı aşama no-op olup kanıt üretmez.
- Karar gerekçesi ve seçilen güncel not/değerlendirmelerin karar anındaki içerikleri değişmez. Not düzeltmesi eski kararı yeniden yazmaz. Aşama seçimi aday iletişimi, randevu, teklif teslimi veya işe alma değildir.
- `20261002094555_AddCandidateStageGuidance` yalnız nullable `GuidanceSnapshotJson` ekler. Kaynaklı karar varsa Down 51000 ile geçmiş kaybını durdurur.

## İşe alma ve ayrı hesap isteği

- Hire isteğinde isteğe bağlı `expectedCandidateVersion` ve `guidanceContentRevisionId` vardır. Kaynak seçilmişse incelenen aday sürümü ve açık başlangıç tarihi zorunludur; sürüm aynı aday kilidinde kontrol edilir. Arayüz ilk açıldığı aday sürümünü korur.
- Çalışan/ilk çalışma dönemi, aday hareketi/sürümü, pozisyon etkisi ve kalıcı hesap hazırlığı isteği mevcut akışta saklanır. Kaynak seçilmişse `GuidancePreparationEvidence` üzerindeki `candidate-hire-create` kanıtı aynı tenant transaction'ında eklenir; hata iş kayıtlarını da geri alır.
- Kanıt doğru aday ve oluşan personel/hesap isteği kimliklerini gösterir; kişisel e-posta, not veya puan içeriği ikinci depoya kopyalanmaz. Yeni bir işe alma makbuzu veya güvenli geri alma geliştirilmedi.
- Retry, aynı aday kilidinde okunan durum/deneme sayısı/e-posta ve boş/süresi dolmuş lease koşuluyla SQL'de yeniden kuyruğa alır. Aktif dış teslimatın kilidi kaldırılmaz; tamamlanmış veya arada değişmiş kayıt 409 olur.
- `candidate-provisioning-retry` kanıtı aynı transaction'da kabul edilen isteğe aittir. Sonraki dış hesap/davet işlemi commit sonrasında çalışır. Kanıt tarihsel yeniden istek olarak kalır; sonradan başarılı teslim kanıtı gibi değiştirilmez.
- Kaynaksız eski istemciler uyumludur. Kullanıcının ön kontrolde okuduğu retry durumuna ait ayrı sunucu token'ı ve genel gönderim makbuzu bu pakette eklenmedi; bu sınırlar T06/API kaydında açıktır.

## Öğrenme ve arayüz

Her gerçek yazım kendi kaynak seçimini kullanır. Kaynak seçme ve ön kontrol yazım yapmaz; hata sonrası taslak ve seçilmiş kaynak korunur. Kaynak değiştirmek işe alma ön kontrolünü kaldırır; yeniden teyit gerekir. Hesap yeniden denemesinin e-posta/kaynak özeti ve son onayı ayrıdır.

Yeni ders ne/neden, ayrıntılı örnek, düzeltme, ön koşul, kavram, uzman yönlendirmesi ve sürümlü alıştırma içerir. Personel, hesap hazırlığı, davet teslimi, özlük/ücret ve dış bildirim ayrı sonuçlardır. [ÇSGB'nin 4857 metni](https://www.csgb.gov.tr/Media/crldhi51/4857-say%C4%B1l%C4%B1-i%C5%9F-kanunu.pdf) m.8 ve m.75 dayanak alanında kullanılır; kaynak 2 Ekim'de kontrol edildi. Ürün ekran davranışı hukuki uygunluk veya resmî bildirim kabulü değildir.

Mevcut kaynak sürümüne ait doğru alıştırma ve kişinin gerçek işlem kanıtı birlikte varsa uyarlanan anlatımın aynı adım/sürüm kuralı bu derste de işler. İşlem geçmişi ve kişisel veri dışa aktarımı yalnız kayıt sahibinin kanıtını içerir; bu yeterlilik onayı değildir.

## Doğrulama ve kalan iş

SQL senaryoları ilk kanıtın korunması, sonraki kararın kendi kaynağı, eşzamanlı eski sürüm, no-op, tenant/rol, retire sonrası geçmiş, Down engeli, işe alma aday sürümü, ayrı retry isteği ve aktif lease'i kapsar. Zorlanmış öğrenme yazım hatası aşama, işe alma ve yeniden kuyruklama işlemlerini geri alır. Arayüz senaryoları son teyit, 409 sonrası taslak/kaynak, bekleyen hesap ayrımı ve yeniden denemede ikinci personel gönderilmemesini doğrular. Son tam kontrol sonuçları Git senkronizasyon raporundadır.

Tarayıcı provası test API'siyle geniş ve 390×844 ekranlarda aşama, işe alma ve hesap yeniden deneme kaynaklarını/teyitlerini inceler; gerçek insan veya uzman kabulü değildir.

T02 kaynak/gerçek sonuç kapsamı tamamlandı. Kalan teknik başlıklar T01 ve T03–T08, toplam yedidir; [güncel matris](2026-10-02-ik-rehberi-olagan-islem-matrisi.md) kapanış ölçütüdür. Sonraki paket T01 şirket yardımcı işlemleri ve erişim hazırlığıdır. İletişim, tarihsel huni, genel tekrar/cihaz devamı, uzman/ayrı editör yayını, deneyimsiz kullanıcı kabulü ve Turhost hedef ortam doğrulaması açık kalır. Tüm öğrenme kapsamı tamamlanmadan yayın yapılmaması kararı sürer.
