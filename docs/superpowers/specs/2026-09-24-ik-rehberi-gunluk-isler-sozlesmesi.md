# İK rehberi günlük işler — ilk veri sözleşmesi

24 Eylül 2026 · 28 Eylül 2026 bordro, aday ve ücret hazırlığı görevleriyle genişletildi

Ana plan: [İK rehberi ve öğrenme yolculuğu](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md).

## Kapsam ve kaynak

`GET /api/guidance/daily-tasks` yalnız yönetim rollerine açıktır. Güncel uygulama beş kayıt türünden okunur:

| Görev | Kaynak durum | Kim görür? | Listeden çıkma ve yeniden açılma |
| --- | --- | --- | --- |
| İzin onayı | Kapsamındaki `pending` izin talebi | İK yöneticisi; ekip kapsamındaki yönetici | Onay/ret sonrası kaybolur; kaynak yeniden bekleyen duruma getirilebilirse aynı anahtarla döner. |
| Belge takibi | `Eksik` veya `Süresi Yaklaşıyor` uyum belgesi | Belgeyi değiştirebilen İK yöneticisi | `İncelemede` veya `Tamamlandı` olunca kaybolur; yeniden açılırsa aynı anahtarla döner. |
| Ücret hazırlığı | Bugün başlayan/açık istihdam dönemi olan etkin çalışanda bugün yürürlükte ücret paketi yok | Paketi kaydedebilen İK yöneticisi | Yürürlükte paket kaydedilince kaybolur; yalnız ileri tarihli paket varsa açık kalır. Çalışan pasif olursa görünmez. |
| Bordro dönemi | `draft` veya `control` dönem | Dönemi inceleyip onaylayabilen İK yöneticisi | `approved`/`closed` olunca kaybolur; mevcut akış onaylı dönemi yeniden açmaz. |
| Aday ilk incelemesi | `Yeni` aşamasındaki aday | Aday aşamasını değiştirebilen İK yöneticisi | Başka aşamaya geçince kaybolur; `Yeni` aşamasına dönerse aynı anahtarla görünür. |

Görev oluşturma ayrı bir veri yazımı değildir. Her GET mevcut izin, uyum, çalışan/ücret, bordro ve aday kayıtlarının yetki kapsamını yeniden değerlendirir. `leave-approval:{id}`, `compliance-document:{id}`, `employee-compensation:{id}`, `payroll-period:{id}` ve `candidate-review:{id}` anahtarları aynı kaynağın tekrar listelenmesini tekilleştirir. Kullanıcının kartı açması tamamlanma kanıtı sayılmaz; gerçek kaynak durumu belirleyicidir.

Yanıt alanları: `key`, `kind`, `recordId`, `title`, `reason`, `nextAction`, `dueDate`, `dateNote`, `targetPath`. `targetPath` uygulama içindeki göreli yoldur. İzin talebinin başlangıcı kararın son tarihi değildir; bu görevde `dueDate` boş kalır. Belgede son tarih kayıtlı değilse `dueDate` boş ve `dateNote` eksikliği açıklar. Bordro döneminde kaynakta karar son tarihi bulunmadığından `dueDate` boştur; dönem ayı son tarih gibi kullanılmaz. Aday görevinde de `dueDate` boştur; başvuru tarihi karar son tarihi değildir. Ücret hazırlığında `dueDate` boştur; işe giriş tarihi karar son tarihi sayılmaz. Mevcut belge son tarihinin hukuki dayanakla doğrulandığı bu görev akışından varsayılmaz.

Ana Sayfa ilk dört görevi, İş Kutusu tam listeyi gösterir. İzin bağlantısı ilgili talebi açık ve üstte, belge bağlantısı ilgili kaydı üstte ve vurgulu gösterir. İzin, belge, bordro dönemi, aday durumu ve ilgili çalışan/ücret mutasyonları açık görev sorgusunu yeniler. Bordro görevi `periodId`, aday görevi `candidateId` bağlantısıyla ilgili kaydı seçer; ücret görevi doğrudan çalışanın mali sekmesini açar. Boş liste yalnız bu beş kaynakta bekleyen iş olmadığını ifade eder.

## Sonraki kapsam

Diğer İK süreçleri, güvenilir sorumlu ve son tarih kuralları, şirket takvimi/saat dilimi, görev sıralama ve sayfalama, dış sistem kanıtı ve eksik veriden dolayı değerlendirilemeyen koşullar ayrı dilimlerde eklenir. Bu kapsam yasal süre üretmez, diğer modüllerin tamamlandığını söylemez.
