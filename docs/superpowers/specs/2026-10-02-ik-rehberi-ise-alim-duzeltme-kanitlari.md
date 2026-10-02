# Ekipzen — işe alım düzeltme ve dayanak öğrenme bağlantıları

2 Ekim 2026. Kullanıcının onayladığı `0cc5b59`, `362c770`, `78ed5d7` commitleri `codex/ik-rehberi-kapsam-sirket-ise-alim` dalından `ekipzen` remote'una gönderildi, `main` fast-forward ilerletildi ve push edildi. İki remote uç `78ed5d70cf0be2937524d22cc91ab820242ebb58` ile doğrulandı. Yeni geliştirme bu uçtan `codex/ik-rehberi-ise-alim-duzeltme-kanitlari` dalında sırasıyla yürütüldü.

## Kapanan alt işlemler

| Sıra / işlem | Mevcut API ve operation key | Kaynak adım |
| --- | --- | --- |
| 1 pozisyon düzelt/kapat | PUT positions/{id}, `position-correct` | recruitment/candidate-create |
| 2 aday temel alan/pozisyon düzelt | PUT candidates/{id}, `candidate-correct` | recruitment/candidate-create |
| 3 görüşme notu ekle | POST candidates/{id}/notes, `candidate-note-create` | recruitment/first-review |
| 4 görüşme notu yeni sürüm | POST notes/{id}/revisions, `candidate-note-revise` | recruitment/first-review |
| 5 ölçüt değerlendirmesi ekle | POST candidates/{id}/evaluations, `candidate-evaluation-create` | recruitment/first-review |
| 6 ölçüt değerlendirmesi yeni sürüm | POST evaluations/{id}/revisions, `candidate-evaluation-revise` | recruitment/first-review |

İstekler isteğe bağlı `guidanceContentRevisionId` taşır. Bu teslim R02/R04/R06/R07'yi bağlar; yeni süreç/adım sayısı eklemez. İlk hazırlık ve ilk inceleme ders taslakları mevcut alanlara göre ayrıntılandırıldı: kadro/pozisyon koşulu, rol uygunluk teyidi, gerçek görüşme kaynağı, ölçüt/puan/azami puan, 4/10 ile 4/5 farkı, eski karar dayanağının korunması, hata sonrası doğru yeni sürüm ve ayrı iletişim sonucu. Mevcut kaynak/dayanak ve uzman kapısı değişmez; taslak içeriği gerçek uzman onayı gibi yayımlanmaz.

Her formda kaynak açıkça ayrı seçilir; pozisyon/aday oluşturma seçimi düzeltmeye, not seçimi değerlendirmeye otomatik taşınmaz. Ön kontrol kaynak veya gerçek işlem kanıtı yazmaz. Kaynaklı başarı kişisel yolculuğu yeniler. Güncel kaydın arka planda okunması düzeltmenin ilk aday/kaynak sürümlerini değiştirmez; sunucu hatasında taslak ve ders seçimi korunur. Başarılı eklemede ilgili kaynak seçimi sıfırlanır; aday değişince iki ekleme kaynağı da temizlenir. İşe alınmış başvuruda ekleme/düzeltme kaynağı seçtirilmez; salt okunur geçmiş ve çalışan kartındaki ayrı süreç korunur.

## Atomik iş kaydı ve kişisel geçmiş

Kaynak, yeni bir paralel iş geçmişi oluşturmak yerine mevcut `PositionCorrections`, `CandidateCorrections`, `InterviewNotes`, `CandidateEvaluations`, `CandidateEvidenceRevisions` kayıtlarında nullable `GuidanceSnapshotJson` olarak saklanır. Değerlendirme ilk kaydında daha önce aktör/zaman bulunmadığından yalnız kaynaklı yeni yazım için nullable `GuidanceUserId` ve `GuidanceOccurredAtUtc` eklenir. Eski kayda aktör/kaynak tahmin edilmez; geçmiş işlem sonradan bağlanamaz.

Mevcut merkezî yayın kilidi tenant kilidinden önce alınır. Güncel/erişilebilir doğru adım kaynağı ve kaynak snapshotı iş/history/sürüm yazımıyla aynı transaction içinde korunur. Eski kaynak/adım dışı kaynak, eski iş sürümü, no-op veya kilitli başvuru başarısız olduğunda yeni kanıt yoktur. Snapshot yazımındaki SQL hatası iş değişikliklerini de geri alır. Kaynaksız eski istemcinin olağan iş/düzeltme kaydı kalır; öğrenme kanıtına dönüşmez.

İlk not/değerlendirme değişmez; düzeltme yeni geçerli sürüm oluşturur. Önceki aşama kararında seçilmiş dayanak snapshotı güncel revizyonla değiştirilmez. Not/değerlendirme eklemenin aktörü ve revizyonun aktörü ayrı kişisel kanıtlardır. Kişisel işlem/dışa aktarım yalnız kendi yazımlarını gösterir; tenant/rol kapsamı genişletilmez.

