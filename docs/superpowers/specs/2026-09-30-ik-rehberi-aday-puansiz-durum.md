# Aday genel puanında puansız durum

Yeni aday için genel puanın dayanağı henüz yoksa puan alanı boş bırakılarak gerçek başvuru kaydedilebilir. “Henüz puan verilmedi” durumu başvurunun bütünüyle incelenmediği anlamına gelmez; mülakat notları ve ölçüt değerlendirmeleri ayrı kayıtlardır.

`Candidate.Score`, oluşturma isteği, liste/detay DTO'ları ve temel düzeltmenin puan alanı nullable'dır. Oluşturmada eksik/null puan null olarak saklanır; açıkça 0 veya 100 verilebilir. Verilen puan 0–100 tam sayı olmalıdır. Eski sayısal puanlar, özellikle 0 değerleri, geriye dönük yorumlanmaz veya null'a çevrilmez.

İşe alınmamış adayda mevcut sürümlü ve gerekçeli temel düzeltme puanı null, 0 veya başka geçerli sayıya dönüştürebilir. Önce/sonra snapshot'ları null–0 ayrımını korur. Başka alanı düzeltmek puansız durumu sessizce 0'a dönüştürmez. Mevcut kilit, aday/pozisyon sürümleri, rol uygunluk teyidi ve işe alınmış aday kısıtı sürer. Puan değişimi aşamayı, ölçüt puanlarını veya öğrenme işlem kanıtını değiştirmez.

Oluşturma önizlemesi, liste, detay ve gerekçeli düzeltme geçmişi puansız durumu sayısal 0'dan ayırır. Not/değerlendirme eklemek genel puanı otomatik doldurmaz; kullanıcı bu puanı bağımsız girer. Genel puan dayanağı hâlâ yapılandırılmış bir kaynak ilişkisi değildir; düzeltmede kullanıcı gerekçesi vardır.

Aday oluşturma alıştırmasının artık eski “puan zorunlu” açıklaması yerine boş puanla kayıt davranışını anlatması nedeniyle recruitment alıştırma içerik sürümü 2'ye çıkarılır. Önceki sürüm cevapları korunur; yeni sürüm yanıtı ve yolculuk sayıları eski cevabı başarı olarak devralmaz. Gerçek ilk inceleme kanıtı değişmez.

Migration yalnız Candidates.Score alanını nullable yapar. Güncel puanı veya temel düzeltme geçmişinde puanı null olan kayıt varken geriye migration açık hata ile durur; null değerleri 0'a dönüştürerek anlamı kaybettirmez. Uygulama sürümü geri alınmadan önce yeni nullable yanıtları okuyamayan eski istemci/backend uyumluluğu ayrıca değerlendirilmelidir.

Kalan ihtiyaçlar: genel puana yapılandırılmış dayanak, aday mükerrer/iptal ve birleştirme, yetenek/deneyim satırlarının düzeltmesi, iletişim ve tarihsel aşama analizi.
