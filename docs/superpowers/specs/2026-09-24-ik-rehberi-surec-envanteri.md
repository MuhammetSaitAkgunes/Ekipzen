# İK rehberi süreç envanteri — ilk kaynak haritası

24 Eylül 2026 · Durum: Paket 1 çalışma kaydı. Bu tablo API ve mevcut ekranlardan çıkarılmıştır; mevzuat ve şirket politikası içeriği için uzman onayı yerine geçmez.

Ana plan: [`../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md`](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md).

Her alt işlem için rehbere girmeden önce cevaplanacak sorular: Kim başlatabilir? Hangi kayıt önkoşul? Tamamlandığını hangi sunucu durumu kanıtlar? Hata hangi aşamada nasıl düzeltilir? Kullanıcının hangi kararı öğrenmesi gerekir? Uygulama dışındaki adım hangisidir?

| Süreç | Kaynak ve mevcut alt işlemler | Rehberde bağlanacak karar ve kontrol | Henüz çözülmesi gerekenler |
| --- | --- | --- | --- |
| Şirket hazırlığı | `SetupController`: başlangıç bilgisi ve kurulum. `DepartmentsController`: liste/ekle/düzenle/sil. `SettingsController`: şirket, bildirim, güvenlik, logo. `AuthController`: davet kabulü ve parola. | Kurulum durumunu gerçek kayıttan oku; departman olmadan çalışan oluşturma adımını başlatma. Roller ve ayarlar yalnız yetkili kullanıcıya gösterilsin. | Her kurulum adımının kanıtı, hata düzeltmesi ve şirket politikasının sahibi belirlenmeli. Kiracı provizyonu son kullanıcı akışı değildir. |
| İşe alım | `RecruitmentController`: pozisyon, aday, durum, not, değerlendirme, işe alma, başarısız hesap oluşturmayı yeniden deneme, huni. | Adayın çalışan kaydına dönüşümünü tek işlem olarak varsayma; dönüşüm sonucundaki çalışan kimliğini ve hesap oluşturma durumunu doğrula. | Aday aşamalarının karar gerekçeleri ve başarısız dönüşümün iyileştirme yolu envanterlenecek. |
| İşe giriş | `EmployeesController`: çalışan oluşturma, tekil çalışan, evrak, fotoğraf, çalışma temeli ve ücret paketi. `PersonnelProfilePage`/`PersonnelModal`/`DocumentsTab`: form ve belge akışı. | Kimlik, departman, işe giriş tarihi, sözleşme ve çalışma şeklini kontrol et; başarılı `POST /api/employees` yanıtından sonra evrak/ücret adımına geç. | Yasal bildirimlerin uygulama dışı kanıtları, içerik kaynağı ve belge gereklilik matrisi gerekir. İsteğe bağlı sağlık/çalışma uygunluğu kutularında `false` ile “değerlendirilmedi” ayrımı veri modelinde bulunmuyor. |
| Personel yönetimi | `EmployeesController`: güncelleme, içe aktarma önizleme/kayıt, durum, toplu pasife alma, işe dönüş, evrak ve fotoğraf. | Önizleme ile gerçek kaydı ayır; düzenleme ve işe dönüşte hangi geçmişin korunduğunu açıkla. | İçe aktarma ve toplu işlem için satır bazlı düzeltme; eski varsayılanların API üzerinden sessizce kaydedilmesini önleme. |
| İzin ve onay | `LeavesController`: türler, bakiye/defter, talep, iptal, bekleyenler, onay/ret, ekip izinleri. | İzin türünü koşullarla açıkla; bakiye ve çakışmayı göster; onay kararını ve talebin güncel durumunu doğrula. | İzin türü karar ağacı, şirket politikası ile kanuni hak ayrımı, her tür için kaynak ve uzman incelemesi. |
| Mesai ve puantaj | `AttendanceController`: canlı görünüm, kayıt listeleme/oluşturma/güncelleme, özet. | Kayıt ile özetin farkını ve bordroya etkisini göster; eksik ve çelişkili veriyi açıkça belirt. | Çalışma/mesai kurallarının güncel kaynakları, dönem kapanışı ve hatalı kaydın düzeltme matrisi. |
| Bordro | `PayrollController`: dönem, satır güncelleme, kontrol, satır onayı, gönderim, önizleme, ayar taslağı/onayı, pusula. `TerminationCalculationRulesController`: ayrılış hesabı kuralları. | Desteklenen profil ve onay kapılarını mevcut hesaplama motorundan oku. Taslak, önizleme, onay ve gönderimi ayrı durumlar olarak öğret. | Her kalem için doğrulanmış kaynak ve yürürlük dönemi; dış ödeme/bildirim kanıtı; uzman onaylı eğitim içeriği. |
| Uyum ve belgeler | `ComplianceController`: belge, hazır olma görünümü, ekle/düzenle, durum ve sorumlu. `DashboardController`: uyum riski. | Eksik ve süresi yaklaşan kaydı göreve dönüştür; belge ekleme ile yükümlülüğün tamamlanmasını karıştırma. | Belge türü, son tarih ve gerekçe için güncel mevzuat/şirket politikası eşlemesi. |
| İşten ayrılış | `EmployeesController`: ayrılış bağlamı, dosya taslağı, hesap taslağı, tamamlama, geri alma; `TerminationCalculationRulesController`: kural onayı. | Taslak ve tamamlanmayı ayır; geri alma koşulunu gerçek dosya durumundan oku; uzman gerektiğinde işlemi durdur. | Dış bildirim, ödeme, hesap ve istisnaların kaynaklı karar matrisi. Toplu pasife alma ayrılış dosyasının yerine geçmez. |
| Günlük yönetim ve analiz | `ActionsController`: aksiyon, durum, denetim izi, arama. `DashboardController`: genel durum ve risk görünümleri. `HomePage`/`InboxPage`: günün işleri. | Görevi gerçek kayıt ve sorumluya bağla; risk sinyalinin öneri ile doğrulanmış olguyu ayırdığını açıkla. | Mevcut aksiyonun `Due` alanı metin etiketi; güvenilir son tarih ve otomatik görev tekilliği için ayrı sözleşme gerekli. Analizlerin yanlış pozitifleri açıklanmalı. |

