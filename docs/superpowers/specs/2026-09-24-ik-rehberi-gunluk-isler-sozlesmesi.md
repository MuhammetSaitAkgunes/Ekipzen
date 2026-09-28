# İK rehberi günlük işler — ilk veri sözleşmesi

24 Eylül 2026 · 28 Eylül 2026 bordro ve aday görevleriyle genişletildi

Ana plan: [İK rehberi ve öğrenme yolculuğu](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md).

## Kapsam ve kaynak

`GET /api/guidance/daily-tasks` yalnız yönetim rollerine açıktır. Güncel uygulama dört kayıt türünden okunur:

| Görev | Kaynak durum | Kim görür? | Listeden çıkma ve yeniden açılma |
| --- | --- | --- | --- |
| İzin onayı | Kapsamındaki `pending` izin talebi | İK yöneticisi; ekip kapsamındaki yönetici | Onay/ret sonrası kaybolur; kaynak yeniden bekleyen duruma getirilebilirse aynı anahtarla döner. |
| Belge takibi | `Eksik` veya `Süresi Yaklaşıyor` uyum belgesi | Belgeyi değiştirebilen İK yöneticisi | `İncelemede` veya `Tamamlandı` olunca kaybolur; yeniden açılırsa aynı anahtarla döner. |
| Bordro dönemi | `draft` veya `control` dönem | Dönemi inceleyip onaylayabilen İK yöneticisi | `approved`/`closed` olunca kaybolur; mevcut akış onaylı dönemi yeniden açmaz. |
| Aday ilk incelemesi | `Yeni` aşamasındaki aday | Aday aşamasını değiştirebilen İK yöneticisi | Başka aşamaya geçince kaybolur; `Yeni` aşamasına dönerse aynı anahtarla görünür. |

Görev oluşturma ayrı bir veri yazımı değildir. Her GET mevcut izin, uyum, bordro ve aday sorgularının yetki kapsamını yeniden değerlendirir. `leave-approval:{id}`, `compliance-document:{id}`, `payroll-period:{id}` ve `candidate-review:{id}` anahtarları aynı kaynağın tekrar listelenmesini tekilleştirir. Kullanıcının kartı açması tamamlanma kanıtı sayılmaz; gerçek kaynak durumu belirleyicidir.

Yanıt alanları: `key`, `kind`, `recordId`, `title`, `reason`, `nextAction`, `dueDate`, `dateNote`, `targetPath`. `targetPath` uygulama içindeki göreli yoldur. İzin talebinin başlangıcı kararın son tarihi değildir; bu görevde `dueDate` boş kalır. Belgede son tarih kayıtlı değilse `dueDate` boş ve `dateNote` eksikliği açıklar. Bordro döneminde kaynakta karar son tarihi bulunmadığından `dueDate` boştur; dönem ayı son tarih gibi kullanılmaz. Aday görevinde de `dueDate` boştur; başvuru tarihi karar son tarihi değildir. Mevcut belge son tarihinin hukuki dayanakla doğrulandığı bu görev akışından varsayılmaz.

Ana Sayfa ilk dört görevi, İş Kutusu tam listeyi gösterir. İzin bağlantısı ilgili talebi açık ve üstte, belge bağlantısı ilgili kaydı üstte ve vurgulu gösterir. İzin, belge, bordro dönemi ve aday durum mutasyonları açık görev sorgusunu yeniler. Bordro görevi `periodId`, aday görevi `candidateId` bağlantısıyla ilgili kaydı seçer. Boş liste yalnız bu dört kaynakta bekleyen iş olmadığını ifade eder.

## Sonraki kapsam

Diğer İK süreçleri, güvenilir sorumlu ve son tarih kuralları, şirket takvimi/saat dilimi, görev sıralama ve sayfalama, dış sistem kanıtı ve eksik veriden dolayı değerlendirilemeyen koşullar ayrı dilimlerde eklenir. Bu kapsam yasal süre üretmez, diğer modüllerin tamamlandığını söylemez.
