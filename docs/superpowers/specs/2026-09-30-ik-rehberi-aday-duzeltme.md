# Aday Kaydını Gerekçeli Düzeltme

İK yöneticisi işe alınmamış adayın adını, başvurulan rolünü, pozisyon bağlantısını, elle verilen 0–100 puanını, lokasyonunu, deneyim yılını ve başvuru özetini düzeltir. `PUT /api/candidates/{id}` beklenen aday sürümünü ve boş olmayan gerekçeyi zorunlu tutar. Önce/sonra değerler, gerekçe, aktör ve zaman `GET /api/candidates/{id}/corrections` ile okunur. Ekran kaydetmeden önce karşılaştırma sunar.

Kullanıcı kararları:

- İşe alınmış adayın başvuru kaydı kilitlidir; hata çalışan kartındaki ayrı düzeltme akışında değerlendirilir. Bu işlem çalışan verisini değiştirmez veya işe alımı geri almaz.
- Rol veya pozisyon değişince mevcut aşama, mülakat notları, değerlendirmeler, yetenekler, deneyim satırları ve başvuru tarihi korunur. Mevcut kanıtların yeni role uygunluğunu kontrol eden açık teyit gerekir. Sistem yeni role uygunluk sonucu veya yeni puan üretmez.

Yeni bağlı pozisyonun incelenen sürümü de gönderilir; hedef açık ve kalan kadrosu en az bir olmalıdır. Mevcut kapalı pozisyon bağlantısı ad düzeltmesi gibi işlemlerde korunabilir veya teyitle kaldırılabilir. Bağlantı değiştirmek kadro tüketmez; kadro yalnız işe alımda azalır.

Aday düzeltmesi, aşama değişimi, not/değerlendirme ekleme ve işe alma aynı aday kilidini kullanır; gerçek mutasyon aday sürümünü artırır. Pozisyon değişimi önceki ve yeni pozisyon kilitlerini de alır. Eski form 409 alır; arka plan yenilemesi taslağın beklenen sürümünü sessizce yükseltmez. Reddedilen veya değişikliksiz istek düzeltme geçmişi yazmaz. Geçmiş ve kayıt değişikliği aynı transaction içindedir. Mevcut adaylar migration ile sürüm 1 alır.

Düzeltme bir ilk aşama kararı değildir ve öğrenme yolculuğunun gerçek ilk inceleme sayısını artırmaz. Aşama API'sinin ayrıca beklenen sürüm ve karar gerekçesi alması hâlâ açık iştir.

Bu kapsam not/değerlendirme metnini düzenleme, puansız/değerlendirilmedi durumunu modelleme, aday birleştirme/silme/iptal, kalıcı aday–çalışan bağlantısı sorgusu veya yetenek/deneyim satırlarını düzeltme sözleşmesi sunmaz. Bu ihtiyaçlar API eksikleri listesinde izlenir. Ekrandaki açıklamalar işlem rehberidir; mevzuat onayı değildir.
