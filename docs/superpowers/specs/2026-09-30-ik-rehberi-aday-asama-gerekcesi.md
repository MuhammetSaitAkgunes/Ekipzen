# Aday Aşama Kararının Gerekçesi ve Dayanağı

Kullanıcı kararı: aşama değişikliğinde gerekçe zorunludur; mevcut not ve değerlendirmeler karara kullanıcı seçimiyle bağlanır. Kayıtlı dayanak olmadan da gerekçesi açıklanan karar verilebilir. Sistem dayanağın doğruluğunu, hukuki uygunluğu veya adayın işe uygunluğunu otomatik onaylamaz.

`PATCH /api/candidates/{id}/status` hedef `status`, zorunlu `expectedVersion` ve 1–2000 karakterlik `reason` alır. İsteğe bağlı `evidence` listesi en fazla 20 farklı `{kind: note|evaluation, id, version}` referansı içerir. Eski yalnız-status istemcisi 400 alır; sürüm/gerekçe eklemelidir. Hedefler Yeni, Mülakat, Teklif ve Red olarak kalır; işe alma ayrı API'dir.

Ortak aday kilidinde beklenen aday sürümü doğrulanır; seçilen kaynaklar aday/tenant kapsamıyla okunur ve güncel kayıt sürümüyle karşılaştırılır. Gerçek geçişte `CandidateStageDecisions` önceki/sonraki aşama, aday sürüm aralığı, gerekçe, aktör/zaman ve sunucunun okuduğu dayanakların tam snapshot'ını ekler. Aday aşaması/sürümü, geçmiş olayı ve varsa ilk inceleme öğrenme kanıtı aynı transaction içinde yazılır. Sonradan yapılan not/değerlendirme düzeltmesi geçmiş kararın dayanağını değiştirmez.

Eski aday veya dayanak sürümü 409, farklı adayın dayanağı 404, boş/tekrarlanan/geçersiz dayanak veya gerekçe 400 alır. İşe alınmış aday kilitlidir. Güncel sürümle aynı aşamaya istek değişiklik ve karar/öğrenme kanıtı üretmez; eski sürüm aynı aşamayı istese de reddedilir. İlk gerçek Yeni geçişi aynı kullanıcı/aday için önceki tekil öğrenme davranışını korur; dönüş ve tekrar karar bu sayıyı artırmaz.

`CandidateDetailDto.stageDecisions` azalan aday sürümü sırasıyla yeni gerekçeli kararları döndürür. Eski genel geçmiş olaylarının gerekçesi/dayanağı geriye dönük uydurulmaz. Ekran gerekçeyi ve kullanılacak mevcut kayıtları gösterir, değişiklikte teyidi düşürür, kaydetmeden önce aday sürümünü yeniden okur; son kontrol sunucudadır. İncelenen sürüm arka plan yenilemesiyle sessizce yükseltilmez. Geçmiş sekmesi karar anındaki kaynak sürümünü ve içeriğini gösterir.

Aşama değişikliği görüşme randevusu/teklif belgesi/adaya mesaj oluşturmaz. Genel puanı değiştirmez. Günlük ilk-inceleme görevi gerçek aşama durumuna göre kapanır veya yeniden açılır. Açık ihtiyaçlar: karar gerekçesini ayrı düzeltme/iptal akışı, aday iletişim durumunu izleme, karar bekleme süresi/tarihsel akış analizi ve kalıcı aday–çalışan bağlantısı.
