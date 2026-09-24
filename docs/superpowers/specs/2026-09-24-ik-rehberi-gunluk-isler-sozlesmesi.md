# İK rehberi günlük işler — ilk veri sözleşmesi

24 Eylül 2026 · İlk uygulama dilimi

Ana plan: [İK rehberi ve öğrenme yolculuğu](../plans/2026-09-24-ik-rehberi-ve-ogrenme-yolculugu.md).

## Kapsam ve kaynak

`GET /api/guidance/daily-tasks` yalnız yönetim rollerine açıktır. İlk dilim iki kayıt türünden okunur:

| Görev | Kaynak durum | Kim görür? | Listeden çıkma ve yeniden açılma |
| --- | --- | --- | --- |
| İzin onayı | Kapsamındaki `pending` izin talebi | İK yöneticisi; ekip kapsamındaki yönetici | Onay/ret sonrası kaybolur; kaynak yeniden bekleyen duruma getirilebilirse aynı anahtarla döner. |
| Belge takibi | `Eksik` veya `Süresi Yaklaşıyor` uyum belgesi | Belgeyi değiştirebilen İK yöneticisi | `İncelemede` veya `Tamamlandı` olunca kaybolur; yeniden açılırsa aynı anahtarla döner. |

Görev oluşturma ayrı bir veri yazımı değildir. Her GET mevcut izin ve uyum sorgularının yetki kapsamını yeniden değerlendirir. `leave-approval:{id}` ve `compliance-document:{id}` anahtarları aynı kaynağın tekrar listelenmesini tekilleştirir. Kullanıcının kartı açması tamamlanma kanıtı sayılmaz; gerçek kaynak durumu belirleyicidir.

Yanıt alanları: `key`, `kind`, `recordId`, `title`, `reason`, `nextAction`, `dueDate`, `dateNote`, `targetPath`. `targetPath` uygulama içindeki göreli yoldur. İzin talebinin başlangıcı kararın son tarihi değildir; bu görevde `dueDate` boş kalır. Belgede son tarih kayıtlı değilse `dueDate` boş ve `dateNote` eksikliği açıklar. Mevcut belge son tarihinin hukuki dayanakla doğrulandığı bu görev akışından varsayılmaz.

Ana Sayfa ilk dört görevi, İş Kutusu tam listeyi gösterir. İzin bağlantısı ilgili talebi açık ve üstte, belge bağlantısı ilgili kaydı üstte ve vurgulu gösterir. İzin ve belge mutasyonları açık görev sorgusunu yeniler. Boş liste yalnız bu iki kaynakta bekleyen iş olmadığını ifade eder.

## Sonraki kapsam

Diğer İK süreçleri, güvenilir sorumlu ve son tarih kuralları, şirket takvimi/saat dilimi, görev sıralama ve sayfalama, dış sistem kanıtı ve eksik veriden dolayı değerlendirilemeyen koşullar ayrı dilimlerde eklenir. Bu ilk dilim yasal süre üretmez, diğer modüllerin tamamlandığını söylemez.
