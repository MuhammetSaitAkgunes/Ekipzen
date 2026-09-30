# Onaylı Bordroda İkinci Parasal Düzeltme

## Karar ve kapsam

Aynı ücret paketindeki yeni hata için önceki onaylı uzman dosyası değiştirilmez; ayrı dosya açılır. Önceki bordro sürümü geçerli hesap olarak önizlemenin başlangıcıdır. Sonraki onaylı ay varsa bu ilk ikinci-düzeltme kapsamı parasal uygulamayı kilitler; zincirleme yeniden sürümleme ayrı iş olarak kalır.

## Uygulama

Yeni dosyanın önerisi onaylandıktan sonra `approved-impact` geçerli sürümü ve yeni olası hesabı karşılaştırır. Etki dosyası, önceki sürüm kimliği ve uzman teyitleri `POST .../approved-impact/revise` çağrısında kilit altında yeniden doğrulanır. Yeni sürümün `BeforeJson` görüntüsü önceki sürümün `AfterJson` görüntüsüdür; `BasedOnRevisionId` bağı ve artan sürüm numarası geçmişi açıkça kurar. Özgün onaylı sonuç, eski sürümler ve kanıtları değişmez. Yalnız yeni sürüm dönem/pusulada geçerli olur; sonraki açık ayın kümülatif matrahı bu sürümü kullanır. Eski dış işlem kanıtı salt okunur kalır, yeni sürümün ödeme ve bildirim işleri ayrı yeniden açılır. Bu işlem banka ödemesi veya resmî bildirim yapmaz.

Yeni uzman dosyası ve önceki dosyalar personel ekranında geçmiş olarak görünür. Yeni dosya açılırken önceki öneri başlangıç değeri olabilir; kullanıcı yeni tutarı, kaynak referansını ve hata gerekçesini ayrıca kaydeder. Onaylanan her dosya değişmez.

Yeni dosya açıldıktan sonra eski dosya yeni bordro veya ayrılış parasal uygulaması başlatamaz. Eski dosya ve ona bağlı mevcut parasal sürümlerin dış işlem kanıtları geçmişte erişilir; yeni dosyanın açılması, hâlâ geçerli eski parasal sürümün dış işlemini otomatik olarak tamamlamaz.
