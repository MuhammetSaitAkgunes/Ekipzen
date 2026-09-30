# Onaylı Bordro Zincirleme Telafisi

## Kullanıcı kararı

Bir çalışanın hatalı onaylı bordro ayından sonra onaylı ayları varsa tek ayın parasal uygulaması kilitli kalır. Güvenle doğrulanan etkilenen ayların tamamı, tek uzman onayıyla atomik olarak parasal sürümlere dönüştürülmelidir. Oluşan her ayın uygulama dışı ödeme ve bildirim sonucu ayrı iki kanıt ve günlük işle izlenmelidir.

## Etki dosyası ve uygulama

`approved-impact`, kaynak ayın onaylı sonucu ile önerilen ücret hesabını karşılaştırır. Aynı takvim yılındaki sonraki onaylı satırlar için önerilen matrahı ay sırasıyla taşır ve gelir vergisi/net hesap farkını gösterir. Sonraki yılın satırları listelenir, fakat önceki yılın vergi matrahına dahil edilmez. Satırın kanuni profili, saklı önceki matrahı, onaylı sonucu veya yeniden hesaplanan baz sonucu uyuşmazsa tahmin durur; hiçbir kayıt değişmez. Hesaplanan toplam, yapılmış veya yapılacak gerçek ödeme ya da beyan tutarı değildir.

## Atomik uygulama için sınırlar

- Uygulama sırasında kaynak ücret paketi, uzman dosyası, kaynak satır, bütün sonraki onaylı satırlar, dönem parametreleri ve hesap görüntüleri yeniden okunmalı; onaylanan önizlemeyle aynı oldukları doğrulanmalıdır. Eski önizleme 409 ile reddedilmelidir.
- Kaynak ay ve etkilenen onaylı aylar tek veritabanı transaction'ı içinde, deterministik sırayla kilitlenmelidir. Bir ayın kontrolü başarısızsa hiçbir ay için yeni parasal sürüm oluşmamalıdır.
- Her ayın ilk onaylı sonucu korunmalı; önce/sonra görüntüsü, uzman referansı, onaylayan kişi/zaman ve ortak zincir işlem kimliğiyle yeni geçerli sürümler eklenmelidir. Bir ayda zaten ayrı bir parasal sürüm varsa ilk atomik kapsam uygulamayı durdurmalıdır.
- Sonraki ayın yeni `PreviousTaxBase` değeri önceki ayın **geçerli sürümündeki** önceki matrah ve gelir vergisi matrahından türetilmelidir. Açık ayların matrah yenilemesi de bu geçerli sürüm zincirini kullanmalıdır.
- Toplu onay, banka ödemesi veya resmî bildirim gerçekleştirmez. Her ayın yeni sürümü için ödeme ve bildirim kanıtları ayrı ayrı beklemeye açılır; eski kanıtlar değişmez.
- Takvim yılı sınırı, desteklenmeyen kanuni profil, eksik dönem snapshot'ı veya onaylı sonuç ile yeniden hesaplama uyuşmazlığı otomatik parasal uygulamanın dışında kalır ve uzman incelemesi gerektirir.

`POST .../approved-impact/chain/apply`, doğrulanmış etki dosyasının belirtecini, uzman referansını/notunu ve üç açık teyidi ister. Kaynak ay ile aynı takvim yılının sonraki onaylı ayları, tek işlem kimliği ve tek uzman onayı altında atomik olarak sürümlenir. Her sürüm dönem/pusulada geçerlidir; eski onaylı hesap ve ilk onaylı PDF geçmişte korunur. Ekran, oluşan ayları ayrı gösterir ve her ayın ödeme/bildirim kanıt panelini açar. Bayat önizleme, mevcut bir sürüm, güvenilmez hesap veya sonraki yılda onaylı ay bulunması 409 ile uygulamayı durdurur. Ayrı bir sonraki düzeltme/sürüm zinciri ve dış sistemlere otomatik gönderim bu kapsamda değildir.
