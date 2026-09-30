# Mülakat Notu ve Değerlendirme Düzeltmesi

İK yöneticisi işe alınmamış adayın mevcut mülakat notunun türünü/metnini veya değerlendirmesinin ölçütünü/puanını/azami puanını gerekçeli yeni sürümle düzeltir. Kullanıcı kararı gereği ilk kaynak satırı değişmez; önceki sürümler korunur ve son sürüm güncel kabul edilir. İşe alınmış adayın başvuru geçmişine bu ekrandan yeni not/değerlendirme eklenemez veya düzeltme yapılamaz; geçmiş okunabilir.

API uçları:

- `POST /api/candidates/{id}/notes/{noteId}/revisions`: `expectedCandidateVersion`, `expectedRecordVersion`, `noteType`, `text`, `reason`.
- `POST /api/candidates/{id}/evaluations/{evaluationId}/revisions`: aynı iki sürüm, `criterion`, `score`, `maxScore`, `reason`.
- Her iki yolun GET karşılığı değişmez `original` ve azalan sırada `revisions` döndürür; her sürüm önce/sonra, gerekçe, aktör/zaman ve aday sürüm aralığını içerir.

İlk kayıt sürüm 1 sayılır. `CandidateEvidenceRevisions` kaynak not veya değerlendirmeye bağlı eklemeli tablodur; bir satır yalnız bir kaynak taşıyabilir. Kaynak başına sürüm numarası tekildir. Düzeltme aynı aday kilidinde iki beklenen sürümü doğrular; aday sürümünü bir artırır, yeni kanıt sürümünü ve aday geçmişi olayını aynı transaction içinde kaydeder. Yanlış adayın kaynak kimliği 404, eski sürüm/işe alınmış aday/değişikliksiz istek 409, hatalı alan veya boş gerekçe 400 alır. İK dışındaki roller erişemez; tenant filtreleri kaynak ve sürüm sorgularında uygulanır.

`CandidateDetailDto.notes/evaluations` güncel sürüm değerlerini ve kayıt sürümünü döndürür. Notun ilk yazarı ve oluşturulma zamanı korunur; düzeltme yapan kişi/zaman ayrı geçmiştedir. Kaynak satırın eski metni veya puanı doğrudan güncellenmez. Tekrar düzeltme önceki geçerli sürümden başlar. Düzeltme adayın genel puanını, aşamasını, başvuru tarihini veya ilk inceleme öğrenme kanıtını değiştirmez.

Ekran mevcut kayıt üzerinden formu açar, gerekçe ister, önce/sonra önizleme sunar ve ilk kayıt/geçmişi gösterir. Taslak ilk aday ve kayıt sürümünü korur; arka plan yenilemesi eski taslağı yeni sürümle kaydetmez. Çakışmada önizleme düşer, alanlar korunur ve kullanıcı güncel kayda dönerek yeniden açabilir. Alan değişimi önceki önizlemeyi geçersiz kılar.

Kapsam dışındaki ihtiyaçlar: yanlış adaya ait not/değerlendirmeyi iptal etme veya taşıma, fiziksel silme ve saklama süresi, değerlendirme ölçüt kataloğu, yapılandırılmış dayanak notu bağlantısı, aşama kararını otomatik yeniden değerlendirme. Not düzeltmesi ilk yazarın imzası veya mevzuat uygunluğu onayı anlamına gelmez.