## İlk dikey dilim: işe giriş

İlk kod dilimi formun altı bölümüne bağlamsal açıklama ekler. Yeni kayıtta bazı bilinmeyen seçenekler varsayılan olarak doldurulmaz. Sözleşme türü ve çalışma şekli açıkça seçilmeden form gönderilmez. Sunucu çalışan kaydı oluşturduğunu yanıtladığında evrak sekmesine geçilir; kartın oluşması evrakın, ücret paketinin veya dış bildirimin tamamlandığı anlamına gelmez.

Bu dilim öğrenme ilerlemesini veya görev durumunu kalıcı olarak saklamaz. Ortak süreç kataloğu, sunucu tarafından doğrulanan görev üretimi ve kullanıcı bazlı öğrenme kaydı ana plandaki sonraki paketlerdir. Mevzuat dayanağı incelemesi tamamlanmayan kararlar bu dilimde kaynaklı hukuki yönlendirme gibi gösterilmez.

## Paket 1 için kalan doğrulamalar

- [ ] Her alt işlemin mevcut ekranı, API uç noktası ve rolünü tek tek eşle.
- [ ] Gerçek veriyle başlama/tamamlama kanıtı ve görev tekilleştirme anahtarını belirle.
- [ ] Düzenleme, iptal, ters işlem ve dış sistem düzeltmesi matrisini tamamla.
- [ ] İçerik sahibi, mevzuat inceleyen uzman ve kaynak/yürürlük takibini ata.
- [ ] Kullanıcı araştırmasında yeni mezun ve firma sahibiyle dil ve akışları doğrula.
