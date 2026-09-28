# İzin onayı karar alıştırması ve yolculuğu

28 Eylül 2026

`GET /api/guidance/leave-approval/checks/{stepKey}` ve `POST .../attempts`, `Management` kapsamındaki kullanıcıya iki isteğe bağlı karar alıştırması sunar: `review-request` çelişkili talep bilgisini netleştirmeyi, `decision-effect` karar anındaki yeniden kontrolü öğretir. Sorular genel işlem davranışını açıklar; bir izin türünün hukuki koşulunu veya yasal süreyi belirlemez. İlk GET doğru seçeneği açıklamaz. Yanıt sunucuda değerlendirilir ve kullanıcı/tenant/süreç/adım/içerik sürümü başına son seçim, doğruluk ve deneme sayısı saklanır. Hatalı cevap düzeltilebilir; alıştırma gerçek onay veya ret komutu göndermez.

`GET /api/guidance/leave-approval/journey`, yalnız oturum sahibinin mevcut izin taleplerindeki `DecisionByUserId` ve `DecisionAtUtc` alanlarından gerçek karar sayısını ve son zamanı üretir. Alıştırma sayaçları ayrı gösterilir. Gerçek karar sayısı kararın doğru olduğunu veya bağımsız yeterliliği kanıtlamaz. İzin kararı başarılı olunca frontend yolculuk sorgusunu yeniler. Yönetici yolculuk sayfasında bu kartı ve erişebildiği süreç bağlantılarını görür; yalnız İK yöneticisine açık işe giriş ve aday verilerini sorgulamaz.

İzin türünün koşulları, belge gerekleri ve mevzuat dayanakları için onaylı, sürümlü içerik kataloğu henüz yoktur. Bu alıştırmalar onun yerine geçmez; ilgili API ihtiyacı [eksikler listesinde](2026-09-25-ik-rehberi-backend-api-eksikleri.md) tutulur.
