# Aday ilk kararının öğrenme kanıtı

28 Eylül 2026 · İşe alım yolculuğunun ilk uygulama dilimi

Ana plan: [İK rehberi ve öğrenme yolculuğu](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md).

`CandidateReviewEvidence`, İK yöneticisinin bir adayı `Yeni` aşamasından farklı bir pipeline aşamasına geçirdiği başarılı işlemle aynı `SaveChanges` içinde yazılır. Aday oluşturma, ayrıntı açma, aynı durumu tekrar gönderme ve yer imi seçme kanıt üretmez. Aynı kullanıcı ve aday için ilk geçiş tek sayılır; aday yeniden `Yeni` aşamasına dönüp başka karar verilse bile sayaç artmaz. Başka kullanıcının işlemi oturum sahibinin yolculuğuna eklenmez.

Kanıt kaydı kiracı, kullanıcı, aday ve ilk karar durumunu taşır; aday silinirse FK cascade ile silinir. `GET /api/guidance/recruitment/journey` yalnız İK yöneticisine kendi kanıt sayısını ve son işlem zamanını verir. `/api/me/data-export` yalnız oturum sahibinin aday kanıtlarını içerir. Yolculuk ekranı bu sayıyı **gerçek ilk aşama kararı** olarak sunar; kararın doğru olduğunu, adayla iletişim kurulduğunu veya bağımsız yeterlilik kazanıldığını iddia etmez.

Bu dilim doğrudan `hire` işleminden, mülakat notundan veya değerlendirme puanından kanıt üretmez. Senaryo sorularının cevapları ve içerik sürümüne bağlı beceri düzeyi de henüz saklanmaz. Bunlar sonraki veri sözleşmesi ve içerik incelemesi gerektirir.