İşlem DTO'sundaki `recruitmentChanges` aday/pozisyon kimliği, kayıt türü/kimliği, önce/sonra sürüm ve varsa gerekçeyi taşır. Not/değerlendirme düzeltmesinde kaynak kaydın sürümüyle aday sürümü ayrı alanlardır. Pozisyon/aday düzeltmesinde recordId düzeltme geçmişi satırıdır; not/değerlendirme için özgün kaynak kimliğidir. Tarihsel kanıt metni notun kişisel içeriğini veya puanı ikinci bir depoya kopyalamaz. Son kaynak yayından kaldırılırsa eski kanıt ve ilk kaynak sürümü kalır, güncel işareti kalkar.

Mevcut FK/saklama ilişkileri korunur; bu teslim fiziksel silme veya yanlış aday kaydını taşıma/iptal etme yeteneği eklemez. Öğrenme kanıtı bağımsız yeterlilik, aday aşaması/genel puan, işe alma veya dış iletişim onayı değildir.

## Tekrar, sürüm ve görev sınırı

Düzeltmeler mevcut beklenen aday/pozisyon/kaynak sürümleri, gerekçe, rol uygunluk teyidi ve kilit kurallarıyla çalışır. Pozisyon kapatılması bağlı adayı/geçmişi silmez, yeni bağlantı ve işe almayı durdurur. İşe alınmış adayın geçmişi kilitli kalır. Değişmeyen düzeltme 409 alır; yeni öğrenme kanıtı üretmez.

Not/değerlendirme ilk eklemesi mevcut aday kilidinde yazılır fakat beklenen aday sürümü veya kalıcı gönderim makbuzu kazanmadı. Ağ sonucu belirsizse gerçek liste kontrol edilir. Eski kaynak kapısının başarısız tekrar davranışı otomatik ağ tekilliği sayılmaz; tam tekrar/oturum devamı T06'da kapanır. Her not veya puana yapay günlük görev eklenmez; Yeni adayın mevcut ilk inceleme görevi kaynak durumdan yönetilir. Sonraki aşamanın karar/iletişim işi R05/T05'te açıktır.

## Migration ve doğrulama

Tenant migration `20261002083732_AddRecruitmentOperationGuidance` beş tabloda kaynak alanı ve ilk değerlendirmede iki sahiplik alanını ekler. Herhangi bir kaynaklı kayıt varken Down 51000 ile durur. API, migration ve frontend birlikte teslim edilir; üretime migration/publish yapılmadı.

Üç yeni SQL senaryosu altı ayrı operasyon kaynağı, farklı ekleme/revizyon aktörleri, değişmez ilk not/eski karar ölçeği, kişisel dışa aktarım ve tenant sınırı, kaynak emekliliği, eski sürüm yarışı, rol/hired kilidi, migration Down ve altı iş yazımında zorla snapshot hatasının tam geri almasını kapsar. Beş yeni UI senaryosu final kaynak/ilk sürüm, 409 sonrası taslak/seçim, not/eval iki sürüm, ön kontrolde yazım olmaması ve ekleme kaynaklarının ayrılığını doğrular.

Release .NET ve frontend üretim derlemeleri başarılı, .NET sıfır uyarı/hata; model/migration eşleşiyor. Lint dört eski uyarı, yeni uyarı/hata yok. Masaüstü ve 390 × 844 görünümde altı işlem ders seçimi/açıklaması görüntülendi; sayfa genişliği 390, dört modalın genişliği 364; yatay taşma yok. Görseller incelendi. Tarayıcı örnek API/test yayın verisidir, gerçek SQL yazımları entegrasyon testindedir; insan/uzman/erişilebilirlik kabulü değildir. Son toplam test sonuçları ve kesin commit/dosya listesi senkronizasyon raporunda kaydedilir.

Tam SQL entegrasyonu **333 başarılı**, birim **101 başarılı**, frontend **569 başarılı / 95 dosya**. Frontend tam paketi `--maxWorkers=2` ile çalıştı; test süreleri değiştirilmedi. `git diff --check` başarılı. Üretim migrationı, uzman yayını veya canlı publish yapılmadı.

## Sıradaki paket

T02 içinde sırayla R05 sonraki aşama kararlarının öğrenme bağları, ardından R08 işe alma/provizyon sonuçları. [T01–T08](2026-10-02-ik-rehberi-olagan-islem-matrisi.md) ve dış bağımlılık listesi korunur; gerçek uzman/editör yayını, insan kabulü ve hedef ortam kabulü ayrıca bekler. Tüm öğrenme kapsamı tamamlanmadan yayın yapılmama kararı sürer.
