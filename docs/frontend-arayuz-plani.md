# Ekipzen çalışma alanı için frontend planı

Kapsam, giriş sonrasında kullanılan çalışma alanıdır. Tasarım mevcut veriler, roller ve işlemler üzerine kurulacak.

## Birlikte seçilen tasarım

- Ana sayfayı **Bekleyen işler** listesi açar. Rolün görebildiği öncelikli birkaç kayıt ayrı satırlarda gösterilir. Satır ilgili kaydı açar. **Tümünü gör** tam listeye götürür.
- Listenin hemen altında role uygun iki veya üç metinli kısayol bulunur. İK için örnekler **Çalışan ekle** ve **Puantajı aç**, çalışan için **İzin talebi oluştur** ve **Bordrolarım**. Son seçim mevcut erişim yetkilerine göre yapılır.
- Rehber içeriği yalnızca **Rehber** düğmesine basılınca açılır. Masaüstünde yan panel, telefonda ekran genişliğinde bir görünüm kullanılır. Kapatınca arama, filtreler, form değerleri ve klavye odağı korunur. Doğrulama hataları, işlem sonuçları ve kayda özel uyarılar ilgili alanın yanında görünür.
- Sol menü koyu petrol mavisi, çalışma zemini sıcak taş rengi, ana metin koyu lacivert olur. Ana işlemlerde petrol tonu kullanılır. Aciliyet ve durum hem kısa metinle hem renkle belirtilir.

## Uygulama sırası

1. **Ortak görünüm ve ana sayfa.** Yeni renkleri uygulama kabuğuna uygula. Günlük takip ve açık aksiyon kaynaklarını inceleyip aynı kaydı iki kez göstermeyen bir liste oluştur. Kayıt hedefini mevcut kimlik ve izinlerden çöz. Öncelik ve son tarih için mevcut veriyi kullan. Özet sayıları ve ekip bilgilerini ilgili ekranlardan erişilebilir tut. Bekleyen iş yoksa tek satırlık mesajın ardından kısayolları göster. Veri yüklenirken veya alınamadığında ayrı durum göster.
2. **Çalışanlar ile örnek hizmet ekranı.** Başlık ve ana işlemden hemen sonra arama, filtreler ve ilk çalışan satırı gelsin. Excel içe aktarma ikincil işlem olarak erişilebilir olsun. Mevcut dersleri ve kayıt bulma yardımını bağlama uygun rehber paneline taşı. Sonuç bulunamadığında filtre temizleme işlemini aramanın yanında sun.
3. **Diğer hizmetler.** Aynı düzeni puantaj, izinler, evrak, bordro ve ilgili diğer çalışma ekranlarına uygula. Her ekranda kullanıcının asıl işini öne al. Rehberin tekrar eden satır düğmelerini ve açıklamalarını gözden geçir. Kayda özel rehber bağlantılarını, kaynak sürümü seçimini ve işlem için gereken onayları koru. Gelen kutusunda gerçek işleri rehber içeriğinden ayır.
4. **Kullanım kontrolü.** İK, yönetici ve çalışan rolleriyle girişten ilgili kayda ulaşmayı ve her kısayolun doğru hedefi açmasını doğrula. Masaüstü ve telefonda ilk görünümü, klavyeyle paneli açıp kapatmayı, arama ve form durumunun korunmasını kontrol et. Açık ve koyu temada metin, odak ve durum renklerinin okunabilirliğini kontrol et. İlgili frontend testlerini ve üretim derlemesini çalıştır.

İlk görsel inceleme ana sayfa ve Çalışanlar ekranı üzerinde yapılacak. Bu iki ekranda iş listesi, ana işlem ve rehber erişimi birlikte değerlendirildikten sonra düzen diğer hizmetlere yayılacak.
