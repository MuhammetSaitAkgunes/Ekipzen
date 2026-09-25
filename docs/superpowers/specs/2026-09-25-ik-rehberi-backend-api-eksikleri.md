# İK rehberi — backend API eksikleri

25 Eylül 2026 · Frontend geliştirmesi sırasında doğrulanan bağımlılıklar

Ana plan: [İK rehberi ve öğrenme yolculuğu](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md). Bu liste, frontend dilimleri tamamlandıktan sonra yapılacak backend tasarımı ve geliştirmesi içindir. Mevcut bir API'nin varlığını veya bir işin tamamlandığını varsaymaz. Her madde uygulama öncesinde güncel kod, yetki ve veri yaşam döngüsüyle yeniden doğrulanmalıdır.

## Kodda doğrulanan eksikler

| Alan | Mevcut sözleşme ve kaynak | Eksik davranış / kullanıcı etkisi | Backend çalışmasında karara bağlanacaklar |
| --- | --- | --- | --- |
| Çalışan evrakı düzeltme | `EmployeesController` çalışan evrakında yalnız listeleme, yükleme ve indirme sunuyor; `Features/Employees/Files` altında silme/değiştirme komutu yok. | Yanlış kişiye, türle veya dosyayla yüklenen evrak mevcut ekrandan geri alınamıyor. Frontend yüklemeden önce kontrol ettiriyor ve sınırı söylüyor. | Yetkili silme veya sürümlü değiştirme; dosya depolama temizliği, denetim izi, başka kayıtlardaki referanslar ve kişisel veri yaşam döngüsü. |
| Ücret paketi düzeltme | `EmployeesController` ücret paketi için GET çalışma temeli ve POST yeni paket sunuyor. `SaveEmployeeCompensationPackageCommand` aynı yürürlük tarihini reddediyor ve yeni kayda göre dönem sınırlarını değiştiriyor. | Yanlış tutar, tarih veya kaynak referansıyla kaydedilmiş paketin güvenli düzeltme uç noktası yok. Yeni paket eklemek aynı tarihteki hatayı düzeltmez. | Geçmişi koruyan düzeltme/iptal sözleşmesi; bordro ve ayrılış hesabına etkisi, kilitli dönem, denetim izi, yetki ve eşzamanlılık. |
| Adaydan çalışan oluştururken sözleşme türü | HireCandidateCommand yalnız departman, e-posta, ünvan ve işe giriş tarihi alıyor; Employee.ContractType varsayılanı belirsiz süreli ve aynı değer yeni istihdam dönemine yazılıyor. | Belirli süreli sözleşmeyle güvenilir dönüşüm yapılamıyor. Frontend belge türünü sorup bu durumda gönderimi durduruyor. | Sözleşme türünü zorunlu ve doğrulanmış istek alanı yapma; personel ile istihdam dönemi tutarlılığı, eski istemciler ve testler. |
| İşe alınan adayın çalışan bağlantısı ve düzeltmesi | HireResultDto oluşan employeeId değerini döndürüyor, fakat CandidateDetailDto bu ilişkiyi döndürmüyor. İşe alınan adayın pipeline durumu değiştirilemiyor; dönüşümü geri alma ucu yok. | Sayfa yenilendiğinde adaydan çalışan kartına doğrudan geçiş kayboluyor. Hatalı dönüşüm için güvenli, izlenebilir düzeltme akışı sunulamıyor. | Kalıcı aday–çalışan bağlantısı ve yetkili sorgu; geri alma/düzeltmenin çalışan, izin, hesap provizyonu ve pozisyon kontenjanına etkisi. |
| Günlük işler kapsamı | `GetDailyGuidanceTasksQuery` yalnız bekleyen izin onayı ve eksik/süresi yaklaşan uyum belgesini üretiyor. | Diğer süreçlerin işleri günlük rehberde görünmüyor; boş liste bütün İK işlerinin bittiği anlamına gelmiyor. | Her kaynak için gerçek tetikleyici, sorumlu rolü, kararlı anahtar, güvenilir son tarih ve kaynak durumundan kapanma; sırayla genişletme. |
| Öğrenme kanıtı ve süreç ilerlemesi | `GuidanceController` işe giriş yolculuğu/yer imi ve çalışan oluşturma kanıtı sunuyor. Yer imi yalnız konum; uygulama kanıtı yalnız çalışan oluşturma işlemiyle yazılıyor. | Diğer süreçlerde kullanıcı ve beceri bazlı kanıt, soru sonucu, yardım seviyesi ve kalıcı ilerleme API'si yok. | Gerçek işlemden üretilen kanıt, içerik sürümü, kullanıcı/tenant sınırı, veri dışa aktarma ve silme; ekran gezintisini başarı saymama. |
| Onaylı rehber içeriği | Guidance API'sinde sürümlü süreç/içerik kataloğu, kaynak ve inceleme durumu uç noktası yok. | Mevzuat dayanağı, yürürlük ve uzman incelemesi doğrulanmış içerik bütün ekranlarda sunulamıyor. Frontend geçici işlem açıklamalarını hukuki hüküm olarak sunmuyor. | Taslak/inceleme/onay/yayından kaldırma; resmî kaynak, hüküm, koşul, yürürlük, sürüm ve inceleyen bilgisi; yetki ve geçmiş sürümler. |

## Sözleşmesi ayrıca tasarlanacak bağımlılıklar

- İşe girişin belge, ücret ve uygulama dışı adımlarına ait tamamlanma kanıtı: mevcut `PersonnelEntryFollowUp` yalnız kayıtları okur; dış bildirimin yapıldığını doğrulamaz. Hangi adımın kullanıcı beyanı, hangisinin sunucu kanıtı olacağı netleştirilmeli.
- Eksik bilgiye dayalı günlük görevler ve tüm süreçlerin düzeltme matrisi: süreç envanteri genişledikçe API ihtiyacı somut alt işlemle eşlenmeli. Her eksik için otomatik mutasyon varsayılmamalı.

Bu dosya backend iş kuyruğudur. Frontend tarafındaki rehber, yalnız mevcut API'nin doğruladığı durumları tamamlandı diye gösterecek; burada listelenen uç noktaları varmış gibi çağırmayacaktır.
